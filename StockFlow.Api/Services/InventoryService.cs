using Microsoft.EntityFrameworkCore;

public class InventoryService
{
    private const int MaxStock = 100_000;

    private readonly StockFlowDbContext _db;

    public InventoryService(StockFlowDbContext db)
    {
        _db = db;
    }

    public async Task<InventoryMovement?> StockInAsync(
        StockMovementDto dto)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var product = await _db.Products
        .FromSqlInterpolated(
        $"SELECT * FROM \"Products\" WHERE \"Id\" = {dto.ProductId} FOR UPDATE")
        .SingleOrDefaultAsync();

        if(product == null)
        {
            return null;
        }

        if(dto.Quantity > MaxStock - product.QuantityInStock)
        {
            throw new StockLimitExceededException($"Stock limit exceeded. Maximum: {MaxStock}, " +
              $"Current: {product.QuantityInStock}, Requested: {dto.Quantity}.");
        }

        product.QuantityInStock += dto.Quantity;

        var movement = new InventoryMovement
        {
            Type = MovementType.StockIn,
            ProductId = dto.ProductId,
            Quantity = dto.Quantity,
            Reason = dto.Reason
        };

        _db.InventoryMovements.Add(movement);

        await _db.SaveChangesAsync();

        await transaction.CommitAsync();

        return movement;
    }

    public async Task<InventoryMovement?> StockOutAsync(StockMovementDto dto)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync();

        var product = await _db.Products
        .FromSqlInterpolated(
        $"SELECT * FROM \"Products\" WHERE \"Id\" = {dto.ProductId} FOR UPDATE")
        .SingleOrDefaultAsync();

        if (product == null)
        {
            return null;
        }

        if (dto.Quantity > product.QuantityInStock)
        {
            throw new InsufficientStockException(
            $"Insufficient stock. Available: {product.QuantityInStock}, requested: {dto.Quantity}.");
        }

        product.QuantityInStock -= dto.Quantity;


        var movement = new InventoryMovement
        {
            Type = MovementType.StockOut,
            ProductId = dto.ProductId,
            Quantity = dto.Quantity,
            Reason = dto.Reason
        };

        _db.InventoryMovements.Add(movement);

        await _db.SaveChangesAsync();

        await transaction.CommitAsync();

        return movement;
    }

    public async Task<List<InventoryMovementResponseDto>?> GetMovementHistoryAsync(int productId)
    {
        var productExists = await _db.Products.AnyAsync(p => p.Id == productId);
        if (!productExists)
        {
            return null;
        }

        return await _db.InventoryMovements
        // 1 & 2. Filter the table by the incoming productId parameter
        .Where(m => m.ProductId == productId)

        // 3. Sort by CreatedAt descending, followed by Id descending
        .OrderByDescending(m => m.CreatedAt)
        .ThenByDescending(m => m.Id)

        // 4. Project (Transform) each database entity into your safe response DTO
        .Select(m => new InventoryMovementResponseDto
        {
            Id = m.Id,
            ProductId = m.ProductId,
            Type = m.Type,
            Quantity = m.Quantity,
            Reason = m.Reason,
            CreatedAt = m.CreatedAt
        })

        // 5. Execute the completed SQL query asynchronously
        .ToListAsync();
    }

}
