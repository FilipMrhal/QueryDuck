namespace QueryDuck.Core.Capture;

public interface IQueryCaptureEventPublisher
{
    Task PublishAsync(
        QueryCaptureEvent captureEvent,
        QueryCapturePublishContext context,
        CancellationToken cancellationToken = default);
}

public sealed class QueryCapturePublishContext
{
    public bool IsSlow { get; init; }

    public bool IsFailure => !CaptureEvent.Succeeded;

    public int SlowQueryThresholdMs { get; init; }

    public required QueryCaptureEvent CaptureEvent { get; init; }
}
