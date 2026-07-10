using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using QueryDuck.Core.Learning;

namespace QueryDuck.Core.Capture;

public static class QueryDuckEventServerHost
{
    private static QueryDuckEventServer? _server;
    private static readonly Lock Gate = new();

    public static void EnsureStarted(string prefix = QueryDuckDefaults.ServerPrefix)
    {
        lock (Gate)
        {
            if (_server is not null)
            {
                return;
            }

            _server = new QueryDuckEventServer();
            _server.Start(prefix);
        }
    }

    public static async Task StopAsync()
    {
        QueryDuckEventServer? server;
        lock (Gate)
        {
            server = _server;
            _server = null;
        }

        if (server is not null)
        {
            await server.DisposeAsync().ConfigureAwait(false);
        }
    }
}

public sealed class QueryDuckEventServer : IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public void Start(string prefix = QueryDuckDefaults.ServerPrefix)
    {
        if (_listener.IsListening)
        {
            return;
        }

        EnsureLoopbackPrefix(prefix);
        _listener.Prefixes.Add(prefix);
        _listener.Start();
        _loop = Task.Run(() => ListenAsync(_cts.Token));
    }

    /// <summary>
    /// The server exposes captured SQL, parameters, and execution plans without authentication,
    /// so it must never listen on a network-reachable interface.
    /// </summary>
    private static void EnsureLoopbackPrefix(string prefix)
    {
        if (!Uri.TryCreate(prefix, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Invalid event server prefix '{prefix}'.", nameof(prefix));
        }

        var isLoopback = uri.IsLoopback
            || (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address));

        if (!isLoopback)
        {
            throw new InvalidOperationException(
                $"QueryDuck event server refuses to bind to non-loopback prefix '{prefix}'. " +
                "The server is unauthenticated and exposes captured SQL; only 127.0.0.1, [::1], or localhost are allowed.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _listener.Close();
        _cts.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
        }
    }

    private static async Task HandleRequestAsync(HttpListenerContext context)
    {
        AddCors(context);
        if (string.Equals(context.Request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = 204;
            context.Response.Close();
            return;
        }

        try
        {
            var request = context.Request;
            var path = request.Url?.AbsolutePath ?? "/";

            if (path.Equals(QueryDuckApiRoutes.Health, StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context, new
                {
                    status = "ok",
                    count = QueryDuckCapture.LastCommands.Count,
                    sessionWarnings = QueryDuckSession.Warnings,
                }).ConfigureAwait(false);
                return;
            }

            if (IsGet(request, QueryDuckApiRoutes.SchemaAudit))
            {
                var audit = QueryDuckSchemaAuditCache.GetPresentation();
                if (audit is null)
                {
                    await WriteJsonAsync(context, new { status = "empty" }).ConfigureAwait(false);
                }
                else
                {
                    await WriteJsonAsync(context, audit).ConfigureAwait(false);
                }
                return;
            }

            if (IsGet(request, QueryDuckApiRoutes.StatementCacheDiagnostics))
            {
                var diagnostics = await QueryDuckStatementCacheDiagnosticsBuilder.BuildAsync(
                    QueryDuckCaptureRuntime.Adapters,
                    QueryDuckCaptureRuntime.LastConnection,
                    QueryDuckCaptureRuntime.LastProviderName).ConfigureAwait(false);
                await WriteJsonAsync(context, diagnostics).ConfigureAwait(false);
                return;
            }

            if (IsPost(request, QueryDuckApiRoutes.SessionBaseline))
            {
                var options = QueryDuckCaptureRuntime.GetCurrentOptions();
                var snapshot = QueryDuckSessionSnapshot.Capture(QueryDuckCapture.LastCommands, options);
                QueryDuckSessionComparer.SetBaseline(snapshot);
                await WriteJsonAsync(context, snapshot).ConfigureAwait(false);
                return;
            }

            if (IsGet(request, QueryDuckApiRoutes.SessionCompare))
            {
                var options = QueryDuckCaptureRuntime.GetCurrentOptions();
                var current = QueryDuckSessionSnapshot.Capture(QueryDuckCapture.LastCommands, options);
                try
                {
                    var comparison = QueryDuckSessionComparer.Compare(current);
                    await WriteJsonAsync(context, comparison).ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    context.Response.StatusCode = 400;
                    await WriteJsonAsync(context, new { error = ex.Message }).ConfigureAwait(false);
                }

                return;
            }

            if (IsGet(request, QueryDuckApiRoutes.SessionWarnings))
            {
                await WriteJsonAsync(context, QueryDuckSession.Warnings).ConfigureAwait(false);
                return;
            }

            if (IsGet(request, QueryDuckApiRoutes.SessionHotspots))
            {
                await WriteJsonAsync(context, QueryDuckSessionHotspotsBuilder.Build(QueryDuckCapture.LastCommands))
                    .ConfigureAwait(false);
                return;
            }

            if (IsGet(request, QueryDuckApiRoutes.SessionExport))
            {
                var options = QueryDuckCaptureRuntime.GetCurrentOptions();
                await WriteTextAsync(
                    context,
                    QueryDuckSessionExportService.ExportJson(options),
                    "application/json").ConfigureAwait(false);
                return;
            }

            if (IsPost(request, QueryDuckApiRoutes.SessionImport))
            {
                var (success, body) = await QueryDuckHttpRequestHelpers.TryReadBodyAsync(request).ConfigureAwait(false);
                if (!success)
                {
                    await QueryDuckHttpRequestHelpers.WritePayloadTooLargeAsync(context, WriteJsonAsync).ConfigureAwait(false);
                    return;
                }

                try
                {
                    var imported = QueryDuckSessionExportService.ImportJson(body!);
                    await WriteJsonAsync(context, new { imported }).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    await QueryDuckHttpRequestHelpers.WriteInvalidPayloadAsync(
                        context,
                        WriteJsonAsync,
                        "invalid session export payload").ConfigureAwait(false);
                }

                return;
            }

            if (IsGet(request, QueryDuckApiRoutes.SessionTimeline))
            {
                await WriteJsonAsync(context, QueryDuckTransactionTimeline.Snapshot()).ConfigureAwait(false);
                return;
            }

            if (IsGet(request, QueryDuckApiRoutes.SessionTraces))
            {
                var options = QueryDuckCaptureRuntime.GetCurrentOptions();
                await WriteJsonAsync(
                    context,
                    QueryDuckTraceGroupingBuilder.Build(QueryDuckCapture.LastCommands, options.SlowQueryThresholdMs))
                    .ConfigureAwait(false);
                return;
            }

            if (IsPost(request, QueryDuckApiRoutes.MemoryFeedback))
            {
                var (success, body) = await QueryDuckHttpRequestHelpers.TryReadBodyAsync(request).ConfigureAwait(false);
                if (!success)
                {
                    await QueryDuckHttpRequestHelpers.WritePayloadTooLargeAsync(context, WriteJsonAsync).ConfigureAwait(false);
                    return;
                }

                var feedback = QueryDuckHttpRequestHelpers.DeserializeOrDefault<QueryHeuristicMemoryFeedbackRequest>(body!, SerializerOptions);
                if (feedback is null ||
                    string.IsNullOrWhiteSpace(feedback.Provider) ||
                    string.IsNullOrWhiteSpace(feedback.Sql) ||
                    string.IsNullOrWhiteSpace(feedback.Category) ||
                    string.IsNullOrWhiteSpace(feedback.Title) ||
                    string.IsNullOrWhiteSpace(feedback.Action) ||
                    !Enum.TryParse<QueryHeuristicMemoryAction>(feedback.Action, ignoreCase: true, out var action))
                {
                    await QueryDuckHttpRequestHelpers.WriteInvalidPayloadAsync(
                        context,
                        WriteJsonAsync,
                        "invalid feedback payload").ConfigureAwait(false);
                    return;
                }

                QueryHeuristicMemory.RecordFeedback(
                    feedback.Provider,
                    feedback.Sql,
                    feedback.Category,
                    feedback.Title,
                    action);
                await WriteJsonAsync(context, new { ok = true }).ConfigureAwait(false);
                return;
            }

            if (IsGet(request, QueryDuckApiRoutes.MemoryStats))
            {
                await WriteJsonAsync(context, QueryHeuristicMemory.GetStats()).ConfigureAwait(false);
                return;
            }

            if (IsPost(request, QueryDuckApiRoutes.MemoryClear))
            {
                QueryHeuristicMemory.Clear();
                await WriteJsonAsync(context, new { cleared = true }).ConfigureAwait(false);
                return;
            }

            if (IsGet(request, QueryDuckApiRoutes.MemoryWorkload))
            {
                var provider = context.Request.QueryString["provider"];
                await WriteJsonAsync(context, QueryHeuristicMemory.GetWorkloadStats(provider)).ConfigureAwait(false);
                return;
            }

            if (IsPost(request, QueryDuckApiRoutes.EventsDiff))
            {
                var (success, body) = await QueryDuckHttpRequestHelpers.TryReadBodyAsync(request).ConfigureAwait(false);
                if (!success)
                {
                    await QueryDuckHttpRequestHelpers.WritePayloadTooLargeAsync(context, WriteJsonAsync).ConfigureAwait(false);
                    return;
                }

                var diffRequest = QueryDuckHttpRequestHelpers.DeserializeOrDefault<QueryEventDiffRequest>(body!, SerializerOptions);
                if (diffRequest is null ||
                    string.IsNullOrWhiteSpace(diffRequest.LeftEventId) ||
                    string.IsNullOrWhiteSpace(diffRequest.RightEventId))
                {
                    await QueryDuckHttpRequestHelpers.WriteInvalidPayloadAsync(
                        context,
                        WriteJsonAsync,
                        "leftEventId and rightEventId are required").ConfigureAwait(false);
                    return;
                }

                var left = QueryDuckCapture.LastCommands.FirstOrDefault(e => e.EventId == diffRequest.LeftEventId);
                var right = QueryDuckCapture.LastCommands.FirstOrDefault(e => e.EventId == diffRequest.RightEventId);
                if (left is null || right is null)
                {
                    context.Response.StatusCode = 404;
                    await WriteJsonAsync(context, new { error = "one or both events were not found" }).ConfigureAwait(false);
                    return;
                }

                await WriteJsonAsync(context, QueryDuckEventDiffBuilder.Build(left, right)).ConfigureAwait(false);
                return;
            }

            if (IsPost(request, QueryDuckApiRoutes.EventsClear))
            {
                QueryDuckCapture.Clear();
                await WriteJsonAsync(context, new { cleared = true }).ConfigureAwait(false);
                return;
            }

            if (IsGet(request, QueryDuckApiRoutes.Events))
            {
                await WriteJsonAsync(context, QueryDuckCapture.LastCommands).ConfigureAwait(false);
                return;
            }

            if (IsGet(request, QueryDuckApiRoutes.EventsLatest))
            {
                var payload = string.Join('\n', QueryDuckCapture.LastCommands.Select(e => JsonSerializer.Serialize(e, SerializerOptions)));
                await WriteTextAsync(context, payload, "application/x-ndjson").ConfigureAwait(false);
                return;
            }

            if (IsPost(request, QueryDuckApiRoutes.Events))
            {
                var (success, body) = await QueryDuckHttpRequestHelpers.TryReadBodyAsync(request).ConfigureAwait(false);
                if (!success)
                {
                    await QueryDuckHttpRequestHelpers.WritePayloadTooLargeAsync(context, WriteJsonAsync).ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(body))
                {
                    var captureEvent = QueryDuckHttpRequestHelpers.DeserializeOrDefault<QueryCaptureEvent>(body!, SerializerOptions);
                    if (captureEvent is null)
                    {
                        await QueryDuckHttpRequestHelpers.WriteInvalidPayloadAsync(context, WriteJsonAsync).ConfigureAwait(false);
                        return;
                    }

                    QueryDuckCapture.Record(captureEvent);
                }

                await WriteJsonAsync(context, new { ok = true }).ConfigureAwait(false);
                return;
            }

            context.Response.StatusCode = 404;
            await WriteJsonAsync(context, new { error = "not found", path }).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            context.Response.StatusCode = 400;
            await WriteJsonAsync(context, new { error = "invalid event payload" }).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Never echo exception details: messages can contain SQL fragments or connection info.
            context.Response.StatusCode = 500;
            await WriteJsonAsync(context, new { error = "internal error" }).ConfigureAwait(false);
        }
    }

    private static bool IsGet(HttpListenerRequest request, string route) =>
        MatchesRoute(request, route, "GET");

    private static bool IsPost(HttpListenerRequest request, string route) =>
        MatchesRoute(request, route, "POST");

    private static bool MatchesRoute(HttpListenerRequest request, string route, string method) =>
        (request.Url?.AbsolutePath ?? "/").Equals(route, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(request.HttpMethod, method, StringComparison.OrdinalIgnoreCase);

    private static void AddCors(HttpListenerContext context)
    {
        // Deliberately no Access-Control-Allow-Origin: the IDE plugins use plain HTTP clients,
        // and a wildcard would let any web page a developer visits read captured SQL from localhost.
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Cache-Control"] = "no-store";
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, object payload)
    {
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        await WriteTextAsync(context, json, "application/json").ConfigureAwait(false);
    }

    private static async Task WriteTextAsync(HttpListenerContext context, string text, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        context.Response.ContentType = contentType + "; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.Close();
    }
}
