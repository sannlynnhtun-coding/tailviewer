using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Automation;
using NUnit.Framework;

namespace Tailviewer.Acceptance.Tests.Migration;

public sealed partial class SourceEntryPointTest
{
    [Test, Category("Performance")]
    public void NativeFiveMinuteTailDisplaysBatchesAndCopiesEveryRecord()
    {
        Assert.That(Environment.ProcessorCount, Is.GreaterThanOrEqualTo(4));
        Assert.That(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, Is.GreaterThanOrEqualTo(16L * 1024 * 1024 * 1024));
        File.WriteAllText(_path, "");
        var window = StartApplication();
        var rows = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Log lines")), "The native log list must be accessible.");
        rows.SetFocus();
        Assert.That(NativeKeyboard.IsForeground(_process!), Is.True, "Native samples require the owned foreground window.");
        var logPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "native-live-tail-" + Guid.NewGuid().ToString("N") + ".jsonl");
        using var report = new StreamWriter(logPath, false, new UTF8Encoding(false)) { AutoFlush = true };
        TestContext.AddTestAttachment(logPath, "Native live-tail measurements; synthetic records only.");
        void Report(object value) => report.WriteLine(JsonSerializer.Serialize(value));
        Report(new { process_id = _process!.Id, dpi = GetDpiForWindow(_process.MainWindowHandle), records = PacedLogWriter.Records });
        using var stop = new CancellationTokenSource(TimeSpan.FromMinutes(6));
        var times = new double[PacedLogWriter.Batches];
        var writerLateness = new double[PacedLogWriter.Batches];
        var delays = new List<double>(PacedLogWriter.Batches);
        var written = 0;
        var clock = Stopwatch.StartNew();
        var writing = Task.Run(() => PacedLogWriter.WriteAsync(_path, clock, (end, time, lateness) =>
        {
            var batch = end / PacedLogWriter.BatchSize - 1;
            writerLateness[batch] = lateness;
            Volatile.Write(ref times[batch], time);
            Volatile.Write(ref written, end);
        }, stop.Token), stop.Token);
        try
        {
            var nextBatch = 0;
            var nextProgress = 15000d;
            while (!writing.IsCompleted || nextBatch < PacedLogWriter.Batches)
            {
                stop.Token.ThrowIfCancellationRequested();
                if (writing.IsFaulted) writing.GetAwaiter().GetResult();
                Assert.That(_process!.HasExited, Is.False, "The owned viewer must stay alive.");
                Assert.That(NativeKeyboard.IsForeground(_process), Is.True, "Foreground changed; native display samples are not qualified.");
                var visible = -1;
                try { visible = LastVisibleNumericRecord(rows); }
                catch (ElementNotAvailableException) { Report(new { unavailable_sample = true }); }
                catch (COMException error) { Report(new { unavailable_sample_hresult = error.HResult }); }
                var completed = Math.Min(visible + 1, Volatile.Read(ref written)) / PacedLogWriter.BatchSize;
                var observed = clock.Elapsed.TotalMilliseconds;
                // Include every pending batch when the visible tail advances, so freezes are not omitted from p95.
                while (nextBatch < completed)
                {
                    delays.Add(observed - Volatile.Read(ref times[nextBatch]));
                    ++nextBatch;
                }
                if (observed >= nextProgress)
                {
                    Report(new { elapsed_seconds = clock.Elapsed.TotalSeconds, written = Volatile.Read(ref written), samples = delays.Count, visible });
                    nextProgress += 15000;
                }
                Assert.That(delays.Count > 0 || observed < 10000, Is.True, "No qualified numeric row became visible within ten seconds.");
                Assert.That(!writing.IsCompleted || nextBatch == PacedLogWriter.Batches ||
                    observed - Volatile.Read(ref times[^1]) < 10000, Is.True, "The native tail did not catch up with the final batch.");
                Thread.Sleep(50);
            }
            writing.GetAwaiter().GetResult();
            var duration = times[^1];
            delays.Sort();
            Array.Sort(writerLateness);
            var p95 = delays[(int)Math.Ceiling(delays.Count * .95) - 1];
            var pacingP95 = writerLateness[(int)Math.Ceiling(writerLateness.Length * .95) - 1];
            Report(new { writer_seconds = duration / 1000, samples = delays.Count, ui_tail_advance_p95_ms = p95,
                max_advance_ms = delays[^1], writer_p95_lateness_ms = pacingP95 });

            NativeKeyboard.SelectAll(_process!, rows);
            var expected = string.Join(Environment.NewLine, Enumerable.Range(0, PacedLogWriter.Records)
                .Select(index => index.ToString(CultureInfo.InvariantCulture))) + Environment.NewLine;
            CopyAndCheck(window, expected);
            var follow = window.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox));
            Assert.That(((TogglePattern)follow.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState, Is.EqualTo(ToggleState.Off));
            File.AppendAllText(_path, PacedLogWriter.Records.ToString(CultureInfo.InvariantCulture) + "\n", new UTF8Encoding(false));
            WaitFor(() => LastVisibleNumericRecord(rows) >= PacedLogWriter.Records ? rows : null,
                "The selected EOF row must receive the extra append while paused.");
            Assert.That(((TogglePattern)follow.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState, Is.EqualTo(ToggleState.Off));
            CopyAndCheck(window, expected + PacedLogWriter.Records.ToString(CultureInfo.InvariantCulture));
            Report(new { exact_records_copied = PacedLogWriter.Records, paused_append_preserves_selection = true });
            Assert.That(delays.Count, Is.EqualTo(PacedLogWriter.Batches));
            Assert.That(pacingP95, Is.LessThanOrEqualTo(100), "Writer pacing must qualify the workload.");
            Assert.That(p95, Is.LessThanOrEqualTo(1000), "Visible native tail advancement must meet the one-second p95 budget.");
        }
        finally
        {
            stop.Cancel();
            try { writing.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
    }

    private static int LastVisibleNumericRecord(AutomationElement rows)
    {
        var last = -1;
        var cache = new CacheRequest { TreeScope = TreeScope.Element };
        cache.Add(AutomationElement.NameProperty);
        cache.Add(AutomationElement.IsOffscreenProperty);
        using (cache.Activate())
        {
            foreach (AutomationElement label in rows.FindAll(TreeScope.Descendants,
                         new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)))
            {
                if (label.Cached.IsOffscreen) continue;
                var parts = label.Cached.Name.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && int.TryParse(parts[0], out var number) && int.TryParse(parts[1], out var value) && number == value + 1)
                    last = Math.Max(last, value);
            }
        }
        return last;
    }

    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
}
