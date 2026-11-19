using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAssertions;
using NUnit.Framework;
using Tailviewer.Api;
using Tailviewer.Settings;

namespace Tailviewer.Avalonia.Tests;

[TestFixture]
public sealed class SourcesTest
{
    private string _folder = null!;
    private string _first = null!;
    private string _second = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "tailviewer-sources-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _first = Path.Combine(_folder, "first.log");
        _second = Path.Combine(_folder, "second.log");
        File.WriteAllText(_first, "first\n  original spaces  ");
        File.WriteAllText(_second, "second");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [AvaloniaTest]
    public async Task ReopeningTheSameFileRetainsOneSourceTreeEntry()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            await window.OpenFileAsync(_first);
            var sources = window.GetVisualDescendants().OfType<TreeView>().SingleOrDefault();
            sources.Should().NotBeNull("because open sources must remain selectable in a source tree");
            sources!.ItemCount.Should().Be(1, "because reopening the same canonical path must reuse its source");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task OpeningAFolderRetainsTheFileAndAddsItsMatchingLogs()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            await window.OpenFileAsync(_folder);
            window.GetVisualDescendants().OfType<TextBlock>().Should().NotContain(text =>
                text.Text != null && text.Text.StartsWith("Cannot open file:"),
                "because a folder path must open a folder source rather than be rejected as a file");
            var sources = window.GetVisualDescendants().OfType<TreeView>().SingleOrDefault();
            sources.Should().NotBeNull("because folder arguments must create a selectable folder source");
            sources!.ItemCount.Should().Be(2, "because adding a folder must preserve the already open file");
            await WaitUntil(() => Rows(window).ItemCount == 3);
            File.ReadAllText(_second).Should().Be("second");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task SavedSelectedSourceReopensWithItsOriginalIdentityAndContent()
    {
        var profile = Path.Combine(_folder, "settings.xml");
        var first = new DataSource(_first) { Id = DataSourceId.CreateNew() };
        var second = new DataSource(_second) { Id = DataSourceId.CreateNew() };
        var settings = new ApplicationSettings(profile);
        settings.DataSources.Add(first);
        settings.DataSources.Add(second);
        settings.DataSources.SelectedItem = second.Id;
        settings.Save().Should().BeTrue();
        var reopened = new ApplicationSettings(profile);
        reopened.Restore();
        var selected = reopened.DataSources.Single(source => source.Id == reopened.DataSources.SelectedItem);
        selected.Id.Should().Be(second.Id);
        selected.File.Should().Be(_second);
        using var document = new LogDocument(selected);
        await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 1; });
        document.SourceId.Should().Be(second.Id);
        (await document.CopyTextAsync(new[] { 0 })).Should().Be("second");
    }

    [AvaloniaTest]
    public async Task OpeningAndSwitchingFilesCopiesOnlyTheActiveSource()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            var rows = Rows(window);
            await WaitUntil(() => rows.ItemCount == 2);
            await Copy(window, 1, "  original spaces  ");
            await window.OpenFileAsync(_second);
            await WaitUntil(() => rows.ItemCount == 1);
            window.Title.Should().Contain("second.log");
            await Copy(window, 0, "second");
            File.ReadAllText(_first).Should().Be("first\n  original spaces  ");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task MissingOpenRetainsTheCurrentRowsAndCopyableText()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            await window.OpenFileAsync(Path.Combine(_folder, "missing.log"));
            Rows(window).ItemCount.Should().Be(2);
            window.Title.Should().Contain("first.log");
            window.GetVisualDescendants().OfType<TextBlock>().Should().Contain(text =>
                text.Text != null && text.Text.StartsWith("Cannot open file:"));
            await Copy(window, 0, "first");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task OpeningAfterWindowClosureDoesNotPublishRows()
    {
        var window = new MainWindow();
        window.Show();
        var rows = Rows(window);
        window.Close();
        await window.OpenFileAsync(_first);
        rows.ItemCount.Should().Be(0);
        File.ReadAllText(_first).Should().Be("first\n  original spaces  ");
    }

    [AvaloniaTest]
    public async Task MissingConfiguredSourceCanRecoverWithoutChangingItsIdentity()
    {
        var path = Path.Combine(_folder, "later.log");
        var source = new DataSource(path) { Id = DataSourceId.CreateNew() };
        var configuration = new SourceConfiguration(new DataSourceSettings { source });
        Assert.Throws<FileNotFoundException>(() => new LogDocument(configuration.Snapshot().Single()));
        configuration.Snapshot().Single().Id.Should().Be(source.Id);
        configuration.Snapshot().Single().File.Should().Be(path);
        File.WriteAllText(path, "recovered");
        using var document = new LogDocument(configuration.Snapshot().Single());
        await WaitUntil(() => { document.Refresh(); return document.Lines.Count == 1; });
        document.SourceId.Should().Be(source.Id);
        (await document.CopyTextAsync(new[] { 0 })).Should().Be("recovered");
    }

    [AvaloniaTest]
    public async Task GroupMemberIdentitiesSelectTheirOwnContentDespiteEqualDisplayNames()
    {
        var group = DataSourceId.CreateNew();
        var first = new DataSource(_first) { Id = DataSourceId.CreateNew(), ParentId = group, DisplayName = "same" };
        var second = new DataSource(_second) { Id = DataSourceId.CreateNew(), ParentId = group, DisplayName = "same" };
        var settings = new DataSourceSettings { new DataSource { Id = group }, first, second };
        var configuration = new SourceConfiguration(settings);
        var members = configuration.GetMembers(group);
        members.Select(member => member.Id).Should().Equal(first.Id, second.Id);
        using var firstDocument = new LogDocument(members[0]);
        using var secondDocument = new LogDocument(members[1]);
        await WaitUntil(() =>
        {
            firstDocument.Refresh();
            secondDocument.Refresh();
            return firstDocument.Lines.Count == 2 && secondDocument.Lines.Count == 1;
        });
        firstDocument.SourceId.Should().Be(first.Id);
        secondDocument.SourceId.Should().Be(second.Id);
        (await firstDocument.CopyTextAsync(new[] { 0 })).Should().Be("first");
        (await secondDocument.CopyTextAsync(new[] { 0 })).Should().Be("second");
    }

    private static ListBox Rows(MainWindow window) => window.GetVisualDescendants().OfType<ListBox>().Single();

    private static async Task Copy(MainWindow window, int index, string expected)
    {
        await window.Clipboard!.SetTextAsync("pending-" + Guid.NewGuid().ToString("N"));
        Rows(window).Selection.Select(index);
        window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "_Copy selected"))
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(8))
        {
            Dispatcher.UIThread.RunJobs();
            if (await window.Clipboard!.TryGetTextAsync() == expected) return;
            await Task.Delay(10);
        }
        (await window.Clipboard!.TryGetTextAsync()).Should().Be(expected);
    }

    private static async Task WaitUntil(Func<bool> ready)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(8))
        {
            Dispatcher.UIThread.RunJobs();
            if (ready()) return;
            await Task.Delay(10);
        }
        ready().Should().BeTrue("because the source and UI must converge within eight seconds");
    }
}
