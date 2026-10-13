public class InventoryMovement
{
    public int Id { get; set; }

    public int ProductId { get; set; }

    public MovementType Type { get; set; }

    public int Quantity { get; set;}

    public required string Reason { get; set;}

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Product Product { get; set; } = null!;
}
