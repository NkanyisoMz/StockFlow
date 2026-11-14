public class ProductInUseException : Exception
{
    public ProductInUseException(int productId)
        : base(
            $"Product {productId} cannot be deleted because it has inventory history or order items.")
    {
    }
}
