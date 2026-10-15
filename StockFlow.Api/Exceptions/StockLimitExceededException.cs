public class StockLimitExceededException : Exception
{
    public StockLimitExceededException(string message) : base(message)
    {
    }
}
