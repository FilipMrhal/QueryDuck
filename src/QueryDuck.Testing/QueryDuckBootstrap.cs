using QueryDuck.Core.Adapters;

namespace QueryDuck.Testing;

public static class QueryDuckBootstrap
{
    public static DatabaseAdapterRegistry CreateDefaultRegistry() =>
        DatabaseAdapterRegistry.CreateWithAllProviders();
}
