using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Capture;

namespace QueryDuck.Core;

public static class QueryDuckOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder<TContext> UseQueryDuckCapture<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        Action<QueryCaptureOptions>? configure = null,
        DatabaseAdapterRegistry? adapters = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        var options = new QueryCaptureOptions();
        configure?.Invoke(options);
        EnsureEventServer(options);

        var interceptors = new List<IInterceptor>
        {
            new QueryCaptureInterceptor(options, adapters),
        };

        if (options.AutoCaptureAllQueries)
        {
            interceptors.Add(new QueryDuckAutoCaptureInterceptor());
        }

        optionsBuilder.AddInterceptors(interceptors.ToArray());
        return optionsBuilder;
    }

    /// <summary>
    /// Enables capture, auto-attaches expression trees to all queries, starts the local HTTP server for Rider, and wires interceptors.
    /// </summary>
    public static DbContextOptionsBuilder<TContext> UseQueryDuckDebugging<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        Action<QueryCaptureOptions>? configure = null,
        DatabaseAdapterRegistry? adapters = null)
        where TContext : DbContext
    {
        return optionsBuilder.UseQueryDuckCapture(o =>
        {
            o.PublishEvents = true;
            o.StartLocalEventServer = true;
            o.AutoCaptureAllQueries = true;
            configure?.Invoke(o);
        }, adapters);
    }

    internal static void EnsureEventServer(QueryCaptureOptions options)
    {
        if (options.StartLocalEventServer)
        {
            QueryDuckEventServerHost.EnsureStarted(options.ServerPrefix);
        }
    }
}
