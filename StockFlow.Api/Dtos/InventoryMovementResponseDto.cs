public class InventoryMovementResponseDto
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public MovementType Type { get; set; }

    public int Quantity { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
