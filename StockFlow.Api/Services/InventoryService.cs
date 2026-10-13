public class InventoryService
{
    private readonly StockFlowDbContext _db;

    public InventoryService(StockFlowDbContext db)
    {
        _db = db;
    }

    public async Task<InventoryMovement?> StockInAsync(
        StockMovementDto dto)
    {
        var product = await _db.Products.FindAsync(dto.ProductId);

        if(product == null)
        {
            return null;
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

        return movement;
    }

    public async Task<InventoryMovement?> StockOutAsync(StockMovementDto dto)
    {
        var product = await _db.Products.FindAsync(dto.ProductId);

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

        return movement;
    }
}
