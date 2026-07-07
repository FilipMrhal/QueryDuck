using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public void Start(string prefix = "http://127.0.0.1:17654/")
    {
        if (_listener.IsListening)
        {
            return;
        }

        _listener.Prefixes.Add(prefix);
        _listener.Start();
        _loop = Task.Run(() => ListenAsync(_cts.Token));
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

            if (path.Equals("/queryduck/session/warnings", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(context, QueryDuckSession.Warnings).ConfigureAwait(false);
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
                using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
                var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(body))
                {
                    QueryDuckCapture.Record(JsonSerializer.Deserialize<QueryCaptureEvent>(body, SerializerOptions)!);
                }

                await WriteJsonAsync(context, new { ok = true }).ConfigureAwait(false);
                return;
            }

            context.Response.StatusCode = 404;
            await WriteJsonAsync(context, new { error = "not found", path }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await WriteJsonAsync(context, new { error = ex.Message }).ConfigureAwait(false);
        }
    }

    private static void AddCors(HttpListenerContext context)
    {
        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
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
