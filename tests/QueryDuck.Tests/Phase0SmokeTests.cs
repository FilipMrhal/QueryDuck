using Microsoft.EntityFrameworkCore;
using QueryDuck.Core;
using QueryDuck.Core.Capture;
using QueryDuck.Core.Debugging;
using QueryDuck.Core.Diagnostics;
using QueryDuck.Core.ExpressionTrees;
using QueryDuck.Core.Providers;
using QueryDuck.Sample;
using QueryDuck.Testing;
using QueryDuck.Testing.Assertions;
using QueryDuck.Testing.Fixtures;

namespace QueryDuck.Tests;

public sealed class Phase0SmokeTests
{
    [Fact]
    public void CoreAssembly_HasVersion()
    {
        Assert.Equal("1.4.0", QueryDuckAssembly.Version);
    }

    [Fact]
    public void OracleFixture_IntegrationDisabledByDefault()
    {
        Assert.False(OracleContainerFixture.IsIntegrationEnabled);
    }

    [Fact]
    public void Bootstrap_RegistersAllProviderAdapters()
    {
        var registry = QueryDuckBootstrap.CreateDefaultRegistry();
        Assert.NotNull(registry.Resolve(DatabaseProvider.Oracle));
        Assert.NotNull(registry.Resolve(DatabaseProvider.PostgreSql));
        Assert.NotNull(registry.Resolve(DatabaseProvider.SqlServer));
        Assert.NotNull(registry.Resolve(DatabaseProvider.MySql));
    }
}

public sealed class DebugViewTests
{
    [Fact]
    public void Debug_ReturnsSqlAndExpressionTree()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1")
            .Options;

        using var context = new SampleDbContext(options);
        var view = SampleQueries.FindByEmptyCode(context).Debug(context);

        Assert.Contains("CUSTOMERS", view.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Lambda", view.ExpressionTree, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Oracle", view.Provider);
    }

    [Fact]
    public void EmptyCodeQuery_TriggersQl001OnOracle()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1")
            .Options;

        using var context = new SampleDbContext(options);
        var view = SampleQueries.FindByEmptyCode(context).Debug(context);
        view.ShouldContainRule("QD001");
    }
}

public sealed class CaptureScopeTests
{
    [Fact]
    public void SetPendingFromExpression_StoresDiagnosticsForNextCapture()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1")
            .Options;

        using var context = new SampleDbContext(options);
        var expression = SampleQueries.FindByEmptyCode(context).Expression;

        QueryDuckCaptureScope.SetPending(expression, context);
        var pending = QueryDuckCaptureScope.TakePending();

        Assert.NotNull(pending);
        Assert.Contains(pending.Diagnostics, d => d.RuleId == "QD001");
        Assert.Equal("Lambda", pending.ExpressionTree.Kind);
    }

    [Fact]
    public void RecordFromQuery_IncludesStructuredExpressionTree()
    {
        QueryDuckCapture.Clear();
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1")
            .Options;

        using var context = new SampleDbContext(options);
        var query = SampleQueries.FindByEmptyCode(context);
        var recorded = QueryDuckCapture.RecordFromQuery(query, context);

        Assert.NotNull(recorded.ExpressionTree);
        Assert.Equal("Lambda", recorded.ExpressionTree!.Kind);
        Assert.Contains(recorded.Diagnostics, d => d.RuleId == "QD001");
        Assert.False(string.IsNullOrWhiteSpace(recorded.ExpressionCSharp));
    }

    [Fact]
    public void ExpressionTreeBuilder_ProducesNestedNodes()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1")
            .Options;

        using var context = new SampleDbContext(options);
        var tree = ExpressionTreeBuilder.Build(SampleQueries.FindByEmptyCode(context).Expression);

        Assert.Equal("Lambda", tree.Kind);
        Assert.NotNull(tree.Children);
        Assert.NotEmpty(tree.Children!);
    }
}

public sealed class RuleEngineTests
{
    [Fact]
    public void InlinedConstantRule_DetectsLiteralInPredicate()
    {
        var runner = new QueryRuleRunner();
        var query = new[] { 1, 2, 3 }.AsQueryable().Where(x => x == 42);
        var diagnostics = runner.Analyze(query.Expression, DatabaseProvider.Oracle);
        Assert.Contains(diagnostics, d => d.RuleId == "QD002");
    }

    [Fact]
    public void CaptureHeuristics_DetectsPossibleNPlusOne()
    {
        var events = Enumerable.Range(0, 6).Select(_ => new QueryCaptureEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            Timestamp = DateTimeOffset.UtcNow,
            Sql = "SELECT * FROM CUSTOMERS WHERE Id = :p0",
            Provider = "Oracle",
        });

        var warnings = QueryCaptureHeuristics.DetectNPlusOne(events, threshold: 5);
        Assert.NotEmpty(warnings);
    }
}

public sealed class CaptureBufferTests
{
    [Fact]
    public void LastCommands_KeepsRingBufferEntries()
    {
        QueryDuckCapture.Clear();
        QueryDuckCapture.Record(new QueryCaptureEvent
        {
            EventId = "1",
            Timestamp = DateTimeOffset.UtcNow,
            Sql = "SELECT 1",
            Provider = "Oracle",
        });

        Assert.Single(QueryDuckCapture.LastCommands);
    }
}
