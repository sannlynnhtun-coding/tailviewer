using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

internal static class PacedLogWriter
{
    public const int Batches = 3000, BatchSize = 100, Records = Batches * BatchSize;

    public static async Task WriteAsync(string path, Stopwatch clock, Action<int, double, double> flushed, CancellationToken token)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n" };
        for (var batch = 1; batch <= Batches; ++batch)
        {
            var due = batch * 100d;
            var wait = due - clock.Elapsed.TotalMilliseconds;
            if (wait > 0) await Task.Delay(TimeSpan.FromMilliseconds(wait), token);
            token.ThrowIfCancellationRequested();
            for (var index = (batch - 1) * BatchSize; index < batch * BatchSize; ++index)
                writer.WriteLine(index.ToString(CultureInfo.InvariantCulture));
            await writer.FlushAsync(token);
            var time = clock.Elapsed.TotalMilliseconds;
            flushed(batch * BatchSize, time, Math.Max(0, time - due));
        }
    }
}
