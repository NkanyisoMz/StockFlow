using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockFlow.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace StockFlow.Api.IntegrationTests;

public sealed class OrderTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public OrderTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateOrder_CapturesPriceAndLeavesStockUnchanged()
    {
        using var client = CreateClient();

        // Arrange: create a product, receive stock, and create a customer.
        var sku = $"TEST-{Guid.NewGuid():N}";

        using var productResponse = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name = "Order Price Test Product",
                sku,
                price = 125.50m
            });

        Assert.Equal(
            HttpStatusCode.Created,
            productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var productId = product.GetProperty("id").GetInt32();

        using var stockResponse = await client.PostAsJsonAsync(
            "/api/inventory/stock-in",
            new
            {
                productId,
                quantity = 10,
                reason = "Initial order test stock"
            });

        Assert.Equal(HttpStatusCode.OK, stockResponse.StatusCode);

        using var customerResponse = await client.PostAsJsonAsync(
            "/api/customers",
            new
            {
                name = "Order Test Customer",
                email = $"test-{Guid.NewGuid():N}@example.com"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            customerResponse.StatusCode);

        var customer = await customerResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var customerId = customer.GetProperty("id").GetInt32();

        // Act: create an order for two units at the current price.
        using var orderResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new
            {
                customerId,
                items = new[]
                {
                    new { productId, quantity = 2 }
                }
            });

        Assert.Equal(
            HttpStatusCode.Created,
            orderResponse.StatusCode);

        var order = await orderResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var orderId = order.GetProperty("id").GetInt32();

        Assert.True(orderId > 0);
        Assert.Equal(
            customerId,
            order.GetProperty("customerId").GetInt32());

        // Pending is represented as 0 in the current HTTP contract.
        Assert.Equal(0, order.GetProperty("status").GetInt32());

        Assert.Equal(
            251.00m,
            order.GetProperty("totalAmount").GetDecimal());

        // Change the current product price after creating the order.
        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/products/{productId}",
            new
            {
                name = "Order Price Test Product",
                sku,
                price = 200.00m
            });

        Assert.Equal(
            HttpStatusCode.NoContent,
            updateResponse.StatusCode);

        // Assert: the saved order retains its original price and total.
        using var savedOrderResponse = await client.GetAsync(
            $"/api/orders/{orderId}");

        Assert.Equal(
            HttpStatusCode.OK,
            savedOrderResponse.StatusCode);

        var savedOrder = await savedOrderResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            0,
            savedOrder.GetProperty("status").GetInt32());

        Assert.Equal(
            251.00m,
            savedOrder.GetProperty("totalAmount").GetDecimal());

        var items = savedOrder.GetProperty("items");

        Assert.Equal(1, items.GetArrayLength());

        var item = items[0];

        Assert.Equal(
            productId,
            item.GetProperty("productId").GetInt32());

        Assert.Equal(2, item.GetProperty("quantity").GetInt32());

        Assert.Equal(
            125.50m,
            item.GetProperty("unitPrice").GetDecimal());

        Assert.Equal(
            251.00m,
            item.GetProperty("lineTotal").GetDecimal());

        using var savedProductResponse = await client.GetAsync(
            $"/api/products/{productId}");

        Assert.Equal(
            HttpStatusCode.OK,
            savedProductResponse.StatusCode);

        var savedProduct = await savedProductResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            200.00m,
            savedProduct.GetProperty("price").GetDecimal());

        Assert.Equal(
            10,
            savedProduct.GetProperty("quantityInStock").GetInt32());

        using var historyResponse = await client.GetAsync(
            $"/api/inventory/products/{productId}/history");

        Assert.Equal(
            HttpStatusCode.OK,
            historyResponse.StatusCode);

        var history = await historyResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        // Creating a pending order must not create a stock movement.
        Assert.Equal(1, history.GetArrayLength());
        Assert.Equal(0, history[0].GetProperty("type").GetInt32());
        Assert.Equal(10, history[0].GetProperty("quantity").GetInt32());
    }

    [Fact]
