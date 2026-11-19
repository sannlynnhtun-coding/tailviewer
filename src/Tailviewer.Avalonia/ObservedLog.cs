using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tailviewer.Avalonia;

internal sealed record ObservedLog(string File, long Line, string Message, DateTimeOffset? Timestamp,
    string? Level, string? Service, string? Environment, string? Host, string? Trace, int? Status,
    double? Latency, string? Method, string? Url, JsonElement? Payload)
{
    private static readonly Regex TextHeader = new(
        @"^(?<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{1,7} [+-]\d{2}:\d{2}) \[(?<level>TRC|VRB|DBG|INF|WRN|ERR|FTL)\] ?",
        RegexOptions.CultureInvariant);
    private static readonly Regex RollingFileName = new(
        @"^(?<service>.+)-log-\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-(?:AM|PM)\.log$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static async IAsyncEnumerable<ObservedLog> ReadAsync(StreamReader reader, string file,
        [EnumeratorCancellation] CancellationToken token)
    {
        StringBuilder? entry = null;
        long firstLine = 0;
        long line = 0;
        while (await reader.ReadLineAsync(token) is { } text)
        {
            ++line;
            if (TextHeader.IsMatch(text))
            {
                if (entry != null) yield return Parse(file, firstLine, entry.ToString());
                entry = new StringBuilder(text);
                firstLine = line;
            }
            else if (entry != null) entry.Append('\n').Append(text);
            else yield return Parse(file, line, text);
        }

        if (entry != null) yield return Parse(file, firstLine, entry.ToString());
    }
    public string StatusGroup => Status is >= 100 and <= 599 ? $"{Status / 100}xx" : "None";

    public static ObservedLog Parse(string file, long line, string text)
    {
        JsonElement? payload = null;
        if (text.AsSpan().TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            try
            {
                using var json = JsonDocument.Parse(text);
                payload = json.RootElement.Clone();
            }
            catch (JsonException) { /* Preserve malformed records as raw text. */ }
        }

        string? Field(params string[] names)
        {
            foreach (var name in names)
            {
                var field = payload;
                foreach (var segment in name.Split('.'))
                    field = field is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty(segment, out var value)
                        ? value : null;
                if (field is { ValueKind: JsonValueKind.String or JsonValueKind.Number } scalar)
                    return scalar.ToString();
            }

            return null;
        }

        var header = TextHeader.Match(text);
        var timestamp = header.Success ? header.Groups["timestamp"].Value : Field("@t", "timestamp", "Timestamp", "time");
        if (timestamp == null)
        {
            var prefix = Regex.Match(text, @"^\[?(\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:[.,]\d+)?(?:Z|[+-]\d{2}:?\d{2})?)");
            if (prefix.Success) timestamp = prefix.Groups[1].Value;
        }

        var level = Field("@l", "level", "Level")?.ToUpperInvariant() ?? (Field("@t") != null ? "INFO" : null);
        if (header.Success) level = header.Groups["level"].Value;
        level = level switch
        {
            "INF" or "INFORMATION" => "INFO", "WRN" or "WARNING" => "WARN",
            "ERR" => "ERROR", "DBG" => "DEBUG", "FTL" => "FATAL",
            "TRC" or "VRB" or "VERBOSE" => "TRACE", _ => level
        };
        var service = Field("service", "Service", "service.name");
        if (service == null && header.Success)
        {
            var name = RollingFileName.Match(Path.GetFileName(file));
            if (name.Success) service = name.Groups["service"].Value;
        }
        var message = header.Success ? text[header.Length..] : Field("@m", "@mt", "message", "Message") ?? text;
        return new(file, line, message,
            DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var time) ? time : null,
            level, service, Field("environment", "env", "Environment"),
            Field("host", "node", "Host"), Field("@tr", "traceId", "trace_id", "TraceId"),
            int.TryParse(Field("status", "status_code", "http.status", "http.status_code"), CultureInfo.InvariantCulture, out var status) ? status : null,
            double.TryParse(Field("latency", "latency_ms", "elapsed_ms", "http.latency_ms"), CultureInfo.InvariantCulture, out var latency) && double.IsFinite(latency) ? latency : null,
            Field("method", "http.method"), Field("url", "http.url", "url.full"), payload);
    }
}

internal sealed class FolderFileTypes
{
    private readonly HashSet<string> _extensions;

    public FolderFileTypes(string selection)
    {
        _extensions = selection.Split([';', ',', ' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value == "*" ? "" : value.StartsWith("*.") ? value[1..] : value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (_extensions.Count == 0 || _extensions.Any(value => value.Length != 0 &&
                !Regex.IsMatch(value, @"^\.[\p{L}\p{N}_-]+$")))
            throw new ArgumentException("Enter extensions such as .log; .txt, or * for all files.");
    }

    public string Pattern => string.Join(";", _extensions.Select(value => "ext:" + (value.Length == 0 ? "*" : value)));
}
