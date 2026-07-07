using QueryDuck.Core.Adapters;
using QueryDuck.Core.Capture;
using QueryDuck.Core.Performance;

namespace QueryDuck.Core.Learning;

public static class QueryHeuristicMemory
{
    private static readonly Lock Gate = new();
    private static QueryHeuristicMemoryStore? _store;
    private static bool _enabled;

    public static void Configure(QueryCaptureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (Gate)
        {
            _enabled = options.EnableHeuristicMemory;
            if (!_enabled)
            {
                _store = null;
                return;
            }

            var path = ResolveStorePath(options.HeuristicMemoryStorePath);
            _store = new QueryHeuristicMemoryStore(path, options.HeuristicMemoryMaxEntries);
        }
    }

    public static bool IsEnabled
    {
        get
        {
            lock (Gate)
            {
                return _enabled && _store is not null;
            }
        }
    }

    public static void RecordSlowCapture(string provider, string sql, double durationMs)
    {
        if (!IsEnabled)
        {
            return;
        }

        var shape = QueryShapeFingerprint.Compute(sql, provider);
        lock (Gate)
        {
            _store?.RecordSlowCapture(shape, provider, durationMs);
        }
    }

    public static void RecordFeedback(
        string provider,
        string sql,
        string category,
        string title,
        QueryHeuristicMemoryAction action)
    {
        if (!IsEnabled)
        {
            return;
        }

        var shape = QueryShapeFingerprint.Compute(sql, provider);
        lock (Gate)
        {
            _store?.RecordFeedback(shape, provider, category, title, action);
        }
    }

    public static void RecordSchemaFeedback(
        string provider,
        string tableName,
        string columnName,
        string category,
        string title,
        QueryHeuristicMemoryAction action)
    {
        RecordFeedback(provider, BuildSchemaFeedbackKey(tableName, columnName), category, title, action);
    }

