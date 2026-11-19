using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace Tailviewer.Acceptance.Tests.Migration;

internal static class NativeKeyboard
{
    public static void SelectAll(Process process, AutomationElement list)
    {
        if (IntPtr.Size != 8 || Marshal.SizeOf<Input>() != 40)
            throw new PlatformNotSupportedException("This native keyboard fixture requires Windows x64 INPUT layout.");
        var inputKeys = new[] { 1, 2, 4, 5, 6, 16, 17, 18, 65, 91, 92 };
        if (inputKeys.Any(key => (GetAsyncKeyState(key) & 0x8000) != 0))
            throw new InvalidOperationException("Release mouse buttons, modifiers and A before the owned keyboard fixture.");
        if (list.Current.ProcessId != process.Id) throw new InvalidOperationException("The log list is not owned by the test process.");
        list.SetFocus();
        GetWindowThreadProcessId(GetForegroundWindow(), out var foreground);
        if (foreground != process.Id) throw new InvalidOperationException("The owned app must have foreground focus before keyboard input.");
        var focused = AutomationElement.FocusedElement;
        for (var depth = 0; depth < 16 && focused != null; ++depth)
        {
            if (Automation.Compare(focused, list))
            {
                GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
                if (foreground != process.Id || inputKeys.Any(key => (GetAsyncKeyState(key) & 0x8000) != 0))
                    throw new InvalidOperationException("Focus or input state changed before the owned chord.");
                var sent = SendInput(4, new[] { Key(17, 0), Key(65, 0), Key(65, 2), Key(17, 2) }, 40);
                if (sent == 4) return;
                // Release only keys whose down event this fixture actually injected.
                if (sent >= 2) SendInput(1, new[] { Key(65, 2) }, 40);
                if (sent >= 1) SendInput(1, new[] { Key(17, 2) }, 40);
                throw new Win32Exception("The owned select-all chord was not fully injected.");
            }
            focused = TreeWalker.ControlViewWalker.GetParent(focused);
        }
        throw new InvalidOperationException("Keyboard focus is outside the owned log list.");
    }

    private static Input Key(ushort key, uint flags) => new() { Type = 1, Key = key, Flags = flags };
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public ushort Key;
        [FieldOffset(12)] public uint Flags;
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
}
