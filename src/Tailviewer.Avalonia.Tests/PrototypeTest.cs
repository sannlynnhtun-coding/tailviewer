using System.Diagnostics;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using FluentAssertions;
using NUnit.Framework;
using Tailviewer.Avalonia;

[assembly: AvaloniaTestApplication(typeof(Tailviewer.Avalonia.Tests.TestAppBuilder))]

namespace Tailviewer.Avalonia.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

[TestFixture]
public sealed class PrototypeTest
{
    private string _path = null!;

    [SetUp]
    public void SetUp() => _path = Path.Combine(Path.GetTempPath(), $"tailviewer-prototype-{Guid.NewGuid():N}.log");

    [TearDown]
    public void TearDown()
    {
        File.Delete(_path);
        File.Delete(_path + ".xml");
    }

    [AvaloniaTest]
    public async Task DashboardSearchesAcrossFolderRootsAndDeduplicatesOverlaps()
    {
        var first = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var second = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var day = Path.Combine(first, "2026", "11", "20");
        Directory.CreateDirectory(day);
        Directory.CreateDirectory(second);
        var window = new MainWindow(showObservability: true);
        try
        {
            for (var hour = 0; hour < 31; hour++)
                File.WriteAllText(Path.Combine(day, $"hour-{hour:D2}.log"), hour == 30 ? "needle" : "other");
            File.WriteAllText(Path.Combine(second, "another.TXT"), "needle");
            File.WriteAllText(Path.Combine(second, "excluded.log.bak"), "needle");
            window.Show();
            await WaitUntil(() => window.GetVisualDescendants().OfType<Button>().Any(button => Equals(button.Content, "Add Folders")));
            window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Add Folders"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntil(() => window.OwnedWindows.Any(dialog => dialog.IsVisible));
            var addFolders = window.OwnedWindows.Single(dialog => dialog.IsVisible);
            await WaitUntil(() => addFolders.GetVisualDescendants().OfType<TextBox>().Any(box => box.AcceptsReturn));
            addFolders.GetVisualDescendants().OfType<TextBox>().Single(box => box.AcceptsReturn).Text =
                string.Join(Environment.NewLine, first, second, day);
            addFolders.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "Add Folders"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntil(() => !addFolders.IsVisible);
            var search = window.GetVisualDescendants().OfType<TextBox>().Single(input => input.PlaceholderText == "Search logs…");
            search.Text = "needle";
            var result = window.GetVisualDescendants().OfType<ListBox>().Single();
            await WaitUntil(() => result.ItemCount == 2);
            var logs = result.Items.Cast<ObservedLog>().ToArray();
            logs.Select(log => log.File).Should().BeEquivalentTo(Path.Combine(day, "hour-30.log"), Path.Combine(second, "another.TXT"));
            logs.Should().OnlyContain(log => log.Line == 1);
            result.SelectedIndex = 0;
            using var frame = window.CaptureRenderedFrame();
            frame.Should().NotBeNull();
            result.SelectedIndex = -1;
            File.AppendAllText(Path.Combine(second, "another.TXT"), "\nneedle");
            await WaitUntil(() => result.ItemCount == 3);
            result.Items.Cast<ObservedLog>().Should().Contain(log => log.Line == 2 && log.File == Path.Combine(second, "another.TXT"));
        }
        finally
        {
            window.Close();
            Directory.Delete(first, true);
            Directory.Delete(second, true);
        }
    }

