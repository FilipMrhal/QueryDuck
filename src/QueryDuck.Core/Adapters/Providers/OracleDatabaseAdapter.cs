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
        return SchemaAuditHelper.Compare(model, columns, defaultSchema: connection.Database.ToUpperInvariant());
    }

    public async Task<ExecutionPlanResult> GetExecutionPlanAsync(
        DbConnection connection,
        string sql,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);

        await using var explain = connection.CreateCommand();
        explain.CommandText = $"EXPLAIN PLAN FOR {sql}";
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var findings = new List<StatementCacheFinding>();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT FORCE_MATCHING_SIGNATURE, COUNT(*) AS VARIANT_COUNT
                FROM V$SQL
                WHERE FORCE_MATCHING_SIGNATURE > 0
                GROUP BY FORCE_MATCHING_SIGNATURE
                HAVING COUNT(*) > 5
                ORDER BY COUNT(*) DESC
                FETCH FIRST 20 ROWS ONLY
                """;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var signature = reader.GetValue(0)?.ToString() ?? "unknown";
                var count = reader.GetInt32(1);
                findings.Add(new StatementCacheFinding(
                    signature,
                    count,
                    $"Oracle hard-parse risk: {count} SQL variants share signature {signature}."));
            }
        }
        catch (Exception ex)
        {
            findings.Add(new StatementCacheFinding(
                "unsupported",
                0,
                $"V$SQL diagnostics unavailable: {ex.Message}"));
        }

        return findings;
    }

    private static async Task<List<SchemaColumnInfo>> ReadColumnsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var columns = new List<SchemaColumnInfo>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, NULLABLE, DATA_LENGTH, DATA_PRECISION, DATA_SCALE
            FROM ALL_TAB_COLUMNS
            WHERE OWNER = USER
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            columns.Add(new SchemaColumnInfo(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3).Equals("Y", StringComparison.OrdinalIgnoreCase),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : Convert.ToInt32(reader.GetValue(5)),
                reader.IsDBNull(6) ? null : Convert.ToInt32(reader.GetValue(6))));
        }

        return columns;
    }
}

public static class OracleQueryDuckExtensions
{
    public static DatabaseAdapterRegistry AddOracle(this DatabaseAdapterRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return registry.Register(new OracleDatabaseAdapter());
    }
}
