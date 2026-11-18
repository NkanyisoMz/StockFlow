using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockFlow.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace StockFlow.Api.IntegrationTests;

public sealed class CustomerTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public CustomerTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateCustomer_NormalizesEmailAndRejectsDuplicate()
    {
        using var client = CreateClient();

        var uniquePart = Guid.NewGuid().ToString("N");
        var normalizedEmail = $"customer-{uniquePart}@example.com";

        using var createResponse = await client.PostAsJsonAsync(
            "/api/customers",
            new
            {
                name = "Original Customer",
                email = normalizedEmail.ToUpperInvariant()
            });

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created = await createResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var customerId = created.GetProperty("id").GetInt32();

        Assert.True(customerId > 0);

        Assert.Equal(
            normalizedEmail,
            created.GetProperty("email").GetString());

        using var duplicateResponse = await client.PostAsJsonAsync(
            "/api/customers",
            new
            {
                name = "Duplicate Customer",
                email = normalizedEmail
            });

        Assert.Equal(
            HttpStatusCode.Conflict,
            duplicateResponse.StatusCode);

        using var savedResponse = await client.GetAsync(
            $"/api/customers/{customerId}");

        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);

        var saved = await savedResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Original Customer",
            saved.GetProperty("name").GetString());

        Assert.Equal(
            normalizedEmail,
            saved.GetProperty("email").GetString());

        using var listResponse = await client.GetAsync(
            "/api/customers");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var customers = await listResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var matchingCustomer = Assert.Single(
            customers.EnumerateArray(),
            customer => string.Equals(
                customer.GetProperty("email").GetString(),
                normalizedEmail,
                StringComparison.OrdinalIgnoreCase));

        Assert.Equal(
            customerId,
            matchingCustomer.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task UpdateCustomer_WithValidInput_PersistsChanges()
    {
        using var client = CreateClient();

        var customerId = await TestData.CreateCustomerAsync(client);
        var normalizedEmail = $"updated-{Guid.NewGuid():N}@example.com";

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/customers/{customerId}",
            new
            {
                name = "Updated Customer",
                email = normalizedEmail.ToUpperInvariant()
            });

        Assert.Equal(
            HttpStatusCode.NoContent,
            updateResponse.StatusCode);

        // A separate request verifies the saved values.
        using var savedResponse = await client.GetAsync(
            $"/api/customers/{customerId}");

        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);

        var saved = await savedResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            customerId,
            saved.GetProperty("id").GetInt32());

        Assert.Equal(
            "Updated Customer",
            saved.GetProperty("name").GetString());

        Assert.Equal(
            normalizedEmail,
            saved.GetProperty("email").GetString());
    }

    [Fact]
    public async Task UpdateCustomer_WithAnotherCustomersEmail_PreservesOriginal()
    {
        using var client = CreateClient();

        var ownerId = await TestData.CreateCustomerAsync(client);
        var targetId = await TestData.CreateCustomerAsync(client);

        using var ownerResponse = await client.GetAsync(
            $"/api/customers/{ownerId}");

        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);

        var owner = await ownerResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var ownerEmail = owner.GetProperty("email").GetString()!;

        using var originalResponse = await client.GetAsync(
            $"/api/customers/{targetId}");

        Assert.Equal(HttpStatusCode.OK, originalResponse.StatusCode);

        var original = await originalResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        var originalName = original.GetProperty("name").GetString();
        var originalEmail = original.GetProperty("email").GetString();

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/customers/{targetId}",
            new
            {
                name = "Rejected Name Change",
                email = ownerEmail.ToUpperInvariant()
            });

        Assert.Equal(
            HttpStatusCode.Conflict,
            updateResponse.StatusCode);

        using var savedResponse = await client.GetAsync(
            $"/api/customers/{targetId}");

        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);

        var saved = await savedResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            originalName,
            saved.GetProperty("name").GetString());

        Assert.Equal(
            originalEmail,
            saved.GetProperty("email").GetString());

        using var savedOwnerResponse = await client.GetAsync(
            $"/api/customers/{ownerId}");

        Assert.Equal(
            HttpStatusCode.OK,
            savedOwnerResponse.StatusCode);

        var savedOwner = await savedOwnerResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            ownerEmail,
            savedOwner.GetProperty("email").GetString());
    }

    private HttpClient CreateClient()
    {
        return _fixture.CreateAuthenticatedClient();
    }
}
