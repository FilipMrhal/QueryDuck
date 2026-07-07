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
        return SchemaAuditHelper.Compare(model, columns, defaultSchema: "public");
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
        command.CommandText = $"EXPLAIN (FORMAT JSON) {sql}";
        var planText = (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))?.ToString() ?? string.Empty;
        return new ExecutionPlanResult(planText, SchemaAuditHelper.ComputePlanHash(planText));
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
                SELECT queryid::text, COUNT(*) AS variant_count
                FROM pg_stat_statements
                GROUP BY queryid
                HAVING COUNT(*) > 5
                ORDER BY COUNT(*) DESC
                LIMIT 20
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                findings.Add(new StatementCacheFinding(
                    reader.GetString(0),
                    reader.GetInt32(1),
                    $"PostgreSQL plan cache: {reader.GetInt32(1)} variants for query id {reader.GetString(0)}."));
            }
        }
        catch (Exception ex)
        {
            findings.Add(new StatementCacheFinding("unsupported", 0, $"pg_stat_statements unavailable: {ex.Message}"));
        }

        return findings;
    }

    public async Task<PgStatStatementInsight?> TryMatchPgStatStatementAsync(
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
                SELECT query,
                       calls,
                       mean_exec_time,
                       total_exec_time,
                       rows,
                       shared_blks_hit,
                       shared_blks_read
                FROM pg_stat_statements
                ORDER BY mean_exec_time DESC
                LIMIT 200
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var queryText = reader.GetString(0);
                if (!PgStatStatementSqlMatcher.IsLikelyMatch(sql, queryText))
                {
                    continue;
                }

                var calls = reader.GetInt64(1);
                var meanMs = reader.GetDouble(2);
                var totalMs = reader.GetDouble(3);
                var rows = reader.GetInt64(4);
                var hit = reader.GetInt64(5);
                var read = reader.GetInt64(6);
                var ratio = hit + read == 0 ? 1.0 : hit / (double)(hit + read);

                return new PgStatStatementInsight(calls, meanMs, totalMs, rows, ratio, queryText);
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

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

    private static async Task<List<SchemaColumnInfo>> ReadColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var columns = new List<SchemaColumnInfo>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT table_name, column_name, data_type, is_nullable, character_maximum_length, numeric_precision, numeric_scale
            FROM information_schema.columns
            WHERE table_schema = 'public'
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(new SchemaColumnInfo(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3).Equals("YES", StringComparison.OrdinalIgnoreCase),
                reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetValue(4)),
                reader.IsDBNull(5) ? null : Convert.ToInt32(reader.GetValue(5)),
                reader.IsDBNull(6) ? null : Convert.ToInt32(reader.GetValue(6))));
        }

        return columns;
    }
}

public static class PostgreSqlQueryDuckExtensions
{
    public static DatabaseAdapterRegistry AddPostgreSql(this DatabaseAdapterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.Register(new PostgreSqlDatabaseAdapter());
    }
}
