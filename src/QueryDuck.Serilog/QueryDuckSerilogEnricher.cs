using QueryDuck.Core.Capture;
using QueryDuck.Core.ExpressionTrees;
using QueryDuck.Core.Performance;

namespace QueryDuck.Serilog;

internal static class QueryDuckSerilogEnricher
{
    public static Dictionary<string, object?> BuildProperties(
        QueryCaptureEvent captureEvent,
        QueryCapturePublishContext context,
        QueryDuckSerilogOptions options)
    {
        var sensitive = options.SensitiveData;
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["EventId"] = captureEvent.EventId,
            ["Timestamp"] = captureEvent.Timestamp,
            ["Provider"] = captureEvent.Provider,
            ["Source"] = captureEvent.Source,
            ["Caller"] = captureEvent.Caller,
            ["Tag"] = captureEvent.Tag,
            ["BulkOperation"] = captureEvent.BulkOperation,
            ["DurationMs"] = captureEvent.Duration.TotalMilliseconds,
            ["SlowQueryThresholdMs"] = context.SlowQueryThresholdMs,
            ["IsSlow"] = context.IsSlow,
            ["Succeeded"] = captureEvent.Succeeded,
            ["SchemaVersion"] = captureEvent.SchemaVersion,
            ["WarningCount"] = captureEvent.Diagnostics.Count(d =>
                string.Equals(d.Severity, "Warning", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.Severity, "Error", StringComparison.OrdinalIgnoreCase)),
        };

        if (!captureEvent.Succeeded)
        {
            properties["ErrorMessage"] = captureEvent.ErrorMessage;
            properties["ExceptionType"] = captureEvent.ExceptionType;
        }

        if (sensitive.IncludeSqlText)
        {
            properties["Sql"] = captureEvent.Sql;
        }
        else
        {
            properties["SqlHash"] = QueryDuckSensitiveDataRedactor.HashValue(captureEvent.Sql);
        }

        if (sensitive.IncludeParameterNames || sensitive.IncludeParameterValues)
        {
            var parameterPayload = BuildParameters(captureEvent.Parameters, sensitive);
            if (parameterPayload.Count > 0)
            {
                properties["Parameters"] = parameterPayload;
            }

            if (sensitive.IncludeParameterNames && !sensitive.IncludeParameterValues)
            {
                properties["ParameterNames"] = captureEvent.Parameters.Keys.ToArray();
            }
        }

        if (captureEvent.Diagnostics.Count > 0)
        {
            properties["Diagnostics"] = captureEvent.Diagnostics
                .Select(d => new Dictionary<string, object?>
                {
                    ["RuleId"] = d.RuleId,
                    ["Severity"] = d.Severity,
                    ["Message"] = d.Message,
                    ["FixHint"] = d.FixHint,
                })
                .ToArray();
        }

        if (sensitive.IncludeExpressionCSharp && !string.IsNullOrWhiteSpace(captureEvent.ExpressionCSharp))
        {
            properties["ExpressionCSharp"] = ProtectOptionalText(
                captureEvent.ExpressionCSharp,
                sensitive,
                treatAsPii: true);
        }

        if (sensitive.IncludeExpressionTree)
        {
            if (!string.IsNullOrWhiteSpace(captureEvent.ExpressionTreeText))
            {
                properties["ExpressionTreeText"] = ProtectOptionalText(
                    captureEvent.ExpressionTreeText,
                    sensitive,
                    treatAsPii: true);
            }

            if (captureEvent.ExpressionTree is not null)
            {
                properties["ExpressionTree"] = SerializeExpressionTree(captureEvent.ExpressionTree, sensitive);
            }
        }

        if (sensitive.IncludeExecutionPlan && !string.IsNullOrWhiteSpace(captureEvent.ExecutionPlan))
        {
            properties["ExecutionPlan"] = ProtectOptionalText(
                captureEvent.ExecutionPlan,
                sensitive,
                treatAsPii: false);
            properties["PlanHash"] = captureEvent.PlanHash;
        }

        if (context.IsSlow && captureEvent.ImprovementAnalysis is not null)
        {
            properties["ImprovementAnalysis"] = BuildImprovementAnalysis(captureEvent.ImprovementAnalysis, sensitive);
        }

