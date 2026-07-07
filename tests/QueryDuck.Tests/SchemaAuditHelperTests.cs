using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Adapters;
using QueryDuck.Sample;

namespace QueryDuck.Tests;

public sealed class SchemaAuditHelperTests
{
    [Fact]
    public void Compare_DetectsNullabilityAndTypeMismatches()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1")
            .Options;

        using var context = new SampleDbContext(options);
        var columns = new List<SchemaColumnInfo>
        {
            new("CUSTOMERS", "Name", "VARCHAR2", IsNullable: false, MaxLength: 100, Precision: null, Scale: null),
            new("CUSTOMERS", "Region", "VARCHAR2", IsNullable: true, MaxLength: 50, Precision: null, Scale: null),
            new("CUSTOMERS", "Code", "NUMBER", IsNullable: false, MaxLength: null, Precision: 10, Scale: 0),
        };

        var result = SchemaAuditHelper.Compare(context.Model, columns, defaultSchema: "dbo");

        Assert.True(result.HasIssues);
        Assert.Contains(result.NullabilityMismatches, m => m.PropertyName == "Region");
        Assert.Contains(result.TypeMismatches, m => m.PropertyName == "Code");
        Assert.Equal("Customer", result.NullabilityMismatches[0].EntityType);
        Assert.Equal("Customer", result.TypeMismatches[0].EntityType);
    }

    [Fact]
    public void ComputePlanHash_ReturnsStablePrefix()
    {
        var hash = SchemaAuditHelper.ComputePlanHash("<ShowPlanXML/>");
        Assert.Equal(16, hash.Length);
        Assert.Equal(hash, SchemaAuditHelper.ComputePlanHash("<ShowPlanXML/>"));
    }

    [Fact]
    public void AdapterRecords_ExposeAuditMetadata()
    {
        var mismatch = new NullabilityMismatch("Customer", "CUSTOMERS", "Region", "Region", false, true, "Mismatch");
        var typeMismatch = new TypeMismatch("Customer", "CUSTOMERS", "Code", "Code", "string", "NUMBER", "Type mismatch");
        var audit = new SchemaAuditResult([mismatch], [typeMismatch]);
        var plan = new ExecutionPlanResult("<plan/>", SchemaAuditHelper.ComputePlanHash("<plan/>"));
        var finding = new StatementCacheFinding("hash", 3, "variants");

        Assert.True(audit.HasIssues);
        Assert.Equal("Customer", mismatch.EntityType);
        Assert.Equal("NUMBER", typeMismatch.DatabaseType);
        Assert.False(string.IsNullOrWhiteSpace(plan.PlanHash));
        Assert.Equal("hash", finding.Signature);
    }
}
