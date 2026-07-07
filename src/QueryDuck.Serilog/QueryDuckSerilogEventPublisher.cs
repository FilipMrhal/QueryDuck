using QueryDuck.Core.Capture;
using Serilog;
using Serilog.Events;

namespace QueryDuck.Serilog;

public sealed class QueryDuckSerilogEventPublisher : IQueryCaptureEventPublisher
{
    private readonly ILogger _logger;
    private readonly QueryDuckSerilogOptions _options;

    public QueryDuckSerilogEventPublisher(ILogger logger, QueryDuckSerilogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger.ForContext("QueryDuck.Exporter", "Serilog");
        _options = options ?? new QueryDuckSerilogOptions();
    }

    public Task PublishAsync(
        QueryCaptureEvent captureEvent,
        QueryCapturePublishContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(captureEvent);
        ArgumentNullException.ThrowIfNull(context);

        if (!ShouldExport(context))
        {
            return Task.CompletedTask;
        }

        var properties = QueryDuckSerilogEnricher.BuildProperties(captureEvent, context, _options);
        var level = ResolveLevel(context);
        var outcome = context.IsFailure
            ? "SQL failure"
            : context.IsSlow
                ? "slow query"
                : "SQL executed";

        _logger
            .ForContext("QueryDuck", properties, destructureObjects: true)
            .Write(
                level,
                "QueryDuck {Outcome} on {Provider} in {DurationMs} ms (threshold {SlowQueryThresholdMs} ms)",
                outcome,
                captureEvent.Provider,
                captureEvent.Duration.TotalMilliseconds,
                context.SlowQueryThresholdMs);

        return Task.CompletedTask;
    }

    private bool ShouldExport(QueryCapturePublishContext context)
    {
        if (context.IsFailure)
        {
            return _options.LogSqlFailures;
        }

        if (context.IsSlow)
        {
            return _options.LogSlowQueries;
        }

        return _options.LogSuccessfulQueries;
    }

    private LogEventLevel ResolveLevel(QueryCapturePublishContext context)
    {
        if (context.IsFailure)
        {
            return _options.FailureLevel;
        }

        if (context.IsSlow)
        {
            return _options.SlowQueryLevel;
        }

        return LogEventLevel.Information;
    }
}
