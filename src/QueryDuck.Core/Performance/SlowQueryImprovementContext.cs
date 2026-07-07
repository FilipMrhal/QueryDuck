using QueryDuck.Core.Adapters;

namespace QueryDuck.Core.Performance;

public sealed record SlowQueryImprovementContext(
    PgStatStatementInsight? PgStatStatements = null,
    QueryHistoricalStatsInsight? HistoricalStats = null,
    IReadOnlyDictionary<string, IReadOnlyList<ColumnStatistics>>? TableStatistics = null,
    bool EmitMermaidPlanGraphs = false);
