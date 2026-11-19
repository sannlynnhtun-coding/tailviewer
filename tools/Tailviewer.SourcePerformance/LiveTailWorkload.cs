using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Tailviewer.Api;
using Tailviewer.Core;
using SourceProperties = Tailviewer.Core.Properties;

internal static class LiveTailWorkload
{
    public static async Task Run(string path, CancellationToken cancellationToken)
    {
        const int batches = 3000, batchSize = 100, records = batches * batchSize;
        File.WriteAllText(path, "");
        using var scheduler = new DefaultTaskScheduler();
        using var source = new StreamingTextLogSourceFactory(new Filesystem(scheduler), scheduler).OpenRead(path);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pending = new ConcurrentQueue<(int End, double WrittenMs)>();
        var delays = new List<double>(batches);
        var writerLateness = new List<double>(batches);
        var clock = Stopwatch.StartNew();
        var lastFlush = 0d;
        var flushedRecords = 0;
        var writing = Task.Run(async () =>
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n" };
            for (var batch = 1; batch <= batches; ++batch)
            {
                var due = batch * 100d;
                var wait = due - clock.Elapsed.TotalMilliseconds;
                if (wait > 0) await Task.Delay(TimeSpan.FromMilliseconds(wait), stop.Token);
                stop.Token.ThrowIfCancellationRequested();
                for (var index = (batch - 1) * batchSize; index < batch * batchSize; ++index)
                    writer.WriteLine(index.ToString(CultureInfo.InvariantCulture));
                await writer.FlushAsync(stop.Token);
                var flushed = clock.Elapsed.TotalMilliseconds;
                writerLateness.Add(Math.Max(0, flushed - due));
                Volatile.Write(ref lastFlush, flushed);
                Volatile.Write(ref flushedRecords, batch * batchSize);
                pending.Enqueue((batch * batchSize, flushed));
            }
        }, stop.Token);
        try
        {
            var nextProgress = 15000d;
            while (!writing.IsCompleted || !pending.IsEmpty)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (writing.IsFaulted) await writing;
                if (pending.TryPeek(out var sample) && source.GetProperty(SourceProperties.LogEntryCount) >= sample.End)
                {
                    var value = source.GetColumn(new LogSourceSection(sample.End - 1, 1), Columns.RawContent,
                        new LogSourceQueryOptions(LogSourceQueryMode.Default, TimeSpan.FromSeconds(2))).Single();
                    if (value != null)
                    {
                        if (value != (sample.End - 1).ToString(CultureInfo.InvariantCulture))
                            throw new InvalidOperationException("Incorrect sampled live-tail record at " + (sample.End - 1));
                        pending.TryDequeue(out _);
                        delays.Add(clock.Elapsed.TotalMilliseconds - sample.WrittenMs);
                    }
                }
                if (clock.Elapsed.TotalMilliseconds >= nextProgress)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new
                    { progress_seconds = clock.Elapsed.TotalSeconds, written_records = Volatile.Read(ref flushedRecords), ready_samples = delays.Count }));
                    nextProgress += 15000;
                }
                if (writing.IsCompleted && clock.Elapsed.TotalMilliseconds - Volatile.Read(ref lastFlush) > 30000)
                    throw new TimeoutException("Core did not catch up with the completed writer within 30 seconds.");
                await Task.Delay(10, cancellationToken);
            }
            await writing;
            while (source.GetProperty(SourceProperties.LogEntryCount) != records + 1 ||
                   source.GetProperty(SourceProperties.PercentageProcessed) != Percentage.HundredPercent)
            {
                if (clock.Elapsed.TotalMilliseconds - Volatile.Read(ref lastFlush) > 30000)
                    throw new TimeoutException("Final source count/index did not converge.");
                await Task.Delay(10, cancellationToken);
            }
            // Check complete records separately from the terminal EOF offset, as in the large-file recipe.
            for (var start = 0; start < records; start += 4096)
            {
                var values = source.GetColumn(new LogSourceSection(start, Math.Min(4096, records - start)), Columns.RawContent,
                    new LogSourceQueryOptions(LogSourceQueryMode.Default, TimeSpan.FromSeconds(2)));
                for (var index = 0; index < values.Length; ++index)
                    if (values[index] != (start + index).ToString(CultureInfo.InvariantCulture))
                        throw new InvalidOperationException("Incorrect final live-tail sequence at " + (start + index));
            }
            if (delays.Count != batches) throw new InvalidOperationException("Not all live-tail samples were observed.");
            delays.Sort();
            writerLateness.Sort();
            var p95 = delays[(int)Math.Ceiling(delays.Count * .95) - 1];
            var writerP95 = writerLateness[(int)Math.Ceiling(writerLateness.Count * .95) - 1];
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                writer_seconds = Volatile.Read(ref lastFlush) / 1000, complete_records = records, samples = delays.Count,
                core_batch_record_ready_p95_ms = p95, max_ready_ms = delays[^1], writer_p95_lateness_ms = writerP95,
                exact_final_sequence_verified = true, terminal_eof_positions = 1
            }));
            if (writerP95 > 100) throw new InvalidOperationException("Writer missed its pacing deadlines; the workload is not qualified.");
            if (p95 > 1000) throw new InvalidOperationException("Core live-tail readiness exceeds the one-second p95 budget.");
        }
        finally
        {
            stop.Cancel();
            try { await writing; }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
    }
}
