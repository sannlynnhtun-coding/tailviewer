using System.Diagnostics;
using System.Text.Json;
using Tailviewer.Api;
using Tailviewer.Core;
using SourceProperties = Tailviewer.Core.Properties;

// Core-only preflight; native UI/interaction and complete-application memory gates remain separate.
if (args.Length > 1 || args.Length == 1 && args[0] != "tail")
{
    Console.Error.WriteLine("Usage: Tailviewer.SourcePerformance [tail]");
    return 2;
}
var root = Path.Combine(Path.GetTempPath(), "tailviewer-source-perf-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(12));
var failures = new List<string>();
try
{
    Check(Environment.ProcessorCount >= 4 && GC.GetGCMemoryInfo().TotalAvailableMemoryBytes >= 16L * 1024 * 1024 * 1024,
        "The reference workload requires at least four logical processors and 16 GiB available to the runtime.");
    Check(new DriveInfo(Path.GetPathRoot(root)!).AvailableFreeSpace >= 2L * 1024 * 1024 * 1024,
        "The temporary drive needs at least 2 GiB free before generating the fixtures.");
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        scope = "Core data readiness, not rendered UI", processors = Environment.ProcessorCount,
        available_memory_bytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
        runtime = Environment.Version.ToString(), temp_drive = Path.GetPathRoot(root)
    }));
    if (args.Length == 1)
    {
        await LiveTailWorkload.Run(Path.Combine(root, "tail.log"), deadline.Token);
        return 0;
    }
    foreach (var sizeMiB in new[] { 100, 1024 })
    {
        var path = Path.Combine(root, "records.log");
        var records = sizeMiB * 1024 * 1024 / 128;
        var block = Enumerable.Repeat((byte)'x', 128 * 1024).ToArray();
        for (var index = 127; index < block.Length; index += 128) block[index] = (byte)'\n';
        using (var output = File.Create(path))
        {
            for (var index = 0; index < sizeMiB * 8; ++index) output.Write(block);
            output.Flush(true);
        }
        Check(new FileInfo(path).Length == (long)records * 128, "Generated fixture length differs from the manifest recipe.");
        for (var run = 1; run <= 3; ++run)
        {
            // Read through without allocating the entire fixture; warm-cache intent is recorded, not guaranteed.
            using (var warm = File.OpenRead(path)) while (warm.Read(block) > 0) { }
            await Measure(path, sizeMiB, records, run);
        }
    }
    foreach (var failure in failures) Console.Error.WriteLine(failure);
    return failures.Count == 0 ? 0 : 1;
}
finally
{
    Check(Path.GetDirectoryName(Path.GetFullPath(root)) == Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
        "Refusing cleanup outside the owned temporary fixture.");
    Directory.Delete(root, true);
}

