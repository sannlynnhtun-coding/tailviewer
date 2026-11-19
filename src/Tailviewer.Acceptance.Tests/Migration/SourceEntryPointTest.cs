using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using NUnit.Framework;

namespace Tailviewer.Acceptance.Tests.Migration;

[TestFixture, NonParallelizable, Apartment(ApartmentState.STA)]
public sealed partial class SourceEntryPointTest
{
    private string _folder = null!;
    private string _path = null!;
    private string _marker = null!;
    private Process? _process;
    private DataObject? _clipboard;
    private readonly HashSet<string> _ownedCopies = new();
    private const string SelectedLine = "  မြန်မာ 😀  ";
    private const string PickerLine = "picker မြန်မာ 😀";

    [SetUp]
    public void SetUp()
    {
        _process = null;
        _clipboard = null;
        _ownedCopies.Clear();
        _folder = null!;
        Assert.That(Environment.UserInteractive, Is.True, "Native tests require an interactive Windows desktop.");
        var existing = Process.GetProcessesByName("Tailviewer.Avalonia");
        try { Assert.That(existing, Is.Empty, "Close existing Tailviewer apps before native tests; never hand test paths to a user-owned instance."); }
        finally { foreach (var process in existing) process.Dispose(); }
        using (var ownership = Tailviewer.Avalonia.SingleApplicationHelper.TryAcquire(Tailviewer.Avalonia.SingleApplicationHelper.GetChannelName()))
            Assert.That(ownership, Is.Not.Null, "An existing primary owns this user/session channel; native tests must not modify its workspace.");
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
                    if (current == _marker || current != null && _ownedCopies.Contains(current) || current == SelectedLine || current == PickerLine ||
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
    public void NativeSystemCodePageChoiceDecodesAndCopiesOriginalText()
    {
        var encoding = CodePagesEncodingProvider.Instance.GetEncoding(0) ?? Encoding.Default;
        var bytes = encoding.GetBytes("café");
        File.WriteAllBytes(_path, bytes);
        var window = StartApplication();
        var selector = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Text encoding")), "The encoding selector must be accessible.");
        Assert.That(selector.Current.ProcessId, Is.EqualTo(_process!.Id));
        var expand = (ExpandCollapsePattern)selector.GetCurrentPattern(ExpandCollapsePattern.Pattern);
        expand.Expand();
        var choice = WaitFor(() => AutomationElement.RootElement.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ProcessIdProperty, _process.Id)).Cast<AutomationElement>()
            .Select(root => root.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                new PropertyCondition(AutomationElement.NameProperty, encoding.WebName))))
            .FirstOrDefault(item => item != null), "The Windows system encoding must be selectable in the native dropdown.");
        ((SelectionItemPattern)choice.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        expand.Collapse();
        var rows = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Log lines")), "The log list must be accessible.");
        var line = WaitFor(() => rows.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>()
            .FirstOrDefault(item => item.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)).Cast<AutomationElement>()
                .Any(text => text.Current.Name.Trim() == "1  café")), "The selected decoder must render the original text.");
        ((SelectionItemPattern)line.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        CopyAndCheck(window, "café");
        Assert.That(File.ReadAllBytes(_path), Is.EqualTo(bytes));
    }

    [Test]
    public void NativeTerminalNewlineRowRendersWithoutLoadingAndCopiesTheTrailingNewline()
    {
        File.WriteAllText(_path, "first\n" + SelectedLine + "\n");
        var window = StartApplication();
        var rows = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Log lines")), "The log list must be accessible.");
        WaitFor(() =>
        {
            var items = rows.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
            return items.Count == 3 ? rows : null;
        }, "The complete terminal newline must have its indexed EOF row.");
        // Virtualized peer enumeration order can differ from logical line order.
        WaitFor(() => rows.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)).Cast<AutomationElement>()
            .Any(text => text.Current.Name.Trim() == "3") ? rows : null,
            "The EOF row must render its line number without a permanent Loading label.");
        NativeKeyboard.SelectAll(_process!, rows);
        CopyAndCheck(window, string.Join(Environment.NewLine, "first", SelectedLine, ""));
        Assert.That(File.ReadAllText(_path), Is.EqualTo("first\n" + SelectedLine + "\n"));
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

    [Test]
    public void SecondInstanceHandsOffARelativeUnicodeFileAndRestoresThePrimaryWindow()
    {
        var other = Path.Combine(_folder, "handoff မြန်မာ.log");
        File.WriteAllText(other, "picked-first\n" + PickerLine + "\npicked-last");
        var window = StartApplication();
        SelectSecondLine(window);
        CopyAndCheck(window);
        SendSecondary(Path.GetFileName(other));
        var sources = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Sources")), "The source tree must be accessible.");
        WaitFor(() => window.Current.Name.Contains(Path.GetFileName(other), StringComparison.Ordinal) &&
            sources.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Count == 2
            ? window : null, "The primary must retain both sources and activate the handed-off relative path.");
        SelectSecondLine(window);
        CopyAndCheck(window, PickerLine);
        var pattern = (WindowPattern)window.GetCurrentPattern(WindowPattern.Pattern);
        pattern.SetWindowVisualState(WindowVisualState.Minimized);
        SendSecondary();
        WaitFor(() => pattern.Current.WindowVisualState != WindowVisualState.Minimized ? window : null,
            "An empty handoff must restore the same primary window.");
        Assert.That(_process!.HasExited, Is.False);
        Assert.That(window.Current.ProcessId, Is.EqualTo(_process.Id));
        CopyAndCheck(window, PickerLine);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ANewPrimaryCanStartAfterTheOwnedPrimaryExits(bool crash)
    {
        var first = StartApplication();
        SelectSecondLine(first);
        CopyAndCheck(first);
        var channel = Tailviewer.Avalonia.SingleApplicationHelper.GetChannelName();
        // Keep the kernel object alive across a crash so restart exercises abandoned ownership.
        using var witness = new Mutex(false, channel,
            new NamedWaitHandleOptions { CurrentUserOnly = true, CurrentSessionOnly = true });
        if (crash) _process!.Kill(entireProcessTree: true);
        else Assert.That(_process!.CloseMainWindow(), Is.True);
        Assert.That(_process.WaitForExit(10000), Is.True, "Only the owned primary is stopped.");
        if (!crash) Assert.That(_process.ExitCode, Is.EqualTo(0));
        _process.Dispose();
        _process = null;
        var restarted = StartApplication();
        SelectSecondLine(restarted);
        CopyAndCheck(restarted);
        SendSecondary();
        Assert.That(_process!.HasExited, Is.False);
        Assert.That(restarted.Current.ProcessId, Is.EqualTo(_process.Id));
        Assert.That(File.ReadAllText(_path), Is.EqualTo("first\n" + SelectedLine + "\nlast"));
    }

    [Test]
    public void OccupiedChannelWithoutAListenerShowsAnErrorAndExitsWithoutTakingOwnership()
    {
        var channel = Tailviewer.Avalonia.SingleApplicationHelper.GetChannelName();
        using var occupied = Tailviewer.Avalonia.SingleApplicationHelper.TryAcquire(channel);
        Assert.That(occupied, Is.Not.Null);
        var error = StartApplication();
        Assert.That(error.Current.Name, Is.EqualTo("Tailviewer — connection error"));
        Assert.That(error.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Log lines")), Is.Null,
            "An unresponsive primary must not create a second workspace.");
        var message = error.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
            .Cast<AutomationElement>().FirstOrDefault(element => element.Current.Name.Contains("Cannot contact the existing Tailviewer instance.", StringComparison.Ordinal));
        Assert.That(message, Is.Not.Null, "The timeout must be visible to the user.");
        InvokeButton(error, "Close");
        Assert.That(_process!.WaitForExit(5000), Is.True);
        Assert.That(_process.ExitCode, Is.EqualTo(1));
        Assert.That(Tailviewer.Avalonia.SingleApplicationHelper.TryAcquire(channel), Is.Null,
            "The owned occupancy lease must remain intact after the secondary exits.");
    }

    [TestCase("before")]
    [TestCase("group")]
    [TestCase("cancel")]
    public void NativePointerTreeDragReordersGroupsOrCancelsWithoutMovingFiles(string action)
    {
        var other = Path.Combine(_folder, "pointer မြန်မာ.log");
        var content = "picked-first\n" + PickerLine + "\npicked-last";
        File.WriteAllText(other, content);
        var window = StartApplication(_path, other);
        var tree = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Sources")), "The owned tree must be accessible.");
        WaitFor(() => window.Current.Name.Contains(Path.GetFileName(other), StringComparison.Ordinal) &&
            tree.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Count == 2
            ? tree : null, "Both owned sources must finish opening.");
        SelectSecondLine(window);
        CopyAndCheck(window, PickerLine);
        var sourceItem = tree.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, other));
        var targetItem = tree.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, _path));
        Assert.That(sourceItem, Is.Not.Null, "Each source tree item must expose its full path to native accessibility.");
        Assert.That(targetItem, Is.Not.Null);
        var source = sourceItem.FindFirst(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
            new PropertyCondition(AutomationElement.NameProperty, Path.GetFileName(other))));
        var target = targetItem.FindFirst(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
            new PropertyCondition(AutomationElement.NameProperty, Path.GetFileName(_path))));
        Assert.That(source, Is.Not.Null);
        Assert.That(target, Is.Not.Null);
        var from = source!.Current.BoundingRectangle;
        var to = target!.Current.BoundingRectangle;
        NativeOleDrag.DragWithin(_process!, new Point(from.Left + Math.Min(30, from.Width / 2), from.Top + from.Height / 2),
            new Point(to.Left + Math.Min(30, to.Width / 2), to.Top + (action == "before" ? 1 : to.Height / 2)), action == "cancel");
        var grouped = action == "group";
        var expectedFirst = action == "cancel" ? _path : other;
        var root = WaitFor(() =>
        {
            var items = tree.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem));
            if (items.Count != (grouped ? 1 : 2)) return null;
            var siblings = grouped ? items[0].FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)) : items;
            return siblings.Count == 2 && siblings[0].Current.Name == expectedFirst ? items[0] : null;
        }, "Native drag must produce the expected root/member ordering.");
        if (grouped)
        {
            var child = root.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem))[0];
            ((SelectionItemPattern)child.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            SelectSecondLine(window);
        }
        CopyAndCheck(window, PickerLine);
        Assert.That(File.ReadAllText(other), Is.EqualTo(content));
        Assert.That(File.ReadAllText(_path), Is.EqualTo("first\n" + SelectedLine + "\nlast"));
    }

    [Test]
    public void NativePointerHoverExpandsACollapsedGroupBeforeDroppingTheSource()
    {
        var memberPath = Path.Combine(_folder, "hover-member.log");
        var sourcePath = Path.Combine(_folder, "hover-source မြန်မာ.log");
        File.WriteAllText(memberPath, "first\nfirst\nlast");
        var content = "picked-first\n" + PickerLine + "\npicked-last";
        File.WriteAllText(sourcePath, content);
        var window = StartApplication(_path, memberPath, sourcePath);
        var tree = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Sources")), "The owned tree must be accessible.");
        WaitFor(() => window.Current.Name.Contains(Path.GetFileName(sourcePath), StringComparison.Ordinal) ? window : null,
            "All owned startup paths must finish opening.");
        InvokeButton(window, "New group");
        var group = WaitFor(() => tree.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Merged Data Source")), "The new group must have an accessible name.");
        var member = WaitFor(() => tree.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, memberPath)), "The member must remain configured.");
        ((SelectionItemPattern)member.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        SelectSecondLine(window);
        CopyAndCheck(window, "first");
        NativeOleDrag.DragWithin(_process!, HeaderPoint(member, Path.GetFileName(memberPath)), HeaderPoint(group, "Merged Data Source"), false);
        WaitFor(() => group.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Count == 1 ? group : null,
            "The initial owned member must join the group.");
        var expansion = (ExpandCollapsePattern)group.GetCurrentPattern(ExpandCollapsePattern.Pattern);
        expansion.Collapse();
        Assert.That(expansion.Current.ExpandCollapseState, Is.EqualTo(ExpandCollapseState.Collapsed));
        var source = WaitFor(() => tree.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, sourcePath)), "The drag source must remain a root.");
        ((SelectionItemPattern)source.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        SelectSecondLine(window);
        CopyAndCheck(window, PickerLine);
        NativeOleDrag.DragWithin(_process!, HeaderPoint(source, Path.GetFileName(sourcePath)), HeaderPoint(group, "Merged Data Source"), false,
            () => WaitFor(() => expansion.Current.ExpandCollapseState == ExpandCollapseState.Expanded ? group : null,
                "The native captured pointer must expand the group before mouse release."));
        WaitFor(() => group.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Count == 2 ? group : null,
            "The source must join the already expanded group.");
        var children = group.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Cast<AutomationElement>().ToArray();
        Assert.That(children.Select(child => child.Current.Name), Is.EqualTo(new[] { memberPath, sourcePath }));
        Assert.That(tree.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Count, Is.EqualTo(2));
        ((SelectionItemPattern)children[1].GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        SelectSecondLine(window);
        CopyAndCheck(window, PickerLine);
        Assert.That(File.ReadAllText(sourcePath), Is.EqualTo(content));
        Assert.That(File.ReadAllText(memberPath), Is.EqualTo("first\nfirst\nlast"));
        Assert.That(File.ReadAllText(_path), Is.EqualTo("first\n" + SelectedLine + "\nlast"));

        static Point HeaderPoint(AutomationElement item, string name)
        {
            var label = WaitFor(() => item.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                new PropertyCondition(AutomationElement.NameProperty, name))), "The owned header text must be accessible.");
            var bounds = label.Current.BoundingRectangle;
            return new Point(bounds.Left + Math.Min(30, bounds.Width / 2), bounds.Top + bounds.Height / 2);
        }
    }

    [Test]
    public void NativeCapturedTreeDragScrollsAtTheViewportEdgeAndStopsAfterEscape()
    {
        var content = "first\n" + SelectedLine + "\nlast";
        var paths = new[] { _path }.Concat(Enumerable.Range(0, 40).Select(index =>
        {
            var path = Path.Combine(_folder, $"edge-{index:D3}.log");
            File.WriteAllText(path, content);
            return path;
        })).ToArray();
        var window = StartApplication(paths);
        var tree = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Sources")), "The owned tree must be accessible.");
        WaitFor(() => window.Current.Name.Contains(Path.GetFileName(paths[^1]), StringComparison.Ordinal) &&
            tree.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Count == paths.Length
            ? tree : null, "All owned files must be configured before dragging.");
        var source = tree.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, _path));
        Assert.That(source, Is.Not.Null);
        ((SelectionItemPattern)source.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        SelectSecondLine(window);
        CopyAndCheck(window);
        var label = WaitFor(() => source.FindFirst(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
            new PropertyCondition(AutomationElement.NameProperty, Path.GetFileName(_path)))), "The first owned source must be visible.");
        var from = label.Current.BoundingRectangle;
        var viewport = tree.Current.BoundingRectangle;
        Assert.That(viewport.Contains(new Point(from.Left + 30, from.Top + from.Height / 2)), Is.True);
        NativeOleDrag.DragWithin(_process!, new Point(from.Left + Math.Min(30, from.Width / 2), from.Top + from.Height / 2),
            new Point(viewport.Left + 90, viewport.Bottom - 8), true, () =>
                WaitFor(() => FirstVisible() is { } first && first.Current.Name != _path &&
                    (source.Current.IsOffscreen || source.Current.BoundingRectangle.Bottom <= viewport.Top) ? tree : null,
                    "The real captured pointer must scroll the first source out of the viewport before Escape."));
        var stopped = FirstVisible() ?? throw new InvalidOperationException("The tree must retain visible sources after Escape.");
        var name = stopped.Current.Name;
        var top = stopped.Current.BoundingRectangle.Top;
        Thread.Sleep(350); // Observe more than three production refresh periods after cancellation.
        var after = FirstVisible() ?? throw new InvalidOperationException("The tree must remain populated after cancellation.");
        Assert.That(after.Current.Name, Is.EqualTo(name));
        Assert.That(after.Current.BoundingRectangle.Top, Is.EqualTo(top));
        var roots = tree.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Cast<AutomationElement>().ToArray();
        Assert.That(roots.Select(item => item.Current.Name), Is.EqualTo(paths));
        foreach (var path in paths) Assert.That(File.ReadAllText(path), Is.EqualTo(content));

        AutomationElement? FirstVisible() => tree.FindAll(TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem))
            .Cast<AutomationElement>().Where(item => !item.Current.IsOffscreen && item.Current.BoundingRectangle.Bottom > viewport.Top &&
                item.Current.BoundingRectangle.Top < viewport.Bottom).OrderBy(item => item.Current.BoundingRectangle.Top).FirstOrDefault();
    }

    private void SendSecondary(params string[] paths)
    {
        var start = new ProcessStartInfo(_process!.StartInfo.FileName) { UseShellExecute = false, WorkingDirectory = _folder };
        foreach (var path in paths) start.ArgumentList.Add(path);
        using var secondary = Process.Start(start) ?? throw new InvalidOperationException("The owned secondary process did not start.");
        try
        {
            Assert.That(secondary.WaitForExit(10000), Is.True, "The secondary must acknowledge handoff and exit.");
            Assert.That(secondary.ExitCode, Is.EqualTo(0));
        }
        finally
        {
            if (!secondary.HasExited)
            {
                secondary.Kill(entireProcessTree: true);
                Assert.That(secondary.WaitForExit(5000), Is.True, "The owned secondary must exit during cleanup.");
            }
        }
    }

    private static void SelectSecondLine(AutomationElement window)
    {
        var rows = WaitFor(() => window.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Log lines")), "The app must expose its log list.");
        var item = WaitFor(() =>
        {
            var items = rows.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
            if (items.Count < 3) return null;
            return items.Cast<AutomationElement>().FirstOrDefault(row => row.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text)).Cast<AutomationElement>()
                .Any(text => text.Current.Name.TrimStart().StartsWith("2  ", StringComparison.Ordinal)));
        }, "The owned three-line file must become readable.");
        ((SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
    }

    private void CopyAndCheck(AutomationElement window, string expected = SelectedLine)
    {
        _ownedCopies.Add(expected);
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
