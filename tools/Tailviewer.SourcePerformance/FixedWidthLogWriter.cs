using System;
using System.IO;
using System.Linq;

internal static class FixedWidthLogWriter
{
    public const int RecordWidth = 128;

    public static void Write(string path, int sizeMiB)
    {
        var block = Enumerable.Repeat((byte)'x', 128 * 1024).ToArray();
        for (var index = RecordWidth - 1; index < block.Length; index += RecordWidth) block[index] = (byte)'\n';
        using var output = File.Create(path);
        for (var index = 0; index < sizeMiB * 8; ++index) output.Write(block);
        output.Flush(true);
    }

    public static void ReadThrough(string path)
    {
        var block = new byte[128 * 1024];
        using var input = File.OpenRead(path);
        while (input.Read(block, 0, block.Length) > 0) { }
    }
}
