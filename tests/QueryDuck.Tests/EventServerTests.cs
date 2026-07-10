using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core;
using QueryDuck.Core.Capture;
using QueryDuck.Core.Learning;
using QueryDuck.Sample;
using QueryDuck.Testing.Factories;
using TestEventFactory = QueryDuck.Testing.Factories.QueryCaptureEventFactory;

namespace QueryDuck.Tests;

[Collection("QueryDuckCapture")]
public sealed class EventServerTests : IAsyncLifetime
{
    private static readonly Uri Prefix = new("http://127.0.0.1:17655/");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task InitializeAsync()
    {
        QueryDuckCapture.Clear();
        await QueryDuckEventServerHost.StopAsync();
    }

    public Task DisposeAsync() => InitializeAsync();

    [Fact]
    public async Task Health_ReturnsOkWithCount()
    {
        StartServer();
        QueryDuckCapture.Record(TestEventFactory.CreateMarker("health-test"));

        using var client = new HttpClient();
        var response = await client.GetFromJsonAsync<HealthResponse>(new Uri(Prefix, "queryduck/health"));

        Assert.NotNull(response);
        Assert.Equal("ok", response.Status);
        Assert.Equal(1, response.Count);
    }

    [Fact]
    public async Task GetEvents_ReturnsCapturedCommands()
    {
        StartServer();
        QueryDuckCapture.Record(TestEventFactory.CreateMarker("listed"));

        using var client = new HttpClient();
        var events = await client.GetFromJsonAsync<List<QueryCaptureEvent>>(new Uri(Prefix, "queryduck/events"));

        Assert.NotNull(events);
        Assert.Contains(events, e => e.Sql == "SELECT listed");
    }

    [Fact]
    public async Task PostEvents_AppendsToBuffer()
    {
        StartServer();
        QueryDuckCapture.Clear();

        using var client = new HttpClient();
        var payload = JsonSerializer.Serialize(TestEventFactory.CreateMarker("posted"), JsonOptions);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync(new Uri(Prefix, "queryduck/events"), content);

        response.EnsureSuccessStatusCode();
        await Task.Delay(50);
        Assert.Single(QueryDuckCapture.LastCommands);
    }

    [Fact]
    public async Task ClearEvents_RemovesBufferedCommands()
    {
        StartServer();
        QueryDuckCapture.Record(TestEventFactory.CreateMarker("clear-me"));

        using var client = new HttpClient();
        var response = await client.PostAsync(new Uri(Prefix, "queryduck/events/clear"), null);

        response.EnsureSuccessStatusCode();
        Assert.Empty(QueryDuckCapture.LastCommands);
    }

    [Fact]
    public async Task LatestEvents_ReturnsNdjsonPayload()
    {
        StartServer();
        QueryDuckCapture.Record(TestEventFactory.CreateMarker("latest"));

        using var client = new HttpClient();
        var body = await client.GetStringAsync(new Uri(Prefix, "queryduck/events/latest"));

        Assert.Contains("latest", body, StringComparison.Ordinal);
        Assert.Contains('\n', body);
    }

    [Fact]
    public async Task OptionsRequest_ReturnsNoContent()
    {
        StartServer();

        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, new Uri(Prefix, "queryduck/events"));
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Start_RefusesNonLoopbackPrefix()
    {
        await using var server = new QueryDuckEventServer();
        Assert.Throws<InvalidOperationException>(() => server.Start("http://0.0.0.0:17656/"));
        // Wildcard prefixes are not parseable URIs and are rejected as invalid arguments.
        Assert.Throws<ArgumentException>(() => server.Start("http://+:17656/"));
    }

    [Fact]
    public async Task PostEvents_RejectsOversizedPayload()
    {
        StartServer();
        QueryDuckCapture.Clear();

        using var client = new HttpClient();
        var oversized = new byte[6 * 1024 * 1024];
        using var content = new ByteArrayContent(oversized);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        try
        {
            var response = await client.PostAsync(new Uri(Prefix, "queryduck/events"), content);
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            // The server may close the connection before the client finishes uploading;
            // the broken pipe is itself proof of rejection.
        }

        Assert.Empty(QueryDuckCapture.LastCommands);
    }

