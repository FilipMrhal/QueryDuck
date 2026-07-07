using QueryDuck.Core.Capture;

namespace QueryDuck.OpenTelemetry;

public static class QueryCaptureOptionsOpenTelemetryExtensions
{
    /// <summary>
    /// Registers an OpenTelemetry exporter that emits QueryDuck capture events as <see cref="System.Diagnostics.Activity"/> spans.
    /// </summary>
    public static QueryCaptureOptions AddOpenTelemetryExporter(
        this QueryCaptureOptions options,
        Action<QueryDuckOpenTelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var otelOptions = new QueryDuckOpenTelemetryOptions();
        configure?.Invoke(otelOptions);
        options.EventPublishers.Add(new QueryDuckOpenTelemetryEventPublisher(otelOptions));
        return options;
    }
}
