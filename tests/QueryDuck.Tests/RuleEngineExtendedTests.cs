using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Adapters;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.Providers;
using QueryDuck.Sample;
using QueryDuck.Sample.Entities;

namespace QueryDuck.Tests;

public sealed class RuleEngineExtendedTests
{
    [Fact]
    public void NonNullableAggregateRule_DetectsSumWithoutNullableCast()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1")
            .Options;

        using var context = new SampleDbContext(options);
#pragma warning disable QD003
        Expression<Func<decimal>> expression = () => context.Orders.Sum(o => o.Amount);
#pragma warning restore QD003
        var diagnostics = new QueryRuleRunner().Analyze(expression.Body, DatabaseProvider.Oracle);
        Assert.Contains(diagnostics, d => d.RuleId == "QD003");
    }

    [Fact]
    public void NullableComparisonRule_DetectsCapturedNullComparison()
    {
        var holder = new RegionHolder { Region = null };
        var runner = new QueryRuleRunner();
        var query = new List<Customer> { new() { Region = "EMEA" } }.AsQueryable()
            .Where(c => c.Region == holder.Region);
        var diagnostics = runner.Analyze(query.Expression, DatabaseProvider.Oracle);
        Assert.Contains(diagnostics, d => d.RuleId == "QD004");
    }

    private sealed class RegionHolder
    {
        public string? Region { get; set; }
    }

    [Fact]
    public void CaseSensitivityRule_DetectsStringEqualityOnSqlServer()
    {
        var runner = new QueryRuleRunner();
        var query = new[] { "a" }.AsQueryable().Where(x => x == "A");
        var diagnostics = runner.Analyze(query.Expression, DatabaseProvider.SqlServer);
        Assert.Contains(diagnostics, d => d.RuleId == "QD005");
    }

    [Fact]
    public void CaseSensitivityRule_DetectsStringEqualsOnMySql()
    {
        var runner = new QueryRuleRunner();
        var query = new[] { "a" }.AsQueryable().Where(x => x.Equals("A", StringComparison.Ordinal));
        var diagnostics = runner.Analyze(query.Expression, DatabaseProvider.MySql);
        Assert.Contains(diagnostics, d => d.RuleId == "QD005");
    }

    [Fact]
    public void ProviderNames_MapKnownEfProviders()
    {
        Assert.Equal(DatabaseProvider.Oracle, DatabaseProviderNames.FromProviderName(DatabaseProviderNames.Oracle));
        Assert.Equal(DatabaseProvider.PostgreSql, DatabaseProviderNames.FromProviderName(DatabaseProviderNames.PostgreSql));
        Assert.Equal(DatabaseProvider.SqlServer, DatabaseProviderNames.FromProviderName(DatabaseProviderNames.SqlServer));
        Assert.Equal(DatabaseProvider.MySql, DatabaseProviderNames.FromProviderName(DatabaseProviderNames.MySql));
        Assert.Equal(DatabaseProvider.Sqlite, DatabaseProviderNames.FromProviderName(DatabaseProviderNames.Sqlite));
        Assert.Equal(DatabaseProvider.Unknown, DatabaseProviderNames.FromProviderName("Microsoft.EntityFrameworkCore.InMemory"));
    }
}
