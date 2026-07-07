using System.Diagnostics;
using QueryDuck.Core.Capture;

namespace QueryDuck.OpenTelemetry;

internal static class QueryDuckOpenTelemetryEnricher
{
    public static ActivityTagsCollection BuildTags(
        QueryCaptureEvent captureEvent,
        QueryCapturePublishContext context,
        QueryDuckOpenTelemetryOptions options)
    {
        var tags = new ActivityTagsCollection
        {
            ["queryduck.event_id"] = captureEvent.EventId,
            ["queryduck.provider"] = captureEvent.Provider,
            ["queryduck.source"] = captureEvent.Source,
            ["queryduck.duration_ms"] = captureEvent.Duration.TotalMilliseconds,
            ["queryduck.slow_query_threshold_ms"] = context.SlowQueryThresholdMs,
            ["queryduck.is_slow"] = context.IsSlow,
            ["queryduck.succeeded"] = captureEvent.Succeeded,
            ["queryduck.schema_version"] = captureEvent.SchemaVersion,
        };

        if (!string.IsNullOrWhiteSpace(captureEvent.TraceId))
        {
            tags["queryduck.trace_id"] = captureEvent.TraceId;
        }

        if (!string.IsNullOrWhiteSpace(captureEvent.SpanId))
        {
            tags["queryduck.span_id"] = captureEvent.SpanId;
        }

        if (!string.IsNullOrWhiteSpace(captureEvent.CorrelationId))
        {
            tags["queryduck.correlation_id"] = captureEvent.CorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(captureEvent.RequestPath))
        {
            tags["queryduck.request_path"] = captureEvent.RequestPath;
        }

        if (!captureEvent.Succeeded)
        {
            tags["queryduck.error_message"] = captureEvent.ErrorMessage;
            tags["queryduck.exception_type"] = captureEvent.ExceptionType;
        }

        if (options.IncludeSqlText)
        {
            tags["db.statement"] = captureEvent.Sql;
        }

        if (options.IncludeParameters && captureEvent.Parameters.Count > 0)
        {
            foreach (var (name, value) in captureEvent.Parameters)
            {
                tags[$"db.parameter.{name}"] = value?.ToString();
            }
        }

        if (options.IncludeDiagnostics && captureEvent.Diagnostics.Count > 0)
        {
            tags["queryduck.diagnostics.count"] = captureEvent.Diagnostics.Count;
        }

        return tags;
    }
}
