using System.Data.Common;
using Microsoft.EntityFrameworkCore.Metadata;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Providers;

namespace QueryDuck.SqlServer;

public sealed class SqlServerDatabaseAdapter : IDatabaseAdapter
{
    public DatabaseProvider Provider => DatabaseProvider.SqlServer;

    public async Task<SchemaAuditResult> AuditSchemaAsync(
        IModel model,
        DbConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(connection);
        var columns = await ReadColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        return SchemaAuditHelper.Compare(model, columns, defaultSchema: "dbo");
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
        command.CommandText = sql;
        var planText = command.ExecuteExplainPlanXml();
        return await Task.FromResult(new ExecutionPlanResult(planText, SchemaAuditHelper.ComputePlanHash(planText)))
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<StatementCacheFinding>> GetStatementCacheDiagnosticsAsync(
        DbConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var findings = new List<StatementCacheFinding>();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP (20) query_hash, COUNT(*) AS variant_count
                FROM sys.dm_exec_query_stats
                GROUP BY query_hash
                HAVING COUNT(*) > 5
                ORDER BY COUNT(*) DESC
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var hash = reader.GetValue(0)?.ToString() ?? "unknown";
                var count = reader.GetInt32(1);
                findings.Add(new StatementCacheFinding(hash, count, $"SQL Server plan cache: {count} variants for query_hash {hash}."));
            }
        }
        catch (Exception ex)
        {
            findings.Add(new StatementCacheFinding("unsupported", 0, $"DMV diagnostics unavailable: {ex.Message}"));
        }

        return findings;
    }

    private static async Task<List<SchemaColumnInfo>> ReadColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var columns = new List<SchemaColumnInfo>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.name, c.name, ty.name, c.is_nullable, c.max_length, c.precision, c.scale
            FROM sys.columns c
            INNER JOIN sys.tables t ON c.object_id = t.object_id
            INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(new SchemaColumnInfo(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetValue(4)),
                reader.IsDBNull(5) ? null : Convert.ToInt32(reader.GetValue(5)),
                reader.IsDBNull(6) ? null : Convert.ToInt32(reader.GetValue(6))));
        }

        return columns;
    }
}

internal static class SqlServerExplainExtensions
{
    public static string ExecuteExplainPlanXml(this DbCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        using var setShowPlan = command.Connection!.CreateCommand();
        setShowPlan.CommandText = "SET SHOWPLAN_XML ON";
        setShowPlan.ExecuteNonQuery();

        try
        {
            using var reader = command.ExecuteReader();
            return reader.Read() ? reader.GetString(0) : string.Empty;
        }
        finally
        {
            using var unset = command.Connection.CreateCommand();
            unset.CommandText = "SET SHOWPLAN_XML OFF";
            unset.ExecuteNonQuery();
        }
    }
}

public static class SqlServerQueryDuckExtensions
{
    public static DatabaseAdapterRegistry AddSqlServer(this DatabaseAdapterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.Register(new SqlServerDatabaseAdapter());
    }
}
