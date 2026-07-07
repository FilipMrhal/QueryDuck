using QueryDuck.Core.Capture;
using Serilog;

namespace QueryDuck.Serilog;

public static class QueryCaptureOptionsSerilogExtensions
{
    /// <summary>
    /// Registers a Serilog exporter that emits structured logs for SQL failures and slow queries.
    /// Sensitive data and PII are excluded by default; opt in via <see cref="QueryDuckSerilogOptions.SensitiveData"/>.
    /// </summary>
    public static QueryCaptureOptions AddSerilogExporter(
        this QueryCaptureOptions options,
        ILogger logger,
        Action<QueryDuckSerilogOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        var serilogOptions = new QueryDuckSerilogOptions();
        configure?.Invoke(serilogOptions);
        options.EventPublishers.Add(new QueryDuckSerilogEventPublisher(logger, serilogOptions));
        return options;
    }
}
