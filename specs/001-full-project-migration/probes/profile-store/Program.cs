using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using Tailviewer.Settings;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
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
            return 0;
        }
        finally
        {
            Check(string.Equals(Path.GetDirectoryName(Path.GetFullPath(folder)), Path.TrimEndingDirectorySeparator(parent), StringComparison.OrdinalIgnoreCase), "Cleanup path escaped owned temporary parent.");
            Directory.Delete(folder, true);
        }
    }

    private static string ReadValue(string path) => (string)ProfileStore.Read(path).Root.Attribute("value");
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
