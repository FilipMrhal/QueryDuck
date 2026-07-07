package com.queryduck.rider

data class QueryDiagnosticDto(
    val ruleId: String = "",
    val severity: String = "",
    val message: String = "",
    val fixHint: String? = null,
)

data class ExpressionTreeNodeDto(
    val kind: String = "",
    val type: String = "",
    val name: String? = null,
    val value: String? = null,
    val children: List<ExpressionTreeNodeDto>? = null,
)

data class SourceLocationDto(
    val filePath: String = "",
    val line: Int = 0,
    val methodName: String? = null,
)

data class QueryCaptureEventDto(
    val eventId: String = "",
    val timestamp: String = "",
    val sql: String = "",
    val provider: String = "",
    val tag: String? = null,
    val caller: String? = null,
    val duration: String? = null,
    val parameters: Map<String, Any?> = emptyMap(),
    val diagnostics: List<QueryDiagnosticDto> = emptyList(),
    val warnings: List<String> = emptyList(),
    val expressionTreeText: String? = null,
    val expressionCSharp: String? = null,
    val expressionTree: ExpressionTreeNodeDto? = null,
    val executionPlan: String? = null,
    val planHash: String? = null,
    val improvementAnalysis: SlowQueryImprovementAnalysisDto? = null,
    val source: String = "EfCore",
    val bulkOperation: String? = null,
    val schemaVersion: Int = 8,
    val traceId: String? = null,
    val spanId: String? = null,
    val correlationId: String? = null,
    val requestPath: String? = null,
    val sourceLocation: SourceLocationDto? = null,
) {
    val warningCount: Int get() = diagnostics.count { it.severity.equals("Warning", true) || it.severity.equals("Error", true) }

    fun sqlPreview(maxLength: Int = 80): String {
        val singleLine = sql.lineSequence().joinToString(" ") { it.trim() }.trim()
        return if (singleLine.length <= maxLength) singleLine else singleLine.take(maxLength - 1) + "…"
    }

    fun formattedTime(): String =
        timestamp.substringAfter('T').substringBefore('.').ifBlank { timestamp }

    fun formattedDuration(): String =
        duration?.substringBefore('.')?.ifBlank { null } ?: "—"
}

data class QueryHistoricalStatsInsightDto(
    val calls: Long = 0,
    val meanExecTimeMs: Double = 0.0,
    val totalExecTimeMs: Double = 0.0,
    val rows: Long = 0,
    val cacheHitRatio: Double? = null,
    val matchedQueryText: String? = null,
    val sourceView: String? = null,
)

data class PgStatStatementInsightDto(
    val calls: Long = 0,
    val meanExecTimeMs: Double = 0.0,
    val totalExecTimeMs: Double = 0.0,
    val rows: Long = 0,
    val sharedBlocksHitRatio: Double = 0.0,
    val matchedQueryText: String? = null,
)

data class PlanDiffVisualizationDto(
    val originalSteps: List<PlanStepSummaryDto> = emptyList(),
    val improvedSteps: List<PlanStepSummaryDto> = emptyList(),
    val summaryLines: List<String> = emptyList(),
    val textDiff: String = "",
    val originalMermaid: String? = null,
    val improvedMermaid: String? = null,
    val sideBySideMermaid: String? = null,
)

data class PlanStepSummaryDto(
    val operation: String = "",
    val objectName: String? = null,
    val detail: String? = null,
    val cost: Double? = null,
)

data class SlowQueryRecommendationDto(
    val category: String = "",
    val title: String = "",
    val description: String = "",
    val suggestedSql: String? = null,
    val suggestedIndexSql: String? = null,
    val improvedPlanText: String? = null,
    val planDiff: PlanDiffVisualizationDto? = null,
    val heuristicScore: Double? = null,
    val heuristicHint: String? = null,
    val suggestedMigrationSql: String? = null,
)

