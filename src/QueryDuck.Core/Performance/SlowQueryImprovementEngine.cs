using QueryDuck.Core.Adapters;
using QueryDuck.Core.Capture;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Performance;

public static class SlowQueryImprovementEngine
{
    public static SlowQueryImprovementAnalysis? AnalyzeIfSlow(
        QueryCaptureEvent captureEvent,
        int slowQueryThresholdMs,
        SlowQueryImprovementContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(captureEvent);
        if (slowQueryThresholdMs <= 0 ||
            captureEvent.Duration.TotalMilliseconds < slowQueryThresholdMs)
        {
            return null;
        }

        return Analyze(captureEvent, context);
    }

    public static SlowQueryImprovementAnalysis Analyze(
        QueryCaptureEvent captureEvent,
        SlowQueryImprovementContext? context = null)
    {
        ArgumentNullException.ThrowIfNull(captureEvent);
        var provider = Enum.TryParse<DatabaseProvider>(captureEvent.Provider, out var parsed)
            ? parsed
            : DatabaseProvider.Unknown;

        var sql = captureEvent.Sql;
        var patterns = SqlPatternAnalyzer.Analyze(sql);
        var plan = ExecutionPlanAnalyzer.Analyze(captureEvent.ExecutionPlan, provider);
        var recommendations = new List<SlowQueryRecommendation>();
        var emitMermaid = context?.EmitMermaidPlanGraphs == true;

        if (plan.FullTableScan)
        {
            var table = FindFirstObjectName(plan.Steps)
                ?? (patterns.JoinTables.Count > 0 ? patterns.JoinTables[0] : null)
                ?? "target_table";
            var column = (patterns.WhereColumns.Count > 0 ? patterns.WhereColumns[0] : null)
                ?? (patterns.OrderByColumns.Count > 0 ? patterns.OrderByColumns[0] : null)
                ?? "filter_column";

            var statsRecommendation = TryStatisticsIndexRecommendation(
                provider,
                table,
                patterns,
                context?.TableStatistics);
            if (statsRecommendation is not null)
            {
                recommendations.Add(statsRecommendation);
            }
            else
            {
                recommendations.Add(new SlowQueryRecommendation(
                    SlowQueryImprovementCategory.IndexCreation,
                    "Add a selective index",
                    $"Execution plan shows a full scan on {table}. Index the predicate/join column to avoid scanning the entire table.",
                    SuggestedIndexSql: SqlRewriteSuggestions.SuggestIndex(provider, table, column)));
            }
        }

        if (patterns.SelectStar)
        {
            var rewrite = SqlRewriteSuggestions.SuggestSelectListRewrite(sql);
            recommendations.Add(new SlowQueryRecommendation(
                SlowQueryImprovementCategory.ManualRewrite,
                "Replace SELECT * with explicit columns",
                "Wide row fetches increase I/O and prevent index-only scans. Project only the columns you need.",
                SuggestedSql: rewrite,
                PlanDiff: PlanDiffBuilder.BuildEstimated(
                    captureEvent.ExecutionPlan,
                    new SlowQueryRecommendation(
                        SlowQueryImprovementCategory.ManualRewrite, "Replace SELECT *", "", rewrite),
                    emitMermaid,
                    provider)));
        }

        if (patterns.LeadingWildcardLike)
        {
            recommendations.Add(new SlowQueryRecommendation(
                SlowQueryImprovementCategory.ManualRewrite,
                "Avoid leading-wildcard LIKE predicates",
                "LIKE '%value' prevents index use on every provider. Prefer prefix search, full-text indexes, or trigram/GIN indexes.",
                SuggestedSql: sql.Replace("LIKE '%", "LIKE '", StringComparison.OrdinalIgnoreCase)));
        }

        if (patterns.FunctionOnFilteredColumn)
        {
            recommendations.Add(new SlowQueryRecommendation(
                SlowQueryImprovementCategory.IndexCreation,
                "Avoid functions on filtered columns",
                "Wrapping a column in UPPER()/CAST()/TRUNC() blocks normal B-tree indexes. Use a functional index or compare raw values.",
                SuggestedIndexSql: provider switch
                {
                    DatabaseProvider.PostgreSql => "-- Example: CREATE INDEX ix_customers_upper_name ON customers (upper(name));",
                    DatabaseProvider.Oracle => "-- Example: CREATE INDEX ix_customers_upper_name ON customers (UPPER(name));",
                    _ => "-- Create a functional index matching the expression used in the WHERE clause.",
                }));
        }

        if (patterns.OrAcrossColumns)
        {
            var unionSql = SqlRewriteSuggestions.SuggestUnionInsteadOfOr(sql);
            recommendations.Add(new SlowQueryRecommendation(
                SlowQueryImprovementCategory.ManualRewrite,
                "Rewrite OR predicates as UNION ALL",
                "OR conditions on different columns often defeat composite indexes. UNION ALL lets each branch use its own index.",
                SuggestedSql: unionSql,
                PlanDiff: unionSql is not null
                    ? PlanDiffBuilder.BuildEstimated(
                        captureEvent.ExecutionPlan,
                        new SlowQueryRecommendation(
                            SlowQueryImprovementCategory.ManualRewrite, "UNION ALL rewrite", "", unionSql),
                        emitMermaid,
                        provider)
                    : null));
        }

        if (patterns.CorrelatedSubquery)
        {
            var cteSql = SqlRewriteSuggestions.SuggestCteForRepeatedScan(sql, patterns.JoinTables);
            recommendations.Add(new SlowQueryRecommendation(
                SlowQueryImprovementCategory.UseCte,
                "Materialize repeated scans with a CTE",
                "Correlated or repeated subqueries re-scan the same table. Filter once in a CTE and join to it.",
                SuggestedSql: cteSql));
        }

        if (patterns.SelectStar && patterns.JoinTables.Count >= 2)
        {
            recommendations.Add(new SlowQueryRecommendation(
                SlowQueryImprovementCategory.SchemaSeparation,
                "Split hot and cold columns",
                "Wide joined rows suggest vertical partitioning: keep searchable metadata in a narrow table and move large/rare columns elsewhere."));
        }

        if (patterns.MissingLimit && plan.FullTableScan)
        {
            recommendations.Add(new SlowQueryRecommendation(
                SlowQueryImprovementCategory.ApplicationChange,
                "Add paging or TOP/LIMIT",
                "The query returns an unbounded rowset. Add paging to reduce memory, network, and sort cost."));
        }

        if (plan.NestedLoop && plan.FullTableScan)
        {
            recommendations.Add(new SlowQueryRecommendation(
                SlowQueryImprovementCategory.IndexCreation,
                "Support nested-loop joins with indexes",
                "Nested loops plus full scans usually mean a missing index on the inner join key."));
        }

        if (context?.HistoricalStats is { } historical)
        {
            recommendations.Insert(0, HistoricalStatsRecommendation(
                historical,
                captureEvent.Duration.TotalMilliseconds));
        }
        else if (context?.PgStatStatements is { } pgStat)
        {
            recommendations.Insert(0, new SlowQueryRecommendation(
                SlowQueryImprovementCategory.ApplicationChange,
                "Historical workload from pg_stat_statements",
                BuildPgStatSummary(pgStat, captureEvent.Duration.TotalMilliseconds)));
        }

        var primaryRewrite = FindPrimaryRewrite(recommendations);
        var enriched = EnrichWithMigrationSnippets(recommendations, provider);

        return new SlowQueryImprovementAnalysis(
            captureEvent.EventId,
            captureEvent.Duration.TotalMilliseconds,
            sql,
            enriched,
            primaryRewrite?.PlanDiff ?? FindFirstPlanDiff(enriched),
            context?.HistoricalStats,
            context?.PgStatStatements);
    }

