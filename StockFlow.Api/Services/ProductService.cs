using Microsoft.EntityFrameworkCore;
using Npgsql;

public class ProductService
{
    private readonly StockFlowDbContext _db;

    public ProductService(StockFlowDbContext db)
    {
        _db = db;
    }

    public async Task<List<Product>> GetAllAsync()
    {
        return await _db.Products.ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _db.Products.FindAsync(id);
    }

    public async Task<Product> CreateAsync(CreateProductDto dto)
    {
        var skuExists = await _db.Products.AnyAsync(p => p.Sku == dto.Sku);

        if(skuExists)
        {
            throw new DuplicateSkuException(dto.Sku);
        }

        var product = new Product
        {
            Name = dto.Name,
            Sku = dto.Sku,
            Price = dto.Price,
            QuantityInStock = dto.QuantityInStock
        };

        _db.Products.Add(product);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException pgEx
            && pgEx.SqlState == PostgresErrorCodes.UniqueViolation
            && pgEx.ConstraintName == "IX_Products_Sku")
        {
            throw new DuplicateSkuException(dto.Sku);
        }

        return product;
    }

    public async Task<Product?> UpdateAsync(int id, UpdateProductDto dto)
    {
        var product = await _db.Products.FindAsync(id);

        if (product is null)
        {
            return null; // controller decide how to handle a missing product (e.g., return 404)
        }

        product.Name = dto.Name;
        product.Sku = dto.Sku;
        product.Price = dto.Price;
        product.QuantityInStock = dto.QuantityInStock;

        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<Product?> DeleteAsync(int id)
    {
        var product = await _db.Products.FindAsync(id);

        if (product is null)
        {
            return null;
        }

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        return product; // Return the deleted item in case the client wants to see what was removed
    }
}
