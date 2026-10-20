using System.ComponentModel.DataAnnotations;

namespace StockFlow.Api.DTOs;

public class UpdateCustomerDto
{
    [Required]
    [StringLength(100)]
    public required string Name { get; set; }

    [Required]
    [StringLength(150)]
    [EmailAddress]
    public required string Email { get; set; }
}