    [Fact]
    public async Task PostEvents_RejectsMalformedJson()
    {
        StartServer();

        using var client = new HttpClient();
        using var content = new StringContent("{not json", Encoding.UTF8, "application/json");
        var response = await client.PostAsync(new Uri(Prefix, "queryduck/events"), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Responses_DoNotAllowCrossOriginReads()
    {
        StartServer();

        using var client = new HttpClient();
        using var response = await client.GetAsync(new Uri(Prefix, "queryduck/health"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task UnknownRoute_ReturnsNotFound()
    {
        StartServer();

        using var client = new HttpClient();
        using var response = await client.GetAsync(new Uri(Prefix, "queryduck/missing"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UseQueryDuckDebugging_StartsDefaultEventServer()
    {
        QueryDuckOptionsBuilderExtensions.EnsureEventServer(new QueryCaptureOptions
        {
            StartLocalEventServer = true,
            ServerPrefix = Prefix.ToString(),
        });

        using var client = new HttpClient();
        var response = await client.GetFromJsonAsync<HealthResponse>(new Uri(Prefix, "queryduck/health"));

        Assert.NotNull(response);
        Assert.Equal("ok", response.Status);
    }

    [Fact]
    public async Task RemotePublish_WritesToConfiguredEndpoint()
    {
        StartServer();
        QueryDuckCapture.Clear();

        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseSqlite(connection)
            .UseQueryDuckCapture(o =>
            {
                o.AutoCaptureAllQueries = false;
                o.StartLocalEventServer = false;
                o.PublishEvents = true;
                o.PublishEndpoint = new Uri(Prefix, "queryduck/events").ToString();
            })
            .Options;

        await using var context = new SampleDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Customers.Add(new QueryDuck.Sample.Entities.Customer
        {
            Id = 1,
            Name = "Remote",
            Region = "EMEA",
            Code = "R1",
        });
        await context.SaveChangesAsync();
        QueryDuckCapture.Clear();

        await context.Customers.ToListAsync();
        await Task.Delay(50);

        Assert.Equal(2, QueryDuckCapture.LastCommands.Count);
    }

    [Fact]
    public async Task MemoryFeedback_RecordsAndReturnsStats()
    {
        var storePath = Path.Combine(Path.GetTempPath(), $"queryduck-memory-api-{Guid.NewGuid():N}.db");
        try
        {
            StartServer();
            QueryHeuristicMemory.Configure(new QueryCaptureOptions
            {
                EnableHeuristicMemory = true,
                HeuristicMemoryStorePath = storePath,
            });

            using var client = new HttpClient();
            var payload = JsonSerializer.Serialize(new
            {
                provider = "PostgreSql",
                sql = "SELECT * FROM Orders WHERE Id = @p0",
                category = "IndexCreation",
                title = "Add index on Id",
                action = "Copied",
            }, JsonOptions);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var feedbackResponse = await client.PostAsync(new Uri(Prefix, "queryduck/memory/feedback"), content);
            feedbackResponse.EnsureSuccessStatusCode();

            var stats = await client.GetFromJsonAsync<MemoryStatsResponse>(new Uri(Prefix, "queryduck/memory/stats"));
            Assert.NotNull(stats);
            Assert.Equal(1, stats.FeedbackCount);
            Assert.Equal(1, stats.CopiedCount);

            var clearResponse = await client.PostAsync(new Uri(Prefix, "queryduck/memory/clear"), null);
            clearResponse.EnsureSuccessStatusCode();

            stats = await client.GetFromJsonAsync<MemoryStatsResponse>(new Uri(Prefix, "queryduck/memory/stats"));
            Assert.NotNull(stats);
            Assert.Equal(0, stats.FeedbackCount);
        }
        finally
        {
            QueryHeuristicMemory.Configure(new QueryCaptureOptions { EnableHeuristicMemory = false });
            if (File.Exists(storePath))
            {
                File.Delete(storePath);
            }
        }
    }

    [Fact]
    public async Task StatementCache_ReturnsUnavailableWithoutConnection()
    {
        StartServer();
        QueryDuckCaptureRuntime.LastConnection = null;
        QueryDuckCaptureRuntime.LastProviderName = null;

        using var client = new HttpClient();
        var response = await client.GetFromJsonAsync<StatementCacheResponse>(
            new Uri(Prefix, "queryduck/diagnostics/statement-cache"));

        Assert.NotNull(response);
        Assert.False(response.ConnectionAvailable);
        Assert.Empty(response.Findings);
    }

    private static void StartServer()
    {
        QueryDuckOptionsBuilderExtensions.EnsureEventServer(new QueryCaptureOptions
        {
            StartLocalEventServer = true,
            ServerPrefix = Prefix.ToString(),
        });
    }

    private sealed record StatementCacheResponse(
        string Provider,
        bool ConnectionAvailable,
        IReadOnlyList<StatementCacheFindingResponse> Findings);

    private sealed record StatementCacheFindingResponse(
        string Signature,
        int VariantCount,
        string Message);

    private sealed record HealthResponse(string Status, int Count);

    private sealed record MemoryStatsResponse(
        int FeedbackCount,
        int DistinctShapes,
        int CopiedCount,
        int DismissedCount,
        string StorePath);
}
