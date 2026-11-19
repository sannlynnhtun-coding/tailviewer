using System.Xml;
using FluentAssertions;
using NUnit.Framework;
using Tailviewer.Api;
using Tailviewer.Avalonia.ViewModels;
using Tailviewer.Settings;

namespace Tailviewer.Avalonia.Tests;

[TestFixture]
public sealed class SourceGroupsTest
{
    [Test]
    public void DuplicateConfiguredIdsAreRejectedWithoutAddingAnotherGroup()
    {
        var workspace = new SourcesViewModel();
        var first = File("missing.log");
        workspace.Roots.AddRange(new[] { first, first.Clone() });
        Assert.Throws<XmlException>(() => workspace.AddGroup());
        workspace.Roots.Count.Should().Be(2);
    }

    [Test]
    public void DuplicateMembershipIsIdempotentAndPreservesOrderedIdentities()
    {
        var workspace = new SourcesViewModel();
        var first = File("missing-first.log");
        var second = File("missing-second.log");
        workspace.Roots.AddRange(new[] { first, second });
        var group = workspace.AddGroup();
        workspace.AddToGroup(second.Id, group.Id);
        workspace.AddToGroup(first.Id, group.Id);
        workspace.AddToGroup(second.Id, group.Id);
        workspace.GetChildren(group.Id).Select(source => source.Id).Should().Equal(second.Id, first.Id);
        workspace.All.Select(source => source.Id).Distinct().Count().Should().Be(3);
        workspace.Roots.Should().Equal(group);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SelfAndNestedCyclesLeaveTheGraphUnchanged(bool nested)
    {
        var workspace = new SourcesViewModel();
        var first = workspace.AddGroup();
        var second = nested ? workspace.AddGroup() : first;
        if (nested) workspace.AddToGroup(second.Id, first.Id);
        var before = workspace.All.Select(source => (source.Id, source.ParentId)).ToArray();
        Assert.Throws<XmlException>(() => workspace.AddToGroup(first.Id, second.Id));
        workspace.All.Select(source => (source.Id, source.ParentId)).Should().Equal(before);
    }

    [Test]
    public void ClosingAGroupPromotesItsMembersAtItsOriginalPosition()
    {
        var workspace = new SourcesViewModel();
        var group = workspace.AddGroup();
        var first = File("unavailable-a.log");
        var second = File("unavailable-b.log");
        workspace.Roots.AddRange(new[] { first, second });
        workspace.AddToGroup(first.Id, group.Id);
        workspace.AddToGroup(second.Id, group.Id);
        workspace.Remove(group.Id);
        workspace.Roots.Should().Equal(first, second);
        workspace.All.Should().OnlyContain(source => source.ParentId == DataSourceId.Empty);
        first.File.Should().Be("unavailable-a.log");
    }

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public void ClosingNestedGroupPreservesPromotedMemberExclusions(bool excludedParent)
    {
        var workspace = new SourcesViewModel();
        var parent = workspace.AddGroup();
        var nested = workspace.AddGroup();
        var file = File("missing.log");
        workspace.Roots.Add(file);
        workspace.AddToGroup(nested.Id, parent.Id);
        workspace.AddToGroup(file.Id, nested.Id);
        if (excludedParent) parent.ExcludedDataSources.Add(nested.Id);
        else nested.ExcludedDataSources.Add(file.Id);
        workspace.Remove(nested.Id);
        workspace.GetChildren(parent.Id).Should().Equal(file);
        file.ParentId.Should().Be(parent.Id);
        parent.ExcludedDataSources.Should().Contain(file.Id).And.NotContain(nested.Id);
    }

    [Test]
    public void UngroupClearsTheParentsExclusionAndKeepsSourceIdentity()
    {
        var workspace = new SourcesViewModel();
        var group = workspace.AddGroup();
        var file = File("missing.log");
        workspace.Roots.Add(file);
        workspace.AddToGroup(file.Id, group.Id);
        group.ExcludedDataSources.Add(file.Id);
        workspace.Ungroup(file.Id);
        group.ExcludedDataSources.Should().BeEmpty();
        file.ParentId.Should().Be(DataSourceId.Empty);
        workspace.Roots.Should().Equal(group, file);
    }

    [Test]
    public void ReparentingRequiresRemovalFromTheCurrentGroup()
    {
        var workspace = new SourcesViewModel();
        var first = workspace.AddGroup();
        var second = workspace.AddGroup();
        var file = File("missing.log");
        workspace.Roots.Add(file);
        workspace.AddToGroup(file.Id, first.Id);
        Assert.Throws<ArgumentException>(() => workspace.AddToGroup(file.Id, second.Id));
        workspace.GetChildren(first.Id).Should().Equal(file);
        workspace.GetChildren(second.Id).Should().BeEmpty();
    }

    [Test]
    public void ExclusionIsIdempotentAndCannotTargetAnUngroupedOrClosedSource()
    {
        var workspace = new SourcesViewModel();
        var group = workspace.AddGroup();
        var file = File("missing.log");
        workspace.Roots.Add(file);
        Assert.Throws<ArgumentException>(() => workspace.SetExcluded(file.Id, true));
        group.ExcludedDataSources.Should().BeEmpty();
        workspace.AddToGroup(file.Id, group.Id);
        workspace.SetExcluded(file.Id, true);
        workspace.SetExcluded(file.Id, true);
        group.Clone().ExcludedDataSources.Should().Equal(file.Id);
        workspace.GetChildren(group.Id).Should().Equal(file);
        workspace.Ungroup(file.Id);
        group.ExcludedDataSources.Should().BeEmpty();
        Assert.Throws<ArgumentException>(() => workspace.SetExcluded(file.Id, false));
        workspace.Remove(file.Id);
        Assert.Throws<ArgumentException>(() => workspace.SetExcluded(file.Id, true));
        group.ExcludedDataSources.Should().BeEmpty();
    }
    private static DataSource File(string path) => new(path) { Id = DataSourceId.CreateNew() };
}
