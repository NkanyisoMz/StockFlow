using System.ComponentModel.DataAnnotations;

public class ProductQueryDto : IValidatableObject
{
    public string? Search { get; set; }

    [Range(0, 1_000_000)]
    public decimal? MinPrice { get; set; }

    [Range(0, 1_000_000)]
    public decimal? MaxPrice { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (MinPrice.HasValue &&
            MaxPrice.HasValue &&
            MinPrice.Value > MaxPrice.Value)
        {
            yield return new ValidationResult(
                "Minimum price cannot exceed maximum price.",
                new[] { nameof(MinPrice), nameof(MaxPrice) }
            );
        }
    }
}
