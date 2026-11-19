using FluentAssertions;
using NUnit.Framework;
using Tailviewer.Api;
using Tailviewer.Avalonia.ViewModels;
using Tailviewer.Settings;

namespace Tailviewer.Avalonia.Tests;

[TestFixture]
public sealed class QuickNavigationTest
{
    [Test]
    public void MatchingPreservesOriginalCaseAndSubstringSegments()
    {
        var workspace = new SourcesViewModel();
        workspace.Roots.Add(Source("Tailviewer.log"));
        var match = workspace.FindNavigationMatches("AIL").Single();
        match.Prefix.Should().Be("T");
        match.Midfix.Should().Be("ail");
        match.Postfix.Should().Be("viewer.log");
        match.ToString().Should().Be("Tailviewer.log");
    }

    [Test]
    public void GroupingDoesNotChangeLegacySourceInsertionOrder()
    {
        var workspace = new SourcesViewModel();
        var first = Source("Tail-first.log");
        var second = Source("Tail-second.log");
        workspace.Roots.AddRange(new[] { first, second });
        var group = workspace.AddGroup();
        group.DisplayName = "Tail group";
        workspace.AddToGroup(second.Id, group.Id);
        workspace.AddToGroup(first.Id, group.Id);
        workspace.FindNavigationMatches("TAIL").Select(match => match.DataSource.Id).Should().Equal(first.Id, second.Id, group.Id);
    }

    [Test]
    public void EmptyAndUnmatchedSearchesReturnNoCandidates()
    {
        var workspace = new SourcesViewModel();
        workspace.Roots.Add(Source("one.log"));
        workspace.FindNavigationMatches(null).Should().BeEmpty();
        workspace.FindNavigationMatches("").Should().BeEmpty();
        workspace.FindNavigationMatches("unmatched").Should().BeEmpty();
    }

    [Test]
    public void FoldersAndDuplicateNamedGroupsAreSelectedByIdentity()
    {
        var workspace = new SourcesViewModel();
        var folder = new DataSource { Id = DataSourceId.CreateNew(), LogFileFolderPath = "Folder-Logs" };
        workspace.Roots.Add(folder);
        var first = workspace.AddGroup();
        var second = workspace.AddGroup();
        workspace.FindNavigationMatches("folder").Single().DataSource.Id.Should().Be(folder.Id);
        workspace.FindNavigationMatches("MERGED").Select(match => match.DataSource.Id).Should().Equal(first.Id, second.Id);
        workspace.Remove(first.Id);
        workspace.FindNavigationMatches("merged").Single().DataSource.Id.Should().Be(second.Id);
    }

    private static DataSource Source(string path) => new(path) { Id = DataSourceId.CreateNew() };
}
