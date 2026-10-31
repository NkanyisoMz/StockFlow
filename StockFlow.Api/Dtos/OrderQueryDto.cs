using System.ComponentModel.DataAnnotations;
using StockFlow.Domain.Enums;

public class OrderQueryDto
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int? CustomerId { get; set; }

    public OrderStatus? Status { get; set; }
}
