using Tailviewer.BusinessLogic.FileExplorer;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Tailviewer.Avalonia;

internal sealed class WindowsFileExplorer : IFileExplorer
{
    private readonly Func<IntPtr, uint, IntPtr[], uint, int>? _select;
    public WindowsFileExplorer() { }
    internal WindowsFileExplorer(Func<IntPtr, uint, IntPtr[], uint, int> select) => _select = select ?? throw new ArgumentNullException(nameof(select));
    public void SelectFile(string filePath)
    {
        var path = FullPath(filePath);
        SelectFiles(Path.GetDirectoryName(path) ?? path, path);
    }

    public void OpenFolder(string folderPath)
    {
        var folder = FullPath(folderPath);
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("The containing folder is unavailable.");
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows Explorer is supported on Windows.");
        using var process = Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    internal static string[] ValidateChildren(string folderPath, IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var folder = FullPath(folderPath);
        var children = files.Select(FullPath).Where(path => !string.Equals(path, folder, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (children.Any(path => !string.Equals(Path.GetDirectoryName(path), folder, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Selected items must be immediate children of the containing folder.", nameof(files));
        return children;
    }

    public void SelectFiles(string folder, params string[] filesToSelect)
    {
        folder = FullPath(folder);
        var children = ValidateChildren(folder, filesToSelect).Where(path => File.Exists(path) || Directory.Exists(path)).ToArray();
        if (children.Length == 0) { OpenFolder(folder); return; }
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows Explorer is supported on Windows.");
        var initialized = CoInitializeEx(IntPtr.Zero, 0);
        if (initialized < 0 && initialized != unchecked((int)0x80010106)) Marshal.ThrowExceptionForHR(initialized);
        var allocated = new List<IntPtr>();
        try
        {
            var parent = Parse(folder);
            var relative = children.Select(path => ILFindLastID(Parse(path))).ToArray();
            if (relative.Any(pointer => pointer == IntPtr.Zero)) throw new IOException("Cannot resolve the selected filesystem items.");
            Marshal.ThrowExceptionForHR(_select != null ? _select(parent, (uint)relative.Length, relative, 0) :
                SHOpenFolderAndSelectItems(parent, (uint)relative.Length, relative, 0));
        }
        finally
        {
            foreach (var pointer in allocated) Marshal.FreeCoTaskMem(pointer);
            if (initialized >= 0) CoUninitialize();
        }

        IntPtr Parse(string path)
        {
            Marshal.ThrowExceptionForHR(SHParseDisplayName(path, IntPtr.Zero, out var pointer, 0, out _));
            if (pointer == IntPtr.Zero) throw new IOException("Cannot resolve the filesystem path.");
            allocated.Add(pointer);
            return pointer;
        }
    }

    private static string FullPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    [DllImport("ole32.dll", ExactSpelling = true)] private static extern int CoInitializeEx(IntPtr reserved, uint flags);
    [DllImport("ole32.dll", ExactSpelling = true)] private static extern void CoUninitialize();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SHParseDisplayName(string path, IntPtr context, out IntPtr item, uint attributes, out uint resultAttributes);
    [DllImport("shell32.dll", ExactSpelling = true)] private static extern IntPtr ILFindLastID(IntPtr item);
    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHOpenFolderAndSelectItems(IntPtr folder, uint count, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] items, uint flags);
}
