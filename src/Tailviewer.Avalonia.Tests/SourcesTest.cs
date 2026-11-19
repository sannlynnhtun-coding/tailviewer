using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Platform.Storage;
using FluentAssertions;
using NUnit.Framework;
using Tailviewer.Api;
using Tailviewer.Settings;
using Tailviewer.Avalonia.ViewModels;

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
    public void TearDown()
    {
        var owned = Path.GetFullPath(_folder);
        Path.GetDirectoryName(owned).Should().Be(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())));
        Directory.Delete(owned, recursive: true);
    }

    [AvaloniaTest]
    public async Task NewGroupCreatesANamedSelectableSourceWithAStableIdentity()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            Command(window, "New group").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            await WaitUntil(() => tree.ItemCount == 1);
            var group = (DataSource)tree.SelectedItem!;
            group.Id.Should().NotBe(DataSourceId.Empty);
            group.DisplayName.Should().Be("Merged Data Source");
            group.File.Should().BeNull();
            group.LogFileFolderPath.Should().BeNull();
            window.Title.Should().Contain("Merged Data Source");
            Command(window, "Close current source").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntil(() => tree.ItemCount == 0);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ContextMenuGroupingPreservesIdsAndClosingTheGroupKeepsItsFile()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var file = (DataSource)tree.SelectedItem!;
            Command(window, "New group").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var group = (DataSource)tree.SelectedItem!;
            await AddThroughMenu(tree, file, group);
            await WaitUntil(() => tree.ItemCount == 1 && Rows(window).ItemCount == 2);
            ((DataSource)tree.SelectedItem!).Id.Should().Be(file.Id);
            file.ParentId.Should().Be(group.Id);
            await Copy(window, 0, "first");
            await window.OpenFileAsync(_first);
            tree.ItemCount.Should().Be(1, "because a configured group member must not be duplicated at the root");
            var profile = new ApplicationSettings(Path.Combine(_folder, "group-profile.xml"));
            foreach (var source in ((SourcesViewModel)window.DataContext!).All) profile.DataSources.Add(source.Clone());
            profile.DataSources.SelectedItem = file.Id;
            profile.Save().Should().BeTrue();
            var reopened = new ApplicationSettings(Path.Combine(_folder, "group-profile.xml"));
            reopened.Restore();
            reopened.DataSources.Single(source => source.Id == file.Id).ParentId.Should().Be(group.Id);
            reopened.DataSources.Single(source => source.Id == group.Id).DisplayName.Should().Be("Merged Data Source");
            tree.SelectedItem = group;
            Command(window, "Close current source").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntil(() => tree.ItemCount == 1 && Rows(window).ItemCount == 2);
            ((DataSource)tree.SelectedItem!).Id.Should().Be(file.Id);
            file.ParentId.Should().Be(DataSourceId.Empty);
            File.ReadAllText(_first).Should().Be("first\n  original spaces  ");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task InvalidGroupCycleReportsAnErrorWithoutChangingMembership()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            Command(window, "New group").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var group = (DataSource)tree.SelectedItem!;
            await AddThroughMenu(tree, group, group);
            await WaitUntil(() => window.GetVisualDescendants().OfType<TextBlock>().Any(text =>
                text.Text != null && text.Text.StartsWith("Cannot change group:")));
            await Task.Delay(150);
            Dispatcher.UIThread.RunJobs();
            window.GetVisualDescendants().OfType<TextBlock>().Should().Contain(text =>
                text.Text != null && text.Text.StartsWith("Cannot change group:"));
            tree.ItemCount.Should().Be(1);
            group.ParentId.Should().Be(DataSourceId.Empty);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task NestedGroupCycleIsRejectedByTheContextMenu()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            Command(window, "New group").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var first = (DataSource)tree.SelectedItem!;
            Command(window, "New group").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var second = (DataSource)tree.SelectedItem!;
            await AddThroughMenu(tree, first, second);
            first.ParentId.Should().Be(second.Id);
            await AddThroughMenu(tree, second, first);
            await WaitUntil(() => window.GetVisualDescendants().OfType<TextBlock>().Any(text =>
                text.Text != null && text.Text.StartsWith("Cannot change group:")));
            first.ParentId.Should().Be(second.Id);
            second.ParentId.Should().Be(DataSourceId.Empty);
            tree.ItemCount.Should().Be(1);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task MissingFolderKeepsItsConfiguredIdentityAndRecoversInTheTree()
    {
        var watched = Path.Combine(_folder, "watched");
        Directory.CreateDirectory(watched);
        var original = Path.Combine(watched, "original.log");
        File.WriteAllText(original, "original");
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(watched);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var folder = (DataSource)tree.SelectedItem!;
            await WaitUntil(() => (tree.ContainerFromItem(folder) as TreeViewItem)?.ItemCount == 1);
            File.Delete(original);
            Directory.Delete(watched);
            await WaitUntil(() => window.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Folder is unavailable."));
            tree.ItemCount.Should().Be(1);
            ((DataSource)tree.SelectedItem!).Id.Should().Be(folder.Id);
            Directory.CreateDirectory(watched);
            var recovered = Path.Combine(watched, "recovered.log");
            File.WriteAllText(recovered, "recovered");
            await WaitUntil(() => (tree.ContainerFromItem(folder) as TreeViewItem)?.ItemCount == 1 &&
                ((TreeViewItem)tree.ContainerFromItem(folder)!).Items.OfType<DataSource>().Single().File == recovered);
            tree.SelectedItem = ((TreeViewItem)tree.ContainerFromItem(folder)!).Items.OfType<DataSource>().Single();
            await WaitUntil(() => Rows(window).ItemCount == 1);
            await Copy(window, 0, "recovered");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ClosingTheCurrentRootActivatesTheRemainingFileWithoutDeletingLogs()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            await window.OpenFileAsync(_second);
            await WaitUntil(() => Rows(window).ItemCount == 1);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            Command(window, "Close current source").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntil(() => tree.ItemCount == 1 && Rows(window).ItemCount == 2);
            ((DataSource)tree.SelectedItem!).File.Should().Be(_first);
            await Copy(window, 0, "first");
            File.ReadAllText(_second).Should().Be("second");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ClosingTheLastSourceClearsRowsAndReleasesTheFile()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            Command(window, "Close current source").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntil(() => window.GetVisualDescendants().OfType<TreeView>().Single().ItemCount == 0 && Rows(window).ItemCount == 0);
            Command(window, "Close current source").IsEnabled.Should().BeFalse();
            Command(window, "Close all sources").IsEnabled.Should().BeFalse();
            await WaitUntil(() =>
            {
                try
                {
                    using var exclusive = new FileStream(_first, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    return true;
                }
                catch (IOException) { return false; }
            });
            File.ReadAllText(_first).Should().Be("first\n  original spaces  ");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ClosingAllReleasesFoldersAndAllowsANewWorkspace()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await window.OpenFileAsync(_folder);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var folder = tree.Items.OfType<DataSource>().Single(source => source.LogFileFolderPath != null);
            await WaitUntil(() => (tree.ContainerFromItem(folder) as TreeViewItem)?.ItemCount == 2);
            Command(window, "Close all sources").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            tree.ItemCount.Should().Be(0);
            Rows(window).ItemCount.Should().Be(0);
            File.ReadAllText(_first).Should().Be("first\n  original spaces  ");
            File.ReadAllText(_second).Should().Be("second");
            File.WriteAllText(Path.Combine(_folder, "later.log"), "later");
            Dispatcher.UIThread.RunJobs();
            tree.ItemCount.Should().Be(0);
            await window.OpenFileAsync(_folder);
            var reopened = tree.Items.OfType<DataSource>().Single();
            await WaitUntil(() => (tree.ContainerFromItem(reopened) as TreeViewItem)?.ItemCount == 3);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ClosingAFolderKeepsItsStandaloneFileAndDoesNotDeleteChildren()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await window.OpenFileAsync(_folder);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            Command(window, "Close current source").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await WaitUntil(() => tree.ItemCount == 1 && Rows(window).ItemCount == 2);
            ((DataSource)tree.SelectedItem!).File.Should().Be(_first);
            tree.Items.OfType<DataSource>().Should().NotContain(source => source.LogFileFolderPath != null);
            await Copy(window, 0, "first");
            File.ReadAllText(_second).Should().Be("second");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task FolderManagedChildrenCannotBeClosedIndividually()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_folder);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var root = tree.Items.OfType<DataSource>().Single();
            await WaitUntil(() => (tree.ContainerFromItem(root) as TreeViewItem)?.ItemCount == 2);
            var container = (TreeViewItem)tree.ContainerFromItem(root)!;
            tree.SelectedItem = container.Items.OfType<DataSource>().Single(source => source.File == _second);
            await WaitUntil(() => Rows(window).ItemCount == 1);
            var close = Command(window, "Close current source");
            close.IsEnabled.Should().BeFalse("because the legacy folder owns its discovered children");
            close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            tree.ItemCount.Should().Be(1);
            container.ItemCount.Should().Be(2);
            await Copy(window, 0, "second");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ClosingAllCannotBeUndoneByAnOutstandingOpen()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            var close = Command(window, "Close all sources");
            var opening = window.OpenFileAsync(_second);
            close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await opening;
            window.GetVisualDescendants().OfType<TreeView>().Single().ItemCount.Should().Be(0);
            Rows(window).ItemCount.Should().Be(0);
        }
        finally { window.Close(); }
    }

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
    public async Task SelectingAnEarlierSourceRetainsItsIdentityAndPausedFollowState()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var first = (DataSource)tree.SelectedItem!;
            await Copy(window, 0, "first");
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked.Should().BeFalse();
            await window.OpenFileAsync(_second);
            await WaitUntil(() => Rows(window).ItemCount == 1);
            tree.ItemCount.Should().Be(2);
            tree.SelectedItem = first;
            await WaitUntil(() => Rows(window).ItemCount == 2 && window.Title!.Contains("first.log"));
            ((DataSource)tree.SelectedItem!).Id.Should().Be(first.Id);
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked.Should().BeFalse();
            await Copy(window, 1, "  original spaces  ");
            await window.OpenFileAsync(Path.Combine(_folder, ".", "first.log"));
            tree.ItemCount.Should().Be(2);
            ((DataSource)tree.SelectedItem!).Id.Should().Be(first.Id);
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
            var folder = sources.Items.OfType<DataSource>().Single(source => source.LogFileFolderPath == _folder);
            await WaitUntil(() => (sources.ContainerFromItem(folder) as TreeViewItem)?.ItemCount == 2);
            var children = ((TreeViewItem)sources.ContainerFromItem(folder)!).Items.OfType<DataSource>().ToArray();
            children.Select(source => source.File).Should().BeEquivalentTo(_first, _second);
            sources.SelectedItem = children.Single(source => source.File == _first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            await Copy(window, 1, "  original spaces  ");
            sources.SelectedItem = children.Single(source => source.File == _second);
            await WaitUntil(() => Rows(window).ItemCount == 1);
            await Copy(window, 0, "second");
            sources.ItemCount.Should().Be(2, "because folder members must not be duplicated as root entries");
            File.ReadAllText(_second).Should().Be("second");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task LiveFolderMembershipKeepsTheSelectedChildAndItsIdentity()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_folder);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var folder = tree.Items.OfType<DataSource>().Single();
            await WaitUntil(() => (tree.ContainerFromItem(folder) as TreeViewItem)?.ItemCount == 2);
            var container = (TreeViewItem)tree.ContainerFromItem(folder)!;
            var second = container.Items.OfType<DataSource>().Single(source => source.File == _second);
            tree.SelectedItem = second;
            await WaitUntil(() => Rows(window).ItemCount == 1);
            await Copy(window, 0, "second");
            var added = Path.Combine(_folder, "aaa.log");
            File.WriteAllText(added, "new");
            await WaitUntil(() => container.ItemCount == 3);
            tree.SelectedItem.Should().BeSameAs(second);
            ((DataSource)tree.SelectedItem!).Id.Should().Be(second.Id);
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked.Should().BeFalse();
            await Copy(window, 0, "second");
            File.Delete(added);
            await WaitUntil(() => container.ItemCount == 2);
            tree.SelectedItem.Should().BeSameAs(second);
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

    [AvaloniaTest]
    public async Task EmptyBatchPreservesTheActiveSourceSelectionAndFollowState()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            Rows(window).Selection.Select(1);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var identity = ((DataSource)tree.SelectedItem!).Id;
            await window.OpenFilesAsync(Array.Empty<string>());
            ((DataSource)tree.SelectedItem!).Id.Should().Be(identity);
            Rows(window).Selection.SelectedIndexes.Should().Equal(1);
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked.Should().BeFalse();
            await Copy(window, 1, "  original spaces  ");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task BatchKeepsValidFilesAndReportsEachFailedPath()
    {
        var missing = Path.Combine(_folder, "missing-one.log");
        var otherMissing = Path.Combine(_folder, "missing-two.log");
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFilesAsync(new[] { missing, _first, otherMissing, _second });
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            tree.ItemCount.Should().Be(2);
            ((DataSource)tree.SelectedItem!).File.Should().Be(_second);
            window.GetVisualDescendants().OfType<TextBlock>().Should().Contain(block => block.Text != null &&
                block.Text.Contains("missing-one.log") && block.Text.Contains("missing-two.log"));
            await WaitUntil(() => Rows(window).ItemCount == 1);
            await Copy(window, 0, "second");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ClosingAllDuringABatchDoesNotOpenTheRemainingPaths()
    {
        var third = Path.Combine(_folder, "third.log");
        File.WriteAllText(third, "third");
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            var batch = window.OpenFilesAsync(new[] { _second, third });
            Command(window, "Close all sources").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await batch;
            window.GetVisualDescendants().OfType<TreeView>().Single().ItemCount.Should().Be(0);
            Rows(window).ItemCount.Should().Be(0);
            File.ReadAllText(third).Should().Be("third");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task FileDropOpensBothFilesWithoutMovingOrDuplicatingThem()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            using var data = new DataTransfer();
            DragDrop.GetAllowDrop(window).Should().BeTrue();
            data.Add(DataTransferItem.CreateFile(await window.StorageProvider.TryGetFileFromPathAsync(_first)));
            data.Add(DataTransferItem.CreateFile(await window.StorageProvider.TryGetFileFromPathAsync(_second)));
            var over = new DragEventArgs(DragDrop.DragOverEvent, data, window, new Point(300, 200), KeyModifiers.None)
                { DragEffects = DragDropEffects.Copy | DragDropEffects.Move };
            window.RaiseEvent(over);
            over.DragEffects.Should().Be(DragDropEffects.Copy, "because opening logs must never request that the source move files");
            foreach (var attempt in new[] { 0, 1 })
            {
                window.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, data, window, new Point(300, 200), KeyModifiers.None)
                    { DragEffects = DragDropEffects.Copy });
                var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
                await WaitUntil(() => tree.ItemCount == 2 && ((DataSource?)tree.SelectedItem)?.File == _second && Rows(window).ItemCount == 1 &&
                    window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "_Open file…")).IsEnabled);
                await Copy(window, 0, "second");
            }
            File.ReadAllText(_first).Should().Be("first\n  original spaces  ");
            File.ReadAllText(_second).Should().Be("second");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task TextDropIsRejectedWithoutChangingTheOpenSource()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            using var data = new DataTransfer();
            data.Add(DataTransferItem.CreateText(_second));
            foreach (var routedEvent in new[] { DragDrop.DragOverEvent, DragDrop.DropEvent })
            {
                var args = new DragEventArgs(routedEvent, data, window, new Point(300, 200), KeyModifiers.None)
                    { DragEffects = DragDropEffects.Copy | DragDropEffects.Move };
                window.RaiseEvent(args);
                args.DragEffects.Should().Be(DragDropEffects.None);
            }
            window.GetVisualDescendants().OfType<TreeView>().Single().ItemCount.Should().Be(1);
            await Copy(window, 0, "first");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task FolderAndFileDropUseTheExistingWorkspaceAndWatcher()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            using var data = new DataTransfer();
            data.Add(DataTransferItem.CreateFile(await window.StorageProvider.TryGetFolderFromPathAsync(_folder)));
            data.Add(DataTransferItem.CreateFile(await window.StorageProvider.TryGetFileFromPathAsync(_second)));
            window.RaiseEvent(new DragEventArgs(DragDrop.DropEvent, data, window, new Point(300, 200), KeyModifiers.None)
                { DragEffects = DragDropEffects.Link | DragDropEffects.Copy });
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            await WaitUntil(() => tree.ItemCount == 3 && ((DataSource?)tree.SelectedItem)?.File == _second && Rows(window).ItemCount == 1);
            var folder = ((SourcesViewModel)window.DataContext!).Roots.Single(source => source.LogFileFolderPath != null);
            await WaitUntil(() => tree.ContainerFromItem(folder) is TreeViewItem item && item.ItemCount == 2);
            await Copy(window, 0, "second");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task FileDropNegotiationPrefersLinkAndRejectsMoveOnly()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            using var data = new DataTransfer();
            data.Add(DataTransferItem.CreateFile(await window.StorageProvider.TryGetFileFromPathAsync(_first)));
            foreach (var (allowed, expected) in new[]
                     {
                         (DragDropEffects.Link | DragDropEffects.Copy | DragDropEffects.Move, DragDropEffects.Link),
                         (DragDropEffects.Copy | DragDropEffects.Move, DragDropEffects.Copy),
                         (DragDropEffects.Move, DragDropEffects.None)
                     })
            {
                var args = new DragEventArgs(DragDrop.DragOverEvent, data, window, new Point(300, 200), KeyModifiers.None)
                    { DragEffects = allowed };
                window.RaiseEvent(args);
                args.DragEffects.Should().Be(expected);
            }
            var drop = new DragEventArgs(DragDrop.DropEvent, data, window, new Point(300, 200), KeyModifiers.None)
                { DragEffects = DragDropEffects.Move };
            window.RaiseEvent(drop);
            drop.DragEffects.Should().Be(DragDropEffects.None);
            window.GetVisualDescendants().OfType<TreeView>().Single().ItemCount.Should().Be(0);
            File.Exists(_first).Should().BeTrue();
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ThrowingDropProviderDoesNotApplyAValidPrefix()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            using var data = new DataTransfer();
            data.Add(DataTransferItem.CreateFile(await window.StorageProvider.TryGetFileFromPathAsync(_second)));
            data.Add(DataTransferItem.Create<IStorageItem>(DataFormat.File, () => throw new IOException("owned transfer failure")));
            var args = new DragEventArgs(DragDrop.DropEvent, data, window, new Point(300, 200), KeyModifiers.None)
                { DragEffects = DragDropEffects.Copy };
            window.RaiseEvent(args);
            args.DragEffects.Should().Be(DragDropEffects.None);
            window.GetVisualDescendants().OfType<TreeView>().Single().ItemCount.Should().Be(1);
            window.GetVisualDescendants().OfType<TextBlock>().Should().Contain(block =>
                block.Text != null && block.Text.Contains("Cannot open dropped items: owned transfer failure"));
            await Copy(window, 0, "first");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task GroupMemberCanBeExcludedAndIncludedWithoutClosingItsLog()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var file = (DataSource)tree.SelectedItem!;
            Command(window, "New group").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var group = (DataSource)tree.SelectedItem!;
            await AddThroughMenu(tree, file, group);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            foreach (var exclude in new[] { true, false })
            {
                var parent = (TreeViewItem)tree.ContainerFromItem(group)!;
                await WaitUntil(() => parent.ContainerFromItem(file) is TreeViewItem item && item.ContextMenu != null);
                var container = (TreeViewItem)parent.ContainerFromItem(file)!;
                var menu = container.ContextMenu!;
                container.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = InputElement.ContextRequestedEvent });
                try
                {
                    await WaitUntil(() => menu.IsOpen);
                    var command = menu.Items.OfType<MenuItem>().SingleOrDefault(item =>
                        Equals(item.Header, exclude ? "Exclude from group" : "Include in group"));
                    command.Should().NotBeNull("because group exclusion must be reversible through the source menu");
                    command!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    group.ExcludedDataSources.Contains(file.Id).Should().Be(exclude);
                    var profilePath = Path.Combine(_folder, "exclusion-profile.xml");
                    var profile = new ApplicationSettings(profilePath);
                    foreach (var item in ((SourcesViewModel)window.DataContext!).All) profile.DataSources.Add(item.Clone());
                    profile.Save().Should().BeTrue();
                    var restored = new ApplicationSettings(profilePath);
                    restored.Restore();
                    restored.DataSources.Single(item => item.Id == group.Id).ExcludedDataSources.Contains(file.Id).Should().Be(exclude);
                    ((DataSource)tree.SelectedItem!).Id.Should().Be(file.Id);
                    file.ParentId.Should().Be(group.Id);
                    await Copy(window, 1, "  original spaces  ");
                }
                finally { menu.Close(); }
            }
            File.ReadAllText(_first).Should().Be("first\n  original spaces  ");
        }
        finally { window.Close(); }
    }
    [AvaloniaTest]
    public async Task GroupRenameCommitsOrCancelsWithoutSwitchingTheActiveFile()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            Command(window, "New group").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var group = (DataSource)tree.SelectedItem!;
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            var file = (DataSource)tree.SelectedItem!;
            foreach (var commit in new[] { true, false })
            {
                await WaitUntil(() => tree.ContainerFromItem(group) is TreeViewItem item && item.ContextMenu != null);
                var container = (TreeViewItem)tree.ContainerFromItem(group)!;
                var menu = container.ContextMenu!;
                container.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = InputElement.ContextRequestedEvent });
                await WaitUntil(() => menu.IsOpen);
                var rename = menu.Items.OfType<MenuItem>().SingleOrDefault(item => Equals(item.Header, "Rename group"));
                rename.Should().NotBeNull("because legacy merged sources support renaming");
                rename!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                menu.Close();
                await WaitUntil(() => container.GetVisualDescendants().OfType<TextBox>().Any(box => box.IsVisible));
                var editor = container.GetVisualDescendants().OfType<TextBox>().Single(box => box.IsVisible);
                AutomationProperties.GetName(editor).Should().Be("Group name");
                editor.Text = commit ? "မြန်မာ 😀 group" : "discard this";
                editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = commit ? Key.Enter : Key.Escape });
                group.DisplayName.Should().Be("မြန်မာ 😀 group");
                ((DataSource)tree.SelectedItem!).Id.Should().Be(file.Id);
                container.GetVisualDescendants().OfType<TextBlock>().Should().Contain(block => block.Text == group.DisplayName);
                ((SourcesViewModel)window.DataContext!).FindNavigationMatches("မြန်မာ").Single().DataSource.Id.Should().Be(group.Id);
                await Copy(window, 1, "  original spaces  ");
            }
            var profilePath = Path.Combine(_folder, "rename-profile.xml");
            var profile = new ApplicationSettings(profilePath);
            foreach (var item in ((SourcesViewModel)window.DataContext!).All) profile.DataSources.Add(item.Clone());
            profile.Save().Should().BeTrue();
            var restored = new ApplicationSettings(profilePath);
            restored.Restore();
            restored.DataSources.Single(item => item.Id == group.Id).DisplayName.Should().Be("မြန်မာ 😀 group");
            file.File.Should().Be(_first);
        }
        finally { window.Close(); }
    }
    [AvaloniaTest]
    public async Task SourceTreeShowsEmptyGroupFolderAndFileStatesAndClearsAfterAppend()
    {
        var empty = Path.Combine(_folder, "empty.log");
        File.WriteAllText(empty, "");
        var watched = Path.Combine(_folder, "empty-folder");
        Directory.CreateDirectory(watched);
        var window = new MainWindow();
        window.Show();
        try
        {
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            Command(window, "New group").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var group = (DataSource)tree.SelectedItem!;
            await WaitUntil(() => SourceStatus(tree, group) == "No sources in this group.");
            await window.OpenFileAsync(watched);
            var folder = (DataSource)tree.SelectedItem!;
            await WaitUntil(() => SourceStatus(tree, folder) == "No matching files.");
            Directory.Delete(watched);
            await WaitUntil(() => SourceStatus(tree, folder) == "Folder is unavailable.");
            Directory.CreateDirectory(watched);
            await WaitUntil(() => SourceStatus(tree, folder) == "No matching files.");
            await window.OpenFileAsync(empty);
            var file = (DataSource)tree.SelectedItem!;
            await WaitUntil(() => SourceStatus(tree, file) == "No log lines.");
            File.AppendAllText(empty, "arrived");
            await WaitUntil(() => Rows(window).ItemCount == 1 && string.IsNullOrEmpty(SourceStatus(tree, file)));
            ((DataSource)tree.SelectedItem!).Id.Should().Be(file.Id);
            await Copy(window, 0, "arrived");
            File.Delete(empty);
            await WaitUntil(() => Rows(window).ItemCount == 0 && !string.IsNullOrEmpty(SourceStatus(tree, file)) &&
                SourceStatus(tree, file) != "No log lines." && SourceStatus(tree, file) != "Loading…");
            File.WriteAllText(empty, "recovered");
            await WaitUntil(() => Rows(window).ItemCount == 1 && string.IsNullOrEmpty(SourceStatus(tree, file)));
            await Copy(window, 0, "recovered");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task MissingConfiguredTreeItemKeepsItsErrorAndIdentityUntilSuccessfulRetry()
    {
        var path = Path.Combine(_folder, "later-tree.log");
        var missing = new DataSource(path) { Id = DataSourceId.CreateNew() };
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var first = (DataSource)tree.SelectedItem!;
            ((SourcesViewModel)window.DataContext!).Roots.Add(missing);
            tree.SelectedItem = missing;
            await WaitUntil(() => SourceStatus(tree, missing)?.StartsWith("Cannot open file: ") == true);
            tree.ItemCount.Should().Be(2);
            missing.File.Should().Be(path);
            await Copy(window, 0, "first");
            File.WriteAllText(path, "available");
            tree.SelectedItem = first;
            tree.SelectedItem = missing;
            await WaitUntil(() => Rows(window).ItemCount == 1 && string.IsNullOrEmpty(SourceStatus(tree, missing)));
            ((DataSource)tree.SelectedItem!).Id.Should().Be(missing.Id);
            await Copy(window, 0, "available");
        }
        finally { window.Close(); }
    }
    private static string? SourceStatus(TreeView tree, DataSource source) =>
        (tree.ContainerFromItem(source) as TreeViewItem)?.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(block => AutomationProperties.GetName(block) == "Source status")?.Text;
    private static Button Command(MainWindow window, string name)
    {
        var button = window.GetVisualDescendants().OfType<Button>()
            .SingleOrDefault(control => AutomationProperties.GetName(control) == name);
        button.Should().NotBeNull("because the workspace must expose " + name);
        return button!;
    }

    private static async Task AddThroughMenu(TreeView tree, DataSource member, DataSource group)
    {
        await WaitUntil(() => tree.ContainerFromItem(member) is TreeViewItem item && item.ContextMenu != null);
        var container = (TreeViewItem)tree.ContainerFromItem(member)!;
        var menu = container.ContextMenu!;
        container.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = InputElement.ContextRequestedEvent });
        try
        {
            await WaitUntil(() => menu.IsOpen);
            await WaitUntil(() => menu.Items.OfType<MenuItem>().Any(item => Equals(item.Header, "Add to group") &&
                item.Items.OfType<MenuItem>().Any(target => Equals(target.Tag, group.Id))));
            var add = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Add to group"));
            add.Items.OfType<MenuItem>().Single(item => Equals(item.Tag, group.Id))
                .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }
        finally { menu.Close(); }
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
