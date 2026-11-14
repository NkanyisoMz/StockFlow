using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockFlow.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace StockFlow.Api.IntegrationTests;

public sealed class OrderQueryTests
    : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public OrderQueryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetOrders_WithCombinedFilters_ReturnsStablePages()
    {
        using var client = CreateClient();

        var productId = await TestData.CreateProductAsync(
            client,
            price: 100.00m,
            initialStock: 10);

        var customerId = await TestData.CreateCustomerAsync(client);
        var otherCustomerId = await TestData.CreateCustomerAsync(client);

        var pendingIds = new List<int>();

        // Create three matching orders for the target customer.
        for (var index = 0; index < 3; index++)
        {
            var orderId = await TestData.CreateOrderAsync(
                client,
                customerId,
                (productId, 1));

            pendingIds.Add(orderId);
        }

        // Same customer, different status: this order must be excluded.
        var cancelledOrderId = await TestData.CreateOrderAsync(
            client,
            customerId,
            (productId, 1));

        using var cancelResponse = await client.PostAsync(
            $"/api/orders/{cancelledOrderId}/cancel",
            content: null);

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        // Same status, different customer: this order must also be excluded.
        await TestData.CreateOrderAsync(
            client,
            otherCustomerId,
            (productId, 1));

        // Sequential creation produces newest-first order in reverse ID order.
        pendingIds.Reverse();

        var expectedPages = new[]
        {
            pendingIds.Take(2).ToArray(),
            pendingIds.Skip(2).ToArray(),
            Array.Empty<int>()
        };

        for (var index = 0; index < expectedPages.Length; index++)
        {
            var pageNumber = index + 1;

            using var response = await client.GetAsync(
                "/api/orders"
                + $"?customerId={customerId}&status=0"
                + $"&page={pageNumber}&pageSize=2");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content
                .ReadFromJsonAsync<JsonElement>();

            Assert.Equal(
                pageNumber,
                result.GetProperty("page").GetInt32());

            Assert.Equal(
                2,
                result.GetProperty("pageSize").GetInt32());

            Assert.Equal(
                3,
                result.GetProperty("totalCount").GetInt32());

            Assert.Equal(
                2,
                result.GetProperty("totalPages").GetInt32());

            var items = result.GetProperty("items");

            var actualIds = items.EnumerateArray()
                .Select(order => order.GetProperty("id").GetInt32())
                .ToArray();

            Assert.Equal(expectedPages[index], actualIds);

            foreach (var order in items.EnumerateArray())
            {
                Assert.Equal(
                    customerId,
                    order.GetProperty("customerId").GetInt32());

                Assert.Equal(
                    0,
                    order.GetProperty("status").GetInt32());

                var orderItems = order.GetProperty("items");

                Assert.Equal(1, orderItems.GetArrayLength());

                Assert.Equal(
                    productId,
                    orderItems[0].GetProperty("productId").GetInt32());
            }
        }
    }

    [Theory]
[InlineData("page=0")]
[InlineData("page=-1")]
[InlineData("pageSize=0")]
[InlineData("pageSize=101")]
[InlineData("customerId=0")]
[InlineData("customerId=-1")]
[InlineData("page=abc")]
[InlineData("customerId=abc")]
[InlineData("status=unknown")]
[InlineData("status=999")]
[InlineData("status=-1")]
public async Task GetOrders_WithInvalidQuery_ReturnsBadRequest(
    string query)
{
    using var client = CreateClient();

    using var response = await client.GetAsync(
        $"/api/orders?{query}");

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
