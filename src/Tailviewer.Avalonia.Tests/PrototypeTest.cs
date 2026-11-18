using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using NUnit.Framework;
using Tailviewer.Avalonia;

[assembly: AvaloniaTestApplication(typeof(Tailviewer.Avalonia.Tests.TestAppBuilder))]

namespace Tailviewer.Avalonia.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

[TestFixture]
public sealed class PrototypeTest
{
    private string _path = null!;

    [SetUp]
    public void SetUp() => _path = Path.Combine(Path.GetTempPath(), $"tailviewer-prototype-{Guid.NewGuid():N}.log");

    [TearDown]
    public void TearDown() => File.Delete(_path);

    [AvaloniaTest]
    public async Task TestCoreReadingAndCopyOrder()
    {
        File.WriteAllText(_path, "မြန်မာ\n  keep spaces  \nမြန်မာ", new UTF8Encoding(true));
        using var document = new LogDocument(_path);
        await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 3; });
        var row = document.GetLine(1);
        await WaitUntil(() => row.Text != null);
        row.Text.Should().Be("  keep spaces  ", "because display reads must preserve log content");
        (await document.CopyTextAsync(new[] { 2, 0, 1, 1 })).Should().Be(
            string.Join(Environment.NewLine, "မြန်မာ", "  keep spaces  ", "မြန်မာ"),
            "because copying must retain duplicate lines in file order, without display line numbers");
    }

    [AvaloniaTest]
    public async Task TestUtf16Bom()
    {
        File.WriteAllText(_path, "မြန်မာ\r\nsecond", Encoding.Unicode);
        using var document = new LogDocument(_path);
        await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 2; });
        (await document.CopyTextAsync(new[] { 0, 1 })).Should().Be("မြန်မာ" + Environment.NewLine + "second",
            "because BOM detection must feed the same encoding to Core's indexer and reader");
    }

    [AvaloniaTest]
    public async Task TestEmptyFileAndPartialLineTailing()
    {
        File.WriteAllText(_path, "");
        using var document = new LogDocument(_path);
        document.Refresh();
        document.Lines.Should().BeEmpty();
        File.AppendAllText(_path, "first\npartial");
        await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 2; });
        var row = document.GetLine(1);
        await WaitUntil(() => row.Text == "partial");
        File.AppendAllText(_path, " completed\nthird");
        await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 3 && row.Text == "partial completed"; });
        (await document.CopyTextAsync(new[] { 2 })).Should().Be("third", "because appended lines must become readable");
    }

    [AvaloniaTest]
    public async Task TestSelectionAndClipboardSurviveAppend()
    {
        File.WriteAllText(_path, "first\nselected\nlast");
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            var list = window.GetVisualDescendants().OfType<ListBox>().Single();
            await WaitUntil(() => list.ItemCount == 3);
            list.Selection.Select(1);
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked.Should().BeFalse(
                "because selecting a line must pause following the tail");
            File.AppendAllText(_path, "\nnew");
            await WaitUntil(() => list.ItemCount == 4);
            list.Selection.SelectedIndexes.Should().Equal(new[] { 1 }, "because live appends must preserve selection");
            var copy = window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "_Copy selected"));
            copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(async () => await window.Clipboard!.TryGetTextAsync() == "selected");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task TestScrollingVirtualizesAndLoadsDistantLines()
    {
        File.WriteAllText(_path, string.Join('\n', Enumerable.Range(0, 20000).Select(i => $"line-{i:D5}")));
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            var list = window.GetVisualDescendants().OfType<ListBox>().Single();
            await WaitUntil(() => list.ItemCount == 20000);
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = false;
            list.ScrollIntoView(15000);
            await WaitUntil(() => list.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains("line-15000") == true));
            list.GetRealizedContainers().Count().Should().BeLessThan(100,
                "because scrolling must realize the viewport rather than all indexed lines");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task TestTruncationClearsStaleSelection()
    {
        File.WriteAllText(_path, "old line that will be replaced\nsecond old line");
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            var list = window.GetVisualDescendants().OfType<ListBox>().Single();
            await WaitUntil(() => list.ItemCount == 2);
            list.Selection.Select(1);
            File.WriteAllText(_path, "new");
            await WaitUntil(() => list.ItemCount == 1);
            list.Selection.SelectedIndexes.Should().BeEmpty("because selected indices no longer identify the same content after truncation");
            await WaitUntil(() => list.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.EndsWith("  new") == true));
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task TestFailedOpenKeepsCurrentFileAndError()
    {
        File.WriteAllText(_path, "keep this file");
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            var list = window.GetVisualDescendants().OfType<ListBox>().Single();
            await WaitUntil(() => list.ItemCount == 1);
            var original = list.ItemsSource;
            await window.OpenFileAsync(_path + ".missing");
            await Task.Delay(250);
            list.ItemsSource.Should().BeSameAs(original, "because a failed open must keep the current document");
            window.GetVisualDescendants().OfType<TextBlock>().Should().Contain(t => t.Text != null && t.Text.StartsWith("Cannot open file:"),
                "because the refresh timer must not immediately erase errors");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task TestCloseDuringRead()
    {
        File.WriteAllText(_path, "first\nsecond");
        using var document = new LogDocument(_path);
        await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 2; });
        document.GetLine(0);
        document.Dispose();
        await Task.Delay(100);
        document.Refresh().Should().BeFalse("because a closed document must stop updating");
    }

    private static async Task WaitUntil(Func<bool> ready)
    {
        var clock = Stopwatch.StartNew();
        while (!ready() && clock.Elapsed < TimeSpan.FromSeconds(8))
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
        ready().Should().BeTrue("because Core and the UI should converge within eight seconds");
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> ready)
    {
        var clock = Stopwatch.StartNew();
        while (!await ready() && clock.Elapsed < TimeSpan.FromSeconds(8))
            await Task.Delay(20);
        (await ready()).Should().BeTrue("because the asynchronous UI operation should complete");
    }
}
