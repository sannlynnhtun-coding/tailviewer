using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using NUnit.Framework;

namespace Tailviewer.Acceptance.Tests.Migration;

[TestFixture, NonParallelizable, Apartment(ApartmentState.STA)]
public sealed class SourceEntryPointTest
{
    private string _folder = null!;
    private string _path = null!;
    private string _marker = null!;
    private Process? _process;
    private DataObject? _clipboard;
    private const string SelectedLine = "  မြန်မာ 😀  ";
    private const string PickerLine = "picker မြန်မာ 😀";

    [SetUp]
    public void SetUp()
    {
        _process = null;
        _clipboard = null;
        _folder = null!;
        Assert.That(Environment.UserInteractive, Is.True, "Native tests require an interactive Windows desktop.");
        _folder = Path.Combine(Path.GetTempPath(), "tailviewer-native-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _path = Path.Combine(_folder, "native မြန်မာ.log");
        File.WriteAllText(_path, "first\n" + SelectedLine + "\nlast");
        _marker = "tailviewer-native-" + Guid.NewGuid().ToString("N");
        var original = Clipboard.GetDataObject();
        var snapshot = new DataObject();
        if (original != null)
            foreach (var format in original.GetFormats(autoConvert: false))
            {
                var value = original.GetData(format, autoConvert: false) ??
                    throw new InvalidOperationException("An original clipboard format could not be snapshotted; clipboard contents were not changed.");
                snapshot.SetData(format, value, autoConvert: false);
            }
        _clipboard = snapshot;
        Clipboard.SetText(_marker);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (_process != null)
            {
                if (!_process.HasExited)
                {
                    _process.CloseMainWindow();
                    if (!_process.WaitForExit(2000))
                    {
                        _process.Kill(entireProcessTree: true);
                        Assert.That(_process.WaitForExit(5000), Is.True, "The owned test process must exit.");
                    }
                }
                _process.Dispose();
            }
        }
        finally
        {
            try
            {
                if (_clipboard != null)
                {
                    var current = Clipboard.ContainsText() ? Clipboard.GetText() : null;
                    if (current == _marker || current == SelectedLine || current == PickerLine ||
                        current is "first" or "last" or "picked-first" or "picked-last")
                        Clipboard.SetDataObject(_clipboard, copy: true);
                    else TestContext.Progress.WriteLine("Clipboard changed externally; preserved its current contents.");
                }
            }
            finally
            {
                if (_folder != null)
                {
                    Assert.That(Directory.GetParent(Path.GetFullPath(_folder))!.FullName,
                        Is.EqualTo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))));
                    Directory.Delete(_folder, recursive: true);
                }
            }
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CommandLineOpenCopiesOriginalUnicodeThroughNativeClipboard(bool leadingEmptyLine)
    {
        if (leadingEmptyLine) File.WriteAllText(_path, "\n" + SelectedLine + "\nlast");
        var window = StartApplication();
        SelectSecondLine(window);
        CopyAndCheck(window);
    }

    [Test]
    public void CancellingNativePickerPreservesTheOpenSourceAndSelection()
    {
        var window = StartApplication();
        SelectSecondLine(window);
        var title = window.Current.Name;
        var dialog = OpenPicker(window);
        // Closing IFileDialog through its native window pattern follows the Cancel path.
        ((WindowPattern)dialog.GetCurrentPattern(WindowPattern.Pattern)).Close();
        WaitFor(() => !window.Current.IsEnabled ? null : window, "The main window must be re-enabled after picker cancellation.");
        Assert.That(window.Current.Name, Is.EqualTo(title));
        CopyAndCheck(window);
    }

    [Test]
    public void NativePickerOpensAnOwnedUnicodePathAndCopiesItsContent()
    {
        var other = Path.Combine(_folder, "picked မြန်မာ.log");
        File.WriteAllText(other, "picked-first\n" + PickerLine + "\npicked-last");
        var window = StartApplication();
        var dialog = OpenPicker(window);
        SubmitPicker(dialog, other);
        WaitFor(() => window.Current.IsEnabled && window.Current.Name.Contains(Path.GetFileName(other), StringComparison.Ordinal) ? window : null,
            "The picked owned file must become the active source.");
        SelectSecondLine(window);
        CopyAndCheck(window, PickerLine);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MultiFileEntryPointsKeepBothSourcesAndActivateTheLastFile(bool picker)
    {
        var other = Path.Combine(_folder, "picked မြန်မာ.log");
        File.WriteAllText(other, "picked-first\n" + PickerLine + "\npicked-last");
        var window = picker ? StartApplication() : StartApplication(_path, other);
        if (picker) SubmitPicker(OpenPicker(window), "\"" + _path + "\" \"" + other + "\"");
        var sources = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Sources")), "The source tree must be accessible.");
        WaitFor(() => window.Current.IsEnabled &&
            sources.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Count == 2 &&
            window.Current.Name.Contains(Path.GetFileName(other), StringComparison.Ordinal) ? window : null,
            "Both requested sources must remain open and the last must become active without duplicating the first.");
        SelectSecondLine(window);
        CopyAndCheck(window, PickerLine);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NativeFolderPickerOpensOrCancelsWithoutRemovingExistingSources(bool cancel)
    {
        var window = StartApplication();
        SelectSecondLine(window);
        var title = window.Current.Name;
        var dialog = OpenPicker(window, folder: true);
        if (cancel)
        {
            ((WindowPattern)dialog.GetCurrentPattern(WindowPattern.Pattern)).Close();
            WaitFor(() => window.Current.IsEnabled ? window : null, "The main window must recover after folder-picker cancellation.");
            Assert.That(window.Current.Name, Is.EqualTo(title));
            CopyAndCheck(window);
            return;
        }
        SubmitPicker(dialog, _folder);
        WaitFor(() => window.Current.IsEnabled && window.Current.Name.Contains(Path.GetFileName(_folder), StringComparison.Ordinal) ? window : null,
            "The selected folder must become the active source.");
        var sources = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Sources")), "The source tree must be accessible.");
        var folderItem = WaitFor(() => sources.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem))
            .Cast<AutomationElement>().FirstOrDefault(item => item.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Count == 1),
            "The folder watcher must discover the owned log file.");
        Assert.That(sources.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Count, Is.EqualTo(2));
        var child = folderItem.FindFirst(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem));
        ((SelectionItemPattern)child.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        SelectSecondLine(window);
        CopyAndCheck(window);
    }

    [Test]
    public void NativeOleDropOpensFilesAndFoldersWithoutMovingOrDuplicatingLogs()
    {
        var other = Path.Combine(_folder, "picked မြန်မာ.log");
        var content = "picked-first\n" + PickerLine + "\npicked-last";
        File.WriteAllText(other, content);
        var window = StartApplication();
        SelectSecondLine(window);
        var data = new DataObject();
        data.SetFileDropList(new System.Collections.Specialized.StringCollection { _folder, other });
        for (var attempt = 0; attempt < 2; ++attempt)
        {
            Assert.That(NativeOleDrag.Drop(data, window, _process!), Is.EqualTo(DragDropEffects.Link));
            WaitFor(() => window.Current.Name.Contains(Path.GetFileName(other), StringComparison.Ordinal) ? window : null,
                "The last OLE-dropped file must become active.");
            SelectSecondLine(window);
            CopyAndCheck(window, PickerLine);
        }
        var sources = window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Sources"));
        Assert.That(sources.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Count, Is.EqualTo(3));
        Assert.That(File.ReadAllText(other), Is.EqualTo(content));
        Assert.That(File.ReadAllText(_path), Is.EqualTo("first\n" + SelectedLine + "\nlast"));
    }

    [Test]
    public void NativeOleTextDropIsRejectedWithoutChangingTheSourceOrSelection()
    {
        var window = StartApplication();
        SelectSecondLine(window);
        var title = window.Current.Name;
        var data = new DataObject();
        data.SetText(_path);
        Assert.That(NativeOleDrag.Drop(data, window, _process!), Is.EqualTo(DragDropEffects.None));
        Assert.That(window.Current.Name, Is.EqualTo(title));
        CopyAndCheck(window);
    }

    private static void SubmitPicker(AutomationElement dialog, string paths)
    {
        var fileName = WaitFor(() => dialog.FindFirst(TreeScope.Descendants,
            new OrCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "1148"),
                new PropertyCondition(AutomationElement.AutomationIdProperty, "1152"))), "The native picker must expose its file/folder-name field.");
        if (!fileName.TryGetCurrentPattern(ValuePattern.Pattern, out var value))
        {
            fileName = fileName.FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            value = fileName.GetCurrentPattern(ValuePattern.Pattern);
        }
        ((ValuePattern)value).SetValue(paths);
        var open = WaitFor(() => dialog.FindFirst(TreeScope.Descendants,
            new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "1"),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))), "The native picker must expose its Open button.");
        ((InvokePattern)open.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }

    private AutomationElement OpenPicker(AutomationElement window, bool folder = false)
    {
        InvokeButton(window, folder ? "Open folder" : "Open file");
        var title = folder ? "Open log folder" : "Open log file";
        return WaitFor(() => window.FindFirst(TreeScope.Descendants,
                new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window),
                    new PropertyCondition(AutomationElement.NameProperty, title))) ??
            AutomationElement.RootElement.FindAll(TreeScope.Children,
                    new PropertyCondition(AutomationElement.ProcessIdProperty, _process!.Id))
                .Cast<AutomationElement>().FirstOrDefault(element => element.Current.Name == title),
            "The owned process must show its native file picker.");
    }

    private AutomationElement StartApplication(params string[] paths)
    {
        var executable = Path.Combine(TestContext.CurrentContext.TestDirectory, "app", "Tailviewer.Avalonia.exe");
        Assert.That(File.Exists(executable), Is.True, "Build the isolated native acceptance project first.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = _folder };
        foreach (var path in paths.Length == 0 ? new[] { _path } : paths) start.ArgumentList.Add(path);
        _process = Process.Start(start) ?? throw new InvalidOperationException("The owned app process did not start.");
        return WaitFor(() =>
        {
            _process.Refresh();
            Assert.That(_process.HasExited, Is.False, "The owned application must stay alive during startup.");
            return _process.MainWindowHandle == IntPtr.Zero ? null : AutomationElement.FromHandle(_process.MainWindowHandle);
        }, "The owned application must expose a native main window.");
    }

    private static void SelectSecondLine(AutomationElement window)
    {
        var rows = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Log lines")), "The app must expose its log list.");
        var item = WaitFor(() =>
        {
            var items = rows.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
            return items.Count >= 3 ? items[1] : null;
        }, "The owned three-line file must become readable.");
        ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
    }

    private static void CopyAndCheck(AutomationElement window, string expected = SelectedLine)
    {
        InvokeButton(window, "Copy selected");
        WaitFor(() => Clipboard.ContainsText() && Clipboard.GetText() == expected ? window : null,
            "The native clipboard must receive the original owned fixture line (contents are not logged).");
    }

    private static void InvokeButton(AutomationElement root, string text)
    {
        var button = WaitFor(() => root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
            .Cast<AutomationElement>().FirstOrDefault(element => element.Current.Name.Contains(text, StringComparison.Ordinal) && element.Current.IsEnabled),
            "The expected enabled app button must be accessible: " + text);
        ((InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
    }

    private static AutomationElement WaitFor(Func<AutomationElement?> query, string message)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            try { if (query() is { } element) return element; }
            catch (ElementNotAvailableException) { }
            catch (COMException) { }
            Thread.Sleep(50);
        }
        Assert.Fail(message);
        throw new InvalidOperationException(message);
    }
}
