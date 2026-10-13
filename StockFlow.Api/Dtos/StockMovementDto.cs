using System.ComponentModel.DataAnnotations;

public class StockMovementDto
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public required string Reason { get; set; }
}
