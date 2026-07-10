using System.Data.Common;
using Microsoft.EntityFrameworkCore.Metadata;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Providers;

namespace QueryDuck.Oracle;

public sealed class OracleDatabaseAdapter : IDatabaseAdapter
{
    public DatabaseProvider Provider => DatabaseProvider.Oracle;

    public async Task<SchemaAuditResult> AuditSchemaAsync(
        IModel model,
        DbConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(connection);
        var columns = await ReadColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        return SchemaAuditHelper.Compare(model, columns, defaultSchema: ProviderSchemaHelper.DefaultSchemaForAudit(Provider, connection));
    }

    public async Task<ExecutionPlanResult> GetExecutionPlanAsync(
        DbConnection connection,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);

        await using var explain = ExplainCommandHelper.CreateCommand(connection, $"EXPLAIN PLAN FOR {sql}", parameters);
        await explain.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var display = connection.CreateCommand();
        display.CommandText = "SELECT PLAN_TABLE_OUTPUT FROM TABLE(DBMS_XPLAN.DISPLAY())";
        var plan = new System.Text.StringBuilder();
        await using var reader = await display.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            plan.AppendLine(reader.GetString(0));
        }

        var planText = plan.ToString();
        return new ExecutionPlanResult(planText, SchemaAuditHelper.ComputePlanHash(planText));
    }

    public async Task<IReadOnlyList<StatementCacheFinding>> GetStatementCacheDiagnosticsAsync(
        DbConnection connection,
        CancellationToken cancellationToken = default) =>
        await StatementCacheDiagnosticsHelper.QueryAsync(
            connection,
            $"""
            SELECT FORCE_MATCHING_SIGNATURE, COUNT(*) AS VARIANT_COUNT
            FROM V$SQL
            WHERE FORCE_MATCHING_SIGNATURE > 0
            GROUP BY FORCE_MATCHING_SIGNATURE
            HAVING COUNT(*) > {DiagnosticsLimits.StatementCacheVariantThreshold}
            ORDER BY COUNT(*) DESC
            FETCH FIRST {DiagnosticsLimits.StatementCacheResultLimit} ROWS ONLY
            """,
            reader =>
            {
                var signature = reader.GetValue(0)?.ToString() ?? "unknown";
                var count = reader.GetInt32(1);
                return new StatementCacheFinding(
                    signature,
                    count,
                    $"Oracle hard-parse risk: {count} SQL variants share signature {signature}.");
            },
            ex => $"V$SQL diagnostics unavailable: {ex.Message}",
            cancellationToken).ConfigureAwait(false);

    public Task<QueryHistoricalStatsInsight?> TryMatchHistoricalStatsAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken = default) =>
        HistoricalStatsQueryHelper.TryMatchAsync(
            connection,
            sql,
            $"""
            SELECT sql_text,
                   executions,
                   elapsed_time / NULLIF(executions, 0) / 1000 AS mean_ms,
                   elapsed_time / 1000 AS total_ms,
                   rows_processed,
                   buffer_gets,
                   disk_reads
            FROM (
                SELECT sql_text, executions, elapsed_time, rows_processed, buffer_gets, disk_reads
                FROM v$sql
                WHERE sql_text IS NOT NULL
                ORDER BY elapsed_time / NULLIF(executions, 0) DESC
                FETCH FIRST {DiagnosticsLimits.HistoricalStatsSampleSize} ROWS ONLY
            )
            """,
            MapHistoricalStatsRow,
            cancellationToken);

    private static QueryHistoricalStatsInsight? MapHistoricalStatsRow(DbDataReader reader, string sql) =>
        HistoricalStatsRowMapper.TryMap(
            reader,
            sql,
            r => r.GetString(0),
            (r, queryText) =>
            {
                var calls = r.GetInt64(1);
                var meanMs = r.IsDBNull(2) ? 0 : Convert.ToDouble(r.GetValue(2));
                var totalMs = r.IsDBNull(3) ? 0 : Convert.ToDouble(r.GetValue(3));
                var rows = r.IsDBNull(4) ? 0 : r.GetInt64(4);
                var bufferGets = r.IsDBNull(5) ? 0 : r.GetInt64(5);
                var diskReads = r.IsDBNull(6) ? 0 : r.GetInt64(6);
                var ratio = bufferGets + diskReads == 0
                    ? (double?)null
                    : (bufferGets - diskReads) / (double)Math.Max(1, bufferGets);

                return new QueryHistoricalStatsInsight(
                    calls,
                    meanMs,
                    totalMs,
                    rows,
                    ratio,
                    queryText,
                    "V$SQL");
            });

    private static Task<List<SchemaColumnInfo>> ReadColumnsAsync(
        DbConnection connection,
        CancellationToken cancellationToken) =>
        SchemaColumnReader.QueryAsync(
            connection,
            """
            SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, NULLABLE, DATA_LENGTH, DATA_PRECISION, DATA_SCALE
            FROM ALL_TAB_COLUMNS
            WHERE OWNER = USER
            """,
            reader => SchemaColumnReader.MapOracleRow(reader),
            cancellationToken);
}

public static class OracleQueryDuckExtensions
{
    public static DatabaseAdapterRegistry AddOracle(this DatabaseAdapterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.Register(new OracleDatabaseAdapter());
    }
}
