using System.ComponentModel.DataAnnotations;

namespace StockFlow.Api.DTOs;

public class CreateOrderDto
{
    [Range(1, int.MaxValue, ErrorMessage = "CustomerId must be greater than zero.")]
    public int CustomerId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "An order must contain at least one item.")]
    public required List<CreateOrderItemDto> Items { get; set; }
}
