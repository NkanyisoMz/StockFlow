using System.ComponentModel.DataAnnotations;

namespace StockFlow.Api.DTOs;

public class CreateOrderItemDto
{
    [Range(1, int.MaxValue, ErrorMessage = "ProductId must be greater than zero.")]
    public int ProductId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    public int Quantity { get; set; }
}
