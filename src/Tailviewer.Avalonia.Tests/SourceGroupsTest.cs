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
    public void MovingToAnotherGroupClearsThePreviousExclusionAndRetainsAnEmptyOldGroup()
    {
        var workspace = new SourcesViewModel();
        var first = workspace.AddGroup();
        var second = workspace.AddGroup();
        var file = File("missing.log");
        workspace.Roots.Add(file);
        workspace.AddToGroup(file.Id, first.Id);
        workspace.SetExcluded(file.Id, true);
        workspace.AddToGroup(file.Id, second.Id);
        workspace.GetChildren(first.Id).Should().BeEmpty();
        workspace.GetChildren(second.Id).Should().Equal(file);
        first.ExcludedDataSources.Should().BeEmpty();
        file.ParentId.Should().Be(second.Id);
        workspace.Roots.Should().Equal(first, second);
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

    [TestCase(false, false, false)]
    [TestCase(false, false, true)]
    [TestCase(false, true, false)]
    [TestCase(false, true, true)]
    [TestCase(true, false, false)]
    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    [TestCase(true, true, true)]
    public void ReorderPreservesIdentityExclusionsAndNavigationInsertionOrder(bool grouped, bool forward, bool after)
    {
        var workspace = new SourcesViewModel();
        var group = grouped ? workspace.AddGroup() : null;
        var members = Enumerable.Range(0, 4).Select(index => File($"missing-{index}.log")).ToArray();
        workspace.Roots.AddRange(members);
        if (group != null)
        {
            foreach (var member in members) workspace.AddToGroup(member.Id, group.Id);
            workspace.SetExcluded(members[1].Id, true);
        }
        var source = forward ? members[0] : members[3];
        var target = forward ? members[2] : members[1];
        workspace.MoveRelative(source.Id, target.Id, after);
        var expected = forward ? (after ? new[] { 1, 2, 0, 3 } : new[] { 1, 0, 2, 3 }) :
            (after ? new[] { 0, 1, 3, 2 } : new[] { 0, 3, 1, 2 });
        var siblings = group == null ? workspace.Roots : workspace.GetChildren(group.Id);
        siblings.Should().Equal(expected.Select(index => members[index]));
        members.Should().OnlyContain(member => member.ParentId == (group == null ? DataSourceId.Empty : group.Id));
        if (group != null) group.ExcludedDataSources.Should().Equal(members[1].Id);
        workspace.GetAdjacentSource(members[0].Id, false)!.Id.Should().Be(members[1].Id);
        workspace.MoveRelative(source.Id, source.Id, after);
        siblings.Should().Equal(expected.Select(index => members[index]));
        var settings = new DataSourceSettings();
        settings.AddRange(workspace.All);
        new SourceConfiguration(settings).GetMembers(group?.Id ?? DataSourceId.Empty).Select(item => item.Id)
            .Should().Equal(expected.Select(index => members[index].Id));
    }

    [Test]
    public void ReorderingCyclesOrClosedIdentitiesDoNotMutateTheGraph()
    {
        var workspace = new SourcesViewModel();
        var group = workspace.AddGroup();
        var child = File("child.log");
        var root = File("root.log");
        workspace.Roots.AddRange(new[] { child, root });
        workspace.AddToGroup(child.Id, group.Id);
        workspace.SetExcluded(child.Id, true);
        var before = workspace.All.Select(source => (source.Id, source.ParentId)).ToArray();
        Assert.Throws<XmlException>(() => workspace.MoveRelative(group.Id, child.Id, false));
        Assert.Throws<ArgumentException>(() => workspace.MoveRelative(root.Id, DataSourceId.CreateNew(), true));
        workspace.All.Select(source => (source.Id, source.ParentId)).Should().Equal(before);
        group.ExcludedDataSources.Should().Equal(child.Id);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CrossParentArrangePromotesTheOldSiblingAndDefaultsTheMovedMemberToIncluded(bool after)
    {
        var workspace = new SourcesViewModel();
        var oldGroup = workspace.AddGroup();
        var newGroup = workspace.AddGroup();
        var moved = File("moved.log");
        var remaining = File("remaining.log");
        var target = File("target.log");
        workspace.Roots.AddRange(new[] { moved, remaining, target });
        workspace.AddToGroup(moved.Id, oldGroup.Id);
        workspace.AddToGroup(remaining.Id, oldGroup.Id);
        workspace.AddToGroup(target.Id, newGroup.Id);
        workspace.SetExcluded(moved.Id, true);
        workspace.MoveRelative(moved.Id, target.Id, after);
        workspace.Roots.Should().Equal(remaining, newGroup);
        workspace.GetChildren(newGroup.Id).Should().Equal(after ? new[] { target, moved } : new[] { moved, target });
        moved.ParentId.Should().Be(newGroup.Id);
        remaining.ParentId.Should().Be(DataSourceId.Empty);
        oldGroup.ExcludedDataSources.Should().NotContain(moved.Id);
        newGroup.ExcludedDataSources.Should().NotContain(moved.Id);
        workspace.Contains(oldGroup.Id).Should().BeFalse();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ArrangingBesideTheOldParentUsesThePromotedChildAsItsAnchor(bool after)
    {
        var workspace = new SourcesViewModel();
        var group = workspace.AddGroup();
        var moved = File("moved.log");
        var remaining = File("remaining.log");
        workspace.Roots.AddRange(new[] { moved, remaining });
        workspace.AddToGroup(moved.Id, group.Id);
        workspace.AddToGroup(remaining.Id, group.Id);
        workspace.MoveRelative(moved.Id, group.Id, after);
        workspace.Roots.Should().Equal(after ? new[] { remaining, moved } : new[] { moved, remaining });
        workspace.All.Should().OnlyContain(source => source.ParentId == DataSourceId.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AFullDestinationRejectsReparentingWithoutChangingTheOldGroup(bool arrange)
    {
        var workspace = new SourcesViewModel();
        var full = workspace.AddGroup();
        for (var index = 0; index < LogEntrySourceId.MaxSources; ++index)
        {
            var member = File($"full-{index}.log");
            workspace.Roots.Add(member);
            workspace.AddToGroup(member.Id, full.Id);
        }
        var previous = workspace.AddGroup();
        var moved = File("moved.log");
        var remaining = File("remaining.log");
        workspace.Roots.AddRange(new[] { moved, remaining });
        workspace.AddToGroup(moved.Id, previous.Id);
        workspace.AddToGroup(remaining.Id, previous.Id);
        workspace.SetExcluded(moved.Id, true);
        var before = workspace.All.Select(source => (source.Id, source.ParentId)).ToArray();
        Assert.Throws<ArgumentException>(() =>
        {
            if (arrange) workspace.MoveRelative(moved.Id, workspace.GetChildren(full.Id).First().Id, false);
            else workspace.AddToGroup(moved.Id, full.Id);
        });
        workspace.All.Select(source => (source.Id, source.ParentId)).Should().Equal(before);
        workspace.GetChildren(previous.Id).Should().Equal(moved, remaining);
        previous.ExcludedDataSources.Should().Equal(moved.Id);
        workspace.GetChildren(full.Id).Count().Should().Be(LogEntrySourceId.MaxSources);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PairGroupingUsesDestinationPositionAndLegacyMemberOrder(bool reversedRoots)
    {
        var workspace = new SourcesViewModel();
        var before = File("before.log");
        var after = File("after.log");
        var source = File("source.log");
        var target = File("target.log");
        workspace.Roots.AddRange(reversedRoots ? new[] { before, target, source, after } : new[] { before, source, target, after });
        var group = workspace.GroupWith(source.Id, target.Id);
        workspace.Roots.Should().Equal(before, group, after);
        workspace.GetChildren(group.Id).Should().Equal(source, target);
        source.ParentId.Should().Be(group.Id);
        target.ParentId.Should().Be(group.Id);
        workspace.GetAdjacentSource(reversedRoots ? target.Id : source.Id, false)!.Id.Should().Be(reversedRoots ? source.Id : target.Id);
        var settings = new DataSourceSettings();
        settings.AddRange(workspace.All);
        new SourceConfiguration(settings).GetMembers(group.Id).Select(item => item.Id).Should().Equal(source.Id, target.Id);
    }

    [Test]
    public void PairGroupingTransfersAMemberAndPromotesItsOldSibling()
    {
        var workspace = new SourcesViewModel();
        var previous = workspace.AddGroup();
        var source = File("source.log");
        var remaining = File("remaining.log");
        var target = File("target.log");
        workspace.Roots.AddRange(new[] { source, remaining, target });
        workspace.AddToGroup(source.Id, previous.Id);
        workspace.AddToGroup(remaining.Id, previous.Id);
        workspace.SetExcluded(source.Id, true);
        var group = workspace.GroupWith(source.Id, target.Id);
        workspace.Roots.Should().Equal(remaining, group);
        workspace.GetChildren(group.Id).Should().Equal(source, target);
        workspace.Contains(previous.Id).Should().BeFalse();
        remaining.ParentId.Should().Be(DataSourceId.Empty);
        previous.ExcludedDataSources.Should().NotContain(source.Id);
        group.ExcludedDataSources.Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GroupingOntoAGroupOrItsMemberReusesTheExistingGroup(bool targetMember)
    {
        var workspace = new SourcesViewModel();
        var group = workspace.AddGroup();
        var source = File("source.log");
        var target = File("target.log");
        workspace.Roots.AddRange(new[] { source, target });
        workspace.AddToGroup(target.Id, group.Id);
        workspace.GroupWith(source.Id, targetMember ? target.Id : group.Id).Should().BeSameAs(group);
        workspace.GroupWith(source.Id, target.Id).Should().BeSameAs(group);
        workspace.Roots.Should().Equal(group);
        workspace.GetChildren(group.Id).Should().Equal(target, source);
    }

    [Test]
    public void InvalidPairTargetsAreRejectedBeforeCreatingAnyGroup()
    {
        var workspace = new SourcesViewModel();
        var group = workspace.AddGroup();
        var file = File("source.log");
        var folder = new DataSource { Id = DataSourceId.CreateNew(), LogFileFolderPath = "missing-folder" };
        workspace.Roots.AddRange(new[] { file, folder });
        var before = workspace.All.Select(source => (source.Id, source.ParentId)).ToArray();
        foreach (var pair in new[] { (file.Id, file.Id), (group.Id, file.Id), (folder.Id, file.Id),
                     (file.Id, folder.Id), (file.Id, DataSourceId.CreateNew()) })
            Assert.Throws<ArgumentException>(() => workspace.GroupWith(pair.Item1, pair.Item2));
        workspace.All.Select(source => (source.Id, source.ParentId)).Should().Equal(before);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void ClosingNestedGroupChecksPromotionCapacityBeforeChangingMembership(bool root, bool overflow)
    {
        var workspace = new SourcesViewModel();
        var parent = root ? null : workspace.AddGroup();
        var nested = workspace.AddGroup();
        if (parent != null) workspace.AddToGroup(nested.Id, parent.Id);
        var members = new[] { File("nested-first.log"), File("nested-second.log") };
        foreach (var member in members)
        {
            workspace.Roots.Add(member);
            workspace.AddToGroup(member.Id, nested.Id);
        }
        for (var index = 0; index < LogEntrySourceId.MaxSources - (overflow ? 1 : 2); ++index)
        {
            var sibling = File($"sibling-{index}.log");
            workspace.Roots.Add(sibling);
            if (parent != null) workspace.AddToGroup(sibling.Id, parent.Id);
        }
        nested.ExcludedDataSources.Add(members[1].Id);
        parent?.ExcludedDataSources.Add(nested.Id);
        var before = workspace.All.Select(source => (source.Id, source.ParentId)).ToArray();
        if (overflow && parent != null)
        {
            Assert.Throws<ArgumentException>(() => workspace.Remove(nested.Id));
            workspace.All.Select(source => (source.Id, source.ParentId)).Should().Equal(before);
            workspace.GetChildren(nested.Id).Should().Equal(members);
            parent!.ExcludedDataSources.Should().Equal(nested.Id);
            nested.ExcludedDataSources.Should().Equal(members[1].Id);
        }
        else
        {
            workspace.Remove(nested.Id);
            workspace.Contains(nested.Id).Should().BeFalse();
            var promoted = parent == null ? workspace.Roots : workspace.GetChildren(parent.Id);
            promoted.Take(2).Should().Equal(members);
            promoted.Count().Should().Be(LogEntrySourceId.MaxSources + (overflow ? 1 : 0));
            if (parent != null) parent.ExcludedDataSources.Should().BeEquivalentTo(members.Select(member => member.Id));
        }
    }

    private static DataSource File(string path) => new(path) { Id = DataSourceId.CreateNew() };
}
