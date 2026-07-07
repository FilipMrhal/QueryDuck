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
    val schemaVersion: Int = 5,
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
)

data class SlowQueryImprovementAnalysisDto(
    val eventId: String = "",
    val durationMs: Double = 0.0,
    val originalSql: String = "",
    val recommendations: List<SlowQueryRecommendationDto> = emptyList(),
    val primaryPlanDiff: PlanDiffVisualizationDto? = null,
    val pgStatStatements: PgStatStatementInsightDto? = null,
)

data class HealthResponse(
    val status: String = "",
    val count: Int = 0,
    val sessionWarnings: List<String> = emptyList(),
)
