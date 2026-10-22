using System.ComponentModel.DataAnnotations;

public class UpdateProductDto
{
    [Required]
    [StringLength(100)]
    public required string Name {get; set;}

    [Required]
    [StringLength(50)]
    public required string Sku {get; set;}

    public decimal Price { get; set; }

}
