using Microsoft.EntityFrameworkCore;

public class StockFlowDbContext : DbContext
{
    public StockFlowDbContext(DbContextOptions<StockFlowDbContext> options) :base(options){

    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

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
            "CK_Products_Quantity_NonNegative",
            "\"QuantityInStock\" >= 0"
            );
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

    }
}
