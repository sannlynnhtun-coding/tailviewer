#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Tailviewer.Api;
using Tailviewer.Settings;

namespace Tailviewer.Core
{
    /// <summary>Probes configured file availability without indexing inactive documents.</summary>
    public sealed class FileSourceStates : IDisposable
    {
        private readonly object _sync = new object();
        private readonly IFilesystem _filesystem;
        private readonly ITaskScheduler _scheduler;
        private readonly IPeriodicTask _task;
        private Dictionary<DataSourceId, string> _paths = new Dictionary<DataSourceId, string>();
        private Dictionary<DataSourceId, string> _states = new Dictionary<DataSourceId, string>();
        private long _generation;
        private bool _disposed;

        /// <summary>Uses a shared scheduler; probes never read file contents.</summary>
        public FileSourceStates(IFilesystem filesystem, ITaskScheduler scheduler)
        {
            _filesystem = filesystem ?? throw new ArgumentNullException(nameof(filesystem));
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _task = scheduler.StartPeriodic(Refresh, TimeSpan.FromMilliseconds(500), "File availability");
        }

        /// <summary>Replaces the observed configuration and invalidates pending old results.</summary>
        public void SetSources(IEnumerable<DataSource> sources)
        {
            var snapshot = sources.ToArray();
            SourceConfiguration.Validate(snapshot);
            var paths = snapshot.Where(source => source.File != null).ToDictionary(source => source.Id, source => source.File);
            lock (_sync)
            {
                if (_disposed || paths.Count == _paths.Count && paths.All(pair => _paths.TryGetValue(pair.Key, out var path) && path == pair.Value)) return;
                ++_generation;
                _states = _states.Where(pair => paths.TryGetValue(pair.Key, out var path) && _paths[pair.Key] == path)
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                _paths = paths;
            }
        }

        /// <summary>Returns a cached status, including null for an accessible nonempty file.</summary>
        public bool TryGetStatus(DataSourceId id, out string status)
        {
            lock (_sync) return _states.TryGetValue(id, out status);
        }

        private void Refresh()
        {
            Dictionary<DataSourceId, string> paths;
            long generation;
            lock (_sync)
            {
                if (_disposed) return;
                paths = _paths;
                generation = _generation;
            }
            var states = new Dictionary<DataSourceId, string>();
            foreach (var pair in paths)
            {
                lock (_sync) { if (_disposed || generation != _generation) return; }
                string status;
                try
                {
                    // shortcut: inactive parser/index errors need the shared reader lifecycle in US2.
                    using var stream = _filesystem.Open(Path.GetFullPath(pair.Value), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    status = stream.Length == 0 ? "No log lines." : null;
                }
                catch (FileNotFoundException) { status = "File is unavailable."; }
                catch (DirectoryNotFoundException) { status = "File is unavailable."; }
                catch (Exception failure) when (failure is IOException || failure is UnauthorizedAccessException)
                { status = "File cannot be accessed."; }
                catch (Exception failure) when (failure is ArgumentException || failure is NotSupportedException)
                { status = "Invalid file path."; }
                states.Add(pair.Key, status);
            }
            lock (_sync)
                if (!_disposed && generation == _generation) _states = states;
        }

        /// <summary>Stops probing and rejects outstanding results.</summary>
        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
                ++_generation;
                _paths = new Dictionary<DataSourceId, string>();
                _states.Clear();
            }
            _scheduler.StopPeriodic(_task);
        }
    }
}
#endif
