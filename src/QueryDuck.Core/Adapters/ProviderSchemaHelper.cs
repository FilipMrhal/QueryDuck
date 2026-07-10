using System.Data.Common;
using QueryDuck.Core.Providers;

namespace QueryDuck.Core.Adapters;

internal static class ProviderSchemaHelper
{
    public static string DefaultSchema(DatabaseProvider provider) =>
        provider switch
        {
            DatabaseProvider.PostgreSql => "public",
            DatabaseProvider.SqlServer => "dbo",
            DatabaseProvider.Oracle => "PUBLIC",
            DatabaseProvider.MySql => string.Empty,
            DatabaseProvider.Sqlite => "main",
            _ => "public",
        };

    public static string DefaultSchemaForAudit(DatabaseProvider provider, DbConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return provider switch
        {
            DatabaseProvider.MySql => connection.Database,
            DatabaseProvider.Oracle => connection.Database.ToUpperInvariant(),
            _ => DefaultSchema(provider),
        };
    }

    public static (string Schema, string Table) ResolveTableReference(string tableReference, DatabaseProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableReference);

        var parts = tableReference
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Trim('"', '[', ']', '`'))
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length switch
        {
            >= 2 => (parts[^2], parts[^1]),
            1 => (DefaultSchema(provider), parts[0]),
            _ => (DefaultSchema(provider), tableReference),
        };
    }
}