public async Task ConfirmOrder_WhenStockIsAvailable_PersistsStockAndStatus()
{
    using var client = CreateClient();

    var productId = await TestData.CreateProductAsync(
        client,
        price: 125.50m,
        initialStock: 10);

    var customerId = await TestData.CreateCustomerAsync(client);

    var orderId = await TestData.CreateOrderAsync(
        client,
        customerId,
        (productId, 2));

    // Act: confirm the pending order.
    using var confirmResponse = await client.PostAsync(
        $"/api/orders/{orderId}/confirm",
        content: null);

    Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

    var confirmed = await confirmResponse.Content
        .ReadFromJsonAsync<JsonElement>();

    Assert.Equal(orderId, confirmed.GetProperty("id").GetInt32());

    // Confirmed is represented as 1 in the current HTTP contract.
    Assert.Equal(1, confirmed.GetProperty("status").GetInt32());

    // Verify the transition was persisted.
    using var orderResponse = await client.GetAsync(
        $"/api/orders/{orderId}");

    Assert.Equal(HttpStatusCode.OK, orderResponse.StatusCode);

    var savedOrder = await orderResponse.Content
        .ReadFromJsonAsync<JsonElement>();

    Assert.Equal(1, savedOrder.GetProperty("status").GetInt32());

    Assert.Equal(
        251.00m,
        savedOrder.GetProperty("totalAmount").GetDecimal());

    using var productResponse = await client.GetAsync(
        $"/api/products/{productId}");

    Assert.Equal(HttpStatusCode.OK, productResponse.StatusCode);

    var product = await productResponse.Content
        .ReadFromJsonAsync<JsonElement>();

    Assert.Equal(
        8,
        product.GetProperty("quantityInStock").GetInt32());

    using var historyResponse = await client.GetAsync(
        $"/api/inventory/products/{productId}/history");

    Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

    var history = await historyResponse.Content
        .ReadFromJsonAsync<JsonElement>();

    Assert.Equal(2, history.GetArrayLength());

    // Select by type rather than depending on history ordering.

    var stockOut = Assert.Single(
        history.EnumerateArray(),
            movement => movement.GetProperty("type").GetInt32() == 1);

    Assert.Equal(
        productId,
        stockOut.GetProperty("productId").GetInt32());

    Assert.Equal(
        2,
        stockOut.GetProperty("quantity").GetInt32());

    Assert.Equal(
        $"Order #{orderId} confirmed",
        stockOut.GetProperty("reason").GetString());

    var stockIn = Assert.Single(
        history.EnumerateArray(),
            movement => movement.GetProperty("type").GetInt32() == 0);

    Assert.Equal(
        10,
        stockIn.GetProperty("quantity").GetInt32());
}

[Fact]
public async Task ConfirmOrder_WhenOneProductHasInsufficientStock_PreservesAllState()
{
    using var client = CreateClient();

    // Arrange: the first product can fulfill the order; the second cannot.
    var firstProductId = await TestData.CreateProductAsync(
        client,
        price: 100.00m,
        initialStock: 10);

    var secondProductId = await TestData.CreateProductAsync(
        client,
        price: 50.00m,
        initialStock: 1);

    var customerId = await TestData.CreateCustomerAsync(client);

    var orderId = await TestData.CreateOrderAsync(
        client,
        customerId,
        (firstProductId, 2),
        (secondProductId, 2));

    // Act: confirmation must reject the entire order.
    using var confirmResponse = await client.PostAsync(
        $"/api/orders/{orderId}/confirm",
        content: null);

    Assert.Equal(
        HttpStatusCode.Conflict,
        confirmResponse.StatusCode);

    // Verify the order remains pending with its original items and total.
    using var orderResponse = await client.GetAsync(
        $"/api/orders/{orderId}");

    Assert.Equal(HttpStatusCode.OK, orderResponse.StatusCode);

    var order = await orderResponse.Content
        .ReadFromJsonAsync<JsonElement>();

    Assert.Equal(0, order.GetProperty("status").GetInt32());

    Assert.Equal(
        300.00m,
        order.GetProperty("totalAmount").GetDecimal());

    Assert.Equal(
        2,
        order.GetProperty("items").GetArrayLength());

    // Check both products, including the one that had sufficient stock.
    var expectedBalances = new[]
    {
        (ProductId: firstProductId, Quantity: 10),
        (ProductId: secondProductId, Quantity: 1)
    };

    foreach (var expected in expectedBalances)
    {
        using var productResponse = await client.GetAsync(
            $"/api/products/{expected.ProductId}");

        Assert.Equal(
            HttpStatusCode.OK,
            productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            expected.Quantity,
            product.GetProperty("quantityInStock").GetInt32());

        using var historyResponse = await client.GetAsync(
            $"/api/inventory/products/{expected.ProductId}/history");

        Assert.Equal(
            HttpStatusCode.OK,
            historyResponse.StatusCode);

        var history = await historyResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        // Only the original receipt should exist for each product.
        Assert.Equal(1, history.GetArrayLength());

        var movement = history[0];

        Assert.Equal(
            expected.ProductId,
            movement.GetProperty("productId").GetInt32());

        Assert.Equal(
            0,
            movement.GetProperty("type").GetInt32());

        Assert.Equal(
            expected.Quantity,
            movement.GetProperty("quantity").GetInt32());
    }
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
