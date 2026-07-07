namespace QueryDuck.Core.Capture;

public enum QueryDuckTimelineEntryKind
{
    Query,
    SaveChanges,
    Failure,
}

public sealed record QueryDuckTimelineEntry(
    DateTimeOffset Timestamp,
    QueryDuckTimelineEntryKind Kind,
    string Label,
    double? DurationMs = null,
    string? EventId = null,
    string? TraceId = null,
    string? CorrelationId = null,
    string? RequestPath = null);

public static class QueryDuckTransactionTimeline
{
    private static readonly Lock Gate = new();
    private static readonly List<QueryDuckTimelineEntry> Entries = [];

    public static IReadOnlyList<QueryDuckTimelineEntry> Snapshot()
    {
        lock (Gate)
        {
            return Entries.ToArray();
        }
    }

    public static void RecordQuery(QueryCaptureEvent captureEvent)
    {
        ArgumentNullException.ThrowIfNull(captureEvent);
        lock (Gate)
        {
            Entries.Add(new QueryDuckTimelineEntry(
                captureEvent.Timestamp,
                captureEvent.Succeeded ? QueryDuckTimelineEntryKind.Query : QueryDuckTimelineEntryKind.Failure,
                PreviewSql(captureEvent.Sql),
                captureEvent.Duration.TotalMilliseconds,
                captureEvent.EventId,
                captureEvent.TraceId,
                captureEvent.CorrelationId,
                captureEvent.RequestPath));
        }
    }

    public static void RecordSaveChanges(int callCount)
    {
        lock (Gate)
        {
            Entries.Add(new QueryDuckTimelineEntry(
                DateTimeOffset.UtcNow,
                QueryDuckTimelineEntryKind.SaveChanges,
                $"SaveChanges #{callCount}",
                null,
                null,
                null,
                null,
                null));
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Entries.Clear();
        }
    }

    private static string PreviewSql(string sql)
    {
        var line = sql.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? sql;
        return line.Length <= 80 ? line : line[..77] + "...";
    }
}
