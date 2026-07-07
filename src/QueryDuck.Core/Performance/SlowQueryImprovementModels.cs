namespace QueryDuck.Core.Performance;

using QueryDuck.Core.Adapters;

public enum SlowQueryImprovementCategory
{
    IndexCreation,
    SchemaSeparation,
    UseCte,
    ManualRewrite,
    ApplicationChange,
}

public sealed record PlanStepSummary(
    string Operation,
    string? ObjectName = null,
    string? Detail = null,
    double? Cost = null);

public sealed record PlanDiffVisualization(
    IReadOnlyList<PlanStepSummary> OriginalSteps,
    IReadOnlyList<PlanStepSummary> ImprovedSteps,
    IReadOnlyList<string> SummaryLines,
    string TextDiff,
    string? OriginalMermaid = null,
    string? ImprovedMermaid = null,
    string? SideBySideMermaid = null);

public sealed record SlowQueryRecommendation(
    SlowQueryImprovementCategory Category,
    string Title,
    string Description,
    string? SuggestedSql = null,
    string? SuggestedIndexSql = null,
    string? ImprovedPlanText = null,
    PlanDiffVisualization? PlanDiff = null);

public sealed record SlowQueryImprovementAnalysis(
    string EventId,
    double DurationMs,
    string OriginalSql,
    IReadOnlyList<SlowQueryRecommendation> Recommendations,
    PlanDiffVisualization? PrimaryPlanDiff = null,
    PgStatStatementInsight? PgStatStatements = null);
