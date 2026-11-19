#if NET10_0_OR_GREATER
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Tailviewer.Settings;
using Tailviewer.Api;

namespace Tailviewer.Core.Tests.Migration
{
    [TestFixture]
    public sealed class FileSourceStatesTest
    {
        [Test]
        public void MissingEmptyLockedAndRecoveredFilesReleaseTheirHandles()
        {
            var path = Path.Combine(Path.GetTempPath(), "tailviewer-state-" + Guid.NewGuid().ToString("N"));
            var scheduler = new ManualTaskScheduler();
            using var states = new FileSourceStates(new Filesystem(scheduler), scheduler);
            var source = new DataSource(path) { Id = DataSourceId.CreateNew() };
            states.SetSources(new[] { source });
            try
            {
                scheduler.RunOnce();
                Assert.That(states.TryGetStatus(source.Id, out var status), Is.True);
                Assert.That(status, Is.EqualTo("File is unavailable."));
                File.WriteAllText(path, "");
                scheduler.RunOnce();
                states.TryGetStatus(source.Id, out status);
                Assert.That(status, Is.EqualTo("No log lines."));
                using (var locked = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    scheduler.RunOnce();
                    states.TryGetStatus(source.Id, out status);
                    Assert.That(status, Is.EqualTo("File cannot be accessed."));
                }
                File.WriteAllText(path, "recovered");
                scheduler.RunOnce();
                Assert.That(states.TryGetStatus(source.Id, out status), Is.True);
                Assert.That(status, Is.Null);
                using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            finally { File.Delete(path); }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public async Task ClosedDisposedOrReconfiguredSourcesRejectAnInFlightProbe(int change)
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var scheduler = new ManualTaskScheduler();
            var filesystem = new Mock<IFilesystem>();
            filesystem.Setup(fs => fs.Open(It.IsAny<string>(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                .Returns(() =>
                {
                    entered.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
                    return new MemoryStream(new byte[] { 1 });
                });
            using var states = new FileSourceStates(filesystem.Object, scheduler);
            var source = new DataSource(Path.Combine(Path.GetTempPath(), "probe.log")) { Id = DataSourceId.CreateNew() };
            states.SetSources(new[] { source });
            var probing = Task.Run(() => scheduler.RunOnce());
            try
            {
                Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                if (change == 1) states.Dispose();
                else if (change == 2)
                    states.SetSources(new[] { new DataSource(Path.Combine(Path.GetTempPath(), "replacement.log")) { Id = source.Id } });
                else states.SetSources(Array.Empty<DataSource>());
            }
            finally { release.Set(); }
            await probing;
            Assert.That(states.TryGetStatus(source.Id, out _), Is.False);
            if (change == 2)
            {
                scheduler.RunOnce();
                Assert.That(states.TryGetStatus(source.Id, out var status), Is.True);
                Assert.That(status, Is.Null);
            }
        }
    }
}
#endif
