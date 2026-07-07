namespace QueryDuck.Core.Learning;

public sealed record QueryHeuristicMemoryFeedbackRequest(
    string Provider,
    string Sql,
    string Category,
    string Title,
    string Action);
