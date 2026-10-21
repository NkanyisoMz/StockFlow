using Microsoft.EntityFrameworkCore;
using StockFlow.Domain.Entities;

public class StockFlowDbContext : DbContext
{
    public StockFlowDbContext(DbContextOptions<StockFlowDbContext> options) :base(options){

    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>()
            .HasIndex(p => p.Sku)
            .IsUnique();

        modelBuilder.Entity<Product>()
        .Property(p => p.Name)
        .HasMaxLength(100);

        modelBuilder.Entity<Product>()
        .Property(p => p.Sku)
        .HasMaxLength(50);

        modelBuilder.Entity<Product>()
        .ToTable(t =>
        {
            t.HasCheckConstraint(
            "CK_Products_Price_NonNegative",
            "\"Price\" >= 0"
            );

            t.HasCheckConstraint(
            "CK_Products_QuantityInStock_Range",
            "\"QuantityInStock\" >= 0 AND \"QuantityInStock\" <= 100000");
        });

        modelBuilder.Entity<InventoryMovement>()
            .HasOne(m => m.Product)
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InventoryMovement>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                "CK_InventoryMovements_Quantity_Positive",
                "\"Quantity\" > 0"
                );
            });

        modelBuilder.Entity<Customer>(entity =>
       {
        entity.Property(c => c.Name)
        .IsRequired()
        .HasMaxLength(100);

        entity.Property(c => c.Email)
        .IsRequired()
        .HasMaxLength(150);

        entity.HasIndex(c => c.Email)
        .IsUnique();
        });

    modelBuilder.Entity<Order>(entity =>
    {
        entity.HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.Property(o => o.TotalAmount)
            .HasPrecision(18, 2);

        entity.ToTable(t => t.HasCheckConstraint(
            "CK_Orders_TotalAmount_NonNegative",
            "\"TotalAmount\" >= 0"
        ));
    });

    modelBuilder.Entity<OrderItem>(entity =>
    {
        entity.HasOne(oi => oi.Order)
            .WithMany(o => o.OrderItems)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(oi => oi.Product)
            .WithMany()
            .HasForeignKey(oi => oi.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        entity.Property(oi => oi.UnitPrice)
            .HasPrecision(18, 2);

        entity.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_OrderItems_Quantity_GreaterThanZero",
                "\"Quantity\" > 0"
            );

            t.HasCheckConstraint(
                "CK_OrderItems_UnitPrice_NonNegative",
                "\"UnitPrice\" >= 0"
            );
        });
    });

    }

}
