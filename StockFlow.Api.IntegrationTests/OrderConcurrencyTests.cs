using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockFlow.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace StockFlow.Api.IntegrationTests;

public sealed class OrderConcurrencyTests
    : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public OrderConcurrencyTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ConfirmOrder_WhenRequestsCompete_DeductsStockOnce()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();

        var productId = await TestData.CreateProductAsync(
            firstClient,
            price: 100.00m,
            initialStock: 10);

        var customerId = await TestData.CreateCustomerAsync(firstClient);

        var orderId = await TestData.CreateOrderAsync(
            firstClient,
            customerId,
            (productId, 2));

        var firstRequest = firstClient.PostAsync(
            $"/api/orders/{orderId}/confirm",
            content: null);

        var secondRequest = secondClient.PostAsync(
            $"/api/orders/{orderId}/confirm",
            content: null);

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

        using var orderResponse = await firstClient.GetAsync(
            $"/api/orders/{orderId}");

        Assert.Equal(
            HttpStatusCode.OK,
            orderResponse.StatusCode);

        var order = await orderResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            1,
            order.GetProperty("status").GetInt32());

        Assert.Equal(
            200.00m,
            order.GetProperty("totalAmount").GetDecimal());

        using var productResponse = await firstClient.GetAsync(
            $"/api/products/{productId}");

        Assert.Equal(
            HttpStatusCode.OK,
            productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            8,
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
    public async Task ConfirmOrders_WhenStockCannotCoverBoth_OnlyOneConfirms()
    {
        using var firstClient = CreateClient();
        using var secondClient = CreateClient();

        var productId = await TestData.CreateProductAsync(
            firstClient,
            price: 100.00m,
            initialStock: 7);

        var customerId = await TestData.CreateCustomerAsync(firstClient);

        var firstOrderId = await TestData.CreateOrderAsync(
            firstClient,
            customerId,
            (productId, 5));

        var secondOrderId = await TestData.CreateOrderAsync(
            firstClient,
            customerId,
            (productId, 5));

        var firstRequest = firstClient.PostAsync(
            $"/api/orders/{firstOrderId}/confirm",
            content: null);

        var secondRequest = secondClient.PostAsync(
            $"/api/orders/{secondOrderId}/confirm",
            content: null);

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

        var orderIds = new[] { firstOrderId, secondOrderId };
        var confirmedOrderId = 0;

        for (var index = 0; index < orderIds.Length; index++)
        {
            using var orderResponse = await firstClient.GetAsync(
                $"/api/orders/{orderIds[index]}");

            Assert.Equal(
                HttpStatusCode.OK,
                orderResponse.StatusCode);

            var order = await orderResponse.Content
                .ReadFromJsonAsync<JsonElement>();

            var expectedStatus =
                responses[index].StatusCode == HttpStatusCode.OK ? 1 : 0;

            Assert.Equal(
                expectedStatus,
                order.GetProperty("status").GetInt32());

            if (expectedStatus == 1)
            {
                confirmedOrderId = orderIds[index];
            }
        }

        using var productResponse = await firstClient.GetAsync(
            $"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.OK, productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            2,
            product.GetProperty("quantityInStock").GetInt32());

        using var historyResponse = await firstClient.GetAsync(
            $"/api/inventory/products/{productId}/history");

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        var history = await historyResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(2, history.GetArrayLength());

        var stockOut = Assert.Single(
            history.EnumerateArray(),
            movement => movement.GetProperty("type").GetInt32() == 1);

        Assert.Equal(
            5,
            stockOut.GetProperty("quantity").GetInt32());

        Assert.Equal(
            $"Order #{confirmedOrderId} confirmed",
            stockOut.GetProperty("reason").GetString());

        var stockIn = Assert.Single(
            history.EnumerateArray(),
            movement => movement.GetProperty("type").GetInt32() == 0);

        Assert.Equal(
            7,
            stockIn.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task ConfirmAndCancel_WhenRequestsCompete_OnlyOneTransitionSucceeds()
    {
        using var confirmClient = CreateClient();
        using var cancelClient = CreateClient();

        var productId = await TestData.CreateProductAsync(
            confirmClient,
            price: 100.00m,
            initialStock: 10);

        var customerId = await TestData.CreateCustomerAsync(confirmClient);

        var orderId = await TestData.CreateOrderAsync(
            confirmClient,
            customerId,
            (productId, 2));

        var confirmRequest = confirmClient.PostAsync(
            $"/api/orders/{orderId}/confirm",
            content: null);

        var cancelRequest = cancelClient.PostAsync(
            $"/api/orders/{orderId}/cancel",
            content: null);

        var responses = await Task.WhenAll(
            confirmRequest,
            cancelRequest);

        using var confirmResponse = responses[0];
        using var cancelResponse = responses[1];

        Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.OK);

        Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.Conflict);

        var confirmationWon =
            confirmResponse.StatusCode == HttpStatusCode.OK;

        var expectedStatus = confirmationWon ? 1 : 2;
        var expectedStock = confirmationWon ? 8 : 10;
        var expectedMovementCount = confirmationWon ? 2 : 1;

        using var orderResponse = await confirmClient.GetAsync(
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

        using var productResponse = await confirmClient.GetAsync(
            $"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.OK, productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            expectedStock,
            product.GetProperty("quantityInStock").GetInt32());

        using var historyResponse = await confirmClient.GetAsync(
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

        if (confirmationWon)
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

    [Fact]
    public async Task StockOutAndConfirm_WhenRequestsCompete_PreserveInventory()
    {
        using var inventoryClient = CreateClient();
        using var orderClient = CreateClient();

        var productId = await TestData.CreateProductAsync(
            inventoryClient,
            price: 100.00m,
            initialStock: 5);

        var customerId = await TestData.CreateCustomerAsync(orderClient);

        var orderId = await TestData.CreateOrderAsync(
            orderClient,
            customerId,
            (productId, 4));

        const string manualReason = "Competing manual stock-out";

        var stockOutRequest = inventoryClient.PostAsJsonAsync(
            "/api/inventory/stock-out",
            new
            {
                productId,
                quantity = 4,
                reason = manualReason
            });

        var confirmRequest = orderClient.PostAsync(
            $"/api/orders/{orderId}/confirm",
            content: null);

        var responses = await Task.WhenAll(
            stockOutRequest,
            confirmRequest);

        using var stockOutResponse = responses[0];
        using var confirmResponse = responses[1];

        Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.OK);

        Assert.Single(
            responses,
            response => response.StatusCode == HttpStatusCode.Conflict);

        var confirmationWon =
            confirmResponse.StatusCode == HttpStatusCode.OK;

        var expectedStatus = confirmationWon ? 1 : 0;

        using var orderResponse = await orderClient.GetAsync(
            $"/api/orders/{orderId}");

        Assert.Equal(HttpStatusCode.OK, orderResponse.StatusCode);

        var order = await orderResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            expectedStatus,
            order.GetProperty("status").GetInt32());

        using var productResponse = await inventoryClient.GetAsync(
            $"/api/products/{productId}");

        Assert.Equal(HttpStatusCode.OK, productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            1,
            product.GetProperty("quantityInStock").GetInt32());

        using var historyResponse = await inventoryClient.GetAsync(
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
            4,
            stockOut.GetProperty("quantity").GetInt32());

        var expectedReason = confirmationWon
            ? $"Order #{orderId} confirmed"
            : manualReason;

        Assert.Equal(
            expectedReason,
            stockOut.GetProperty("reason").GetString());

        var stockIn = Assert.Single(
            history.EnumerateArray(),
            movement => movement.GetProperty("type").GetInt32() == 0);

        Assert.Equal(
            5,
            stockIn.GetProperty("quantity").GetInt32());
    }

    private HttpClient CreateClient()
    {
        return _fixture.CreateAuthenticatedClient();
    }
}
