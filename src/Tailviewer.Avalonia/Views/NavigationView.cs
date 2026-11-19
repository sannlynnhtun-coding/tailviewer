using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Tailviewer.Avalonia.Views;

public sealed class NavigationView : UserControl
{
    private readonly TextBox _line = new() { Width = 120, PlaceholderText = "Line number" };
    private readonly TextBlock _status = new();
    public LogDocument? Document { get; private set; }
    public event Action<int>? LineChosen;
    public event Action? Cancelled;

    public NavigationView()
    {
        IsVisible = false;
        var go = new Button { Content = "Go" };
        var cancel = new Button { Content = "Cancel" };
        AutomationProperties.SetName(_line, "Line number");
        AutomationProperties.SetName(go, "Go to line");
        AutomationProperties.SetName(cancel, "Cancel line navigation");
        AutomationProperties.SetName(_status, "Navigation status");
        Content = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal, Spacing = 8,
                    Children = { new TextBlock { Text = "Line:", VerticalAlignment = VerticalAlignment.Center }, _line, go, cancel }
                },
                _status
            }
        };
        go.Click += (_, _) => Submit();
        cancel.Click += (_, _) => Cancel();
        _line.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter) { Submit(); args.Handled = true; }
            else if (args.Key == Key.Escape) { Cancel(); args.Handled = true; }
        };
    }

    public void BindDocument(LogDocument? document)
    {
        Document = document;
        IsVisible = false;
        _line.Text = null;
        _status.Text = "";
    }

    public void BeginNavigation()
    {
        IsVisible = true;
        _line.Text = null;
        _status.Text = "";
        Dispatcher.UIThread.Post(() => { if (IsVisible) _line.Focus(); }, DispatcherPriority.Loaded);
    }

    private void Submit()
    {
        var document = Document;
        if (document == null) { _status.Text = "Open a log file before choosing a line."; return; }
        if (!int.TryParse(_line.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var number) ||
            number < 1 || number > document.Lines.Count)
        {
            _status.Text = document.Lines.Count == 0 ? "No lines are available yet." :
                $"Enter a line number from 1 to {document.Lines.Count:N0}.";
            return;
        }
        _status.Text = "";
        LineChosen?.Invoke(number - 1);
        IsVisible = false;
    }

    private void Cancel()
    {
        IsVisible = false;
        Cancelled?.Invoke();
    }
}
