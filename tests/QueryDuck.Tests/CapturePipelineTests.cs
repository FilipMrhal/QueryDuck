using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core;
using QueryDuck.Core.Capture;
using QueryDuck.Sample;
using QueryDuck.Sample.Entities;

namespace QueryDuck.Tests;

[Collection("QueryDuckCapture")]
public sealed class CapturePipelineTests : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        QueryDuckCapture.Clear();
        await QueryDuckEventServerHost.StopAsync();
    }

    public Task DisposeAsync() => InitializeAsync();

    [Fact]
    public async Task UseQueryDuckDebugging_AutoCapturesExpressionTreeOnExecution()
    {
        var options = CreateSqliteOptions(o =>
        {
            o.StartLocalEventServer = false;
            o.PublishEvents = false;
        });

        await using var context = new SampleDbContext(options);
        await SeedCustomersAsync(context);
        QueryDuckCapture.Clear();

        await context.Customers.Where(c => c.Code == string.Empty).ToListAsync();

        var captured = Assert.Single(QueryDuckCapture.LastCommands);
        Assert.NotNull(captured.ExpressionTree);
        Assert.False(string.IsNullOrWhiteSpace(captured.ExpressionCSharp));
    }

    [Fact]
    public async Task WithQueryDuckScope_ManuallyAttachesDiagnostics()
    {
        var options = CreateSqliteOptions(o =>
        {
            o.AutoCaptureAllQueries = false;
            o.StartLocalEventServer = false;
            o.PublishEvents = false;
        });

        await using var context = new SampleDbContext(options);
        await SeedCustomersAsync(context);
        QueryDuckCapture.Clear();

        await context.Customers
            .Where(c => c.Code == string.Empty)
            .WithQueryDuckScope(context)
            .ToListAsync();

        var captured = Assert.Single(QueryDuckCapture.LastCommands);
        Assert.NotNull(captured.ExpressionTree);
    }

    [Fact]
    public async Task WithQueryDuckScope_NonGenericOverload_Works()
    {
        var options = CreateSqliteOptions(o =>
        {
            o.AutoCaptureAllQueries = false;
            o.StartLocalEventServer = false;
            o.PublishEvents = false;
        });

        await using var context = new SampleDbContext(options);
        await SeedCustomersAsync(context);
        QueryDuckCapture.Clear();

        IQueryable query = context.Customers.Where(c => c.Code == string.Empty);
        await query.WithQueryDuckScope(context).Cast<Customer>().ToListAsync();

        var captured = Assert.Single(QueryDuckCapture.LastCommands);
        Assert.NotNull(captured.ExpressionTree);
    }

    [Fact]
    public void SetPending_WithOracleContext_IncludesQl001Diagnostics()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1")
            .Options;

        using var context = new SampleDbContext(options);
        QueryDuckCaptureScope.SetPending(context.Customers.Where(c => c.Code == string.Empty).Expression, context);

        var pending = QueryDuckCaptureScope.TakePending();

        Assert.NotNull(pending);
        Assert.Contains(pending.Diagnostics, d => d.RuleId == "QD001");
    }

    [Fact]
    public async Task UseQueryDuckCapture_WithoutAutoCapture_RegistersCommandInterceptorOnly()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseSqlite(CreateSqliteConnection())
            .UseQueryDuckCapture(o =>
            {
                o.AutoCaptureAllQueries = false;
                o.StartLocalEventServer = false;
                o.PublishEvents = false;
            })
            .Options;

        await using var context = new SampleDbContext(options);
        await SeedCustomersAsync(context);
        QueryDuckCapture.Clear();

        await context.Customers.ToListAsync();

        var captured = Assert.Single(QueryDuckCapture.LastCommands);
        Assert.Null(captured.ExpressionTree);
    }

    [Fact]
    public async Task CaptureBuffer_RespectsConfiguredCapacity()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseSqlite(CreateSqliteConnection())
            .UseQueryDuckCapture(o =>
            {
                o.BufferCapacity = 2;
                o.StartLocalEventServer = false;
                o.PublishEvents = false;
            })
            .Options;

        await using var context = new SampleDbContext(options);
        await SeedCustomersAsync(context);
        QueryDuckCapture.Clear();

        await context.Customers.Where(c => c.Id == 1).ToListAsync();
        await context.Customers.Where(c => c.Id == 2).ToListAsync();
        await context.Customers.Where(c => c.Id == 3).ToListAsync();

        Assert.Equal(2, QueryDuckCapture.LastCommands.Count);
    }

    [Fact]
    public void NormalizeSqlShape_ReplacesParameterTokens()
    {
        var normalized = QueryCaptureHeuristics.NormalizeSqlShape("SELECT * FROM T WHERE Id = :p0 AND Code = :p1");
        Assert.Equal("SELECT * FROM T WHERE Id = ? AND Code = ?", normalized);
        Assert.Equal(string.Empty, QueryCaptureHeuristics.NormalizeSqlShape("  "));
    }

    private static DbContextOptions<SampleDbContext> CreateSqliteOptions(Action<QueryCaptureOptions> configure)
    {
        var connection = CreateSqliteConnection();
        return new DbContextOptionsBuilder<SampleDbContext>()
            .UseSqlite(connection)
            .UseQueryDuckDebugging(configure)
            .Options;
    }

    private static SqliteConnection CreateSqliteConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static async Task SeedCustomersAsync(SampleDbContext context)
    {
        await context.Database.EnsureCreatedAsync();
        context.Customers.AddRange(
            new Customer { Id = 1, Name = "Acme", Region = "EMEA", Code = string.Empty },
            new Customer { Id = 2, Name = "Globex", Region = "APAC", Code = "A1" });
        await context.SaveChangesAsync();
    }
}

[CollectionDefinition("QueryDuckCapture", DisableParallelization = true)]
public sealed class QueryDuckCaptureCollection;