    private static IReadOnlyList<SlowQueryRecommendation> EnrichWithMigrationSnippets(
        IReadOnlyList<SlowQueryRecommendation> recommendations,
        DatabaseProvider provider)
    {
        return recommendations
            .Select(r => string.IsNullOrWhiteSpace(r.SuggestedIndexSql)
                ? r
                : r with
                {
                    SuggestedMigrationSql = MigrationSnippetBuilder.FromIndexDdl(r.SuggestedIndexSql, provider),
                })
            .ToArray();
    }

    private static SlowQueryRecommendation HistoricalStatsRecommendation(
        QueryHistoricalStatsInsight historical,
        double currentDurationMs) =>
        new(
            SlowQueryImprovementCategory.ApplicationChange,
            $"Historical workload from {historical.SourceView ?? "database stats"}",
            BuildHistoricalStatsSummary(historical, currentDurationMs));

    private static string BuildHistoricalStatsSummary(QueryHistoricalStatsInsight insight, double currentDurationMs)
    {
        var cache = insight.CacheHitRatio.HasValue ? $", cache hit {insight.CacheHitRatio:P0}" : string.Empty;
        return
            $"{insight.SourceView ?? "Historical stats"}: {insight.Calls} calls, mean {insight.MeanExecTimeMs:F1} ms, " +
            $"total {insight.TotalExecTimeMs:F0} ms, {insight.Rows} rows returned{cache}. Current capture: {currentDurationMs:F0} ms.";
    }

