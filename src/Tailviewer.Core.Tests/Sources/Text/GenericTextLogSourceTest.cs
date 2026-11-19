using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Moq;
using Tailviewer.Api;

namespace Tailviewer.Core.Tests.Sources.Text
{
    [TestFixture]
    public sealed class GenericTextLogSourceTest
    {
        [Test]
        public void MetadataViewRejectsNullInputs()
        {
            using var source = new InMemoryLogSource();
            Assert.Throws<ArgumentNullException>(() => FileLogSourceFactory.CreateMetadataView(null, source));
            Assert.Throws<ArgumentNullException>(() => FileLogSourceFactory.CreateMetadataView(new ManualTaskScheduler(), null));
        }

        [Test]
        public void MetadataViewUsesTheEarliestParsedTimestampAndLeavesItsInputOpen()
        {
            using var raw = new InMemoryLogSource();
            raw.AddEntry("2026-11-20 12:00:03.000 INFO later", LevelFlags.None);
            raw.AddEntry("2026-11-20 12:00:01.000 INFO earlier", LevelFlags.None);
            using var parsed = FileLogSourceFactory.ParseText(raw);
            var scheduler = new ManualTaskScheduler();
            using var metadata = FileLogSourceFactory.CreateMetadataView(scheduler, parsed);
            scheduler.Run(4);
            Assert.That(metadata.GetProperty(Core.Properties.StartTimestamp), Is.EqualTo(new DateTime(2026, 11, 20, 12, 0, 1)));
            Assert.That(metadata.GetColumn(new LogSourceSection(0, 2), Core.Columns.ElapsedTime),
                Is.EqualTo(new TimeSpan?[] { TimeSpan.FromSeconds(2), TimeSpan.Zero }));
            metadata.Dispose();
            Assert.That(parsed.GetColumn(new LogSourceSection(0, 1), Core.Columns.RawContent)[0], Does.EndWith("INFO later"));
        }

        [Test]
        public void MultilineParserViewRetainsInheritedTimestampWhenEntriesAreReadTogether()
        {
            using var raw = new InMemoryLogSource();
            raw.AddEntry("2026-11-19 12:00:01.000 ERROR header", LevelFlags.None);
            raw.AddEntry("    at original frame", LevelFlags.None);
            using var parsed = FileLogSourceFactory.ParseText(raw);
            var scheduler = new ManualTaskScheduler();
            using var multiline = FileLogSourceFactory.CreateMultiLineView(scheduler, parsed);
            scheduler.Run(4);
            var expected = new DateTime(2026, 11, 19, 12, 0, 1);
            Assert.That(multiline.GetColumn(new LogSourceSection(0, 2), Core.Columns.Timestamp), Is.EqualTo(new DateTime?[] { expected, expected }));
            var entries = multiline.GetEntries(new LogSourceSection(0, 2), new IColumnDescriptor[]
                { Core.Columns.Index, Core.Columns.LogEntryIndex, Core.Columns.Timestamp });
            Assert.That(entries[1].Timestamp, Is.EqualTo(expected));
            Assert.That(entries[1].LogEntryIndex, Is.EqualTo(new LogEntryIndex(0)));
        }

        [Test]
        public void FactoryParserRedetectsTimestampFormatAfterSourceReset()
        {
            using var raw = new InMemoryLogSource();
            raw.AddEntry("2026-11-19 12:00:01.000 INFO original", LevelFlags.Info);
            using var parsed = new GenericTextLogSource(raw, () => new GenericTextLogEntryParser());
            Assert.That(parsed.GetColumn(new LogSourceSection(0, 1), Core.Columns.Timestamp),
                Is.EqualTo(new DateTime?[] { new DateTime(2026, 11, 19, 12, 0, 1) }));
            raw.Clear();
            raw.AddEntry("19/11/2026 12:00:02:000 INFO replaced", LevelFlags.Info);
            Assert.That(parsed.GetColumn(new LogSourceSection(0, 1), Core.Columns.Timestamp),
                Is.EqualTo(new DateTime?[] { new DateTime(2026, 11, 19, 12, 0, 2) }));
        }

