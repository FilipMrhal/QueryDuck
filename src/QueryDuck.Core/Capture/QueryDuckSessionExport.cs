using System.Text.Json;

namespace QueryDuck.Core.Capture;

public sealed record QueryDuckSessionExport(
    DateTimeOffset ExportedAt,
    int EventCount,
    IReadOnlyList<QueryCaptureEvent> Events,
    IReadOnlyList<string> SessionWarnings,
    QueryDuckSessionSnapshot? Snapshot = null);

public static class QueryDuckSessionExportService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static QueryDuckSessionExport Export(QueryCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var events = QueryDuckCapture.LastCommands;
        return new QueryDuckSessionExport(
            DateTimeOffset.UtcNow,
            events.Count,
            events,
            QueryDuckSession.Warnings,
            QueryDuckSessionSnapshot.Capture(events, options));
    }

    public static string ExportJson(QueryCaptureOptions options) =>
        JsonSerializer.Serialize(Export(options), SerializerOptions);

    public static int Import(QueryDuckSessionExport export, bool append = false)
    {
        ArgumentNullException.ThrowIfNull(export);
        if (!append)
        {
            QueryDuckCapture.Clear();
        }

        foreach (var captureEvent in export.Events)
        {
            QueryDuckCapture.Record(captureEvent);
        }

        foreach (var warning in export.SessionWarnings)
        {
            QueryDuckSession.AddWarning(warning);
        }

        return export.Events.Count;
    }

    public static int ImportJson(string json, bool append = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var export = JsonSerializer.Deserialize<QueryDuckSessionExport>(json, SerializerOptions)
            ?? throw new InvalidOperationException("Invalid session export payload.");
        return Import(export, append);
    }
}