    private static SlowQueryRecommendation? TryStatisticsIndexRecommendation(
        DatabaseProvider provider,
        string table,
        SqlPatternAnalyzer.SqlPatternFindings patterns,
        IReadOnlyDictionary<string, IReadOnlyList<ColumnStatistics>>? tableStatistics)
    {
        if (tableStatistics is null || tableStatistics.Count == 0)
        {
            return null;
        }

        var normalizedTable = SqlIdentifierNormalizer.NormalizeTableName(table);
        if (!tableStatistics.TryGetValue(normalizedTable, out var stats) &&
            !tableStatistics.TryGetValue(table, out stats))
        {
            stats = tableStatistics.Values.FirstOrDefault();
        }

        return stats is null
            ? null
            : StatisticsIndexRecommendationEngine.RecommendFromStatistics(
                provider,
                normalizedTable,
                patterns.WhereColumns,
                patterns.OrderByColumns,
                stats);
    }

    private static string BuildPgStatSummary(PgStatStatementInsight pgStat, double currentDurationMs)
    {
        var hitRatio = pgStat.SharedBlocksHitRatio;
        return
            $"pg_stat_statements: {pgStat.Calls} calls, mean {pgStat.MeanExecTimeMs:F1} ms, total {pgStat.TotalExecTimeMs:F0} ms, " +
            $"{pgStat.Rows} rows returned, cache hit {hitRatio:P0}. Current capture: {currentDurationMs:F0} ms.";
    }

    private static SlowQueryRecommendation? FindPrimaryRewrite(IReadOnlyList<SlowQueryRecommendation> recommendations)
    {
        foreach (var recommendation in recommendations)
        {
            if (recommendation.Category == SlowQueryImprovementCategory.ManualRewrite &&
                recommendation.SuggestedSql is not null)
            {
                return recommendation;
            }
        }

        return null;
    }

    private static PlanDiffVisualization? FindFirstPlanDiff(IReadOnlyList<SlowQueryRecommendation> recommendations)
    {
        foreach (var recommendation in recommendations)
        {
            if (recommendation.PlanDiff is not null)
            {
                return recommendation.PlanDiff;
            }
        }

        return null;
    }

    private static string? FindFirstObjectName(IReadOnlyList<PlanStepSummary> steps)
    {
        foreach (var step in steps)
        {
            if (step.ObjectName is not null)
            {
                return step.ObjectName;
            }
        }

        return null;
    }

    public static async Task<SlowQueryImprovementAnalysis> EnrichWithImprovedPlanAsync(
        SlowQueryImprovementAnalysis analysis,
        IDatabaseAdapter adapter,
        System.Data.Common.DbConnection connection,
        IReadOnlyDictionary<string, object?> parameters,
        string? originalPlanText,
        bool emitMermaidPlanGraphs = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(connection);

        var recommendations = analysis.Recommendations.ToList();
        PlanDiffVisualization? primaryPlanDiff = analysis.PrimaryPlanDiff;

        for (var i = 0; i < recommendations.Count; i++)
        {
            var recommendation = recommendations[i];
            if (recommendation.Category != SlowQueryImprovementCategory.ManualRewrite ||
                string.IsNullOrWhiteSpace(recommendation.SuggestedSql) ||
                recommendation.SuggestedSql.Contains('<', StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                var improvedPlan = await adapter.GetExecutionPlanAsync(
                    connection,
                    recommendation.SuggestedSql,
                    parameters,
                    cancellationToken).ConfigureAwait(false);

                var planDiff = PlanDiffBuilder.Build(originalPlanText, improvedPlan.PlanText, emitMermaidPlanGraphs, adapter.Provider);
                recommendations[i] = recommendation with
                {
                    ImprovedPlanText = improvedPlan.PlanText,
                    PlanDiff = planDiff,
                };
                primaryPlanDiff ??= planDiff;
            }
            catch (Exception)
            {
                // Improved EXPLAIN is best-effort during debugging.
            }
        }

        if (emitMermaidPlanGraphs && primaryPlanDiff is not null &&
            primaryPlanDiff.OriginalMermaid is null)
        {
            primaryPlanDiff = PlanDiffBuilder.Build(
                originalPlanText,
                recommendations.Select(r => r.ImprovedPlanText).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)),
                emitMermaid: true,
                adapter.Provider);
        }

        return analysis with
        {
            Recommendations = recommendations,
            PrimaryPlanDiff = primaryPlanDiff,
        };
    }
}