data class SlowQueryImprovementAnalysisDto(
    val eventId: String = "",
    val durationMs: Double = 0.0,
    val originalSql: String = "",
    val recommendations: List<SlowQueryRecommendationDto> = emptyList(),
    val primaryPlanDiff: PlanDiffVisualizationDto? = null,
    val pgStatStatements: PgStatStatementInsightDto? = null,
    val historicalStats: QueryHistoricalStatsInsightDto? = null,
)

data class HealthResponse(
    val status: String = "",
    val count: Int = 0,
    val sessionWarnings: List<String> = emptyList(),
)

data class QueryHeuristicMemoryStatsDto(
    val feedbackCount: Int = 0,
    val distinctShapes: Int = 0,
    val copiedCount: Int = 0,
    val dismissedCount: Int = 0,
    val storePath: String = "",
)

data class QueryDuckSessionSnapshotDto(
    val capturedAt: String = "",
    val eventCount: Int = 0,
    val slowQueryCount: Int = 0,
    val failureCount: Int = 0,
    val diagnosticWarningCount: Int = 0,
    val eventsByProvider: Map<String, Int> = emptyMap(),
    val diagnosticsByRule: Map<String, Int> = emptyMap(),
    val sessionWarnings: List<String> = emptyList(),
)

data class QueryDuckSessionComparisonDto(
    val baseline: QueryDuckSessionSnapshotDto = QueryDuckSessionSnapshotDto(),
    val current: QueryDuckSessionSnapshotDto = QueryDuckSessionSnapshotDto(),
    val eventCountDelta: Int = 0,
    val slowQueryCountDelta: Int = 0,
    val failureCountDelta: Int = 0,
    val diagnosticWarningCountDelta: Int = 0,
    val newSessionWarnings: List<String> = emptyList(),
    val resolvedSessionWarnings: List<String> = emptyList(),
    val providerCountDeltas: Map<String, Int> = emptyMap(),
    val ruleCountDeltas: Map<String, Int> = emptyMap(),
)

data class SessionTableRelevanceDto(
    val tableName: String = "",
    val hitCount: Int = 0,
    val totalDurationMs: Double = 0.0,
    val relevanceScore: Double = 0.0,
)

data class SchemaRecommendationDto(
    val kind: String = "",
    val tableName: String = "",
    val columnName: String = "",
    val referencedTable: String? = null,
    val message: String = "",
    val sessionRelevanceScore: Double? = null,
    val heuristicScore: Double? = null,
    val heuristicHint: String? = null,
    val feedbackKey: String? = null,
    val feedbackCategory: String? = null,
    val feedbackTitle: String? = null,
) {
    fun listLabel(): String {
        val relevance = sessionRelevanceScore?.let { " · session ${"%.1f".format(it)}" }.orEmpty()
        val score = heuristicScore?.let { " · learned ${"%.1f".format(it)}" }.orEmpty()
        return "$kind · $tableName.$columnName$relevance$score"
    }
}

data class SchemaAuditResultDto(
    val nullabilityMismatches: List<SchemaDriftFindingDto> = emptyList(),
    val typeMismatches: List<SchemaDriftFindingDto> = emptyList(),
    val missingColumns: List<SchemaDriftFindingDto> = emptyList(),
    val missingIndexes: List<SchemaRecommendationDto> = emptyList(),
    val foreignKeyIssues: List<SchemaRecommendationDto> = emptyList(),
)

data class SchemaDriftFindingDto(
    val entityType: String = "",
    val tableName: String = "",
    val propertyName: String = "",
    val columnName: String = "",
    val message: String = "",
    val sessionRelevanceScore: Double? = null,
)

data class QueryDuckSchemaAuditPresentationDto(
    val capturedAt: String = "",
    val provider: String = "",
    val result: SchemaAuditResultDto = SchemaAuditResultDto(),
    val hasIssues: Boolean = false,
    val sessionFilterActive: Boolean = false,
    val hiddenFindingCount: Int = 0,
    val sessionTables: List<SessionTableRelevanceDto> = emptyList(),
)
