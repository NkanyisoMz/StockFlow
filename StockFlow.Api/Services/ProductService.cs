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

        int totalCount = await query.CountAsync();

        var products = await query.OrderBy(p => p.Id)
                              .Skip((filters.Page - 1) * filters.PageSize)
                              .Take(filters.PageSize)
                              .ToListAsync();

        int totalPages = (int)Math.Ceiling((double)totalCount / filters.PageSize);

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

        const decimal MaxPrice = 9999999999999999.99m;

        if (dto.Price < 0 ||
            dto.Price > MaxPrice ||
            decimal.Round(dto.Price, 2) != dto.Price)
        {
            throw new ArgumentException(
                "Price must be non-negative, have at most two decimal places, and fit the supported monetary range.");
        }

        var product = new Product
        {
            Name = dto.Name,
            Sku = dto.Sku,
            Price = dto.Price,
            QuantityInStock = 0
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

        const decimal MaxPrice = 9999999999999999.99m;

        if (dto.Price < 0 ||
            dto.Price > MaxPrice ||
            decimal.Round(dto.Price, 2) != dto.Price)
        {
            throw new ArgumentException(
                "Price must be non-negative, have at most two decimal places, and fit the supported monetary range.");
        }

        product.Name = dto.Name;
        product.Sku = dto.Sku;
        product.Price = dto.Price;

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

        return product;
    }
}