        return properties;
    }

    private static Dictionary<string, object?> BuildParameters(
        IReadOnlyDictionary<string, object?> parameters,
        QueryDuckSensitiveDataLoggingOptions sensitive)
    {
        var exported = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in parameters)
        {
            if (!sensitive.IncludeParameterValues)
            {
                continue;
            }

            var isPii = QueryDuckSensitiveDataRedactor.IsPiiParameterName(name, sensitive);
            var allowInclude = sensitive.IncludeSensitiveData && (!isPii || sensitive.IncludePii);
            var mode = isPii && !sensitive.IncludePii ? sensitive.PiiMode : sensitive.DefaultMode;
            var protectedValue = QueryDuckSensitiveDataRedactor.ProtectValue(value, mode, allowInclude);
            if (protectedValue is not null || mode != QueryDuckSensitiveDataMode.Omit)
            {
                exported[name] = protectedValue;
            }
        }

        return exported;
    }

    private static Dictionary<string, object?> BuildImprovementAnalysis(
        SlowQueryImprovementAnalysisDto analysis,
        QueryDuckSensitiveDataLoggingOptions sensitive)
    {
        var payload = new Dictionary<string, object?>
        {
            ["DurationMs"] = analysis.DurationMs,
        };

        if (sensitive.IncludeRecommendationSummaries)
        {
            payload["Recommendations"] = analysis.Recommendations
                .Select(r => new Dictionary<string, object?>
                {
                    ["Category"] = r.Category,
                    ["Title"] = r.Title,
                    ["Description"] = r.Description,
                })
                .ToArray();
        }

        if (sensitive.IncludeSuggestedSql && sensitive.IncludeSensitiveData)
        {
            payload["SuggestedSql"] = analysis.Recommendations
                .Where(r => !string.IsNullOrWhiteSpace(r.SuggestedSql))
                .Select(r => r.SuggestedSql)
                .ToArray();
            payload["SuggestedIndexSql"] = analysis.Recommendations
                .Where(r => !string.IsNullOrWhiteSpace(r.SuggestedIndexSql))
                .Select(r => r.SuggestedIndexSql)
                .ToArray();
        }

        if (analysis.PrimaryPlanDiff is not null)
        {
            payload["PlanDiffSummary"] = analysis.PrimaryPlanDiff.SummaryLines;
        }

        if (analysis.PgStatStatements is not null)
        {
            payload["PgStatStatements"] = new Dictionary<string, object?>
            {
                ["Calls"] = analysis.PgStatStatements.Calls,
                ["MeanExecTimeMs"] = analysis.PgStatStatements.MeanExecTimeMs,
                ["TotalExecTimeMs"] = analysis.PgStatStatements.TotalExecTimeMs,
                ["Rows"] = analysis.PgStatStatements.Rows,
                ["SharedBlocksHitRatio"] = analysis.PgStatStatements.SharedBlocksHitRatio,
                ["MatchedQueryText"] = sensitive.IncludePgStatMatchedQueryText && sensitive.IncludeSensitiveData
                    ? analysis.PgStatStatements.MatchedQueryText
                    : null,
            };
        }

        return payload;
    }

    private static Dictionary<string, object?> SerializeExpressionTree(
        ExpressionTreeNode node,
        QueryDuckSensitiveDataLoggingOptions sensitive)
    {
        var allowInclude = sensitive.IncludeSensitiveData && sensitive.IncludePii;
        return new Dictionary<string, object?>
        {
            ["Kind"] = node.Kind,
            ["Type"] = node.Type,
            ["Name"] = node.Name,
            ["Value"] = QueryDuckSensitiveDataRedactor.ProtectValue(
                node.Value,
                sensitive.PiiMode,
                allowInclude),
            ["Children"] = node.Children?.Select(child => SerializeExpressionTree(child, sensitive)).ToArray(),
        };
    }

    private static string? ProtectOptionalText(
        string text,
        QueryDuckSensitiveDataLoggingOptions sensitive,
        bool treatAsPii)
    {
        if (!sensitive.IncludeSensitiveData)
        {
            return QueryDuckSensitiveDataRedactor.ProtectText(
                text,
                sensitive.DefaultMode,
                allowInclude: false);
        }

        var allowInclude = !treatAsPii || sensitive.IncludePii;
        var mode = treatAsPii && !sensitive.IncludePii ? sensitive.PiiMode : sensitive.DefaultMode;
        return QueryDuckSensitiveDataRedactor.ProtectText(text, mode, allowInclude);
    }
}
