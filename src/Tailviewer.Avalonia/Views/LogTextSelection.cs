using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.Presenters;

namespace Tailviewer.Avalonia.Views;

public sealed class LogTextSelection
{
    private readonly ListBox _rows;
    private readonly Action _changed;
    private LogDocument? _document;
    private LogSelection? _selection;
    private long _revision;
    private IPointer? _pointer;
    private Point _dragPoint;
    private long _inputEpoch;
    private Task _keyWork = Task.CompletedTask;
    private int? _preferredColumn;
    public event Action<string>? Failed;

    public LogTextSelection(ListBox rows, Action changed)
    {
        _rows = rows;
        _changed = changed;
        rows.AddHandler(InputElement.PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);
        rows.AddHandler(InputElement.PointerMovedEvent, Moved, RoutingStrategies.Tunnel);
        rows.AddHandler(InputElement.PointerReleasedEvent, Released, RoutingStrategies.Tunnel);
        rows.PointerCaptureLost += (_, _) => _pointer = null;
        rows.AddHandler(InputElement.KeyDownEvent, KeyDown, RoutingStrategies.Tunnel);
    }

    public bool HasSelection => _document != null && _document.Revision == _revision &&
        _selection is { } range && (range.AnchorLine != range.ActiveLine || range.AnchorColumn != range.ActiveColumn);

    public bool CanCopy => HasSelection || (_selection != null && !_keyWork.IsCompleted);

    public Task<string>? CopyTextAsync() => CanCopy ? CopyAfterKeysAsync(_keyWork, _document!, _inputEpoch) : null;

    private async Task<string> CopyAfterKeysAsync(Task pending, LogDocument document, long epoch)
    {
        await pending;
        if (_document != document || epoch != _inputEpoch || !HasSelection)
            throw new IOException("The selection changed. Select the text and try again.");
        return await document.CopyTextAsync(_selection!.Value, _revision);
    }

    public void BindDocument(LogDocument? document)
    {
        Clear();
        _document = document;
    }

    public void Clear()
    {
        ++_inputEpoch;
        _preferredColumn = null;
        var hadSelection = _selection != null;
        _selection = null;
        var pointer = _pointer;
        _pointer = null;
        pointer?.Capture(null);
        Highlight();
        if (hadSelection) _changed();
    }

    public void Refresh()
    {
        if (_selection == null) return;
        if (_document?.Revision != _revision) Clear();
        else
        {
            if (_pointer != null)
            {
                UpdateDrag();
                ScrollEdges();
            }
            Highlight();
        }
    }

    private IEnumerable<SelectableTextBlock> Blocks => _rows.GetVisualDescendants().OfType<SelectableTextBlock>()
        .Where(block => block.DataContext is LogLine { Text: not null });

    private Rect Viewport
    {
        get
        {
            var presenter = _rows.GetVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault();
            return presenter?.TranslatePoint(default, _rows) is { } origin
                ? new Rect(origin, presenter.Bounds.Size) : new Rect(_rows.Bounds.Size);
        }
    }

    private IEnumerable<SelectableTextBlock> VisibleBlocks
    {
        get
        {
            var viewport = Viewport;
            return Blocks.Where(block => block.TranslatePoint(default, _rows) is { } origin &&
                new Rect(origin, block.Bounds.Size).Intersects(viewport));
        }
    }

    private (int Line, int Column)? Position(Point point)
    {
        var viewport = Viewport;
        point = new Point(Math.Clamp(point.X, viewport.Left, viewport.Right), Math.Clamp(point.Y, viewport.Top, viewport.Bottom));
        var block = VisibleBlocks.OrderBy(candidate =>
        {
            var local = _rows.TranslatePoint(point, candidate)!.Value;
            return local.Y < 0 ? -local.Y : Math.Max(0, local.Y - candidate.Bounds.Height);
        }).FirstOrDefault();
        if (block?.DataContext is not LogLine { Text: { } text } row) return null;
        var localPoint = _rows.TranslatePoint(point, block)!.Value;
        var hit = block.TextLayout.HitTestPoint(new Point(localPoint.X, Math.Clamp(localPoint.Y, 0, block.Bounds.Height)));
        var prefix = row.DisplayText.Length - text.Length;
        return (row.Index, Math.Clamp(hit.TextPosition - prefix, 0, text.Length));
    }

    private void ScrollEdges()
    {
        if (_document == null || _rows.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is not { } scroll) return;
        var viewport = Viewport;
        const double edge = 16;
        var indices = VisibleBlocks.Select(block => ((LogLine)block.DataContext!).Index).Order().ToArray();
        if (indices.Length == 0) return;
        if (_dragPoint.Y < viewport.Top + edge && indices[0] > 0) _rows.ScrollIntoView(indices[0] - 1);
        else if (_dragPoint.Y > viewport.Bottom - edge && indices[^1] + 1 < _document.Lines.Count) _rows.ScrollIntoView(indices[^1] + 1);
        if (_dragPoint.X < viewport.Left + edge) scroll.LineLeft();
        else if (_dragPoint.X > viewport.Right - edge) scroll.LineRight();
    }

