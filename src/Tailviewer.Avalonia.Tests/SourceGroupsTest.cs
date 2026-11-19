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
    [Test]
    public void RenamePreservesGroupMembershipAndRejectsFilesFoldersAndClosedGroups()
    {
        var workspace = new SourcesViewModel();
        var parent = workspace.AddGroup();
        var group = workspace.AddGroup();
        var file = File("missing.log");
        var folder = new DataSource { Id = DataSourceId.CreateNew(), LogFileFolderPath = "missing-folder" };
        workspace.Roots.AddRange(new[] { file, folder });
        workspace.AddToGroup(group.Id, parent.Id);
        workspace.AddToGroup(file.Id, group.Id);
        workspace.SetExcluded(file.Id, true);
        workspace.RenameGroup(group.Id, "  မြန်မာ 😀  ");
        group.DisplayName.Should().Be("  မြန်မာ 😀  ");
        group.ParentId.Should().Be(parent.Id);
        workspace.GetChildren(group.Id).Should().Equal(file);
        group.ExcludedDataSources.Should().Equal(file.Id);
        Assert.Throws<ArgumentException>(() => workspace.RenameGroup(file.Id, "wrong"));
        Assert.Throws<ArgumentException>(() => workspace.RenameGroup(folder.Id, "wrong"));
        file.File.Should().Be("missing.log");
        folder.LogFileFolderPath.Should().Be("missing-folder");
        workspace.Remove(group.Id);
        Assert.Throws<ArgumentException>(() => workspace.RenameGroup(group.Id, "closed"));
    }
    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(2, false)]
    public void UngroupDissolvesOnlyASingleRemainingMemberAndPreservesItsRootPosition(int remaining, bool dissolve)
    {
        var workspace = new SourcesViewModel();
        var before = File("before.log");
        var after = File("after.log");
        workspace.Roots.Add(before);
        var group = workspace.AddGroup();
        workspace.Roots.Add(after);
        var members = Enumerable.Range(0, remaining + 1).Select(index => File($"missing-{index}.log")).ToArray();
        foreach (var member in members)
        {
            workspace.Roots.Add(member);
            workspace.AddToGroup(member.Id, group.Id);
            workspace.SetExcluded(member.Id, true);
        }
        workspace.Ungroup(members[0].Id);
        workspace.Contains(group.Id).Should().Be(!dissolve);
        workspace.Roots.Should().Equal(before, dissolve ? members[1] : group, after, members[0]);
        members[0].ParentId.Should().Be(DataSourceId.Empty);
        group.ExcludedDataSources.Should().NotContain(members[0].Id);
        if (dissolve)
        {
            members[1].ParentId.Should().Be(DataSourceId.Empty);
            workspace.GetChildren(group.Id).Should().BeEmpty();
        }
        else workspace.GetChildren(group.Id).Should().Equal(members.Skip(1));
        var settings = new DataSourceSettings();
        settings.AddRange(workspace.All);
        new SourceConfiguration(settings).Snapshot().Select(source => source.Id)
            .Should().BeEquivalentTo(workspace.All.Select(source => source.Id));
    }

    private static DataSource File(string path) => new(path) { Id = DataSourceId.CreateNew() };
}
