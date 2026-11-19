using System.ComponentModel;
using System.Text;
using Avalonia.Collections;
using Tailviewer.Api;
using Tailviewer.Core;
using Tailviewer.Settings;
using Tailviewer.Avalonia.Views;

namespace Tailviewer.Avalonia;

public sealed class LogDocument : IDisposable, ILogSourceListener
{
    private const int PageSize = 128;
    private const int MaxPages = 8;
    private readonly DefaultTaskScheduler _scheduler = new();
    private readonly ILogSource _source;
    private readonly SourceGroup? _group;
    internal ILogSource ParsedSource { get; }
    private readonly Dictionary<int, LogLine[]> _pages = new();
    private readonly Dictionary<int, LogLine[]> _loading = new();
    private volatile bool _disposed;
    private int _changed;
    private int _reset;
    private readonly SourceConfiguration _configuration;
    private string? _readError;

    public string FileName { get; }
    public DataSourceId SourceId { get; }
    public long Revision => _configuration.GetGeneration(SourceId);
    // shortcut: one integer per indexed line, use a lazy IList if index memory becomes a bottleneck.
    public AvaloniaList<int> Lines { get; } = new();
    public bool IsLoading => _source.GetProperty(Core.Properties.PercentageProcessed) < Percentage.HundredPercent;
    public string? Error => _source.GetProperty(Core.Properties.EmptyReason)?.Explanation ?? _readError;

    public LogDocument(string fileName)
        : this(new DataSource(fileName) { Id = DataSourceId.CreateNew() })
    {
    }

