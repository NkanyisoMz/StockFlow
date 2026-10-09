using Microsoft.EntityFrameworkCore;

public class StockFlowDbContext : DbContext
{
    public StockFlowDbContext(DbContextOptions<StockFlowDbContext> options) :base(options){

    }

    public DbSet<Product> Products => Set<Product>();

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


    }
}
