using Microsoft.EntityFrameworkCore;
using QueryDuck.Core;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Capture;
using Serilog;

namespace QueryDuck.Serilog;

public static class QueryDuckSerilogOptionsBuilderExtensions
{
    /// <summary>
    /// Production preset: exports SQL failures and slow queries to Serilog while the local HTTP
    /// event server stays off. Both defaults can be overridden via <paramref name="configure"/>,
    /// e.g. <c>o.StartLocalEventServer = true</c> to re-enable the server.
    /// </summary>
    public static DbContextOptionsBuilder<TContext> UseQueryDuckProduction<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        ILogger logger,
        Action<QueryDuckSerilogOptions>? configureSerilog = null,
        Action<QueryCaptureOptions>? configure = null,
        DatabaseAdapterRegistry? adapters = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(logger);

        return optionsBuilder.UseQueryDuckCapture(
            o => ConfigureProductionDefaults(o, logger, configureSerilog, configure),
            adapters);
    }

    internal static void ConfigureProductionDefaults(
        QueryCaptureOptions options,
        ILogger logger,
        Action<QueryDuckSerilogOptions>? configureSerilog,
        Action<QueryCaptureOptions>? configure)
    {
        options.StartLocalEventServer = false;
        options.PublishEvents = false;
        options.AddSerilogExporter(logger, configureSerilog);
        configure?.Invoke(options);
    }

    /// <summary>
    /// Production sampling preset: exports failures and slow queries to Serilog while sampling fast successful queries.
    /// </summary>
    public static DbContextOptionsBuilder<TContext> UseQueryDuckProductionSampling<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        ILogger logger,
        double samplingRate = 0.05,
        Action<QueryDuckSerilogOptions>? configureSerilog = null,
        Action<QueryCaptureOptions>? configure = null,
        DatabaseAdapterRegistry? adapters = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(logger);

        return optionsBuilder.UseQueryDuckCapture(
            o =>
            {
                ConfigureProductionDefaults(o, logger, configureSerilog, configure);
                o.EnableSampling = true;
                o.SamplingRate = samplingRate;
            },
            adapters);
    }
}
