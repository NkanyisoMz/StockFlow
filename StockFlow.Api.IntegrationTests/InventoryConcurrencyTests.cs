using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockFlow.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace StockFlow.Api.IntegrationTests;

public sealed class InventoryConcurrencyTests
    : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public InventoryConcurrencyTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StockOut_WhenRequestsCompete_OnlyOneSucceeds()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();

        var productId = await TestData.CreateProductAsync(
            firstClient,
            price: 100.00m,
            initialStock: 5);

        var firstRequest = firstClient.PostAsJsonAsync(
            "/api/inventory/stock-out",
            new
            {
                productId,
                quantity = 4,
                reason = "First competing request"
            });

        var secondRequest = secondClient.PostAsJsonAsync(
            "/api/inventory/stock-out",
            new
            {
                productId,
                quantity = 4,
                reason = "Second competing request"
            });

        var responses = await Task.WhenAll(
            firstRequest,
            secondRequest);

        using var firstResponse = responses[0];
        using var secondResponse = responses[1];

        Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.OK);

        Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.Conflict);

        using var productResponse = await firstClient.GetAsync(
            $"/api/products/{productId}");

        Assert.Equal(
            HttpStatusCode.OK,
            productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            1,
            product.GetProperty("quantityInStock").GetInt32());

        using var historyResponse = await firstClient.GetAsync(
            $"/api/inventory/products/{productId}/history");

        Assert.Equal(
            HttpStatusCode.OK,
            historyResponse.StatusCode);

        var history = await historyResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(2, history.GetArrayLength());

        var stockOut = Assert.Single(
            history.EnumerateArray(),
            movement => movement.GetProperty("type").GetInt32() == 1);

        Assert.Equal(
            productId,
            stockOut.GetProperty("productId").GetInt32());

        Assert.Equal(
            4,
            stockOut.GetProperty("quantity").GetInt32());

        var stockIn = Assert.Single(
            history.EnumerateArray(),
            movement => movement.GetProperty("type").GetInt32() == 0);

        Assert.Equal(
            5,
            stockIn.GetProperty("quantity").GetInt32());
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
