using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using NUnit.Framework;

namespace Tailviewer.Acceptance.Tests.Migration;

internal static class NativeOleDrag
{
    public static DragDropEffects Drop(DataObject data, AutomationElement window, Process process)
    {
        if (new[] { 1, 2, 4, 16, 17, 18 }.Any(key => (GetAsyncKeyState(key) & 0x8000) != 0))
            throw new InvalidOperationException("Native drag tests require released mouse buttons and modifier keys.");
        if (!GetPhysicalCursorPos(out var original)) throw new Win32Exception();
        SetForegroundWindow(process.MainWindowHandle);
        var bounds = window.Current.BoundingRectangle;
        var target = new ScreenPoint { X = (int)(bounds.Left + bounds.Width * .75), Y = (int)(bounds.Top + bounds.Height * .6) };
        if (!OwnsPoint(process.Id, target)) throw new InvalidOperationException("The owned drop target is occluded.");
        var button = new Button { Content = "Owned OLE source" };
        var source = new Window
        {
            Content = button, Width = 160, Height = 100, Left = SystemParameters.WorkArea.Left,
            Top = SystemParameters.WorkArea.Top, Topmost = true, ShowInTaskbar = false,
            WindowStyle = WindowStyle.ToolWindow, Title = "Tailviewer native test source"
        };
        var frame = new DispatcherFrame();
        using var feedback = new ManualResetEventSlim();
        using var started = new ManualResetEventSlim();
        Exception? failure = null;
        var effect = DragDropEffects.None;
        var completed = false;
        var feedbackCount = 0;
        var queryCount = 0;
        var thread = GetCurrentThreadId();
        var lastPoint = original;
        var moved = false;
        button.GiveFeedback += (_, _) => { ++feedbackCount; feedback.Set(); };
        button.QueryContinueDrag += (_, _) => ++queryCount;
        button.PreviewMouseLeftButtonDown += (_, args) =>
        {
            args.Handled = true;
            started.Set();
            try { effect = System.Windows.DragDrop.DoDragDrop(button, data, DragDropEffects.Copy | DragDropEffects.Link); }
            catch (Exception error) { Interlocked.CompareExchange(ref failure, error, null); }
            finally { completed = true; frame.Continue = false; }
        };
        try
        {
            source.Show();
            source.Activate();
            source.UpdateLayout();
            var position = button.PointToScreen(new Point(button.ActualWidth / 2, button.ActualHeight / 2));
            var start = new ScreenPoint { X = (int)position.X, Y = (int)position.Y };
            var input = Task.Run(() =>
            {
                var pressed = false;
                try
                {
                    if (!OwnsPoint(Environment.ProcessId, start) || !OwnsPoint(process.Id, target))
                        throw new InvalidOperationException("The owned source/target is occluded before dragging.");
                    Move(start);
                    Mouse(2);
                    pressed = true;
                    if (!started.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("The owned source did not receive mouse down.");
                    // Jump directly between owned windows instead of dragging over unrelated applications.
                    Move(target);
                    if (!feedback.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("The owned source did not start OLE feedback.");
                    Thread.Sleep(20);
                    if (!OwnsPoint(process.Id, target)) throw new InvalidOperationException("The drop target changed during the drag.");
                    Mouse(4);
                    pressed = false;
                }
                catch (Exception error)
                {
                    Interlocked.CompareExchange(ref failure, error, null);
                    // Address the owned STA queue even if native capture has not started yet.
                    PostThreadMessage(thread, 0x100, (UIntPtr)0x1b, (IntPtr)1);
                    PostThreadMessage(thread, 0x101, (UIntPtr)0x1b, IntPtr.Zero);
                }
                finally
                {
                    try { if (pressed) Mouse(4); }
                    finally { source.Dispatcher.BeginInvoke(() => frame.Continue = false); }
                }
            });
            Dispatcher.PushFrame(frame);
            if (!input.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("The owned input worker did not finish.");
            if (failure != null) throw new InvalidOperationException($"Native drag failed (completed={completed}, feedback={feedbackCount}, query={queryCount}).", failure);
            Assert.That(completed, Is.True, "The source drag must return before the test ends.");
            Assert.That(feedbackCount, Is.GreaterThan(0));
            Assert.That(queryCount, Is.GreaterThan(0));
            TestContext.WriteLine("OLE feedback={0}, query={1}, effect={2}", feedbackCount, queryCount, effect);
            return effect;
        }
        finally
        {
            source.Close();
            if (moved && GetPhysicalCursorPos(out var current) && current.X == lastPoint.X && current.Y == lastPoint.Y)
                SetPhysicalCursorPos(original.X, original.Y);
        }

        void Move(ScreenPoint point)
        {
            if (!SetPhysicalCursorPos(point.X, point.Y)) throw new Win32Exception();
            lastPoint = point;
            moved = true;
        }
    }

    public static void DragWithin(Process process, Point from, Point to, bool cancel, Action? whileHeld = null)
    {
        if (new[] { 1, 2, 4, 5, 6, 16, 17, 18, 91, 92 }.Any(key => (GetAsyncKeyState(key) & 0x8000) != 0))
            throw new InvalidOperationException("Native pointer tests require released mouse buttons and modifiers.");
        if (!GetPhysicalCursorPos(out var original)) throw new Win32Exception();
        var start = new ScreenPoint { X = (int)from.X, Y = (int)from.Y };
        var end = new ScreenPoint { X = (int)to.X, Y = (int)to.Y };
        var last = original;
        var moved = false;
        var pressed = false;
        try
        {
            SetForegroundWindow(process.MainWindowHandle);
            TestContext.WriteLine("Owned pointer window DPI: {0}", GetDpiForWindow(process.MainWindowHandle));
            Check(start);
            Check(end);
            Move(start);
            Check(start);
            Mouse(2);
            pressed = true;
            Thread.Sleep(50); // Let the owned window process mouse down before threshold crossing.
            Check(end);
            Move(end);
            Thread.Sleep(50); // Native pointer capture/preview must settle before release.
            Check(end);
            if (!GetPhysicalCursorPos(out var current) || current.X != end.X || current.Y != end.Y)
                throw new InvalidOperationException("The cursor changed externally during the owned drag.");
            whileHeld?.Invoke();
            if (cancel)
            {
                if (!PostMessage(process.MainWindowHandle, 0x100, (UIntPtr)0x1b, (IntPtr)1) ||
                    !PostMessage(process.MainWindowHandle, 0x101, (UIntPtr)0x1b, IntPtr.Zero)) throw new Win32Exception();
                Thread.Sleep(50);
            }
            Check(end);
            if (!GetPhysicalCursorPos(out current) || current.X != end.X || current.Y != end.Y)
                throw new InvalidOperationException("The cursor changed externally before release.");
            Mouse(4);
            pressed = false;
        }
        finally
        {
            try { if (pressed) Mouse(4); }
            finally
            {
                if (moved && GetPhysicalCursorPos(out var current) && current.X == last.X && current.Y == last.Y)
                    SetPhysicalCursorPos(original.X, original.Y);
            }
        }

        void Check(ScreenPoint point)
        {
            if (!OwnsPoint(process.Id, point)) throw new InvalidOperationException("The owned drag point is occluded.");
        }
        void Move(ScreenPoint point)
        {
            if (!SetPhysicalCursorPos(point.X, point.Y)) throw new Win32Exception();
            last = point;
            moved = true;
        }
    }

    private static bool OwnsPoint(int process, ScreenPoint point)
    {
        GetWindowThreadProcessId(WindowFromPhysicalPoint(point), out var owner);
        return owner == process;
    }

    private static void Mouse(uint flags)
    {
        var input = new Input { Mouse = new MouseInput { Flags = flags } };
        if (SendInput(1, new[] { input }, Marshal.SizeOf<Input>()) != 1) throw new Win32Exception();
    }

    [StructLayout(LayoutKind.Sequential)] private struct ScreenPoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetPhysicalCursorPos(out ScreenPoint point);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetPhysicalCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPhysicalPoint(ScreenPoint point);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostThreadMessage(uint thread, uint message, UIntPtr w, IntPtr l);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(IntPtr window, uint message, UIntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
}
