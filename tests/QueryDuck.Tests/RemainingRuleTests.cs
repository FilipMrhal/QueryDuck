using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Capture;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.Providers;
using QueryDuck.Sample;
using QueryDuck.Sample.Entities;

namespace QueryDuck.Tests;

public sealed class RemainingRuleTests
{
    [Fact]
    public void QD014_IsRecordedOnRepeatedSaveChanges()
    {
        QueryDuckCapture.Clear();

        QueryDuckSaveChangesCapture.Record(null, 1);
        Assert.Empty(QueryDuckCapture.LastCommands);

        QueryDuckSaveChangesCapture.Record(null, 2);
        var captureEvent = Assert.Single(QueryDuckCapture.LastCommands);
        Assert.Contains(captureEvent.Diagnostics, d => d.RuleId == "QD014");
        Assert.Equal("SaveChanges", captureEvent.Caller);
    }

    [Fact]
    public void QD018_Detects_DynamicOrderByKey()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase(nameof(QD018_Detects_DynamicOrderByKey))
            .Options;

        using var context = new SampleDbContext(options);
        Expression<Func<IQueryable<Order>>> expression = () =>
            context.Orders.OrderBy(o => Math.Sign(o.Amount));
        var diagnostics = new QueryRuleRunner().Analyze(expression.Body, DatabaseProvider.SqlServer);
        Assert.Contains(diagnostics, d => d.RuleId == "QD018");
    }

    [Fact]
    public void QD020_Detects_MultipleOrderByCalls()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase(nameof(QD020_Detects_MultipleOrderByCalls))
            .Options;

        using var context = new SampleDbContext(options);
        Expression<Func<IQueryable<Order>>> expression = () =>
            context.Orders.OrderBy(o => o.Amount).OrderBy(o => o.CustomerId);
        var diagnostics = new QueryRuleRunner().Analyze(expression.Body, DatabaseProvider.PostgreSql);
        Assert.Contains(diagnostics, d => d.RuleId == "QD020");
    }

    [Fact]
    public void QD021_Detects_ToLowerInPredicate()
    {
        var query = new[] { "abc" }.AsQueryable().Where(x => x.ToUpperInvariant().StartsWith('A'));
        var diagnostics = new QueryRuleRunner().Analyze(query.Expression, DatabaseProvider.SqlServer);
        Assert.Contains(diagnostics, d => d.RuleId == "QD021");
    }

    [Fact]
    public void QD022_Detects_DistinctWithoutProjection()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase(nameof(QD022_Detects_DistinctWithoutProjection))
            .Options;

        using var context = new SampleDbContext(options);
        Expression<Func<IQueryable<Order>>> expression = () => context.Orders.Distinct();
        var diagnostics = new QueryRuleRunner().Analyze(expression.Body, DatabaseProvider.Sqlite);
        Assert.Contains(diagnostics, d => d.RuleId == "QD022");
    }

    [Fact]
    public void QD023_Detects_GroupByWithoutAggregate()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase(nameof(QD023_Detects_GroupByWithoutAggregate))
            .Options;

        using var context = new SampleDbContext(options);
        Expression<Func<IQueryable<IGrouping<int, Order>>>> expression = () =>
            context.Orders.GroupBy(o => o.CustomerId);
        var diagnostics = new QueryRuleRunner().Analyze(expression.Body, DatabaseProvider.Oracle);
        Assert.Contains(diagnostics, d => d.RuleId == "QD023");
    }
}
