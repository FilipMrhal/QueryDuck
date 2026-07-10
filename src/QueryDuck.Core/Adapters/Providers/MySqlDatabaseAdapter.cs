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
        return SchemaAuditHelper.Compare(model, columns, defaultSchema: connection.Database);
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var findings = new List<StatementCacheFinding>();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT DIGEST_TEXT, COUNT_STAR
                FROM performance_schema.events_statements_summary_by_digest
                WHERE COUNT_STAR > 5
                ORDER BY COUNT_STAR DESC
                LIMIT 20
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                findings.Add(new StatementCacheFinding(
                    reader.GetString(0),
                    Convert.ToInt32(reader.GetValue(1)),
                    $"MySQL digest executed {reader.GetValue(1)} times: {reader.GetString(0)}"));
            }
        }
        catch (Exception ex)
        {
            findings.Add(new StatementCacheFinding("unsupported", 0, $"performance_schema unavailable: {ex.Message}"));
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
                SELECT DIGEST_TEXT,
                       COUNT_STAR,
                       SUM_TIMER_WAIT / NULLIF(COUNT_STAR, 0) / 1000000000 AS mean_ms,
                       SUM_TIMER_WAIT / 1000000000 AS total_ms,
                       SUM_ROWS_SENT,
                       SUM_NO_INDEX_USED
                FROM performance_schema.events_statements_summary_by_digest
                ORDER BY mean_ms DESC
                LIMIT 200
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var queryText = reader.GetString(0);
                if (!QueryHistoricalStatsSqlMatcher.IsLikelyMatch(sql, queryText))
                {
                    continue;
                }

                var calls = Convert.ToInt64(reader.GetValue(1));
                var meanMs = reader.IsDBNull(2) ? 0 : Convert.ToDouble(reader.GetValue(2));
                var totalMs = reader.IsDBNull(3) ? 0 : Convert.ToDouble(reader.GetValue(3));
                var rows = reader.IsDBNull(4) ? 0 : Convert.ToInt64(reader.GetValue(4));

                return new QueryHistoricalStatsInsight(
                    calls,
                    meanMs,
                    totalMs,
                    rows,
                    null,
                    queryText,
                    "events_statements_summary_by_digest");
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
            SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
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

public static class MySqlQueryDuckExtensions
{
    public static DatabaseAdapterRegistry AddMySql(this DatabaseAdapterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.Register(new MySqlDatabaseAdapter());
    }
}
