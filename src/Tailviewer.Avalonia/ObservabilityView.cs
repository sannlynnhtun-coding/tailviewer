using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;

using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Tailviewer.Avalonia;

internal sealed class ObservabilityView : UserControl, IDisposable
{
    private static readonly IBrush Panel = new SolidColorBrush(Color.Parse("#0e0e11"));
    private static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#e4e4e7"));
    private static readonly IBrush Surface = new SolidColorBrush(Color.Parse("#121215"));
    private static readonly IBrush Rule = new SolidColorBrush(Color.Parse("#27272a"));
    private static readonly ISolidColorBrush Blue = new SolidColorBrush(Color.Parse("#7dd3fc"));
    private static readonly FontFamily Sans = new("avares://Tailviewer.Avalonia/Assets#Inter");
    private static readonly FontFamily Mono = new("avares://Tailviewer.Avalonia/Assets#JetBrains Mono");
    private static readonly ISolidColorBrush Muted = new SolidColorBrush(Color.Parse("#a1a1aa"));
    private static readonly string[] Columns = ["TIMESTAMP (UTC)", "LEVEL", "SERVICE", "TRACE ID", "LATENCY", "EVENT MESSAGE & STRUCTURED PAYLOAD"];
    private static readonly double[] Widths = [165, 72, 135, 120, 82, 0];
    private readonly Window _owner;
    private readonly Func<IReadOnlyList<(string Path, string? Root)>> _files;
    private readonly Func<Encoding?> _encoding;
    private readonly Func<IReadOnlyList<string>> _folderErrors;
    private readonly TextBox _search = new() { PlaceholderText = "Search logs…", MinWidth = 220 };
    private readonly ToggleButton _regex = Toggle(".*");
    private readonly ComboBox _range = new() { ItemsSource = new[] { "All Time", "Last 5 min", "Last 15 min", "Last hour", "Last 24 hours" }, SelectedIndex = 0 };
    private readonly ComboBox _language = new() { ItemsSource = new[] { "Auto", "Plain Text", "JSON", "C#", "JavaScript", "TypeScript", "Python", "Java", "SQL", "HTML/XML", "Shell" }, SelectedIndex = 0 };
    private readonly ComboBox _scope = new() { SelectedIndex = 0, MaxWidth = 220 };
    private readonly ComboBox _services = new() { SelectedIndex = 0, MaxWidth = 180 };
    private readonly WrapPanel _environments = new();
    private bool _cancelled;
    private bool _checkingChanges;
    private readonly ObservableCollection<ObservedLog> _matches = [];
    private readonly ListBox _rows = new();
    private readonly TextBlock _status = new();
    private readonly TextBlock _detail = new() { FontFamily = Mono, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _timeline = new() { Spacing = 4 };
    private readonly WrapPanel _chips = new();
    private readonly StackPanel _facets = new() { Spacing = 7 };
    private readonly StackPanel _drawer = new() { Spacing = 9 };
    private readonly ToggleButton _live = Toggle("Live Tail", true);
    private readonly ToggleButton _auto = Toggle("Auto-scroll", true);
    private readonly ToggleButton _wrap = Toggle("Wrap");
    private readonly ToggleButton _compact = Toggle("Compact", true);
    private readonly StackPanel _empty = new() { IsVisible = false, Spacing = 8,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly bool[] _columns = [true, true, true, true, true, true];
    private readonly Dictionary<string, string> _filters = new();
    private readonly Dictionary<string, HashSet<string>> _facetValues = new();
    private readonly Dictionary<(string Field, string Value), int> _facetCounts = new();
    private readonly SortedDictionary<(DateTimeOffset Time, string Level), int> _buckets = new();
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _watch = new() { Interval = TimeSpan.FromSeconds(2) };
    private CancellationTokenSource? _scan;
    private string[] _roots = [];
    private string _signature = "";
    private ScanCache? _cache;

    private sealed record FileMatches(string Signature, List<ObservedLog> Logs,
        Dictionary<(string Field, string Value), int> Counts, Dictionary<(DateTimeOffset Time, string Level), int> Buckets);
    private sealed record ScanCache(string Query, Dictionary<string, FileMatches> Files);
    private TimeSpan _bucketWidth = TimeSpan.FromMinutes(1);
    private (DateTimeOffset From, DateTimeOffset Until)? _window;
    private bool _disposed;
    private bool _active;
    private bool _updating;
    private readonly Button _curl;

    public ObservabilityView(Window owner, Func<IReadOnlyList<(string Path, string? Root)>> files, Func<Encoding?> encoding,
        Func<Task> addFolders, Func<Task> openFiles, Action sources, Func<IReadOnlyList<string>> folderErrors)
    {
        _owner = owner;
        _files = files;
        _encoding = encoding;
        _folderErrors = folderErrors;
        Background = new SolidColorBrush(Color.Parse("#09090b"));
        Foreground = Ink;
        FontFamily = Sans;

        var terminal = new Border
        {
            Width = 24, Height = 24, CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#17664e")),
            Background = new SolidColorBrush(Color.Parse("#12302b")),
            Child = Icon("M4 17L10 11 4 5 M12 19H20", new SolidColorBrush(Color.Parse("#34d399")))
        };
        var brand = Label("Tailviewer", 14);
        brand.FontWeight = FontWeight.SemiBold;
        var header = Row(
            IconAction("Toggle facets sidebar", "M3 3H21V21H3Z M9 3V21", () => _facets.IsVisible = !_facets.IsVisible),
            terminal, brand, _services, _environments, _search,
            IconAction("Clear search", "M6 6L18 18 M6 18L18 6", () => _search.Text = ""),
            _regex, _range, _live,
            IconAction("Export filtered logs as JSON", "M12 3V15 M7 10L12 15 17 10 M4 16V21H20V16", async () => await ExportAsync()));
        ToolTip.SetTip(_regex, "Toggle regular expression mode");
        global::Avalonia.Automation.AutomationProperties.SetName(_regex, "Regular expression mode");
        _search.FontFamily = Mono;
        _search.FontSize = 12;
        _range.FontSize = _scope.FontSize = _services.FontSize = 12;
        _status.FontFamily = Mono;
        _status.FontSize = 11;
        _status.Foreground = Muted;
        var hints = Row(new[] { "level:ERROR", "status:500", "latency>500", "service:payment-gateway-api", "timeout|deadlock" }
            .Select(query => Action(query, () =>
            {
                if (query.Contains('|')) _regex.IsChecked = true;
                _search.Text = query;
            })).Cast<Control>().ToArray());
        hints.IsVisible = false;
        _search.GotFocus += (_, _) => hints.IsVisible = true;
        _search.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Escape) hints.IsVisible = false; };
        foreach (var chip in hints.Children.OfType<Button>())
            chip.Click += (_, _) => hints.IsVisible = false;
        global::Avalonia.Automation.AutomationProperties.SetName(_language, "Highlight language");
        _language.FontSize = 11;
        var commands = Row(_auto, _wrap, _compact, Label("Language", 11), _language, Action("Columns", ShowColumns), Action("Cancel", Cancel));
        var sourceCommands = Row(AsyncAction("Add Folders", addFolders), AsyncAction("Open File", openFiles), Action("Sources", sources), _scope);
        var streamTools = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        Add(streamTools, _chips, 0);
        Add(streamTools, commands, 0, 1);
        var heading = new ContentControl { Content = BuildRow(Columns, null) };
        _rows.ItemsSource = _matches;
        _rows.ItemTemplate = new FuncDataTemplate<ObservedLog>((log, _) => log == null ? null : BuildRow(
            [log.Timestamp?.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff"), log.Level, log.Service, log.Trace,
                log.Latency is { } ms ? $"{ms:g} ms" : null, log.Message], log), false);
        _rows.SelectionChanged += (_, _) => UpdateInspector();
        _wrap.IsCheckedChanged += (_, _) => RefreshRows(heading);
        _compact.IsCheckedChanged += (_, _) => { _compact.Content = _compact.IsChecked == true ? "Compact" : "Comfy"; RefreshRows(heading); };
        _columnsChanged = () => RefreshRows(heading);
        _language.SelectionChanged += (_, _) =>
        {
            RefreshRows(heading);
            UpdateInspector();
        };

        _curl = AsyncAction("Copy cURL", () => CopyAsync(CreateCurl(_rows.SelectedItem as ObservedLog)));
        _drawer.Children.Add(Row(Label("Log Inspector", 16), IconAction("Previous log", "M6 15L12 9 18 15", () => Navigate(-1)),
            IconAction("Next log", "M6 9L12 15 18 9", () => Navigate(1)), IconAction("Close inspector", "M6 6L18 18 M6 18L18 6", CloseInspector)));
        _drawer.Children.Add(Row(Action("Filter Trace", () => ApplySelected("Trace", log => log.Trace)),
            Action("Exclude Service", () => ApplySelected("Exclude Service", log => log.Service)),
            Action("Surrounding Logs", () =>
            {
                if (_rows.SelectedItem is ObservedLog { Timestamp: { } time }) SetWindow(time.AddSeconds(-5), time.AddSeconds(5));
            })));
        _drawer.Children.Add(Row(AsyncAction("Copy Trace", () => CopyAsync((_rows.SelectedItem as ObservedLog)?.Trace)),
            _curl, AsyncAction("Copy JSON", () => CopyAsync(_rows.SelectedItem is ObservedLog log ? JsonSerializer.Serialize(log) : null))));
        _drawer.Children.Add(_detail);
        var inspector = new ScrollViewer { Content = _drawer, Width = 520, HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(16, 12), Background = Surface, IsVisible = false };
        _inspector = inspector;
        var logArea = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        Add(logArea, new Border { Background = Surface, Padding = new Thickness(6), Child = streamTools }, 0);
        Add(logArea, new Border { Background = Panel, BorderBrush = Rule, BorderThickness = new Thickness(0, 1), Child = heading }, 1);
        Add(logArea, _rows, 2);
        _empty.Children.Add(new Border
        {
            Width = 48, Height = 48, Background = Surface, BorderBrush = Rule, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12), HorizontalAlignment = HorizontalAlignment.Center,
            Child = Icon("M16 11A5 5 0 1 1 6 11A5 5 0 1 1 16 11 M15 15L21 21 M9 9L13 13 M13 9L9 13", Muted)
        });
        var emptyTitle = Label("No matching log events found", 14);
        emptyTitle.FontWeight = FontWeight.SemiBold;
        _empty.Children.Add(emptyTitle);
        var emptyReset = Action("Clear All Filters", () => { Reset(); RequestScan(); });
        emptyReset.HorizontalAlignment = HorizontalAlignment.Center;
        emptyReset.Foreground = Blue;
        _empty.Children.Add(emptyReset);
        Add(logArea, _empty, 2);
        Add(logArea, new Border { Background = Surface, Padding = new Thickness(8, 4), Child = _status }, 3);
        var sidebar = new ScrollViewer { Content = _facets, Background = new SolidColorBrush(Color.Parse("#12151d")), Padding = new Thickness(10), Width = 242 };
        _facets.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) sidebar.IsVisible = _facets.IsVisible; };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 0 };
        Add(body, sidebar, 0);
        Add(body, logArea, 0, 1);
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"), RowSpacing = 0 };
        Add(layout, new Border { Background = Surface, Padding = new Thickness(12, 8), BorderBrush = Rule, BorderThickness = new Thickness(0, 0, 0, 1), Child = header }, 0);
        Add(layout, hints, 1);
        Add(layout, new Border { Background = Surface, Padding = new Thickness(12, 4), Child = sourceCommands }, 2);
        Add(layout, new Border { Background = Panel, Padding = new Thickness(10, 6), Child = _timeline }, 3);
        Add(layout, body, 4);
        Grid.SetRowSpan(inspector, 5);
        Add(layout, inspector, 0);
        layout.SizeChanged += (_, e) => inspector.Width = Math.Min(e.NewSize.Width, e.NewSize.Width >= 1024 ? 580 : 520);
        Content = layout;
        _search.TextChanged += (_, _) => RequestScan();
        _regex.IsCheckedChanged += (_, _) => RequestScan();
        _range.SelectionChanged += (_, _) => RequestScan();
        _scope.SelectionChanged += (_, _) => RequestScan();
        _services.SelectionChanged += (_, _) =>
        {
            if (_updating) return;
            if (_services.SelectedIndex > 0 && _services.SelectedItem is string value) _filters["Service"] = value;
            else _filters.Remove("Service");
            _facetValues.Remove("Service");
            RequestScan();
        };
        _live.IsCheckedChanged += (_, _) =>
        {
            _live.Content = _live.IsChecked == true ? "Live Tail" : "Paused";
            if (_live.IsChecked == true) RequestScan();
            else { Cancel(); _status.Text = $"Paused · {_matches.Count:N0} matches"; }
        };
        _debounce.Tick += async (_, _) => { _debounce.Stop(); await ScanAsync(); };
        _watch.Tick += async (_, _) =>
        {
            if (!_active || _cancelled || _live.IsChecked != true || _checkingChanges ||
                _scan != null || _debounce.IsEnabled || _disposed) return;
            _checkingChanges = true;
            try
            {
                var paths = RefreshPaths();
                var signature = await Task.Run(() => Signature(paths));
                // shortcut: changed files are reread; use byte offsets if profiling shows large active-file rescans dominate.
                if (_active && !_disposed && !_cancelled && !_debounce.IsEnabled && _live.IsChecked == true &&
                    _scan == null && signature != _signature) RequestScan();
            }
            finally { _checkingChanges = false; }
        };
        _watch.Start();
        LayoutFacets();
        LayoutTimeline();
    }

    private readonly ScrollViewer _inspector;
    private readonly Action _columnsChanged;

    public void Activate()
    {
        _active = true;
        RequestScan();
    }

    public void Deactivate()
    {
        _active = false;
        Cancel();
    }

    public void Refresh() => RequestScan();

    public bool HandleKey(KeyEventArgs e)
    {
        if (!_active) return false;
        if (e.Key == Key.Escape && _inspector.IsVisible) { CloseInspector(); return true; }
        if (e.KeyModifiers != KeyModifiers.None ||
            e.Source is Visual origin && origin.GetSelfAndVisualAncestors().Any(control => control is TextBox or ComboBox)) return false;
        if (e.Key == Key.OemQuestion) { _search.Focus(); _search.SelectAll(); return true; }
        if (e.Key == Key.J || e.Key == Key.Down && e.Source is ListBoxItem or ListBox) { Navigate(1); return true; }
        if (e.Key == Key.K || e.Key == Key.Up && e.Source is ListBoxItem or ListBox) { Navigate(-1); return true; }
        return false;
    }

    private string[] RefreshPaths()
    {
        var selected = _scope.SelectedItem as string;
        var entries = _files();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var roots = entries.Select(entry => entry.Root).OfType<string>().Distinct(comparer).Order(comparer).ToArray();
        if (!_roots.SequenceEqual(roots, comparer) || _scope.ItemsSource == null)
        {
            _roots = roots;
            _updating = true;
            _scope.ItemsSource = new[] { "All Folders" }.Concat(roots).ToArray();
            _scope.SelectedItem = selected;
            if (_scope.SelectedIndex < 0) _scope.SelectedIndex = 0;
            _updating = false;
        }

        return entries.Where(entry => _scope.SelectedIndex <= 0 || comparer.Equals(entry.Root, _scope.SelectedItem as string))
            .Select(entry => entry.Path).Distinct(comparer).Order(comparer).ToArray();
    }
    private void RequestScan()
    {
        if (_updating || _disposed || !_active) return;
        _cancelled = false;
        _scan?.Cancel();
        _debounce.Stop();
        _debounce.Start();
    }

    private void Cancel()
    {
        _cancelled = true;
        _debounce.Stop();
        _scan?.Cancel();
    }

    private async Task ScanAsync()
    {
        var paths = RefreshPaths();
        var encoding = _encoding();
        var filters = _filters.ToArray();
        var facets = _facetValues.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        var window = _window;
        var cutoff = _range.SelectedIndex switch
        {
            1 => DateTimeOffset.Now.AddMinutes(-5), 2 => DateTimeOffset.Now.AddMinutes(-15),
            3 => DateTimeOffset.Now.AddHours(-1), 4 => DateTimeOffset.Now.AddDays(-1), _ => (DateTimeOffset?)null
        };
        Func<ObservedLog, bool> query;
        try { query = CreateQuery(_search.Text, _regex.IsChecked == true); }
        catch (ArgumentException error) { _status.Text = error.Message; return; }

        using var cancellation = new CancellationTokenSource();
        _scan = cancellation;
        var token = cancellation.Token;
        var selection = _rows.SelectedItem as ObservedLog;
        _empty.IsVisible = false;
        _matches.Clear();
        _facetCounts.Clear();
        _buckets.Clear();
        _bucketWidth = cutoff is { } start
            ? TimeSpan.FromSeconds(Math.Max(1, (DateTimeOffset.Now - start).TotalSeconds / 24))
            : TimeSpan.FromDays(1);
        var bucketWidth = _bucketWidth;
        var errors = _folderErrors().ToList();
        var cacheKey = JsonSerializer.Serialize(new { Text = _search.Text, Regex = _regex.IsChecked,
            Encoding = encoding?.WebName, Filters = filters, Facets = facets, Window = window });
        var previous = cutoff == null && _cache?.Query == cacheKey ? _cache.Files : null;
        try
        {
            await Task.Run(async () =>
            {
                var signatures = paths.ToDictionary(path => path, FileSignature);
                var signature = string.Join('\n', signatures.Values);
                var found = new Dictionary<string, FileMatches>();
                var batch = new List<ObservedLog>();
                var counts = new Dictionary<(string Field, string Value), int>();
                var buckets = new Dictionary<(DateTimeOffset Time, string Level), int>();
                var lastUpdate = Environment.TickCount64;
                async Task Publish(int filesDone)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (_disposed || token.IsCancellationRequested) return;
                        foreach (var log in batch)
                        {
                            _matches.Add(log);
                            if (selection?.File == log.File && selection.Line == log.Line) _rows.SelectedItem = log;
                        }
                        foreach (var (key, count) in counts) _facetCounts[key] = _facetCounts.GetValueOrDefault(key) + count;
                        foreach (var (key, count) in buckets) _buckets[key] = _buckets.GetValueOrDefault(key) + count;
                        _status.Text = $"{_matches.Count:N0} matches · {filesDone:N0}/{paths.Length:N0} files · {errors.Count} unreadable";
                    });
                    batch.Clear();
                    counts.Clear();
                    buckets.Clear();
                    lastUpdate = Environment.TickCount64;
                }

                for (var index = 0; index < paths.Length; index++)
                {
                    token.ThrowIfCancellationRequested();
                    var path = paths[index];
                    if (previous != null && previous.TryGetValue(path, out var cached) && cached.Signature == signatures[path])
                    {
                        found[path] = cached;
                        foreach (var (key, count) in cached.Counts) counts[key] = counts.GetValueOrDefault(key) + count;
                        foreach (var (key, count) in cached.Buckets) buckets[key] = buckets.GetValueOrDefault(key) + count;
                        foreach (var log in cached.Logs)
                        {
                            token.ThrowIfCancellationRequested();
                            batch.Add(log);
                            if (batch.Count >= 256) await Publish(index);
                        }
                        if (Environment.TickCount64 - lastUpdate >= 250) await Publish(index + 1);
                        continue;
                    }

                    var current = cutoff == null ? new FileMatches(signatures[path], [], [], []) : null;
                    try
                    {
                        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                            FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.SequentialScan | FileOptions.Asynchronous);
                        using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8, encoding == null);
                        await foreach (var log in ObservedLog.ReadAsync(reader, path, token))
                        {
                            if (!query(log) || cutoff is { } limit && (log.Timestamp is not { } ts || ts < limit)) continue;
                            foreach (var field in FacetFields)
                            {
                                var entry = (field, Value(log, field) ?? "Unknown");
                                counts[entry] = counts.GetValueOrDefault(entry) + 1;
                                if (current != null) current.Counts[entry] = current.Counts.GetValueOrDefault(entry) + 1;
                            }
                            if (!filters.All(pair => pair.Key == "Exclude Service"
                                    ? !Same(log.Service, pair.Value) : Same(Value(log, pair.Key), pair.Value))) continue;
                            if (!facets.All(pair => pair.Value.Any(value => Same(Value(log, pair.Key), value)))) continue;
                            if (log.Timestamp is { } time)
                            {
                                var ticks = time.UtcTicks;
                                var bucket = (new DateTimeOffset(ticks - ticks % bucketWidth.Ticks, TimeSpan.Zero), log.Level ?? "Unknown");
                                buckets[bucket] = buckets.GetValueOrDefault(bucket) + 1;
                                if (current != null) current.Buckets[bucket] = current.Buckets.GetValueOrDefault(bucket) + 1;
                            }
                            if (window is { } bounds && (log.Timestamp is not { } stamp || stamp < bounds.From || stamp >= bounds.Until)) continue;
                            current?.Logs.Add(log);
                            batch.Add(log);
                            if (batch.Count >= 256 || Environment.TickCount64 - lastUpdate >= 250) await Publish(index);
                        }
                        if (current != null) found[path] = current;
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        errors.Add($"{path}: {error.Message}");
                    }

                    if (Environment.TickCount64 - lastUpdate >= 250) await Publish(index + 1);
                }

                await Publish(paths.Length);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (_disposed || token.IsCancellationRequested) return;
                    _signature = signature;
                    _cache = cutoff == null ? new ScanCache(cacheKey, found) : null;
                    LayoutFacets();
                    LayoutTimeline();
                    ToolTip.SetTip(_status, errors.Count == 0 ? null : string.Join(Environment.NewLine, errors));
                    _empty.IsVisible = _matches.Count == 0;
                    if (_matches.Count == 0) _status.Text = $"No matching log events found · {paths.Length:N0} files · {errors.Count} unreadable";
                    else if (_auto.IsChecked == true && _live.IsChecked == true && selection == null) _rows.ScrollIntoView(_matches[^1]);
                });
            }, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (ReferenceEquals(_scan, cancellation) && _live.IsChecked == true) _status.Text = $"Search stopped · {_matches.Count:N0} matches";
        }
        catch (RegexMatchTimeoutException)
        {
            if (ReferenceEquals(_scan, cancellation)) _status.Text = "Regular expression took too long to evaluate.";
        }
        finally
        {
            if (ReferenceEquals(_scan, cancellation)) _scan = null;
        }
    }

    internal static Func<ObservedLog, bool> CreateQuery(string? text, bool regex)
    {
        if (string.IsNullOrWhiteSpace(text)) return _ => true;
        var terms = new List<Func<ObservedLog, bool>>();
        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = token.Split(':', 2);
            var field = parts[0].ToLowerInvariant();
            if (parts.Length == 2 && field is "level" or "status" or "service" or "trace" or "traceid")
            {
                var normalized = field == "traceid" ? "Trace" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(field);
                var value = parts[1];
                if (value.Length == 0) throw new ArgumentException($"Enter a value for {field}.");
                terms.Add(log => field == "status" ? Same(log.Status?.ToString(CultureInfo.InvariantCulture), value)
                    : field == "level" ? Same(log.Level, value) : Value(log, normalized)?.Contains(value, StringComparison.OrdinalIgnoreCase) == true);
            }
            else if (token.StartsWith("latency>", StringComparison.OrdinalIgnoreCase))
            {
                if (!double.TryParse(token.AsSpan(8), CultureInfo.InvariantCulture, out var threshold) || !double.IsFinite(threshold))
                    throw new ArgumentException("Enter a numeric latency in milliseconds.");
                terms.Add(log => log.Latency > threshold);
            }
            else if (regex)
            {
                var pattern = new Regex(token, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                terms.Add(log => pattern.IsMatch(SearchText(log)));
            }
            else terms.Add(log => SearchText(log).Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        return log => terms.All(term => term(log));
    }

    private static string SearchText(ObservedLog log) => $"{log.Message} {log.Service} {log.Trace} {log.Host} {log.Payload}";
    private static bool Same(string? value, string expected) => string.Equals(value ?? "Unknown", expected, StringComparison.OrdinalIgnoreCase);
    private static readonly string[] FacetFields = ["Service", "Environment", "Level", "HTTP Status", "Host"];
    private static string? Value(ObservedLog log, string field) => field switch
    {
        "Service" => log.Service, "Environment" => log.Environment, "Level" => log.Level,
        "HTTP Status" => log.StatusGroup, "Host" => log.Host, "Trace" => log.Trace, _ => null
    };

    private static string Signature(string[] paths) => string.Join('\n', paths.Select(FileSignature));

    private static string FileSignature(string path)
    {
        try
        {
            var file = new FileInfo(path);
            return file.Exists ? $"{path}|{file.Length}|{file.LastWriteTimeUtc.Ticks}|{file.CreationTimeUtc.Ticks}" : path;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return path; }
    }

    private void LayoutFacets()
    {
        LayoutSelectors();
        _facets.Children.Clear();
        _facets.Children.Add(Row(AccentLabel("QUICK VIEWS", "Unknown"), Action("Reset all", () =>
        {
            Reset();
            RequestScan();
        })));
        _facets.Children.Add(QuickView("5xx Errors Only", "M12 22C4 22 4 13 9 10C8 14 13 14 13 10C13 7 11 5 13 2C14 8 20 10 20 16C20 20 16 22 12 22Z", Rose, () => Preset("HTTP Status", "5xx")));
        _facets.Children.Add(QuickView("Slow Queries (>500 ms)", "M4 17A9 9 0 1 1 20 17 M12 14L16 9", Amber, () => Preset(null, "latency>500")));
        _facets.Children.Add(QuickView("Payment Timeouts", "M8 3H16L21 8V16L16 21H8L3 16V8Z M12 7V12 M12 16H12.1", Indigo, () =>
        {
            Reset();
            _filters["Service"] = "payment-gateway-api";
            _filters["Level"] = "ERROR";
            RequestScan();
        }));
        foreach (var field in FacetFields)
        {
            _facets.Children.Add(new Border { Height = 1, Background = Rule, Margin = new Thickness(0, 6) });
            _facets.Children.Add(AccentLabel((field == "Host" ? "KUBERNETES NODE / HOST" : field == "Level" ? "LOG LEVEL" : field.ToUpperInvariant()), "Unknown"));
            var values = _facetCounts.Keys.Where(pair => pair.Field == field).Select(pair => pair.Value)
                .Concat(_filters.TryGetValue(field, out var chosen) ? [chosen] : []).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
            foreach (var value in values)
            {
                var check = new CheckBox
                {
                    Content = Row(AccentLabel("● " + value, field is "Level" or "HTTP Status" ? value : "Service"),
                        AccentLabel(_facetCounts.GetValueOrDefault((field, value)).ToString("N0"), "Unknown")),
                    IsChecked = _facetValues.TryGetValue(field, out var group) && group.Contains(value) || Same(chosen, value) && chosen != null
                };
                check.IsCheckedChanged += (_, _) =>
                {
                    if (!_facetValues.TryGetValue(field, out var selected))
                        _facetValues[field] = selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (_filters.Remove(field, out var previous)) selected.Add(previous);
                    if (check.IsChecked == true) selected.Add(value);
                    else selected.Remove(value);
                    if (selected.Count == 0) _facetValues.Remove(field);
                    RequestScan();
                };
                _facets.Children.Add(check);
            }
        }

        _chips.Children.Clear();
        foreach (var (field, value) in _filters)
            _chips.Children.Add(Action($"{field}: {value} ×", () => { _filters.Remove(field); RequestScan(); }));
        foreach (var (field, values) in _facetValues)
            foreach (var value in values)
                _chips.Children.Add(Action($"{field}: {value} ×", () =>
                {
                    values.Remove(value);
                    if (values.Count == 0) _facetValues.Remove(field);
                    RequestScan();
                }));
        if (_window is { } bounds)
            _chips.Children.Add(Action($"{bounds.From.LocalDateTime:g} – {bounds.Until.LocalDateTime:g} ×",
                () => { _window = null; RequestScan(); }));
    }

    private static Button QuickView(string label, string path, ISolidColorBrush color, Action action)
    {
        var button = Action(label, action);
        button.Content = Row(Icon(path, color), Label(label));
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.Background = Tint(color, 16);
        button.BorderBrush = Tint(color, 52);
        return button;
    }
    private void LayoutSelectors()
    {
        _updating = true;
        var selected = _filters.GetValueOrDefault("Service");
        var services = _facetCounts.Keys.Where(pair => pair.Field == "Service" && pair.Value != "Unknown")
            .Select(pair => pair.Value).Concat(selected == null ? [] : [selected])
            .Distinct(StringComparer.OrdinalIgnoreCase).Order().ToArray();
        _services.ItemsSource = new[] { "All Services" }.Concat(services).ToArray();
        _services.SelectedIndex = selected == null ? 0 : Array.FindIndex(services,
            value => string.Equals(value, selected, StringComparison.OrdinalIgnoreCase)) + 1;
        _updating = false;

        _environments.Children.Clear();
        var environments = new[] { "PROD", "STAGING", "DEV" }
            .Concat(_facetCounts.Keys.Where(pair => pair.Field == "Environment" && pair.Value != "Unknown").Select(pair => pair.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var value in environments)
        {
            var active = _filters.TryGetValue("Environment", out var environment) && Same(environment, value);
            var button = Action(value, () =>
            {
                _facetValues.Remove("Environment");
                if (active) _filters.Remove("Environment");
                else _filters["Environment"] = value;
                RequestScan();
            });
            if (active)
            {
                button.Foreground = Accent(value == "PROD" ? "ERROR" : value == "STAGING" ? "WARN" : "INFO");
                button.Background = Tint((ISolidColorBrush)button.Foreground, 27);
            }
            _environments.Children.Add(button);
        }
    }
    private void LayoutTimeline()
    {
        _timeline.Children.Clear();
        var title = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        Add(title, Row(Icon("M3 3V21H21 M8 17V13 M13 17V8 M18 17V5", Blue), Label("Log Ingestion Volume", 11)), 0);
        var countLabel = Label($"{_matches.Count:N0} events matched", 11);
        countLabel.FontFamily = Mono;
        Add(title, Row(AccentLabel("■ INFO", "INFO"), AccentLabel("■ WARN", "WARN"),
            AccentLabel("■ ERROR", "ERROR"), countLabel), 0, 1);
        _timeline.Children.Add(title);
        if (_buckets.Count == 0)
        {
            _timeline.Children.Add(new Border { Height = 56, BorderBrush = Rule, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.Parse("#09090b")) });
            return;
        }
        var origin = _buckets.First().Key.Time;
        var span = _buckets.Last().Key.Time - origin;
        var width = _bucketWidth * Math.Max(1, Math.Ceiling((span / _bucketWidth + 1) / 24));
        var displayed = _buckets.GroupBy(pair => (pair.Key.Time - origin).Ticks / width.Ticks)
            .Select(group => (Start: origin.AddTicks(group.Key * width.Ticks), Count: group.Sum(pair => pair.Value),
                Levels: group.GroupBy(pair => pair.Key.Level).Select(level => (Level: level.Key, Count: level.Sum(pair => pair.Value))).ToArray()))
            .ToArray();
        var peak = displayed.Max(bucket => bucket.Count);
        var chart = new Grid { Height = 52, ColumnSpacing = 3 };
        var slots = (int)(span.Ticks / width.Ticks) + 1;
        for (var slot = 0; slot < slots; slot++)
            chart.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        foreach (var (start, count, levels) in displayed)
        {
            var button = Action("", () => SetWindow(start, start + width));
            button.Height = Math.Max(4, 52d * count / peak);
            var segments = new Grid();
            foreach (var (level, amount) in levels.OrderBy(level => level.Level switch
                { "ERROR" or "FATAL" => 0, "WARN" => 1, "INFO" => 2, _ => 3 }))
            {
                var row = segments.RowDefinitions.Count;
                segments.RowDefinitions.Add(new RowDefinition(new GridLength(amount, GridUnitType.Star)));
                Add(segments, new Border { Background = Tint(Accent(level), 155) }, row);
            }
            button.Background = Panel;
            button.Content = segments;
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.VerticalContentAlignment = VerticalAlignment.Stretch;
            button.BorderThickness = _window is { } selected && selected.From == start ? new Thickness(1) : new Thickness(0);
            button.BorderBrush = Blue;
            button.Padding = new Thickness(0);
            button.VerticalAlignment = VerticalAlignment.Bottom;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.MaxWidth = 32;
            var description = $"{start.LocalDateTime:g} · {count:N0} events";
            ToolTip.SetTip(button, description);
            global::Avalonia.Automation.AutomationProperties.SetName(button, description);
            var column = (int)((start - origin).Ticks / width.Ticks);
            Add(chart, button, 0, column);
        }
        _timeline.Children.Add(chart);
        var labels = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Add(labels, Label(displayed[0].Start.LocalDateTime.ToString("g")), 0);
        Add(labels, Label(displayed[^1].Start.LocalDateTime.ToString("g")), 0, 1);
        _timeline.Children.Add(labels);
    }
    private void Reset()
    {
        _filters.Clear();
        _facetValues.Clear();
        _window = null;
        _range.SelectedIndex = 0;
        _scope.SelectedIndex = 0;
        _search.Text = "";
        _regex.IsChecked = false;
    }

    private void Preset(string? field, string value)
    {
        Reset();
        if (field == null) _search.Text = value;
        else _filters[field] = value;
        RequestScan();
    }

    private void SetWindow(DateTimeOffset from, DateTimeOffset until)
    {
        _window = (from, until);
        RequestScan();
    }

    private void ApplySelected(string field, Func<ObservedLog, string?> read)
    {
        if (_rows.SelectedItem is not ObservedLog log || read(log) is not { } value) return;
        _facetValues.Remove(field);
        _filters[field] = value;
        RequestScan();
    }

    private void Navigate(int direction)
    {
        if (_matches.Count == 0) return;
        _rows.SelectedIndex = Math.Clamp(_rows.SelectedIndex + direction, 0, _matches.Count - 1);
        _rows.ScrollIntoView(_rows.SelectedIndex);
    }

    private void CloseInspector() => _rows.SelectedIndex = -1;

    private void UpdateInspector()
    {
        var log = _rows.SelectedItem as ObservedLog;
        _inspector.IsVisible = log != null;
        if (log == null) return;
        _curl.IsEnabled = CreateCurl(log) != null;
        var json = log.Payload is { } payload
            ? JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }) : log.Message;
        Highlight(_detail, $"{log.File}:{log.Line}\n{log.Timestamp:O}   {log.Level}   {log.Service}\n" +
            $"Host {log.Host ?? "—"}   HTTP {log.Status?.ToString() ?? "—"}   Trace {log.Trace ?? "—"}\n\n{json}", 32768, _language.SelectedItem is string mode && mode != "Auto" ? mode : DetectLanguage(log.File, log.Message), log.File);
    }

    private static string? CreateCurl(ObservedLog? log)
    {
        if (log is not { Method: { } method, Url: { } url } ||
            !Regex.IsMatch(method, @"^[A-Za-z]+$") ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            uri.UserInfo.Length > 0) return null;
        static string Quote(string value) => "'" + value.Replace("'", "'\"'\"'") + "'";
        var header = log.Trace is { } trace && !trace.Any(char.IsControl) ? $" -H {Quote("X-Trace-Id: " + trace)}" : "";
        return $"curl -X {method}{header} -- {Quote(uri.AbsoluteUri)}";
    }

    private async Task CopyAsync(string? value)
    {
        if (value == null || _owner.Clipboard == null) return;
        try { await _owner.Clipboard.SetTextAsync(value); }
        catch (Exception error) { _status.Text = error.Message; }
    }

    private async Task ExportAsync()
    {
        try
        {
            var destination = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export filtered logs", SuggestedFileName = "logs.json",
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });
            if (destination == null) return;
            var snapshot = _matches.ToArray();
            await using var stream = await destination.OpenWriteAsync();
            if (stream.CanSeek) stream.SetLength(0);
            await JsonSerializer.SerializeAsync(stream, snapshot);
            _status.Text = $"Exported {snapshot.Length:N0} logs";
        }
        catch (Exception error) { _status.Text = error.Message; }
    }

    private void ShowColumns()
    {
        var menu = new ContextMenu();
        for (var index = 0; index < Columns.Length; index++)
        {
            var column = index;
            var item = new MenuItem { Header = Columns[index], ToggleType = MenuItemToggleType.CheckBox, IsChecked = _columns[index] };
            item.Click += (_, _) => { _columns[column] = item.IsChecked; _columnsChanged(); };
            menu.Items.Add(item);
        }
        menu.Open(this);
    }

    private void RefreshRows(ContentControl heading)
    {
        heading.Content = BuildRow(Columns, null);
        var template = _rows.ItemTemplate;
        _rows.ItemTemplate = null;
        _rows.ItemTemplate = template;
    }

    private Control BuildRow(string?[] values, ObservedLog? log)
    {
        var row = new Grid { ColumnSpacing = 7, Margin = new Thickness(5, _compact.IsChecked == true ? 1 : 4) };
        for (var index = 0; index < Columns.Length; index++)
        {
            if (!_columns[index]) continue;
            var column = row.ColumnDefinitions.Count;
            row.ColumnDefinitions.Add(new ColumnDefinition(Widths[index] == 0 ? GridLength.Star : new GridLength(Widths[index])));
            var label = Label(values[index] ?? "—", 12);
            label.FontFamily = Mono;
            if (log == null) { label.Foreground = Muted; label.FontSize = 11; label.FontWeight = FontWeight.Medium; }
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            if (index == 5 && log != null)
            {
                var message = values[index] ?? "—";
                if (_wrap.IsChecked == true) label.TextWrapping = TextWrapping.Wrap;
                else if (message.IndexOfAny(['\r', '\n']) is var newline && newline >= 0)
                    message = message[..newline] + " …";
                Highlight(label, message, 512, _language.SelectedItem as string, log.File);
            }
            if (log != null && index == 1)
                label.Foreground = Accent(log.Level);
            if (index == 4 && log?.Latency is >= 500)
                label.Foreground = Accent(log.Latency >= 1000 ? "ERROR" : "WARN");
            if (log != null && index is 2 or 3 && values[index] is { } value)
            {
                var field = index == 2 ? "Service" : "Trace";
                var link = Action(value, () =>
                {
                    _facetValues.Remove(field);
                    _filters[field] = value;
                    RequestScan();
                });
                link.Foreground = Accent(index == 2 ? "Service" : "Unknown");
                link.FontFamily = Mono;
                link.Background = Brushes.Transparent;
                link.BorderThickness = new Thickness(0);
                Add(row, link, 0, column);
            }
            else if (log != null && index == 1 && log.Level != null)
                Add(row, new Border
                {
                    Child = label, Background = new SolidColorBrush(Color.FromArgb(26,
                        ((ISolidColorBrush)label.Foreground!).Color.R, ((ISolidColorBrush)label.Foreground!).Color.G,
                        ((ISolidColorBrush)label.Foreground!).Color.B)),
                    Padding = new Thickness(5, 1), CornerRadius = new CornerRadius(3),
                    HorizontalAlignment = HorizontalAlignment.Left
                }, 0, column);
            else Add(row, label, 0, column);
        }
        if (log?.Level is "ERROR" or "FATAL" or "WARN")
            return new Border { Child = row,
                BorderBrush = Tint(Accent(log.Level), 135), BorderThickness = new Thickness(2, 0, 0, 0) };
        return row;
    }

    private static ISolidColorBrush Accent(string? level) => level switch
    {
        "ERROR" or "FATAL" or "5xx" => Rose,
        "WARN" or "4xx" => Amber,
        "INFO" or "2xx" => Emerald,
        "DEBUG" or "TRACE" => Blue,
        "Service" => Indigo,
        _ => Muted
    };

    private static readonly ISolidColorBrush Rose = new SolidColorBrush(Color.Parse("#fb7185"));
    private static readonly ISolidColorBrush Amber = new SolidColorBrush(Color.Parse("#fbbf24"));
    private static readonly ISolidColorBrush Emerald = new SolidColorBrush(Color.Parse("#34d399"));
    private static readonly ISolidColorBrush Indigo = new SolidColorBrush(Color.Parse("#a5b4fc"));

    private static IBrush Tint(ISolidColorBrush brush, byte opacity) =>
        new SolidColorBrush(Color.FromArgb(opacity, brush.Color.R, brush.Color.G, brush.Color.B));

    private static TextBlock AccentLabel(string text, string level)
    {
        var label = Label(text, 11);
        label.Foreground = Accent(level);
        label.FontFamily = Mono;
        return label;
    }

    private const string Locations = """(?<url>\b(?:https?|ftp)://[^\s"'<>]+)|(?<path>(?:\b[A-Za-z]:[\\/]|\\\\(?=[\w.-]))[^\r\n"'<>;|]+|(?<![\w/])/(?:[\w.~%:@{}-]+/)*[\w.~%:@{}-]+(?:\?[^\s"'<>]+)?)""";
    private const string Values = """(?<key>"(?:\\.|[^"\\])*"(?=\s*:))|(?<string>"(?:\\.|[^"\\])*")|(?<exception>\b[\w.]*Exception\b)|(?<frame>^[ \t]+at[ \t]+[^\r\n]+)|(?<literal>\b(?:true|false|null|True|False|None)\b)|(?<number>(?<![\w.])-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?(?![\w.]))""";

    private static Regex TokenPattern(string extra = "") => new(Locations + "|" + Values + extra,
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex Syntax = TokenPattern();
    private static readonly Regex LocationSyntax = new(Locations, RegexOptions.CultureInvariant);
    private static readonly IReadOnlyDictionary<string, Regex> CodeSyntax = new Dictionary<string, Regex>
    {
        ["C#"] = Keywords("using|namespace|class|struct|record|public|private|internal|static|var|new|return|async|await|throw|try|catch|if|else|foreach|void|string|int|bool"),
        ["JavaScript"] = Keywords("const|let|var|function|class|import|export|from|return|async|await|new|throw|try|catch|if|else|undefined"),
        ["TypeScript"] = Keywords("interface|type|enum|implements|extends|readonly|const|let|function|class|import|export|from|return|async|await|new|if|else"),
        ["Python"] = Keywords("def|class|from|import|return|yield|async|await|raise|try|except|with|if|elif|else|for|in|lambda|self"),
        ["Java"] = Keywords("package|import|class|interface|public|private|protected|final|static|void|new|return|throws|throw|try|catch|if|else|int|boolean"),
        ["SQL"] = TokenPattern(@"|(?<code>\b(?i:select|from|where|insert|into|update|set|delete|join|on|group|order|by|having|values|create|table|as|and|or)\b)"),
        ["HTML/XML"] = TokenPattern(@"|(?<code></?[A-Za-z][\w:-]*)"),
        ["Shell"] = Keywords("export|function|if|then|else|fi|for|do|done|case|esac|echo|sudo|cd|grep|curl")
    };

    private static Regex Keywords(string words) => TokenPattern($@"|(?<code>\b(?:{words})\b)");

    private static string? DetectLanguage(string? file, string text)
    {
        var snippet = text.AsSpan().TrimStart();
        if (snippet.StartsWith("```", StringComparison.Ordinal))
        {
            var end = snippet.IndexOfAny('\r', '\n');
            var tag = (end < 0 ? snippet[3..] : snippet[3..end]).Trim().ToString().ToLowerInvariant();
            var language = tag switch
            {
                "cs" or "csharp" => "C#", "js" or "javascript" => "JavaScript",
                "ts" or "typescript" => "TypeScript", "py" or "python" => "Python",
                "java" => "Java", "sql" => "SQL", "html" or "xml" => "HTML/XML",
                "sh" or "bash" or "shell" => "Shell", "json" => "JSON", _ => null
            };
            if (language != null) return language;
        }

        return Path.GetExtension(file)?.ToLowerInvariant() switch
        {
            ".cs" => "C#", ".js" or ".jsx" => "JavaScript", ".ts" or ".tsx" => "TypeScript",
            ".py" => "Python", ".java" => "Java", ".sql" => "SQL",
            ".html" or ".xml" => "HTML/XML", ".sh" or ".ps1" => "Shell", _ => null
        };
    }
    private static readonly IBrush SyntaxString = new SolidColorBrush(Color.Parse("#86efac"));
    private static readonly IBrush SyntaxNumber = new SolidColorBrush(Color.Parse("#fcd34d"));
    private static readonly IBrush SyntaxLiteral = new SolidColorBrush(Color.Parse("#c4b5fd"));
    private static readonly IBrush SyntaxException = new SolidColorBrush(Color.Parse("#fda4af"));

    internal static void Highlight(TextBlock target, string text, int limit, string? language = null, string? file = null)
    {
        // shortcut: color a bounded prefix to keep large entries responsive; use token virtualization for larger ranges.
        var prefix = text[..Math.Min(text.Length, limit)];
        var runs = new InlineCollection();
        var offset = 0;
        if (language is null or "Auto") language = DetectLanguage(file, prefix);
        var pattern = language == "Plain Text" ? LocationSyntax
            : language != null && CodeSyntax.TryGetValue(language, out var code) ? code : Syntax;
        foreach (Match match in pattern.Matches(prefix))
        {
            if (match.Index > offset) runs.Add(new Run(text[offset..match.Index]));
            var quotedLocation = match.Groups["string"].Success
                ? LocationSyntax.Match(match.Value[1..^1]) : Match.Empty;
            var color = match.Groups["url"].Success || quotedLocation.Groups["url"].Success ? Blue
                : match.Groups["path"].Success || quotedLocation.Groups["path"].Success ? Amber
                : match.Groups["code"].Success ? SyntaxLiteral
                : match.Groups["key"].Success ? Blue
                : match.Groups["string"].Success ? SyntaxString
                : match.Groups["exception"].Success ? SyntaxException
                : match.Groups["frame"].Success ? Muted
                : match.Groups["literal"].Success ? SyntaxLiteral : SyntaxNumber;
            runs.Add(new Run(match.Value) { Foreground = color });
            offset = match.Index + match.Length;
        }

        if (offset < text.Length) runs.Add(new Run(text[offset..]));
        target.Text = null;
        target.Inlines = runs;
    }
    private static TextBlock Label(string text, double size = 12) => new()
    {
        Text = text, FontSize = size, Foreground = Ink, VerticalAlignment = VerticalAlignment.Center
    };

    private static Control Icon(string path, IBrush stroke) => new global::Avalonia.Controls.Shapes.Path
    {
        Data = Geometry.Parse(path), Stroke = stroke, StrokeThickness = 1.8, Width = 15, Height = 15,
        Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false
    };

    private static Button IconAction(string description, string path, Action action)
    {
        var button = Action("", action);
        button.Content = Icon(path, Muted);
        ToolTip.SetTip(button, description);
        global::Avalonia.Automation.AutomationProperties.SetName(button, description);
        return button;
    }
    private static ToggleButton Toggle(string label, bool enabled = false)
    {
        var button = new ToggleButton
        {
            Content = label, IsChecked = enabled, FontSize = 11, Padding = new Thickness(7, 4),
            BorderBrush = Rule, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5)
        };
        void Restyle()
        {
            button.Background = button.IsChecked == true ? new SolidColorBrush(Color.Parse("#10242e")) : Panel;
            button.Foreground = button.IsChecked == true ? Blue : Muted;
        }
        button.IsCheckedChanged += (_, _) => Restyle();
        Restyle();
        return button;
    }
    private static Button Action(string text, Action action)
    {
        var button = new Button { Content = text, FontSize = 12, Padding = new Thickness(7, 4), Background = Panel,
            BorderBrush = Rule, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5) };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button AsyncAction(string text, Func<Task> action)
    {
        var button = new Button { Content = text, FontSize = 12, Padding = new Thickness(7, 4), Background = Panel,
            BorderBrush = Rule, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5) };
        button.Click += async (_, _) => await action();
        return button;
    }

    private static WrapPanel Row(params Control[] children)
    {
        var row = new WrapPanel { ItemSpacing = 6 };
        foreach (var child in children) row.Children.Add(child);
        return row;
    }

    private static void Add(Grid grid, Control control, int row, int column = 0)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    public void Dispose()
    {
        _disposed = true;
        Cancel();
        _watch.Stop();
    }
}
