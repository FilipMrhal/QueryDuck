namespace QueryDuck.Core.Capture;

public sealed record QueryCaptureEventDiff(
    QueryCaptureEvent Left,
    QueryCaptureEvent Right,
    bool SqlChanged,
    bool ParametersChanged,
    bool PlanChanged,
    bool DiagnosticsChanged,
    bool DurationChanged,
    string SqlDiff,
    string ParametersDiff,
    string DiagnosticsDiff);

public static class QueryDuckEventDiffBuilder
{
    public static QueryCaptureEventDiff Build(QueryCaptureEvent left, QueryCaptureEvent right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var sqlChanged = !string.Equals(left.Sql, right.Sql, StringComparison.Ordinal);
        var parametersChanged = !ParametersEqual(left.Parameters, right.Parameters);
        var planChanged = !string.Equals(left.ExecutionPlan, right.ExecutionPlan, StringComparison.Ordinal);
        var diagnosticsChanged = !string.Equals(
            string.Join('|', left.Diagnostics.Select(d => d.RuleId + d.Message)),
            string.Join('|', right.Diagnostics.Select(d => d.RuleId + d.Message)),
            StringComparison.Ordinal);
        var durationChanged = Math.Abs(left.Duration.TotalMilliseconds - right.Duration.TotalMilliseconds) > 0.5;

        return new QueryCaptureEventDiff(
            left,
            right,
            sqlChanged,
            parametersChanged,
            planChanged,
            diagnosticsChanged,
            durationChanged,
            BuildLineDiff(left.Sql, right.Sql),
            BuildParametersDiff(left.Parameters, right.Parameters),
            BuildDiagnosticsDiff(left.Diagnostics, right.Diagnostics));
    }

    private static bool ParametersEqual(
        IReadOnlyDictionary<string, object?> left,
        IReadOnlyDictionary<string, object?> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value) || !Equals(pair.Value, value))
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildLineDiff(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal))
        {
            return "(no SQL changes)";
        }

        return $"--- left\n{left}\n+++ right\n{right}";
    }

    private static string BuildParametersDiff(
        IReadOnlyDictionary<string, object?> left,
        IReadOnlyDictionary<string, object?> right)
    {
        var keys = left.Keys.Concat(right.Keys).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(k => k);
        var lines = new List<string>();
        foreach (var key in keys)
        {
            left.TryGetValue(key, out var leftValue);
            right.TryGetValue(key, out var rightValue);
            if (!Equals(leftValue, rightValue))
            {
                lines.Add($"{key}: {leftValue} -> {rightValue}");
            }
        }

        return lines.Count == 0 ? "(no parameter changes)" : string.Join(Environment.NewLine, lines);
    }

    private static string BuildDiagnosticsDiff(
        IReadOnlyList<QueryDiagnosticDto> left,
        IReadOnlyList<QueryDiagnosticDto> right)
    {
        var leftRules = left.Select(d => d.RuleId).ToHashSet(StringComparer.Ordinal);
        var rightRules = right.Select(d => d.RuleId).ToHashSet(StringComparer.Ordinal);
        var added = rightRules.Except(leftRules).OrderBy(r => r).ToArray();
        var removed = leftRules.Except(rightRules).OrderBy(r => r).ToArray();
        if (added.Length == 0 && removed.Length == 0)
        {
            return "(no diagnostic changes)";
        }

        return string.Join(
            Environment.NewLine,
            removed.Select(r => $"- {r}").Concat(added.Select(r => $"+ {r}")));
    }
}
