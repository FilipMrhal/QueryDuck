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
}

class QueryDuckClientException(message: String) : Exception(message)
