using System.Globalization;
using System.Text;

namespace QueryDuck.Core.Performance;

public static class PlanMermaidGraphBuilder
{
    public static string BuildFlowchart(IReadOnlyList<PlanStepSummary> steps, string title)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(title);
        if (steps.Count == 0)
        {
            return $"flowchart TD\n  empty[\"{Escape(title)}: no steps\"]";
        }

        var builder = new StringBuilder();
        builder.AppendLine("flowchart TD");
        builder.Append("  title[[\"");
        builder.Append(Escape(title));
        builder.AppendLine("\"]]");

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var nodeId = $"s{i}";
            builder.Append("  ");
            builder.Append(nodeId);
            builder.Append("[\"");
            builder.Append(Escape(FormatStep(step)));
            builder.AppendLine("\"]");

            if (i == 0)
            {
                builder.Append("  title --> ");
                builder.AppendLine(nodeId);
            }
            else
            {
                builder.Append("  s");
                builder.Append(i - 1);
                builder.Append(" --> ");
                builder.AppendLine(nodeId);
            }
        }

        return builder.ToString().TrimEnd();
    }

    public static string BuildSideBySideComparison(
        IReadOnlyList<PlanStepSummary> originalSteps,
        IReadOnlyList<PlanStepSummary> improvedSteps)
    {
        ArgumentNullException.ThrowIfNull(originalSteps);
        ArgumentNullException.ThrowIfNull(improvedSteps);
        var builder = new StringBuilder();
        builder.AppendLine("flowchart LR");
        builder.AppendLine("  subgraph original [Original plan]");
        AppendSubgraph(builder, "o", originalSteps);
        builder.AppendLine("  end");
        builder.AppendLine("  subgraph improved [Improved plan]");
        AppendSubgraph(builder, "i", improvedSteps);
        builder.AppendLine("  end");
        return builder.ToString().TrimEnd();
    }

    private static void AppendSubgraph(StringBuilder builder, string prefix, IReadOnlyList<PlanStepSummary> steps)
    {
        if (steps.Count == 0)
        {
            builder.Append("    ");
            builder.Append(prefix);
            builder.AppendLine("0[\"no steps\"]");
            return;
        }

        for (var i = 0; i < steps.Count; i++)
        {
            builder.Append("    ");
            builder.Append(prefix);
            builder.Append(i);
            builder.Append("[\"");
            builder.Append(Escape(FormatStep(steps[i])));
            builder.AppendLine("\"]");

            if (i > 0)
            {
                builder.Append("    ");
                builder.Append(prefix);
                builder.Append(i - 1);
                builder.Append(" --> ");
                builder.Append(prefix);
                builder.AppendLine(i.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    private static string FormatStep(PlanStepSummary step)
    {
        var cost = step.Cost?.ToString("F0", CultureInfo.InvariantCulture) ?? "?";
        var name = string.IsNullOrWhiteSpace(step.ObjectName) ? string.Empty : $" {step.ObjectName}";
        return $"{step.Operation}{name}<br/>cost {cost}";
    }

    private static string Escape(string value) =>
        value.Replace("\"", "#quot;", StringComparison.Ordinal)
            .Replace("[", "#91;", StringComparison.Ordinal)
            .Replace("]", "#93;", StringComparison.Ordinal)
            .Replace("<br/>", " / ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Replace('\r', ' ');
}
