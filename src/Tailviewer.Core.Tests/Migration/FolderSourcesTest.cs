#if NET10_0_OR_GREATER
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Tailviewer.Api;
using Tailviewer.Settings;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class FolderSourcesTest
    {
        private string _folder;
        private DefaultTaskScheduler _scheduler;
        private IFilesystem _filesystem;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "tailviewer-folder-members-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            _scheduler = new DefaultTaskScheduler();
            _filesystem = new Filesystem(_scheduler);
        }

        [TearDown]
        public void TearDown()
        {
            _scheduler.Dispose();
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(_folder)), Is.EqualTo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))));
            Directory.Delete(_folder, true);
        }

        [TestCase(false, 2)]
        [TestCase(true, 3)]
        public async Task SemicolonPatternsHonorRecursiveConfiguration(bool recursive, int expected)
        {
            File.WriteAllText(Path.Combine(_folder, "one.log"), "one");
            File.WriteAllText(Path.Combine(_folder, "two.txt"), "two");
            File.WriteAllText(Path.Combine(_folder, "excluded.bin"), "binary");
            var nested = Path.Combine(_folder, "nested");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(nested, "three.log"), "three");
            var settings = Settings("*.log;*.txt", recursive);
            using (var source = new FolderSources(_filesystem, _scheduler, settings))
            {
                await Until(() => source.UnfilteredFileCount == (recursive ? 4 : 3));
                Assert.That(source.Sources.Count, Is.EqualTo(expected));
                Assert.That(source.Sources.All(child => child.ParentId == settings.Id), Is.True);
                Assert.That(source.Sources.Select(child => child.File), Is.Ordered.Using<string>(StringComparer.Ordinal));
                Assert.That(source.Error, Is.Null);
            }
        }

        [TestCase("ext:.log;ext:.txt", "upper.LOG", true)]
        [TestCase("ext:.log", "one.log.bak", false)]
        [TestCase("ext:.log;ext:.txt", "lower.txt", true)]
        [TestCase("ext:*", "no-extension", true)]
        [TestCase("*.log", "one.log.bak", true)]
        [TestCase("*.log", "upper.LOG", false)]
        [TestCase("?.log", "a.log", false)]
        [TestCase("*.txt|*.log", "one.txt", false)]
        [TestCase("", "one.bin", true)]
        public async Task PatternRulesSupportExactExtensionsAndPreserveLegacyMatching(string pattern, string name, bool matches)
        {
            File.WriteAllText(Path.Combine(_folder, name), "owned");
            using (var source = new FolderSources(_filesystem, _scheduler, Settings(pattern, false)))
            {
                await Until(() => source.UnfilteredFileCount == 1);
                Assert.That(source.Sources.Count, Is.EqualTo(matches ? 1 : 0));
            }
        }

        [Test]
        public void ReadingCachedFolderStatusDoesNotAccessTheFilesystem()
        {
            var filesystem = new Mock<IFilesystem>();
            filesystem.SetupGet(fs => fs.Watchdog).Returns(_filesystem.Watchdog);
            var calls = 0;
            filesystem.Setup(fs => fs.DirectoryExists(_folder)).Returns(() => { ++calls; return true; });
            var scheduler = new ManualTaskScheduler();
            using var source = new FolderSources(filesystem.Object, scheduler, Settings("*.log", false));
            var previousCalls = calls;
            for (var index = 0; index < 10; ++index) Assert.That(source.Error, Is.Null);
            Assert.That(calls, Is.EqualTo(previousCalls));
        }

        [Test]
        public async Task EmptyFolderDisappearanceAndRecoveryUpdateStatusWithoutMembershipChanges()
        {
            var settings = Settings("*.log", false);
            settings.LogFileFolderPath = Path.Combine(_folder, "empty");
            Directory.CreateDirectory(settings.LogFileFolderPath);
            using var source = new FolderSources(_filesystem, _scheduler, settings);
            Assert.That(source.Error, Is.Null);
            Directory.Delete(settings.LogFileFolderPath);
            await Until(() => source.Error == "Folder is unavailable.");
            Directory.CreateDirectory(settings.LogFileFolderPath);
            await Until(() => source.Error == null);
            Assert.That(source.Sources, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task SlowProbeDoesNotBlockCachedStatusAndCannotPublishAfterDisposal(bool dispose)
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var scheduler = new ManualTaskScheduler();
            var pause = false;
            var filesystem = new Mock<IFilesystem>();
            filesystem.SetupGet(fs => fs.Watchdog).Returns(_filesystem.Watchdog);
            filesystem.Setup(fs => fs.DirectoryExists(_folder)).Returns(() =>
            {
                if (!pause) return true;
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
                return false;
            });
            using var source = new FolderSources(filesystem.Object, scheduler, Settings("*.log", false));
            var notifications = 0;
            source.Changed += () => ++notifications;
            pause = true;
            var probing = Task.Run(() => scheduler.RunOnce());
            try
            {
                Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                var reading = Task.Run(() => source.Error);
                Assert.That(await Task.WhenAny(reading, Task.Delay(TimeSpan.FromSeconds(1))), Is.SameAs(reading));
                Assert.That(await reading, Is.Null);
                if (dispose) source.Dispose();
            }
            finally { release.Set(); }
            await probing;
            Assert.That(source.Error, Is.EqualTo(dispose ? null : "Folder is unavailable."));
            Assert.That(notifications, Is.EqualTo(dispose ? 0 : 1));
        }

        [Test]
        public async Task MissingFolderRecoversAndChildIdsSurviveRemovalAndReopening()
        {
            var settings = Settings("*.log", false);
            settings.LogFileFolderPath = Path.Combine(_folder, "later");
            var path = Path.Combine(settings.LogFileFolderPath, "same.log");
            DataSourceId child;
            using (var source = new FolderSources(_filesystem, _scheduler, settings))
            {
                Assert.That(source.Error, Is.Not.Null);
                Assert.That(source.Sources, Is.Empty);
                Directory.CreateDirectory(settings.LogFileFolderPath);
                File.WriteAllText(path, "first");
                await Until(() => source.Sources.Count == 1 && source.Error == null);
                child = source.Sources.Single().Id;
                File.Delete(path);
                await Until(() => source.UnfilteredFileCount == 0);
                Assert.That(source.Sources, Is.Empty);
                File.WriteAllText(path, "returned");
                await Until(() => source.Sources.Count == 1);
                Assert.That(source.Sources.Single().Id, Is.EqualTo(child));
            }
            using (var reopened = new FolderSources(_filesystem, _scheduler, settings))
            {
                await Until(() => reopened.Sources.Count == 1);
                Assert.That(reopened.Sources.Single().Id, Is.EqualTo(child));
                Assert.That(reopened.Sources.Single().File, Is.EqualTo(path));
            }
            Assert.That(settings.LogFileFolderPath, Is.EqualTo(Path.Combine(_folder, "later")));
        }

        private DataSource Settings(string pattern, bool recursive) => new DataSource
        {
            Id = DataSourceId.CreateNew(), LogFileFolderPath = _folder,
            LogFileSearchPattern = pattern, Recursive = recursive
        };

        private static async Task Until(Func<bool> ready)
        {
            var timer = Stopwatch.StartNew();
            while (!ready() && timer.Elapsed < TimeSpan.FromSeconds(8)) await Task.Delay(10);
            Assert.That(ready(), Is.True, "Folder discovery did not converge within eight seconds.");
        }
    }
}
#endif