    [AvaloniaTest]
    public async Task DashboardArrowKeysKeepFocusAndRevealRowsBeyondTheViewport()
    {
        #region Open the log and focus its first row

        var path = TestContext.Parameters.Get("DashboardNavigationLog", _path);
        if (path == _path)
            File.WriteAllLines(path, Enumerable.Range(0, 200).Select(index => $"INFO sample event {index}"));

        var window = new MainWindow(showObservability: true);
        window.Show();
        try
        {
            await window.OpenFileAsync(path);
            var rows = window.GetVisualDescendants().OfType<ListBox>().Single();
            await WaitUntil(() => rows.ItemCount >= 200 && rows.ContainerFromIndex(0) != null);
            ((ObservedLog)rows.Items[0]!).File.Should().Be(path);

            rows.SelectedIndex = 0;
            rows.ContainerFromIndex(0)!.Focus().Should().BeTrue();
            rows.ContainerFromIndex(80).Should().BeNull();

            #endregion

            #region Cross the viewport in both directions

            for (var index = 1; index <= 80; index++)
                PressArrow(Key.Down, PhysicalKey.ArrowDown, index);

            await WaitUntil(() => rows.ContainerFromIndex(80) != null);
            rows.ContainerFromIndex(0).Should().BeNull();

            for (var index = 79; index >= 0; index--)
                PressArrow(Key.Up, PhysicalKey.ArrowUp, index);

            PressArrow(Key.Up, PhysicalKey.ArrowUp, 0);
            await WaitUntil(() => rows.ContainerFromIndex(0) != null);

            #endregion

            #region Keep focus at the last row

            await WaitUntil(() => window.GetVisualDescendants().OfType<TextBlock>()
                .Any(block => block.Text?.EndsWith("1/1 files · 0 unreadable", StringComparison.Ordinal) == true));
            var last = rows.ItemCount - 1;
            rows.SelectedIndex = last;
            rows.ScrollIntoView(last);
            await WaitUntil(() => rows.ContainerFromIndex(last) != null);
            PressArrow(Key.Down, PhysicalKey.ArrowDown, last);

            #endregion

            void PressArrow(Key key, PhysicalKey physicalKey, int expected)
            {
                window.KeyPress(key, RawInputModifiers.None, physicalKey, null);
                window.KeyRelease(key, RawInputModifiers.None, physicalKey, null);
                window.UpdateLayout();

                rows.SelectedIndex.Should().Be(expected);
                rows.IsKeyboardFocusWithin.Should().BeTrue($"focus should stay in the table at row {expected}");
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTest]
    public void DashboardHighlightColorsTokensWithoutChangingTheText()
    {
        const string text = """{"message":"example","status":503,"retry":false}""";
        var label = new TextBlock();
        ObservabilityView.Highlight(label, text, 40);
        var runs = label.Inlines!.OfType<global::Avalonia.Controls.Documents.Run>().ToArray();
        string.Concat(runs.Select(run => run.Text)).Should().Be(text);
        runs.Count(run => run.Foreground != null).Should().BeGreaterThan(3);
    }
    [AvaloniaTest]
    public void DashboardHighlightsLocationsAndDetectsFencedCode()
    {
        const string text = "```csharp\npublic class Example {}\n```\nGET /api/accounts https://example.test/api C:\\Logs\\MOB Bank";
        var label = new TextBlock();
        ObservabilityView.Highlight(label, text, 512, "Auto");
        var runs = label.Inlines!.OfType<global::Avalonia.Controls.Documents.Run>().ToArray();
        string.Concat(runs.Select(run => run.Text)).Should().Be(text);
        var tokens = runs.Where(run => run.Foreground != null).Select(run => run.Text);
        tokens.Should().Contain(["public", "class", "/api/accounts", "https://example.test/api", @"C:\Logs\MOB Bank"]);
    }
    [Test]
    public async Task DashboardParsesOffsetAndMultilineRollingLogs()
    {
        const string text = """
            2026-10-30 10:00:00.123 +06:30 [ERR] Request failed
            System.InvalidOperationException: synthetic error
               at Example.Handler()
            2026-10-30 10:00:01.456 +06:30 [INF] Request finished
            """;
        using var reader = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(text)));
        var logs = new List<ObservedLog>();
        await foreach (var log in ObservedLog.ReadAsync(reader, "MOBBankAPI-log-2026-10-30-10-00-AM.log", CancellationToken.None))
            logs.Add(log);

        logs.Select(log => log.Line).Should().Equal(1, 4);
        logs.Select(log => log.Level).Should().Equal("ERROR", "INFO");
        logs[0].Timestamp!.Value.UtcDateTime.Should().Be(new DateTime(2026, 10, 30, 3, 30, 0, 123, DateTimeKind.Utc));
        ObservabilityView.CreateQuery("level:ERROR service:MOBBankAPI synthetic", false)(logs[0]).Should().BeTrue();
        logs[0].Message.Should().EndWith("   at Example.Handler()");
    }
    [Test]
    public void DashboardParsesStructuredFieldsAndKeepsMalformedJsonSearchable()
    {
        var structured = ObservedLog.Parse("app.log", 1,
            """{"@t":"2026-11-20T01:02:03Z","@l":"Error","@mt":"Request failed","service":"orders","http":{"status":503},"latency_ms":750}""");
        Assert.That(ObservabilityView.CreateQuery("level:ERROR status:503 service:orders latency>500", false)(structured), Is.True);
        var malformed = ObservedLog.Parse("app.log", 2, "{broken timeout");
        Assert.That(ObservabilityView.CreateQuery("timeout|deadlock", true)(malformed), Is.True);
        Assert.That(malformed.Payload, Is.Null);
    }
    [AvaloniaTest]
    public async Task EncodingSelectionReopensTheFileWithoutChangingItsIdentityOrPausedState()
    {
        var bytes = new UnicodeEncoding(false, false).GetBytes("မြန်မာ\nsecond");
        File.WriteAllBytes(_path, bytes);
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var id = ((Tailviewer.Settings.DataSource)tree.SelectedItem!).Id;
            var follow = window.GetVisualDescendants().OfType<CheckBox>().Single();
            follow.IsChecked = false;
            var selector = window.GetVisualDescendants().OfType<ComboBox>()
                .Single(combo => AutomationProperties.GetName(combo) == "Text encoding");
            selector.SelectedItem = selector.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Content, "utf-16"));
            var list = window.GetVisualDescendants().OfType<ListBox>().Single();
            await WaitUntil(() => list.ItemCount == 2 && list.GetVisualDescendants().OfType<SelectableTextBlock>()
                .Any(block => block.Text?.Contains("မြန်မာ") == true));
            list.Selection.Select(0);
            window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "_Copy selected"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(async () => await window.Clipboard!.TryGetTextAsync() == "မြန်မာ");
            ((Tailviewer.Settings.DataSource)tree.SelectedItem!).Id.Should().Be(id);
            tree.ItemCount.Should().Be(1);
            follow.IsChecked.Should().BeFalse();
            File.ReadAllBytes(_path).Should().Equal(bytes);
            File.WriteAllText(_path, "😀 auto\nreplacement", new UTF8Encoding(false));
            selector.SelectedItem = selector.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Content, "Auto detect"));
            await WaitUntil(() => list.ItemCount == 2 && list.GetVisualDescendants().OfType<SelectableTextBlock>()
                .Any(block => block.Text?.Contains("😀 auto") == true));
            list.Selection.Select(0);
            window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "_Copy selected"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(async () => await window.Clipboard!.TryGetTextAsync() == "😀 auto");
            ((Tailviewer.Settings.DataSource)tree.SelectedItem!).Id.Should().Be(id);
            follow.IsChecked.Should().BeFalse();
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task PersistedCodePageEncodingFeedsTheReaderAndCanBeClearedToAutomatic()
    {
        var profilePath = _path + ".xml";
        File.WriteAllBytes(_path, new byte[] { 0x63, 0x61, 0x66, 0xe9 });
        var profile = new Tailviewer.Settings.ApplicationSettings(profilePath) { AllowSave = true };
        profile.LogFile.DefaultEncoding = CodePagesEncodingProvider.Instance.GetEncoding(1252)!;
        profile.Save().Should().BeTrue();
        var restored = new Tailviewer.Settings.ApplicationSettings(profilePath) { AllowSave = true };
        restored.Restore();
        var window = new MainWindow(logFileSettings: restored.LogFile);
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            var list = window.GetVisualDescendants().OfType<ListBox>().Single();
            await WaitUntil(() => list.ItemCount == 1 && list.GetVisualDescendants().OfType<SelectableTextBlock>()
                .Any(block => block.Text?.Contains("café") == true));
            list.Selection.Select(0);
            window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "_Copy selected"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(async () => await window.Clipboard!.TryGetTextAsync() == "café");
            var selector = window.GetVisualDescendants().OfType<ComboBox>()
                .Single(combo => AutomationProperties.GetName(combo) == "Text encoding");
            ((ComboBoxItem)selector.SelectedItem!).Content.Should().Be("windows-1252");
            selector.SelectedItem = selector.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Content, "Auto detect"));
            restored.LogFile.DefaultEncoding.Should().BeNull();
            restored.Save().Should().BeTrue();
            var automatic = new Tailviewer.Settings.ApplicationSettings(profilePath);
            automatic.Restore();
            automatic.LogFile.DefaultEncoding.Should().BeNull();
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task EncodingChosenDuringOpenAppliesToThePendingFile()
    {
        File.WriteAllBytes(_path, new UnicodeEncoding(false, false).GetBytes("pending မြန်မာ"));
        var window = new MainWindow();
        window.Show();
        try
        {
            var selector = window.GetVisualDescendants().OfType<ComboBox>()
                .Single(combo => AutomationProperties.GetName(combo) == "Text encoding");
            var opening = window.OpenFileAsync(_path);
            selector.SelectedItem = selector.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Content, "utf-16"));
            await opening;
            var list = window.GetVisualDescendants().OfType<ListBox>().Single();
            await WaitUntil(() => list.ItemCount == 1 && list.GetVisualDescendants().OfType<SelectableTextBlock>()
                .Any(block => block.Text?.Contains("pending မြန်မာ") == true));
            window.GetVisualDescendants().OfType<TreeView>().Single().ItemCount.Should().Be(1);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ManualEncodingOverridesAConflictingBomWhileAutomaticStillDetectsIt()
    {
        foreach (var pair in new[] { (File: Encoding.Unicode, Selected: Encoding.UTF8),
                     (File: Encoding.UTF8, Selected: Encoding.Unicode),
                     (File: Encoding.BigEndianUnicode, Selected: Encoding.Unicode),
                     (File: Encoding.UTF8, Selected: Encoding.UTF8) })
        foreach (var content in new[] { "abc", "first\nsecond" })
        {
            File.WriteAllText(_path, content, pair.File);
            var bytes = File.ReadAllBytes(_path);
            var preamble = pair.Selected.GetPreamble();
            var skip = bytes.Take(preamble.Length).SequenceEqual(preamble) ? preamble.Length : 0;
            var expected = pair.Selected.GetString(bytes, skip, bytes.Length - skip).Split('\n');
            var source = new Tailviewer.Settings.DataSource(_path) { Id = Tailviewer.Api.DataSourceId.CreateNew() };
            using (var manual = new LogDocument(source, pair.Selected))
            {
                await WaitUntil(() => { manual.Refresh(); return manual.Lines.Count == expected.Length; });
                (await manual.CopyTextAsync(Enumerable.Range(0, expected.Length))).Should().Be(string.Join(Environment.NewLine, expected),
                    "because an explicit setting must override a conflicting BOM");
            }
            using var automatic = new LogDocument(source);
            var originalLines = content.Split('\n');
            await WaitUntil(() => { automatic.Refresh(); return automatic.Lines.Count == originalLines.Length; });
            (await automatic.CopyTextAsync(Enumerable.Range(0, originalLines.Length))).Should().Be(string.Join(Environment.NewLine, originalLines));
            File.ReadAllBytes(_path).Should().Equal(bytes);
        }
    }

    [AvaloniaTest]
    public void UnavailableEncodingIsRetainedUntilAnExplicitUserSelection()
    {
        foreach (var fallback in new Encoding?[] { null, Encoding.ASCII })
        {
            var settings = new Tailviewer.Settings.LogFileSettings { DefaultEncoding = fallback };
            using var reader = System.Xml.XmlReader.Create(new StringReader("<logfile defaultencoding='future-encoding-x'/>"));
            reader.MoveToContent();
            settings.Restore(reader);
            var copy = settings.Clone();
            var window = new MainWindow(logFileSettings: copy);
            window.Show();
            try
            {
                var selector = window.GetVisualDescendants().OfType<ComboBox>()
                    .Single(combo => AutomationProperties.GetName(combo) == "Text encoding");
                ((ComboBoxItem)selector.SelectedItem!).Content.Should().Be("Unavailable: future-encoding-x");
                copy.EncodingName.Should().Be("future-encoding-x");
                copy.DefaultEncoding.Should().Be(fallback);
                selector.SelectedItem = selector.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Content, "Auto detect"));
                copy.EncodingName.Should().BeEmpty();
                copy.DefaultEncoding.Should().BeNull();
                settings.EncodingName.Should().Be("future-encoding-x", "because editing a clone must retain the original setting");
            }
            finally { window.Close(); }
        }
    }

    [AvaloniaTest]
    public async Task WindowsSystemCodePageCanBeSelectedWithoutAnInjectedProfile()
    {
        var systemEncoding = CodePagesEncodingProvider.Instance.GetEncoding(0) ?? Encoding.Default;
        File.WriteAllBytes(_path, systemEncoding.GetBytes("café"));
        var window = new MainWindow();
        window.Show();
        try
        {
            var selector = window.GetVisualDescendants().OfType<ComboBox>()
                .Single(combo => AutomationProperties.GetName(combo) == "Text encoding");
            selector.SelectedItem = selector.Items.OfType<ComboBoxItem>()
                .Single(item => item.Tag is Encoding encoding && encoding.CodePage == systemEncoding.CodePage);
            await window.OpenFileAsync(_path);
            var list = window.GetVisualDescendants().OfType<ListBox>().Single();
            await WaitUntil(() => list.ItemCount == 1 && list.GetVisualDescendants().OfType<SelectableTextBlock>()
                .Any(block => block.Text?.Contains("café") == true));
            list.Selection.Select(0);
            window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "_Copy selected"))
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(async () => await window.Clipboard!.TryGetTextAsync() == "café");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ControlHomeAndEndSelectAndRevealTheBoundaryLines()
    {
        File.WriteAllText(_path, string.Join("\n", Enumerable.Range(0, 2000).Select(index => "line-" + index)));
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            var rows = window.GetVisualDescendants().OfType<ListBox>().Single();
            await WaitUntil(() => rows.ItemCount == 2000);
            window.UpdateLayout();
            rows.Selection.Select(1000);
            rows.ScrollIntoView(1000);
            rows.Focus().Should().BeTrue();
            foreach (var key in new[] { Key.Home, Key.End })
            {
                rows.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Right,
                    KeyModifiers = KeyModifiers.Alt | KeyModifiers.Shift });
                window.KeyPress(key, RawInputModifiers.Control, key == Key.Home ? PhysicalKey.Home : PhysicalKey.End, null);
                window.KeyRelease(key, RawInputModifiers.Control, key == Key.Home ? PhysicalKey.Home : PhysicalKey.End, null);
                var expected = key == Key.Home ? 0 : 1999;
                rows.Selection.SelectedIndexes.Should().Equal(expected);
                await WaitUntil(() => rows.GetVisualDescendants().OfType<SelectableTextBlock>()
                    .Any(text => text.Text?.EndsWith("  line-" + expected, StringComparison.Ordinal) == true));
                window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked.Should().BeFalse();
                await window.Clipboard!.SetTextAsync("boundary-copy-sentinel");
                window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "_Copy selected"))
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await WaitUntilAsync(async () => await window.Clipboard!.TryGetTextAsync() == "line-" + expected);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task CompleteTerminalNewlineHasAnEmptyCopyableRowThatReceivesAppends()
    {
        File.WriteAllText(_path, "first\nမြန်မာ 😀\n");
        using var document = new LogDocument(_path);
        await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 3; });
        var last = document.GetLine(2);
        await WaitUntil(() => last.Text != null);
        last.Text.Should().Be("");
        (await document.CopyTextAsync(new[] { 0, 1, 2 })).Should().Be(string.Join(Environment.NewLine, "first", "မြန်မာ 😀", ""));
        File.AppendAllText(_path, "continued");
        await WaitUntil(() => { document.Refresh(); return document.GetLine(2).Text == "continued"; });
        document.Lines.Count.Should().Be(3);
        (await document.CopyTextAsync(new[] { 2 })).Should().Be("continued");
    }

    [AvaloniaTest]
    public void TestLogoLoadsInLightAndDarkThemes()
    {
        foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            var window = new MainWindow { RequestedThemeVariant = theme, Width = 640, Height = 400 };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                var image = window.GetVisualDescendants().OfType<Image>().Single();
                image.Source.Should().BeOfType<Bitmap>("because the packaged logo resource must load in either theme");
                ((Bitmap)image.Source!).PixelSize.Should().Be(new PixelSize(64, 64));
                image.Bounds.Width.Should().Be(28, "because branding must remain compact at the minimum window width");
                window.Icon.Should().NotBeNull("because the title bar and taskbar need the same branding");
                var folder = window.GetVisualDescendants().OfType<Button>()
                    .Single(button => AutomationProperties.GetName(button) == "Open folder");
                folder.IsEffectivelyVisible.Should().BeTrue();
                var follow = window.GetVisualDescendants().OfType<CheckBox>().Single();
                follow.TranslatePoint(new Point(follow.Bounds.Width, 0), window)!.Value.X.Should()
                    .BeLessThanOrEqualTo(window.ClientSize.Width, "because the complete toolbar must fit the minimum window width");
                using var screenshot = new RenderTargetBitmap(new PixelSize(640, 400));
                screenshot.Render(window);
                var screenshotPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, $"branding-{theme}.png");
                screenshot.Save(screenshotPath, new PngBitmapEncoderOptions());
                TestContext.AddTestAttachment(screenshotPath);
            }
            finally { window.Close(); }
        }
    }

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
    public async Task TestConfiguredIdentitySurvivesReplacementAndReopening()
    {
        File.WriteAllText(_path, "old");
        var configured = new Tailviewer.Settings.DataSource(_path)
        {
            Id = Tailviewer.Api.DataSourceId.CreateNew()
        };
        using (var document = new LogDocument(configured))
        {
            await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 1; });
            var old = document.GetLine(0);
            await WaitUntil(() => old.Text == "old");
            File.WriteAllText(_path, "new");
            await WaitUntil(() =>
            {
                document.Refresh();
                return document.Lines.Count == 1 && document.GetLine(0).Text == "new";
            });
            document.SourceId.Should().Be(configured.Id);
        }
        using var reopened = new LogDocument(configured);
        await WaitUntil(() => { reopened.Refresh(); return reopened.Lines.Count == 1; });
        reopened.SourceId.Should().Be(configured.Id);
        (await reopened.CopyTextAsync(new[] { 0 })).Should().Be("new");
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
    public async Task DefaultDecoderReturnsToUtf8WhenAUtf16BomFileIsReplacedWithoutABom()
    {
        File.WriteAllText(_path, "old\nမြန်မာ", Encoding.Unicode);
        using var document = new LogDocument(_path);
        await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 2; });
        (await document.CopyTextAsync(new[] { 0, 1 })).Should().Be("old" + Environment.NewLine + "မြန်မာ");
        File.WriteAllText(_path, "replacement\n😀 UTF8", new UTF8Encoding(false));
        await WaitUntil(() =>
        {
            document.Refresh();
            return document.Lines.Count == 2 && document.GetLine(0).Text == "replacement" && document.GetLine(1).Text == "😀 UTF8";
        });
        (await document.CopyTextAsync(new[] { 0, 1 })).Should().Be("replacement" + Environment.NewLine + "😀 UTF8");
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
            // The source count can update before headless layout realizes any rows.
            window.UpdateLayout();
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
    public async Task TestSameSizeRewriteClearsCachedRowsAndSelection()
    {
        const string original = "old-first\nold-second";
        const string replacement = "new\nmiddle\nlast-line";
        Encoding.UTF8.GetByteCount(replacement).Should().Be(Encoding.UTF8.GetByteCount(original));
        File.WriteAllText(_path, original);
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_path);
            var list = window.GetVisualDescendants().OfType<ListBox>().Single();
            await WaitUntil(() => list.ItemCount == 2 && list.GetVisualDescendants().OfType<TextBlock>()
                .Any(t => t.Text?.EndsWith("  old-first") == true));
            list.Selection.Select(1);
            var modified = File.GetLastWriteTimeUtc(_path);
            File.WriteAllText(_path, replacement);
            File.SetLastWriteTimeUtc(_path, modified.AddSeconds(2));
            await WaitUntil(() => list.ItemCount == 3 && list.Selection.SelectedIndexes.Count == 0 &&
                list.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.EndsWith("  middle") == true));

            list.Selection.Select(1);
            var copy = window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "_Copy selected"));
            copy.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntilAsync(async () => await window.Clipboard!.TryGetTextAsync() == "middle");
            File.AppendAllText(_path, "\nnew-tail");
            await WaitUntil(() => list.ItemCount == 4);
            list.Selection.SelectedIndexes.Should().Equal(new[] { 1 },
                "because ordinary appends after replacement must preserve selection");
            await WaitUntil(() => list.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.EndsWith("  new-tail") == true));
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task TestRenameRotationToLargerFile()
    {
        File.WriteAllText(_path, "old-first\nold-second");
        using var document = new LogDocument(_path);
        await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 2; });
        var cached = document.GetLine(0);
        await WaitUntil(() => cached.Text == "old-first");
        var created = File.GetCreationTimeUtc(_path);
        var modified = File.GetLastWriteTimeUtc(_path);
        var rotated = _path + ".rotated";
        var incoming = _path + ".incoming";
        try
        {
            File.WriteAllText(incoming, "rotated-first\nrotated-second\nrotated-third");
            File.SetCreationTimeUtc(incoming, created.AddMinutes(1));
            File.SetLastWriteTimeUtc(incoming, modified);
            File.Move(_path, rotated);
            File.Move(incoming, _path);
            await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 3; });
            (await document.CopyTextAsync(new[] { 0, 1, 2 })).Should().Be(
                string.Join(Environment.NewLine, "rotated-first", "rotated-second", "rotated-third"),
                "because a larger replacement must not reuse offsets into the old file");
            var current = document.GetLine(0);
            current.Should().NotBeSameAs(cached, "because a replacement invalidates cached rows");
            await WaitUntil(() => current.Text == "rotated-first");
            File.AppendAllText(_path, "\ncontinued");
            await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 4; });
            (await document.CopyTextAsync(new[] { 3 })).Should().Be("continued");
        }
        finally
        {
            File.Delete(rotated);
            File.Delete(incoming);
        }
    }

    [AvaloniaTest]
    public async Task TestMissingFileReappearsWithOriginalMetadata()
    {
        File.WriteAllText(_path, "old-first\nold-second");
        using var document = new LogDocument(_path);
        await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 2; });
        var created = File.GetCreationTimeUtc(_path);
        var modified = File.GetLastWriteTimeUtc(_path);
        File.Delete(_path);
        await WaitUntil(() => { document.Refresh(); return document.Error != null && document.Lines.Count == 0; });
        File.WriteAllText(_path, "new\nmiddle\nlast-line");
        File.SetCreationTimeUtc(_path, created);
        File.SetLastWriteTimeUtc(_path, modified);
        await WaitUntil(() => { document.Refresh(); return document.Error == null && document.Lines.Count == 3; });
        (await document.CopyTextAsync(new[] { 0, 1, 2 })).Should().Be(
            string.Join(Environment.NewLine, "new", "middle", "last-line"),
            "because an observed disappearance invalidates both the fingerprint and read offsets");
    }

    [AvaloniaTest]
    public async Task TestSameSizeRewriteWithPreservedMetadata()
    {
        File.WriteAllText(_path, "first\nsecond");
        using var document = new LogDocument(_path);
        await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 2; });
        var cached = document.GetLine(0);
        await WaitUntil(() => cached.Text == "first");
        var created = File.GetCreationTimeUtc(_path);
        var modified = File.GetLastWriteTimeUtc(_path);
        var length = new FileInfo(_path).Length;
        File.WriteAllText(_path, "new\nrow\nlast");
        File.SetCreationTimeUtc(_path, created);
        File.SetLastWriteTimeUtc(_path, modified);
        new FileInfo(_path).Length.Should().Be(length, "because this regression must not rely on file size changing");
        await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 3; });
        document.GetLine(0).Should().NotBeSameAs(cached, "because byte changes must invalidate rows even when metadata matches");
        (await document.CopyTextAsync(new[] { 0, 1, 2 })).Should().Be(string.Join(Environment.NewLine, "new", "row", "last"));
    }

    [AvaloniaTest]
    public async Task TestCopyTruncateRegrowthWithUnchangedHeader()
    {
        var header = new string('h', 5000);
        File.WriteAllText(_path, header + "\nold-first\nold-second");
        using var document = new LogDocument(_path);
        await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 3; });
        var cached = document.GetLine(1);
        await WaitUntil(() => cached.Text == "old-first");
        File.WriteAllText(_path, header + "\nnew\nmiddle\nlast-line\nnew-tail");
        await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 5; });
        document.GetLine(1).Should().NotBeSameAs(cached,
            "because a truncate-and-regrow between polls must reset even when the first 4 KiB stay the same");
        (await document.CopyTextAsync(new[] { 1, 2, 3, 4 })).Should().Be(
            string.Join(Environment.NewLine, "new", "middle", "last-line", "new-tail"));
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
