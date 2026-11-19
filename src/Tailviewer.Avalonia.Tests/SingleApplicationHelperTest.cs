using FluentAssertions;
using NUnit.Framework;

namespace Tailviewer.Avalonia.Tests;

[TestFixture]
public sealed class SingleApplicationHelperTest
{
    [Test]
    public void OwnershipIsExclusiveAcrossThreadsAndCanBeReacquiredAfterDispose()
    {
        var name = "tailviewer-owner-test-" + Guid.NewGuid().ToString("N");
        var first = SingleApplicationHelper.TryAcquire(name);
        first.Should().NotBeNull();
        try
        {
            SingleApplicationHelper.TryAcquire(name).Should().BeNull("because Windows mutex recursion must not create a second primary");
            SingleApplicationHelper? competing = null;
            var acquiredKernelMutex = false;
            Exception? error = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using var raw = new Mutex(false, name, new NamedWaitHandleOptions { CurrentUserOnly = true, CurrentSessionOnly = true });
                    acquiredKernelMutex = raw.WaitOne(0);
                    if (acquiredKernelMutex) raw.ReleaseMutex();
                    using var candidate = SingleApplicationHelper.TryAcquire(name);
                    competing = candidate;
                }
                catch (Exception failure) { error = failure; }
            });
            thread.Start();
            thread.Join(TimeSpan.FromSeconds(5)).Should().BeTrue();
            error.Should().BeNull();
            acquiredKernelMutex.Should().BeFalse("because exclusivity must also exist outside the in-process guard");
            competing.Should().BeNull();
        }
        finally { first!.Dispose(); }
        first.Dispose();
        using var next = SingleApplicationHelper.TryAcquire(name);
        next.Should().NotBeNull();
    }

    [Test]
    public void AbandonedOperatingSystemMutexTransfersOwnershipToTheNextAttempt()
    {
        var name = "tailviewer-abandoned-test-" + Guid.NewGuid().ToString("N");
        var options = new NamedWaitHandleOptions { CurrentUserOnly = true, CurrentSessionOnly = true };
        using var witness = new Mutex(false, name, options);
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var abandoned = new Mutex(false, name, options);
                abandoned.WaitOne();
                // Exit without ReleaseMutex; witness keeps the named kernel object alive.
            }
            catch (Exception failure) { error = failure; }
        });
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(5)).Should().BeTrue();
        error.Should().BeNull();
        using var owner = SingleApplicationHelper.TryAcquire(name);
        owner.Should().NotBeNull();
        SingleApplicationHelper.TryAcquire(name).Should().BeNull();
    }

    [Test]
    public void DisposalFromAnotherThreadDoesNotReleaseOrCorruptOwnership()
    {
        var name = "tailviewer-affinity-test-" + Guid.NewGuid().ToString("N");
        using var owner = SingleApplicationHelper.TryAcquire(name);
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { owner!.Dispose(); }
            catch (Exception failure) { error = failure; }
        });
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(5)).Should().BeTrue();
        error.Should().BeOfType<InvalidOperationException>();
        SingleApplicationHelper.TryAcquire(name).Should().BeNull();
    }

    [Test]
    public void ChannelNamesAreStableAndDistinctAcrossUsersAndSessions()
    {
        var first = SingleApplicationHelper.GetChannelName("test-user", 1);
        first.Should().Be(SingleApplicationHelper.GetChannelName("test-user", 1));
        first.Should().NotBe(SingleApplicationHelper.GetChannelName("other-user", 1));
        first.Should().NotBe(SingleApplicationHelper.GetChannelName("test-user", 2));
        first.Should().NotContain("test-user").And.NotContain("\\").And.NotContain("/");
        SingleApplicationHelper.GetChannelName().Should().Be(SingleApplicationHelper.GetChannelName());
    }
}
