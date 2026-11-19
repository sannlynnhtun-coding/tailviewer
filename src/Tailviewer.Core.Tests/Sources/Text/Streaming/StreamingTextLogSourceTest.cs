using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;
using Tailviewer.Api;

namespace Tailviewer.Core.Tests.Sources.Text.Streaming
{
    [TestFixture]
    public sealed class StreamingTextLogSourceTest
    {
        private string _folder;
        private string _path;
        private ManualTaskScheduler _scheduler;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "tailviewer-stream-recovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            _path = Path.Combine(_folder, "owned.log");
            _scheduler = new ManualTaskScheduler();
        }

        [TearDown]
        public void TearDown()
        {
            Assert.That(Directory.GetParent(Path.GetFullPath(_folder)).FullName,
                Is.EqualTo(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
            Directory.Delete(_folder, true);
        }

        [Test]
        public async Task DisposalReleasesTheFileHandleWhileAnOwnedScanIsStillUnwinding()
        {
            Write("owned scan");
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var firstRead = 0;
            var real = new Filesystem(_scheduler);
            var filesystem = new Mock<IFilesystem>();
            filesystem.Setup(value => value.FileExists(_path)).Returns(true);
            filesystem.Setup(value => value.GetFileInfo(_path)).Returns(() => real.GetFileInfo(_path));
            filesystem.Setup(value => value.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                .Returns(() => new ChangeAfterOffsetReadStream(_path, () => { }, (length, count) =>
                {
                    if (Interlocked.Exchange(ref firstRead, 1) != 0) return;
                    entered.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("The owned scan gate was not released.");
                }));
            using var source = new StreamingTextLogSource(filesystem.Object, _scheduler, _path,
                LogFileFormats.GenericText, new UTF8Encoding(false));
            var scanning = Task.Run(() => _scheduler.RunOnce());
            try
            {
                Assert.That(entered.Wait(TimeSpan.FromSeconds(3)), Is.True);
                source.Dispose();
                using var exclusive = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                Assert.That(exclusive.Length, Is.GreaterThan(0));
            }
            finally
            {
                release.Set();
                await scanning;
            }
            Assert.That(source.GetLogLineIndexOfOriginalLineIndex(0), Is.EqualTo(LogLineIndex.Invalid));
        }

        [Test]
        public void AppendAfterTheEmptyLengthCheckCannotLoseTheFirstLine()
        {
            Write("");
            var real = new Filesystem(_scheduler);
            var filesystem = new Mock<IFilesystem>();
            filesystem.Setup(value => value.FileExists(_path)).Returns(() => File.Exists(_path));
            filesystem.Setup(value => value.GetFileInfo(_path)).Returns(() => real.GetFileInfo(_path));
            filesystem.Setup(value => value.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                .Returns(() => new AppendAfterEmptyLengthStream(_path));
            using (var source = new StreamingTextLogSource(filesystem.Object, _scheduler, _path,
                       LogFileFormats.GenericText, new UTF8Encoding(false)))
            {
                _scheduler.Run(2);
                Assert.That(File.ReadAllText(_path), Is.EqualTo("first\npartial"), "the owned append gate must have run");
                Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(2));
                Assert.That(Read(source, 2), Is.EqualTo(new[] { "first", "partial" }));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RewriteDuringOffsetScanCannotPairOldOffsetsWithANewCheckpoint(bool append)
        {
            const string original = "head\nold\nmiddle\ntail";
            var replacement = append ? original + "\nnew" : "head\nnew-middle\ntail";
            Write(original);
            var timestamp = File.GetLastWriteTimeUtc(_path);
            var rewritten = false;
            var real = new Filesystem(_scheduler);
            var filesystem = new Mock<IFilesystem>();
            filesystem.Setup(value => value.FileExists(_path)).Returns(() => File.Exists(_path));
            filesystem.Setup(value => value.GetFileInfo(_path)).Returns(() => real.GetFileInfo(_path));
            filesystem.Setup(value => value.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                .Returns(() => new ChangeAfterOffsetReadStream(_path, () =>
                {
                    if (rewritten) return;
                    rewritten = true;
                    Write(replacement);
                    File.SetLastWriteTimeUtc(_path, timestamp);
                }));
            using (var source = new StreamingTextLogSource(filesystem.Object, _scheduler, _path,
                       LogFileFormats.GenericText, new UTF8Encoding(false)))
            {
                _scheduler.Run(3);
                Assert.That(rewritten, Is.True, "the write must occur after the offset scanner read its old bytes");
                Assert.That(File.ReadAllText(_path), Is.EqualTo(replacement));
                var expected = replacement.Split('\n');
                Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(expected.Length));
                Assert.That(Read(source, expected.Length), Is.EqualTo(expected));
            }
        }

        private sealed class ChangeAfterOffsetReadStream : FileStream
        {
            private readonly Action _change;
            private readonly Action<int, int> _beforeRead;
            private readonly Action<int, int> _afterRead;
            public ChangeAfterOffsetReadStream(string path, Action change, Action<int, int> beforeRead = null, Action<int, int> afterRead = null)
                : base(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1)
            { _change = change; _beforeRead = beforeRead; _afterRead = afterRead; }

            public override int Read(byte[] buffer, int offset, int count)
            {
                _beforeRead?.Invoke(buffer.Length, count);
                var read = base.Read(buffer, offset, count);
                _afterRead?.Invoke(buffer.Length, read);
                if (buffer.Length == 16384 && read > 0) _change();
                return read;
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TemporaryContentOrBomRewriteCannotLeaveAnIndexForRestoredBytes(bool bom)
        {
            const string text = "head\nold\nmiddle\ntail";
            var encoding = bom ? Encoding.Unicode : new UTF8Encoding(false);
            var original = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            var alternate = bom ? (byte[])original.Clone() : Encoding.UTF8.GetBytes("head\nnew-middle\ntail");
            if (bom) { alternate[0] = 0xfe; alternate[1] = 0xff; }
            File.WriteAllBytes(_path, original);
            var timestamp = File.GetLastWriteTimeUtc(_path);
            var changed = false;
            var restored = false;
            var real = new Filesystem(_scheduler);
            var fingerprint = FileFingerprint.FromFile(real, _path);
            var filesystem = new Mock<IFilesystem>();
            filesystem.Setup(value => value.FileExists(_path)).Returns(() => File.Exists(_path));
            filesystem.Setup(value => value.GetFileInfo(_path)).Returns(() => real.GetFileInfo(_path));
            bool IsGatedRead(int size) => bom ? size == 1024 || size == 4 : size == 16384;
            filesystem.Setup(value => value.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                .Returns(() => new ChangeAfterOffsetReadStream(_path, () => { }, (size, count) =>
                {
                    if (changed || !IsGatedRead(size)) return;
                    File.WriteAllBytes(_path, alternate);
                    File.SetLastWriteTimeUtc(_path, timestamp);
                    changed = true;
                }, (size, read) =>
                {
                    if (!changed || restored || !IsGatedRead(size) || read == 0) return;
                    File.WriteAllBytes(_path, original);
                    File.SetLastWriteTimeUtc(_path, timestamp);
                    restored = true;
                }));
            using (var source = new StreamingTextLogSource(filesystem.Object, _scheduler, _path,
                       LogFileFormats.GenericText, bom ? Encoding.UTF8 : encoding))
            {
                _scheduler.Run(3);
                Assert.That(changed && restored, Is.True);
                Assert.That(FileFingerprint.FromFile(real, _path), Is.EqualTo(fingerprint));
                Assert.That(File.ReadAllBytes(_path), Is.EqualTo(original));
                Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(4));
                Assert.That(Read(source, 4), Is.EqualTo(text.Split('\n')));
            }
        }

        [Test]
        public void PrefixRewriteAfterTheIdleAuditCannotSurviveAnIncrementalAppendScan()
        {
            const string original = "head\nold\nmiddle\ntail";
            const string replacement = "head\nnew-middle\ntail\nnew";
            Write(original);
            var rewritten = false;
            var real = new Filesystem(_scheduler);
            var filesystem = new Mock<IFilesystem>();
            filesystem.Setup(value => value.FileExists(_path)).Returns(() => File.Exists(_path));
            filesystem.Setup(value => value.GetFileInfo(_path)).Returns(() => real.GetFileInfo(_path));
            filesystem.Setup(value => value.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                .Returns(() => new ChangeAfterOffsetReadStream(_path, () => { }, (size, count) =>
                {
                    if (rewritten || size != 65536 || count != original.LastIndexOf('\n') + 1) return;
                    rewritten = true;
                    var timestamp = File.GetLastWriteTimeUtc(_path);
                    Write(replacement);
                    File.SetLastWriteTimeUtc(_path, timestamp);
                }));
            using (var source = new StreamingTextLogSource(filesystem.Object, _scheduler, _path,
                       LogFileFormats.GenericText, new UTF8Encoding(false)))
            {
                _scheduler.RunOnce();
                Assert.That(Read(source, 4), Is.EqualTo(original.Split('\n')));
                File.AppendAllText(_path, "\nnew");
                _scheduler.Run(3);
                Assert.That(rewritten, Is.True);
                Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(4));
                Assert.That(Read(source, 4), Is.EqualTo(replacement.Split('\n')));
            }
        }

        private sealed class AppendAfterEmptyLengthStream : FileStream
        {
            private readonly string _path;
            public AppendAfterEmptyLengthStream(string path) : base(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)
            {
                _path = path;
            }

            public override long Length
            {
                get
                {
                    var length = base.Length;
                    if (length == 0) File.AppendAllText(_path, "first\npartial", new UTF8Encoding(false));
                    return length;
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MiddleOnlySameSizeRewriteReindexesEvenWithIdenticalMetadata(bool preserveMetadata)
        {
            var prefix = new string('h', 8192);
            var suffix = new string('t', 8192);
            var original = prefix + "\nold\nmiddle\n" + suffix;
            var replacement = prefix + "\nnew-middle\n" + suffix;
            Assert.That(Encoding.UTF8.GetByteCount(replacement), Is.EqualTo(Encoding.UTF8.GetByteCount(original)));
            Write(original);
            using (var source = Open())
            {
                _scheduler.RunOnce();
                Assert.That(Read(source, 4), Is.EqualTo(new[] { prefix, "old", "middle", suffix }));
                var filesystem = new Filesystem(_scheduler);
                var before = FileFingerprint.FromFile(filesystem, _path);
                Write(replacement);
                File.SetLastWriteTimeUtc(_path, preserveMetadata ? before.LastModified : before.LastModified.AddSeconds(1));
                if (preserveMetadata)
                    Assert.That(FileFingerprint.FromFile(filesystem, _path), Is.EqualTo(before),
                        "the hidden rewrite fixture must retain size, path and creation/write timestamps");
                _scheduler.RunOnce();
                Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(3));
                Assert.That(Read(source, 3), Is.EqualTo(new[] { prefix, "new-middle", suffix }));
                File.AppendAllText(_path, "\ncontinued", new UTF8Encoding(false));
                _scheduler.RunOnce();
                Assert.That(Read(source, 4), Is.EqualTo(new[] { prefix, "new-middle", suffix, "continued" }));
            }
        }

        [TestCase(1)]
        [TestCase(100)]
        public void LargeHiddenRewriteReportsAuditCostAndCorrectOffsets(int sizeMiB)
        {
            var block = Enumerable.Repeat((byte)'a', 1024 * 1024).ToArray();
            using (var output = File.Create(_path))
                for (var i = 0; i < sizeMiB; ++i) output.Write(block, 0, block.Length);
            var fingerprint = FileFingerprint.FromFile(new Filesystem(_scheduler), _path);
            using (var source = Open())
            {
                var timer = Stopwatch.StartNew();
                _scheduler.RunOnce();
                var initial = timer.Elapsed.TotalMilliseconds;
                Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(1));
                var idle = new double[3];
                for (var i = 0; i < idle.Length; ++i)
                {
                    timer.Restart();
                    _scheduler.RunOnce();
                    idle[i] = timer.Elapsed.TotalMilliseconds;
                }
                var middle = fingerprint.Size / 2;
                using (var output = new FileStream(_path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                    output.Position = middle;
                    output.WriteByte((byte)'\n');
                    output.Flush(true);
                }
                File.SetLastWriteTimeUtc(_path, fingerprint.LastModified);
                Assert.That(FileFingerprint.FromFile(new Filesystem(_scheduler), _path), Is.EqualTo(fingerprint));
                timer.Restart();
                _scheduler.RunOnce();
                var recovery = timer.Elapsed.TotalMilliseconds;
                Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(2));
                Assert.That(source.GetColumn(new LogSourceSection(0, 2), StreamingTextLogSource.LineOffsetInBytes),
                    Is.EqualTo(new[] { 0L, middle + 1 }));
                TestContext.WriteLine("MiB={0}; initial_ms={1:F2}; idle_audits_ms={2}; recovery_ms={3:F2}",
                    sizeMiB, initial, string.Join(",", idle.Select(value => value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))), recovery);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TruncateAndRegrowConvergesWithOrWithoutAnObservedEmptyFile(bool scanEmpty)
        {
            Write("old\ncontent\nlast");
            using (var source = Open())
            {
                _scheduler.RunOnce();
                Assert.That(Read(source, 3), Is.EqualTo(new[] { "old", "content", "last" }));
                Write("");
                if (scanEmpty)
                {
                    _scheduler.RunOnce();
                    Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.Zero);
                }
                Write("new text\n  မြန်မာ  \nlast line is longer");
                _scheduler.RunOnce();
                Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(3));
                Assert.That(Read(source, 3), Is.EqualTo(new[] { "new text", "  မြန်မာ  ", "last line is longer" }));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReadsQueuedBeforeResetCannotPublishReplacementContent(bool contiguous)
        {
            Write("old\nold second");
            using (var source = Open())
            {
                _scheduler.RunOnce();
                var stale = QueueRead(source, 2, contiguous);
                Write("new\nnew second");
                _scheduler.RunOnce();
                Assert.That(stale.GetAwaiter().GetResult(), Is.EqualTo(new string[] { null, null }),
                    "a request queued against the old index must not silently return the replacement generation");
                Assert.That(Read(source, 2), Is.EqualTo(new[] { "new", "new second" }));
            }
        }

        [Test]
        public void ReadAlreadyInProgressCannotPublishOldBytesAfterReset()
        {
            Write("old");
            using (var source = Open())
            using (var started = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                _scheduler.RunOnce();
                var destination = new[] { "sentinel" };
                using (var request = new PausedRead(source,
                           new SingleColumnLogBufferView<string>(Core.Columns.RawContent, destination, 0, 1), started, release))
                using (var reader = new StreamReader(new MemoryStream()))
                {
                    var read = Task.Run(() => request.Serve(new LogBufferList(StreamingTextLogSource.LineOffsetInBytes), reader));
                    try
                    {
                        Assert.That(started.Wait(TimeSpan.FromSeconds(2)), Is.True);
                        Write("new");
                        _scheduler.RunOnce();
                    }
                    finally { release.Set(); }
                    Assert.That(read.Wait(TimeSpan.FromSeconds(3)), Is.True);
                    Assert.That(destination, Is.EqualTo(new string[] { null }));
                }
            }
        }

        [TestCase(1200)]
        [TestCase(1201)]
        [TestCase(12000)]
        [TestCase(12001)]
        public void BomEncodingChangesRebuildOffsetsAndDecodeEveryRequestedLine(int codePage)
        {
            File.WriteAllText(_path, "old\nsecond", Encoding.UTF8);
            using (var source = Open(Encoding.UTF8))
            {
                _scheduler.RunOnce();
                Assert.That(Read(source, 2), Is.EqualTo(new[] { "old", "second" }));
                var encoding = Encoding.GetEncoding(codePage);
                File.WriteAllText(_path, "မြန်မာ\n😀 second\n  last  ", encoding);
                _scheduler.RunOnce();
                Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(3));
                Assert.That(Read(source, 3), Is.EqualTo(new[] { "မြန်မာ", "😀 second", "  last  " }));
                var expected = new[] { "မြန်မာ", "😀 second", "  last  " };
                foreach (var index in new[] { 2, 1, 0 })
                {
                    var random = QueueRead(source, 1, index % 2 == 0, index);
                    _scheduler.RunOnce();
                    Assert.That(random.GetAwaiter().GetResult(), Is.EqualTo(new[] { expected[index] }),
                        "random access must use the replacement encoding and its byte offsets");
                }
                File.AppendAllText(_path, "\nappended", encoding);
                _scheduler.RunOnce();
                Assert.That(Read(source, 4), Is.EqualTo(new[] { "မြန်မာ", "😀 second", "  last  ", "appended" }));
                Write("restored\nplain");
                _scheduler.RunOnce();
                Assert.That(Read(source, 2), Is.EqualTo(new[] { "restored", "plain" }));
            }
        }

        [Test]
        public void PreamblelessExplicitEncodingKeepsBomBytesAsContent()
        {
            File.WriteAllText(_path, "text\nsecond", Encoding.UTF8);
            using (var source = Open(new UTF8Encoding(false)))
            {
                _scheduler.RunOnce();
                Assert.That(Read(source, 2), Is.EqualTo(new[] { "\uFEFFtext", "second" }));
            }
        }

        [TestCase("a\nb", 2)]
        [TestCase("\na", 2)]
        [TestCase("\n\nz", 3)]
        [TestCase("\r\nx", 2)]
        [TestCase("a", 1)]
        public void BomFallbackRetainsLeadingNewlinesWhenThePreambleIsAbsent(string text, int count)
        {
            Write(text);
            using (var source = Open(Encoding.UTF8))
            {
                _scheduler.RunOnce();
                Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(count));
                Assert.That(string.Join("\n", Read(source, count)), Is.EqualTo(text.Replace("\r\n", "\n")));
                File.AppendAllText(_path, "\nnext", new UTF8Encoding(false));
                _scheduler.RunOnce();
                Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(count + 1));
                Assert.That(string.Join("\n", Read(source, count + 1)), Is.EqualTo(text.Replace("\r\n", "\n") + "\nnext"));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void DefaultFactoryDetectsBomWithoutChangingItsUtf8ReplacementFallback(int variant)
        {
            var encodings = new Encoding[]
            {
                new UTF8Encoding(false), new UTF8Encoding(true),
                new UnicodeEncoding(false, true), new UnicodeEncoding(true, true),
                new UTF32Encoding(false, true), new UTF32Encoding(true, true)
            };
            File.WriteAllText(_path, "\nမြန်မာ 😀\nlast", encodings[variant]);
            var factory = new StreamingTextLogSourceFactory(new Filesystem(_scheduler), _scheduler);
            using (var source = (StreamingTextLogSource)factory.OpenRead(_path))
            {
                _scheduler.RunOnce();
                Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(3));
                Assert.That(Read(source, 3), Is.EqualTo(new[] { "", "မြန်မာ 😀", "last" }));
                File.WriteAllText(_path, "new\nမြန်မာ 😀\nend", new UTF8Encoding(false));
                _scheduler.RunOnce();
                Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(3));
                Assert.That(Read(source, 3), Is.EqualTo(new[] { "new", "မြန်မာ 😀", "end" }));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DefaultFactoryRejectsMissingOrLockedFilesAndCanRetryAfterRecovery(bool locked)
        {
            var factory = new StreamingTextLogSourceFactory(new Filesystem(_scheduler), _scheduler);
            using (var exclusive = locked ? new FileStream(_path, FileMode.Create, FileAccess.ReadWrite, FileShare.None) : null)
                Assert.Catch<IOException>(() => factory.OpenRead(_path));
            Write("recovered");
            using (var source = (StreamingTextLogSource)factory.OpenRead(_path))
            {
                _scheduler.RunOnce();
                Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(1));
                Assert.That(Read(source, 1), Is.EqualTo(new[] { "recovered" }));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void IndexColumnUsesRequestedPositionsAndDestinationSlice(int variant)
        {
            Write("zero\none\ntwo");
            var factory = new StreamingTextLogSourceFactory(new Filesystem(_scheduler), _scheduler);
            using (var source = factory.OpenRead(_path))
            {
                _scheduler.RunOnce();
                var positions = new[] { new int[0], new[] { 2 }, new[] { 2, 0, 2 }, new[] { -1, 3, 1 } }[variant];
                var requested = positions.Select(position => new LogLineIndex(position)).ToArray();
                var destination = Enumerable.Repeat(new LogLineIndex(99), requested.Length + 2).ToArray();
                source.GetColumn(requested, Core.Columns.Index, destination, 1, LogSourceQueryOptions.Default);
                Assert.That(destination[0], Is.EqualTo(new LogLineIndex(99)));
                Assert.That(destination[destination.Length - 1], Is.EqualTo(new LogLineIndex(99)));
                Assert.That(destination.Skip(1).Take(requested.Length), Is.EqualTo(positions.Select(position =>
                    position >= 0 && position < 3 ? new LogLineIndex(position) : Core.Columns.Index.DefaultValue)));
            }
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void RejectedIndexDestinationDoesNotPartiallyOverwriteTheBuffer(int destinationIndex)
        {
            Write("zero\none\ntwo");
            var factory = new StreamingTextLogSourceFactory(new Filesystem(_scheduler), _scheduler);
            using (var source = factory.OpenRead(_path))
            {
                _scheduler.RunOnce();
                var destination = Enumerable.Repeat(new LogLineIndex(99), 3).ToArray();
                Assert.Catch<ArgumentException>(() => source.GetColumn(new[] { new LogLineIndex(2), new LogLineIndex(1) },
                    Core.Columns.Index, destination, destinationIndex, LogSourceQueryOptions.Default));
                Assert.That(destination, Is.All.EqualTo(new LogLineIndex(99)));
            }
        }

        [Test]
        public void OriginalPositionLookupTracksAvailableRowsAcrossSourceLifecycle()
        {
            Write("");
            var factory = new StreamingTextLogSourceFactory(new Filesystem(_scheduler), _scheduler);
            using (var source = factory.OpenRead(_path))
            {
                Assert.That(source.GetLogLineIndexOfOriginalLineIndex(0), Is.EqualTo(LogLineIndex.Invalid));
                _scheduler.RunOnce();
                Assert.That(source.GetLogLineIndexOfOriginalLineIndex(0), Is.EqualTo(LogLineIndex.Invalid));
                Write("zero\none\ntwo");
                _scheduler.RunOnce();
                Assert.That(source.GetLogLineIndexOfOriginalLineIndex(LogLineIndex.Invalid), Is.EqualTo(LogLineIndex.Invalid));
                Assert.That(source.GetLogLineIndexOfOriginalLineIndex(0), Is.EqualTo(new LogLineIndex(0)));
                Assert.That(source.GetLogLineIndexOfOriginalLineIndex(2), Is.EqualTo(new LogLineIndex(2)));
                Assert.That(source.GetLogLineIndexOfOriginalLineIndex(3), Is.EqualTo(LogLineIndex.Invalid));
                File.AppendAllText(_path, "\nthree");
                _scheduler.RunOnce();
                Assert.That(source.GetLogLineIndexOfOriginalLineIndex(3), Is.EqualTo(new LogLineIndex(3)));
                Write("only");
                _scheduler.RunOnce();
                Assert.That(source.GetLogLineIndexOfOriginalLineIndex(0), Is.EqualTo(new LogLineIndex(0)));
                Assert.That(source.GetLogLineIndexOfOriginalLineIndex(2), Is.EqualTo(LogLineIndex.Invalid));
                File.Delete(_path);
                _scheduler.RunOnce();
                Assert.That(source.GetLogLineIndexOfOriginalLineIndex(0), Is.EqualTo(LogLineIndex.Invalid));
                Write("returned\nnext");
                _scheduler.RunOnce();
                Assert.That(source.GetLogLineIndexOfOriginalLineIndex(1), Is.EqualTo(new LogLineIndex(1)));
                source.Dispose();
                Assert.That(source.GetLogLineIndexOfOriginalLineIndex(1), Is.EqualTo(LogLineIndex.Invalid));
            }
        }

        [TestCase(true, 0)]
        [TestCase(false, 0)]
        [TestCase(true, 1)]
        [TestCase(false, 1)]
        [TestCase(true, 2)]
        [TestCase(false, 2)]
        [TestCase(true, 3)]
        [TestCase(false, 3)]
        [TestCase(true, 4)]
        [TestCase(false, 4)]
        [TestCase(true, 5)]
        [TestCase(false, 5)]
        public void TerminalNewlineReadsAsEmptyTextWithoutInventingOutOfRangeRows(bool contiguous, int variant)
        {
            var encodings = new Encoding[]
            {
                new UTF8Encoding(false), new UTF8Encoding(true),
                new UnicodeEncoding(false, true), new UnicodeEncoding(true, true),
                new UTF32Encoding(false, true), new UTF32Encoding(true, true)
            };
            var encoding = encodings[variant];
            File.WriteAllText(_path, "first\nမြန်မာ 😀\n", encoding);
            var factory = new StreamingTextLogSourceFactory(new Filesystem(_scheduler), _scheduler);
            using (var source = (StreamingTextLogSource)factory.OpenRead(_path))
            {
                _scheduler.RunOnce();
                var reading = QueueRead(source, 4, contiguous);
                _scheduler.RunOnce();
                Assert.That(reading.GetAwaiter().GetResult(), Is.EqualTo(new[] { "first", "မြန်မာ 😀", "", null }));
                File.AppendAllText(_path, "continued", encoding);
                _scheduler.RunOnce();
                reading = QueueRead(source, 4, contiguous);
                _scheduler.RunOnce();
                Assert.That(reading.GetAwaiter().GetResult(), Is.EqualTo(new[] { "first", "မြန်မာ 😀", "continued", null }));
                File.AppendAllText(_path, "\n", encoding);
                _scheduler.RunOnce();
                reading = QueueRead(source, 5, contiguous);
                _scheduler.RunOnce();
                Assert.That(reading.GetAwaiter().GetResult(), Is.EqualTo(new[] { "first", "မြန်မာ 😀", "continued", "", null }));
            }
        }

        [Test]
        public void FragmentedReadContinuesAfterEofRowsAndPreservesRequestedOrder()
        {
            Write("first\nsecond\n");
            using (var source = Open())
            {
                _scheduler.RunOnce();
                var positions = new[] { 2, 0, 2, 3, 1 }.Select(index => new LogLineIndex(index)).ToArray();
                var destination = Enumerable.Repeat("sentinel", positions.Length).ToArray();
                var reading = Task.Run(() => source.GetColumn(positions, Core.Columns.RawContent, destination, 0,
                    new LogSourceQueryOptions(LogSourceQueryMode.Default, TimeSpan.FromSeconds(3))));
                Assert.That(SpinWait.SpinUntil(() => source.HasPendingReadRequests || reading.IsCompleted, TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(source.HasPendingReadRequests, Is.True);
                _scheduler.RunOnce();
                reading.GetAwaiter().GetResult();
                Assert.That(destination, Is.EqualTo(new[] { "", "first", "", null, "second" }));
            }
        }

        private sealed class PausedRead : StreamingTextLogSource.AbstractReadRequest
        {
            private readonly ManualResetEventSlim _started;
            private readonly ManualResetEventSlim _release;

            public PausedRead(StreamingTextLogSource source, ILogBuffer destination,
                ManualResetEventSlim started, ManualResetEventSlim release) : base(source, destination, 0)
            {
                _started = started;
                _release = release;
            }

            protected override int ReadLines(LogBufferList index, StreamReader reader)
            {
                _started.Set();
                if (!_release.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("The owned test gate was not released.");
                CopyDataToDestination(new[] { new LogLineIndex(0) }, new[] { "old" }, 1);
                return 1;
            }
        }

        private StreamingTextLogSource Open(Encoding encoding = null) => new StreamingTextLogSource(new Filesystem(_scheduler),
            _scheduler, _path, LogFileFormats.GenericText, encoding ?? new UTF8Encoding(false));

#if NET10_0_OR_GREATER
        [Test]
        public void IndexingManyShortLinesKeepsAllocationsBelowPerEntryObjectConstruction()
        {
            Write(string.Concat(Enumerable.Repeat("x\n", 20000)));
            using (var source = Open())
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                _scheduler.RunOnce();
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                TestContext.WriteLine("Indexed 20,000 complete records; scanner allocated {0} bytes.", allocated);
                Assert.That(source.GetProperty(Core.Properties.LogEntryCount), Is.EqualTo(20001));
                Assert.That(allocated, Is.LessThan(4 * 1024 * 1024), "offset indexing must not allocate a LogEntry object graph per row");
                Assert.That(source.GetColumn(new LogSourceSection(0, 20001), StreamingTextLogSource.LineOffsetInBytes),
                    Is.EqualTo(Enumerable.Range(0, 20001).Select(index => (long)index * 2)));
            }
        }
#endif

        private void Write(string text) => File.WriteAllText(_path, text, new UTF8Encoding(false));

        private string[] Read(StreamingTextLogSource source, int count)
        {
            var read = QueueRead(source, count, true);
            _scheduler.RunOnce();
            return read.GetAwaiter().GetResult();
        }

        private Task<string[]> QueueRead(StreamingTextLogSource source, int count, bool contiguous, int start = 0)
        {
            var destination = Enumerable.Repeat("sentinel", count).ToArray();
            var read = Task.Run(() =>
            {
                source.GetColumn(contiguous
                    ? (System.Collections.Generic.IReadOnlyList<LogLineIndex>)new LogSourceSection(start, count)
                    : Enumerable.Range(start, count).Select(index => new LogLineIndex(index)).ToArray(),
                    Core.Columns.RawContent, destination, 0,
                    new LogSourceQueryOptions(LogSourceQueryMode.Default, TimeSpan.FromSeconds(3)));
                return destination;
            });
            Assert.That(SpinWait.SpinUntil(() => source.HasPendingReadRequests || read.IsCompleted, TimeSpan.FromSeconds(2)), Is.True);
            Assert.That(source.HasPendingReadRequests, Is.True, "the owned manual scheduler has not served this request yet");
            return read;
        }
    }
}
