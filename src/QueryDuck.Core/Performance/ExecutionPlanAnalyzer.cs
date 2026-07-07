using System.Text.Json;
using System.Text.RegularExpressions;
using QueryDuck.Core.Providers;

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

    public static PlanFindings Analyze(string? planText, DatabaseProvider provider = DatabaseProvider.Unknown) =>
        string.IsNullOrWhiteSpace(planText)
            ? new PlanFindings(false, false, false, false, [], null)
            : AnalyzeCore(planText, provider);

    private static PlanFindings AnalyzeCore(string planText, DatabaseProvider provider)
    {
        var steps = ExtractSteps(planText, provider);
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

    private static IReadOnlyList<PlanStepSummary> ExtractSteps(string planText, DatabaseProvider provider) =>
        provider switch
        {
            DatabaseProvider.PostgreSql when LooksLikeJsonArray(planText) ||
                planText.Contains("\"Node Type\"", StringComparison.Ordinal) =>
                ExtractPostgreSqlJsonSteps(planText),
            DatabaseProvider.SqlServer when planText.Contains("<ShowPlanXML", StringComparison.OrdinalIgnoreCase) =>
                ExtractSqlServerXmlSteps(planText),
            DatabaseProvider.Oracle when planText.Contains('|', StringComparison.Ordinal) =>
                ExtractOracleTextSteps(planText),
            DatabaseProvider.MySql when LooksLikeJsonObject(planText) =>
                ExtractMySqlJsonSteps(planText),
            DatabaseProvider.Sqlite => ExtractSqliteSteps(planText),
            _ => ExtractGenericSteps(planText),
        };

    private static bool LooksLikeJsonArray(string planText)
    {
        foreach (var ch in planText)
        {
            if (char.IsWhiteSpace(ch))
            {
                continue;
            }

            return ch == '[';
        }

        return false;
    }

    private static bool LooksLikeJsonObject(string planText)
    {
        foreach (var ch in planText)
        {
            if (char.IsWhiteSpace(ch))
            {
                continue;
            }

            return ch == '{';
        }

        return false;
    }

    private static IReadOnlyList<PlanStepSummary> ExtractPostgreSqlJsonSteps(string planText)
    {
        var steps = new List<PlanStepSummary>();
        try
        {
            using var document = JsonDocument.Parse(planText);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in document.RootElement.EnumerateArray())
                {
                    WalkPostgreSqlNode(item, steps);
                }
            }
            else
            {
                WalkPostgreSqlNode(document.RootElement, steps);
            }
        }
        catch (JsonException)
        {
            return ExtractGenericSteps(planText);
        }

        return steps.Count > 0 ? steps : ExtractGenericSteps(planText);
    }

    private static void WalkPostgreSqlNode(JsonElement node, List<PlanStepSummary> steps)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (node.TryGetProperty("Plan", out var plan))
        {
            WalkPostgreSqlPlan(plan, steps);
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in node.EnumerateArray())
            {
                WalkPostgreSqlNode(item, steps);
            }
        }
    }

    private static void WalkPostgreSqlPlan(JsonElement plan, List<PlanStepSummary> steps)
    {
        var nodeType = plan.TryGetProperty("Node Type", out var typeProp) ? typeProp.GetString() : "PLAN";
        var relation = plan.TryGetProperty("Relation Name", out var rel) ? rel.GetString() : null;
        var cost = plan.TryGetProperty("Total Cost", out var costProp) && costProp.TryGetDouble(out var c) ? c : (double?)null;
        steps.Add(new PlanStepSummary(nodeType ?? "PLAN", relation, null, cost));

        if (plan.TryGetProperty("Plans", out var children))
        {
            foreach (var child in children.EnumerateArray())
            {
                WalkPostgreSqlPlan(child, steps);
            }
        }
    }

    private static IReadOnlyList<PlanStepSummary> ExtractSqlServerXmlSteps(string planText)
    {
        var steps = new List<PlanStepSummary>();
        foreach (Match match in SqlServerRelOpRegex().Matches(planText))
        {
            var physicalOp = match.Groups[1].Value;
            var logicalOp = match.Groups[2].Success ? match.Groups[2].Value : null;
            var table = match.Groups[3].Success ? match.Groups[3].Value : null;
            var cost = match.Groups[4].Success && double.TryParse(match.Groups[4].Value, out var c) ? c : (double?)null;
            steps.Add(new PlanStepSummary(physicalOp, table, logicalOp, cost));
        }

        return steps.Count > 0 ? steps : ExtractGenericSteps(planText);
    }

    private static IReadOnlyList<PlanStepSummary> ExtractOracleTextSteps(string planText)
    {
        var steps = new List<PlanStepSummary>();
        foreach (var line in planText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.Contains('|', StringComparison.Ordinal))
            {
                continue;
            }

            var operation = line.Split('|', StringSplitOptions.TrimEntries).ElementAtOrDefault(1);
            if (string.IsNullOrWhiteSpace(operation))
            {
                continue;
            }

            var objectName = OracleObjectRegex().Match(line).Groups[1].Success
                ? OracleObjectRegex().Match(line).Groups[1].Value
                : null;
            var cost = OracleCostRegex().Match(line).Groups[1].Success &&
                double.TryParse(OracleCostRegex().Match(line).Groups[1].Value, out var c)
                ? c
                : (double?)null;
            steps.Add(new PlanStepSummary(operation, objectName, line, cost));
        }

        return steps.Count > 0 ? steps : ExtractGenericSteps(planText);
    }

    private static IReadOnlyList<PlanStepSummary> ExtractMySqlJsonSteps(string planText)
    {
        var steps = new List<PlanStepSummary>();
        try
        {
            using var document = JsonDocument.Parse(planText);
            WalkMySqlNode(document.RootElement, steps);
        }
        catch (JsonException)
        {
            return ExtractGenericSteps(planText);
        }

        return steps.Count > 0 ? steps : ExtractGenericSteps(planText);
    }

    private static void WalkMySqlNode(JsonElement node, List<PlanStepSummary> steps)
    {
        if (node.TryGetProperty("access_type", out var accessType))
        {
            var table = node.TryGetProperty("table_name", out var tableProp) ? tableProp.GetString() : null;
            var cost = node.TryGetProperty("cost_info", out var costInfo) &&
                costInfo.TryGetProperty("query_cost", out var queryCost) &&
                queryCost.TryGetDouble(out var c)
                ? c
                : (double?)null;
            steps.Add(new PlanStepSummary(accessType.GetString() ?? "ACCESS", table, null, cost));
        }

        foreach (var property in node.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                WalkMySqlJsonValue(property.Value, steps);
            }
        }
    }

    private static void WalkMySqlJsonValue(JsonElement element, List<PlanStepSummary> steps)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                WalkMySqlNode(element, steps);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    WalkMySqlJsonValue(item, steps);
                }

                break;
        }
    }

    private static IReadOnlyList<PlanStepSummary> ExtractSqliteSteps(string planText)
    {
        var steps = new List<PlanStepSummary>();
        foreach (var line in planText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length < 4)
            {
                continue;
            }

            var detail = parts[^1];
            var operation = detail.Contains("SCAN", StringComparison.OrdinalIgnoreCase) ? "SCAN" :
                detail.Contains("SEARCH", StringComparison.OrdinalIgnoreCase) ? "SEARCH" :
                detail.Contains("JOIN", StringComparison.OrdinalIgnoreCase) ? "JOIN" : "STEP";
            var table = SqliteTableRegex().Match(detail).Groups[1].Success
                ? SqliteTableRegex().Match(detail).Groups[1].Value
                : null;
            steps.Add(new PlanStepSummary(operation, table, detail, null));
        }

        return steps.Count > 0 ? steps : ExtractGenericSteps(planText);
    }

    private static IReadOnlyList<PlanStepSummary> ExtractGenericSteps(string planText)
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

    [GeneratedRegex(@"PhysicalOp=""([^""]+)""(?:[^>]*LogicalOp=""([^""]+)"")?(?:[^>]*Table=""([^""]+)"")?(?:[^>]*EstimatedTotalSubtreeCost=""([\d.]+)"")?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SqlServerRelOpRegex();

    [GeneratedRegex(@"\|[^|]*\|\s*(\w+)", RegexOptions.CultureInvariant)]
    private static partial Regex OracleObjectRegex();

    [GeneratedRegex(@"Cost\s*\(([\d.]+)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OracleCostRegex();

    [GeneratedRegex(@"(?:USING|FROM)\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SqliteTableRegex();

    [GeneratedRegex(@"(TABLE ACCESS FULL|FULL TABLE SCAN|SEQ SCAN|CLUSTERED INDEX SCAN|TABLE SCAN|SCAN TABLE)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FullScanRegex();

    [GeneratedRegex(@"(INDEX (?:RANGE|UNIQUE )?SCAN|INDEX ONLY SCAN|BITMAP INDEX SCAN|Index Scan)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IndexScanRegex();

    [GeneratedRegex(@"(TABLE ACCESS FULL|Seq Scan on|FULL TABLE SCAN|SCAN TABLE)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
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
