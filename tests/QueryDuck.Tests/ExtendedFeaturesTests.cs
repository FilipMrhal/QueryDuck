using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Capture;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.Performance;
using QueryDuck.Core.Providers;
using QueryDuck.Sample;
using QueryDuck.Sample.Entities;

namespace QueryDuck.Tests;

public sealed class ExtendedFeaturesTests
{
    [Fact]
    public void QD017_Detects_ExecuteDeleteWithoutFilter()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase(nameof(QD017_Detects_ExecuteDeleteWithoutFilter))
            .Options;

        using var context = new SampleDbContext(options);
        Expression<Func<Task<int>>> expression = () => context.Orders.ExecuteDeleteAsync();
        var diagnostics = new QueryRuleRunner().Analyze(expression.Body, DatabaseProvider.PostgreSql);
        Assert.Contains(diagnostics, d => d.RuleId == "QD017");
    }

    [Fact]
    public void QD019_Detects_UnfilteredCount()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase(nameof(QD019_Detects_UnfilteredCount))
            .Options;

        using var context = new SampleDbContext(options);
        Expression<Func<int>> expression = () => context.Orders.Count();
        var diagnostics = new QueryRuleRunner().Analyze(expression.Body, DatabaseProvider.SqlServer);
        Assert.Contains(diagnostics, d => d.RuleId == "QD019");
    }

    [Fact]
    public void QD024_Detects_IgnoreQueryFilters()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase(nameof(QD024_Detects_IgnoreQueryFilters))
            .Options;

        using var context = new SampleDbContext(options);
        Expression<Func<IQueryable<Order>>> expression = () => context.Orders.IgnoreQueryFilters();
        var diagnostics = new QueryRuleRunner().Analyze(expression.Body, DatabaseProvider.Sqlite);
        Assert.Contains(diagnostics, d => d.RuleId == "QD024");
    }

    [Fact]
    public void SessionHotspots_GroupsRepeatedShapes()
    {
        QueryDuckCapture.Clear();
        QueryDuckCapture.Record(CreateEvent("SELECT 1", 10));
        QueryDuckCapture.Record(CreateEvent("SELECT 1", 20));
        QueryDuckCapture.Record(CreateEvent("SELECT 2", 5));

        var hotspots = QueryDuckSessionHotspotsBuilder.Build(QueryDuckCapture.LastCommands);

        Assert.Equal(3, hotspots.TotalEvents);
        Assert.Equal(2, hotspots.DistinctShapes);
        Assert.True(hotspots.Hotspots[0].ExecutionCount >= 2);
    }

    [Fact]
    public void EventDiff_DetectsSqlChanges()
    {
        var left = CreateEvent("SELECT 1", 10);
        var right = CreateEvent("SELECT 2", 10);
        var diff = QueryDuckEventDiffBuilder.Build(left, right);
        Assert.True(diff.SqlChanged);
        Assert.False(diff.DurationChanged);
    }

    [Fact]
    public void MigrationSnippet_BuildsFromIndexDdl()
    {
        var snippet = MigrationSnippetBuilder.FromIndexDdl(
            "CREATE INDEX ix_orders_status ON orders(status);",
            DatabaseProvider.PostgreSql);
        Assert.NotNull(snippet);
        Assert.Contains("migrationBuilder.Sql", snippet, StringComparison.Ordinal);
    }

    [Fact]
    public void Sampling_SkipsMostFastQueriesWhenEnabled()
    {
        var options = new QueryCaptureOptions { EnableSampling = true, SamplingRate = 0.0 };
        Assert.False(QueryCaptureSampling.ShouldCapture(options));
        options.SamplingRate = 1.0;
        Assert.True(QueryCaptureSampling.ShouldCapture(options));
    }

    private static QueryCaptureEvent CreateEvent(string sql, double durationMs) => new()
    {
        EventId = Guid.NewGuid().ToString("N"),
        Timestamp = DateTimeOffset.UtcNow,
        Sql = sql,
        Provider = "PostgreSql",
        Duration = TimeSpan.FromMilliseconds(durationMs),
    };
}
