#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Tailviewer.Api;
using Tailviewer.Settings;

namespace Tailviewer.Core
{
    /// <summary>Owns a merged view without taking ownership of its member sources.</summary>
    public sealed class SourceGroup : IDisposable
    {
        private readonly ITaskScheduler _scheduler;
        private readonly LogSourceProxy _proxy;
        private MergedLogSource _merged;
        private bool _disposed;

        /// <summary>Creates a stable forwarding view for an ordered group.</summary>
        public SourceGroup(ITaskScheduler scheduler)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _proxy = new LogSourceProxy(scheduler, TimeSpan.Zero);
        }
        /// <summary>The stable log source presented to group consumers.</summary>
        public ILogSource LogSource => _proxy;
        /// <summary>Configured member identities in merged source-slot order, including exclusions.</summary>
        public IReadOnlyList<DataSourceId> SourceIds { get; private set; } = Array.Empty<DataSourceId>();
        /// <summary>Replaces the ordered membership and exclusions after validating the complete snapshot.</summary>
        public void Configure(DataSource settings, IEnumerable<(DataSourceId Id, ILogSource Source)> members)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(members);
            if (settings.Id == DataSourceId.Empty) throw new ArgumentException("Group identity must be nonempty.", nameof(settings));
            var snapshot = members.ToArray();
            var ids = new HashSet<DataSourceId>();
            if (snapshot.Length > LogEntrySourceId.MaxSources || snapshot.Any(member =>
                member.Id == DataSourceId.Empty || member.Id == settings.Id || member.Source == null || !ids.Add(member.Id)))
                throw new ArgumentException("Group members must have unique nonempty identities, usable sources and fit the source-slot limit.", nameof(members));

            // Empty slots preserve legacy source attribution when a member is excluded.
            var merged = new MergedLogSource(_scheduler, TimeSpan.Zero, snapshot.Select(member =>
                settings.ExcludedDataSources.Contains(member.Id) ? new EmptyLogSource() : member.Source));
            var previous = _merged;
            _proxy.InnerLogSource = merged;
            _merged = merged;
            SourceIds = Array.AsReadOnly(snapshot.Select(member => member.Id).ToArray());
            previous?.Dispose();
        }
        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _proxy.Dispose();
            _merged?.Dispose();
        }
    }
}
#endif
