using System.Diagnostics;
using Avalonia;
using Avalonia.Headless;
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
using Tailviewer.Avalonia.ViewModels;
using Tailviewer.Avalonia.Views;

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
    public async Task QuickNavigationKeyboardChoosesTheHighlightedCandidate()
    {
        var other = Path.Combine(_folder, "other.log");
        File.WriteAllText(other, "other");
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            await window.OpenFileAsync(other);
            var input = OpenQuickNavigation(window);
            input.Text = ".log";
            var matches = QuickMatches(window);
            await Until(() => matches.ItemCount == 2);
            matches.SelectedIndex.Should().Be(0);
            input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down });
            matches.SelectedIndex.Should().Be(1);
            input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Up });
            matches.SelectedIndex.Should().Be(0);
            input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
            await Until(() => Rows(window).ItemCount == 3 && window.Title!.Contains("navigation.log"));
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ChoosingTheCurrentSourceKeepsItsSelectionAndPausedFollow()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            await Until(() => Rows(window).ItemCount == 3);
            Rows(window).Selection.Select(1);
            OpenQuickNavigation(window).Text = "navigation.log";
            await Until(() => QuickMatches(window).ItemCount == 1);
            ChooseQuick(window);
            await Until(() => window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "_Open file…")).IsEnabled);
            Rows(window).Selection.SelectedIndexes.Should().Equal(1);
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked.Should().BeFalse();
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task QuickNavigationClearsStaleCandidatesForEmptyAndUnmatchedInput()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            await Until(() => Rows(window).ItemCount == 3);
            var input = OpenQuickNavigation(window);
            input.Text = "navigation.log";
            var matches = QuickMatches(window);
            await Until(() => matches.ItemCount == 1);
            input.Text = "not-an-open-source";
            await Until(() => matches.ItemCount == 0);
            matches.SelectedItem.Should().BeNull();
            window.GetVisualDescendants().OfType<Button>().Single(control =>
                AutomationProperties.GetName(control) == "Open selected source").IsEnabled.Should().BeFalse();
            ChooseQuick(window);
            Rows(window).ItemCount.Should().Be(3);
            window.Title.Should().Contain("navigation.log");
            input.Text = "";
            await Until(() => matches.ItemCount == 0);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task CancellingQuickNavigationDoesNotSwitchSourceOrSelection()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            await Until(() => Rows(window).ItemCount == 3);
            Rows(window).Selection.Select(1);
            var input = OpenQuickNavigation(window);
            input.Text = "navigation.log";
            await Until(() => QuickMatches(window).ItemCount == 1);
            input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            input.IsEffectivelyVisible.Should().BeFalse();
            Rows(window).Selection.SelectedIndexes.Should().Equal(1);
            Rows(window).IsKeyboardFocusWithin.Should().BeTrue();
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task CopyInTheSourceSearchBoxDoesNotCopyTheSelectedLogLine()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            await Until(() => Rows(window).ItemCount == 3);
            Rows(window).Selection.Select(0);
            var input = OpenQuickNavigation(window);
            input.Text = "NAVIGATION.LOG";
            input.SelectionStart = 0;
            input.SelectionEnd = input.Text.Length;
            var clipboard = window.Clipboard!;
            await clipboard.SetTextAsync("pending");
            input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.C, KeyModifiers = KeyModifiers.Control });
            await CheckClipboard(clipboard, "NAVIGATION.LOG");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task QuickNavigationFindsAndActivatesAGroupByItsName()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var group = ((SourcesViewModel)window.DataContext!).AddGroup();
            var input = OpenQuickNavigation(window);
            input.Text = "MERGED";
            await Until(() => QuickMatches(window).ItemCount == 1);
            ((DataSourceSuggestionViewModel)QuickMatches(window).SelectedItem!).DataSource.Id.Should().Be(group.Id);
            ChooseQuick(window);
            await Until(() => window.Title!.Contains("Merged Data Source"));
            Rows(window).ItemCount.Should().Be(0);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task QuickNavigationChoosesAnExistingSourceByCaseInsensitivePath()
    {
        var other = Path.Combine(_folder, "other.log");
        File.WriteAllText(other, "other");
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            await window.OpenFileAsync(other);
            OpenQuickNavigation(window).Text = "NAVIGATION.LOG";
            var matches = QuickMatches(window);
            await Until(() => matches.ItemCount == 1);
            matches.ItemCount.Should().Be(1);
            ChooseQuick(window);
            await Until(() => Rows(window).ItemCount == 3 && window.Title!.Contains("navigation.log"));
        }
        finally { window.Close(); }
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

    [AvaloniaTest]
    public async Task SourceShortcutsWrapInBothDirectionsWithoutChangingPlainTab()
    {
        var other = Path.Combine(_folder, "other.log");
        File.WriteAllText(other, "other");
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            await window.OpenFileAsync(other);
            await Until(() => Rows(window).ItemCount == 1);
            var unchanged = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Tab };
            window.RaiseEvent(unchanged);
            window.Title.Should().Contain("other.log");
            foreach (var shift in new[] { false, true, true, false })
            {
                var expected = window.Title!.Contains("other.log") ? "navigation.log" : "other.log";
                var key = new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent, Key = Key.Tab,
                    KeyModifiers = KeyModifiers.Control | (shift ? KeyModifiers.Shift : KeyModifiers.None)
                };
                window.RaiseEvent(key);
                key.Handled.Should().BeTrue("because Ctrl+Tab and Ctrl+Shift+Tab navigate sources");
                await Until(() => window.Title!.Contains(expected));
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task SourceShortcutsWithZeroOrOneSourceDoNotResetTheDocument()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Tab, KeyModifiers = KeyModifiers.Control });
            Rows(window).ItemCount.Should().Be(0);
            await window.OpenFileAsync(_path);
            await Until(() => Rows(window).ItemCount == 3);
            Rows(window).Selection.Select(1);
            foreach (var shift in new[] { false, true })
                window.RaiseEvent(new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent, Key = Key.Tab,
                    KeyModifiers = KeyModifiers.Control | (shift ? KeyModifiers.Shift : KeyModifiers.None)
                });
            Dispatcher.UIThread.RunJobs();
            Rows(window).Selection.SelectedIndexes.Should().Equal(1);
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked.Should().BeFalse();
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task CharacterSelectionCopiesOnlyOriginalTextInEitherDirection()
    {
        File.WriteAllText(_path, "  A😀B  \n\nမြန်မာ tail");
        using var document = new LogDocument(_path);
        await Until(() => { document.Refresh(); return document.Lines.Count == 3; });
        (await document.CopyTextAsync(new LogSelection(0, 3, 0, 5))).Should().Be("😀");
        (await document.CopyTextAsync(new LogSelection(0, 5, 0, 3))).Should().Be("😀");
        var expected = string.Join(Environment.NewLine, "😀B  ", "", "မြန်မာ");
        (await document.CopyTextAsync(new LogSelection(0, 3, 2, 6))).Should().Be(expected);
        (await document.CopyTextAsync(new LogSelection(2, 6, 0, 3))).Should().Be(expected);
        (await document.CopyTextAsync(new LogSelection(0, 3, 0, 3))).Should().BeEmpty();
    }

    [AvaloniaTest]
    public async Task CharacterSelectionRejectsInvalidCoordinatesRatherThanClamping()
    {
        using var document = new LogDocument(_path);
        await Until(() => { document.Refresh(); return document.Lines.Count == 3; });
        foreach (var selection in new[]
                 {
                     new LogSelection(-1, 0, 0, 0), new LogSelection(0, -1, 0, 0),
                     new LogSelection(0, 0, 3, 0), new LogSelection(0, 0, 0, 6),
                     new LogSelection(2, 6, 0, 0), new LogSelection(int.MaxValue, 0, 0, 0)
                 })
        {
            Func<Task> copy = async () => await document.CopyTextAsync(selection);
            await copy.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }
        foreach (var indices in new[] { new[] { -1 }, new[] { 3 }, new[] { int.MaxValue } })
        {
            Func<Task> copy = async () => await document.CopyTextAsync(indices);
            await copy.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }
        (await document.CopyTextAsync(Array.Empty<int>())).Should().BeEmpty();
    }

    [AvaloniaTest]
    public async Task AltDragCopiesCharacterRangeAcrossVirtualizedRows()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            await Until(() => Rows(window).GetVisualDescendants().OfType<TextBlock>()
                .Count(block => block.DataContext is LogLine line && line.Text != null) == 3);
            window.UpdateLayout();
            foreach (var reverse in new[] { false, true })
            {
                DragText(window, reverse ? TextPoint(window, 1, 8) : TextPoint(window, 0, 2),
                    reverse ? TextPoint(window, 0, 2) : TextPoint(window, 1, 8));
                Rows(window).Selection.SelectedIndexes.Should().BeEmpty();
                await CopyAndCheck(window, "rst" + Environment.NewLine + "  မြန်မာ");
                Rows(window).GetVisualDescendants().OfType<SelectableTextBlock>()
                    .Single(block => block.DataContext is LogLine { Index: 0 }).SelectedText.Should().Be("rst");
                Rows(window).GetVisualDescendants().OfType<SelectableTextBlock>()
                    .Single(block => block.DataContext is LogLine { Index: 0 }).SelectionBrush.Should().NotBeNull();
            }
            File.AppendAllText(_path, "\nappended");
            await Until(() => Rows(window).ItemCount == 4);
            await CopyAndCheck(window, "rst" + Environment.NewLine + "  မြန်မာ");
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked.Should().BeFalse();
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task CharacterDragIsClearedOnReplacementAndSourceSwitch()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            await Until(() => Rows(window).GetVisualDescendants().OfType<TextBlock>()
                .Any(block => block.DataContext is LogLine { Index: 0, Text: "first" }));
            window.UpdateLayout();
            DragText(window, TextPoint(window, 0, 1), TextPoint(window, 0, 4));
            await CopyAndCheck(window, "irs");
            File.WriteAllText(_path, "replaced");
            await Until(() => Rows(window).ItemCount == 1 && Rows(window).GetVisualDescendants().OfType<TextBlock>()
                .Any(block => block.DataContext is LogLine { Text: "replaced" }));
            window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "_Copy selected")).IsEnabled.Should().BeFalse();
            window.UpdateLayout();
            DragText(window, TextPoint(window, 0, 0), TextPoint(window, 0, 3));
            await CopyAndCheck(window, "rep");
            var other = Path.Combine(_folder, "other.log");
            File.WriteAllText(other, "other");
            await window.OpenFileAsync(other);
            await Until(() => Rows(window).ItemCount == 1);
            window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "_Copy selected")).IsEnabled.Should().BeFalse();
            Rows(window).Selection.Select(0);
            await CopyAndCheck(window, "other");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task CharacterCopyRejectsARevisionCapturedBeforeResetNotification()
    {
        using var document = new LogDocument(_path);
        await Until(() => { document.Refresh(); return document.Lines.Count == 3; });
        var revision = document.Revision;
        document.OnLogFileModified(null!, Tailviewer.Api.LogSourceModification.Reset());
        Func<Task> copy = async () => await document.CopyTextAsync(new LogSelection(0, 1, 0, 4), revision);
        await copy.Should().ThrowAsync<IOException>();
    }

    private static void DragText(MainWindow window, Point start, Point end)
    {
        window.MouseMove(start);
        window.MouseDown(start, MouseButton.Left, RawInputModifiers.Alt);
        window.MouseMove(end, RawInputModifiers.Alt | RawInputModifiers.LeftMouseButton);
        window.MouseUp(end, MouseButton.Left, RawInputModifiers.Alt);
    }

    private static Point TextPoint(MainWindow window, int index, int column)
    {
        var block = Rows(window).GetVisualDescendants().OfType<TextBlock>()
            .Single(block => block.DataContext is LogLine line && line.Index == index);
        var row = (LogLine)block.DataContext!;
        var offset = row.DisplayText.Length - row.Text!.Length;
        var caret = block.TextLayout.HitTestTextPosition(offset + column);
        return block.TranslatePoint(new Point(caret.X, caret.Y + caret.Height / 2), window)!.Value;
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

    private static TextBox OpenQuickNavigation(MainWindow window)
    {
        window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.T, KeyModifiers = KeyModifiers.Control });
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        var input = window.GetVisualDescendants().OfType<TextBox>()
            .SingleOrDefault(control => AutomationProperties.GetName(control) == "Source search");
        input.Should().NotBeNull("because Ctrl+T must search already open sources");
        input!.IsEffectivelyVisible.Should().BeTrue();
        return input;
    }

    private static ComboBox QuickMatches(MainWindow window) => window.GetVisualDescendants().OfType<ComboBox>()
        .Single(control => AutomationProperties.GetName(control) == "Source matches");

    private static void ChooseQuick(MainWindow window) => window.GetVisualDescendants().OfType<Button>()
        .Single(control => AutomationProperties.GetName(control) == "Open selected source")
        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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
        await CheckClipboard(clipboard, expected);
    }

    private static async Task CheckClipboard(IClipboard clipboard, string expected)
    {
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
