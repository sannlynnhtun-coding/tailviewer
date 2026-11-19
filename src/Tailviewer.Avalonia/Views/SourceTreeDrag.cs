using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Diagnostics;
using Tailviewer.Api;
using Tailviewer.Avalonia.ViewModels;
using Tailviewer.Settings;

namespace Tailviewer.Avalonia.Views;

internal sealed class SourceTreeDrag
{
    private static readonly Cursor MoveCursor = new(StandardCursorType.SizeAll);
    private static readonly Cursor NoDropCursor = new(StandardCursorType.No);
    private readonly TreeView _tree;
    private readonly SourcesViewModel _workspace;
    private readonly Func<DataSource, DataSource, bool?, Task> _apply;
    private DataSource? _source;
    private SourcesView? _target;
    private IPointer? _pointer;
    private Point _start;
    private Point _point;
    private bool _dragging;
    private bool? _after;
    private DataSourceId _hoverGroup;
    private long _hoverStarted;

    public bool IsDragging => _dragging;

    public SourceTreeDrag(TreeView tree, SourcesViewModel workspace, Func<DataSource, DataSource, bool?, Task> apply)
    {
        _tree = tree;
        _workspace = workspace;
        _apply = apply;
        tree.AddHandler(InputElement.PointerPressedEvent, Pressed, RoutingStrategies.Tunnel);
        tree.AddHandler(InputElement.PointerMovedEvent, Moved, RoutingStrategies.Tunnel);
        tree.AddHandler(InputElement.PointerReleasedEvent, Released, RoutingStrategies.Tunnel);
        tree.PointerCaptureLost += (_, _) => { if (_dragging && _pointer?.Captured != tree) Clear(); };
    }

    public void Clear()
    {
        var pointer = _pointer;
        _pointer = null;
        _source = null;
        _dragging = false;
        _target?.ShowDropPosition(false, null);
        _target = null;
        _hoverGroup = DataSourceId.Empty;
        _tree.Cursor = null;
        if (pointer?.Captured == _tree) pointer.Capture(null);
    }

    public void Refresh()
    {
        if (!_dragging || _source == null) return;
        if (!_workspace.Contains(_source.Id)) { Clear(); return; }
        var scroll = _tree.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        var presenter = scroll?.GetVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault();
        if (scroll == null || presenter?.TranslatePoint(default, _tree) is not { } origin) return;
        var viewport = new Rect(origin, presenter.Bounds.Size);
        const double edge = 16;
        if (_point.X >= viewport.Left && _point.X <= viewport.Right &&
            _point.Y >= viewport.Top - edge && _point.Y <= viewport.Bottom + edge)
        {
            if (_point.Y < viewport.Top + edge) scroll.LineUp();
            else if (_point.Y > viewport.Bottom - edge) scroll.LineDown();
        }
        if (_point.Y >= viewport.Top && _point.Y <= viewport.Bottom &&
            _point.X >= viewport.Left - edge && _point.X <= viewport.Right + edge)
        {
            if (_point.X < viewport.Left + edge) scroll.LineLeft();
            else if (_point.X > viewport.Right - edge) scroll.LineRight();
        }
        _tree.UpdateLayout();
        UpdateTarget(_point);
        if (_hoverGroup != DataSourceId.Empty && Stopwatch.GetElapsedTime(_hoverStarted) >= TimeSpan.FromMilliseconds(500) &&
            _workspace.GetChildren(_hoverGroup).Any() && _target?.FindAncestorOfType<TreeViewItem>() is { IsExpanded: false } item)
            item.IsExpanded = true;
    }

    private void Pressed(object? sender, PointerPressedEventArgs args)
    {
        Clear();
        if (!args.GetCurrentPoint(_tree).Properties.IsLeftButtonPressed || args.KeyModifiers != KeyModifiers.None) return;
        var ancestors = (args.Source as Visual)?.GetSelfAndVisualAncestors().ToArray();
        if (ancestors == null || ancestors.Any(item => item is TextBox)) return;
        var source = ancestors.OfType<SourcesView>().FirstOrDefault()?.Source;
        if (source == null || !_workspace.Contains(source.Id) || source.LogFileFolderPath != null) return;
        _source = source;
        _pointer = args.Pointer;
        _start = args.GetPosition(_tree);
    }

    private void Moved(object? sender, PointerEventArgs args)
    {
        if (_source == null || _pointer != args.Pointer) return;
        if (!args.GetCurrentPoint(_tree).Properties.IsLeftButtonPressed) { Clear(); return; }
        _point = args.GetPosition(_tree);
        if (!_dragging)
        {
            var position = args.GetPosition(_tree);
            if (Math.Abs(position.X - _start.X) < 4 && Math.Abs(position.Y - _start.Y) < 4) return;
            _dragging = true;
            args.Pointer.Capture(_tree);
        }
        UpdateTarget(_point);
        args.Handled = true;
    }

    private void UpdateTarget(Point point)
    {
        var previousHover = _hoverGroup;
        var previousStart = _hoverStarted;
        _hoverGroup = DataSourceId.Empty;
        _target?.ShowDropPosition(false, null);
        _target = null;
        _tree.Cursor = NoDropCursor;
        if (_source == null || !_workspace.Contains(_source.Id) || !new Rect(_tree.Bounds.Size).Contains(point)) return;
        var view = (_tree.GetVisualAt(point) as Visual)?.GetSelfAndVisualAncestors().OfType<SourcesView>().FirstOrDefault();
        if (view == null || !_workspace.Contains(view.Source.Id) || view.Source.Id == _source.Id || view.Source.LogFileFolderPath != null) return;
        var fraction = _tree.TranslatePoint(point, view)!.Value.Y / view.Bounds.Height;
        _after = SourcesViewModel.IsGroup(_source) ? fraction >= .5 : fraction < .2 ? false : fraction > .8 ? true : null;
        if (SourcesViewModel.IsGroup(_source) && view.Source.ParentId != DataSourceId.Empty) return;
        var parent = _after == null && SourcesViewModel.IsGroup(view.Source) ? view.Source.Id : view.Source.ParentId;
        if (parent != DataSourceId.Empty && parent != _source.ParentId && _workspace.GetChildren(parent).Count() >= LogEntrySourceId.MaxSources) return;
        _target = view;
        if (_after == null && SourcesViewModel.IsGroup(view.Source))
        {
            _hoverGroup = view.Source.Id;
            _hoverStarted = previousHover == _hoverGroup ? previousStart : Stopwatch.GetTimestamp();
        }
        view.ShowDropPosition(true, _after);
        _tree.Cursor = MoveCursor;
    }

    private async void Released(object? sender, PointerReleasedEventArgs args)
    {
        if (_pointer != args.Pointer || args.GetCurrentPoint(_tree).Properties.IsLeftButtonPressed) return;
        if (!_dragging) { Clear(); return; }
        UpdateTarget(args.GetPosition(_tree));
        var source = _source;
        var target = _target?.Source;
        var after = _after;
        Clear();
        args.Handled = true;
        if (source != null && target != null) await _apply(source, target, after);
    }
}
