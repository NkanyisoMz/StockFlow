using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly InventoryService _inventoryService;

    public InventoryController(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    [HttpPost("stock-in")]
    public async Task<ActionResult<InventoryMovementResponseDto>> StockInAsync(StockMovementDto dto)
    {
        // 1. Call the service to process the movement
        var movement = await _inventoryService.StockInAsync(dto);

        // Scenario: Product does not exist
        if (movement is null)
        {
            return NotFound($"Product with ID {dto.ProductId} does not exist.");
        }

        var response = new InventoryMovementResponseDto
        {
            Id = movement.Id,
            ProductId = movement.ProductId,
            Type = movement.Type,
            Quantity = movement.Quantity,
            Reason = movement.Reason,
            CreatedAt = movement.CreatedAt
        };

        // Scenario: Product exists and stock is updated
        return Ok(response);
    }

    [HttpPost("stock-out")]
    public async Task<ActionResult<InventoryMovementResponseDto>> StockOutAsync(StockMovementDto dto)
    {

        try
        {
            var movement = await _inventoryService.StockOutAsync(dto);

            if(movement == null)
            {
                return NotFound($"Product with ID {dto.ProductId} does not exist.");
            }

            var response = new InventoryMovementResponseDto
            {
                Id = movement.Id,
                ProductId = movement.ProductId,
                Type = movement.Type,
                Quantity = movement.Quantity,
                Reason = movement.Reason,
                CreatedAt = movement.CreatedAt
            };

            return Ok(response);
        }
        catch (InsufficientStockException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
