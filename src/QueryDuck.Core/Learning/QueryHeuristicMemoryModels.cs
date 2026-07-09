namespace QueryDuck.Core.Learning;

public sealed record QueryHeuristicMemoryFeedback(
    string ShapeHash,
    string Provider,
    string Category,
    string Title,
    QueryHeuristicMemoryAction Action,
    DateTimeOffset RecordedAt);

public sealed record QueryHeuristicMemoryStats(
    int FeedbackCount,
    int DistinctShapes,
    int CopiedCount,
    int DismissedCount,
    string StorePath);

public sealed record QueryWorkloadShapeStats(
    string ShapeHash,
    string Provider,
    int CaptureCount,
    double TotalDurationMs,
    double MaxDurationMs,
    double AverageDurationMs);

public sealed record QueryHeuristicWorkloadStats(
    IReadOnlyList<QueryWorkloadShapeStats> Shapes);

public sealed record RecommendationHeuristicScore(
    double Score,
    int CopiedCount,
    int SelectedCount,
    int DismissedCount,
    string? Hint);
