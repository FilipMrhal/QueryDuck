namespace QueryDuck.Core.Adapters;

public sealed record QueryHistoricalStatsInsight(
    long Calls,
    double MeanExecTimeMs,
    double TotalExecTimeMs,
    long Rows,
    double? CacheHitRatio,
    string? MatchedQueryText = null,
    string? SourceView = null);

public sealed record PgStatStatementInsight(
    long Calls,
    double MeanExecTimeMs,
    double TotalExecTimeMs,
    long Rows,
    double SharedBlocksHitRatio,
    string? MatchedQueryText = null)
{
    public static PgStatStatementInsight FromHistoricalStats(QueryHistoricalStatsInsight insight)
    {
        ArgumentNullException.ThrowIfNull(insight);
        return new PgStatStatementInsight(
            insight.Calls,
            insight.MeanExecTimeMs,
            insight.TotalExecTimeMs,
            insight.Rows,
            insight.CacheHitRatio ?? 0,
            insight.MatchedQueryText);
    }
}

public sealed record ColumnStatistics(
    string SchemaName,
    string TableName,
    string ColumnName,
    double? DistinctCount,
    double? DistinctFraction,
    double NullFraction,
    int AverageWidth,
    double? Correlation);
