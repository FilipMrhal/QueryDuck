using System.Globalization;
using System.Text;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Performance;

internal static class PlanDiffBuilder
{
    public static PlanDiffVisualization Build(string? originalPlan, string? improvedPlan, bool emitMermaid = false, DatabaseProvider provider = DatabaseProvider.Unknown)
    {
        var originalSteps = ExecutionPlanAnalyzer.Analyze(originalPlan, provider).Steps;
        var improvedSteps = ExecutionPlanAnalyzer.Analyze(improvedPlan, provider).Steps;
        var summary = BuildSummary(originalSteps, improvedSteps);
        var textDiff = BuildTextDiff(originalPlan, improvedPlan, originalSteps, improvedSteps, summary);
        return CreateVisualization(originalSteps, improvedSteps, summary, textDiff, emitMermaid);
    }

    public static PlanDiffVisualization BuildEstimated(
        string? originalPlan,
        SlowQueryRecommendation rewriteRecommendation,
        bool emitMermaid = false,
        DatabaseProvider provider = DatabaseProvider.Unknown)
    {
        var originalSteps = ExecutionPlanAnalyzer.Analyze(originalPlan, provider).Steps;
        var estimatedCost = originalSteps.Count > 0 && originalSteps[0].Cost is { } cost ? cost * 0.05 : (double?)null;
        var improvedSteps = new List<PlanStepSummary>
        {
            new("INDEX SCAN", rewriteRecommendation.SuggestedIndexSql is not null ? "proposed index" : null,
                "Estimated after rewrite", estimatedCost),
        };

        var summary = new List<string>
        {
            "Estimated plan improvement (EXPLAIN not run for rewritten SQL).",
            rewriteRecommendation.Title,
        };
        summary.AddRange(BuildSummary(originalSteps, improvedSteps));

        var textDiff = BuildTextDiff(originalPlan, rewriteRecommendation.SuggestedSql, originalSteps, improvedSteps, summary);
        return CreateVisualization(originalSteps, improvedSteps, summary, textDiff, emitMermaid);
    }

    private static PlanDiffVisualization CreateVisualization(
        IReadOnlyList<PlanStepSummary> originalSteps,
        IReadOnlyList<PlanStepSummary> improvedSteps,
        IReadOnlyList<string> summary,
        string textDiff,
        bool emitMermaid)
    {
        if (!emitMermaid)
        {
            return new PlanDiffVisualization(originalSteps, improvedSteps, summary, textDiff);
        }

        return new PlanDiffVisualization(
            originalSteps,
            improvedSteps,
            summary,
            textDiff,
            PlanMermaidGraphBuilder.BuildFlowchart(originalSteps, "Original plan"),
            PlanMermaidGraphBuilder.BuildFlowchart(improvedSteps, "Improved plan"),
            PlanMermaidGraphBuilder.BuildSideBySideComparison(originalSteps, improvedSteps));
    }

    private static IReadOnlyList<string> BuildSummary(
        IReadOnlyList<PlanStepSummary> originalSteps,
        IReadOnlyList<PlanStepSummary> improvedSteps)
    {
        var lines = new List<string>();
        var originalCost = originalSteps.Where(s => s.Cost.HasValue).Select(s => s.Cost!.Value).DefaultIfEmpty(0).Max();
        var improvedCost = improvedSteps.Where(s => s.Cost.HasValue).Select(s => s.Cost!.Value).DefaultIfEmpty(0).Max();

        if (originalCost > 0 && improvedCost > 0 && improvedCost < originalCost)
        {
            var reduction = (1 - (improvedCost / originalCost)) * 100;
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"Estimated cost reduction: {reduction:F0}% ({originalCost:F0} -> {improvedCost:F0})"));
        }

        var originalFullScan = FindFirstFullScan(originalSteps);
        var improvedIndex = FindFirstIndexScan(improvedSteps);
        if (originalFullScan is not null && improvedIndex is not null)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture,
                $"FULL SCAN {originalFullScan.ObjectName ?? "table"} -> INDEX SCAN {improvedIndex.ObjectName ?? "index"}"));
        }

        if (lines.Count == 0)
        {
            lines.Add("Review rewritten SQL with EXPLAIN on your database to validate the plan change.");
        }

        return lines;
    }

    private static string BuildTextDiff(
        string? originalPlan,
        string? improvedPlan,
        IReadOnlyList<PlanStepSummary> originalSteps,
        IReadOnlyList<PlanStepSummary> improvedSteps,
        IReadOnlyList<string> summary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("=== QueryDuck Plan Comparison ===");
        builder.AppendLine();
        foreach (var line in summary)
        {
            builder.Append("* ");
            builder.AppendLine(line);
        }

        builder.AppendLine();
        builder.AppendLine("--- Original plan (key steps) ---");
        foreach (var step in originalSteps.Take(8))
        {
            AppendStep(builder, step);
        }

        builder.AppendLine();
        builder.AppendLine("--- Improved plan (key steps) ---");
        foreach (var step in improvedSteps.Take(8))
        {
            AppendStep(builder, step);
        }

        if (!string.IsNullOrWhiteSpace(improvedPlan))
        {
            builder.AppendLine();
            builder.AppendLine("--- Improved EXPLAIN output ---");
            builder.AppendLine(TrimPlan(improvedPlan));
        }
        else if (!string.IsNullOrWhiteSpace(originalPlan))
        {
            builder.AppendLine();
            builder.AppendLine("--- Original EXPLAIN output (excerpt) ---");
            builder.AppendLine(TrimPlan(originalPlan));
        }

        return builder.ToString();
    }

    private static void AppendStep(StringBuilder builder, PlanStepSummary step)
    {
        var cost = step.Cost?.ToString("F0", CultureInfo.InvariantCulture) ?? "?";
        builder.Append("  - ");
        builder.Append(step.Operation);
        if (!string.IsNullOrWhiteSpace(step.ObjectName))
        {
            builder.Append(' ');
            builder.Append(step.ObjectName);
        }

        builder.Append(" (cost ");
        builder.Append(cost);
        builder.AppendLine(")");
    }

    private static PlanStepSummary? FindFirstFullScan(IReadOnlyList<PlanStepSummary> steps)
    {
        foreach (var step in steps)
        {
            if (step.Operation.Contains("FULL", StringComparison.OrdinalIgnoreCase))
            {
                return step;
            }
        }

        return null;
    }

    private static PlanStepSummary? FindFirstIndexScan(IReadOnlyList<PlanStepSummary> steps)
    {
        foreach (var step in steps)
        {
            if (step.Operation.Contains("INDEX", StringComparison.OrdinalIgnoreCase))
            {
                return step;
            }
        }

        return null;
    }

    private static string TrimPlan(string plan) =>
        plan.Length <= 2000 ? plan : plan[..2000] + Environment.NewLine + "...";
}
