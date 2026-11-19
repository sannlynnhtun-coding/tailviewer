using Avalonia.Automation;
using Avalonia.Controls;
using Tailviewer.Settings;

namespace Tailviewer.Avalonia.Views;

public sealed class SourcesView : StackPanel
{
    private readonly TextBlock _status = new() { FontSize = 11, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap, IsVisible = false };
    public DataSource Source { get; }

    public SourcesView(DataSource source, Control heading)
    {
        Source = source;
        AutomationProperties.SetName(_status, "Source status");
        Children.Add(heading);
        Children.Add(_status);
    }

    public void UpdateStatus(string? text)
    {
        if (_status.Text != text) _status.Text = text;
        _status.IsVisible = !string.IsNullOrEmpty(text);
    }
}