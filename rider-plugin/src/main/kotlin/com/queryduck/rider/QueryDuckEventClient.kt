package com.queryduck.rider

import com.google.gson.Gson
import com.google.gson.reflect.TypeToken
import java.net.URI
import java.net.http.HttpClient
import java.net.http.HttpRequest
import java.net.http.HttpResponse
import java.time.Duration

class QueryDuckEventClient(
    val baseUrl: String = "http://127.0.0.1:17654",
) {
    private val gson = Gson()
    private val http = HttpClient.newBuilder()
        .connectTimeout(Duration.ofSeconds(2))
        .build()

    fun fetchEvents(): List<QueryCaptureEventDto> {
        val request = HttpRequest.newBuilder()
            .uri(URI.create("$baseUrl/queryduck/events"))
            .timeout(Duration.ofSeconds(3))
            .GET()
            .build()

        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() != 200) {
            throw QueryDuckClientException("HTTP ${response.statusCode()}: ${response.body()}")
        }

        val type = object : TypeToken<List<QueryCaptureEventDto>>() {}.type
        return gson.fromJson(response.body(), type) ?: emptyList()
    }

    fun fetchHealth(): HealthResponse {
        val request = HttpRequest.newBuilder()
            .uri(URI.create("$baseUrl/queryduck/health"))
            .timeout(Duration.ofSeconds(2))
            .GET()
            .build()

        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() != 200) {
            throw QueryDuckClientException("HTTP ${response.statusCode()}")
        }

        return gson.fromJson(response.body(), HealthResponse::class.java)
    }

    fun clearEvents() {
        val request = HttpRequest.newBuilder()
            .uri(URI.create("$baseUrl/queryduck/events/clear"))
            .timeout(Duration.ofSeconds(2))
            .POST(HttpRequest.BodyPublishers.noBody())
            .build()

        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() != 200) {
            throw QueryDuckClientException("Clear failed: HTTP ${response.statusCode()}")
        }
    }

    fun fetchSchemaAudit(): QueryDuckSchemaAuditPresentationDto {
        val request = HttpRequest.newBuilder()
            .uri(URI.create("$baseUrl/queryduck/schema/audit"))
            .timeout(Duration.ofSeconds(3))
            .GET()
            .build()

        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() != 200) {
            throw QueryDuckClientException("HTTP ${response.statusCode()}: ${response.body()}")
        }

        val body = response.body()
        if (body.contains("\"status\":\"empty\"")) {
            return QueryDuckSchemaAuditPresentationDto()
        }

        return gson.fromJson(body, QueryDuckSchemaAuditPresentationDto::class.java)
    }

    fun setSessionBaseline(): QueryDuckSessionSnapshotDto {
        val request = HttpRequest.newBuilder()
            .uri(URI.create("$baseUrl/queryduck/session/baseline"))
            .timeout(Duration.ofSeconds(3))
            .POST(HttpRequest.BodyPublishers.noBody())
            .build()

        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() != 200) {
            throw QueryDuckClientException("HTTP ${response.statusCode()}: ${response.body()}")
        }

        return gson.fromJson(response.body(), QueryDuckSessionSnapshotDto::class.java)
    }

    fun compareSession(): QueryDuckSessionComparisonDto {
        val request = HttpRequest.newBuilder()
            .uri(URI.create("$baseUrl/queryduck/session/compare"))
            .timeout(Duration.ofSeconds(3))
            .GET()
            .build()

        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() != 200) {
            throw QueryDuckClientException("HTTP ${response.statusCode()}: ${response.body()}")
        }

        return gson.fromJson(response.body(), QueryDuckSessionComparisonDto::class.java)
    }

    fun exportSession(): String {
        val request = HttpRequest.newBuilder()
            .uri(URI.create("$baseUrl/queryduck/session/export"))
            .timeout(Duration.ofSeconds(5))
            .GET()
            .build()

        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() != 200) {
            throw QueryDuckClientException("HTTP ${response.statusCode()}")
        }

        return response.body()
    }

    fun importSession(json: String): Int {
        val request = HttpRequest.newBuilder()
            .uri(URI.create("$baseUrl/queryduck/session/import"))
            .timeout(Duration.ofSeconds(5))
            .header("Content-Type", "application/json")
            .POST(HttpRequest.BodyPublishers.ofString(json))
            .build()

        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() != 200) {
            throw QueryDuckClientException("Import failed: HTTP ${response.statusCode()}")
        }

        val map = gson.fromJson(response.body(), Map::class.java)
        return (map["imported"] as? Number)?.toInt() ?: 0
    }

    fun fetchSessionHotspots(): QueryDuckSessionHotspotsDto =
        fetchTyped("$baseUrl/queryduck/session/hotspots", QueryDuckSessionHotspotsDto::class.java)

    fun fetchSessionTimeline(): List<QueryDuckTimelineEntryDto> {
        val request = HttpRequest.newBuilder()
            .uri(URI.create("$baseUrl/queryduck/session/timeline"))
            .timeout(Duration.ofSeconds(3))
            .GET()
            .build()

        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() != 200) {
            throw QueryDuckClientException("HTTP ${response.statusCode()}")
        }

        val type = object : TypeToken<List<QueryDuckTimelineEntryDto>>() {}.type
        return gson.fromJson(response.body(), type) ?: emptyList()
    }

    fun fetchSessionTraces(): QueryDuckTraceGroupingDto =
        fetchTyped("$baseUrl/queryduck/session/traces", QueryDuckTraceGroupingDto::class.java)

    fun diffEvents(leftEventId: String, rightEventId: String): QueryCaptureEventDiffDto {
        val payload = gson.toJson(mapOf("leftEventId" to leftEventId, "rightEventId" to rightEventId))
        val request = HttpRequest.newBuilder()
            .uri(URI.create("$baseUrl/queryduck/events/diff"))
            .timeout(Duration.ofSeconds(3))
            .header("Content-Type", "application/json")
            .POST(HttpRequest.BodyPublishers.ofString(payload))
            .build()

        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() != 200) {
            throw QueryDuckClientException("Diff failed: HTTP ${response.statusCode()}")
        }

        return gson.fromJson(response.body(), QueryCaptureEventDiffDto::class.java)
    }

    fun fetchStatementCacheDiagnostics(): QueryDuckStatementCacheDiagnosticsDto =
        fetchTyped("$baseUrl/queryduck/diagnostics/statement-cache", QueryDuckStatementCacheDiagnosticsDto::class.java)

    private fun <T> fetchTyped(url: String, clazz: Class<T>): T {
        val request = HttpRequest.newBuilder()
            .uri(URI.create(url))
            .timeout(Duration.ofSeconds(3))
            .GET()
            .build()

        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() != 200) {
            throw QueryDuckClientException("HTTP ${response.statusCode()}")
        }

        return gson.fromJson(response.body(), clazz)
    }

    fun clearHeuristicMemory() {
        val request = HttpRequest.newBuilder()
            .uri(URI.create("$baseUrl/queryduck/memory/clear"))
            .timeout(Duration.ofSeconds(2))
            .POST(HttpRequest.BodyPublishers.noBody())
            .build()

        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() != 200) {
            throw QueryDuckClientException("Clear memory failed: HTTP ${response.statusCode()}")
        }
    }

    fun fetchHeuristicWorkload(provider: String? = null): String {
        val uri = if (provider.isNullOrBlank()) {
            "$baseUrl/queryduck/memory/workload"
        } else {
            "$baseUrl/queryduck/memory/workload?provider=$provider"
        }
        val request = HttpRequest.newBuilder()
            .uri(URI.create(uri))
            .timeout(Duration.ofSeconds(2))
            .GET()
            .build()

        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() != 200) {
            throw QueryDuckClientException("HTTP ${response.statusCode()}")
        }

        return response.body()
    }

    fun recordHeuristicFeedback(
        provider: String,
        sql: String,
        category: String,
        title: String,
        action: String,
    ) {
        val payload = gson.toJson(
            mapOf(
                "provider" to provider,
                "sql" to sql,
                "category" to category,
                "title" to title,
                "action" to action,
            ),
        )
        val request = HttpRequest.newBuilder()
            .uri(URI.create("$baseUrl/queryduck/memory/feedback"))
            .timeout(Duration.ofSeconds(2))
            .header("Content-Type", "application/json")
            .POST(HttpRequest.BodyPublishers.ofString(payload))
            .build()

        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() != 200) {
            throw QueryDuckClientException("Feedback failed: HTTP ${response.statusCode()}")
        }
    }

    fun fetchHeuristicMemoryStats(): QueryHeuristicMemoryStatsDto {
        val request = HttpRequest.newBuilder()
            .uri(URI.create("$baseUrl/queryduck/memory/stats"))
            .timeout(Duration.ofSeconds(2))
            .GET()
            .build()

        val response = http.send(request, HttpResponse.BodyHandlers.ofString())
        if (response.statusCode() != 200) {
            throw QueryDuckClientException("HTTP ${response.statusCode()}")
        }

        return gson.fromJson(response.body(), QueryHeuristicMemoryStatsDto::class.java)
    }
}

class QueryDuckClientException(message: String) : Exception(message)
