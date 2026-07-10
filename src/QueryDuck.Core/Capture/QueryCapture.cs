using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.ExpressionTrees;
using QueryDuck.Core.Performance;

namespace QueryDuck.Core.Capture;

public sealed record QueryDiagnosticDto(
    string RuleId,
    string Severity,
    string Message,
    string? FixHint = null);

public sealed record QueryCaptureEvent
{
    public required string EventId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public required string Sql { get; init; }

    public required string Provider { get; init; }

    /// <summary>
    /// Capture origin: <c>EfCore</c> (default) or <c>EntityFrameworkExtensions</c>.
    /// </summary>
    public string Source { get; init; } = QueryCaptureSources.EfCore;

    /// <summary>
    /// When <see cref="Source"/> is Entity Framework Extensions, the bulk/batch operation name (e.g. BulkInsert).
    /// </summary>
    public string? BulkOperation { get; init; }

    public string? Tag { get; init; }

    public string? Caller { get; init; }

    public TimeSpan Duration { get; init; }

    public IReadOnlyDictionary<string, object?> Parameters { get; init; } =
        new Dictionary<string, object?>();

    public IReadOnlyList<QueryDiagnosticDto> Diagnostics { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public string? ExpressionTreeText { get; init; }

    public string? ExpressionCSharp { get; init; }

    public ExpressionTreeNode? ExpressionTree { get; init; }

    public string? ExecutionPlan { get; init; }

    public string? PlanHash { get; init; }

    public SlowQueryImprovementAnalysisDto? ImprovementAnalysis { get; init; }

    public bool Succeeded { get; init; } = true;

    public string? ErrorMessage { get; init; }

    public string? ExceptionType { get; init; }

    public string? TraceId { get; init; }

    public string? SpanId { get; init; }

    public string? CorrelationId { get; init; }

    public string? RequestPath { get; init; }

    public SourceLocation? SourceLocation { get; init; }

    public int SchemaVersion { get; init; } = QueryDuckDefaults.EventSchemaVersion;
}

public sealed class QueryCaptureOptions
{
    public int BufferCapacity { get; set; } = 200;

    public bool PublishEvents { get; set; }

    public bool StartLocalEventServer { get; set; } = true;

    public string ServerPrefix { get; set; } = QueryDuckDefaults.ServerPrefix;

    public string PublishEndpoint { get; set; } = QueryDuckDefaults.EventsUrl;

    public bool CaptureExecutionPlans { get; set; }

    /// <summary>
    /// When true, every LINQ query automatically captures its expression tree and diagnostics
    /// without calling <c>WithQueryDuckScope</c> manually.
    /// </summary>
    public bool AutoCaptureAllQueries { get; set; } = true;

    public int NPlusOneThreshold { get; set; } = 5;

    public bool DetectNPlusOne { get; set; } = true;

    public int SlowQueryThresholdMs { get; set; } = 500;

    public bool AnalyzeSlowQueries { get; set; } = true;

    public bool CapturePlansForSlowQueries { get; set; } = true;

    /// <summary>
    /// When true, enriches slow queries with historical workload stats from the active provider
    /// (pg_stat_statements, dm_exec_query_stats, V$SQL, performance_schema, etc.).
    /// </summary>
    public bool EnableHistoricalStatsInsights { get; set; }

    /// <summary>
    /// When true, enriches slow PostgreSQL queries with matching <c>pg_stat_statements</c> metrics.
    /// Alias for <see cref="EnableHistoricalStatsInsights"/>.
    /// </summary>
    public bool EnablePgStatStatementsInsights
    {
        get => EnableHistoricalStatsInsights;
        set => EnableHistoricalStatsInsights = value;
    }

    /// <summary>
    /// When true, reads <c>pg_stats</c> (PostgreSQL) to refine index recommendations with column selectivity.
    /// </summary>
    public bool EnableStatisticsBasedIndexRecommendations { get; set; }

    /// <summary>
    /// When true, attaches Mermaid flowchart graphs to plan diff visualizations for Rider rendering.
    /// </summary>
    public bool EmitMermaidPlanGraphs { get; set; }

    /// <summary>
    /// When true, learns from slow-query captures and IDE feedback to re-rank recommendations locally.
    /// </summary>
    public bool EnableHeuristicMemory { get; set; } = true;

    /// <summary>
    /// SQLite path for heuristic memory. Defaults to <c>~/.queryduck/memory.db</c>.
    /// </summary>
    public string? HeuristicMemoryStorePath { get; set; }

