using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockFlow.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace StockFlow.Api.IntegrationTests;

public sealed class ProductInventoryTests
    : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public ProductInventoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateProduct_WithValidInput_PersistsZeroStock()
    {
        using var client = CreateClient();

        var sku = $"TEST-{Guid.NewGuid():N}";

        using var createResponse = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name = "Test USB-C Hub",
                sku,
                price = 450.00m
            });

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created = await createResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var productId = created.GetProperty("id").GetInt32();

        Assert.True(productId > 0);
        Assert.Equal(
            0,
            created.GetProperty("quantityInStock").GetInt32());

        // A separate request verifies the product was persisted.
        using var getResponse = await client.GetAsync(
            $"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var product = await getResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Test USB-C Hub",
            product.GetProperty("name").GetString());

        Assert.Equal(sku, product.GetProperty("sku").GetString());

        Assert.Equal(
            450.00m,
            product.GetProperty("price").GetDecimal());

        Assert.Equal(
            0,
            product.GetProperty("quantityInStock").GetInt32());
    }

    [Fact]
    public async Task StockIn_WithValidInput_PersistsBalanceAndMovement()
    {
        using var client = CreateClient();

        using var createResponse = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name = "Stock Receipt Test Product",
                sku = $"TEST-{Guid.NewGuid():N}",
                price = 125.50m
            });

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created = await createResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var productId = created.GetProperty("id").GetInt32();

        using var stockResponse = await client.PostAsJsonAsync(
            "/api/inventory/stock-in",
            new
            {
                productId,
                quantity = 10,
                reason = "Integration test receipt"
            });

        Assert.Equal(HttpStatusCode.OK, stockResponse.StatusCode);

        var movement = await stockResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var movementId = movement.GetProperty("id").GetInt32();

        Assert.True(movementId > 0);
        Assert.Equal(
            productId,
            movement.GetProperty("productId").GetInt32());

        Assert.Equal(10, movement.GetProperty("quantity").GetInt32());

        // StockIn is represented as 0 in the current HTTP contract.
        Assert.Equal(0, movement.GetProperty("type").GetInt32());

        using var productResponse = await client.GetAsync(
            $"/api/products/{productId}");

        Assert.Equal(
            HttpStatusCode.OK,
            productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            10,
            product.GetProperty("quantityInStock").GetInt32());

        using var historyResponse = await client.GetAsync(
            $"/api/inventory/products/{productId}/history");

        Assert.Equal(
            HttpStatusCode.OK,
            historyResponse.StatusCode);

        var history = await historyResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        // This newly created product should have exactly one movement.
        Assert.Equal(1, history.GetArrayLength());

        var savedMovement = history[0];

        Assert.Equal(
            movementId,
            savedMovement.GetProperty("id").GetInt32());

        Assert.Equal(
            productId,
            savedMovement.GetProperty("productId").GetInt32());

        Assert.Equal(
            10,
            savedMovement.GetProperty("quantity").GetInt32());

        Assert.Equal(
            0,
            savedMovement.GetProperty("type").GetInt32());

        Assert.Equal(
            "Integration test receipt",
            savedMovement.GetProperty("reason").GetString());
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
