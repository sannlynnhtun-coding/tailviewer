using System;
using NUnit.Framework;
using Moq;
using Tailviewer.Api;

namespace Tailviewer.Core.Tests.Sources.Text
{
    [TestFixture]
    public sealed class GenericTextLogSourceTest
    {
        [Test]
        public void OriginalTextRemainsExactWhileParsing()
        {
            using var raw = new InMemoryLogSource();
            raw.AddEntry("A\u0001B", LevelFlags.Info);
            using var parsed = new GenericTextLogSource(raw, new GenericTextLogEntryParser());
            Assert.That(parsed.GetColumn(new LogSourceSection(0, 1), Core.Columns.RawContent), Is.EqualTo(new[] { "A\u0001B" }));
        }

        [Test]
        public void RemovedListenersCanBeReusedWithoutRetainingTheOldProxy()
        {
            using var raw = new InMemoryLogSource();
            using var parsed = new GenericTextLogSource(raw, new GenericTextLogEntryParser());
            var listener = new Mock<ILogSourceListener>();
            parsed.AddListener(listener.Object, TimeSpan.Zero, 100);
            parsed.RemoveListener(listener.Object);
            Assert.DoesNotThrow(() => parsed.AddListener(listener.Object, TimeSpan.Zero, 100));
            parsed.RemoveListener(listener.Object);
        }

        [Test]
        public void ParsingRetainsOriginalPositionAndSourceWhileAddingTimestampAndLevel()
        {
            using var raw = new InMemoryLogSource(Core.Columns.SourceId);
            for (var index = 0; index < 42; ++index)
                raw.AddEntry("earlier", LevelFlags.Info, new DateTime(2026, 1, 1).AddSeconds(index));
            raw.Add(new LogEntry
            {
                RawContent = "2026-11-19 12:34:56.123 ERROR original",
                Timestamp = new DateTime(2026, 1, 2),
                SourceId = new LogEntrySourceId(3)
            });
            using var parsed = new GenericTextLogSource(raw, new GenericTextLogEntryParser());
            var entries = parsed.GetEntries(new LogSourceSection(42, 1), new IColumnDescriptor[]
            {
                Core.Columns.RawContent, Core.Columns.Index, Core.Columns.OriginalIndex,
                Core.Columns.LogEntryIndex, Core.Columns.SourceId, Core.Columns.Timestamp, Core.Columns.LogLevel
            });
            Assert.That(entries[0].RawContent, Is.EqualTo("2026-11-19 12:34:56.123 ERROR original"));
            Assert.That(entries[0].Index, Is.EqualTo(new LogLineIndex(42)));
            Assert.That(entries[0].OriginalIndex, Is.EqualTo(new LogLineIndex(42)));
            Assert.That(entries[0].LogEntryIndex, Is.EqualTo(new LogEntryIndex(42)));
            Assert.That(entries[0].GetValue(Core.Columns.SourceId), Is.EqualTo(new LogEntrySourceId(3)));
            Assert.That(entries[0].Timestamp, Is.EqualTo(new DateTime(2026, 11, 19, 12, 34, 56, 123)));
            Assert.That(entries[0].LogLevel, Is.EqualTo(LevelFlags.Error));
        }
    }
}
