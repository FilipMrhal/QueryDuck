using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Capture;

internal static class QueryDuckSaveChangesCapture
{
    public static void Record(DbContext? context, int callCount)
    {
        if (callCount <= 1)
        {
            return;
        }

        var provider = DatabaseProviderNames.FromProviderName(context?.Database.ProviderName);
        var diagnostic = new QueryDiagnosticDto(
            "QD014",
            QueryDiagnosticSeverity.Warning.ToString(),
            $"Repeated SaveChanges detected ({callCount} calls this session) — consider batching updates or a single unit-of-work commit.",
            "Batch entity updates and commit once per unit of work instead of calling SaveChanges multiple times.");

        var correlation = QueryCaptureCorrelation.ReadCurrent();
        var captureEvent = new QueryCaptureEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            Timestamp = DateTimeOffset.UtcNow,
            Sql = $"-- SaveChanges #{callCount}",
            Provider = provider.ToString(),
            Source = QueryCaptureSources.EfCore,
            Caller = "SaveChanges",
            Tag = "SaveChanges",
            Diagnostics = [diagnostic],
            Warnings = [$"[QD014] {diagnostic.Message}"],
            TraceId = correlation.TraceId,
            SpanId = correlation.SpanId,
            CorrelationId = correlation.CorrelationId,
            RequestPath = correlation.RequestPath,
            SchemaVersion = 8,
        };

        QueryDuckCapture.Record(captureEvent);
    }
}
