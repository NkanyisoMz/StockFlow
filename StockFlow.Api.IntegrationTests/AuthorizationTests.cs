using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using StockFlow.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace StockFlow.Api.IntegrationTests;

public sealed class AuthorizationTests
    : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public AuthorizationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/customers")]
    [InlineData("/api/orders")]
    [InlineData("/api/inventory/products/1/history")]
    public async Task BusinessEndpoint_WithoutToken_ReturnsUnauthorized(
        string path)
    {
        // Deliberately use an anonymous client.
        using var client = _fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        using var response = await client.GetAsync(path);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }
}
