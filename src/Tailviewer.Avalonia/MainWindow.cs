using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Tailviewer.Avalonia;

public sealed class MainWindow : Window
{
    private readonly Button _open = new() { Content = "_Open file…" };
    private readonly Button _copy = new() { Content = "_Copy selected", IsEnabled = false };
    private readonly CheckBox _follow = new() { Content = "Follow _tail", IsChecked = true };
    private readonly TextBlock _path = new() { Text = "Open a log file to begin.", TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _status = new() { Text = "Select lines with Shift/Ctrl • Ctrl/Cmd+C to copy" };
    private readonly ListBox _rows = new() { SelectionMode = SelectionMode.Multiple };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private LogDocument? _document;
    private string? _operationError;
    private bool _closed;

    public MainWindow(string? fileName = null)
    {
        Title = "Tailviewer — Avalonia prototype";
        Width = 1100;
        Height = 700;
        MinWidth = 640;
        MinHeight = 360;
        AutomationProperties.SetName(_rows, "Log lines");
        ScrollViewer.SetHorizontalScrollBarVisibility(_rows, ScrollBarVisibility.Auto);
        _rows.ItemTemplate = new FuncDataTemplate<int>((index, _) =>
        {
            var row = _document?.GetLine(index);
            return new TextBlock
            {
                DataContext = row,
                FontFamily = new FontFamily("Cascadia Mono,Consolas,Menlo,monospace"),
                FontSize = 14,
                [!TextBlock.TextProperty] = new Binding(nameof(LogLine.DisplayText))
            };
        });
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { _open, _copy, _follow }
        };
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), Margin = new Thickness(12), RowSpacing = 8 };
        layout.Children.Add(toolbar);
        Grid.SetRow(_path, 1);
        Grid.SetRow(_rows, 2);
        Grid.SetRow(_status, 3);
        layout.Children.Add(_path);
        layout.Children.Add(_rows);
        layout.Children.Add(_status);
        Content = layout;

        _open.Click += async (_, _) => await PickFileAsync();
        _copy.Click += async (_, _) => await CopyAsync();
        _follow.IsCheckedChanged += (_, _) => ScrollToTail();
        _rows.SelectionChanged += (_, _) =>
        {
            _copy.IsEnabled = _rows.Selection.SelectedIndexes.Count > 0;
            if (_copy.IsEnabled)
                _follow.IsChecked = false;
        };
        _rows.AddHandler(PointerWheelChangedEvent, (_, _) => _follow.IsChecked = false, RoutingStrategies.Tunnel);
        _rows.AddHandler(PointerPressedEvent, (_, _) => _follow.IsChecked = false, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        _timer.Tick += (_, _) => RefreshDocument();
        Closed += (_, _) =>
        {
            _closed = true;
            _timer.Stop();
            _document?.Dispose();
        };
        _timer.Start();
        if (fileName != null)
            Opened += async (_, _) => await OpenFileAsync(fileName);
    }

    private async Task PickFileAsync()
    {
        try
        {
            if (!StorageProvider.CanOpen)
                throw new NotSupportedException("File picking is unavailable. Pass a local file path on the command line.");
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open log file", AllowMultiple = false
            });
            if (files.Count == 0)
                return;
            var path = files[0].TryGetLocalPath();
            if (path == null)
                throw new NotSupportedException("This prototype opens local files only.");
            await OpenFileAsync(path);
        }
        catch (Exception ex)
        {
            _status.Text = _operationError = ex.Message;
        }
    }

    public async Task OpenFileAsync(string fileName)
    {
        _open.IsEnabled = false;
        try
        {
            var document = await Task.Run(() => new LogDocument(fileName));
            if (_closed)
            {
                document.Dispose();
                return;
            }
            _rows.ItemsSource = null;
            _document?.Dispose();
            _document = document;
            _operationError = null;
            _rows.ItemsSource = document.Lines;
            _follow.IsChecked = true;
            _path.Text = document.FileName;
            Title = $"{Path.GetFileName(document.FileName)} — Tailviewer prototype";
            RefreshDocument();
        }
        catch (Exception ex)
        {
            _status.Text = _operationError = $"Cannot open file: {ex.Message}";
        }
        finally
        {
            _open.IsEnabled = !_closed;
        }
    }

    private void RefreshDocument()
    {
        if (_document == null)
            return;
        if (_document.Refresh())
            ScrollToTail();
        _status.Text = _operationError ?? _document.Error ?? $"{_document.Lines.Count:N0} lines • {(_follow.IsChecked == true ? "Following tail" : "Paused — enable Follow tail to jump to the end")}";
    }

    private void ScrollToTail()
    {
        if (_follow.IsChecked == true && _document?.Lines.Count > 0)
            _rows.ScrollIntoView(_document.Lines.Count - 1);
    }

    private async Task CopyAsync()
    {
        var document = _document;
        var indices = _rows.Selection.SelectedIndexes.ToArray();
        if (document == null || indices.Length == 0)
            return;
        try
        {
            var text = await document.CopyTextAsync(indices);
            if (document != _document || _closed)
                return;
            if (Clipboard == null)
                throw new NotSupportedException("Clipboard is unavailable.");
            await Clipboard.SetTextAsync(text);
            await Clipboard.FlushAsync();
            _operationError = null;
        }
        catch (Exception ex)
        {
            if (document == _document && !_closed)
                _status.Text = _operationError = $"Cannot copy: {ex.Message}";
        }
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && e.Key is Key.O or Key.C)
        {
            e.Handled = true;
            if (e.Key == Key.O && _open.IsEnabled)
                await PickFileAsync();
            else if (e.Key == Key.C)
                await CopyAsync();
        }
        else if (_rows.IsKeyboardFocusWithin && e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown or Key.Home or Key.End)
        {
            _follow.IsChecked = false;
        }
    }
}
