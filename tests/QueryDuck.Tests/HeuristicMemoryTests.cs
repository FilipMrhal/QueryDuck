using QueryDuck.Core.Adapters;
using QueryDuck.Core.Capture;
using QueryDuck.Core.Learning;
using QueryDuck.Core.Performance;

namespace QueryDuck.Tests;

public sealed class HeuristicMemoryTests : IDisposable
{
    private readonly string _storePath;

    public HeuristicMemoryTests()
    {
        _storePath = Path.Combine(Path.GetTempPath(), $"queryduck-memory-{Guid.NewGuid():N}.db");
        QueryHeuristicMemory.Configure(new QueryCaptureOptions
        {
            EnableHeuristicMemory = true,
            HeuristicMemoryStorePath = _storePath,
            HeuristicMemoryMaxEntries = 100,
        });
    }

    public void Dispose()
    {
        QueryHeuristicMemory.Clear();
        QueryHeuristicMemory.Configure(new QueryCaptureOptions { EnableHeuristicMemory = false });
        if (File.Exists(_storePath))
        {
            File.Delete(_storePath);
        }
    }

    [Fact]
    public void ShapeFingerprint_IsStableForEquivalentSql()
    {
        var first = QueryShapeFingerprint.Compute("SELECT * FROM Users WHERE Id = @p0", "PostgreSql");
        var second = QueryShapeFingerprint.Compute("SELECT * FROM Users WHERE Id = @p1", "PostgreSql");

        Assert.Equal(first, second);
    }

    [Fact]
    public void ShapeFingerprint_DiffersAcrossProviders()
    {
        var sql = "SELECT * FROM Users WHERE Id = @p0";
        var postgres = QueryShapeFingerprint.Compute(sql, "PostgreSql");
        var sqlServer = QueryShapeFingerprint.Compute(sql, "SqlServer");

        Assert.NotEqual(postgres, sqlServer);
    }

    [Fact]
    public void RecordFeedback_IncreasesRecommendationScore()
    {
        const string provider = "PostgreSql";
        const string sql = "SELECT * FROM Orders WHERE CustomerId = @p0";
        const string category = nameof(SlowQueryImprovementCategory.IndexCreation);
        const string title = "Add index on CustomerId";

        var before = ScoreRecommendation(provider, sql, category, title);
        QueryHeuristicMemory.RecordFeedback(provider, sql, category, title, QueryHeuristicMemoryAction.Copied);
        QueryHeuristicMemory.RecordFeedback(provider, sql, category, title, QueryHeuristicMemoryAction.Copied);
        var after = ScoreRecommendation(provider, sql, category, title);

        Assert.True(after > before);
    }

    [Fact]
    public void Apply_ReordersRecommendationsByLearnedPreference()
    {
        const string provider = "PostgreSql";
        const string sql = "SELECT * FROM Orders WHERE Status = @p0 ORDER BY CreatedAt";

        QueryHeuristicMemory.RecordFeedback(
            provider,
            sql,
            nameof(SlowQueryImprovementCategory.UseCte),
            "Use CTE for readability",
            QueryHeuristicMemoryAction.Copied);
        QueryHeuristicMemory.RecordFeedback(
            provider,
            sql,
            nameof(SlowQueryImprovementCategory.UseCte),
            "Use CTE for readability",
            QueryHeuristicMemoryAction.Copied);

        var analysis = new SlowQueryImprovementAnalysis(
            "evt-1",
            1200,
            sql,
            [
                new SlowQueryRecommendation(
                    SlowQueryImprovementCategory.IndexCreation,
                    "Add index on Status",
                    "Filter column may benefit from an index."),
                new SlowQueryRecommendation(
                    SlowQueryImprovementCategory.UseCte,
                    "Use CTE for readability",
                    "Consider a CTE for the filtered subset."),
            ]);

        var ranked = QueryHeuristicMemory.Apply(analysis, provider);

        Assert.Equal("Use CTE for readability", ranked.Recommendations[0].Title);
        Assert.True(ranked.Recommendations[0].HeuristicScore > 0);
    }

