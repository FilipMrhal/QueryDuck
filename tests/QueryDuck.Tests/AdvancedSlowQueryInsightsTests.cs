using QueryDuck.Core.Adapters;
using QueryDuck.Core.Capture;
using QueryDuck.Core.Performance;
using QueryDuck.Core.Providers;

namespace QueryDuck.Tests;

public sealed class AdvancedSlowQueryInsightsTests
{
    [Fact]
    public void PgStatStatementSqlMatcher_matches_normalized_queries()
    {
        const string captured = "SELECT * FROM orders WHERE customer_id = @p0";
        const string pgStat = "SELECT * FROM orders WHERE customer_id = $1";

        Assert.True(PgStatStatementSqlMatcher.IsLikelyMatch(captured, pgStat));
    }

    [Fact]
    public void StatisticsIndexRecommendationEngine_builds_partial_index_for_sparse_column()
    {
        var stats = new[]
        {
            new ColumnStatistics("public", "orders", "customer_id", 5000, 0.4, 0.02, 8, 0.1),
            new ColumnStatistics("public", "orders", "notes", 100, 0.01, 0.82, 256, 0.0),
        };

        var recommendation = StatisticsIndexRecommendationEngine.RecommendFromStatistics(
            DatabaseProvider.PostgreSql,
            "orders",
            ["customer_id"],
            [],
            stats);

        Assert.NotNull(recommendation);
        Assert.Contains("CREATE INDEX", recommendation!.SuggestedIndexSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customer_id", recommendation.SuggestedIndexSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("statistics", recommendation.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlanMermaidGraphBuilder_emits_side_by_side_flowchart()
    {
        var original = new[] { new PlanStepSummary("SEQ SCAN", "orders", Cost: 1500) };
        var improved = new[] { new PlanStepSummary("INDEX SCAN", "ix_orders_customer", Cost: 120) };

        var sideBySide = PlanMermaidGraphBuilder.BuildSideBySideComparison(original, improved);

        Assert.Contains("subgraph original", sideBySide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("subgraph improved", sideBySide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INDEX SCAN", sideBySide, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_withMermaidOption_includes_graphs_in_plan_diff()
    {
        var captureEvent = new QueryCaptureEvent
        {
            EventId = "mermaid-1",
            Timestamp = DateTimeOffset.UtcNow,
            Sql = "SELECT * FROM ORDERS WHERE CUSTOMER_ID = @p0",
            Provider = "PostgreSql",
            Duration = TimeSpan.FromMilliseconds(900),
            ExecutionPlan = "Seq Scan on orders  (cost=0.00..1500.00 rows=50000 width=120)",
        };

        var analysis = SlowQueryImprovementEngine.Analyze(
            captureEvent,
            new SlowQueryImprovementContext(EmitMermaidPlanGraphs: true));

        var rewrite = analysis.Recommendations.FirstOrDefault(r => r.PlanDiff is not null);
        Assert.NotNull(rewrite?.PlanDiff?.OriginalMermaid);
        Assert.NotNull(rewrite?.PlanDiff?.ImprovedMermaid);
        Assert.NotNull(rewrite?.PlanDiff?.SideBySideMermaid);
    }

    [Fact]
    public void Analyze_withPgStatInsight_adds_historical_recommendation()
    {
        var captureEvent = new QueryCaptureEvent
        {
            EventId = "pgstat-1",
            Timestamp = DateTimeOffset.UtcNow,
            Sql = "SELECT ID FROM ORDERS WHERE CUSTOMER_ID = @p0",
            Provider = "PostgreSql",
            Duration = TimeSpan.FromMilliseconds(1200),
            ExecutionPlan = "Seq Scan on orders  (cost=0.00..1500.00 rows=50000 width=120)",
        };

        var context = new SlowQueryImprovementContext(
            new PgStatStatementInsight(42, 850, 35700, 9000, 0.93, "select id from orders where customer_id = $1"));

        var analysis = SlowQueryImprovementEngine.Analyze(captureEvent, context);

        Assert.NotNull(analysis.PgStatStatements);
        Assert.Contains(analysis.Recommendations, r => r.Title.Contains("pg_stat_statements", StringComparison.OrdinalIgnoreCase));
    }
}
