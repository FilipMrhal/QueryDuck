namespace QueryDuck.Core.Adapters;

/// <summary>
/// Backward-compatible alias for <see cref="QueryHistoricalStatsSqlMatcher"/>.
/// </summary>
public static class PgStatStatementSqlMatcher
{
    public static string NormalizeForMatch(string sql) =>
        QueryHistoricalStatsSqlMatcher.NormalizeForMatch(sql);

    public static bool IsLikelyMatch(string capturedSql, string pgStatQueryText) =>
        QueryHistoricalStatsSqlMatcher.IsLikelyMatch(capturedSql, pgStatQueryText);
}
