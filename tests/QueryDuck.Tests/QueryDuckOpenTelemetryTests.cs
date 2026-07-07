using QueryDuck.Core.Capture;
using QueryDuck.OpenTelemetry;

namespace QueryDuck.Tests;

public sealed class QueryDuckOpenTelemetryTests
{
    [Fact]
    public async Task OpenTelemetryPublisher_exports_slow_query_events()
    {
        var options = new QueryCaptureOptions();
        options.AddOpenTelemetryExporter(o =>
        {
            o.IncludeSqlText = true;
            o.RecordSuccessfulQueries = true;
            o.RecordSlowQueriesOnly = false;
        });

        var publisher = options.EventPublishers.OfType<QueryDuckOpenTelemetryEventPublisher>().Single();
        var captureEvent = new QueryCaptureEvent
        {
            EventId = "otel-1",
            Timestamp = DateTimeOffset.UtcNow,
            Sql = "SELECT 1",
            Provider = "Sqlite",
            Duration = TimeSpan.FromMilliseconds(900),
            TraceId = "00-abc-def-01",
            SpanId = "span-1",
            SchemaVersion = 7,
        };

        var context = new QueryCapturePublishContext
        {
            CaptureEvent = captureEvent,
            IsSlow = true,
            SlowQueryThresholdMs = 500,
        };

        await publisher.PublishAsync(captureEvent, context);
    }
}
