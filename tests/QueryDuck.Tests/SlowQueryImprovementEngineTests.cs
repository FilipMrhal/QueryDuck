using QueryDuck.Core.Capture;
using QueryDuck.Core.Performance;

namespace QueryDuck.Tests;

public sealed class SlowQueryImprovementEngineTests
{
    [Fact]
    public void Analyze_SelectStarAndFullScan_SuggestsIndexAndRewrite()
    {
        var captureEvent = new QueryCaptureEvent
        {
            EventId = "slow-1",
            Timestamp = DateTimeOffset.UtcNow,
            Sql = "SELECT * FROM ORDERS o INNER JOIN CUSTOMERS c ON c.ID = o.CUSTOMER_ID WHERE c.REGION = @p0",
            Provider = "PostgreSql",
            Duration = TimeSpan.FromMilliseconds(900),
            ExecutionPlan = "Seq Scan on orders  (cost=0.00..1500.00 rows=50000 width=120)",
        };

        var analysis = SlowQueryImprovementEngine.Analyze(captureEvent);

        Assert.True(analysis.DurationMs >= 900);
        Assert.Contains(analysis.Recommendations, r => r.Category == SlowQueryImprovementCategory.IndexCreation);
        Assert.Contains(analysis.Recommendations, r => r.Category == SlowQueryImprovementCategory.ManualRewrite);
        Assert.NotNull(analysis.Recommendations.First(r => r.Category == SlowQueryImprovementCategory.ManualRewrite).SuggestedSql);
    }

    [Fact]
    public void AnalyzeIfSlow_ReturnsNullForFastQueries()
    {
        var captureEvent = new QueryCaptureEvent
        {
            EventId = "fast-1",
            Timestamp = DateTimeOffset.UtcNow,
            Sql = "SELECT 1",
            Provider = "Oracle",
            Duration = TimeSpan.FromMilliseconds(10),
        };

        Assert.Null(SlowQueryImprovementEngine.AnalyzeIfSlow(captureEvent, slowQueryThresholdMs: 500));
    }

    [Fact]
    public void PlanDiffBuilder_HighlightsFullScanToIndexImprovement()
    {
        var diff = PlanDiffBuilder.Build(
            "Seq Scan on orders  (cost=0.00..1500.00 rows=50000 width=120)",
            "Index Scan using ix_orders_customer on orders  (cost=0.42..120.00 rows=50 width=40)");

        Assert.True(
            diff.SummaryLines.Any(line => line.Contains("cost reduction", StringComparison.OrdinalIgnoreCase)) ||
            diff.SummaryLines.Any(line => line.Contains("FULL SCAN", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("INDEX SCAN", StringComparison.OrdinalIgnoreCase)),
            string.Join("; ", diff.SummaryLines));
        Assert.Contains("Original plan", diff.TextDiff, StringComparison.Ordinal);
        Assert.Contains("Improved plan", diff.TextDiff, StringComparison.Ordinal);
    }

    [Fact]
    public void Analyze_OrPredicate_SuggestsUnionRewrite()
    {
        var captureEvent = new QueryCaptureEvent
        {
            EventId = "or-1",
            Timestamp = DateTimeOffset.UtcNow,
            Sql = "SELECT ID, NAME FROM CUSTOMERS WHERE REGION = @p0 OR CODE = @p1",
            Provider = "SqlServer",
            Duration = TimeSpan.FromMilliseconds(800),
            ExecutionPlan = "Clustered Index Scan CUSTOMERS  (cost=0.00..900.00 rows=10000)",
        };

        var analysis = SlowQueryImprovementEngine.Analyze(captureEvent);

        Assert.Contains(analysis.Recommendations, r =>
            r.Category == SlowQueryImprovementCategory.ManualRewrite &&
            r.SuggestedSql?.Contains("UNION ALL", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void ToDto_RoundTripsRecommendationCategories()
    {
        var analysis = SlowQueryImprovementEngine.Analyze(new QueryCaptureEvent
        {
            EventId = "dto-1",
            Timestamp = DateTimeOffset.UtcNow,
            Sql = "SELECT * FROM CUSTOMERS WHERE NAME LIKE '%ACME%'",
            Provider = "Oracle",
            Duration = TimeSpan.FromMilliseconds(1200),
            ExecutionPlan = "TABLE ACCESS FULL CUSTOMERS",
        });

        var dto = analysis.ToDto();
        Assert.Equal("dto-1", dto.EventId);
        Assert.NotEmpty(dto.Recommendations);
        Assert.Contains(dto.Recommendations, r => r.Category == nameof(SlowQueryImprovementCategory.ManualRewrite));
    }
}
