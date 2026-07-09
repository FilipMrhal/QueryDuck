using System.Data.Common;
using Microsoft.EntityFrameworkCore.Metadata;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Providers;

namespace QueryDuck.Sqlite;

public sealed class SqliteDatabaseAdapter : IDatabaseAdapter
{
    public DatabaseProvider Provider => DatabaseProvider.Sqlite;

    public async Task<SchemaAuditResult> AuditSchemaAsync(
        IModel model,
        DbConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(connection);
        var columns = await ReadColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        return SchemaAuditHelper.Compare(model, columns, defaultSchema: "main");
    }

    public async Task<ExecutionPlanResult> GetExecutionPlanAsync(
        DbConnection connection,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);

        await using var command = connection.CreateCommand();
        command.CommandText = $"EXPLAIN QUERY PLAN {sql}";
        var plan = new System.Text.StringBuilder();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            plan.AppendLine($"{reader.GetValue(0)}|{reader.GetValue(1)}|{reader.GetValue(2)}|{reader.GetValue(3)}");
        }

        var planText = plan.ToString();
        return new ExecutionPlanResult(planText, SchemaAuditHelper.ComputePlanHash(planText));
    }

    public Task<IReadOnlyList<StatementCacheFinding>> GetStatementCacheDiagnosticsAsync(
        DbConnection connection,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StatementCacheFinding>>([]);

    public Task<QueryHistoricalStatsInsight?> TryMatchHistoricalStatsAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<QueryHistoricalStatsInsight?>(null);

    private static async Task<List<SchemaColumnInfo>> ReadColumnsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var columns = new List<SchemaColumnInfo>();
        var tableNames = new List<string>();

        await using (var listTables = connection.CreateCommand())
        {
            listTables.CommandText = """
                SELECT name FROM sqlite_master
                WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
                """;
            await using var tablesReader = await listTables.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await tablesReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                tableNames.Add(tablesReader.GetString(0));
            }
        }

        foreach (var table in tableNames)
        {
            await using var pragma = connection.CreateCommand();
            pragma.CommandText = $"PRAGMA table_info('{table.Replace("'", "''", StringComparison.Ordinal)}')";
            await using var reader = await pragma.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                columns.Add(new SchemaColumnInfo(
                    table,
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3) == 0,
                    null,
                    null,
                    null));
            }
        }

        return columns;
    }
}

public static class SqliteQueryDuckExtensions
{
    public static DatabaseAdapterRegistry AddSqlite(this DatabaseAdapterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.Register(new SqliteDatabaseAdapter());
    }
}