async Task Measure(string path, int sizeMiB, int records, int run)
{
    using var scheduler = new DefaultTaskScheduler();
    using var process = Process.GetCurrentProcess();
    var allocatedStart = GC.GetTotalAllocatedBytes(false);
    var cpuStart = process.TotalProcessorTime.TotalMilliseconds;
    var timer = Stopwatch.StartNew();
    using var source = new StreamingTextLogSourceFactory(new Filesystem(scheduler), scheduler).OpenRead(path);
    await Until(() => source.GetProperty(SourceProperties.LogEntryCount) >= 32, TimeSpan.FromSeconds(120));
    var firstPage = source.GetColumn(new LogSourceSection(0, 32), Columns.RawContent,
        new LogSourceQueryOptions(LogSourceQueryMode.Default, TimeSpan.FromSeconds(2)));
    Check(firstPage.All(line => line == new string('x', 127)), "First-page contents are incorrect or unavailable.");
    var firstPageMs = timer.Elapsed.TotalMilliseconds;
    // The raw offset index includes the EOF position following a complete terminal LF.
    await Until(() => source.GetProperty(SourceProperties.LogEntryCount) == records + 1 &&
        source.GetProperty(SourceProperties.PercentageProcessed) == Percentage.HundredPercent, TimeSpan.FromSeconds(120));
    var indexedMs = timer.Elapsed.TotalMilliseconds;
    process.Refresh();
    var peakBytes = process.PeakWorkingSet64;
    var indexAllocated = GC.GetTotalAllocatedBytes(false) - allocatedStart;
    var indexCpuMs = process.TotalProcessorTime.TotalMilliseconds - cpuStart;
    VerifyOffsets(source, records + 1, index => (long)index * 128);

    var timestamp = File.GetLastWriteTimeUtc(path);
    var removedLine = records / 2 - 1;
    var changedByte = (long)(removedLine + 1) * 128 - 1;
    try
    {
        using (var output = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        {
            output.Position = changedByte;
            output.WriteByte((byte)'x');
            output.Flush(true);
        }
        File.SetLastWriteTimeUtc(path, timestamp);
        allocatedStart = GC.GetTotalAllocatedBytes(false);
        cpuStart = process.TotalProcessorTime.TotalMilliseconds;
        timer.Restart();
        await Until(() => source.GetProperty(SourceProperties.LogEntryCount) == records &&
            source.GetProperty(SourceProperties.PercentageProcessed) == Percentage.HundredPercent, TimeSpan.FromSeconds(120));
        var recoveryMs = timer.Elapsed.TotalMilliseconds;
        var recoveryAllocated = GC.GetTotalAllocatedBytes(false) - allocatedStart;
        process.Refresh();
        var recoveryCpuMs = process.TotalProcessorTime.TotalMilliseconds - cpuStart;
        VerifyOffsets(source, records, index => (long)(index <= removedLine ? index : index + 1) * 128);
        var merged = source.GetColumn(new LogSourceSection(removedLine, 1), Columns.RawContent,
            new LogSourceQueryOptions(LogSourceQueryMode.Default, TimeSpan.FromSeconds(2)));
        Check(merged.Single() == new string('x', 255), "Hidden rewrite did not merge the expected adjacent records.");
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            size_mib = sizeMiB, records, run, core_first_page_ms = firstPageMs,
            complete_index_ms = indexedMs, process_peak_working_set_bytes = peakBytes,
            indexing_allocated_bytes = indexAllocated, indexing_cpu_ms = indexCpuMs,
            recovery_allocated_bytes = recoveryAllocated, recovery_cpu_ms = recoveryCpuMs,
            hidden_rewrite_ms = recoveryMs, all_offsets_verified = true
        }));
        if (firstPageMs > (sizeMiB == 100 ? 3000 : 10000)) failures.Add($"{sizeMiB} MiB run {run}: Core first page exceeds its budget.");
        if (recoveryMs > 3000) failures.Add($"{sizeMiB} MiB run {run}: recovery exceeds 3 seconds ({recoveryMs:F0} ms).");
        if (sizeMiB == 1024 && peakBytes >= 1024L * 1024 * 1024) failures.Add($"1 GiB run {run}: Core harness process peaks at or above 1 GiB.");
    }
    finally
    {
        source.Dispose();
        using var output = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        output.Position = changedByte;
        output.WriteByte((byte)'\n');
        output.Flush(true);
        File.SetLastWriteTimeUtc(path, timestamp);
    }
}

async Task Until(Func<bool> ready, TimeSpan maximum)
{
    var timer = Stopwatch.StartNew();
    while (!ready())
    {
        deadline.Token.ThrowIfCancellationRequested();
        if (timer.Elapsed >= maximum) throw new TimeoutException("Core source did not reach the expected state within " + maximum);
        await Task.Delay(10, deadline.Token);
    }
}

static void VerifyOffsets(ILogSource source, int count, Func<int, long> expected)
{
    var column = source.Columns.OfType<IColumnDescriptor<long>>().Single(value => value.Id == "line_offset_in_bytes");
    for (var start = 0; start < count; start += 65536)
    {
        var offsets = source.GetColumn(new LogSourceSection(start, Math.Min(65536, count - start)), column);
        for (var index = 0; index < offsets.Length; ++index)
            Check(offsets[index] == expected(start + index), "Incorrect byte offset at row " + (start + index));
    }
}

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
