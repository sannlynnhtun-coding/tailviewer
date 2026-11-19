#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using Tailviewer.Api;

namespace Tailviewer.Settings
{
    /// <summary>An isolated snapshot of configured sources and their runtime content generations.</summary>
    public sealed class SourceConfiguration
    {
        private readonly DataSourceSettings _settings;
        private readonly Dictionary<DataSourceId, long> _generations;
        private readonly object _sync = new object();

        /// <summary>Snapshots existing source models without opening files or changing their identities.</summary>
        public SourceConfiguration(DataSourceSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _settings = settings.Clone();
            _generations = _settings.ToDictionary(source => source.Id, _ => 0L);
        }

        /// <summary>Returns an independent copy, including unavailable paths and unknown settings.</summary>
        public DataSourceSettings Snapshot() => _settings.Clone();

        /// <summary>Returns direct group members in configured order using parent identities.</summary>
        public IReadOnlyList<DataSource> GetMembers(DataSourceId parent) =>
            _settings.Where(source => source.ParentId == parent).Select(source => source.Clone()).ToArray();

        /// <summary>Captures the generation for a pending read.</summary>
        public long GetGeneration(DataSourceId source)
        {
            lock (_sync) return _generations[source];
        }

        /// <summary>Invalidates pending reads after replacement, reconfiguration or closure.</summary>
        public long Invalidate(DataSourceId source)
        {
            lock (_sync) return _generations[source] = checked(_generations[source] + 1);
        }

        /// <summary>Publishes synchronously only while the captured generation remains current.</summary>
        public bool TryPublish(DataSourceId source, long generation, Action publish)
        {
            ArgumentNullException.ThrowIfNull(publish);
            lock (_sync)
            {
                if (_generations[source] != generation) return false;
                publish();
                return true;
            }
        }
    }
}
#endif
