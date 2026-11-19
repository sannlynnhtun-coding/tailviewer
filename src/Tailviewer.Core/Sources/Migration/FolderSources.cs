#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Tailviewer.Api;
using Tailviewer.Settings;

namespace Tailviewer.Core
{
    /// <summary>Discovers folder members using the legacy watcher and semicolon pattern rules.</summary>
    public sealed class FolderSources : IDisposable
    {
        private readonly IFilesystem _filesystem;
        private readonly IFilesystemWatcher _watcher;
        private readonly DataSource _settings;
        private readonly Regex _filter;
        private readonly object _sync = new object();
        private DataSource[] _sources = Array.Empty<DataSource>();
        private bool _disposed;
        private string _error;
        private int _unfilteredFileCount;

        /// <summary>Watches an existing folder configuration without changing its persisted identity.</summary>
        public FolderSources(IFilesystem filesystem, DataSource settings)
        {
            _filesystem = filesystem ?? throw new ArgumentNullException(nameof(filesystem));
            ArgumentNullException.ThrowIfNull(settings);
            _settings = settings.Clone();
            SourceConfiguration.Validate(new[] { _settings });
            _settings.LogFileFolderPath = Path.GetFullPath(_settings.LogFileFolderPath);
            var patterns = (_settings.LogFileSearchPattern ?? "").Split(';')
                .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim());
            // Preserve legacy case-sensitive, unanchored '*' matching; '?' stays literal.
            _filter = new Regex(string.Join("|", patterns.Select(value => Regex.Escape(value).Replace("\\*", ".*?"))),
                RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
            _watcher = _filesystem.Watchdog.StartDirectoryWatch(_settings.LogFileFolderPath,
                TimeSpan.FromMilliseconds(500), null, _settings.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            _watcher.Changed += Refresh;
            Refresh();
        }

        /// <summary>Notifies consumers when folder membership or discovery status changes.</summary>
        public event Action Changed;

        /// <summary>The observed file count before the legacy pattern filter.</summary>
        public int UnfilteredFileCount
        {
            get { lock (_sync) return _unfilteredFileCount; }
        }

        /// <summary>Returns independent members in deterministic path order.</summary>
        public IReadOnlyList<DataSource> Sources
        {
            get { lock (_sync) return _sources.Select(source => source.Clone()).ToArray(); }
        }

        /// <summary>Reports an unavailable folder or bounded pattern evaluation failure.</summary>
        public string Error
        {
            get
            {
                lock (_sync) return !_filesystem.DirectoryExists(_settings.LogFileFolderPath)
                    ? "Folder is unavailable." : _error;
            }
        }

        private void Refresh()
        {
            lock (_sync)
            {
                if (_disposed) return;
                try
                {
                    var files = _watcher.Files.ToArray();
                    _unfilteredFileCount = files.Length;
                    _sources = files.Where(file => _filter.IsMatch(file.Name))
                        .OrderBy(file => file.FullPath, StringComparer.Ordinal).Select(file =>
                        {
                            var path = Path.GetFullPath(file.FullPath);
                            var key = OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path;
                            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(_settings.Id.Value + "/" + key));
                            return new DataSource(path)
                            {
                                Id = new DataSourceId(new Guid(hash.AsSpan(0, 16))),
                                ParentId = _settings.Id,
                                FollowTail = _settings.FollowTail
                            };
                        }).ToArray();
                    _error = null;
                }
                catch (RegexMatchTimeoutException) { _error = "Folder pattern took too long to evaluate."; }
                catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
                { _error = "Folder could not be read."; }
            }
            Changed?.Invoke();
        }

        /// <summary>Stops discovery and releases the owned directory watch.</summary>
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
            }
            _watcher.Changed -= Refresh;
            _watcher.Dispose();
        }
    }
}
#endif
