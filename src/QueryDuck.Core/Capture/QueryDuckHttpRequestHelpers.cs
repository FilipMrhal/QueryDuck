using System.Net;
using System.Text;
using System.Text.Json;

namespace QueryDuck.Core.Capture;

internal static class QueryDuckHttpRequestHelpers
{
    public static bool IsPayloadTooLarge(HttpListenerRequest request) =>
        request.ContentLength64 > QueryDuckDefaults.MaxRequestBodyBytes;

    public static async Task<(bool Success, string? Body)> TryReadBodyAsync(HttpListenerRequest request)
    {
        if (IsPayloadTooLarge(request))
        {
            return (false, null);
        }

        var body = await ReadBodyAsync(request).ConfigureAwait(false);
        return body is null ? (false, null) : (true, body);
    }

    public static async Task WritePayloadTooLargeAsync(
        HttpListenerContext context,
        Func<HttpListenerContext, object, Task> writeJsonAsync)
    {
        context.Response.StatusCode = 413;
        await writeJsonAsync(context, new { error = "payload too large" }).ConfigureAwait(false);
    }

    public static async Task WriteInvalidPayloadAsync(
        HttpListenerContext context,
        Func<HttpListenerContext, object, Task> writeJsonAsync,
        string error = "invalid event payload")
    {
        context.Response.StatusCode = 400;
        await writeJsonAsync(context, new { error }).ConfigureAwait(false);
    }

    public static T? DeserializeOrDefault<T>(string body, JsonSerializerOptions options)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(body, options);
    }

    public static async Task<bool> TryHandlePostJsonAsync<T>(
        HttpListenerContext context,
        HttpListenerRequest request,
        JsonSerializerOptions options,
        Func<T, Task<object>> handleValid,
        Func<HttpListenerContext, object, Task> writeJsonAsync,
        Func<T, bool>? validate = null,
        string invalidPayloadMessage = "invalid event payload")
    {
        var (success, body) = await TryReadBodyAsync(request).ConfigureAwait(false);
        if (!success)
        {
            await WritePayloadTooLargeAsync(context, writeJsonAsync).ConfigureAwait(false);
            return true;
        }

        var payload = DeserializeOrDefault<T>(body!, options);
        if (payload is null || validate is not null && !validate(payload))
        {
            await WriteInvalidPayloadAsync(context, writeJsonAsync, invalidPayloadMessage).ConfigureAwait(false);
            return true;
        }

        await writeJsonAsync(context, await handleValid(payload).ConfigureAwait(false)).ConfigureAwait(false);
        return true;
    }

    public static async Task<bool> TryHandlePostBodyAsync(
        HttpListenerContext context,
        HttpListenerRequest request,
        Func<string?, Task> handleValid,
        Func<HttpListenerContext, object, Task> writeJsonAsync,
        Func<string?, bool>? validate = null,
        string invalidPayloadMessage = "invalid event payload")
    {
        var (success, body) = await TryReadBodyAsync(request).ConfigureAwait(false);
        if (!success)
        {
            await WritePayloadTooLargeAsync(context, writeJsonAsync).ConfigureAwait(false);
            return true;
        }

        if (validate is not null && !validate(body))
        {
            await WriteInvalidPayloadAsync(context, writeJsonAsync, invalidPayloadMessage).ConfigureAwait(false);
            return true;
        }

        await handleValid(body).ConfigureAwait(false);
        return true;
    }

    private static async Task<string?> ReadBodyAsync(HttpListenerRequest request)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await request.InputStream.ReadAsync(chunk).ConfigureAwait(false)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > QueryDuckDefaults.MaxRequestBodyBytes)
            {
                return null;
            }
        }

        return (request.ContentEncoding ?? Encoding.UTF8).GetString(buffer.ToArray());
    }
}
