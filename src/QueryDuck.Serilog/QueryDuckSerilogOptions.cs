using Serilog.Events;

namespace QueryDuck.Serilog;

public enum QueryDuckSensitiveDataMode
{
    /// <summary>Exclude the value from structured log properties.</summary>
    Omit,

    /// <summary>Replace with a fixed redaction token.</summary>
    Redact,

    /// <summary>Replace with a short SHA-256 prefix for correlation without exposing the value.</summary>
    Hash,

    /// <summary>Include the original value (requires explicit sensitive-data opt-in).</summary>
    Include,
}

public sealed class QueryDuckSensitiveDataLoggingOptions
{
    /// <summary>
    /// Master opt-in for logging potentially sensitive payloads such as parameter values,
    /// expression source, execution plans, and suggested rewrite SQL.
    /// Defaults to <c>false</c> (safe-by-default).
    /// </summary>
    public bool IncludeSensitiveData { get; set; }

    /// <summary>
    /// Explicit opt-in for fields that commonly contain PII (emails, names, identifiers).
    /// When <c>false</c>, parameters whose names match <see cref="PiiParameterNamePatterns"/> are always protected.
    /// </summary>
    public bool IncludePii { get; set; }

    /// <summary>Include SQL text in exported logs. Defaults to <c>true</c>.</summary>
    public bool IncludeSqlText { get; set; } = true;

    /// <summary>Include parameter names (never values unless enabled below). Defaults to <c>true</c>.</summary>
    public bool IncludeParameterNames { get; set; } = true;

    /// <summary>Include parameter values. Defaults to <c>false</c>.</summary>
    public bool IncludeParameterValues { get; set; }

    /// <summary>Include captured C# expression source. Defaults to <c>false</c>.</summary>
    public bool IncludeExpressionCSharp { get; set; }

    /// <summary>Include captured expression tree text/nodes. Defaults to <c>false</c>.</summary>
    public bool IncludeExpressionTree { get; set; }

    /// <summary>Include execution plan text. Defaults to <c>false</c>.</summary>
    public bool IncludeExecutionPlan { get; set; }

    /// <summary>Include slow-query recommendation summaries (category/title). Defaults to <c>true</c>.</summary>
    public bool IncludeRecommendationSummaries { get; set; } = true;

    /// <summary>Include suggested rewrite SQL and index DDL. Defaults to <c>false</c>.</summary>
    public bool IncludeSuggestedSql { get; set; }

    /// <summary>Include matched <c>pg_stat_statements</c> query text. Defaults to <c>false</c>.</summary>
    public bool IncludePgStatMatchedQueryText { get; set; }

    /// <summary>How non-PII sensitive values are exported when not fully included.</summary>
    public QueryDuckSensitiveDataMode DefaultMode { get; set; } = QueryDuckSensitiveDataMode.Redact;

    /// <summary>How PII-like parameter values are exported when <see cref="IncludePii"/> is false.</summary>
    public QueryDuckSensitiveDataMode PiiMode { get; set; } = QueryDuckSensitiveDataMode.Redact;

    /// <summary>
    /// Case-insensitive substring patterns used to detect PII-like parameter names
    /// (for example <c>email</c>, <c>password</c>, <c>ssn</c>).
    /// </summary>
    public IList<string> PiiParameterNamePatterns { get; } =
    [
        "email",
        "mail",
        "phone",
        "mobile",
        "password",
        "passwd",
        "secret",
        "token",
        "ssn",
        "social",
        "firstname",
        "lastname",
        "fullname",
        "name",
        "address",
        "birth",
        "national",
        "passport",
        "credit",
        "card",
        "iban",
        "account",
    ];
}

public sealed class QueryDuckSerilogOptions
{
    /// <summary>Export SQL execution failures. Defaults to <c>true</c>.</summary>
    public bool LogSqlFailures { get; set; } = true;

    /// <summary>Export slow queries (per capture threshold). Defaults to <c>true</c>.</summary>
    public bool LogSlowQueries { get; set; } = true;

    /// <summary>Export successful queries that are not slow. Defaults to <c>false</c>.</summary>
    public bool LogSuccessfulQueries { get; set; }

    public LogEventLevel SlowQueryLevel { get; set; } = LogEventLevel.Warning;

    public LogEventLevel FailureLevel { get; set; } = LogEventLevel.Error;

    public QueryDuckSensitiveDataLoggingOptions SensitiveData { get; set; } = new();
}
