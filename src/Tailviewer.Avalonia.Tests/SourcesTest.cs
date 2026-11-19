using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Headless;
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
using Tailviewer.Avalonia.Views;

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
    public async Task SwitchingSourcesRestoresLineSelectionAndThePausedViewport()
    {
        File.WriteAllText(_first, string.Join('\n', Enumerable.Range(0, 2000).Select(index => index + " " + new string('x', 400))));
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2000);
            window.UpdateLayout();
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var first = (DataSource)tree.SelectedItem!;
            Rows(window).Selection.Select(1000);
            Rows(window).Selection.Select(1002);
            await WaitUntil(() => Rows(window).GetVisualDescendants().OfType<SelectableTextBlock>()
                .Any(text => text.Text?.Contains("1000 " + new string('x', 400)) == true));
            Rows(window).ScrollIntoView(1400);
            await WaitUntil(() => Rows(window).GetVisualDescendants().OfType<SelectableTextBlock>()
                .Any(text => text.Text?.Contains("1400 " + new string('x', 400)) == true));
            var scroll = Rows(window).GetVisualDescendants().OfType<ScrollViewer>().Single();
            scroll.Offset = new Vector(140, scroll.Offset.Y);
            await WaitUntil(() => scroll.Offset.X > 100 && scroll.Offset.Y > 1000);
            var offset = scroll.Offset;
            await window.OpenFileAsync(_second);
            await WaitUntil(() => Rows(window).ItemCount == 1);
            tree.SelectedItem = first;
            await WaitUntil(() => Rows(window).ItemCount == 2000);
            Rows(window).Selection.SelectedIndexes.Should().Equal(1000, 1002);
            await WaitUntil(() => Math.Abs(scroll.Offset.X - offset.X) < 1 && Math.Abs(scroll.Offset.Y - offset.Y) < 1);
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked.Should().BeFalse();
            var expected = "1000 " + new string('x', 400) + Environment.NewLine + "1002 " + new string('x', 400);
            await CopySelected(window, expected);
            await window.OpenFileAsync(_second);
            await window.OpenFileAsync(_first);
            await window.OpenFileAsync(_second);
            Dispatcher.UIThread.RunJobs();
            Rows(window).ItemCount.Should().Be(1);
            await Copy(window, 0, "second");
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Math.Abs(scroll.Offset.X - offset.X) < 1 && Math.Abs(scroll.Offset.Y - offset.Y) < 1);
            Rows(window).Selection.SelectedIndexes.Should().Equal(1000, 1002);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ReturningToAFollowingSourceRetainsSelectionWithoutPausingItsTail()
    {
        File.WriteAllText(_first, string.Join('\n', Enumerable.Range(0, 2000).Select(index => index + " " + new string('x', 400))));
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2000);
            window.UpdateLayout();
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var first = (DataSource)tree.SelectedItem!;
            Rows(window).Selection.Select(0);
            Rows(window).ScrollIntoView(0);
            await WaitUntil(() => Rows(window).GetVisualDescendants().OfType<SelectableTextBlock>()
                .Any(text => text.Text?.Contains("      1  0 " + new string('x', 400)) == true));
            var follow = window.GetVisualDescendants().OfType<CheckBox>().Single();
            follow.IsChecked = true;
            await WaitUntil(() => Rows(window).GetVisualDescendants().OfType<SelectableTextBlock>()
                .Any(text => text.Text?.Contains("1999 " + new string('x', 400)) == true));
            var scroll = Rows(window).GetVisualDescendants().OfType<ScrollViewer>().Single();
            scroll.Offset = new Vector(140, scroll.Offset.Y);
            await WaitUntil(() => scroll.Offset.X > 100);
            var horizontal = scroll.Offset.X;
            await window.OpenFileAsync(_second);
            tree.SelectedItem = first;
            await WaitUntil(() => Rows(window).ItemCount == 2000);
            Rows(window).Selection.SelectedIndexes.Should().Equal(0);
            follow.IsChecked.Should().BeTrue();
            await WaitUntil(() => Math.Abs(scroll.Offset.X - horizontal) < 1);
            await WaitUntil(() => Rows(window).GetVisualDescendants().OfType<SelectableTextBlock>()
                .Any(text => text.Text?.Contains("1999 " + new string('x', 400)) == true));
            await CopySelected(window, "0 " + new string('x', 400));
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task InactiveSourceKeepsIndexingAndReusesItsDocumentWhenSelectedAgain()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var first = (DataSource)tree.SelectedItem!;
            var firstLines = (global::Avalonia.Collections.AvaloniaList<int>)Rows(window).ItemsSource!;
            await Copy(window, 0, "first");
            await window.OpenFileAsync(_second);
            await WaitUntil(() => Rows(window).ItemCount == 1);
            File.AppendAllText(_first, "\nappended while inactive");
            await WaitUntil(() => firstLines.Count == 3);
            ((DataSource)tree.SelectedItem!).File.Should().Be(_second);
            tree.SelectedItem = first;
            await WaitUntil(() => Rows(window).ItemCount == 3);
            Rows(window).ItemsSource.Should().BeSameAs(firstLines);
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked.Should().BeFalse();
            await Copy(window, 2, "appended while inactive");
            Command(window, "Close all sources").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            tree.ItemCount.Should().Be(0);
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 3);
            Rows(window).ItemsSource.Should().NotBeSameAs(firstLines);
            ((DataSource)tree.SelectedItem!).Id.Should().NotBe(first.Id);
            await Copy(window, 2, "appended while inactive");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ReturningToAnInactiveRewrittenSourceDoesNotPublishItsOldCachedText()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var first = (DataSource)tree.SelectedItem!;
            var firstLines = Rows(window).ItemsSource;
            await Copy(window, 0, "first");
            await window.OpenFileAsync(_second);
            await WaitUntil(() => Rows(window).ItemCount == 1);
            var timestamp = File.GetLastWriteTimeUtc(_first);
            var reset = false;
            ((global::Avalonia.Collections.AvaloniaList<int>)firstLines!).CollectionChanged += (_, args) =>
                reset |= args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset ||
                    args.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove;
            File.WriteAllText(_first, "other\n  original spaces  ");
            File.SetLastWriteTimeUtc(_first, timestamp);
            await WaitUntil(() => reset);
            tree.SelectedItem = first;
            await WaitUntil(() => Rows(window).ItemCount == 2);
            Rows(window).ItemsSource.Should().BeSameAs(firstLines);
            Rows(window).Selection.SelectedIndexes.Should().BeEmpty();
            await Copy(window, 0, "other");
            first.FollowTail.Should().BeFalse();
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task AnInactiveDocumentIsReplacedWhenItsEncodingChoiceChanges()
    {
        File.WriteAllText(_first, "café", new System.Text.UTF8Encoding(false));
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 1);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var first = (DataSource)tree.SelectedItem!;
            var firstLines = Rows(window).ItemsSource;
            await Copy(window, 0, "café");
            await window.OpenFileAsync(_second);
            await WaitUntil(() => Rows(window).ItemCount == 1 && window.Title!.Contains("second.log"));
            var selector = window.GetVisualDescendants().OfType<ComboBox>()
                .Single(combo => AutomationProperties.GetName(combo) == "Text encoding");
            selector.SelectedItem = selector.Items.OfType<ComboBoxItem>().Single(item => Equals(item.Content, "us-ascii"));
            await WaitUntil(() => Command(window, "Open folder").IsEnabled);
            tree.SelectedItem = first;
            await WaitUntil(() => window.Title!.Contains("first.log") && Rows(window).ItemCount == 1);
            Rows(window).ItemsSource.Should().NotBeSameAs(firstLines);
            await Copy(window, 0, "caf??");
            ((DataSource)tree.SelectedItem!).Id.Should().Be(first.Id);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ChangingAConfiguredFilePathCannotReuseThePreviousFilesDocument()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var first = (DataSource)tree.SelectedItem!;
            var previousLines = Rows(window).ItemsSource;
            await Copy(window, 0, "first");
            await window.OpenFileAsync(_second);
            var changedPath = Path.Combine(_folder, "changed.log");
            File.WriteAllText(changedPath, "changed target");
            first.File = changedPath;
            tree.SelectedItem = first;
            await WaitUntil(() => Rows(window).ItemCount == 1 && window.Title!.Contains("changed.log"));
            ((DataSource)tree.SelectedItem!).Id.Should().Be(first.Id);
            Rows(window).ItemsSource.Should().NotBeSameAs(previousLines);
            await Copy(window, 0, "changed target");
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
    public async Task NewlyMissingPathRemainsConfiguredAndRetryPreservesItsIdentity()
    {
        var path = Path.Combine(_folder, "new-missing.log");
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var active = (DataSource)tree.SelectedItem!;
            Rows(window).Selection.Select(1);
            await window.OpenFileAsync(path);
            tree.ItemCount.Should().Be(2);
            var missing = tree.Items.OfType<DataSource>().Single(source => source.File == path);
            missing.Id.Should().NotBe(DataSourceId.Empty);
            ((DataSource)tree.SelectedItem!).Id.Should().Be(active.Id);
            Rows(window).Selection.SelectedIndexes.Should().Equal(1);
            await Copy(window, 1, "  original spaces  ");
            await window.OpenFileAsync(path);
            tree.ItemCount.Should().Be(2);
            tree.Items.OfType<DataSource>().Single(source => source.File == path).Id.Should().Be(missing.Id);
            File.WriteAllText(path, "returned");
            await window.OpenFileAsync(path);
            await WaitUntil(() => Rows(window).ItemCount == 1);
            ((DataSource)tree.SelectedItem!).Id.Should().Be(missing.Id);
            tree.ItemCount.Should().Be(2);
            await Copy(window, 0, "returned");
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
            tree.ItemCount.Should().Be(4);
            tree.Items.OfType<DataSource>().Select(source => source.File).Should().Equal(missing, _first, otherMissing, _second);
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
                AutomationProperties.GetName(container).Should().Be(group.DisplayName);
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
    [AvaloniaTest]
    public async Task HandoffRequestsOpenInOrderAndBringToFrontPreservesTheActiveSelection()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            _ = window.AcceptHandoff(new[] { _first }, CancellationToken.None);
            await window.AcceptHandoff(new[] { _second }, CancellationToken.None);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            await WaitUntil(() => tree.ItemCount == 2 && ((DataSource?)tree.SelectedItem)?.File == _second &&
                Rows(window).ItemCount == 1 && Command(window, "Open folder").IsEnabled);
            await Copy(window, 0, "second");
            var selected = (DataSource)tree.SelectedItem!;
            window.WindowState = WindowState.Minimized;
            await window.AcceptHandoff(Array.Empty<string>(), CancellationToken.None);
            window.WindowState.Should().Be(WindowState.Normal);
            ((DataSource)tree.SelectedItem!).Id.Should().Be(selected.Id);
            Rows(window).Selection.SelectedIndexes.Should().Equal(0);
            window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked.Should().BeFalse();
            await Copy(window, 0, "second");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task HiddenWindowHandoffDoesNotReplayStartupArguments()
    {
        var window = new MainWindow(_first);
        window.Show();
        try
        {
            await WaitUntil(() => Rows(window).ItemCount == 2);
            await window.OpenFileAsync(_second);
            await WaitUntil(() => Rows(window).ItemCount == 1);
            await Copy(window, 0, "second");
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var selected = (DataSource)tree.SelectedItem!;
            window.Hide();
            await window.AcceptHandoff(Array.Empty<string>(), CancellationToken.None);
            await WaitUntil(() => Command(window, "Open folder").IsEnabled);
            window.IsVisible.Should().BeTrue();
            ((DataSource)tree.SelectedItem!).Id.Should().Be(selected.Id);
            Rows(window).Selection.SelectedIndexes.Should().Equal(0);
            await Copy(window, 0, "second");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task CancelledAndClosedHandoffsDoNotModifyTheWorkspace()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => window.AcceptHandoff(new[] { _second }, cancelled.Token));
            ((SourcesViewModel)window.DataContext!).All.Should().ContainSingle().Which.File.Should().Be(_first);
            await Copy(window, 0, "first");
        }
        finally { window.Close(); }
        Assert.Throws<OperationCanceledException>(() => window.AcceptHandoff(new[] { _second }, CancellationToken.None));
    }

    [AvaloniaTest]
    public async Task CloseAllCancelsQueuedHandoffsWithoutResurrectingSources()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            var pending = window.AcceptHandoff(new[] { _first }, CancellationToken.None);
            _ = window.AcceptHandoff(new[] { _second }, CancellationToken.None);
            Command(window, "Close all sources").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await pending;
            ((SourcesViewModel)window.DataContext!).All.Should().BeEmpty();
            Rows(window).ItemCount.Should().Be(0);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task RemovingAGroupMemberPromotesTheRemainingFileWithoutClosingEitherLog()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var first = (DataSource)tree.SelectedItem!;
            await window.OpenFileAsync(_second);
            var second = (DataSource)tree.SelectedItem!;
            Command(window, "New group").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var group = (DataSource)tree.SelectedItem!;
            await AddThroughMenu(tree, first, group);
            await AddThroughMenu(tree, second, group);
            await WaitUntil(() => Rows(window).ItemCount == 1);
            var parent = (TreeViewItem)tree.ContainerFromItem(group)!;
            await WaitUntil(() => parent.ContainerFromItem(second) is TreeViewItem item && item.ContextMenu != null);
            var container = (TreeViewItem)parent.ContainerFromItem(second)!;
            var menu = container.ContextMenu!;
            container.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = InputElement.ContextRequestedEvent });
            try
            {
                await WaitUntil(() => menu.IsOpen);
                menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Remove from group"))
                    .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            }
            finally { menu.Close(); }
            await WaitUntil(() => tree.ItemCount == 2 && Rows(window).ItemCount == 1 && Command(window, "Open folder").IsEnabled);
            tree.Items.OfType<DataSource>().Select(source => source.Id).Should().Equal(first.Id, second.Id);
            first.ParentId.Should().Be(DataSourceId.Empty);
            second.ParentId.Should().Be(DataSourceId.Empty);
            ((SourcesViewModel)window.DataContext!).Contains(group.Id).Should().BeFalse();
            await Copy(window, 0, "second");
            tree.SelectedItem = first;
            await WaitUntil(() => Rows(window).ItemCount == 2);
            await Copy(window, 1, "  original spaces  ");
            File.ReadAllText(_first).Should().Be("first\n  original spaces  ");
            File.ReadAllText(_second).Should().Be("second");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task SourceMenuReorderingPreservesTheActiveDocumentAndSelection()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFilesAsync(new[] { _first, _second });
            await WaitUntil(() => Rows(window).ItemCount == 1);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var selected = (DataSource)tree.SelectedItem!;
            var initial = tree.Items.OfType<DataSource>().Select(source => source.Id).ToArray();
            await Copy(window, 0, "second");
            foreach (var up in new[] { true, false })
            {
                await WaitUntil(() => tree.ContainerFromItem(selected) is TreeViewItem item && item.ContextMenu != null);
                var container = (TreeViewItem)tree.ContainerFromItem(selected)!;
                var menu = container.ContextMenu!;
                container.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = InputElement.ContextRequestedEvent });
                try
                {
                    await WaitUntil(() => menu.IsOpen);
                    var move = menu.Items.OfType<MenuItem>().SingleOrDefault(item => Equals(item.Header, up ? "Move up" : "Move down"));
                    move.Should().NotBeNull("because source order must be editable without reopening the log");
                    move!.IsEnabled.Should().BeTrue();
                    move.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                }
                finally { menu.Close(); }
                tree.Items.OfType<DataSource>().Select(source => source.Id).Should().Equal(up ? initial.Reverse() : initial);
                ((DataSource)tree.SelectedItem!).Id.Should().Be(selected.Id);
                Rows(window).Selection.SelectedIndexes.Should().Equal(0);
                window.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked.Should().BeFalse();
                await Copy(window, 0, "second");
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task SourceMenuMovesAnExistingMemberToAnotherGroupAndPromotesItsOldSibling()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var first = (DataSource)tree.SelectedItem!;
            await window.OpenFileAsync(_second);
            var second = (DataSource)tree.SelectedItem!;
            Command(window, "New group").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var previous = (DataSource)tree.SelectedItem!;
            await AddThroughMenu(tree, first, previous);
            await AddThroughMenu(tree, second, previous);
            Command(window, "New group").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var destination = (DataSource)tree.SelectedItem!;
            var parent = (TreeViewItem)tree.ContainerFromItem(previous)!;
            await WaitUntil(() => parent.ContainerFromItem(second) is TreeViewItem item && item.ContextMenu != null);
            var container = (TreeViewItem)parent.ContainerFromItem(second)!;
            var menu = container.ContextMenu!;
            container.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = InputElement.ContextRequestedEvent });
            try
            {
                await WaitUntil(() => menu.IsOpen);
                var add = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Header, "Add to group"));
                add.IsEnabled.Should().BeTrue();
                add.Items.OfType<MenuItem>().Single(item => Equals(item.Tag, destination.Id))
                    .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            }
            finally { menu.Close(); }
            await WaitUntil(() => second.ParentId == destination.Id && Rows(window).ItemCount == 1 && Command(window, "Open folder").IsEnabled);
            tree.Items.OfType<DataSource>().Should().Equal(first, destination);
            first.ParentId.Should().Be(DataSourceId.Empty);
            ((SourcesViewModel)window.DataContext!).Contains(previous.Id).Should().BeFalse();
            ((DataSource)tree.SelectedItem!).Id.Should().Be(second.Id);
            await Copy(window, 0, "second");
            File.ReadAllText(_first).Should().Be("first\n  original spaces  ");
            File.ReadAllText(_second).Should().Be("second");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task GroupedFolderRetainsOneIdentityAndItsLiveWatcherWhenReactivated()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_folder);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var folder = (DataSource)tree.SelectedItem!;
            Command(window, "New group").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var group = (DataSource)tree.SelectedItem!;
            await AddThroughMenu(tree, folder, group);
            await WaitUntil(() => Command(window, "Open folder").IsEnabled);
            tree.ItemCount.Should().Be(1, "because a group member must not also appear as a root");
            var workspace = (SourcesViewModel)window.DataContext!;
            workspace.All.Select(source => source.Id).Should().Equal(group.Id, folder.Id);
            folder.ParentId.Should().Be(group.Id);
            var parent = (TreeViewItem)tree.ContainerFromItem(group)!;
            await WaitUntil(() => parent.ContainerFromItem(folder) is TreeViewItem item && item.ItemCount == 2);
            var discovered = Path.Combine(_folder, "added.log");
            File.WriteAllText(discovered, "discovered");
            await WaitUntil(() => parent.ContainerFromItem(folder) is TreeViewItem item && item.ItemCount == 3);
            var member = (TreeViewItem)parent.ContainerFromItem(folder)!;
            tree.SelectedItem = member.Items.OfType<DataSource>().Single(source => source.File == discovered);
            await WaitUntil(() => Rows(window).ItemCount == 1);
            await Copy(window, 0, "discovered");
            tree.SelectedItem = folder;
            await WaitUntil(() => Command(window, "Open folder").IsEnabled && Rows(window).ItemCount == 0);
            workspace.All.Select(source => source.Id).Should().Equal(group.Id, folder.Id);
            tree.ItemCount.Should().Be(1);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task GroupWithFileCreatesAnOrderedGroupAtTheDestinationAndKeepsOriginalFiles()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFilesAsync(new[] { _first, _second });
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var first = tree.Items.OfType<DataSource>().First();
            var second = (DataSource)tree.SelectedItem!;
            await WaitUntil(() => tree.ContainerFromItem(second) is TreeViewItem item && item.ContextMenu != null);
            var container = (TreeViewItem)tree.ContainerFromItem(second)!;
            var menu = container.ContextMenu!;
            container.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = InputElement.ContextRequestedEvent });
            try
            {
                await WaitUntil(() => menu.IsOpen);
                var pair = menu.Items.OfType<MenuItem>().SingleOrDefault(item => Equals(item.Header, "Group with"));
                pair.Should().NotBeNull("because dropping a file onto another file creates a new ordered group");
                pair!.Items.OfType<MenuItem>().Single(item => Equals(item.Tag, first.Id))
                    .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            }
            finally { menu.Close(); }
            var workspace = (SourcesViewModel)window.DataContext!;
            await WaitUntil(() => tree.ItemCount == 1 && tree.SelectedItem is DataSource selected && SourcesViewModel.IsGroup(selected));
            var group = (DataSource)tree.SelectedItem!;
            group.Id.Should().NotBe(first.Id).And.NotBe(second.Id);
            workspace.GetChildren(group.Id).Should().Equal(second, first);
            first.ParentId.Should().Be(group.Id);
            second.ParentId.Should().Be(group.Id);
            tree.SelectedItem = second;
            await WaitUntil(() => Rows(window).ItemCount == 1);
            await Copy(window, 0, "second");
            File.ReadAllText(_first).Should().Be("first\n  original spaces  ");
            File.ReadAllText(_second).Should().Be("second");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task PointerDragOntoAFileCreatesAnOrderedGroup()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFilesAsync(new[] { _first, _second });
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var sources = tree.Items.OfType<DataSource>().ToArray();
            await WaitUntil(() => tree.ContainerFromItem(sources[0]) is TreeViewItem && tree.ContainerFromItem(sources[1]) is TreeViewItem);
            var start = TreePoint(window, tree, sources[1], .5);
            var end = TreePoint(window, tree, sources[0], .5);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(end, RawInputModifiers.LeftMouseButton);
            window.MouseUp(end, MouseButton.Left);
            await WaitUntil(() => tree.ItemCount == 1 && tree.SelectedItem is DataSource group && SourcesViewModel.IsGroup(group));
            var selected = (DataSource)tree.SelectedItem!;
            ((SourcesViewModel)window.DataContext!).GetChildren(selected.Id).Should().Equal(sources[1], sources[0]);
            tree.SelectedItem = sources[1];
            await WaitUntil(() => Rows(window).ItemCount == 1);
            await Copy(window, 0, "second");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task PointerDragAtTheRowEdgeReordersWithoutGrouping()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFilesAsync(new[] { _first, _second });
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var sources = tree.Items.OfType<DataSource>().ToArray();
            await WaitUntil(() => tree.ContainerFromItem(sources[0]) is TreeViewItem && tree.ContainerFromItem(sources[1]) is TreeViewItem);
            var start = TreePoint(window, tree, sources[0], .5);
            var end = TreePoint(window, tree, sources[1], .95);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(end, RawInputModifiers.LeftMouseButton);
            window.MouseUp(end, MouseButton.Left);
            await WaitUntil(() => tree.Items.OfType<DataSource>().First().Id == sources[1].Id && Rows(window).ItemCount == 2);
            tree.Items.OfType<DataSource>().Should().Equal(sources[1], sources[0]);
            ((SourcesViewModel)window.DataContext!).All.Should().OnlyContain(source => source.ParentId == DataSourceId.Empty);
            await Copy(window, 1, "  original spaces  ");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task EscapeCancelsCapturedTreeDragWhenKeyboardFocusIsInTheLogList()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFilesAsync(new[] { _first, _second });
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var sources = tree.Items.OfType<DataSource>().ToArray();
            await WaitUntil(() => tree.ContainerFromItem(sources[0]) is TreeViewItem && tree.ContainerFromItem(sources[1]) is TreeViewItem);
            var start = TreePoint(window, tree, sources[1], .5);
            var end = TreePoint(window, tree, sources[0], .5);
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(end, RawInputModifiers.LeftMouseButton);
            Rows(window).Focus().Should().BeTrue();
            Rows(window).IsKeyboardFocusWithin.Should().BeTrue();
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.MouseUp(end, MouseButton.Left);
            tree.Cursor.Should().BeNull();
            tree.Items.OfType<DataSource>().Should().Equal(sources);
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task EscapeOutsideReleaseAndCloseAllCancelCapturedTreeDrags()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFilesAsync(new[] { _first, _second });
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var sources = tree.Items.OfType<DataSource>().ToArray();
            foreach (var cancel in new[] { "escape", "outside", "close" })
            {
                await WaitUntil(() => Command(window, "Open folder").IsEnabled && tree.ContainerFromItem(sources[0]) is TreeViewItem && tree.ContainerFromItem(sources[1]) is TreeViewItem);
                var start = TreePoint(window, tree, sources[1], .5);
                var end = TreePoint(window, tree, sources[0], .5);
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(end, RawInputModifiers.LeftMouseButton);
                if (cancel == "escape") tree.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
                else if (cancel == "outside") end = new Point(window.Width - 30, window.Height - 30);
                else Command(window, "Close all sources").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.MouseUp(end, MouseButton.Left);
                tree.Cursor.Should().BeNull();
                if (cancel == "close") ((SourcesViewModel)window.DataContext!).All.Should().BeEmpty();
                else tree.Items.OfType<DataSource>().Should().Equal(sources);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task PointerDragDoesNotMoveAConfiguredFolderOrItsDiscoveredChildren()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            await window.OpenFileAsync(_folder);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var sources = tree.Items.OfType<DataSource>().ToArray();
            var folder = sources[1];
            await WaitUntil(() => tree.ContainerFromItem(folder) is TreeViewItem item && item.ItemCount == 2);
            var folderItem = (TreeViewItem)tree.ContainerFromItem(folder)!;
            var child = folderItem.Items.OfType<DataSource>().First();
            await WaitUntil(() => folderItem.ContainerFromItem(child) is TreeViewItem);
            var childItem = (TreeViewItem)folderItem.ContainerFromItem(child)!;
            var childRow = childItem.GetVisualDescendants().OfType<SourcesView>().First();
            var childPoint = childRow.TranslatePoint(new Point(30, childRow.Bounds.Height / 2), window)!.Value;
            var folderPoint = TreePoint(window, tree, folder, .5);
            var end = TreePoint(window, tree, sources[0], .5);
            foreach (var start in new[] { folderPoint, childPoint })
            {
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(end, RawInputModifiers.LeftMouseButton);
                window.MouseUp(end, MouseButton.Left);
                tree.Items.OfType<DataSource>().Should().Equal(sources);
                ((SourcesViewModel)window.DataContext!).All.Should().OnlyContain(source => source.ParentId == DataSourceId.Empty);
            }
            Directory.Exists(_folder).Should().BeTrue();
            File.ReadAllText(_first).Should().Be("first\n  original spaces  ");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task CapturedTreeDragScrollsViewportEdgesAndStopsAfterEscape()
    {
        var window = new MainWindow { Height = 400 };
        window.Show();
        try
        {
            await window.OpenFileAsync(_first);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var source = (DataSource)tree.SelectedItem!;
            var workspace = (SourcesViewModel)window.DataContext!;
            workspace.Roots.AddRange(Enumerable.Range(0, 60).Select(index => new DataSource(Path.Combine(_folder, $"missing-{index}.log"))
                { Id = DataSourceId.CreateNew() }));
            var order = workspace.All.Select(item => item.Id).ToArray();
            await WaitUntil(() => tree.GetVisualDescendants().OfType<ScrollViewer>().Any(scroll => scroll.Extent.Height > scroll.Viewport.Height));
            var scroll = tree.GetVisualDescendants().OfType<ScrollViewer>().First();
            var viewport = scroll.GetVisualDescendants().OfType<global::Avalonia.Controls.Presenters.ScrollContentPresenter>().First();
            var start = TreePoint(window, tree, source, .5);
            var bottom = viewport.TranslatePoint(new Point(30, viewport.Bounds.Height - 2), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            window.MouseMove(bottom, RawInputModifiers.LeftMouseButton);
            await WaitUntil(() => scroll.Offset.Y > scroll.Viewport.Height);
            var down = scroll.Offset.Y;
            var top = viewport.TranslatePoint(new Point(30, 2), window)!.Value;
            window.MouseMove(top, RawInputModifiers.LeftMouseButton);
            await WaitUntil(() => scroll.Offset.Y < down);
            tree.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            window.MouseUp(top, MouseButton.Left);
            await WaitUntil(() => Command(window, "Open folder").IsEnabled);
            var stopped = scroll.Offset;
            await Task.Delay(300);
            Dispatcher.UIThread.RunJobs();
            scroll.Offset.Should().Be(stopped);
            workspace.All.Select(item => item.Id).Should().Equal(order);
            tree.Cursor.Should().BeNull();
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task CapturedTreeDragExpandsACollapsedGroupButCancelledHoverDoesNot()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFilesAsync(new[] { _first, _second });
            var workspace = (SourcesViewModel)window.DataContext!;
            var source = workspace.Roots[0];
            var member = workspace.Roots[1];
            var group = workspace.AddGroup();
            workspace.AddToGroup(member.Id, group.Id);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            await WaitUntil(() => tree.ContainerFromItem(group) is TreeViewItem);
            var item = (TreeViewItem)tree.ContainerFromItem(group)!;
            IPointer? pointer = null;
            tree.AddHandler(InputElement.PointerMovedEvent, (_, args) => pointer = args.Pointer, RoutingStrategies.Tunnel, handledEventsToo: true);
            foreach (var cancel in new[] { "escape", "capture", "none" })
            {
                item.IsExpanded = false;
                window.UpdateLayout();
                var start = TreePoint(window, tree, source, .5);
                var end = TreePoint(window, tree, group, .5);
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(end, RawInputModifiers.LeftMouseButton);
                if (cancel != "none")
                {
                    if (cancel == "escape") tree.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
                    else
                    {
                        pointer.Should().NotBeNull();
                        pointer!.Captured.Should().BeSameAs(tree);
                        pointer.Capture(null);
                    }
                    window.MouseUp(end, MouseButton.Left);
                    await Task.Delay(700);
                    Dispatcher.UIThread.RunJobs();
                    item.IsExpanded.Should().BeFalse();
                    workspace.GetChildren(group.Id).Should().Equal(member);
                }
                else
                {
                    await WaitUntil(() => item.IsExpanded);
                    window.MouseUp(end, MouseButton.Left);
                    await WaitUntil(() => source.ParentId == group.Id);
                    workspace.GetChildren(group.Id).Should().Equal(member, source);
                    tree.Items.OfType<DataSource>().Should().Equal(group);
                }
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task InactiveFileAvailabilityRecoversWithoutSwitchingTheActiveDocument()
    {
        var window = new MainWindow();
        window.Show();
        try
        {
            await window.OpenFilesAsync(new[] { _first, _second });
            await WaitUntil(() => Rows(window).ItemCount == 1);
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var sources = tree.Items.OfType<DataSource>().ToArray();
            await Copy(window, 0, "second");
            File.Delete(_first);
            await WaitUntil(() => SourceStatus(tree, sources[0]) == "File is unavailable.");
            ((DataSource)tree.SelectedItem!).Id.Should().Be(sources[1].Id);
            await Copy(window, 0, "second");
            File.WriteAllText(_first, "");
            await WaitUntil(() => SourceStatus(tree, sources[0]) == "No log lines.");
            File.AppendAllText(_first, "returned");
            await WaitUntil(() => string.IsNullOrEmpty(SourceStatus(tree, sources[0])));
            tree.Items.OfType<DataSource>().Select(source => source.Id).Should().Equal(sources.Select(source => source.Id));
            ((DataSource)tree.SelectedItem!).Id.Should().Be(sources[1].Id);
            Rows(window).Selection.SelectedIndexes.Should().Equal(0);
            await Copy(window, 0, "second");
        }
        finally { window.Close(); }
    }

    [AvaloniaTest]
    public async Task ActiveFileRecoversAfterExclusiveLockWithoutReopeningOrEnablingFollow()
    {
        var window = new MainWindow();
        window.Show();
        FileStream? exclusive = null;
        try
        {
            await window.OpenFileAsync(_first);
            await WaitUntil(() => Rows(window).ItemCount == 2);
            await Copy(window, 1, "  original spaces  ");
            var tree = window.GetVisualDescendants().OfType<TreeView>().Single();
            var source = (DataSource)tree.SelectedItem!;
            var follow = window.GetVisualDescendants().OfType<CheckBox>().Single();
            follow.IsChecked = false;
            await WaitUntil(() =>
            {
                try
                {
                    exclusive = File.Open(_first, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    return true;
                }
                catch (IOException) { return false; }
            });
            await WaitUntil(() => Rows(window).ItemCount == 0 && !string.IsNullOrEmpty(SourceStatus(tree, source)) &&
                SourceStatus(tree, source) != "Loading…" && SourceStatus(tree, source) != "No log lines.");
            ((DataSource)tree.SelectedItem!).Id.Should().Be(source.Id);
            follow.IsChecked.Should().BeFalse();
            exclusive!.Dispose();
            exclusive = null;
            await WaitUntil(() => Rows(window).ItemCount == 2 && string.IsNullOrEmpty(SourceStatus(tree, source)));
            ((DataSource)tree.SelectedItem!).Id.Should().Be(source.Id);
            follow.IsChecked.Should().BeFalse();
            await Copy(window, 1, "  original spaces  ");
            File.ReadAllText(_first).Should().Be("first\n  original spaces  ");
        }
        finally { exclusive?.Dispose(); window.Close(); }
    }

    private static Point TreePoint(MainWindow window, TreeView tree, DataSource source, double height)
    {
        var item = (TreeViewItem)tree.ContainerFromItem(source)!;
        var row = item.GetVisualDescendants().OfType<SourcesView>().First();
        return row.TranslatePoint(new Point(Math.Min(40, row.Bounds.Width / 2), row.Bounds.Height * height), window)!.Value;
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
        Rows(window).Selection.Clear();
        Rows(window).Selection.Select(index);
        await CopySelected(window, expected);
    }

    private static async Task CopySelected(MainWindow window, string expected)
    {
        await window.Clipboard!.SetTextAsync("pending-" + Guid.NewGuid().ToString("N"));
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
