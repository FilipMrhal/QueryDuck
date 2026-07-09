using System.Diagnostics;
using QueryDuck.Core.Capture;

namespace QueryDuck.OpenTelemetry;

public sealed class QueryDuckOpenTelemetryEventPublisher : IQueryCaptureEventPublisher
{
    private static readonly ActivitySource SharedSource = new("QueryDuck", "1.0.0");

    private readonly ActivitySource _activitySource;
    private readonly QueryDuckOpenTelemetryOptions _options;

    public QueryDuckOpenTelemetryEventPublisher(QueryDuckOpenTelemetryOptions? options = null, ActivitySource? activitySource = null)
    {
        _options = options ?? new QueryDuckOpenTelemetryOptions();
        _activitySource = activitySource ?? SharedSource;
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

        var tags = QueryDuckOpenTelemetryEnricher.BuildTags(captureEvent, context, _options);
        using var activity = _activitySource.StartActivity(
            context.IsFailure ? "queryduck.sql.failure" : context.IsSlow ? "queryduck.sql.slow" : "queryduck.sql",
            ActivityKind.Client,
            parentContext: default,
            tags: tags);

        activity?.SetTag("queryduck.caller", captureEvent.Caller);
        activity?.SetStatus(context.IsFailure ? ActivityStatusCode.Error : ActivityStatusCode.Ok);

        return Task.CompletedTask;
    }

    private bool ShouldExport(QueryCapturePublishContext context)
    {
        if (context.IsFailure || context.IsSlow)
        {
            return true;
        }

        return _options.RecordSuccessfulQueries && !_options.RecordSlowQueriesOnly;
    }
}
