using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Capture;

namespace QueryDuck.EntityFrameworkExtensions;

public static class QueryDuckEntityFrameworkExtensionsBootstrap
{
    /// <summary>
    /// Enables QueryDuck capture hooks for Z.EntityFramework.Extensions bulk/batch operations.
    /// </summary>
    public static DbContextOptionsBuilder<TContext> UseQueryDuckEntityFrameworkExtensions<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder,
        DatabaseAdapterRegistry? adapters = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        QueryDuckEntityFrameworkExtensionsIntegration.Enable(QueryDuckCaptureRuntime.CurrentOptions, adapters);
        return optionsBuilder;
    }
}
