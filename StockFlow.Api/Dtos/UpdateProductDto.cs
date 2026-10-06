using System.ComponentModel.DataAnnotations;

public class UpdateProductDto
{
    [Required]
    [StringLength(100)]
    public required string Name {get; set;}

    [Required]
    [StringLength(50)]
    public required string Sku {get; set;}

    [Range(0, 1_000_000)]
    public decimal Price {get; set;}

    [Range(0, 100_000)]
    public int QuantityInStock {get; set;}
}
