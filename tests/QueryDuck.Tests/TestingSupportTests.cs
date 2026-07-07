using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using QueryDuck.Core.Debugging;
using QueryDuck.Sample;
using QueryDuck.Sample.Entities;
using QueryDuck.Testing.Assertions;
using QueryDuck.Testing.Fixtures;
using QueryDuck.Testing.Snapshots;

namespace QueryDuck.Tests;

public sealed class TestingSupportTests
{
    [Fact]
    public async Task SampleQueries_SumAmountForRegion_ReturnsMatchingTotal()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new SampleDbContext(options);
        var customer = new Customer { Id = 1, Name = "Acme", Region = "EMEA", Code = "A1" };
        context.Customers.Add(customer);
        context.Orders.Add(new Order { Id = 1, CustomerId = 1, Customer = customer, Amount = 12.5m });
        context.Orders.Add(new Order { Id = 2, CustomerId = 1, Customer = customer, Amount = 7.5m });
        await context.SaveChangesAsync();

        var total = await SampleQueries.SumAmountForRegionAsync(context, "EMEA");

        Assert.Equal(20m, total);
    }

    [Fact]
    public void QueryDuckAssert_ShouldContainRule_PassesWhenPresent()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1")
            .Options;

        using var context = new SampleDbContext(options);
        SampleQueries.FindByEmptyCode(context).Debug(context).ShouldContainRule("QD001");
    }

    [Fact]
    public void QueryDuckAssert_ShouldContainRule_ThrowsWhenMissing()
    {
        var view = new[] { 1, 2, 3 }.AsQueryable().Where(x => x > 0).Debug();
        var ex = Assert.Throws<InvalidOperationException>(() => view.ShouldContainRule("QD001"));
        Assert.Contains("QD001", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryDuckAssert_ShouldHaveNoWarnings_ThrowsWhenWarningsExist()
    {
        var options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/FREEPDB1")
            .Options;

        using var context = new SampleDbContext(options);
        var view = SampleQueries.FindByEmptyCode(context).Debug(context);
        var ex = Assert.Throws<InvalidOperationException>(() => view.ShouldHaveNoWarnings());
        Assert.Contains("QD001", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void QueryDuckQueryableAssert_ReturnsDebugView()
    {
        var view = new[] { 1, 2, 3 }.AsQueryable().Where(x => x > 0).Should();
        Assert.Equal("Unknown", view.Provider);
    }

    [Fact]
    public async Task SqlSnapshot_ThrowsWhenQueryIsNull()
    {
        IQueryable? query = null;
        await Assert.ThrowsAsync<ArgumentNullException>(() => query!.VerifyQueryString());
    }

    [Fact]
    public void ContainerFixtures_ReportIntegrationDisabled()
    {
        Assert.False(MariaDbContainerFixture.IsIntegrationEnabled);
        Assert.False(PostgreSqlContainerFixture.IsIntegrationEnabled);
        Assert.False(SqlServerContainerFixture.IsIntegrationEnabled);
    }
}
