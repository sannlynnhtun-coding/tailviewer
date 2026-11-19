using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

[assembly: InternalsVisibleTo("Tailviewer.Avalonia.Tests")]

namespace Tailviewer.Avalonia;

internal static class SingleApplicationMessage
{
    internal const int MaximumBytes = 256 * 1024;
    internal const int MaximumPaths = 256;
    private sealed record Request(int Version, string[] Paths);
    private static readonly JsonSerializerOptions Options = new()
    {
        MaxDepth = 4,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static async Task WriteAsync(Stream stream, IEnumerable<string> paths, CancellationToken cancellationToken)
    {
        var files = paths.Take(MaximumPaths + 1).ToArray();
        Validate(files, requireAbsolute: false);
        files = files.Select(Path.GetFullPath).ToArray();
        Validate(files, requireAbsolute: true);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Request(1, files), Options);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("The source request is too large.");
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<string[]> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > MaximumBytes) throw new InvalidDataException("Invalid source request length.");
        var bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        Request request;
        try
        {
            request = JsonSerializer.Deserialize<Request>(bytes, Options) ?? throw new JsonException("Missing request.");
        }
        catch (JsonException error) { throw new InvalidDataException("Invalid source request.", error); }
        if (request.Version != 1) throw new InvalidDataException("Unsupported source request version.");
        Validate(request.Paths, requireAbsolute: true);
        return request.Paths;
    }

    private static void Validate(string[]? paths, bool requireAbsolute)
    {
        if (paths == null || paths.Length > MaximumPaths) throw new InvalidDataException("Invalid source count.");
        var characters = 0;
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 32767 || path.Contains('\0') ||
                (requireAbsolute && !Path.IsPathFullyQualified(path)))
                throw new InvalidDataException("Invalid source path.");
            characters += path.Length;
            if (characters > MaximumBytes) throw new InvalidDataException("The source request is too large.");
        }
    }
}
