using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockFlow.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace StockFlow.Api.IntegrationTests;

public sealed class InputValidationTests
    : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public InputValidationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(null, "VALID-SKU")]
    [InlineData("", "VALID-SKU")]
    [InlineData("Valid Product", null)]
    [InlineData("Valid Product", "")]
    public async Task CreateProduct_WithMissingRequiredField_ReturnsBadRequest(
        string? name,
        string? sku)
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name,
                sku,
                price = 100.00m
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Theory]
    [InlineData("stock-in", 0)]
    [InlineData("stock-in", -1)]
    [InlineData("stock-out", 0)]
    [InlineData("stock-out", -1)]
    public async Task StockMovement_WithInvalidQuantity_PreservesInventory(
        string operation,
        int quantity)
    {
        using var client = CreateClient();

        var productId = await TestData.CreateProductAsync(
            client,
            price: 100.00m,
            initialStock: 5);

        using var response = await client.PostAsJsonAsync(
            $"/api/inventory/{operation}",
            new
            {
                productId,
                quantity,
                reason = "Invalid quantity test"
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        using var productResponse = await client.GetAsync(
            $"/api/products/{productId}");

        Assert.Equal(
            HttpStatusCode.OK,
            productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            5,
            product.GetProperty("quantityInStock").GetInt32());

        using var historyResponse = await client.GetAsync(
            $"/api/inventory/products/{productId}/history");

        Assert.Equal(
            HttpStatusCode.OK,
            historyResponse.StatusCode);

        var history = await historyResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, history.GetArrayLength());

        Assert.Equal(
            0,
            history[0].GetProperty("type").GetInt32());

        Assert.Equal(
            5,
            history[0].GetProperty("quantity").GetInt32());
    }

    [Theory]
    [InlineData(null, "valid@example.com")]
    [InlineData("", "valid@example.com")]
    [InlineData("Valid Customer", null)]
    [InlineData("Valid Customer", "")]
    [InlineData("Valid Customer", "invalid-email")]
    public async Task CreateCustomer_WithInvalidInput_ReturnsBadRequest(
        string? name,
        string? email)
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/customers",
            new
            {
                name,
                email
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Theory]
    [InlineData(101, 20)]
    [InlineData(20, 151)]
    public async Task CreateCustomer_WithFieldExceedingMaximumLength_ReturnsBadRequest(
        int nameLength,
        int emailLength)
    {
        using var client = CreateClient();

        const string emailSuffix = "@example.com";

        var email = new string('a', emailLength - emailSuffix.Length)
            + emailSuffix;

        using var response = await client.PostAsJsonAsync(
            "/api/customers",
            new
            {
                name = new string('N', nameLength),
                email
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    private HttpClient CreateClient()
    {
        return _fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
    }
}
