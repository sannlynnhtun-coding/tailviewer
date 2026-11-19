using System.Diagnostics;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using NUnit.Framework;

namespace Tailviewer.Avalonia.Tests;

[TestFixture]
public sealed class NavigationTest
{
    private string _folder = null!;
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "tailviewer-navigation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _path = Path.Combine(_folder, "navigation.log");
        File.WriteAllText(_path, "first\n  မြန်မာ  \nfirst");
    }

    [TearDown]
    public void TearDown()
    {
        Path.GetDirectoryName(Path.GetFullPath(_folder)).Should().Be(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())));
        Directory.Delete(_folder, true);
    }

    [AvaloniaTest]
    public async Task CancellingNavigationRestoresLogFocusWithoutChangingSelection()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            var rows = Rows(window);
            await Until(() => rows.ItemCount == 3);
            rows.Selection.Select(1);
            var input = OpenNavigation(window);
            input.Text = "3";
            input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            input.IsEffectivelyVisible.Should().BeFalse();
            rows.IsKeyboardFocusWithin.Should().BeTrue();
            rows.Selection.SelectedIndexes.Should().Equal(1);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public void NavigationWithoutAnActiveLogReportsGuidance()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            OpenNavigation(window).Text = "1";
            Go(window);
            Rows(window).Selection.SelectedIndexes.Should().BeEmpty();
            window.GetVisualDescendants().OfType<TextBlock>().Single(control =>
                AutomationProperties.GetName(control) == "Navigation status").Text.Should().Contain("Open a log file");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task InvalidLineNumbersDoNotMoveSelectionOrChangeFollowState()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            var rows = Rows(window);
            await Until(() => rows.ItemCount == 3);
            rows.Selection.Select(0);
            var follow = window.GetVisualDescendants().OfType<CheckBox>().Single();
            follow.IsChecked = true;
            var input = OpenNavigation(window);
            foreach (var value in new[] { "0", "-1", "4", "999999999999999", "not-a-number", "" })
            {
                input.Text = value;
                Go(window);
                rows.Selection.SelectedIndexes.Should().Equal(0);
                follow.IsChecked.Should().BeTrue();
                window.GetVisualDescendants().OfType<TextBlock>().Single(control =>
                    AutomationProperties.GetName(control) == "Navigation status").Text.Should().NotBeNullOrEmpty();
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task SwitchingSourcesResetsThePreviousNavigationInput()
    {
        var other = Path.Combine(_folder, "other.log");
        File.WriteAllText(other, "only");
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            await Until(() => Rows(window).ItemCount == 3);
            OpenNavigation(window).Text = "2";
            await window.OpenFileAsync(other);
            await Until(() => Rows(window).ItemCount == 1);
            var input = OpenNavigation(window);
            input.Text.Should().BeNullOrEmpty();
            input.Text = "2";
            Go(window);
            Rows(window).Selection.SelectedIndexes.Should().BeEmpty();
            window.Title.Should().Contain("other.log");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task LineSelectionCopiesOriginalSourceOrderAndRetainsDuplicateText()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            var rows = Rows(window);
            await Until(() => rows.ItemCount == 3);
            foreach (var index in new[] { 2, 0, 1, 1 }) rows.Selection.Select(index);
            await CopyAndCheck(window, string.Join(Environment.NewLine, "first", "  မြန်မာ  ", "first"));
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task GoToLineUsesOneBasedNumbersAndPausesFollow()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            var rows = Rows(window);
            await Until(() => rows.ItemCount == 3);
            OpenNavigation(window).Text = "2";
            Go(window);
            rows.Selection.SelectedIndexes.Should().Equal(1);
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked.Should().BeFalse();
            await CopyAndCheck(window, "  မြန်မာ  ");
        }
        finally { window.Close(); }
    }

    private static TextBox OpenNavigation(MainWindow window)
    {
        window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.G, KeyModifiers = KeyModifiers.Control });
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var input = window.GetVisualDescendants().OfType<TextBox>()
            .SingleOrDefault(control => AutomationProperties.GetName(control) == "Line number");
        input.Should().NotBeNull("because Ctrl+G must expose line navigation");
        input!.IsEffectivelyVisible.Should().BeTrue();
        return input;
    }

    private static void Go(MainWindow window) => window.GetVisualDescendants().OfType<Button>()
        .Single(control => AutomationProperties.GetName(control) == "Go to line")
        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static ListBox Rows(MainWindow window) => window.GetVisualDescendants().OfType<ListBox>().Single();

    private static async Task CopyAndCheck(MainWindow window, string expected)
    {
        var clipboard = window.Clipboard ?? throw new InvalidOperationException("Headless clipboard unavailable.");
        await clipboard.SetTextAsync("pending-" + Guid.NewGuid().ToString("N"));
        window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "_Copy selected"))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(8))
        {
            Dispatcher.UIThread.RunJobs();
            if (await clipboard.TryGetTextAsync() == expected) return;
            await Task.Delay(10);
        }
        (await clipboard.TryGetTextAsync()).Should().Be(expected);
    }

    private static async Task Until(Func<bool> ready)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(8))
        {
            Dispatcher.UIThread.RunJobs();
            if (ready()) return;
            await Task.Delay(10);
        }
        ready().Should().BeTrue("because navigation must converge within eight seconds");
    }
}
