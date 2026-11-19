using System.Xml;
using Avalonia.Collections;
using Tailviewer.Api;
using Tailviewer.Settings;

namespace Tailviewer.Avalonia.ViewModels;

public sealed class SourcesViewModel
{
    private readonly Dictionary<DataSourceId, AvaloniaList<DataSource>> _groups = new();
    private readonly Dictionary<DataSourceId, long> _sourceOrder = new();
    private long _nextSourceOrder;
    public AvaloniaList<DataSource> Roots { get; } = new();

    public SourcesViewModel()
    {
        Roots.CollectionChanged += (_, args) =>
        {
            if (args.NewItems == null) return;
            foreach (DataSource source in args.NewItems)
                if (!_sourceOrder.ContainsKey(source.Id)) _sourceOrder.Add(source.Id, _nextSourceOrder++);
        };
    }

    public IReadOnlyList<DataSourceSuggestionViewModel> FindNavigationMatches(string? term)
    {
        if (string.IsNullOrEmpty(term)) return Array.Empty<DataSourceSuggestionViewModel>();
        var result = new List<DataSourceSuggestionViewModel>();
        foreach (var source in All.OrderBy(source => _sourceOrder.GetValueOrDefault(source.Id, long.MaxValue)))
        {
            var name = source.File ?? source.LogFileFolderPath ?? source.DisplayName ?? "";
            var index = name.IndexOf(term, StringComparison.CurrentCultureIgnoreCase);
            if (index < 0) continue;
            // Culture comparisons can match different UTF-16 lengths; never slice beyond the original name.
            var length = Math.Min(term.Length, name.Length - index);
            result.Add(new DataSourceSuggestionViewModel(source, name[..index], name.Substring(index, length), name[(index + length)..]));
        }
        return result;
    }

    public IReadOnlyList<DataSource> All
    {
        get
        {
            var result = new List<DataSource>();
            var seen = new HashSet<DataSourceId>();
            var pending = new Stack<DataSource>(Roots.Reverse());
            while (pending.TryPop(out var source))
            {
                if (!seen.Add(source.Id)) throw new XmlException("Source identities must be unique.");
                result.Add(source);
                foreach (var child in GetChildren(source.Id).Reverse()) pending.Push(child);
            }
            return result;
        }
    }

    public static bool IsGroup(DataSource source) => source.File == null && source.LogFileFolderPath == null && source.CustomDataSourceId == null;
    public bool Contains(DataSourceId id) => All.Any(source => source.Id == id);
    public IEnumerable<DataSource> GetChildren(DataSourceId id) =>
        _groups.TryGetValue(id, out var children) ? children : Array.Empty<DataSource>();

    public DataSource AddGroup()
    {
        var group = new DataSource { Id = DataSourceId.CreateNew(), DisplayName = "Merged Data Source" };
        Validate(All.Append(group));
        _groups.Add(group.Id, new AvaloniaList<DataSource>());
        Roots.Add(group);
        return group;
    }

    public void AddToGroup(DataSourceId memberId, DataSourceId groupId)
    {
        var member = Find(memberId);
        var group = Find(groupId);
        if (!IsGroup(group) || !_groups.ContainsKey(group.Id)) throw new ArgumentException("The target is not a group.");
        if (member.ParentId == groupId) return;
        if (member.ParentId != DataSourceId.Empty) throw new ArgumentException("This source already belongs to another parent.");
        if (!Roots.Contains(member)) throw new ArgumentException("The source is not an ungrouped root.");
        var candidate = All.Select(source => source.Clone()).ToArray();
        candidate.Single(source => source.Id == memberId).ParentId = groupId;
        Validate(candidate);
        Roots.Remove(member);
        member.ParentId = groupId;
        _groups[groupId].Add(member);
    }

    public void Ungroup(DataSourceId memberId)
    {
        var member = Find(memberId);
        if (member.ParentId == DataSourceId.Empty) return;
        var parent = Find(member.ParentId);
        if (!_groups.TryGetValue(parent.Id, out var children)) throw new ArgumentException("The parent is not a group.");
        children.Remove(member);
        parent.ExcludedDataSources.Remove(member.Id);
        member.ParentId = DataSourceId.Empty;
        Roots.Add(member);
    }

    public void Remove(DataSourceId id)
    {
        var source = Find(id);
        var parent = source.ParentId == DataSourceId.Empty ? null : Find(source.ParentId);
        var container = parent == null ? Roots : _groups[parent.Id];
        var index = container.IndexOf(source);
        var excluded = parent?.ExcludedDataSources.Contains(source.Id) == true;
        parent?.ExcludedDataSources.Remove(source.Id);
        container.Remove(source);
        if (_groups.Remove(id, out var children))
            foreach (var child in children)
            {
                child.ParentId = parent?.Id ?? DataSourceId.Empty;
                if (parent != null && (excluded || source.ExcludedDataSources.Contains(child.Id)))
                    parent.ExcludedDataSources.Add(child.Id);
                container.Insert(index++, child);
            }
        _sourceOrder.Remove(id);
    }

    public void Clear()
    {
        Roots.Clear();
        _groups.Clear();
        _sourceOrder.Clear();
        _nextSourceOrder = 0;
    }

    private DataSource Find(DataSourceId id) => All.SingleOrDefault(source => source.Id == id) ??
        throw new ArgumentException("The source is no longer configured.");

    private static void Validate(IEnumerable<DataSource> sources)
    {
        var settings = new DataSourceSettings();
        settings.AddRange(sources);
        _ = new SourceConfiguration(settings);
    }
}
