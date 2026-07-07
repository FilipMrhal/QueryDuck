using QueryDuck.Core.Adapters;

namespace QueryDuck.Tests;

public sealed class HistoricalStatsMatcherTests
{
    [Fact]
    public void QueryHistoricalStatsSqlMatcher_matches_normalized_queries()
    {
        const string captured = "SELECT * FROM orders WHERE customer_id = @p0";
        const string historical = "SELECT * FROM orders WHERE customer_id = ?";

        Assert.True(QueryHistoricalStatsSqlMatcher.IsLikelyMatch(captured, historical));
    }

    [Fact]
    public void PgStatStatementSqlMatcher_alias_delegates_to_historical_matcher()
    {
        const string captured = "SELECT id FROM customers WHERE region = @p0";
        const string historical = "select id from customers where region = $1";

        Assert.True(PgStatStatementSqlMatcher.IsLikelyMatch(captured, historical));
    }

    [Theory]
    [InlineData("", "SELECT 1")]
    [InlineData("SELECT 1", "")]
    public void QueryHistoricalStatsSqlMatcher_rejects_empty_inputs(string left, string right)
    {
        Assert.False(QueryHistoricalStatsSqlMatcher.IsLikelyMatch(left, right));
    }
}
