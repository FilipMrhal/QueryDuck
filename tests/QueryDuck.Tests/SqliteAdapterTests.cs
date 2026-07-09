using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Adapters;
using QueryDuck.Sample;
using QueryDuck.Sqlite;

namespace QueryDuck.Tests;

public sealed class SqliteAdapterTests
{
    [Fact]
    public async Task SqliteAdapter_returns_explain_query_plan()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = "CREATE TABLE orders (id INTEGER PRIMARY KEY, status TEXT)";
            await create.ExecuteNonQueryAsync();
        }

        var adapter = new SqliteDatabaseAdapter();
        var plan = await adapter.GetExecutionPlanAsync(connection, "SELECT id FROM orders WHERE status = 'open'");

        Assert.Contains("SCAN", plan.PlanText, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(plan.PlanHash));
    }

    [Fact]
    public async Task SqliteAdapter_audits_schema_against_model()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        await using var context = new SampleDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var adapter = new SqliteDatabaseAdapter();
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        var audit = await adapter.AuditSchemaAsync(context.Model, connection);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task SqliteAdapter_historical_stats_returns_null()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var adapter = new SqliteDatabaseAdapter();
        var insight = await adapter.TryMatchHistoricalStatsAsync(connection, "SELECT 1");
        Assert.Null(insight);
    }
}
