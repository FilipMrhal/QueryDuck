namespace QueryDuck.Core.Capture;

public sealed record QueryDuckSessionComparison(
    QueryDuckSessionSnapshot Baseline,
    QueryDuckSessionSnapshot Current,
    int EventCountDelta,
    int SlowQueryCountDelta,
    int FailureCountDelta,
    int DiagnosticWarningCountDelta,
    IReadOnlyList<string> NewSessionWarnings,
    IReadOnlyList<string> ResolvedSessionWarnings,
    IReadOnlyDictionary<string, int> ProviderCountDeltas,
    IReadOnlyDictionary<string, int> RuleCountDeltas);

public static class QueryDuckSessionComparer
{
    private static QueryDuckSessionSnapshot? _baseline;

    public static void SetBaseline(QueryDuckSessionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _baseline = snapshot;
    }

    public static QueryDuckSessionSnapshot? Baseline => _baseline;

    public static QueryDuckSessionComparison Compare(
        QueryDuckSessionSnapshot current,
        QueryDuckSessionSnapshot? baseline = null)
    {
        ArgumentNullException.ThrowIfNull(current);
        baseline ??= _baseline ?? throw new InvalidOperationException("No session baseline has been captured.");

        return new QueryDuckSessionComparison(
            baseline,
            current,
            current.EventCount - baseline.EventCount,
            current.SlowQueryCount - baseline.SlowQueryCount,
            current.FailureCount - baseline.FailureCount,
            current.DiagnosticWarningCount - baseline.DiagnosticWarningCount,
            current.SessionWarnings.Except(baseline.SessionWarnings, StringComparer.Ordinal).ToArray(),
            baseline.SessionWarnings.Except(current.SessionWarnings, StringComparer.Ordinal).ToArray(),
            DiffCounts(baseline.EventsByProvider, current.EventsByProvider),
            DiffCounts(baseline.DiagnosticsByRule, current.DiagnosticsByRule));
    }

    public static void ClearBaseline() => _baseline = null;

    private static Dictionary<string, int> DiffCounts(
        IReadOnlyDictionary<string, int> baseline,
        IReadOnlyDictionary<string, int> current)
    {
        var keys = baseline.Keys.Concat(current.Keys).Distinct(StringComparer.OrdinalIgnoreCase);
        var deltas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            baseline.TryGetValue(key, out var left);
            current.TryGetValue(key, out var right);
            var delta = right - left;
            if (delta != 0)
            {
                deltas[key] = delta;
            }
        }

        return deltas;
    }
}
