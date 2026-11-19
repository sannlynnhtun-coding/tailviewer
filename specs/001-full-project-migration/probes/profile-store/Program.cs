using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using Tailviewer.Settings;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "bundle-child")
        {
            ProfileBundleStore.Publish(args[1], XDocument.Parse("<xml value='new'/>"),
                XDocument.Parse("<xml value='new'/>"), true, CancellationToken.None,
                index =>
                {
                    if (index == 0 && args[2] == "partial") Pause("partial");
                    if (index == 1 && args[2] == "uncommitted") Pause("uncommitted");
                });
            if (args[2] == "committed") Pause("committed");
            return 0;
        }
        if (args.Length == 3 && args[0] == "child")
        {
            ProfileStore.Write(args[1], writer => WriteValue(writer, "new"), document =>
            {
                if (args[2] == "before") Pause("staged");
            });
            if (args[2] == "after") Pause("committed");
            return 0;
        }
        if (args.Length != 0) throw new ArgumentException("Run without arguments for the interruption probe.");
        var parent = Path.GetFullPath(Path.GetTempPath());
        var folder = Path.Combine(parent, "tailviewer-store-interruption-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            foreach (var phase in new[] { "before", "after" })
            {
                var path = Path.Combine(folder, phase + ".xml");
                File.WriteAllText(path, "<xml value='old'/>");
                var start = new ProcessStartInfo("dotnet")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (var argument in new[] { Assembly.GetExecutingAssembly().Location, "child", path, phase })
                    start.ArgumentList.Add(argument);
                using (var child = Process.Start(start) ?? throw new IOException("Could not start owned probe child."))
                {
                    try
                    {
                        var signal = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
                        Check(signal == (phase == "before" ? "staged" : "committed"), "Child did not reach the required file boundary.");
                        var candidates = Directory.GetFiles(folder, "." + phase + ".xml.*.tmp", SearchOption.AllDirectories);
                        Check(candidates.Length == (phase == "before" ? 1 : 0), "Unexpected candidate count.");
                        Check(ReadValue(path) == (phase == "before" ? "old" : "new"), "Unexpected destination before termination.");
                        if (phase == "before") Check(ReadValue(candidates[0]) == "new", "Staged file was not validated.");
                        child.Kill(entireProcessTree: true);
                        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                        Check(ReadValue(path) == (phase == "before" ? "old" : "new"), "Termination damaged the destination.");
                        ProfileStore.Write(path, writer => WriteValue(writer, "recovered"), document => { });
                        Check(ReadValue(path) == "recovered", "Subsequent save did not recover.");
                        if (phase == "before") Check(ReadValue(candidates[0]) == "new", "Recovery modified an abandoned candidate.");
                        Console.WriteLine("PASS: terminated " + phase + " promotion; destination valid; next save succeeded.");
                    }
                    finally
                    {
                        if (!child.HasExited)
                        {
                            child.Kill(entireProcessTree: true);
                            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                        }
                    }
                }
            }
            await CheckBundleInterruption(folder);
            return 0;
        }
        finally
        {
            Check(string.Equals(Path.GetDirectoryName(Path.GetFullPath(folder)), Path.TrimEndingDirectorySeparator(parent), StringComparison.OrdinalIgnoreCase), "Cleanup path escaped owned temporary parent.");
            Directory.Delete(folder, true);
        }
    }

    private static async Task CheckBundleInterruption(string parent)
    {
        foreach (var phase in new[] { "partial", "uncommitted", "committed" })
        {
            var folder = Path.Combine(parent, phase);
            Directory.CreateDirectory(folder);
            var settings = Path.Combine(folder, "settings.xml");
            var bookmarks = Path.Combine(folder, "bookmarks.xml");
            File.WriteAllText(settings, "<xml value='old'/>");
            File.WriteAllText(bookmarks, "<xml value='old'/>");
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var argument in new[] { Assembly.GetExecutingAssembly().Location, "bundle-child", settings, phase })
                start.ArgumentList.Add(argument);
            using (var child = Process.Start(start) ?? throw new IOException("Could not start owned bundle child."))
            {
                try
                {
                    var signal = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    Check(signal == phase, "Bundle child did not reach the required publication boundary.");
                    Check(ReadValue(settings) == "new", "First member was not published.");
                    Check(ReadValue(bookmarks) == (phase == "partial" ? "old" : "new"), "Unexpected second member.");
                    child.Kill(entireProcessTree: true);
                    await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    var expected = phase == "committed" ? "new" : "old";
                    Check((string)ProfileStore.Read(settings).Root.Attribute("value") == expected, "Settings recovery failed.");
                    Check((string)ProfileStore.Read(bookmarks).Root.Attribute("value") == expected, "Bookmark recovery failed.");
                    Check(!File.Exists(Path.Combine(folder, ".tailviewer-import.xml")), "Undo journal was not retired.");
                    Console.WriteLine("PASS: terminated bundle " + phase + "; both members recovered consistently.");
                }
                finally
                {
                    if (!child.HasExited)
                    {
                        child.Kill(entireProcessTree: true);
                        await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                    }
                }
            }
        }
    }

    // Observe physical files while the child deliberately holds the production folder lock.
    private static string ReadValue(string path) => (string)ProfileStore.ReadCore(path).Root.Attribute("value");
    private static void Pause(string signal)
    {
        Console.WriteLine(signal);
        Console.Out.Flush();
        Console.ReadLine();
    }
    private static void WriteValue(XmlWriter writer, string value)
    {
        writer.WriteStartElement("xml");
        writer.WriteAttributeString("value", value);
        writer.WriteEndElement();
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
