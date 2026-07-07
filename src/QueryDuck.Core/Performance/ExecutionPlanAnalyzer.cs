using System.Text.RegularExpressions;

namespace QueryDuck.Core.Performance;

internal static partial class ExecutionPlanAnalyzer
{
    public sealed record PlanFindings(
        bool FullTableScan,
        bool IndexScan,
        bool NestedLoop,
        bool HashJoin,
        IReadOnlyList<PlanStepSummary> Steps,
        double? HighestCost);

    public static PlanFindings Analyze(string? planText)
    {
        if (string.IsNullOrWhiteSpace(planText))
        {
            return new PlanFindings(false, false, false, false, [], null);
        }

        var steps = ExtractSteps(planText);
        var upper = planText.ToUpperInvariant();

        return new PlanFindings(
            FullTableScan: FullScanRegex().IsMatch(upper),
            IndexScan: IndexScanRegex().IsMatch(upper),
            NestedLoop: upper.Contains("NESTED LOOP", StringComparison.Ordinal) ||
                upper.Contains("NESTED LOOPS", StringComparison.Ordinal),
            HashJoin: upper.Contains("HASH JOIN", StringComparison.Ordinal),
            Steps: steps,
            HighestCost: steps.MaxBy(s => s.Cost)?.Cost);
    }

    private static IReadOnlyList<PlanStepSummary> ExtractSteps(string planText)
    {
        var steps = new List<PlanStepSummary>();
        foreach (var line in planText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cost = ParseCost(line);
            if (FullScanLineRegex().IsMatch(line))
            {
                steps.Add(new PlanStepSummary("FULL SCAN", ParseObject(line), line, cost));
            }
            else if (IndexScanLineRegex().IsMatch(line))
            {
                steps.Add(new PlanStepSummary("INDEX SCAN", ParseObject(line), line, cost));
            }
            else if (line.Contains("JOIN", StringComparison.OrdinalIgnoreCase))
            {
                steps.Add(new PlanStepSummary("JOIN", ParseObject(line), line, cost));
            }
            else if (line.Contains("Seq Scan", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Index Scan", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Bitmap", StringComparison.OrdinalIgnoreCase))
            {
                steps.Add(new PlanStepSummary("SCAN", ParseObject(line), line, cost));
            }
        }

        return steps.Count > 0 ? steps : [new PlanStepSummary("PLAN", Detail: planText[..Math.Min(planText.Length, 120)])];
    }

    private static double? ParseCost(string line)
    {
        var match = CostRangeRegex().Match(line);
        if (match.Success)
        {
            return double.TryParse(match.Groups[2].Value, out var cost) ? cost : null;
        }

        match = CostRegex().Match(line);
        return match.Success && double.TryParse(match.Groups[1].Value, out var single) ? single : null;
    }

    private static string? ParseObject(string line)
    {
        var match = ObjectRegex().Match(line);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"(TABLE ACCESS FULL|FULL TABLE SCAN|SEQ SCAN|CLUSTERED INDEX SCAN|TABLE SCAN)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FullScanRegex();

    [GeneratedRegex(@"(INDEX (?:RANGE|UNIQUE )?SCAN|INDEX ONLY SCAN|BITMAP INDEX SCAN|Index Scan)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IndexScanRegex();

    [GeneratedRegex(@"(TABLE ACCESS FULL|Seq Scan on|FULL TABLE SCAN)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FullScanLineRegex();

    [GeneratedRegex(@"(INDEX (?:RANGE|UNIQUE )?SCAN|Index Scan using|Index Only Scan)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IndexScanLineRegex();

    [GeneratedRegex(@"(?:cost=|Cost:)\s*([\d.]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CostRegex();

    [GeneratedRegex(@"(?:cost=|Cost:)\s*[\d.]+\.\.([\d.]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CostRangeRegex();

    [GeneratedRegex(@"(?:on|ON|TABLE|table|OBJECT_NAME)\s+(""?\w+""?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ObjectRegex();
}
