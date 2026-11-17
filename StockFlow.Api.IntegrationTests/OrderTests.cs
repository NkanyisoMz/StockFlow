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

        Assert.Equal(0, order.GetProperty("status").GetInt32());

        Assert.Equal(
            251.00m,
            order.GetProperty("totalAmount").GetDecimal());

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

        using var confirmResponse = await client.PostAsync(
            $"/api/orders/{orderId}/confirm",
            content: null);

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        var confirmed = await confirmResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(orderId, confirmed.GetProperty("id").GetInt32());

        Assert.Equal(1, confirmed.GetProperty("status").GetInt32());

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

        using var confirmResponse = await client.PostAsync(
            $"/api/orders/{orderId}/confirm",
            content: null);

        Assert.Equal(
            HttpStatusCode.Conflict,
            confirmResponse.StatusCode);

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

    [Fact]
    public async Task CancelOrder_WhenPending_PreservesInventoryAndRejectsRepeat()
    {
        using var client = CreateClient();

        var productId = await TestData.CreateProductAsync(
            client,
            price: 100.00m,
            initialStock: 10);

        var customerId = await TestData.CreateCustomerAsync(client);

        var orderId = await TestData.CreateOrderAsync(
            client,
            customerId,
            (productId, 2));

        // Act: cancel the pending order.
        using var cancelResponse = await client.PostAsync(
            $"/api/orders/{orderId}/cancel",
            content: null);

        Assert.Equal(
            HttpStatusCode.OK,
            cancelResponse.StatusCode);

        var cancelled = await cancelResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            orderId,
            cancelled.GetProperty("id").GetInt32());

        Assert.Equal(
            2,
            cancelled.GetProperty("status").GetInt32());

        using var repeatResponse = await client.PostAsync(
            $"/api/orders/{orderId}/cancel",
            content: null);

        Assert.Equal(
            HttpStatusCode.Conflict,
            repeatResponse.StatusCode);

        using var orderResponse = await client.GetAsync(
            $"/api/orders/{orderId}");

        Assert.Equal(
            HttpStatusCode.OK,
            orderResponse.StatusCode);

        var order = await orderResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(2, order.GetProperty("status").GetInt32());

        Assert.Equal(
            200.00m,
            order.GetProperty("totalAmount").GetDecimal());

        var items = order.GetProperty("items");

        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(
            productId,
            items[0].GetProperty("productId").GetInt32());
        Assert.Equal(
            2,
            items[0].GetProperty("quantity").GetInt32());

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

        Assert.Equal(1, history.GetArrayLength());

        var movement = history[0];

        Assert.Equal(
            0,
            movement.GetProperty("type").GetInt32());

        Assert.Equal(
            10,
            movement.GetProperty("quantity").GetInt32());
    }

    [Theory]
    [InlineData("cancel", "confirm", 2, 10, 1)]
    [InlineData("confirm", "confirm", 1, 8, 2)]
    [InlineData("confirm", "cancel", 1, 8, 2)]
    public async Task OrderTransition_WhenNotPending_PreservesExistingState(
        string initialOperation,
        string rejectedOperation,
        int expectedStatus,
        int expectedStock,
        int expectedMovementCount)
    {
        using var client = CreateClient();

        var productId = await TestData.CreateProductAsync(
            client,
            price: 100.00m,
            initialStock: 10);

        var customerId = await TestData.CreateCustomerAsync(client);

        var orderId = await TestData.CreateOrderAsync(
            client,
            customerId,
            (productId, 2));

        using var initialResponse = await client.PostAsync(
            $"/api/orders/{orderId}/{initialOperation}",
            content: null);

        Assert.Equal(
            HttpStatusCode.OK,
            initialResponse.StatusCode);

        using var rejectedResponse = await client.PostAsync(
            $"/api/orders/{orderId}/{rejectedOperation}",
            content: null);

        Assert.Equal(
            HttpStatusCode.Conflict,
            rejectedResponse.StatusCode);

        using var orderResponse = await client.GetAsync(
            $"/api/orders/{orderId}");

        Assert.Equal(HttpStatusCode.OK, orderResponse.StatusCode);

        var order = await orderResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            expectedStatus,
            order.GetProperty("status").GetInt32());

        Assert.Equal(
            200.00m,
            order.GetProperty("totalAmount").GetDecimal());

        using var productResponse = await client.GetAsync(
            $"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.OK, productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            expectedStock,
            product.GetProperty("quantityInStock").GetInt32());

        using var historyResponse = await client.GetAsync(
            $"/api/inventory/products/{productId}/history");

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        var history = await historyResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            expectedMovementCount,
            history.GetArrayLength());

        var stockIn = Assert.Single(
            history.EnumerateArray(),
            movement => movement.GetProperty("type").GetInt32() == 0);

        Assert.Equal(
            10,
            stockIn.GetProperty("quantity").GetInt32());

        if (initialOperation == "confirm")
        {
            var stockOut = Assert.Single(
                history.EnumerateArray(),
                movement => movement.GetProperty("type").GetInt32() == 1);

            Assert.Equal(
                2,
                stockOut.GetProperty("quantity").GetInt32());

            Assert.Equal(
                $"Order #{orderId} confirmed",
                stockOut.GetProperty("reason").GetString());
        }
    }

    [Theory]
    [InlineData("empty-items")]
    [InlineData("zero-quantity")]
    [InlineData("negative-quantity")]
    [InlineData("duplicate-product")]
    public async Task CreateOrder_WithInvalidItems_DoesNotPersistOrder(
        string scenario)
    {
        using var client = CreateClient();

        var productId = await TestData.CreateProductAsync(
            client,
            price: 100.00m,
            initialStock: 10);

        var customerId = await TestData.CreateCustomerAsync(client);

        var items = scenario switch
        {
            "empty-items" => Array.Empty<object>(),

            "zero-quantity" => new object[]
            {
                new { productId, quantity = 0 }
            },

            "negative-quantity" => new object[]
            {
                new { productId, quantity = -1 }
            },

            "duplicate-product" => new object[]
            {
                new { productId, quantity = 1 },
                new { productId, quantity = 2 }
            },

            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        using var createResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new { customerId, items });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            createResponse.StatusCode);

        using var ordersResponse = await client.GetAsync(
            $"/api/orders?customerId={customerId}");

        Assert.Equal(HttpStatusCode.OK, ordersResponse.StatusCode);

        var orders = await ordersResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            0,
            orders.GetProperty("totalCount").GetInt32());

        Assert.Equal(
            0,
            orders.GetProperty("items").GetArrayLength());

        using var productResponse = await client.GetAsync(
            $"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.OK, productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            10,
            product.GetProperty("quantityInStock").GetInt32());
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("product")]
    public async Task CreateOrder_WithMissingReference_ReturnsNotFound(
        string missingReference)
    {
        using var client = CreateClient();

        const int missingId = int.MaxValue;

        var productId = await TestData.CreateProductAsync(
            client,
            price: 100.00m,
            initialStock: 10);

        var customerId = await TestData.CreateCustomerAsync(client);

        var missingResourcePath = missingReference == "customer"
            ? $"/api/customers/{missingId}"
            : $"/api/products/{missingId}";

        using var lookupResponse = await client.GetAsync(
            missingResourcePath);

        Assert.Equal(
            HttpStatusCode.NotFound,
            lookupResponse.StatusCode);

        var requestedCustomerId = missingReference == "customer"
            ? missingId
            : customerId;

        var requestedProductId = missingReference == "product"
            ? missingId
            : productId;

        using var createResponse = await client.PostAsJsonAsync(
            "/api/orders",
            new
            {
                customerId = requestedCustomerId,
                items = new[]
                {
                    new
                    {
                        productId = requestedProductId,
                        quantity = 2
                    }
                }
            });

        Assert.Equal(
            HttpStatusCode.NotFound,
            createResponse.StatusCode);

        using var ordersResponse = await client.GetAsync(
            $"/api/orders?customerId={requestedCustomerId}");

        Assert.Equal(
            HttpStatusCode.OK,
            ordersResponse.StatusCode);

        var orders = await ordersResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            0,
            orders.GetProperty("totalCount").GetInt32());

        Assert.Equal(
            0,
            orders.GetProperty("items").GetArrayLength());

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
    }

    [Theory]
    [InlineData("get")]
    [InlineData("confirm")]
    [InlineData("cancel")]
    public async Task OrderOperation_WhenOrderDoesNotExist_ReturnsNotFound(
        string operation)
    {
        using var client = CreateClient();

        const int missingOrderId = int.MaxValue;

        using var lookupResponse = await client.GetAsync(
            $"/api/orders/{missingOrderId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            lookupResponse.StatusCode);

        using var response = operation switch
        {
            "get" => await client.GetAsync(
                $"/api/orders/{missingOrderId}"),

            "confirm" => await client.PostAsync(
                $"/api/orders/{missingOrderId}/confirm",
                content: null),

            "cancel" => await client.PostAsync(
                $"/api/orders/{missingOrderId}/cancel",
                content: null),

            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        Assert.Equal(
            HttpStatusCode.NotFound,
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
