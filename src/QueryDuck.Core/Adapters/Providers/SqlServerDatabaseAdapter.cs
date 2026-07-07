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

    public async Task<QueryHistoricalStatsInsight?> TryMatchHistoricalStatsAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP (200)
                       SUBSTRING(st.text, (qs.statement_start_offset / 2) + 1,
                           ((CASE qs.statement_end_offset WHEN -1 THEN DATALENGTH(st.text)
                            ELSE qs.statement_end_offset END - qs.statement_start_offset) / 2) + 1) AS query_text,
                       qs.execution_count,
                       qs.total_elapsed_time / 1000.0 / NULLIF(qs.execution_count, 0) AS mean_ms,
                       qs.total_elapsed_time / 1000.0 AS total_ms,
                       qs.total_rows,
                       qs.total_logical_reads,
                       qs.total_physical_reads
                FROM sys.dm_exec_query_stats qs
                CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
                ORDER BY mean_ms DESC
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var queryText = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                if (!QueryHistoricalStatsSqlMatcher.IsLikelyMatch(sql, queryText))
                {
                    continue;
                }

                var calls = reader.GetInt64(1);
                var meanMs = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
                var totalMs = reader.IsDBNull(3) ? 0 : reader.GetDouble(3);
                var rows = reader.IsDBNull(4) ? 0 : reader.GetInt64(4);
                var logicalReads = reader.IsDBNull(5) ? 0 : reader.GetInt64(5);
                var physicalReads = reader.IsDBNull(6) ? 0 : reader.GetInt64(6);
                var ratio = logicalReads + physicalReads == 0
                    ? (double?)null
                    : logicalReads / (double)(logicalReads + physicalReads);

                return new QueryHistoricalStatsInsight(
                    calls,
                    meanMs,
                    totalMs,
                    rows,
                    ratio,
                    queryText,
                    "sys.dm_exec_query_stats");
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
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
