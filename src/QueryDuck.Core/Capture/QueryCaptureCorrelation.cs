using System.Diagnostics;
using System.Text.Json;

namespace QueryDuck.Core.Capture;

public static class QueryCaptureCorrelation
{
    public static QueryCaptureCorrelationContext ReadCurrent()
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return QueryCaptureCorrelationContext.Empty;
        }

        return new QueryCaptureCorrelationContext(
            activity.TraceId.ToString(),
            activity.SpanId.ToString(),
            activity.GetTagItem("correlation.id")?.ToString()
                ?? activity.GetTagItem("correlation_id")?.ToString()
                ?? activity.GetBaggageItem("correlation.id"),
            activity.GetTagItem("http.route")?.ToString()
                ?? activity.GetTagItem("url.path")?.ToString()
                ?? activity.GetTagItem("http.request.path")?.ToString());
    }
}

public sealed record QueryCaptureCorrelationContext(
    string? TraceId,
    string? SpanId,
    string? CorrelationId,
    string? RequestPath)
{
    public static QueryCaptureCorrelationContext Empty { get; } = new(null, null, null, null);
}
