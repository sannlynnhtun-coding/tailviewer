using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Media;
using Tailviewer.Settings;

namespace Tailviewer.Avalonia.Views;

public sealed class SourcesView : StackPanel
{
    private readonly TextBlock _status = new() { FontSize = 11, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, IsVisible = false };
    public DataSource Source { get; }
    private readonly Border _dropPosition = new() { BorderBrush = Brushes.DodgerBlue, IsVisible = false, IsHitTestVisible = false };

    public SourcesView(DataSource source, Control heading)
    {
        Source = source;
        AutomationProperties.SetName(_status, "Source status");
        Children.Add(new Grid { Children = { heading, _dropPosition } });
        Children.Add(_status);
    }

    public void UpdateStatus(string? text)
    {
        if (_status.Text != text) _status.Text = text;
        _status.IsVisible = !string.IsNullOrEmpty(text);
    }

    internal void ShowDropPosition(bool visible, bool? after)
    {
        _dropPosition.BorderThickness = after == null ? new Thickness(1) : after.Value ? new Thickness(0, 0, 0, 2) : new Thickness(0, 2, 0, 0);
        _dropPosition.IsVisible = visible;
    }
}
