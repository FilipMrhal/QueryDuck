using System.Data.Common;
using Microsoft.EntityFrameworkCore.Metadata;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Providers;

namespace QueryDuck.PostgreSql;

public sealed class PostgreSqlDatabaseAdapter : IDatabaseAdapter
{
    public DatabaseProvider Provider => DatabaseProvider.PostgreSql;

    public async Task<SchemaAuditResult> AuditSchemaAsync(
        IModel model,
        DbConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(connection);
        var columns = await ReadColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        return SchemaAuditHelper.Compare(model, columns, defaultSchema: ProviderSchemaHelper.DefaultSchema(Provider));
    }

    public async Task<ExecutionPlanResult> GetExecutionPlanAsync(
        DbConnection connection,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);

        await using var command = ExplainCommandHelper.CreateCommand(connection, $"EXPLAIN (FORMAT JSON) {sql}", parameters);
        var planText = (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))?.ToString() ?? string.Empty;
        return new ExecutionPlanResult(planText, SchemaAuditHelper.ComputePlanHash(planText));
    }

    public async Task<IReadOnlyList<StatementCacheFinding>> GetStatementCacheDiagnosticsAsync(
        DbConnection connection,
        CancellationToken cancellationToken = default) =>
        await StatementCacheDiagnosticsHelper.QueryAsync(
            connection,
            $"""
            SELECT queryid::text, COUNT(*) AS variant_count
            FROM pg_stat_statements
            GROUP BY queryid
            HAVING COUNT(*) > {DiagnosticsLimits.StatementCacheVariantThreshold}
            ORDER BY COUNT(*) DESC
            LIMIT {DiagnosticsLimits.StatementCacheResultLimit}
            """,
            reader => new StatementCacheFinding(
                reader.GetString(0),
                reader.GetInt32(1),
                $"PostgreSQL plan cache: {reader.GetInt32(1)} variants for query id {reader.GetString(0)}."),
            ex => $"pg_stat_statements unavailable: {ex.Message}",
            cancellationToken).ConfigureAwait(false);

    public async Task<PgStatStatementInsight?> TryMatchPgStatStatementAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken = default)
    {
        var insight = await TryMatchHistoricalStatsAsync(connection, sql, cancellationToken).ConfigureAwait(false);
        return insight is null ? null : PgStatStatementInsight.FromHistoricalStats(insight);
    }

    public Task<QueryHistoricalStatsInsight?> TryMatchHistoricalStatsAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken = default) =>
        HistoricalStatsQueryHelper.TryMatchAsync(
            connection,
            sql,
            $"""
            SELECT query,
                   calls,
                   mean_exec_time,
                   total_exec_time,
                   rows,
                   shared_blks_hit,
                   shared_blks_read
            FROM pg_stat_statements
            ORDER BY mean_exec_time DESC
            LIMIT {DiagnosticsLimits.HistoricalStatsSampleSize}
            """,
            MapHistoricalStatsRow,
            cancellationToken);

    public async Task<IReadOnlyList<ColumnStatistics>> GetColumnStatisticsAsync(
        DbConnection connection,
        string schema,
        string table,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);

        var statistics = new List<ColumnStatistics>();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT attname,
                       n_distinct,
                       null_frac,
                       avg_width,
                       correlation
                FROM pg_stats
                WHERE schemaname = @schema AND tablename = @table
                """;

            var schemaParam = command.CreateParameter();
            schemaParam.ParameterName = "@schema";
            schemaParam.Value = schema;
            command.Parameters.Add(schemaParam);

            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@table";
            tableParam.Value = table;
            command.Parameters.Add(tableParam);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var nDistinct = reader.IsDBNull(1) ? (double?)null : reader.GetDouble(1);
                statistics.Add(new ColumnStatistics(
                    schema,
                    table,
                    reader.GetString(0),
                    nDistinct > 0 ? nDistinct : null,
                    nDistinct is < 0 ? Math.Abs(nDistinct.Value) : null,
                    reader.IsDBNull(2) ? 0 : reader.GetDouble(2),
                    reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    reader.IsDBNull(4) ? null : reader.GetDouble(4)));
            }
        }
        catch (Exception)
        {
            return [];
        }

        return statistics;
    }

    private static Task<List<SchemaColumnInfo>> ReadColumnsAsync(DbConnection connection, CancellationToken cancellationToken) =>
        SchemaColumnReader.QueryAsync(
            connection,
            """
            SELECT table_name, column_name, data_type, is_nullable, character_maximum_length, numeric_precision, numeric_scale
            FROM information_schema.columns
            WHERE table_schema = 'public'
            """,
            reader => SchemaColumnReader.MapStringNullableRow(reader),
            cancellationToken);

    private static QueryHistoricalStatsInsight? MapHistoricalStatsRow(DbDataReader reader, string sql) =>
        HistoricalStatsRowMapper.TryMap(
            reader,
            sql,
            r => r.GetString(0),
            (r, queryText) =>
            {
                var calls = r.GetInt64(1);
                var meanMs = r.GetDouble(2);
                var totalMs = r.GetDouble(3);
                var rows = r.GetInt64(4);
                var hit = r.GetInt64(5);
                var read = r.GetInt64(6);
                var ratio = hit + read == 0 ? 1.0 : hit / (double)(hit + read);

                return new QueryHistoricalStatsInsight(
                    calls,
                    meanMs,
                    totalMs,
                    rows,
                    ratio,
                    queryText,
                    "pg_stat_statements");
            });
}

public static class PostgreSqlQueryDuckExtensions
{
    public static DatabaseAdapterRegistry AddPostgreSql(this DatabaseAdapterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.Register(new PostgreSqlDatabaseAdapter());
    }
}
