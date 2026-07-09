using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.Providers;
using QueryDuck.Sample;
using QueryDuck.Sample.Entities;

namespace QueryDuck.Tests;

public sealed class NewRuleTests
{
    [Fact]
    public void QD010_Detects_IncludeChainWithoutAsSplitQuery()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase(nameof(QD010_Detects_IncludeChainWithoutAsSplitQuery))
            .Options;

        using var context = new SampleDbContext(options);
        Expression<Func<IQueryable<Order>>> expression = () =>
            context.Orders.Include(o => o.Customer).ThenInclude(c => c.Orders);
        var diagnostics = new QueryRuleRunner().Analyze(expression.Body, DatabaseProvider.PostgreSql);
        Assert.Contains(diagnostics, d => d.RuleId == "QD010");
    }

    [Fact]
    public void QD011_Detects_TakeWithoutOrderBy()
    {
        var query = new[] { 1, 2, 3 }.AsQueryable().Take(5);
        var diagnostics = new QueryRuleRunner().Analyze(query.Expression, DatabaseProvider.SqlServer);
        Assert.Contains(diagnostics, d => d.RuleId == "QD011");
    }

    [Fact]
    public void QD012_Detects_AsEnumerable()
    {
        var source = new[] { 1, 2, 3 }.AsQueryable();
        Expression<Func<IEnumerable<int>>> expression = () => source.AsEnumerable().Where(x => x > 1);
        var diagnostics = new QueryRuleRunner().Analyze(expression.Body, DatabaseProvider.PostgreSql);
        Assert.Contains(diagnostics, d => d.RuleId == "QD012");
    }

    [Fact]
    public void QD013_Detects_StringContains()
    {
        var query = new[] { "abc" }.AsQueryable().Where(x => x.Contains('a'));
        var diagnostics = new QueryRuleRunner().Analyze(query.Expression, DatabaseProvider.MySql);
        Assert.Contains(diagnostics, d => d.RuleId == "QD013");
    }

    [Fact]
    public void QD015_Detects_ReadOnlyProjectionWithoutAsNoTracking()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase(nameof(QD015_Detects_ReadOnlyProjectionWithoutAsNoTracking))
            .Options;

        using var context = new SampleDbContext(options);
        Expression<Func<IQueryable<decimal>>> expression = () =>
            context.Orders.Select(o => o.Amount).Take(10);
        var diagnostics = new QueryRuleRunner().Analyze(expression.Body, DatabaseProvider.Sqlite);
        Assert.Contains(diagnostics, d => d.RuleId == "QD015");
    }

    [Fact]
    public void QD016_Detects_FromSqlRawLiteral()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase(nameof(QD016_Detects_FromSqlRawLiteral))
            .Options;

        using var context = new SampleDbContext(options);
        Expression<Func<IQueryable<Order>>> expression = () =>
            context.Orders.FromSqlRaw("SELECT * FROM Orders WHERE Status = 'open'");
        var diagnostics = new QueryRuleRunner().Analyze(expression.Body, DatabaseProvider.SqlServer);
        Assert.Contains(diagnostics, d => d.RuleId == "QD016");
    }
}
