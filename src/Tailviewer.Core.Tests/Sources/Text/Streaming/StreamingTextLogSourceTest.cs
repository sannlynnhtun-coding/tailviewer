using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
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
