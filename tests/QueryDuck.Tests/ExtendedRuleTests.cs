using System.Globalization;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Capture;
using QueryDuck.Core.Debugging;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.Providers;
using QueryDuck.Sample;
using QueryDuck.Sample.Entities;
using QueryDuck.Testing.Assertions;

namespace QueryDuck.Tests;

public sealed class ExtendedRuleTests
{
    [Fact]
    public void Ql006_LargeContainsRule_DetectsCapturedIdList()
    {
        int[] ids = [1, 2, 3];
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1")
            .Options;

        using var context = new SampleDbContext(options);
        SampleQueries.FindByIds(context, ids).Debug(context).ShouldContainRule("QD006");
    }

    [Fact]
    public void Ql007_DateTimeRule_DetectsUtcNowInPredicate()
    {
        Expression<Func<bool>> expression = () => DateTime.UtcNow > DateTime.MinValue;
        var diagnostics = new QueryRuleRunner().Analyze(expression.Body, DatabaseProvider.PostgreSql);
        Assert.Contains(diagnostics, d => d.RuleId == "QD007");
    }

    [Fact]
    public void Ql008_BooleanRule_DetectsBoolLiteralComparison()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1")
            .Options;

        using var context = new SampleDbContext(options);
        SampleQueries.ActiveCustomers(context).Debug(context).ShouldContainRule("QD008");
    }

    [Fact]
    public void Ql009_UnorderedFirstRule_WarnsWithoutOrderBy()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1")
            .Options;

        using var context = new SampleDbContext(options);
        Expression<Func<Customer?>> expression = () => context.Customers.FirstOrDefault();
        var diagnostics = new QueryRuleRunner().Analyze(expression.Body, DatabaseProvider.Oracle);
        Assert.Contains(diagnostics, d => d.RuleId == "QD009");
    }

    [Fact]
    public void ExtractTag_ParsesTagWithComments()
    {
        var tag = QueryCaptureEventFactory.ExtractTag("-- Sample:Lookup\nSELECT * FROM CUSTOMERS");
        Assert.Equal("Sample:Lookup", tag);
    }
}

public sealed class SessionInsightsTests
{
    [Fact]
    public void DetectSlowQueries_FlagsLongRunningCommands()
    {
        var events = new[]
        {
            new QueryCaptureEvent
            {
                EventId = "1",
                Timestamp = DateTimeOffset.UtcNow,
                Sql = "SELECT * FROM ORDERS",
                Provider = "Oracle",
                Duration = TimeSpan.FromMilliseconds(750),
            },
        };

        var warnings = QueryCaptureHeuristics.DetectSlowQueries(events, thresholdMs: 500);
        Assert.Contains(warnings, w => w.Contains("750 ms", StringComparison.Ordinal));
    }

    [Fact]
    public void QueryDuckSession_RefreshAggregatesNPlusOneAndSlowQueries()
    {
        QueryDuckCapture.Clear();
        for (var i = 0; i < 5; i++)
        {
            QueryDuckCapture.Record(new QueryCaptureEvent
            {
                EventId = i.ToString(CultureInfo.InvariantCulture),
                Timestamp = DateTimeOffset.UtcNow,
                Sql = "SELECT * FROM CUSTOMERS WHERE Id = @p0",
                Provider = "PostgreSql",
                Duration = TimeSpan.FromMilliseconds(600),
            });
        }

        QueryDuckSession.Refresh(QueryDuckCapture.LastCommands, new QueryCaptureOptions
        {
            DetectNPlusOne = true,
            NPlusOneThreshold = 5,
            SlowQueryThresholdMs = 500,
        });

        Assert.Contains(QueryDuckSession.Warnings, w => w.Contains("N+1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(QueryDuckSession.Warnings, w => w.Contains("Slow query", StringComparison.OrdinalIgnoreCase));
    }
}
