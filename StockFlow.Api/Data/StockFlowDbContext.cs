using Microsoft.EntityFrameworkCore;

class StockFlowDbContext : DbContext
{
    public StockFlowDbContext(DbContextOptions<StockFlowDbContext> options) :base(options){

    }

    public DbSet<Product> Products => Set<Product>();
}
