using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.Performance;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Capture;

public static class QueryCaptureEventFactory
{
    public static QueryCaptureEvent Create(
        DbCommand command,
        CommandEventData eventData,
        DatabaseProvider provider,
        IReadOnlyDictionary<string, object?> parameters,
        string? planText,
        string? planHash,
        TimeSpan duration = default,
        SlowQueryImprovementAnalysisDto? improvementAnalysis = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(eventData);
        return Create(
            command.CommandText,
            provider,
            parameters,
            planText,
            planHash,
            duration,
            eventData.CommandSource.ToString(),
            QueryCaptureSources.EfCore,
            bulkOperation: null,
            improvementAnalysis);
    }

    public static QueryCaptureEvent Create(
        string sql,
        DatabaseProvider provider,
        IReadOnlyDictionary<string, object?> parameters,
        string? planText,
        string? planHash,
        TimeSpan duration,
        string caller,
        string source,
        string? bulkOperation,
        SlowQueryImprovementAnalysisDto? improvementAnalysis = null,
        bool succeeded = true,
        string? errorMessage = null,
        string? exceptionType = null,
        SourceLocation? sourceLocation = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        var pending = QueryDuckCaptureScope.TakePending();
        var diagnostics = pending?.Diagnostics
            .Select(d => new QueryDiagnosticDto(d.RuleId, d.Severity.ToString(), d.Message, d.FixHint))
            .ToArray() ?? [];
        var correlation = QueryCaptureCorrelation.ReadCurrent();

        return new QueryCaptureEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            Timestamp = DateTimeOffset.UtcNow,
            Sql = sql,
            Provider = provider.ToString(),
            Source = source,
            BulkOperation = bulkOperation,
            Tag = ExtractTag(sql),
            Caller = caller,
            Duration = duration,
            Parameters = parameters,
            Diagnostics = diagnostics,
            Warnings = diagnostics.Select(d => $"[{d.RuleId}] {d.Message}").ToArray(),
            ExpressionTree = pending?.ExpressionTree,
            ExpressionTreeText = pending?.ExpressionTreeText,
            ExpressionCSharp = pending?.ExpressionCSharp,
            ExecutionPlan = planText,
            PlanHash = planHash,
            ImprovementAnalysis = improvementAnalysis,
            Succeeded = succeeded,
            ErrorMessage = errorMessage,
            ExceptionType = exceptionType,
            TraceId = correlation.TraceId,
            SpanId = correlation.SpanId,
            CorrelationId = correlation.CorrelationId,
            RequestPath = correlation.RequestPath,
            SourceLocation = sourceLocation,
            SchemaVersion = 8,
        };
    }

    internal static string? ExtractTag(string sql)
    {
        foreach (var line in sql.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith("--", StringComparison.Ordinal))
            {
                break;
            }

            var tag = line[2..].TrimStart();
            if (!string.IsNullOrWhiteSpace(tag))
            {
                return tag;
            }
        }

        return null;
    }
}
