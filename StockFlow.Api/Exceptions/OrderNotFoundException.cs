namespace StockFlow.Api.Exceptions;

public class OrderNotFoundException : Exception
{
    public OrderNotFoundException(string message) : base(message)
    {
    }
}
