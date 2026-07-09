using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace QueryDuck.Client;

public sealed class QueryDuckEventClient : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsClient;

    public QueryDuckEventClient(string baseUrl = "http://127.0.0.1:17654", HttpClient? httpClient = null)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        if (httpClient is null)
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            _ownsClient = true;
        }
        else
        {
            _httpClient = httpClient;
        }
    }

    public string BaseUrl { get; }

    public async Task<IReadOnlyList<QueryCaptureEventDto>> FetchEventsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"{BaseUrl}/queryduck/events", cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<QueryCaptureEventDto>>(json, SerializerOptions)
            ?? new List<QueryCaptureEventDto>();
    }

    public async Task<HealthResponse> FetchHealthAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"{BaseUrl}/queryduck/health", cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<HealthResponse>(json, SerializerOptions)
            ?? new HealthResponse();
    }

    public async Task ClearEventsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"{BaseUrl}/queryduck/events/clear", null, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task<JsonElement> FetchSchemaAuditAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"{BaseUrl}/queryduck/schema/audit", cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<JsonElement>(json, SerializerOptions);
    }

    public async Task<QueryDuckSessionSnapshotDto> SetSessionBaselineAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"{BaseUrl}/queryduck/session/baseline", null, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<QueryDuckSessionSnapshotDto>(json, SerializerOptions)
            ?? new QueryDuckSessionSnapshotDto();
    }

    public async Task<QueryDuckSessionComparisonDto> CompareSessionAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"{BaseUrl}/queryduck/session/compare", cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<QueryDuckSessionComparisonDto>(json, SerializerOptions)
            ?? new QueryDuckSessionComparisonDto();
    }

    public async Task RecordHeuristicFeedbackAsync(
        string provider,
        string sql,
        string category,
        string title,
        string action,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new
        {
            provider,
            sql,
            category,
            title,
            action,
        }, SerializerOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync($"{BaseUrl}/queryduck/memory/feedback", content, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task<QueryHeuristicMemoryStatsDto> FetchHeuristicMemoryStatsAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"{BaseUrl}/queryduck/memory/stats", cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<QueryHeuristicMemoryStatsDto>(json, SerializerOptions)
            ?? new QueryHeuristicMemoryStatsDto();
    }

    public async Task ClearHeuristicMemoryAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"{BaseUrl}/queryduck/memory/clear", null, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
    }

    public async Task<string> ExportSessionAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"{BaseUrl}/queryduck/session/export", cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    public async Task<int> ImportSessionAsync(string json, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync($"{BaseUrl}/queryduck/session/import", content, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("imported").GetInt32();
    }

    public async Task<JsonElement> FetchSessionHotspotsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"{BaseUrl}/queryduck/session/hotspots", cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<JsonElement>(json, SerializerOptions);
    }

    public async Task<JsonElement> DiffEventsAsync(string leftEventId, string rightEventId, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(new { leftEventId, rightEventId }, SerializerOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync($"{BaseUrl}/queryduck/events/diff", content, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<JsonElement>(json, SerializerOptions);
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _httpClient.Dispose();
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        throw new QueryDuckClientException($"HTTP {(int)response.StatusCode}: {body}");
    }
}

public sealed class QueryDuckClientException : Exception
{
    public QueryDuckClientException(string message)
        : base(message)
    {
    }
}
