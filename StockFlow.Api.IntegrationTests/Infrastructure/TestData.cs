using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace StockFlow.Api.IntegrationTests.Infrastructure;

internal static class TestData
{
    public static async Task<int> CreateProductAsync(
        HttpClient client,
        decimal price,
        int initialStock)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name = "Order Test Product",
                sku = $"TEST-{Guid.NewGuid():N}",
                price
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var product = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        var productId = product.GetProperty("id").GetInt32();

        // Initial stock must enter through the inventory API.
        if (initialStock > 0)
        {
            using var stockResponse = await client.PostAsJsonAsync(
                "/api/inventory/stock-in",
                new
                {
                    productId,
                    quantity = initialStock,
                    reason = "Initial test stock"
                });

            Assert.Equal(HttpStatusCode.OK, stockResponse.StatusCode);
        }

        return productId;
    }

    public static async Task<int> CreateCustomerAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/customers",
            new
            {
                name = "Order Test Customer",
                email = $"test-{Guid.NewGuid():N}@example.com"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var customer = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        return customer.GetProperty("id").GetInt32();
    }

    public static async Task<int> CreateOrderAsync(
        HttpClient client,
        int customerId,
        params (int ProductId, int Quantity)[] items)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/orders",
            new
            {
                customerId,
                items = items.Select(item => new
                {
                    productId = item.ProductId,
                    quantity = item.Quantity
                }).ToArray()
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var order = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        return order.GetProperty("id").GetInt32();
    }
}
