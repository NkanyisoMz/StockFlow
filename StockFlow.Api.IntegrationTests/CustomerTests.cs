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

        // The lowercase equivalent must identify the same email.
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

        // Verify that the rejected request did not create another customer.
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