        [Test]
        public void ResetDuringAnUnderlyingReadRejectsTheOldParsedResult()
        {
            var raw = new Mock<ILogSource>();
            raw.SetupGet(source => source.Columns).Returns(new IColumnDescriptor[] { Core.Columns.Index, Core.Columns.RawContent });
            ILogSourceListener resets = null;
            raw.Setup(source => source.AddListener(It.IsAny<ILogSourceListener>(), TimeSpan.Zero, 10000))
                .Callback((ILogSourceListener listener, TimeSpan _, int __) => resets = listener);
            var invalidate = true;
            raw.Setup(source => source.GetEntries(It.IsAny<IReadOnlyList<LogLineIndex>>(), It.IsAny<ILogBuffer>(),
                    It.IsAny<int>(), It.IsAny<LogSourceQueryOptions>()))
                .Callback((IReadOnlyList<LogLineIndex> indices, ILogBuffer buffer, int offset, LogSourceQueryOptions options) =>
                {
                    buffer[offset].Index = 0;
                    buffer[offset].RawContent = "2026-11-19 12:00:01.000 INFO old read";
                    if (invalidate)
                    {
                        invalidate = false;
                        resets?.OnLogFileModified(raw.Object, LogSourceModification.Reset());
                    }
                });
            var revisions = 0;
            using var parsed = FileLogSourceFactory.ParseText(raw.Object, () => ++revisions);
            Assert.That(parsed.GetColumn(new LogSourceSection(0, 1), Core.Columns.Timestamp), Is.EqualTo(new DateTime?[] { null }));
            Assert.That(revisions, Is.EqualTo(1), "derived consumers must invalidate the read epoch that was rejected");
            Assert.That(parsed.GetColumn(new LogSourceSection(0, 1), Core.Columns.Timestamp),
                Is.EqualTo(new DateTime?[] { new DateTime(2026, 11, 19, 12, 0, 1) }));
        }

        [Test]
        public void DisposingDuringAnUnderlyingReadDoesNotUseTheDisposedParser()
        {
            var raw = new Mock<ILogSource>();
            raw.SetupGet(source => source.Columns).Returns(new IColumnDescriptor[] { Core.Columns.Index, Core.Columns.RawContent });
            var parser = new Mock<ILogEntryParser>();
            parser.SetupGet(value => value.Columns).Returns(new IColumnDescriptor[] { Core.Columns.Timestamp });
            GenericTextLogSource parsed = null;
            raw.Setup(source => source.GetEntries(It.IsAny<IReadOnlyList<LogLineIndex>>(), It.IsAny<ILogBuffer>(),
                    It.IsAny<int>(), It.IsAny<LogSourceQueryOptions>()))
                .Callback((IReadOnlyList<LogLineIndex> indices, ILogBuffer buffer, int offset, LogSourceQueryOptions options) =>
                {
                    buffer[offset].Index = 0;
                    buffer[offset].RawContent = "2026-11-19 12:00:01.000 INFO retired read";
                    parsed.Dispose();
                });
            using (parsed = new GenericTextLogSource(raw.Object, () => parser.Object))
                Assert.That(parsed.GetColumn(new LogSourceSection(0, 1), Core.Columns.Timestamp), Is.EqualTo(new DateTime?[] { null }));
            parser.Verify(value => value.Parse(It.IsAny<IReadOnlyLogEntry>()), Times.Never);
            parser.Verify(value => value.Dispose(), Times.Once);
            raw.Verify(source => source.Dispose(), Times.Once);
        }

        [Test]
        public void SourceStillClosesWhenParserDisposalFails()
        {
            var raw = new Mock<ILogSource>();
            raw.SetupGet(source => source.Columns).Returns(new IColumnDescriptor[0]);
            var parser = new Mock<ILogEntryParser>();
            parser.SetupGet(value => value.Columns).Returns(new IColumnDescriptor[0]);
            parser.Setup(value => value.Dispose()).Throws(new InvalidOperationException("owned parser cleanup failure"));
            var parsed = new GenericTextLogSource(raw.Object, () => parser.Object);
            Assert.Throws<InvalidOperationException>(() => parsed.Dispose());
            raw.Verify(source => source.Dispose(), Times.Once);
            Assert.DoesNotThrow(() => parsed.Dispose());
        }

        [Test]
        public void SuppliedParserKeepsItsExistingDisposalBehavior()
        {
            var raw = new Mock<ILogSource>();
            raw.SetupGet(source => source.Columns).Returns(new IColumnDescriptor[0]);
            var parser = new Mock<ILogEntryParser>();
            parser.SetupGet(value => value.Columns).Returns(new IColumnDescriptor[0]);
            var parsed = new GenericTextLogSource(raw.Object, parser.Object);
            parsed.Dispose();
            parser.Verify(value => value.Dispose(), Times.Never);
            raw.Verify(source => source.Dispose(), Times.Once);
        }

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
