#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Tailviewer.Api;
using Tailviewer.Settings;

namespace Tailviewer.Core
{
    /// <summary>Discovers folder members using the legacy watcher and semicolon pattern rules.</summary>
    public sealed class FolderSources : IDisposable
    {
        private readonly IFilesystem _filesystem;
        private readonly IFilesystemWatcher _watcher;
        private readonly ITaskScheduler _scheduler;
        private readonly IPeriodicTask _task;
        private readonly DataSource _settings;
        private readonly Func<string, bool> _matchesFile;
        private readonly object _sync = new object();
        private DataSource[] _sources = Array.Empty<DataSource>();
        private bool _disposed;
        private string _error;
        private int _unfilteredFileCount;

        /// <summary>Watches an existing folder configuration without changing its persisted identity.</summary>
        public FolderSources(IFilesystem filesystem, ITaskScheduler scheduler, DataSource settings)
        {
            _filesystem = filesystem ?? throw new ArgumentNullException(nameof(filesystem));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            ArgumentNullException.ThrowIfNull(settings);
            _settings = settings.Clone();
            SourceConfiguration.Validate(new[] { _settings });
            _settings.LogFileFolderPath = Path.GetFullPath(_settings.LogFileFolderPath);
            var patterns = (_settings.LogFileSearchPattern ?? "").Split(';')
                .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray();
            if (patterns.Length > 0 && patterns.All(pattern => pattern.StartsWith("ext:", StringComparison.Ordinal)))
            {
                var extensions = patterns.Select(pattern => pattern.Substring(4)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                _matchesFile = name => extensions.Contains("*") || extensions.Contains(Path.GetExtension(name));
            }
            else
            {
                // Preserve legacy case-sensitive, unanchored '*' matching; '?' stays literal.
                var filter = new Regex(string.Join("|", patterns.Select(value => Regex.Escape(value).Replace("\\*", ".*?"))),
                    RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
                _matchesFile = filter.IsMatch;
            }
            _watcher = _filesystem.Watchdog.StartDirectoryWatch(_settings.LogFileFolderPath,
                TimeSpan.FromMilliseconds(500), null, _settings.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            Refresh();
            _task = scheduler.StartPeriodic(Refresh, TimeSpan.FromMilliseconds(500), "Folder source status");
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
                lock (_sync) return _error;
            }
        }

        private void Refresh()
        {
            DataSource[] sources = Array.Empty<DataSource>();
            string error = null;
            var count = 0;
            // Filesystem calls stay outside the publication lock so cached UI reads cannot block on I/O.
            try
            {
                if (!_filesystem.DirectoryExists(_settings.LogFileFolderPath)) error = "Folder is unavailable.";
                else
                {
                    var files = _watcher.Files.ToArray();
                    count = files.Length;
                    sources = files.Where(file => _matchesFile(file.Name))
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
                }
            }
            catch (RegexMatchTimeoutException) { sources = null; error = "Folder pattern took too long to evaluate."; }
            catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
            { sources = null; error = "Folder could not be read."; }
            lock (_sync)
            {
                if (_disposed) return;
                if (sources == null) { sources = _sources; count = _unfilteredFileCount; }
                if (_error == error && _unfilteredFileCount == count &&
                    _sources.Select(source => source.File).SequenceEqual(sources.Select(source => source.File))) return;
                _sources = sources;
                _unfilteredFileCount = count;
                _error = error;
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
            _scheduler.StopPeriodic(_task);
            _watcher.Dispose();
        }
    }
}
#endif
