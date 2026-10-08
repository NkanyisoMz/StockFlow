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
    }
}
