using Microsoft.EntityFrameworkCore;
using Npgsql;

public class ProductService
{
    private readonly StockFlowDbContext _db;

    public ProductService(StockFlowDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<Product>> GetAllAsync(ProductQueryDto filters)
    {
        IQueryable<Product> query = _db.Products;

        // 1. Apply your filters first

        if(!string.IsNullOrWhiteSpace(filters.Search))
        {
            query = query.Where(p => p.Name.ToLower().Contains(filters.Search.ToLower()));
        }
        if (filters.MinPrice.HasValue)
        {
            query = query.Where(p => p.Price >= filters.MinPrice.Value);
        }

        if (filters.MaxPrice.HasValue)
        {
            query = query.Where(p => p.Price <= filters.MaxPrice.Value);
        }

        // 2. Count FIRST (This hits the database to find the total filtered count)
        int totalCount = await query.CountAsync();

        // 3. Paginate SECOND into a separate variable (so you don't ruin your base query)
        var products = await query.OrderBy(p => p.Id)
                              .Skip((filters.Page - 1) * filters.PageSize)
                              .Take(filters.PageSize)
                              .ToListAsync();

        // 4. Calculate total pages
        int totalPages = (int)Math.Ceiling((double)totalCount / filters.PageSize);

        // 5. Wrap all into your PagedResult package
        return new PagedResult<Product>
        {
            Items = products,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Page = filters.Page,
            PageSize = filters.PageSize
        };
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
            return null;
        }

        var skuExists = await _db.Products.AnyAsync(p => p.Sku == dto.Sku && p.Id != id);

        if (skuExists)
        {
            throw new DuplicateSkuException(dto.Sku);
        }

        product.Name = dto.Name;
        product.Sku = dto.Sku;
        product.Price = dto.Price;
        product.QuantityInStock = dto.QuantityInStock;

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
