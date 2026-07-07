using QueryDuck.Core.Adapters;
using QueryDuck.Core.Capture;
using QueryDuck.Core.Performance;

namespace QueryDuck.Tests;

public sealed class SchemaAuditSessionPresenterTests
{
    [Fact]
    public void ApplySessionFilter_ReturnsAllFindingsWhenSessionIsEmpty()
    {
        var raw = BuildSampleResult();

        var (filtered, hidden, active) = SchemaAuditSessionPresenter.ApplySessionFilter(
            raw,
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase));

        Assert.False(active);
        Assert.Equal(0, hidden);
        Assert.Equal(raw.NullabilityMismatches.Count, filtered.NullabilityMismatches.Count);
        Assert.Equal(raw.MissingIndexes.Count, filtered.MissingIndexes.Count);
    }

    [Fact]
    public void ApplySessionFilter_KeepsOnlyTablesSeenInSession()
    {
        var raw = BuildSampleResult();
        var session = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["ORDERS"] = 5.5,
        };

        var (filtered, hidden, active) = SchemaAuditSessionPresenter.ApplySessionFilter(raw, session);

        Assert.True(active);
        Assert.Equal(2, hidden);
        Assert.Single(filtered.NullabilityMismatches);
        Assert.Equal("ORDERS", filtered.NullabilityMismatches[0].TableName);
        Assert.Equal(5.5, filtered.NullabilityMismatches[0].SessionRelevanceScore);
        Assert.Empty(filtered.MissingIndexes);
    }

    [Fact]
    public void ApplySessionFilter_RanksSessionTablesByRelevance()
    {
        var raw = new SchemaAuditResult(
            [
                new NullabilityMismatch("Customer", "CUSTOMERS", "Name", "Name", false, true, "Customer mismatch"),
                new NullabilityMismatch("Order", "ORDERS", "Status", "Status", false, true, "Order mismatch"),
            ],
            []);
        var session = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["CUSTOMERS"] = 2,
            ["ORDERS"] = 8,
        };

        var (filtered, _, _) = SchemaAuditSessionPresenter.ApplySessionFilter(raw, session);

        Assert.Equal("ORDERS", filtered.NullabilityMismatches[0].TableName);
        Assert.Equal("CUSTOMERS", filtered.NullabilityMismatches[1].TableName);
    }

    [Fact]
    public void SqlPatternAnalyzer_ExtractsReferencedTablesFromWorkloadSql()
    {
        var patterns = SqlPatternAnalyzer.Analyze(
            "SELECT o.Id FROM Orders o INNER JOIN Customers c ON c.Id = o.CustomerId");

        Assert.Contains(patterns.ReferencedTables, t => t.Equals("Orders", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(patterns.ReferencedTables, t => t.Equals("Customers", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SessionTables_RecordsFromAndJoinTables()
    {
        QueryDuckSessionTables.Clear();
        QueryDuckSessionTables.Record(
            "SELECT o.Id FROM Orders o INNER JOIN Customers c ON c.Id = o.CustomerId",
            TimeSpan.FromMilliseconds(120));
        QueryDuckSessionTables.Record(
            "UPDATE Orders SET Status = @p0 WHERE Id = @p1",
            TimeSpan.FromMilliseconds(80));

        var tables = QueryDuckSessionTables.GetTables();

        Assert.Contains(tables, t => t.TableName.Equals("Orders", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(tables, t => t.TableName.Equals("Customers", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, tables.First(t => t.TableName.Equals("Orders", StringComparison.OrdinalIgnoreCase)).HitCount);
    }

    private static SchemaAuditResult BuildSampleResult() =>
        new(
            [
                new NullabilityMismatch("Order", "ORDERS", "Status", "Status", false, true, "Order mismatch"),
                new NullabilityMismatch("Customer", "CUSTOMERS", "Name", "Name", false, true, "Customer mismatch"),
            ],
            [],
            [],
            [new MissingIndexFinding("CUSTOMERS", "CustomerId", "Consider index on CUSTOMERS.CustomerId")],
            [new ForeignKeyFinding("ORDERS", "CustomerId", "CUSTOMERS", "FK index hint")]);
}
