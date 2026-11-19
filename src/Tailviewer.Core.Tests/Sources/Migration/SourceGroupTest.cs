#if NET10_0_OR_GREATER
using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Tailviewer.Api;
using Tailviewer.Settings;

namespace Tailviewer.Core.Tests.Sources.Migration
{
    [TestFixture]
    public sealed class SourceGroupTest
    {
        [Test]
        public void ExclusionsPreserveSourceSlotsAndUnknownSettings()
        {
            var scheduler = new ManualTaskScheduler();
            using var first = new InMemoryLogSource();
            using var second = new InMemoryLogSource();
            first.AddEntry("first", LevelFlags.Info, new DateTime(2026, 1, 1));
            second.AddEntry("second", LevelFlags.Info, new DateTime(2026, 1, 2));
            var firstId = DataSourceId.CreateNew();
            var secondId = DataSourceId.CreateNew();
            var unknown = DataSourceId.CreateNew();
            var settings = new DataSource { Id = DataSourceId.CreateNew() };
            settings.ExcludedDataSources.UnionWith(new[] { firstId, unknown });
            using var group = new SourceGroup(scheduler);
            var view = group.LogSource;
            group.Configure(settings, new[] { (firstId, (ILogSource)first), (secondId, second) });
            scheduler.Run(4);
            Assert.That(group.SourceIds, Is.EqualTo(new[] { firstId, secondId }));
            Assert.That(view.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(1));
            Assert.That(view.GetColumn(new LogSourceSection(0, 1), Core.Columns.RawContent), Is.EqualTo(new[] { "second" }));
            Assert.That(view.GetColumn(new LogSourceSection(0, 1), Core.Columns.SourceId), Is.EqualTo(new[] { new LogEntrySourceId(1) }));
            settings.ExcludedDataSources.Remove(firstId);
            group.Configure(settings, new[] { (firstId, (ILogSource)first), (secondId, second) });
            scheduler.Run(4);
            Assert.That(group.LogSource, Is.SameAs(view));
            Assert.That(view.GetColumn(new LogSourceSection(0, 2), Core.Columns.RawContent), Is.EqualTo(new[] { "first", "second" }));
            Assert.That(settings.ExcludedDataSources, Is.EquivalentTo(new[] { unknown }));
        }

        [Test]
        public void TimestampTiesFollowMemberOrderAndUnstampedEntriesMatchCoreRules()
        {
            var scheduler = new ManualTaskScheduler();
            using var first = new InMemoryLogSource();
            using var second = new InMemoryLogSource();
            var timestamp = new DateTime(2026, 1, 1);
            first.AddEntry("unmarked", LevelFlags.Info);
            first.AddEntry("first", LevelFlags.Info, timestamp);
            second.AddEntry("second", LevelFlags.Info, timestamp);
            var firstId = DataSourceId.CreateNew();
            var secondId = DataSourceId.CreateNew();
            var settings = new DataSource { Id = DataSourceId.CreateNew() };
            using var group = new SourceGroup(scheduler);
            group.Configure(settings, new[] { (secondId, (ILogSource)second), (firstId, first) });
            scheduler.Run(4);
            Assert.That(group.LogSource.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(2));
            Assert.That(group.LogSource.GetColumn(new LogSourceSection(0, 2), Core.Columns.RawContent), Is.EqualTo(new[] { "second", "first" }));
            Assert.That(group.LogSource.GetColumn(new LogSourceSection(0, 2), Core.Columns.OriginalIndex), Is.EqualTo(new LogLineIndex[] { 0, 1 }));
        }

        [Test]
        public void ReplacingMembersRejectsLateChangesFromThePreviousMergedView()
        {
            var scheduler = new ManualTaskScheduler();
            using var first = new InMemoryLogSource();
            using var second = new InMemoryLogSource();
            var timestamp = new DateTime(2026, 1, 1);
            first.AddEntry("old", LevelFlags.Info, timestamp);
            second.AddEntry("current", LevelFlags.Info, timestamp);
            var firstId = DataSourceId.CreateNew();
            var secondId = DataSourceId.CreateNew();
            var settings = new DataSource { Id = DataSourceId.CreateNew() };
            using var group = new SourceGroup(scheduler);
            group.Configure(settings, new[] { (firstId, (ILogSource)first) });
            scheduler.Run(4);
            Assert.That(group.LogSource.GetColumn(new LogSourceSection(0, 1), Core.Columns.RawContent), Is.EqualTo(new[] { "old" }));
            first.AddEntry("pending old", LevelFlags.Info, timestamp.AddSeconds(1));
            group.Configure(settings, new[] { (secondId, (ILogSource)second) });
            first.AddEntry("late old", LevelFlags.Info, timestamp.AddSeconds(2));
            second.AddEntry("current tail", LevelFlags.Info, timestamp.AddSeconds(3));
            scheduler.Run(6);
            Assert.That(group.SourceIds, Is.EqualTo(new[] { secondId }));
            Assert.That(group.LogSource.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(2));
            Assert.That(group.LogSource.GetColumn(new LogSourceSection(0, 2), Core.Columns.RawContent), Is.EqualTo(new[] { "current", "current tail" }));
            group.Configure(settings, Array.Empty<(DataSourceId, ILogSource)>());
            scheduler.Run(4);
            Assert.That(group.SourceIds, Is.Empty);
            Assert.That(group.LogSource.GetProperty(Core.Properties.LogEntryCount), Is.Zero);
        }

        [Test]
        public void InvalidMembershipKeepsThePreviousViewAndClosingDoesNotDisposeMembers()
        {
            var scheduler = new ManualTaskScheduler();
            using var member = new InMemoryLogSource();
            member.AddEntry("kept", LevelFlags.Info, new DateTime(2026, 1, 1));
            var id = DataSourceId.CreateNew();
            var settings = new DataSource { Id = DataSourceId.CreateNew() };
            using var group = new SourceGroup(scheduler);
            group.Configure(settings, new[] { (id, (ILogSource)member) });
            Assert.Throws<ArgumentException>(() => group.Configure(settings, new[] { (id, (ILogSource)member), (id, member) }));
            Assert.Throws<ArgumentException>(() => group.Configure(settings, new[] { (DataSourceId.Empty, (ILogSource)member) }));
            Assert.Throws<ArgumentException>(() => group.Configure(settings, new[] { (settings.Id, (ILogSource)member) }));
            Assert.Throws<ArgumentException>(() => group.Configure(settings, new[] { (id, (ILogSource)null) }));
            Assert.Throws<ArgumentException>(() => group.Configure(settings, Enumerable.Range(0, LogEntrySourceId.MaxSources + 1)
                .Select(_ => (DataSourceId.CreateNew(), (ILogSource)member))));
            scheduler.Run(4);
            Assert.That(group.SourceIds, Is.EqualTo(new[] { id }));
            Assert.That(group.LogSource.GetColumn(new LogSourceSection(0, 1), Core.Columns.RawContent), Is.EqualTo(new[] { "kept" }));
            group.Dispose();
            group.Dispose();
            Assert.Throws<ObjectDisposedException>(() => group.Configure(settings, new[] { (id, (ILogSource)member) }));
            member.AddEntry("still alive", LevelFlags.Info, new DateTime(2026, 1, 2));
            Assert.That(member.GetColumn(new LogSourceSection(0, 2), Core.Columns.RawContent), Is.EqualTo(new[] { "kept", "still alive" }));
        }
    }
}
#endif
