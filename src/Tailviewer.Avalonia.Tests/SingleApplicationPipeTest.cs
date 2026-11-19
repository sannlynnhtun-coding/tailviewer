using System.Collections.Concurrent;
using System.IO.Pipes;
using FluentAssertions;
using NUnit.Framework;

namespace Tailviewer.Avalonia.Tests;

[TestFixture]
public sealed class SingleApplicationPipeTest
{
    [Test]
    public async Task AcknowledgementFollowsDispatchAndSubsequentRequestsRetainTheirOrder()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new ConcurrentQueue<string[]>();
        await using var host = new Host(async (paths, token) =>
        {
            received.Enqueue(paths);
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        });
        var send = SingleApplicationPipe.SendAsync(host.Name, new[] { "မြန်မာ 😀.log", "folder name" },
            TimeSpan.FromSeconds(5), CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        send.IsCompleted.Should().BeFalse("because accepting a connection is not acknowledgement of dispatch");
        release.TrySetResult();
        await send;
        await SingleApplicationPipe.SendAsync(host.Name, Array.Empty<string>(), TimeSpan.FromSeconds(5), CancellationToken.None);
        received.Should().HaveCount(2);
        received.ToArray()[0].Should().Equal(Path.GetFullPath("မြန်မာ 😀.log"), Path.GetFullPath("folder name"));
        received.ToArray()[1].Should().BeEmpty();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task MalformedOrTruncatedClientsDoNotPoisonTheNextRequest(bool truncated)
    {
        var received = new ConcurrentQueue<string[]>();
        await using var host = new Host((paths, _) => { received.Enqueue(paths); return Task.CompletedTask; });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using (var client = new NamedPipeClientStream(".", host.Name, PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await client.ConnectAsync(deadline.Token);
            await client.WriteAsync(truncated ? new byte[] { 32, 0 } : new byte[4], deadline.Token);
            await client.FlushAsync(deadline.Token);
        }
        await SingleApplicationPipe.SendAsync(host.Name, new[] { "next.log" }, TimeSpan.FromSeconds(5), deadline.Token);
        received.Should().ContainSingle().Which.Should().Equal(Path.GetFullPath("next.log"));
    }

    [Test]
    public async Task StalledClientExpiresAndListenerAcceptsTheNextRequest()
    {
        var received = new ConcurrentQueue<string[]>();
        await using var host = new Host((paths, _) => { received.Enqueue(paths); return Task.CompletedTask; }, TimeSpan.FromMilliseconds(100));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var stalled = new NamedPipeClientStream(".", host.Name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await stalled.ConnectAsync(deadline.Token);
        // No prefix is sent: only the production per-connection deadline can release this server slot.
        await SingleApplicationPipe.SendAsync(host.Name, new[] { "after-stall.log" }, TimeSpan.FromSeconds(5), deadline.Token);
        received.Should().ContainSingle().Which.Should().Equal(Path.GetFullPath("after-stall.log"));
    }

    [Test]
    public void UnavailableServerTimesOutAndCallerCancellationRemainsDistinguishable()
    {
        var name = "tailviewer-absent-test-" + Guid.NewGuid().ToString("N");
        Assert.ThrowsAsync<TimeoutException>(() => SingleApplicationPipe.SendAsync(name, Array.Empty<string>(),
            TimeSpan.FromMilliseconds(100), CancellationToken.None));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.CatchAsync<OperationCanceledException>(() => SingleApplicationPipe.SendAsync(name, Array.Empty<string>(),
            TimeSpan.FromSeconds(5), cancelled.Token));
    }

    [Test]
    public async Task CancellingListenerReleasesItsNameForANewListener()
    {
        var first = new Host((_, _) => Task.CompletedTask);
        var name = first.Name;
        await first.DisposeAsync();
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stopping = new CancellationTokenSource();
        var listener = SingleApplicationPipe.ListenAsync(name, (_, _) => { received.TrySetResult(); return Task.CompletedTask; },
            TimeSpan.FromSeconds(5), stopping.Token);
        try
        {
            await SingleApplicationPipe.SendAsync(name, Array.Empty<string>(), TimeSpan.FromSeconds(5), CancellationToken.None);
            received.Task.IsCompletedSuccessfully.Should().BeTrue();
        }
        finally { stopping.Cancel(); await listener.WaitAsync(TimeSpan.FromSeconds(5)); }
    }

    private sealed class Host : IAsyncDisposable
    {
        public string Name { get; } = "tailviewer-listener-test-" + Guid.NewGuid().ToString("N");
        private readonly CancellationTokenSource _stopping = new();
        private readonly Task _listener;

        public Host(Func<string[], CancellationToken, Task> dispatch, TimeSpan? timeout = null) =>
            _listener = SingleApplicationPipe.ListenAsync(Name, dispatch, timeout ?? TimeSpan.FromSeconds(5), _stopping.Token);

        public async ValueTask DisposeAsync()
        {
            _stopping.Cancel();
            try { await _listener.WaitAsync(TimeSpan.FromSeconds(5)); }
            finally { _stopping.Dispose(); }
        }
    }
}