    /// <summary>
    /// Maximum feedback rows retained before oldest entries are pruned.
    /// </summary>
    public int HeuristicMemoryMaxEntries { get; set; } = 5000;

    /// <summary>
    /// When true, only a sample of successful fast queries are captured (failures and slow queries are always captured).
    /// </summary>
    public bool EnableSampling { get; set; }

    /// <summary>
    /// Sampling rate for successful fast queries when <see cref="EnableSampling"/> is true (0.0–1.0).
    /// </summary>
    public double SamplingRate { get; set; } = 0.05;

    /// <summary>
    /// When true, captures user-code file/line from the call stack for Rider jump-to-source.
    /// </summary>
    public bool CaptureSourceLocations { get; set; } = true;

    /// <summary>
    /// Optional exporters invoked after each captured query (e.g. Serilog structured logging).
    /// </summary>
    public IList<IQueryCaptureEventPublisher> EventPublishers { get; } = [];
}

public static partial class QueryDuckCapture
{
    private static readonly QueryCaptureBuffer Buffer = new();

    public static IReadOnlyList<QueryCaptureEvent> LastCommands => Buffer.Snapshot();

    internal static QueryCaptureBuffer SharedBuffer => Buffer;

    public static void Clear()
    {
        Buffer.Clear();
        QueryDuckSession.Clear();
        QueryDuckSchemaAuditCache.Clear();
        QueryDuckSessionTables.Clear();
        QueryDuckSessionComparer.ClearBaseline();
        QueryDuckTransactionTimeline.Clear();
    }

    public static void Record(QueryCaptureEvent captureEvent)
    {
        ArgumentNullException.ThrowIfNull(captureEvent);
        Buffer.Add(captureEvent);
    }
}

internal sealed class QueryCaptureBuffer
{
    private readonly ConcurrentQueue<QueryCaptureEvent> _events = new();
    private int _capacity = 200;

    public void Configure(int capacity) => _capacity = Math.Max(1, capacity);

    public void Add(QueryCaptureEvent captureEvent)
    {
        _events.Enqueue(captureEvent);
        while (_events.Count > _capacity && _events.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<QueryCaptureEvent> Snapshot() => _events.ToArray();

    public void Clear()
    {
        while (_events.TryDequeue(out _))
        {
        }
    }
}

public static class QueryCaptureHeuristics
{
    public static IReadOnlyList<string> DetectNPlusOne(IEnumerable<QueryCaptureEvent> events, int threshold = 5)
    {
        ArgumentNullException.ThrowIfNull(events);
        var warnings = new List<string>();
        var groups = events
            .GroupBy(e => NormalizeSqlShape(e.Sql))
            .Where(g => g.Count() >= threshold);

        foreach (var group in groups)
        {
            warnings.Add($"Possible N+1: query shape executed {group.Count()} times.");
        }

        return warnings;
    }

    public static IReadOnlyList<string> DetectSlowQueries(IEnumerable<QueryCaptureEvent> events, int thresholdMs)
    {
        ArgumentNullException.ThrowIfNull(events);
        return events
            .Where(e => e.Duration.TotalMilliseconds >= thresholdMs)
            .Select(e => $"Slow query ({e.Duration.TotalMilliseconds:F0} ms): {PreviewSql(e.Sql)}")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string PreviewSql(string sql)
    {
        var line = sql.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? sql;
        return line.Length <= 80 ? line : line[..77] + "...";
    }

    public static string NormalizeSqlShape(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return string.Empty;
        }

        return System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(sql, @":\w+", "?"),
            @"@\w+",
            "?");
    }
}

public sealed class JsonLinesEventPublisher(QueryCaptureOptions options) : IQueryCaptureEventPublisher
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public Task PublishAsync(
        QueryCaptureEvent captureEvent,
        QueryCapturePublishContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(captureEvent);
        ArgumentNullException.ThrowIfNull(context);
        if (!options.PublishEvents || options.StartLocalEventServer)
        {
            return Task.CompletedTask;
        }

        var json = JsonSerializer.Serialize(captureEvent, SerializerOptions);
        return PublishRemoteAsync(json, cancellationToken);
    }

    private async Task PublishRemoteAsync(string json, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        using var content = new StringContent(json + '\n', System.Text.Encoding.UTF8, "application/json");
        try
        {
            await client.PostAsync(new Uri(options.PublishEndpoint), content, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Debug.WriteLine($"QueryDuck event publish failed: {ex.Message}");
        }
    }
}
