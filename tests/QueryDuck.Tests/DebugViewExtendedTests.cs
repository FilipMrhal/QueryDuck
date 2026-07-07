using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Debugging;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Sample;

namespace QueryDuck.Tests;

public sealed class DebugViewExtendedTests
{
    [Fact]
    public void Debug_WithoutContext_UsesUnknownProvider()
    {
        var view = new[] { 1, 2, 3 }.AsQueryable().Where(x => x > 1).Debug();
        Assert.Equal("Unknown", view.Provider);
        Assert.Contains("Where", view.ExpressionTree, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DebugViewTypeProxy_ExposesDebuggerMembers()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1")
            .Options;

        using var context = new SampleDbContext(options);
        var view = SampleQueries.FindByEmptyCode(context).Debug(context);
        var proxy = new QueryDebugViewTypeProxy(view);

        Assert.Equal(view.Sql, proxy.Sql);
        Assert.Equal(view.ExpressionTree, proxy.ExpressionTree);
        Assert.Equal(view.ExpressionCSharp, proxy.ExpressionCSharp);
        Assert.Equal(view.Provider, proxy.Provider);
        Assert.Contains(proxy.Warnings, w => w.RuleId == "QD001");
    }

    [Fact]
    public void Summary_IncludesWarningCount()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1")
            .Options;

        using var context = new SampleDbContext(options);
        var view = SampleQueries.FindByEmptyCode(context).Debug(context);

        Assert.Contains("Oracle query", view.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("warning", view.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(view.WarningsExpanded);
    }
}
