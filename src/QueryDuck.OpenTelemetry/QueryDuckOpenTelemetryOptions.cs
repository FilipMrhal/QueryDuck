namespace QueryDuck.OpenTelemetry;

public sealed class QueryDuckOpenTelemetryOptions
{
    public string ActivitySourceName { get; set; } = "QueryDuck";

    public bool IncludeSqlText { get; set; }

    public bool IncludeParameters { get; set; }

    public bool IncludeDiagnostics { get; set; } = true;

    public bool RecordSuccessfulQueries { get; set; }

    public bool RecordSlowQueriesOnly { get; set; } = true;
}
