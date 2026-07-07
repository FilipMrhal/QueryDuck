using QueryDuck.Core.Providers;
using QueryDuck.MySql;
using QueryDuck.Oracle;
using QueryDuck.PostgreSql;
using QueryDuck.SqlServer;

namespace QueryDuck.Core.Adapters;

public sealed class DatabaseAdapterRegistry
{
    private readonly Dictionary<DatabaseProvider, IDatabaseAdapter> _adapters = [];

    public DatabaseAdapterRegistry Register(IDatabaseAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _adapters[adapter.Provider] = adapter;
        return this;
    }

    public IDatabaseAdapter? Resolve(DatabaseProvider provider) =>
        _adapters.TryGetValue(provider, out var adapter) ? adapter : null;

    public IDatabaseAdapter? Resolve(string? providerName) =>
        Resolve(DatabaseProviderNames.FromProviderName(providerName));

    /// <summary>
    /// Creates an empty registry. Use the AddOracle/AddPostgreSql/AddSqlServer/AddMySql
    /// extension methods, or <see cref="CreateWithAllProviders"/> to register everything.
    /// </summary>
    public static DatabaseAdapterRegistry CreateDefault() =>
        new DatabaseAdapterRegistry();

    /// <summary>
    /// Creates a registry with all built-in provider adapters registered.
    /// </summary>
    public static DatabaseAdapterRegistry CreateWithAllProviders() =>
        new DatabaseAdapterRegistry()
            .AddOracle()
            .AddPostgreSql()
            .AddSqlServer()
            .AddMySql();
}