    public static SchemaAuditResult ApplyToSchemaAudit(SchemaAuditResult result, string provider)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!IsEnabled)
        {
            return EnrichSchemaRecommendations(result, provider, applyMemory: false);
        }

        return EnrichSchemaRecommendations(result, provider, applyMemory: true);
    }

    public static string BuildSchemaFeedbackKey(string tableName, string columnName) =>
        $"schema-audit:{tableName}.{columnName}";

    public static SlowQueryImprovementAnalysis Apply(SlowQueryImprovementAnalysis analysis, string provider)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        if (!IsEnabled)
        {
            return analysis;
        }

        var shape = QueryShapeFingerprint.Compute(analysis.OriginalSql, provider);
        QueryHeuristicMemoryStore? store;
        lock (Gate)
        {
            store = _store;
        }

        if (store is null)
        {
            return analysis;
        }

        var pinned = new List<SlowQueryRecommendation>();
        var ranked = new List<(SlowQueryRecommendation Recommendation, double Score)>();

        foreach (var recommendation in analysis.Recommendations)
        {
            if (IsPinned(recommendation))
            {
                pinned.Add(recommendation);
                continue;
            }

            var score = store.ScoreRecommendation(
                shape,
                provider,
                recommendation.Category.ToString(),
                recommendation.Title);
            ranked.Add((Enrich(recommendation, score), score.Score));
        }

        var ordered = ranked
            .OrderByDescending(r => r.Score)
            .Select(r => r.Recommendation)
            .ToList();

        var merged = pinned.Concat(ordered).ToList();
        var primary = FindPrimaryRewrite(merged)?.PlanDiff ?? analysis.PrimaryPlanDiff;

        return analysis with { Recommendations = merged, PrimaryPlanDiff = primary };
    }

    public static QueryHeuristicMemoryStats GetStats()
    {
        lock (Gate)
        {
            return _store?.GetStats()
                ?? new QueryHeuristicMemoryStats(0, 0, 0, 0, ResolveStorePath(null));
        }
    }

    public static QueryHeuristicWorkloadStats GetWorkloadStats(string? provider = null, int top = 20)
    {
        lock (Gate)
        {
            return _store?.GetWorkloadStats(provider, top)
                ?? new QueryHeuristicWorkloadStats([]);
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            _store?.Clear();
        }
    }

    private static SlowQueryRecommendation Enrich(
        SlowQueryRecommendation recommendation,
        RecommendationHeuristicScore score)
    {
        if (score.Hint is null && score.Score == 0)
        {
            return recommendation;
        }

        var description = recommendation.Description;
        if (!string.IsNullOrWhiteSpace(score.Hint))
        {
            description = $"{description} {score.Hint}";
        }

        return recommendation with
        {
            Description = description,
            HeuristicScore = score.Score,
            HeuristicHint = score.Hint,
        };
    }

    private static bool IsPinned(SlowQueryRecommendation recommendation) =>
        recommendation.Title.Contains("Historical workload", StringComparison.OrdinalIgnoreCase) ||
        recommendation.Title.Contains("pg_stat_statements", StringComparison.OrdinalIgnoreCase);

    private static SlowQueryRecommendation? FindPrimaryRewrite(IReadOnlyList<SlowQueryRecommendation> recommendations)
    {
        foreach (var recommendation in recommendations)
        {
            if (!string.IsNullOrWhiteSpace(recommendation.SuggestedSql) ||
                !string.IsNullOrWhiteSpace(recommendation.SuggestedIndexSql))
            {
                return recommendation;
            }
        }

        return null;
    }

    private static SchemaAuditResult EnrichSchemaRecommendations(
        SchemaAuditResult result,
        string provider,
        bool applyMemory)
    {
        QueryHeuristicMemoryStore? store = null;
        if (applyMemory)
        {
            lock (Gate)
            {
                store = _store;
            }
        }

        var missingIndexes = ProcessMissingIndexRecommendations(result.MissingIndexes, provider, store);
        var foreignKeys = ProcessForeignKeyRecommendations(result.ForeignKeyIssues, provider, store);

        return result with
        {
            MissingIndexes = missingIndexes,
            ForeignKeyIssues = foreignKeys,
        };
    }

    private static List<MissingIndexFinding> ProcessMissingIndexRecommendations(
        IReadOnlyList<MissingIndexFinding> findings,
        string provider,
        QueryHeuristicMemoryStore? store)
    {
        if (findings.Count == 0)
        {
            return [];
        }

        var ranked = new List<(MissingIndexFinding Finding, double Score)>();
        foreach (var finding in findings)
        {
            var title = $"Missing index on {finding.TableName}.{finding.ColumnName}";
            var feedbackKey = BuildSchemaFeedbackKey(finding.TableName, finding.ColumnName);
            RecommendationHeuristicScore? score = null;
            if (store is not null)
            {
                var shape = QueryShapeFingerprint.Compute(feedbackKey, provider);
                score = store.ScoreRecommendation(shape, provider, SchemaHeuristicCategories.MissingIndex, title);
                if (score.DismissedCount >= 1)
                {
                    continue;
                }
            }

            ranked.Add((
                EnrichMissingIndexFinding(finding, title, feedbackKey, score),
                (score?.Score ?? 0) + (finding.SessionRelevanceScore ?? 0)));
        }

        return ranked
            .OrderByDescending(entry => entry.Score)
            .Select(entry => entry.Finding)
            .ToList();
    }

    private static List<ForeignKeyFinding> ProcessForeignKeyRecommendations(
        IReadOnlyList<ForeignKeyFinding> findings,
        string provider,
        QueryHeuristicMemoryStore? store)
    {
        if (findings.Count == 0)
        {
            return [];
        }

        var ranked = new List<(ForeignKeyFinding Finding, double Score)>();
        foreach (var finding in findings)
        {
            var title = $"Index hint for FK {finding.TableName}.{finding.ColumnName}";
            var feedbackKey = BuildSchemaFeedbackKey(finding.TableName, finding.ColumnName);
            RecommendationHeuristicScore? score = null;
            if (store is not null)
            {
                var shape = QueryShapeFingerprint.Compute(feedbackKey, provider);
                score = store.ScoreRecommendation(shape, provider, SchemaHeuristicCategories.ForeignKey, title);
                if (score.DismissedCount >= 1)
                {
                    continue;
                }
            }

            ranked.Add((
                EnrichForeignKeyFinding(finding, title, feedbackKey, score),
                (score?.Score ?? 0) + (finding.SessionRelevanceScore ?? 0)));
        }

        return ranked
            .OrderByDescending(entry => entry.Score)
            .Select(entry => entry.Finding)
            .ToList();
    }

    private static MissingIndexFinding EnrichMissingIndexFinding(
        MissingIndexFinding finding,
        string title,
        string feedbackKey,
        RecommendationHeuristicScore? score)
    {
        var hint = score?.Hint;
        var description = finding.Message;
        if (!string.IsNullOrWhiteSpace(hint))
        {
            description = $"{description} {hint}";
        }

        return finding with
        {
            Message = description,
            FeedbackKey = feedbackKey,
            FeedbackCategory = SchemaHeuristicCategories.MissingIndex,
            FeedbackTitle = title,
            HeuristicScore = score?.Score,
            HeuristicHint = hint,
        };
    }

    private static ForeignKeyFinding EnrichForeignKeyFinding(
        ForeignKeyFinding finding,
        string title,
        string feedbackKey,
        RecommendationHeuristicScore? score)
    {
        var hint = score?.Hint;
        var description = finding.Message;
        if (!string.IsNullOrWhiteSpace(hint))
        {
            description = $"{description} {hint}";
        }

        return finding with
        {
            Message = description,
            FeedbackKey = feedbackKey,
            FeedbackCategory = SchemaHeuristicCategories.ForeignKey,
            FeedbackTitle = title,
            HeuristicScore = score?.Score,
            HeuristicHint = hint,
        };
    }

    private static string ResolveStorePath(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.StartsWith('~')
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    configured.TrimStart('~', '/', '\\'))
                : configured;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".queryduck",
            "memory.db");
    }
}
