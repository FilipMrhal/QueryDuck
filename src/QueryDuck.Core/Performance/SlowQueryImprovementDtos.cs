using QueryDuck.Core.Adapters;

namespace QueryDuck.Core.Performance;

public sealed record PgStatStatementInsightDto(
    long Calls,
    double MeanExecTimeMs,
    double TotalExecTimeMs,
    long Rows,
    double SharedBlocksHitRatio,
    string? MatchedQueryText = null);

public sealed record PlanStepSummaryDto(
    string Operation,
    string? ObjectName = null,
    string? Detail = null,
    double? Cost = null);

public sealed record PlanDiffVisualizationDto(
    IReadOnlyList<PlanStepSummaryDto> OriginalSteps,
    IReadOnlyList<PlanStepSummaryDto> ImprovedSteps,
    IReadOnlyList<string> SummaryLines,
    string TextDiff,
    string? OriginalMermaid = null,
    string? ImprovedMermaid = null,
    string? SideBySideMermaid = null);

public sealed record SlowQueryRecommendationDto(
    string Category,
    string Title,
    string Description,
    string? SuggestedSql = null,
    string? SuggestedIndexSql = null,
    string? ImprovedPlanText = null,
    PlanDiffVisualizationDto? PlanDiff = null);

public sealed record SlowQueryImprovementAnalysisDto(
    string EventId,
    double DurationMs,
    string OriginalSql,
    IReadOnlyList<SlowQueryRecommendationDto> Recommendations,
    PlanDiffVisualizationDto? PrimaryPlanDiff = null,
    PgStatStatementInsightDto? PgStatStatements = null);

internal static class SlowQueryImprovementMapping
{
    public static SlowQueryImprovementAnalysisDto ToDto(this SlowQueryImprovementAnalysis analysis) =>
        new(
            analysis.EventId,
            analysis.DurationMs,
            analysis.OriginalSql,
            analysis.Recommendations.Select(r => r.ToDto()).ToArray(),
            analysis.PrimaryPlanDiff?.ToDto(),
            analysis.PgStatStatements?.ToDto());

    private static PgStatStatementInsightDto ToDto(this PgStatStatementInsight insight) =>
        new(
            insight.Calls,
            insight.MeanExecTimeMs,
            insight.TotalExecTimeMs,
            insight.Rows,
            insight.SharedBlocksHitRatio,
            insight.MatchedQueryText);

    private static SlowQueryRecommendationDto ToDto(this SlowQueryRecommendation recommendation) =>
        new(
            recommendation.Category.ToString(),
            recommendation.Title,
            recommendation.Description,
            recommendation.SuggestedSql,
            recommendation.SuggestedIndexSql,
            recommendation.ImprovedPlanText,
            recommendation.PlanDiff?.ToDto());

    private static PlanDiffVisualizationDto ToDto(this PlanDiffVisualization visualization) =>
        new(
            visualization.OriginalSteps.Select(s => s.ToDto()).ToArray(),
            visualization.ImprovedSteps.Select(s => s.ToDto()).ToArray(),
            visualization.SummaryLines,
            visualization.TextDiff,
            visualization.OriginalMermaid,
            visualization.ImprovedMermaid,
            visualization.SideBySideMermaid);

    private static PlanStepSummaryDto ToDto(this PlanStepSummary step) =>
        new(step.Operation, step.ObjectName, step.Detail, step.Cost);
}
