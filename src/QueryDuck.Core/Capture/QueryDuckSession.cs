namespace QueryDuck.Core.Capture;

public static class QueryDuckSession
{
    private static readonly object Gate = new();
    private static string[] _warnings = [];

    public static IReadOnlyList<string> Warnings
    {
        get
        {
            lock (Gate)
            {
                return _warnings;
            }
        }
    }

    public static void Refresh(IEnumerable<QueryCaptureEvent> events, QueryCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(options);

        var warnings = new List<string>();

        if (options.DetectNPlusOne)
        {
            warnings.AddRange(QueryCaptureHeuristics.DetectNPlusOne(events, options.NPlusOneThreshold));
        }

        if (options.SlowQueryThresholdMs > 0)
        {
            warnings.AddRange(QueryCaptureHeuristics.DetectSlowQueries(events, options.SlowQueryThresholdMs));
        }

        lock (Gate)
        {
            _warnings = warnings.Distinct(StringComparer.Ordinal).ToArray();
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            _warnings = [];
        }
    }
}
