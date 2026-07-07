using QueryDuck.Core.Capture;
using QueryDuck.Core.Diagnostics;

namespace QueryDuck.Testing.Assertions;

public static class QueryDuckCaptureAssert
{
    public static void ShouldExecuteAtMost(int maxCount)
    {
        var count = QueryDuckCapture.LastCommands.Count;
        if (count > maxCount)
        {
            throw new InvalidOperationException(
                $"Expected at most {maxCount} captured queries, but found {count}.");
        }
    }

    public static void ShouldNotTriggerNPlusOne(int threshold = 5)
    {
        var warnings = QueryCaptureHeuristics.DetectNPlusOne(QueryDuckCapture.LastCommands, threshold);
        if (warnings.Count > 0)
        {
            throw new InvalidOperationException(
                $"Expected no N+1 warnings, but found:{Environment.NewLine}{string.Join(Environment.NewLine, warnings)}");
        }
    }

    public static void ShouldNotBeSlow(int thresholdMs = 500)
    {
        var slow = QueryDuckCapture.LastCommands
            .Where(e => e.Duration.TotalMilliseconds >= thresholdMs)
            .ToArray();
        if (slow.Length > 0)
        {
            throw new InvalidOperationException(
                $"Expected no slow queries (>={thresholdMs} ms), but found {slow.Length}.");
        }
    }

    public static void ShouldContainRule(string ruleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        var found = QueryDuckCapture.LastCommands.Any(e => e.Diagnostics.Any(d => d.RuleId == ruleId));
        if (!found)
        {
            throw new InvalidOperationException($"Expected captured event with diagnostic rule '{ruleId}'.");
        }
    }

    public static void ShouldNotContainRule(string ruleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        var found = QueryDuckCapture.LastCommands.Any(e => e.Diagnostics.Any(d => d.RuleId == ruleId));
        if (found)
        {
            throw new InvalidOperationException($"Expected no captured events with diagnostic rule '{ruleId}'.");
        }
    }

    public static void ShouldHaveSqlContaining(string fragment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fragment);
        if (!QueryDuckCapture.LastCommands.Any(e => e.Sql.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Expected captured SQL containing '{fragment}'.");
        }
    }
}

public static class QueryDuckSessionAssert
{
    public static void ShouldHaveHotspot(string normalizedShapeFragment, int minExecutions = 2)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedShapeFragment);
        var hotspots = QueryDuckSessionHotspotsBuilder.Build(QueryDuckCapture.LastCommands);
        var match = hotspots.Hotspots.FirstOrDefault(h =>
            h.NormalizedSqlPreview.Contains(normalizedShapeFragment, StringComparison.OrdinalIgnoreCase) &&
            h.ExecutionCount >= minExecutions);
        if (match is null)
        {
            throw new InvalidOperationException(
                $"Expected hotspot with shape containing '{normalizedShapeFragment}' and >= {minExecutions} executions.");
        }
    }
}
