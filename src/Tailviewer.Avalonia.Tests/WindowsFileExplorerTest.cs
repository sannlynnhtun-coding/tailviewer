using NUnit.Framework;
using System.Runtime.InteropServices;

namespace Tailviewer.Avalonia.Tests;

[TestFixture]
public sealed class WindowsFileExplorerTest
{
    [Test]
    public void WindowsSelectionBuildsNativeChildIdsAndPropagatesShellErrorsWithoutOpeningExplorer()
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("Windows shell interop requires Windows.");
        var folder = Path.Combine(Path.GetTempPath(), "tailviewer-shell-" + Guid.NewGuid().ToString("N") + " မြန်မာ, folder");
        Directory.CreateDirectory(folder);
        try
        {
            var paths = new[] { Path.Combine(folder, "first, မြန်မာ.log"), Path.Combine(folder, "second.log") };
            foreach (var path in paths) File.WriteAllText(path, "owned contents");
            var fail = true;
            var calls = 0;
            var explorer = new WindowsFileExplorer((parent, count, children, flags) =>
            {
                ++calls;
                Assert.That(parent, Is.Not.EqualTo(IntPtr.Zero));
                Assert.That(count, Is.EqualTo(2));
                Assert.That(flags, Is.Zero);
                Assert.That(children, Has.Length.EqualTo(2));
                foreach (var child in children) Assert.That(Marshal.ReadInt16(child), Is.GreaterThan(0));
                return fail ? unchecked((int)0x80004005) : 0;
            });
            Assert.Throws<COMException>(() => explorer.SelectFiles(folder, paths));
            fail = false;
            Assert.DoesNotThrow(() => explorer.SelectFiles(folder, paths));
            Assert.That(calls, Is.EqualTo(2));
            foreach (var path in paths) Assert.That(File.ReadAllText(path), Is.EqualTo("owned contents"));
        }
        finally
        {
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(folder)), Is.EqualTo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))));
            Directory.Delete(folder, recursive: true);
        }
    }

    [Test]
    public void ChildSelectionPreservesUnicodeAndCommasAndRejectsOtherDirectories()
    {
        var folder = Path.Combine(Path.GetTempPath(), "tailviewer-explorer-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(folder, "မြန်မာ, selected.log");
        Assert.That(WindowsFileExplorer.ValidateChildren(folder, new[] { file, file, folder }), Is.EqualTo(new[] { file }));
        Assert.Throws<ArgumentException>(() => WindowsFileExplorer.ValidateChildren(folder, new[] { Path.Combine(folder, "..", "outside.log") }));
        Assert.Throws<ArgumentException>(() => WindowsFileExplorer.ValidateChildren(folder, new[] { "bad\0path" }));
        Assert.Throws<ArgumentNullException>(() => WindowsFileExplorer.ValidateChildren(folder, null!));
    }
}
