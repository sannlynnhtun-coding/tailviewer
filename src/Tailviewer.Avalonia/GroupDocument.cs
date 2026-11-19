using Tailviewer.Api;
using Tailviewer.Settings;
using Tailviewer.Core;

namespace Tailviewer.Avalonia;

// Group views own their merge layers and borrow file readers through leases.
internal sealed class GroupDocument : IDisposable
{
    private readonly List<SourceDocuments.Lease> _leases = new();
    private readonly Dictionary<DataSourceId, long> _revisions = new();
    private readonly List<LogDocument> _groups = new();
    private readonly List<ILogSource> _emptySources = new();
    private readonly List<DataSourceId> _failedReaders = new();
    private readonly List<string> _errors = new();
    private readonly List<(DataSource Source, DataSourceId[] Members, HashSet<DataSourceId> Excluded, bool SingleLine)> _snapshots = new();
    public LogDocument Document => _groups[^1];
    public string? Error => _errors.Count == 0 ? null : string.Join(Environment.NewLine, _errors);

    public static async Task<GroupDocument> OpenAsync(DataSource source, SourceDocuments readers,
        Func<DataSource, IReadOnlyList<DataSource>> children)
    {
        var result = new GroupDocument();
        try
        {
            await Build(source, new HashSet<DataSourceId>(), false);
            return result;
        }
        catch { result.Dispose(); throw; }

        async Task<LogDocument> Build(DataSource parent, HashSet<DataSourceId> ancestors, bool forceSingleLine)
        {
            if (!ancestors.Add(parent.Id)) throw new InvalidOperationException("Source groups cannot contain cycles.");
            var members = children(parent).ToArray();
            var configuration = parent.Clone();
            configuration.IsSingleLine |= forceSingleLine;
            result._snapshots.Add((parent, members.Select(member => member.Id).ToArray(), new(configuration.ExcludedDataSources), parent.IsSingleLine));
            var sources = new List<(DataSourceId, ILogSource)>();
            foreach (var member in members)
            {
                if (configuration.ExcludedDataSources.Contains(member.Id))
                {
                    Empty(member.Id);
                    continue;
                }
                LogDocument document;
                if (member.File != null)
                {
                    try
                    {
                        var lease = await readers.AcquireAsync(member.Id);
                        result._leases.Add(lease);
                        document = lease.Document;
                        result._revisions[member.Id] = document.Revision;
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        result._failedReaders.Add(member.Id);
                        result._errors.Add("Cannot open group member: " + member.File + ": " + error.Message);
                        Empty(member.Id);
                        continue;
                    }
                }
                else document = await Build(member, ancestors, configuration.IsSingleLine);
                sources.Add((member.Id, configuration.IsSingleLine ? document.ParsedSource : document.EntrySource));
            }
            ancestors.Remove(parent.Id);
            var merged = new LogDocument(configuration, sources);
            result._groups.Add(merged);
            return merged;

            void Empty(DataSourceId id)
            {
                var empty = new InMemoryLogSource();
                result._emptySources.Add(empty);
                sources.Add((id, empty));
            }
        }
    }

    public bool IsCurrent(SourceDocuments readers, Func<DataSource, IReadOnlyList<DataSource>> children)
    {
        foreach (var snapshot in _snapshots)
            if (!snapshot.Members.SequenceEqual(children(snapshot.Source).Select(member => member.Id)) ||
                !snapshot.Excluded.SetEquals(snapshot.Source.ExcludedDataSources) ||
                snapshot.SingleLine != snapshot.Source.IsSingleLine) return false;
        foreach (var lease in _leases)
        {
            if (!readers.TryAcquire(lease.Document.SourceId, out var current)) return false;
            using (current)
                if (!ReferenceEquals(current!.Document, lease.Document) ||
                    lease.Document.Revision != _revisions[lease.Document.SourceId]) return false;
        }
        foreach (var failed in _failedReaders)
            if (readers.TryAcquire(failed, out var recovered))
            {
                recovered!.Dispose();
                return false;
            }
        return true;
    }

    public void Dispose()
    {
        for (var index = _groups.Count - 1; index >= 0; --index) _groups[index].Dispose();
        _groups.Clear();
        foreach (var lease in _leases) lease.Dispose();
        _leases.Clear();
        foreach (var empty in _emptySources) empty.Dispose();
        _emptySources.Clear();
    }
}
