using System.Text.Json;
using System.Text.Json.Serialization;

namespace QueryDuck.Client;

public sealed class QueryDuckSchemaAuditSnapshotDto
{
    [JsonPropertyName("capturedAt")]
    public string CapturedAt { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("result")]
    public JsonElement Result { get; set; }

    [JsonPropertyName("hasIssues")]
    public bool HasIssues { get; set; }
}

public sealed class QueryDuckSessionSnapshotDto
{
    [JsonPropertyName("capturedAt")]
    public string CapturedAt { get; set; } = string.Empty;

    [JsonPropertyName("eventCount")]
    public int EventCount { get; set; }

    [JsonPropertyName("slowQueryCount")]
    public int SlowQueryCount { get; set; }

    [JsonPropertyName("failureCount")]
    public int FailureCount { get; set; }

    [JsonPropertyName("diagnosticWarningCount")]
    public int DiagnosticWarningCount { get; set; }

    [JsonPropertyName("eventsByProvider")]
    public Dictionary<string, int> EventsByProvider { get; set; } = new();

    [JsonPropertyName("diagnosticsByRule")]
    public Dictionary<string, int> DiagnosticsByRule { get; set; } = new();

    [JsonPropertyName("sessionWarnings")]
    public List<string> SessionWarnings { get; set; } = new();
}

public sealed class QueryDuckSessionComparisonDto
{
    [JsonPropertyName("baseline")]
    public QueryDuckSessionSnapshotDto Baseline { get; set; } = new();

    [JsonPropertyName("current")]
    public QueryDuckSessionSnapshotDto Current { get; set; } = new();

    [JsonPropertyName("eventCountDelta")]
    public int EventCountDelta { get; set; }

    [JsonPropertyName("slowQueryCountDelta")]
    public int SlowQueryCountDelta { get; set; }

    [JsonPropertyName("failureCountDelta")]
    public int FailureCountDelta { get; set; }

    [JsonPropertyName("diagnosticWarningCountDelta")]
    public int DiagnosticWarningCountDelta { get; set; }

    [JsonPropertyName("newSessionWarnings")]
    public List<string> NewSessionWarnings { get; set; } = new();

    [JsonPropertyName("resolvedSessionWarnings")]
    public List<string> ResolvedSessionWarnings { get; set; } = new();

    [JsonPropertyName("providerCountDeltas")]
    public Dictionary<string, int> ProviderCountDeltas { get; set; } = new();

    [JsonPropertyName("ruleCountDeltas")]
    public Dictionary<string, int> RuleCountDeltas { get; set; } = new();
}
