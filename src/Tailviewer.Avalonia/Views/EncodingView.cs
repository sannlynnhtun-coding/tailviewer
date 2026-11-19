using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Tailviewer.Settings;
using Tailviewer.Core;

namespace Tailviewer.Avalonia.Views;

public sealed class EncodingView : UserControl
{
    public ILogFileSettings Settings { get; }
    public event Action? Changed;

    public EncodingView(ILogFileSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
        var choices = new List<ComboBoxItem> { new() { Content = "Auto detect" } };
#pragma warning disable SYSLIB0001 // Retain the explicit legacy UTF-7 choice; never select it automatically.
        var encodings = new[] { Encoding.ASCII, Encoding.UTF8, Encoding.UTF7,
            Encoding.UTF32, Encoding.BigEndianUnicode, Encoding.Unicode, Encoding.Default };
#pragma warning restore SYSLIB0001
        foreach (var encoding in encodings.DistinctBy(value => value.WebName))
            choices.Add(new ComboBoxItem { Content = encoding.WebName, Tag = encoding });
        var selected = choices.FirstOrDefault(item => Equals(item.Tag, settings.DefaultEncoding));
        if (settings.DefaultEncoding is { } custom && selected == null)
        {
            selected = new ComboBoxItem { Content = custom.WebName, Tag = custom };
            choices.Add(selected);
        }
        if (settings is LogFileSettings { IsEncodingAvailable: false } unavailable)
        {
            selected = new ComboBoxItem { Content = "Unavailable: " + unavailable.EncodingName, IsEnabled = false };
            choices.Add(selected);
        }
        var selector = new ComboBox { ItemsSource = choices, SelectedItem = selected, MinWidth = 160 };
        AutomationProperties.SetName(selector, "Text encoding");
        selector.SelectionChanged += (_, _) =>
        {
            if (selector.SelectedItem is not ComboBoxItem { IsEnabled: true } choice) return;
            Settings.DefaultEncoding = choice.Tag as Encoding;
            Changed?.Invoke();
        };
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { new TextBlock { Text = "Text encoding:", VerticalAlignment = VerticalAlignment.Center }, selector }
        };
    }
}
