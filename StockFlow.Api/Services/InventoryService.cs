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
}
