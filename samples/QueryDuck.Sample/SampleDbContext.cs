using Microsoft.EntityFrameworkCore;
using QueryDuck.Sample.Entities;

namespace QueryDuck.Sample;

public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("CUSTOMERS");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Region).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Code).HasMaxLength(10).IsRequired();
            entity.Property(e => e.IsActive).IsRequired();
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("ORDERS");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasOne(e => e.Customer)
                .WithMany(c => c.Orders)
                .HasForeignKey(e => e.CustomerId);
        });
    }
}