    public LogDocument(DataSource configuration, Encoding? encoding = null)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(configuration);
            _configuration = new SourceConfiguration(new DataSourceSettings { configuration });
            SourceId = configuration.Id;
            FileName = Path.GetFullPath(configuration.File);
            var factory = new StreamingTextLogSourceFactory(new Filesystem(_scheduler), _scheduler);
            _source = factory.OpenRead(FileName, encoding ?? Encoding.UTF8, detectByteOrderMarks: encoding == null);
            ParsedSource = FileLogSourceFactory.ParseText(_source);
            _source.AddListener(this, TimeSpan.Zero, 10000);
        }
        catch
        {
            _source?.Dispose();
            _scheduler.Dispose();
            throw;
        }
    }

    internal LogDocument(DataSource configuration, IEnumerable<(DataSourceId Id, ILogSource Source)> members)
    {
        try
        {
            _configuration = new SourceConfiguration(new DataSourceSettings { configuration });
            SourceId = configuration.Id;
            FileName = configuration.LogFileFolderPath ?? configuration.DisplayName ?? "Group";
            _group = new SourceGroup(_scheduler);
            _group.Configure(configuration, members);
            _source = _group.LogSource;
            ParsedSource = _source;
            _source.AddListener(this, TimeSpan.Zero, 10000);
        }
        catch
        {
            _group?.Dispose();
            _scheduler.Dispose();
            throw;
        }
    }

    public void OnLogFileModified(ILogSource logSource, LogSourceModification modification)
    {
        if (modification.IsReset())
        {
            _configuration.Invalidate(SourceId);
            Interlocked.Exchange(ref _reset, 1);
        }
        Interlocked.Exchange(ref _changed, 1);
    }

    // Called on the UI thread; Core scanning and column reads stay on background threads.
    public bool Refresh()
    {
        if (_disposed)
            return false;
        var previousCount = Lines.Count;
        var count = _source.GetProperty(Core.Properties.LogEntryCount);
        var changed = Interlocked.Exchange(ref _changed, 0) != 0;
        var notifiedReset = Interlocked.Exchange(ref _reset, 0) != 0;
        var reset = notifiedReset || count < previousCount;
        if (reset)
        {
            if (!notifiedReset) _configuration.Invalidate(SourceId);
            _pages.Clear();
            _loading.Clear();
            Lines.Clear();
        }
        if (count > Lines.Count)
            Lines.AddRange(Enumerable.Range(Lines.Count, count - Lines.Count));

        foreach (var (page, rows) in _pages.ToArray())
        {
            if ((changed && page == Math.Max(0, previousCount - 1) / PageSize) ||
                rows.Any(row => row.Index < count && row.Text == null))
                _ = LoadPageAsync(page, rows);
        }
        return changed || reset || count != previousCount;
    }

    public LogLine GetLine(int index)
    {
        if (index < 0 || index >= Lines.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        var page = index / PageSize;
        if (!_pages.TryGetValue(page, out var rows))
        {
            if (_pages.Count >= MaxPages)
                _pages.Remove(_pages.Keys.First());
            rows = Enumerable.Range(page * PageSize, PageSize).Select(i => new LogLine(i)).ToArray();
            _pages.Add(page, rows);
            _ = LoadPageAsync(page, rows);
        }
        return rows[index % PageSize];
    }

    private async Task LoadPageAsync(int page, LogLine[] rows)
    {
        if (_loading.TryGetValue(page, out var pending) && ReferenceEquals(rows, pending))
            return;
        _loading[page] = rows;
        var generation = _configuration.GetGeneration(SourceId);
        try
        {
            var section = new LogSourceSection(page * PageSize, Math.Min(PageSize, Lines.Count - page * PageSize));
            var text = await Task.Run(() => _source.GetColumn(section, Core.Columns.RawContent,
                new LogSourceQueryOptions(LogSourceQueryMode.Default, TimeSpan.FromSeconds(2))));
            if (_disposed)
                return;
            _configuration.TryPublish(SourceId, generation, () =>
            {
                if (_disposed) return;
                for (var i = 0; i < text.Length; ++i)
                    rows[i].SetText(text[i]);
                _readError = null;
            });
        }
        catch (Exception ex)
        {
            if (!_disposed)
                _configuration.TryPublish(SourceId, generation, () => _readError = ex.Message);
        }
        finally
        {
            if (_loading.TryGetValue(page, out var current) && ReferenceEquals(rows, current))
                _loading.Remove(page);
        }
    }

    public Task<string> CopyTextAsync(LogSelection selection, long? expectedRevision = null)
    {
        var start = (Line: selection.AnchorLine, Column: selection.AnchorColumn);
        var end = (Line: selection.ActiveLine, Column: selection.ActiveColumn);
        if (start.CompareTo(end) > 0) (start, end) = (end, start);
        if (start.Line < 0 || end.Line >= Lines.Count || start.Column < 0 || end.Column < 0)
            throw new ArgumentOutOfRangeException(nameof(selection));
        var indices = Enumerable.Range(start.Line, end.Line - start.Line + 1).Select(i => new LogLineIndex(i)).ToArray();
        return ReadSelectionAsync(indices, text =>
        {
            if (start.Column > text[0].Length || end.Column > text[^1].Length)
                throw new ArgumentOutOfRangeException(nameof(selection));
            if (text.Length == 1) return text[0][start.Column..end.Column];
            text[0] = text[0][start.Column..];
            text[^1] = text[^1][..end.Column];
            return string.Join(Environment.NewLine, text);
        }, expectedRevision);
    }

    public Task<string> CopyTextAsync(IEnumerable<int> selection)
    {
        var indices = selection.Distinct().Order().Select(i => new LogLineIndex(i)).ToArray();
        if (indices.Any(index => index < 0 || index >= Lines.Count))
            throw new ArgumentOutOfRangeException(nameof(selection));
        return ReadSelectionAsync(indices, text => string.Join(Environment.NewLine, text));
    }

    private async Task<string> ReadSelectionAsync(LogLineIndex[] indices, Func<string[], string> format, long? expectedRevision = null)
    {
        var generation = expectedRevision ?? Revision;
        var text = await Task.Run(() => _source.GetColumn(indices, Core.Columns.RawContent,
            new LogSourceQueryOptions(LogSourceQueryMode.Default, TimeSpan.FromSeconds(2))));
        var result = string.Empty;
        if (_disposed || text.Any(line => line == null) ||
            !_configuration.TryPublish(SourceId, generation, () => result = format(text)))
            throw new IOException("The file changed or could not be read. Select the lines and try again.");
        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _configuration.Invalidate(SourceId);
        _source.RemoveListener(this);
        if (_group != null) _group.Dispose();
        else ParsedSource.Dispose();
        _scheduler.Dispose();
        _pages.Clear();
    }
}

public sealed class LogLine(int index) : INotifyPropertyChanged
{
    public int Index { get; } = index;
    public string? Text { get; private set; }
    public string DisplayText => $"{Index + 1,7}  {Text ?? "Loading…"}";
    public event PropertyChangedEventHandler? PropertyChanged;

    internal void SetText(string? text)
    {
        if (Text == text)
            return;
        Text = text;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText)));
    }
}
