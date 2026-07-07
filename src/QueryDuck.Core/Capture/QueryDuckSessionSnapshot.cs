namespace QueryDuck.Core.Capture;

public sealed record QueryDuckSessionSnapshot(
    DateTimeOffset CapturedAt,
    int EventCount,
    int SlowQueryCount,
    int FailureCount,
    int DiagnosticWarningCount,
    IReadOnlyDictionary<string, int> EventsByProvider,
    IReadOnlyDictionary<string, int> DiagnosticsByRule,
    IReadOnlyList<string> SessionWarnings)
{
    public static QueryDuckSessionSnapshot Capture(IEnumerable<QueryCaptureEvent> events, QueryCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(options);

        var eventList = events.ToArray();
        var slowThreshold = options.SlowQueryThresholdMs;
        var byProvider = eventList
            .GroupBy(e => e.Provider, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        var byRule = eventList
            .SelectMany(e => e.Diagnostics)
            .GroupBy(d => d.RuleId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return new QueryDuckSessionSnapshot(
            DateTimeOffset.UtcNow,
            eventList.Length,
            eventList.Count(e => slowThreshold > 0 && e.Duration.TotalMilliseconds >= slowThreshold),
            eventList.Count(e => !e.Succeeded),
            eventList.SelectMany(e => e.Diagnostics).Count(d =>
                string.Equals(d.Severity, "Warning", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.Severity, "Error", StringComparison.OrdinalIgnoreCase)),
            byProvider,
            byRule,
            QueryDuckSession.Warnings.ToArray());
    }
}
