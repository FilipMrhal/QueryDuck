using System.Data.Common;
using Microsoft.EntityFrameworkCore.Metadata;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Providers;

namespace QueryDuck.MySql;

public sealed class MySqlDatabaseAdapter : IDatabaseAdapter
{
    public DatabaseProvider Provider => DatabaseProvider.MySql;

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

        await using var command = ExplainCommandHelper.CreateCommand(connection, $"EXPLAIN FORMAT=JSON {sql}", parameters);
        var planText = string.Empty;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            planText += reader.GetValue(0)?.ToString();
        }

        return new ExecutionPlanResult(planText, SchemaAuditHelper.ComputePlanHash(planText));
    }

    public async Task<IReadOnlyList<StatementCacheFinding>> GetStatementCacheDiagnosticsAsync(
        DbConnection connection,
        CancellationToken cancellationToken = default) =>
        await StatementCacheDiagnosticsHelper.QueryAsync(
            connection,
            $"""
            SELECT DIGEST_TEXT, COUNT_STAR
            FROM performance_schema.events_statements_summary_by_digest
            WHERE COUNT_STAR > {DiagnosticsLimits.StatementCacheVariantThreshold}
            ORDER BY COUNT_STAR DESC
            LIMIT {DiagnosticsLimits.StatementCacheResultLimit}
            """,
            reader => new StatementCacheFinding(
                reader.GetString(0),
                Convert.ToInt32(reader.GetValue(1)),
                $"MySQL digest executed {reader.GetValue(1)} times: {reader.GetString(0)}"),
            ex => $"performance_schema unavailable: {ex.Message}",
            cancellationToken).ConfigureAwait(false);

    public Task<QueryHistoricalStatsInsight?> TryMatchHistoricalStatsAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken = default) =>
        HistoricalStatsQueryHelper.TryMatchAsync(
            connection,
            sql,
            $"""
            SELECT DIGEST_TEXT,
                   COUNT_STAR,
                   SUM_TIMER_WAIT / NULLIF(COUNT_STAR, 0) / 1000000000 AS mean_ms,
                   SUM_TIMER_WAIT / 1000000000 AS total_ms,
                   SUM_ROWS_SENT,
                   SUM_NO_INDEX_USED
            FROM performance_schema.events_statements_summary_by_digest
            ORDER BY mean_ms DESC
            LIMIT {DiagnosticsLimits.HistoricalStatsSampleSize}
            """,
            MapHistoricalStatsRow,
            cancellationToken);

    private static QueryHistoricalStatsInsight? MapHistoricalStatsRow(DbDataReader reader, string sql) =>
        HistoricalStatsRowMapper.TryMap(
            reader,
            sql,
            r => r.GetString(0),
            (r, queryText) => new QueryHistoricalStatsInsight(
                Convert.ToInt64(r.GetValue(1)),
                r.IsDBNull(2) ? 0 : Convert.ToDouble(r.GetValue(2)),
                r.IsDBNull(3) ? 0 : Convert.ToDouble(r.GetValue(3)),
                r.IsDBNull(4) ? 0 : Convert.ToInt64(r.GetValue(4)),
                null,
                queryText,
                "events_statements_summary_by_digest"));

    private static Task<List<SchemaColumnInfo>> ReadColumnsAsync(DbConnection connection, CancellationToken cancellationToken) =>
        SchemaColumnReader.QueryAsync(
            connection,
            """
            SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
            """,
            reader => SchemaColumnReader.MapStringNullableRow(reader),
            cancellationToken);
}

public static class MySqlQueryDuckExtensions
{
    public static DatabaseAdapterRegistry AddMySql(this DatabaseAdapterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.Register(new MySqlDatabaseAdapter());
    }
}
