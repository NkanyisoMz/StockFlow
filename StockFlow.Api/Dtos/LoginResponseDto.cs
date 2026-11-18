namespace StockFlow.Api.DTOs;

public class LoginResponseDto
{
    public required string AccessToken { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
}
