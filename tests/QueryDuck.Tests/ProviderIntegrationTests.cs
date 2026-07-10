using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Capture;
using QueryDuck.Core.Performance;
using QueryDuck.Core.Providers;
using QueryDuck.PostgreSql;
using QueryDuck.Testing.Fixtures;

namespace QueryDuck.Tests;

public sealed class ProviderIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _postgres = new();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task PostgreSqlAdapter_AuditSchema_WorksAgainstContainer()
    {
        if (!PostgreSqlContainerFixture.IsIntegrationEnabled || _postgres.ConnectionString is null)
        {
            return;
        }

        var options = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .Options;

        await using var context = new IntegrationDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var adapter = new PostgreSqlDatabaseAdapter();
        await using var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        var audit = await adapter.AuditSchemaAsync(context.Model, connection);
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task StatementCacheEndpoint_ReturnsDiagnosticsWhenConnectionTracked()
    {
        if (!PostgreSqlContainerFixture.IsIntegrationEnabled || _postgres.ConnectionString is null)
        {
            return;
        }

        QueryDuckCapture.Clear();
        await QueryDuckEventServerHost.StopAsync();

        var adapters = new DatabaseAdapterRegistry().AddPostgreSql();
        var captureOptions = new DbContextOptionsBuilder<IntegrationDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .UseQueryDuckCapture(o =>
            {
                o.StartLocalEventServer = true;
                o.ServerPrefix = "http://127.0.0.1:17656/";
            }, adapters)
            .Options;

        await using (var context = new IntegrationDbContext(captureOptions))
        {
            await context.Database.EnsureCreatedAsync();
            _ = await context.Orders.CountAsync();
        }

        using var client = new HttpClient();
        var diagnostics = await client.GetFromJsonAsync<StatementCacheDiagnosticsResponse>(
            new Uri("http://127.0.0.1:17656/queryduck/diagnostics/statement-cache"));

        Assert.NotNull(diagnostics);
        Assert.Equal("PostgreSql", diagnostics.Provider);
        Assert.True(diagnostics.ConnectionAvailable);

        await QueryDuckEventServerHost.StopAsync();
    }

    [Fact]
    public void MigrationSnippetBuilder_GeneratesDownSql()
    {
        var snippet = MigrationSnippetBuilder.FromIndexDdl(
            "CREATE INDEX ix_orders_status_created ON orders (\"Status\", \"CreatedAt\");",
            DatabaseProvider.PostgreSql);

        Assert.NotNull(snippet);
        Assert.Contains("DROP INDEX IF EXISTS", snippet, StringComparison.Ordinal);
        Assert.DoesNotContain("Add DROP INDEX statement for rollback", snippet, StringComparison.Ordinal);
    }

    private sealed class IntegrationDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<IntegrationOrder> Orders => Set<IntegrationOrder>();
    }

    private sealed class IntegrationOrder
    {
        public int Id { get; set; }

        public string Status { get; set; } = string.Empty;

        public decimal Amount { get; set; }
    }

    private sealed record StatementCacheDiagnosticsResponse(
        string Provider,
        bool ConnectionAvailable,
        IReadOnlyList<StatementCacheFindingResponse> Findings);

    private sealed record StatementCacheFindingResponse(
        string Signature,
        int VariantCount,
        string Message);
}
