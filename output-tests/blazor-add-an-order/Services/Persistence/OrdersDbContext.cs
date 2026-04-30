using BlazorAddAnOrder.Services.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorAddAnOrder.Services.Persistence;

public sealed class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options)
        : base(options)
    {
    }

    public DbSet<CustomerEntity> Customers => Set<CustomerEntity>();

    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    public DbSet<OrderEntity> Orders => Set<OrderEntity>();

    public DbSet<OrderLineItemEntity> OrderLineItems => Set<OrderLineItemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerEntity>(b =>
        {
            b.ToTable("Customers");
            b.HasKey(c => c.Id);
            b.Property(c => c.Id).ValueGeneratedNever();
            b.Property(c => c.Name).IsRequired();
        });

        modelBuilder.Entity<ProductEntity>(b =>
        {
            b.ToTable("Products");
            b.HasKey(p => p.Id);
            b.Property(p => p.Id).ValueGeneratedNever();
            b.Property(p => p.Name).IsRequired();
            b.Property(p => p.UnitPrice).HasColumnType("TEXT");
        });

        modelBuilder.Entity<OrderEntity>(b =>
        {
            b.ToTable("Orders");
            b.HasKey(o => o.Id);
            b.HasMany(o => o.LineItems)
                .WithOne()
                .HasForeignKey(li => li.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderLineItemEntity>(b =>
        {
            b.ToTable("OrderLineItems");
            b.HasKey(li => li.Id);
            b.Property(li => li.UnitPrice).HasColumnType("TEXT");
        });
    }
}
