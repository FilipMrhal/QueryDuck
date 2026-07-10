package com.queryduck.rider

/**
 * HTTP routes for the local QueryDuck event server.
 * Keep in sync with QueryDuck.Core.QueryDuckApiRoutes.
 */
object QueryDuckRoutes {
    const val HEALTH = "/queryduck/health"
    const val EVENTS = "/queryduck/events"
    const val EVENTS_LATEST = "/queryduck/events/latest"
    const val EVENTS_CLEAR = "/queryduck/events/clear"
    const val EVENTS_DIFF = "/queryduck/events/diff"
    const val SCHEMA_AUDIT = "/queryduck/schema/audit"
    const val STATEMENT_CACHE = "/queryduck/diagnostics/statement-cache"
    const val SESSION_BASELINE = "/queryduck/session/baseline"
    const val SESSION_COMPARE = "/queryduck/session/compare"
    const val SESSION_WARNINGS = "/queryduck/session/warnings"
    const val SESSION_EXPORT = "/queryduck/session/export"
    const val SESSION_IMPORT = "/queryduck/session/import"
    const val SESSION_HOTSPOTS = "/queryduck/session/hotspots"
    const val SESSION_TIMELINE = "/queryduck/session/timeline"
    const val SESSION_TRACES = "/queryduck/session/traces"
    const val MEMORY_FEEDBACK = "/queryduck/memory/feedback"
    const val MEMORY_STATS = "/queryduck/memory/stats"
    const val MEMORY_CLEAR = "/queryduck/memory/clear"
    const val MEMORY_WORKLOAD = "/queryduck/memory/workload"
}

object QueryDuckDefaults {
    const val SERVER_URL = "http://127.0.0.1:17654"
}
