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

    public static void EnsureStarted(string prefix = "http://127.0.0.1:17654/")
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

    /// <summary>
    /// Upper bound for POSTed event payloads; anything larger is rejected with 413.
    /// </summary>
    private const long MaxRequestBodyBytes = 5 * 1024 * 1024;

    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public void Start(string prefix = "http://127.0.0.1:17654/")
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
            var path = context.Request.Url?.AbsolutePath ?? "/";

            if (path.Equals("/queryduck/health", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context, new
                {
                    status = "ok",
                    count = QueryDuckCapture.LastCommands.Count,
                    sessionWarnings = QueryDuckSession.Warnings,
                }).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/queryduck/schema/audit", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
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

            if (path.Equals("/queryduck/session/baseline", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                var options = QueryDuckCaptureRuntime.CurrentOptions ?? new QueryCaptureOptions();
                var snapshot = QueryDuckSessionSnapshot.Capture(QueryDuckCapture.LastCommands, options);
                QueryDuckSessionComparer.SetBaseline(snapshot);
                await WriteJsonAsync(context, snapshot).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/queryduck/session/compare", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                var options = QueryDuckCaptureRuntime.CurrentOptions ?? new QueryCaptureOptions();
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

            if (path.Equals("/queryduck/session/warnings", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context, QueryDuckSession.Warnings).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/queryduck/session/hotspots", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context, QueryDuckSessionHotspotsBuilder.Build(QueryDuckCapture.LastCommands))
                    .ConfigureAwait(false);
                return;
            }

            if (path.Equals("/queryduck/session/export", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                var options = QueryDuckCaptureRuntime.CurrentOptions ?? new QueryCaptureOptions();
                await WriteTextAsync(
                    context,
                    QueryDuckSessionExportService.ExportJson(options),
                    "application/json").ConfigureAwait(false);
                return;
            }

            if (path.Equals("/queryduck/session/import", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                var body = await ReadBodyAsync(context.Request).ConfigureAwait(false);
                if (body is null)
                {
                    context.Response.StatusCode = 413;
                    await WriteJsonAsync(context, new { error = "payload too large" }).ConfigureAwait(false);
                    return;
                }

                try
                {
                    var imported = QueryDuckSessionExportService.ImportJson(body);
                    await WriteJsonAsync(context, new { imported }).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    context.Response.StatusCode = 400;
                    await WriteJsonAsync(context, new { error = "invalid session export payload" }).ConfigureAwait(false);
                }

                return;
            }

            if (path.Equals("/queryduck/session/timeline", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context, QueryDuckTransactionTimeline.Snapshot()).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/queryduck/session/traces", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                var options = QueryDuckCaptureRuntime.CurrentOptions ?? new QueryCaptureOptions();
                await WriteJsonAsync(
                    context,
                    QueryDuckTraceGroupingBuilder.Build(QueryDuckCapture.LastCommands, options.SlowQueryThresholdMs))
                    .ConfigureAwait(false);
                return;
            }

            if (path.Equals("/queryduck/memory/feedback", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                if (context.Request.ContentLength64 > MaxRequestBodyBytes)
                {
                    context.Response.StatusCode = 413;
                    await WriteJsonAsync(context, new { error = "payload too large" }).ConfigureAwait(false);
                    return;
                }

                var body = await ReadBodyAsync(context.Request).ConfigureAwait(false);
                if (body is null)
                {
                    context.Response.StatusCode = 413;
                    await WriteJsonAsync(context, new { error = "payload too large" }).ConfigureAwait(false);
                    return;
                }

                var feedback = JsonSerializer.Deserialize<QueryHeuristicMemoryFeedbackRequest>(body, SerializerOptions);
                if (feedback is null ||
                    string.IsNullOrWhiteSpace(feedback.Provider) ||
                    string.IsNullOrWhiteSpace(feedback.Sql) ||
                    string.IsNullOrWhiteSpace(feedback.Category) ||
                    string.IsNullOrWhiteSpace(feedback.Title) ||
                    string.IsNullOrWhiteSpace(feedback.Action) ||
                    !Enum.TryParse<QueryHeuristicMemoryAction>(feedback.Action, ignoreCase: true, out var action))
                {
                    context.Response.StatusCode = 400;
                    await WriteJsonAsync(context, new { error = "invalid feedback payload" }).ConfigureAwait(false);
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

            if (path.Equals("/queryduck/memory/stats", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context, QueryHeuristicMemory.GetStats()).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/queryduck/memory/clear", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                QueryHeuristicMemory.Clear();
                await WriteJsonAsync(context, new { cleared = true }).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/queryduck/memory/workload", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                var provider = context.Request.QueryString["provider"];
                await WriteJsonAsync(context, QueryHeuristicMemory.GetWorkloadStats(provider)).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/queryduck/events/diff", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                var body = await ReadBodyAsync(context.Request).ConfigureAwait(false);
                if (body is null)
                {
                    context.Response.StatusCode = 413;
                    await WriteJsonAsync(context, new { error = "payload too large" }).ConfigureAwait(false);
                    return;
                }

                var request = JsonSerializer.Deserialize<QueryEventDiffRequest>(body, SerializerOptions);
                if (request is null ||
                    string.IsNullOrWhiteSpace(request.LeftEventId) ||
                    string.IsNullOrWhiteSpace(request.RightEventId))
                {
                    context.Response.StatusCode = 400;
                    await WriteJsonAsync(context, new { error = "leftEventId and rightEventId are required" }).ConfigureAwait(false);
                    return;
                }

                var left = QueryDuckCapture.LastCommands.FirstOrDefault(e => e.EventId == request.LeftEventId);
                var right = QueryDuckCapture.LastCommands.FirstOrDefault(e => e.EventId == request.RightEventId);
                if (left is null || right is null)
                {
                    context.Response.StatusCode = 404;
                    await WriteJsonAsync(context, new { error = "one or both events were not found" }).ConfigureAwait(false);
                    return;
                }

                await WriteJsonAsync(context, QueryDuckEventDiffBuilder.Build(left, right)).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/queryduck/events/clear", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                QueryDuckCapture.Clear();
                await WriteJsonAsync(context, new { cleared = true }).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/queryduck/events", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context, QueryDuckCapture.LastCommands).ConfigureAwait(false);
                return;
            }

            if (path.Equals("/queryduck/events/latest", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                var payload = string.Join('\n', QueryDuckCapture.LastCommands.Select(e => JsonSerializer.Serialize(e, SerializerOptions)));
                await WriteTextAsync(context, payload, "application/x-ndjson").ConfigureAwait(false);
                return;
            }

            if (path.Equals("/queryduck/events", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                if (context.Request.ContentLength64 > MaxRequestBodyBytes)
                {
                    context.Response.StatusCode = 413;
                    await WriteJsonAsync(context, new { error = "payload too large" }).ConfigureAwait(false);
                    return;
                }

                var body = await ReadBodyAsync(context.Request).ConfigureAwait(false);
                if (body is null)
                {
                    context.Response.StatusCode = 413;
                    await WriteJsonAsync(context, new { error = "payload too large" }).ConfigureAwait(false);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(body))
                {
                    var captureEvent = JsonSerializer.Deserialize<QueryCaptureEvent>(body, SerializerOptions);
                    if (captureEvent is null)
                    {
                        context.Response.StatusCode = 400;
                        await WriteJsonAsync(context, new { error = "invalid event payload" }).ConfigureAwait(false);
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

    /// <summary>
    /// Reads the request body with a hard size cap (Content-Length can be absent or lie with chunked encoding).
    /// Returns null when the cap is exceeded.
    /// </summary>
    private static async Task<string?> ReadBodyAsync(HttpListenerRequest request)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await request.InputStream.ReadAsync(chunk).ConfigureAwait(false)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxRequestBodyBytes)
            {
                return null;
            }
        }

        return (request.ContentEncoding ?? Encoding.UTF8).GetString(buffer.ToArray());
    }

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
