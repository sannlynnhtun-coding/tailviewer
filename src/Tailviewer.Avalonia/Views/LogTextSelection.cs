using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Tailviewer.Avalonia.Views;

public sealed class LogTextSelection
{
    private readonly ListBox _rows;
    private readonly Action _changed;
    private LogDocument? _document;
    private LogSelection? _selection;
    private long _revision;
    private IPointer? _pointer;

    public LogTextSelection(ListBox rows, Action changed)
    {
        _rows = rows;
        _changed = changed;
        rows.AddHandler(InputElement.PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);
        rows.AddHandler(InputElement.PointerMovedEvent, Moved, RoutingStrategies.Tunnel);
        rows.AddHandler(InputElement.PointerReleasedEvent, Released, RoutingStrategies.Tunnel);
        rows.PointerCaptureLost += (_, _) => _pointer = null;
    }

    public bool HasSelection => _document != null && _document.Revision == _revision &&
        _selection is { } range && (range.AnchorLine != range.ActiveLine || range.AnchorColumn != range.ActiveColumn);

    public Task<string>? CopyTextAsync() => HasSelection
        ? _document!.CopyTextAsync(_selection!.Value, _revision) : null;

    public void BindDocument(LogDocument? document)
    {
        Clear();
        _document = document;
    }

    public void Clear()
    {
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
        else Highlight();
    }

    private IEnumerable<SelectableTextBlock> Blocks => _rows.GetVisualDescendants().OfType<SelectableTextBlock>()
        .Where(block => block.DataContext is LogLine { Text: not null });

    private (int Line, int Column)? Position(PointerEventArgs args)
    {
        // shortcut: drag endpoints follow realized rows; add edge autoscroll with the viewport selection phase.
        var block = Blocks.FirstOrDefault(candidate =>
        {
            var point = args.GetPosition(candidate);
            return point.Y >= 0 && point.Y < candidate.Bounds.Height;
        });
        if (block?.DataContext is not LogLine { Text: { } text } row) return null;
        var hit = block.TextLayout.HitTestPoint(args.GetPosition(block));
        var prefix = row.DisplayText.Length - text.Length;
        return (row.Index, Math.Clamp(hit.TextPosition - prefix, 0, text.Length));
    }

    private void Pressed(object? sender, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(_rows).Properties.IsLeftButtonPressed) return;
        if (args.KeyModifiers != KeyModifiers.Alt || _document == null)
        {
            Clear();
            return;
        }
        if (Position(args) is not { } position) return;
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

    private void Moved(object? sender, PointerEventArgs args)
    {
        if (_pointer != args.Pointer || _selection is not { } range) return;
        if (_document?.Revision != _revision) { Clear(); return; }
        if (Position(args) is { } position)
        {
            _selection = range with { ActiveLine = position.Line, ActiveColumn = position.Column };
            Highlight();
            _changed();
        }
        args.Handled = true;
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
