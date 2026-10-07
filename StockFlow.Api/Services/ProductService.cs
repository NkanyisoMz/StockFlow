public class ProductService
{
    private readonly StockFlowDbContext _db;

    public ProductService(StockFlowDbContext db)
    {
        _db = db;
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _db.Products.FindAsync(id);
    }
}
