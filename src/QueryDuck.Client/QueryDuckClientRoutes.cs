namespace QueryDuck.Client;

/// <summary>
/// HTTP routes for the local QueryDuck event server. Keep in sync with <c>QueryDuck.Core.QueryDuckApiRoutes</c>.
/// </summary>
internal static class QueryDuckClientRoutes
{
    public const string Health = "/queryduck/health";

    public const string Events = "/queryduck/events";

    public const string EventsLatest = "/queryduck/events/latest";

    public const string EventsClear = "/queryduck/events/clear";

    public const string EventsDiff = "/queryduck/events/diff";

    public const string SchemaAudit = "/queryduck/schema/audit";

    public const string StatementCacheDiagnostics = "/queryduck/diagnostics/statement-cache";

    public const string SessionBaseline = "/queryduck/session/baseline";

    public const string SessionCompare = "/queryduck/session/compare";

    public const string SessionWarnings = "/queryduck/session/warnings";

    public const string SessionExport = "/queryduck/session/export";

    public const string SessionImport = "/queryduck/session/import";

    public const string SessionHotspots = "/queryduck/session/hotspots";

    public const string SessionTimeline = "/queryduck/session/timeline";

    public const string SessionTraces = "/queryduck/session/traces";

    public const string MemoryFeedback = "/queryduck/memory/feedback";

    public const string MemoryStats = "/queryduck/memory/stats";

    public const string MemoryClear = "/queryduck/memory/clear";

    public const string MemoryWorkload = "/queryduck/memory/workload";
}

internal static class QueryDuckClientDefaults
{
    public const string BaseUrl = "http://127.0.0.1:17654";
}
