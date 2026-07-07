using System.Text.Json.Serialization;

namespace QueryDuck.Client;

public sealed class QueryDiagnosticDto
{
    [JsonPropertyName("ruleId")]
    public string RuleId { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("fixHint")]
    public string? FixHint { get; set; }

    public override string ToString()
    {
        var text = $"[{RuleId}] {Message}";
        return string.IsNullOrWhiteSpace(FixHint) ? text : $"{text} — {FixHint}";
    }
}

public sealed class ExpressionTreeNodeDto
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("children")]
    public List<ExpressionTreeNodeDto>? Children { get; set; }
}

public sealed class QueryCaptureEventDto
{
    [JsonPropertyName("eventId")]
    public string EventId { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("sql")]
    public string Sql { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = "EfCore";

    [JsonPropertyName("bulkOperation")]
    public string? BulkOperation { get; set; }

    [JsonPropertyName("tag")]
    public string? Tag { get; set; }

    [JsonPropertyName("caller")]
    public string? Caller { get; set; }

    [JsonPropertyName("duration")]
    public string? Duration { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object?> Parameters { get; set; } = new();

    [JsonPropertyName("diagnostics")]
    public List<QueryDiagnosticDto> Diagnostics { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    [JsonPropertyName("expressionTreeText")]
    public string? ExpressionTreeText { get; set; }

    [JsonPropertyName("expressionCSharp")]
    public string? ExpressionCSharp { get; set; }

    [JsonPropertyName("expressionTree")]
    public ExpressionTreeNodeDto? ExpressionTree { get; set; }

    [JsonPropertyName("executionPlan")]
    public string? ExecutionPlan { get; set; }

    [JsonPropertyName("planHash")]
    public string? PlanHash { get; set; }

    [JsonPropertyName("improvementAnalysis")]
    public SlowQueryImprovementAnalysisDto? ImprovementAnalysis { get; set; }

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 6;

    public bool Succeeded { get; set; } = true;

    public string? ErrorMessage { get; set; }

    public string? ExceptionType { get; set; }

    public int WarningCount =>
        Diagnostics.Count(d =>
            string.Equals(d.Severity, "Warning", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(d.Severity, "Error", StringComparison.OrdinalIgnoreCase));

    public string SqlPreview(int maxLength = 80)
    {
        var singleLine = string.Join(" ", Sql.Split('\n').Select(line => line.Trim())).Trim();
        return singleLine.Length <= maxLength ? singleLine : singleLine.Substring(0, maxLength - 1) + "…";
    }

    public string FormattedTime()
    {
        var afterT = Timestamp.Contains("T", StringComparison.Ordinal) ? Timestamp.Split('T')[1] : Timestamp;
        var dot = afterT.IndexOf('.');
        return dot >= 0 ? afterT.Substring(0, dot) : afterT;
    }

    public string FormattedDuration()
    {
        if (string.IsNullOrWhiteSpace(Duration))
        {
            return "—";
        }

        var dot = Duration.IndexOf('.');
        return dot >= 0 ? Duration.Substring(0, dot) : Duration;
    }

    public string MetaSourceLabel()
    {
        if (string.Equals(Source, "EntityFrameworkExtensions", StringComparison.OrdinalIgnoreCase))
        {
            var op = BulkOperation ?? Caller ?? "Bulk";
            return $"EF Extensions · {op}";
        }

        return Caller ?? "Unknown source";
    }
}

public sealed class PgStatStatementInsightDto
{
    [JsonPropertyName("calls")]
    public long Calls { get; set; }

    [JsonPropertyName("meanExecTimeMs")]
    public double MeanExecTimeMs { get; set; }

    [JsonPropertyName("totalExecTimeMs")]
    public double TotalExecTimeMs { get; set; }

    [JsonPropertyName("rows")]
    public long Rows { get; set; }

    [JsonPropertyName("sharedBlocksHitRatio")]
    public double SharedBlocksHitRatio { get; set; }

    [JsonPropertyName("matchedQueryText")]
    public string? MatchedQueryText { get; set; }
}

public sealed class PlanStepSummaryDto
{
    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("objectName")]
    public string? ObjectName { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("cost")]
    public double? Cost { get; set; }
}

public sealed class PlanDiffVisualizationDto
{
    [JsonPropertyName("originalSteps")]
    public List<PlanStepSummaryDto> OriginalSteps { get; set; } = new();

    [JsonPropertyName("improvedSteps")]
    public List<PlanStepSummaryDto> ImprovedSteps { get; set; } = new();

    [JsonPropertyName("summaryLines")]
    public List<string> SummaryLines { get; set; } = new();

    [JsonPropertyName("textDiff")]
    public string TextDiff { get; set; } = string.Empty;

    [JsonPropertyName("originalMermaid")]
    public string? OriginalMermaid { get; set; }

    [JsonPropertyName("improvedMermaid")]
    public string? ImprovedMermaid { get; set; }

    [JsonPropertyName("sideBySideMermaid")]
    public string? SideBySideMermaid { get; set; }
}

public sealed class SlowQueryRecommendationDto
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("suggestedSql")]
    public string? SuggestedSql { get; set; }

    [JsonPropertyName("suggestedIndexSql")]
    public string? SuggestedIndexSql { get; set; }

    [JsonPropertyName("improvedPlanText")]
    public string? ImprovedPlanText { get; set; }

    [JsonPropertyName("planDiff")]
    public PlanDiffVisualizationDto? PlanDiff { get; set; }

    public string ListLabel => $"[{Category}] {Title}";
}

public sealed class SlowQueryImprovementAnalysisDto
{
    [JsonPropertyName("eventId")]
    public string EventId { get; set; } = string.Empty;

    [JsonPropertyName("durationMs")]
    public double DurationMs { get; set; }

    [JsonPropertyName("originalSql")]
    public string OriginalSql { get; set; } = string.Empty;

    [JsonPropertyName("recommendations")]
    public List<SlowQueryRecommendationDto> Recommendations { get; set; } = new();

    [JsonPropertyName("primaryPlanDiff")]
    public PlanDiffVisualizationDto? PrimaryPlanDiff { get; set; }

    [JsonPropertyName("pgStatStatements")]
    public PgStatStatementInsightDto? PgStatStatements { get; set; }
}

public sealed class HealthResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("sessionWarnings")]
    public List<string> SessionWarnings { get; set; } = new();
}
