using Microsoft.EntityFrameworkCore;
using QueryDuck.Sample.Entities;

namespace QueryDuck.Sample;

/// <summary>
/// Sample queries that exercise common EF Core + Oracle debugging scenarios.
/// </summary>
public static class SampleQueries
{
    /// <summary>
    /// QD001 target: Oracle treats empty string as NULL.
    /// </summary>
    public static IQueryable<Customer> FindByEmptyCode(SampleDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Customers.Where(c => c.Code == string.Empty);
    }

    /// <summary>
    /// QD003 target: Sum over non-nullable selector when no rows match returns NULL from SQL.
    /// </summary>
    public static Task<decimal> SumAmountForRegionAsync(SampleDbContext context, string region)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Orders
            .Where(o => o.Customer.Region == region)
            .SumAsync(o => o.Amount);
    }

    /// <summary>
    /// Projection that can surface unexpected NULL during materialization.
    /// </summary>
    public static IQueryable<string> CustomerRegionNames(SampleDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Customers.Select(c => c.Region);
    }

    /// <summary>
    /// QD006 target: captured ID list translated to provider-specific IN/ANY constructs.
    /// </summary>
    public static IQueryable<Customer> FindByIds(SampleDbContext context, IReadOnlyCollection<int> ids)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(ids);
        return context.Customers.Where(c => ids.Contains(c.Id));
    }

    /// <summary>
    /// QD007 target: database-side current timestamp semantics differ by provider.
    /// </summary>
    public static IQueryable<Order> RecentOrders(SampleDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Orders.Where(o => o.CreatedAt >= DateTime.UtcNow.AddDays(-7));
    }

    /// <summary>
    /// QD008 target: boolean literal comparisons map differently across providers.
    /// </summary>
    public static IQueryable<Customer> ActiveCustomers(SampleDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Customers.Where(c => c.IsActive == true);
    }

    /// <summary>
    /// QD009 target: First without OrderBy is non-deterministic.
    /// </summary>
    public static Task<Customer?> FirstCustomerAsync(SampleDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Customers.FirstOrDefaultAsync();
    }

    /// <summary>
    /// Tagged query for Rider TagWith-style SQL comment filtering.
    /// </summary>
    public static IQueryable<Customer> TaggedLookup(SampleDbContext context, string region)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Customers
            .TagWith("Sample:CustomerLookup")
            .Where(c => c.Region == region);
    }
}