    [Fact]
    public void Apply_KeepsHistoricalWorkloadPinnedAtTop()
    {
        const string provider = "PostgreSql";
        const string sql = "SELECT * FROM Orders WHERE Status = @p0";

        QueryHeuristicMemory.RecordFeedback(
            provider,
            sql,
            nameof(SlowQueryImprovementCategory.ManualRewrite),
            "Rewrite predicate",
            QueryHeuristicMemoryAction.Copied);
        QueryHeuristicMemory.RecordFeedback(
            provider,
            sql,
            nameof(SlowQueryImprovementCategory.ManualRewrite),
            "Rewrite predicate",
            QueryHeuristicMemoryAction.Copied);

        var analysis = new SlowQueryImprovementAnalysis(
            "evt-2",
            900,
            sql,
            [
                new SlowQueryRecommendation(
                    SlowQueryImprovementCategory.ManualRewrite,
                    "Rewrite predicate",
                    "Try a sargable predicate."),
                new SlowQueryRecommendation(
                    SlowQueryImprovementCategory.ApplicationChange,
                    "Historical workload",
                    "This shape is hot in historical stats."),
            ]);

        var ranked = QueryHeuristicMemory.Apply(analysis, provider);

        Assert.Equal("Historical workload", ranked.Recommendations[0].Title);
    }

    [Fact]
    public void GetStats_ReflectsRecordedFeedback()
    {
        QueryHeuristicMemory.RecordFeedback(
            "SqlServer",
            "SELECT 1",
            nameof(SlowQueryImprovementCategory.IndexCreation),
            "Index A",
            QueryHeuristicMemoryAction.Copied);
        QueryHeuristicMemory.RecordFeedback(
            "SqlServer",
            "SELECT 2",
            nameof(SlowQueryImprovementCategory.IndexCreation),
            "Index B",
            QueryHeuristicMemoryAction.Dismissed);

        var stats = QueryHeuristicMemory.GetStats();

        Assert.Equal(2, stats.FeedbackCount);
        Assert.Equal(2, stats.DistinctShapes);
        Assert.Equal(1, stats.CopiedCount);
        Assert.Equal(1, stats.DismissedCount);
        Assert.Equal(_storePath, stats.StorePath);
    }

    [Fact]
    public void ApplyToSchemaAudit_SuppressesDismissedRecommendations()
    {
        const string provider = "PostgreSql";
        QueryHeuristicMemory.RecordSchemaFeedback(
            provider,
            "ORDERS",
            "CustomerId",
            SchemaHeuristicCategories.MissingIndex,
            "Missing index on ORDERS.CustomerId",
            QueryHeuristicMemoryAction.Dismissed);

        var result = new SchemaAuditResult(
            [],
            [],
            [],
            [new MissingIndexFinding("ORDERS", "CustomerId", "Consider an index on ORDERS.CustomerId")],
            [new ForeignKeyFinding("ORDERS", "CustomerId", "CUSTOMERS", "Verify FK index on ORDERS.CustomerId")]);

        var presented = QueryHeuristicMemory.ApplyToSchemaAudit(result, provider);

        Assert.Empty(presented.MissingIndexes);
        Assert.Single(presented.ForeignKeyIssues);
    }

    [Fact]
    public void ApplyToSchemaAudit_RanksRecommendationsByLearnedPreference()
    {
        const string provider = "PostgreSql";
        QueryHeuristicMemory.RecordSchemaFeedback(
            provider,
            "CUSTOMERS",
            "CustomerId",
            SchemaHeuristicCategories.ForeignKey,
            "Index hint for FK CUSTOMERS.CustomerId",
            QueryHeuristicMemoryAction.Copied);
        QueryHeuristicMemory.RecordSchemaFeedback(
            provider,
            "CUSTOMERS",
            "CustomerId",
            SchemaHeuristicCategories.ForeignKey,
            "Index hint for FK CUSTOMERS.CustomerId",
            QueryHeuristicMemoryAction.Copied);

        var result = new SchemaAuditResult(
            [],
            [],
            [],
            [new MissingIndexFinding("ORDERS", "Status", "Consider an index on ORDERS.Status")],
            [new ForeignKeyFinding("CUSTOMERS", "CustomerId", "CUSTOMERS", "Verify FK index on CUSTOMERS.CustomerId")]);

        var presented = QueryHeuristicMemory.ApplyToSchemaAudit(result, provider);

        Assert.Equal("CUSTOMERS", presented.ForeignKeyIssues[0].TableName);
        Assert.True(presented.ForeignKeyIssues[0].HeuristicScore > 0);
    }

    private static double ScoreRecommendation(string provider, string sql, string category, string title)
    {
        var analysis = QueryHeuristicMemory.Apply(
            new SlowQueryImprovementAnalysis(
                "score",
                1000,
                sql,
                [new SlowQueryRecommendation(
                    SlowQueryImprovementCategory.IndexCreation,
                    title,
                    "Test recommendation.")]),
            provider);

        return analysis.Recommendations[0].HeuristicScore ?? 0;
    }
}
