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
    string? MatchedQueryText = null);

public sealed record ColumnStatistics(
    string SchemaName,
    string TableName,
    string ColumnName,
    double? DistinctCount,
    double? DistinctFraction,
    double NullFraction,
    int AverageWidth,
    double? Correlation);
