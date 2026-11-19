using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tailviewer.Avalonia;

internal sealed record ObservedLog(string File, long Line, string Message, DateTimeOffset? Timestamp,
    string? Level, string? Service, string? Environment, string? Host, string? Trace, int? Status,
    double? Latency, string? Method, string? Url, JsonElement? Payload)
{
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

        var timestamp = Field("@t", "timestamp", "Timestamp", "time");
        if (timestamp == null)
        {
            var prefix = Regex.Match(text, @"^\[?(\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:[.,]\d+)?(?:Z|[+-]\d{2}:?\d{2})?)");
            if (prefix.Success) timestamp = prefix.Groups[1].Value;
        }

        var level = Field("@l", "level", "Level")?.ToUpperInvariant() ?? (Field("@t") != null ? "INFO" : null);
        level = level switch { "INFORMATION" => "INFO", "WARNING" => "WARN", "VERBOSE" => "TRACE", _ => level };
        return new(file, line, Field("@m", "@mt", "message", "Message") ?? text,
            DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var time) ? time : null,
            level, Field("service", "Service", "service.name"), Field("environment", "env", "Environment"),
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
