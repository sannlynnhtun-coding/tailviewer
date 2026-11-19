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
    [TestCase("rewrite"), TestCase("replace"), TestCase("rotate"), TestCase("truncate")]
    [Category("Performance")]
    public void NativeFileRecoveryConvergesWithinThreeSecondsWithoutStaleSelection(string mutation)
    {
        const string original = "first\nold-middle\nlast";
        const string updated = "first\nnew-middle\nlast";
        File.WriteAllText(_path, original, new UTF8Encoding(false));
        var timestamp = File.GetLastWriteTimeUtc(_path);
        var window = StartApplication();
        var rows = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Log lines")), "The log list must be accessible.");
        WaitForNativeLineCount(window, rows, 3);
        SelectSecondLine(window);
        CopyAndCheck(window, "old-middle");
        if (mutation is "replace" or "rotate")
        {
            var incoming = Path.Combine(_folder, "incoming.log");
            File.WriteAllText(incoming, updated, new UTF8Encoding(false));
            File.SetLastWriteTimeUtc(incoming, timestamp);
            if (mutation == "rotate") File.Move(_path, Path.Combine(_folder, "rotated.log"));
            File.Move(incoming, _path, overwrite: true);
        }
        else
        {
            using (var output = new FileStream(_path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            {
                if (mutation == "truncate") output.SetLength(0);
                output.Write(Encoding.UTF8.GetBytes(updated));
                output.Flush(flushToDisk: true);
            }
            File.SetLastWriteTimeUtc(_path, timestamp);
        }
        var clock = Stopwatch.StartNew();
        var reportPath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "native-recovery-" + Guid.NewGuid().ToString("N") + ".json");
        var resolved = false;
        try
        {
            WaitFor(() =>
            {
                Assert.That(NativeKeyboard.IsForeground(_process!), Is.True);
                var visible = rows.FindAll(TreeScope.Descendants, new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                    new PropertyCondition(AutomationElement.IsOffscreenProperty, false)))
                    .Cast<AutomationElement>().Select(text => text.Current.Name).ToArray();
                return new[] { $"{1,7}  first", $"{2,7}  new-middle", $"{3,7}  last" }.All(visible.Contains) &&
                    !visible.Any(name => name.Contains("old-middle", StringComparison.Ordinal)) ? rows : null;
            }, "All replacement rows must converge within three seconds after the final mutation.", timeoutSeconds: 3, pollMilliseconds: 10);
            resolved = true;
        }
        finally
        {
            clock.Stop();
            File.WriteAllText(reportPath, JsonSerializer.Serialize(new { mutation, elapsed_ms = clock.Elapsed.TotalMilliseconds,
                resolved, restored_timestamp = timestamp, original_bytes = Encoding.UTF8.GetByteCount(original),
                updated_bytes = new FileInfo(_path).Length }));
            TestContext.AddTestAttachment(reportPath, "Owned synthetic log recovery observation.");
        }
        Assert.That(clock.Elapsed.TotalMilliseconds, Is.LessThanOrEqualTo(3000));
        Assert.That(((SelectionPattern)rows.GetCurrentPattern(SelectionPattern.Pattern)).Current.GetSelection(), Is.Empty,
            "Replacement must clear stale selection rather than copy an old row.");
        var checkbox = window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox));
        Assert.That(((TogglePattern)checkbox.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState, Is.EqualTo(ToggleState.Off));
        NativeKeyboard.SelectAll(_process!, rows);
        CopyAndCheck(window, updated.Replace("\n", Environment.NewLine));
        Assert.That(File.ReadAllText(_path), Is.EqualTo(updated));
        Assert.That(File.GetLastWriteTimeUtc(_path), Is.EqualTo(timestamp));
        Assert.That(new FileInfo(_path).Length, Is.EqualTo(Encoding.UTF8.GetByteCount(original)));
        if (mutation == "rotate") Assert.That(File.ReadAllText(Path.Combine(_folder, "rotated.log")), Is.EqualTo(original));
    }

    [TestCase(1), TestCase(2), TestCase(3), Category("Performance")]
    public void NativeInteractionsDuringVisibleIndexingMeetResponseBudget(int run)
    {
        const int records = 1024 * 1024 * 1024 / FixedWidthLogWriter.RecordWidth;
        Assert.That(Environment.ProcessorCount, Is.GreaterThanOrEqualTo(4));
        Assert.That(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, Is.GreaterThanOrEqualTo(16L * 1024 * 1024 * 1024));
        Assert.That(new DriveInfo(Path.GetPathRoot(_folder)!).AvailableFreeSpace, Is.GreaterThanOrEqualTo(2L * 1024 * 1024 * 1024));
        FixedWidthLogWriter.Write(_path, 1024);
        Assert.That(new FileInfo(_path).Length, Is.EqualTo(1024L * 1024 * 1024));
        FixedWidthLogWriter.ReadThrough(_path);
        var window = StartApplicationWithPaths(Array.Empty<string>());
        var rows = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Log lines")), "The empty app must expose its log list.");
        var status = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Select lines with Shift/Ctrl • Ctrl/Cmd+C to copy")),
            "The empty app must expose its status text before file opening.");
        NativeKeyboard.GoToLine(_process!, rows);
        InvokeButton(window, "Cancel line navigation");
        var checkbox = window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox));
        var follow = (TogglePattern)checkbox.GetCurrentPattern(TogglePattern.Pattern);
        var logPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "native-index-interactions-" + Guid.NewGuid().ToString("N") + ".jsonl");
        using var report = new StreamWriter(logPath, false, new UTF8Encoding(false)) { AutoFlush = true };
        TestContext.AddTestAttachment(logPath, "Interactions qualified by incomplete visible source count.");
        report.WriteLine(JsonSerializer.Serialize(new { run, records, process_id = _process!.Id, dpi = GetDpiForWindow(_process.MainWindowHandle) }));
        var samples = new Dictionary<string, List<double>>();
        bool Measure(string action, Action invoke, Func<bool> ready)
        {
            var beforeCount = NativeSourceCount(status);
            if (beforeCount < 0 || beforeCount >= records + 1) return false;
            var timer = Stopwatch.StartNew();
            var resolved = false;
            try
            {
                invoke();
                WaitFor(() =>
                {
                    Assert.That(NativeKeyboard.IsForeground(_process), Is.True);
                    return ready() ? rows : null;
                }, "The indexing interaction must produce its expected visible response.", pollMilliseconds: 10);
                resolved = true;
                return true;
            }
            finally
            {
                var elapsed = timer.Elapsed.TotalMilliseconds;
                if (!samples.TryGetValue(action, out var values)) samples[action] = values = new List<double>();
                values.Add(elapsed);
                report.WriteLine(JsonSerializer.Serialize(new { action, source_count_at_submit = beforeCount, elapsed_ms = elapsed, resolved }));
            }
        }
        SendSecondary(_path);
        WaitFor(() =>
        {
            var count = NativeSourceCount(status);
            Assert.That(count, Is.LessThan(records + 1), "Indexing finished before qualification; this run provides no during-index evidence.");
            return count >= 64 ? rows : null;
        }, "An incomplete indexed source count must be observable.", pollMilliseconds: 10);
        var marker = "  " + new string('x', FixedWidthLogWriter.RecordWidth - 1);
        for (var cycle = 0; cycle < 30; ++cycle)
        {
            if (follow.Current.ToggleState == ToggleState.Off &&
                !Measure("follow", follow.Toggle, () => follow.Current.ToggleState == ToggleState.On)) break;
            if (!Measure("pause", follow.Toggle, () => follow.Current.ToggleState == ToggleState.Off)) break;
            rows.SetFocus();
            var first = -1;
            WaitFor(() => (first = FirstVisibleFixedLine(rows, marker)) > 0 ? rows : null,
                "Indexed data must become visible before scrolling.", pollMilliseconds: 10);
            var down = first == 1;
            var after = -1;
            try
            {
                if (!Measure("scroll", () => { if (down) NativeKeyboard.ScrollToEnd(_process, rows); else NativeKeyboard.ScrollToStart(_process, rows); },
                        () => (after = FirstVisibleFixedLine(rows, marker)) > 0 && (down ? after > first : after < first))) break;
            }
            finally
            {
                report.WriteLine(JsonSerializer.Serialize(new { first_before_scroll = first, first_after_scroll = after,
                    follow_state = follow.Current.ToggleState.ToString() }));
            }
            NativeKeyboard.GoToLine(_process, rows);
            var input = WaitFor(() => window.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, "Line number")), "The navigator must be accessible.");
            var target = Math.Max(1, NativeSourceCount(status) / 2);
            ((ValuePattern)input.GetCurrentPattern(ValuePattern.Pattern)).SetValue(target.ToString(CultureInfo.CurrentCulture));
            var go = window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Go to line"));
            var submit = (InvokePattern)go.GetCurrentPattern(InvokePattern.Pattern);
            var expected = $"{target,7}" + marker;
            if (!Measure("navigation", submit.Invoke, () => rows.FindFirst(TreeScope.Descendants, new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, expected),
                    new PropertyCondition(AutomationElement.IsOffscreenProperty, false))) != null)) break;
        }
        WaitFor(() => NativeSourceCount(status) == records + 1 ? rows : null,
            "The qualified source must eventually finish indexing the complete fixture.", timeoutSeconds: 120);
        foreach (var action in new[] { "pause", "scroll", "navigation" })
        {
            Assert.That(samples.ContainsKey(action), Is.True, "Every requested interaction needs a qualified during-index sample: " + action);
            var values = samples[action];
            values.Sort();
            var p95 = values[(int)Math.Ceiling(values.Count * .95) - 1];
            report.WriteLine(JsonSerializer.Serialize(new { action, samples = values.Count, p95_ms = p95, max_ms = values[^1] }));
            Assert.That(p95, Is.LessThanOrEqualTo(200), "During-index response p95 must meet 200 ms: " + action);
        }
    }

    private static int NativeSourceCount(AutomationElement status)
    {
        var name = status.Current.Name;
        var end = name.IndexOf(" lines • ", StringComparison.Ordinal);
        if (end > 0 && int.TryParse(name.AsSpan(0, end), NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var count)) return count;
        return -1;
    }

    private static int FirstVisibleFixedLine(AutomationElement rows, string marker) => rows.FindAll(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)).Cast<AutomationElement>()
        .Where(text => !text.Current.IsOffscreen).Select(text => text.Current.Name)
        .Where(name => name.EndsWith(marker, StringComparison.Ordinal))
        .Select(name => int.TryParse(name.AsSpan(0, name.Length - marker.Length), out var line) ? line : int.MaxValue)
        .Where(line => line != int.MaxValue).DefaultIfEmpty(-1).Min();

    [TestCase(100, 1), TestCase(100, 2), TestCase(100, 3)]
    [TestCase(1024, 1), TestCase(1024, 2), TestCase(1024, 3)]
    [Category("Performance")]
    public void NativeLargeFileVisiblePageAndIndexMemoryMeetReferenceBudgets(int sizeMiB, int run)
    {
        Assert.That(Environment.ProcessorCount, Is.GreaterThanOrEqualTo(4));
        Assert.That(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, Is.GreaterThanOrEqualTo(16L * 1024 * 1024 * 1024));
        Assert.That(new DriveInfo(Path.GetPathRoot(_folder)!).AvailableFreeSpace, Is.GreaterThanOrEqualTo(2L * 1024 * 1024 * 1024));
        FixedWidthLogWriter.Write(_path, sizeMiB);
        var length = (long)sizeMiB * 1024 * 1024;
        Assert.That(new FileInfo(_path).Length, Is.EqualTo(length));
        FixedWidthLogWriter.ReadThrough(_path);
        var records = (int)(length / FixedWidthLogWriter.RecordWidth);
        var logPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "native-large-file-" + Guid.NewGuid().ToString("N") + ".jsonl");
        using var report = new StreamWriter(logPath, false, new UTF8Encoding(false)) { AutoFlush = true };
        TestContext.AddTestAttachment(logPath, "Native large-file measurements; synthetic fixture only.");
        void Report(object value) => report.WriteLine(JsonSerializer.Serialize(value));
        Report(new { size_mib = sizeMiB, run, records, read_through_before_start = true, processors = Environment.ProcessorCount,
            available_memory_bytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes });
        var clock = Stopwatch.StartNew();
        var window = StartApplication();
        var rows = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Log lines")), "The native log list must be accessible.");
        rows.SetFocus();
        var marker = "  " + new string('x', FixedWidthLogWriter.RecordWidth - 1);
        var eofLabel = (records + 1).ToString(CultureInfo.CurrentCulture);
        WaitFor(() =>
        {
            Assert.That(NativeKeyboard.IsForeground(_process!), Is.True, "Visible-page measurements require the owned foreground app.");
            var names = rows.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)).Cast<AutomationElement>()
                .Where(label => !label.Current.IsOffscreen).Select(label => label.Current.Name).ToArray();
            return names.Any(name => name.EndsWith(marker, StringComparison.Ordinal)) &&
                names.All(name => name.EndsWith(marker, StringComparison.Ordinal) || name.Trim() == eofLabel) ? rows : null;
        }, "Every visible row must resolve to the fixed-width fixture content or terminal EOF.");
        var pageMs = clock.Elapsed.TotalMilliseconds;
        Report(new { first_visible_page_ms = pageMs, process_id = _process!.Id, dpi = GetDpiForWindow(_process.MainWindowHandle) });
        var countPrefix = (records + 1).ToString("N0", CultureInfo.CurrentCulture) + " lines";
        WaitFor(() =>
        {
            Assert.That(_process.HasExited, Is.False);
            Assert.That(NativeKeyboard.IsForeground(_process), Is.True, "Index memory measurements require the owned foreground app.");
            return window.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)).Cast<AutomationElement>()
                .Any(label => label.Current.Name.StartsWith(countPrefix, StringComparison.Ordinal)) ? rows : null;
        }, "The complete fixed-width source and terminal EOF must finish indexing.", timeoutSeconds: 120);
        _process.Refresh();
        var peakBytes = _process.PeakWorkingSet64;
        Report(new { complete_index_ms = clock.Elapsed.TotalMilliseconds, app_peak_working_set_bytes = peakBytes });
        MeasureNativeNavigation(window, rows, records, Report);
        Assert.That(pageMs, Is.LessThanOrEqualTo(sizeMiB == 100 ? 3000 : 10000), "The visible page must meet the reference first-page budget.");
        if (sizeMiB == 1024)
            Assert.That(peakBytes, Is.LessThan(1024L * 1024 * 1024), "The complete viewer process must peak below 1 GiB through indexing.");
    }

    private void MeasureNativeNavigation(AutomationElement window, AutomationElement rows, int records, Action<object> report)
    {
        // Cover both ends and midpoint before a fixed spread across the file.
        var targets = new[] { 1, records, records / 2 }.Concat(Enumerable.Range(1, 27)
            .Select(index => 1 + (int)(index * 2654435761L % records))).ToArray();
        var delays = new List<double>(targets.Length);
        var content = new string('x', FixedWidthLogWriter.RecordWidth - 1);
        foreach (var target in targets)
        {
            var preparing = Stopwatch.StartNew();
            NativeKeyboard.GoToLine(_process!, rows);
            var input = WaitFor(() => window.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.NameProperty, "Line number")), "Ctrl+G must expose the line navigator.");
            ((ValuePattern)input.GetCurrentPattern(ValuePattern.Pattern)).SetValue(target.ToString(CultureInfo.CurrentCulture));
            var go = WaitFor(() => window.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.NameProperty, "Go to line"),
                new PropertyCondition(AutomationElement.IsEnabledProperty, true))), "The navigation submit button must be accessible.");
            var invoke = (InvokePattern)go.GetCurrentPattern(InvokePattern.Pattern);
            var prepareMs = preparing.Elapsed.TotalMilliseconds;
            var expected = $"{target,7}  {content}";
            var resolved = false;
            var timer = Stopwatch.StartNew();
            try
            {
                invoke.Invoke();
                WaitFor(() =>
                {
                    Assert.That(NativeKeyboard.IsForeground(_process!), Is.True, "Navigation samples require the owned foreground viewer.");
                    return rows.FindFirst(TreeScope.Descendants, new AndCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                        new PropertyCondition(AutomationElement.NameProperty, expected),
                        new PropertyCondition(AutomationElement.IsOffscreenProperty, false)));
                }, "Navigation must reveal the exact requested line number and content.", pollMilliseconds: 10);
                resolved = true;
            }
            finally
            {
                var elapsed = timer.Elapsed.TotalMilliseconds;
                delays.Add(elapsed);
                report(new { navigation_target_line = target, navigation_ms = elapsed, preparation_ms = prepareMs, resolved });
            }
        }
        delays.Sort();
        var p95 = delays[(int)Math.Ceiling(delays.Count * .95) - 1];
        report(new { navigation_samples = delays.Count, navigation_p95_ms = p95, navigation_max_ms = delays[^1], observer_poll_ms = 10 });
        var follow = window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox));
        Assert.That(((TogglePattern)follow.GetCurrentPattern(TogglePattern.Pattern)).Current.ToggleState, Is.EqualTo(ToggleState.Off));
        Assert.That(delays.Count, Is.EqualTo(30));
        Assert.That(p95, Is.LessThanOrEqualTo(200), "Native navigation must meet the 200 ms p95 budget.");
    }

    [Test]
    public void NativeLargeSelectAllCopiesEveryRecordAndRetainsPausedSelectionAfterAppend()
    {
        var records = Enumerable.Range(0, PacedLogWriter.Records).Select(index => index.ToString(CultureInfo.InvariantCulture)).ToArray();
        File.WriteAllText(_path, string.Join("\n", records) + "\n", new UTF8Encoding(false));
        var window = StartApplication();
        var rows = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Log lines")), "The native log list must be accessible.");
        WaitForNativeLineCount(window, rows, PacedLogWriter.Records + 1);
        NativeKeyboard.SelectAll(_process!, rows);
        var expected = string.Join(Environment.NewLine, records) + Environment.NewLine;
        CopyAndCheck(window, expected);
        AppendAndCheckPausedSelection(window, rows, expected);
    }

    private void AppendAndCheckPausedSelection(AutomationElement window, AutomationElement rows, string expected)
    {
        // Select-all may focus the first line; a paused viewport need not remain at EOF.
        var visibleBeforeAppend = -1;
        WaitFor(() => (visibleBeforeAppend = LastVisibleNumericRecord(rows)) >= 0 ? rows : null,
            "The selected native viewport must remain observable.");
        TestContext.Progress.WriteLine("complete_records_copied=" + PacedLogWriter.Records + "; visible_before_append=" + visibleBeforeAppend);
        var follow = (TogglePattern)window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox)).GetCurrentPattern(TogglePattern.Pattern);
        Assert.That(follow.Current.ToggleState, Is.EqualTo(ToggleState.Off));
        File.AppendAllText(_path, PacedLogWriter.Records.ToString(CultureInfo.InvariantCulture) + "\n", new UTF8Encoding(false));
        WaitForNativeLineCount(window, rows, PacedLogWriter.Records + 2);
        Assert.That(follow.Current.ToggleState, Is.EqualTo(ToggleState.Off));
        CopyAndCheck(window, expected + PacedLogWriter.Records.ToString(CultureInfo.InvariantCulture));
        var visibleAfterAppend = -1;
        WaitFor(() => (visibleAfterAppend = LastVisibleNumericRecord(rows)) >= 0 ? rows : null,
            "The paused viewport must remain observable after append and copy.");
        Assert.That(visibleAfterAppend, Is.EqualTo(visibleBeforeAppend), "Appending while paused must preserve the viewport.");
        Assert.That(follow.Current.ToggleState, Is.EqualTo(ToggleState.Off));
    }

    private static void WaitForNativeLineCount(AutomationElement window, AutomationElement rows, int count)
    {
        var prefix = count.ToString("N0", CultureInfo.CurrentCulture) + " lines";
        WaitFor(() => window.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)).Cast<AutomationElement>()
            .Any(label => label.Current.Name.StartsWith(prefix, StringComparison.Ordinal)) ? rows : null,
            "Every complete record and the indexed EOF must be reflected in the native source count.");
    }

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
                var foreground = NativeKeyboard.ForegroundProcessId;
                if (foreground != _process.Id)
                    Report(new { foreground_changed_at_seconds = clock.Elapsed.TotalSeconds, foreground_process_id = foreground, owned_process_id = _process.Id });
                Assert.That(foreground, Is.EqualTo((uint)_process.Id), "Foreground changed; native display samples are not qualified.");
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

            WaitForNativeLineCount(window, rows, PacedLogWriter.Records + 1);
            NativeKeyboard.SelectAll(_process!, rows);
            var expected = string.Join(Environment.NewLine, Enumerable.Range(0, PacedLogWriter.Records)
                .Select(index => index.ToString(CultureInfo.InvariantCulture))) + Environment.NewLine;
            CopyAndCheck(window, expected);
            AppendAndCheckPausedSelection(window, rows, expected);
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
