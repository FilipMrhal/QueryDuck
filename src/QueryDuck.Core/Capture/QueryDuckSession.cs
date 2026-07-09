namespace QueryDuck.Core.Capture;

public static class QueryDuckSession
{
    private static readonly object Gate = new();
    private static string[] _warnings = [];
    private static readonly List<string> _customWarnings = [];

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
            _warnings = warnings.Concat(_customWarnings).Distinct(StringComparer.Ordinal).ToArray();
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            _warnings = [];
            _customWarnings.Clear();
        }
    }

    public static void AddWarning(string warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
        {
            return;
        }

        lock (Gate)
        {
            _customWarnings.Add(warning);
            _warnings = _warnings.Concat([warning]).Distinct(StringComparer.Ordinal).ToArray();
        }
    }
}
