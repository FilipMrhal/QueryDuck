using System.Net.Http;
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