    private void Pressed(object? sender, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(_rows).Properties.IsLeftButtonPressed) return;
        if (args.KeyModifiers != KeyModifiers.Alt || _document == null)
        {
            Clear();
            return;
        }
        _dragPoint = args.GetPosition(_rows);
        if (Position(_dragPoint) is not { } position) return;
        Clear();
        _rows.Selection.Clear();
        _revision = _document.Revision;
        _selection = new(position.Line, position.Column, position.Line, position.Column);
        _pointer = args.Pointer;
        args.Pointer.Capture(_rows);
        _rows.Focus();
        args.Handled = true;
        Highlight();
        _changed();
    }

    private async void KeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape && _selection != null)
        {
            Clear();
            args.Handled = true;
            return;
        }
        if (_document == null || _document.Lines.Count == 0 ||
            args.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End) ||
            !(args.KeyModifiers == (KeyModifiers.Alt | KeyModifiers.Shift) ||
              (args.KeyModifiers == KeyModifiers.Shift && _selection != null))) return;
        args.Handled = true;
        if (_selection == null)
        {
            var line = _rows.SelectedIndex >= 0 ? _rows.SelectedIndex : 0;
            _rows.Selection.Clear();
            _revision = _document.Revision;
            _selection = new(line, 0, line, 0);
        }
        var epoch = _inputEpoch;
        var pending = _keyWork = MoveAsync(_keyWork, _document, epoch, args.Key);
        _changed();
        await pending;
        if (epoch == _inputEpoch) _changed();
    }

    private async Task MoveAsync(Task previous, LogDocument document, long epoch, Key key)
    {
        await previous;
        if (_document != document || epoch != _inputEpoch || _selection is not { } range) return;
        try
        {
            if (document.Revision != _revision) { Clear(); return; }
            var line = range.ActiveLine;
            var column = range.ActiveColumn;
            var preferredColumn = _preferredColumn;
            var text = await document.CopyTextAsync(new[] { line });
            if (key is Key.Up or Key.Down)
            {
                preferredColumn ??= column;
                line = Math.Clamp(line + (key == Key.Up ? -1 : 1), 0, document.Lines.Count - 1);
                text = await document.CopyTextAsync(new[] { line });
                column = StringInfo.ParseCombiningCharacters(text).Append(text.Length)
                    .Last(offset => offset <= Math.Min(preferredColumn.Value, text.Length));
            }
            else
            {
                preferredColumn = null;
                if (key == Key.Home) column = 0;
                else if (key == Key.End) column = text.Length;
                else if (key == Key.Right && column < text.Length)
                    column = StringInfo.ParseCombiningCharacters(text).Append(text.Length).First(offset => offset > column);
                else if (key == Key.Left && column > 0)
                    column = StringInfo.ParseCombiningCharacters(text).Last(offset => offset < column);
                else if (key == Key.Right && line + 1 < document.Lines.Count) { ++line; column = 0; }
                else if (key == Key.Left && line > 0)
                {
                    --line;
                    column = (await document.CopyTextAsync(new[] { line })).Length;
                }
            }
            if (_document != document || epoch != _inputEpoch) return;
            if (document.Revision != _revision) { Clear(); return; }
            _preferredColumn = preferredColumn;
            _selection = range with { ActiveLine = line, ActiveColumn = column };
            _rows.ScrollIntoView(line);
            Highlight();
            _changed();
        }
        catch (Exception error)
        {
            if (_document != document || epoch != _inputEpoch) return;
            Clear();
            Failed?.Invoke("Cannot select text: " + error.Message);
        }
    }

    private void Moved(object? sender, PointerEventArgs args)
    {
        if (_pointer != args.Pointer || _selection == null) return;
        if (_document?.Revision != _revision) { Clear(); return; }
        _dragPoint = args.GetPosition(_rows);
        UpdateDrag();
        args.Handled = true;
    }

    private void UpdateDrag()
    {
        if (_selection is { } range && Position(_dragPoint) is { } position)
        {
            _selection = range with { ActiveLine = position.Line, ActiveColumn = position.Column };
            Highlight();
            _changed();
        }
    }

    private void Released(object? sender, PointerReleasedEventArgs args)
    {
        if (_pointer != args.Pointer) return;
        Moved(sender, args);
        _pointer = null;
        args.Pointer.Capture(null);
        args.Handled = true;
    }

    private void Highlight()
    {
        var start = _selection is { } selection ? (selection.AnchorLine, selection.AnchorColumn) : (-1, 0);
        var end = _selection is { } range ? (range.ActiveLine, range.ActiveColumn) : (-1, 0);
        if (start.CompareTo(end) > 0) (start, end) = (end, start);
        foreach (var block in Blocks)
        {
            var row = (LogLine)block.DataContext!;
            var length = row.Text!.Length;
            var prefix = row.DisplayText.Length - length;
            var selected = row.Index >= start.Item1 && row.Index <= end.Item1;
            block.SelectionStart = selected ? prefix + (row.Index == start.Item1 ? Math.Min(start.Item2, length) : 0) : 0;
            block.SelectionEnd = selected ? prefix + (row.Index == end.Item1 ? Math.Min(end.Item2, length) : length) : 0;
        }
    }
}
