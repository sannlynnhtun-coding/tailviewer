using System.IO.Pipes;

namespace Tailviewer.Avalonia;

internal static class SingleApplicationPipe
{
    public static async Task SendAsync(string name, IEnumerable<string> paths, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        await using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(deadline.Token).ConfigureAwait(false);
            await SingleApplicationMessage.WriteAsync(pipe, paths, deadline.Token).ConfigureAwait(false);
            var acknowledgement = new byte[1];
            await pipe.ReadExactlyAsync(acknowledgement, deadline.Token).ConfigureAwait(false);
            if (acknowledgement[0] != 1) throw new InvalidDataException("The existing instance did not accept the request.");
        }
        catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("The existing Tailviewer instance did not respond in time.", error);
        }
    }

    public static async Task ListenAsync(string name, Func<string[], CancellationToken, Task> dispatch,
        TimeSpan requestTimeout, CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            await using var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(stopping).ConfigureAwait(false);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopping);
                deadline.CancelAfter(requestTimeout);
                var paths = await SingleApplicationMessage.ReadAsync(pipe, deadline.Token).ConfigureAwait(false);
                // Dispatch must honor cancellation before publishing UI work after a connection expires.
                await dispatch(paths, deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
                await pipe.WriteAsync(new byte[] { 1 }, deadline.Token).ConfigureAwait(false);
                await pipe.FlushAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { return; }
            catch (Exception error) when (error is IOException or InvalidDataException or OperationCanceledException)
            {
                // A malformed, disconnected or stalled client must not disable subsequent handoffs.
            }
        }
    }
}
