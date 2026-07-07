using QueryDuck.Core.Capture;

namespace QueryDuck.Tests;

public sealed class SessionComparerTests
{
    [Fact]
    public void Compare_reports_deltas_against_baseline()
    {
        QueryDuckSessionComparer.ClearBaseline();
        var options = new QueryCaptureOptions { SlowQueryThresholdMs = 100 };

        var baseline = new QueryDuckSessionSnapshot(
            DateTimeOffset.UtcNow,
            EventCount: 2,
            SlowQueryCount: 0,
            FailureCount: 0,
            DiagnosticWarningCount: 1,
            EventsByProvider: new Dictionary<string, int> { ["PostgreSql"] = 2 },
            DiagnosticsByRule: new Dictionary<string, int> { ["QD001"] = 1 },
            SessionWarnings: ["baseline warning"]);

        var current = new QueryDuckSessionSnapshot(
            DateTimeOffset.UtcNow,
            EventCount: 5,
            SlowQueryCount: 2,
            FailureCount: 1,
            DiagnosticWarningCount: 3,
            EventsByProvider: new Dictionary<string, int> { ["PostgreSql"] = 4, ["Sqlite"] = 1 },
            DiagnosticsByRule: new Dictionary<string, int> { ["QD001"] = 2, ["QD010"] = 1 },
            SessionWarnings: ["baseline warning", "new warning"]);

        QueryDuckSessionComparer.SetBaseline(baseline);
        var comparison = QueryDuckSessionComparer.Compare(current);

        Assert.Equal(3, comparison.EventCountDelta);
        Assert.Equal(2, comparison.SlowQueryCountDelta);
        Assert.Equal(1, comparison.FailureCountDelta);
        Assert.Equal(2, comparison.DiagnosticWarningCountDelta);
        Assert.Contains("new warning", comparison.NewSessionWarnings);
        Assert.Equal(2, comparison.ProviderCountDeltas["PostgreSql"]);
        Assert.Equal(1, comparison.ProviderCountDeltas["Sqlite"]);
        Assert.Equal(1, comparison.RuleCountDeltas["QD010"]);
    }
}
