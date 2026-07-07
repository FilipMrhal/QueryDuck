namespace QueryDuck.Core.Capture;

public sealed record QueryDuckTraceGroup(
    string TraceKey,
    string? TraceId,
    string? CorrelationId,
    string? RequestPath,
    int EventCount,
    int SlowQueryCount,
    int FailureCount,
    double TotalDurationMs,
    IReadOnlyList<string> EventIds);

public sealed record QueryDuckTraceGrouping(
    int TotalEvents,
    int GroupCount,
    IReadOnlyList<QueryDuckTraceGroup> Groups);

public static class QueryDuckTraceGroupingBuilder
{
    public static QueryDuckTraceGrouping Build(IEnumerable<QueryCaptureEvent> events, int slowQueryThresholdMs = 500)
    {
        ArgumentNullException.ThrowIfNull(events);
        var materialized = events.ToArray();
        var groups = materialized
            .GroupBy(ResolveTraceKey)
            .Select(g =>
            {
                var durations = g.Select(e => e.Duration.TotalMilliseconds).ToArray();
                var first = g.First();
                return new QueryDuckTraceGroup(
                    g.Key,
                    first.TraceId,
                    first.CorrelationId,
                    first.RequestPath,
                    g.Count(),
                    g.Count(e => e.Duration.TotalMilliseconds >= slowQueryThresholdMs),
                    g.Count(e => !e.Succeeded),
                    durations.Sum(),
                    g.Select(e => e.EventId).ToArray());
            })
            .OrderByDescending(g => g.TotalDurationMs)
            .ThenByDescending(g => g.EventCount)
            .ToArray();

        return new QueryDuckTraceGrouping(materialized.Length, groups.Length, groups);
    }

    private static string ResolveTraceKey(QueryCaptureEvent captureEvent)
    {
        if (!string.IsNullOrWhiteSpace(captureEvent.TraceId))
        {
            return $"trace:{captureEvent.TraceId}";
        }

        if (!string.IsNullOrWhiteSpace(captureEvent.CorrelationId))
        {
            return $"correlation:{captureEvent.CorrelationId}";
        }

        if (!string.IsNullOrWhiteSpace(captureEvent.RequestPath))
        {
            return $"request:{captureEvent.RequestPath}";
        }

        return "ungrouped";
    }
}
