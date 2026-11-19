using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Tailviewer.Avalonia.ViewModels;
using Tailviewer.Settings;

namespace Tailviewer.Avalonia.Views;

public sealed class QuickNavigationView : UserControl
{
    private readonly SourcesViewModel _workspace;
    private readonly TextBox _query = new() { PlaceholderText = "Source name or path" };
    private readonly ComboBox _matches = new() { HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 240 };
    private readonly TextBlock _status = new();
    private readonly Button _choose = new() { Content = "Open source", IsEnabled = false };
    public event Action<DataSource>? SourceChosen;
    public event Action? Cancelled;

    public QuickNavigationView(SourcesViewModel workspace)
    {
        _workspace = workspace;
        IsVisible = false;
        var cancel = new Button { Content = "Cancel" };
        AutomationProperties.SetName(_query, "Source search");
        AutomationProperties.SetName(_matches, "Source matches");
        AutomationProperties.SetName(_status, "Source navigation status");
        AutomationProperties.SetName(_choose, "Open selected source");
        AutomationProperties.SetName(cancel, "Cancel source navigation");
        _matches.ItemTemplate = new FuncDataTemplate<DataSourceSuggestionViewModel>((match, _) => match == null ? new TextBlock() : new TextBlock
        {
            Inlines = new InlineCollection
            {
                new Run(match.Prefix), new Run(match.Midfix) { FontWeight = FontWeight.Bold }, new Run(match.Postfix)
            }
        });
        Content = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                _query, _matches,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _choose, cancel, _status } }
            }
        };
        _query.TextChanged += (_, _) => RefreshMatches();
        _choose.Click += (_, _) => Choose();
        cancel.Click += (_, _) => { Dismiss(); Cancelled?.Invoke(); };
        _query.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter) { Choose(); args.Handled = true; }
            else if (args.Key == Key.Escape) { Dismiss(); Cancelled?.Invoke(); args.Handled = true; }
            else if (args.Key is Key.Up or Key.Down)
            {
                if (_matches.ItemCount > 0)
                    _matches.SelectedIndex = Math.Clamp(_matches.SelectedIndex + (args.Key == Key.Down ? 1 : -1), 0, _matches.ItemCount - 1);
                args.Handled = true;
            }
        };
    }

    public void BeginNavigation()
    {
        Dismiss();
        IsVisible = true;
        Dispatcher.UIThread.Post(() => { if (IsVisible) _query.Focus(); }, DispatcherPriority.Loaded);
    }

    public void Dismiss()
    {
        IsVisible = false;
        _query.Text = null;
        _matches.ItemsSource = Array.Empty<DataSourceSuggestionViewModel>();
        _choose.IsEnabled = false;
        _status.Text = "";
    }

    private void RefreshMatches()
    {
        var matches = _workspace.FindNavigationMatches(_query.Text);
        _matches.ItemsSource = matches;
        _matches.SelectedIndex = matches.Count > 0 ? 0 : -1;
        _choose.IsEnabled = matches.Count > 0;
        _status.Text = string.IsNullOrEmpty(_query.Text) ? "" : matches.Count == 0 ? "No matching sources." : $"{matches.Count} matching sources";
    }

    private void Choose()
    {
        if (_matches.SelectedItem is not DataSourceSuggestionViewModel chosen) return;
        if (!_workspace.Contains(chosen.DataSource.Id))
        {
            RefreshMatches();
            _status.Text = "That source is no longer open. Search again.";
            return;
        }
        var source = chosen.DataSource;
        Dismiss();
        SourceChosen?.Invoke(source);
    }
}
