using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using StockFlow.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace StockFlow.Api.IntegrationTests;

public sealed class ProductQueryTests
    : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public ProductQueryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetProducts_WithCombinedFilters_ReturnsStablePages()
    {
        using var client = CreateClient();

        var searchTerm = $"Query-{Guid.NewGuid():N}";
        var matchingIds = new List<int>();

        // Only the middle three prices should satisfy the price filters.
        foreach (var price in new[] { 50m, 100m, 150m, 200m, 250m })
        {
            using var createResponse = await client.PostAsJsonAsync(
                "/api/products",
                new
                {
                    name = $"{searchTerm}-{price}",
                    sku = $"TEST-{Guid.NewGuid():N}",
                    price
                });

            Assert.Equal(
                HttpStatusCode.Created,
                createResponse.StatusCode);

            var created = await createResponse.Content
                .ReadFromJsonAsync<JsonElement>();

            if (price >= 100m && price <= 200m)
            {
                matchingIds.Add(
                    created.GetProperty("id").GetInt32());
            }
        }

        // This product matches the price range but not the search term.
        using var unrelatedResponse = await client.PostAsJsonAsync(
            "/api/products",
            new
            {
                name = $"Unrelated-{Guid.NewGuid():N}",
                sku = $"TEST-{Guid.NewGuid():N}",
                price = 150m
            });

        Assert.Equal(
            HttpStatusCode.Created,
            unrelatedResponse.StatusCode);

        // Lowercase search also verifies case-insensitive matching.
        var query = "/api/products"
            + $"?search={Uri.EscapeDataString(searchTerm.ToLowerInvariant())}"
            + "&minPrice=100&maxPrice=200&pageSize=2";

        var expectedPages = new[]
        {
            matchingIds.Take(2).ToArray(),
            matchingIds.Skip(2).ToArray(),
            Array.Empty<int>()
        };

        for (var index = 0; index < expectedPages.Length; index++)
        {
            var pageNumber = index + 1;

            using var response = await client.GetAsync(
                $"{query}&page={pageNumber}");

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

            var actualIds = result.GetProperty("items")
                .EnumerateArray()
                .Select(product => product.GetProperty("id").GetInt32())
                .ToArray();

            Assert.Equal(expectedPages[index], actualIds);
        }
    }

    [Theory]
[InlineData("page=0")]
[InlineData("page=-1")]
[InlineData("pageSize=0")]
[InlineData("pageSize=101")]
[InlineData("minPrice=-1")]
[InlineData("maxPrice=-1")]
[InlineData("minPrice=200&maxPrice=100")]
[InlineData("page=abc")]
[InlineData("minPrice=abc")]
[InlineData("minPrice=10000000000000000")]
[InlineData("maxPrice=10000000000000000")]
public async Task GetProducts_WithInvalidQuery_ReturnsBadRequest(
    string query)
{
    using var client = CreateClient();

    using var response = await client.GetAsync(
        $"/api/products?{query}");

    Assert.Equal(
        HttpStatusCode.BadRequest,
        response.StatusCode);
}

[Fact]
public async Task GetProducts_WithHighPriceFilters_ReturnsMatchingProduct()
{
    using var client = CreateClient();

    var uniqueName = $"HighPrice-{Guid.NewGuid():N}";

    using var createResponse = await client.PostAsJsonAsync(
        "/api/products",
        new
        {
            name = uniqueName,
            sku = $"TEST-{Guid.NewGuid():N}",
            price = 1_500_000.00m
        });

    Assert.Equal(
        HttpStatusCode.Created,
        createResponse.StatusCode);

    var created = await createResponse.Content
        .ReadFromJsonAsync<JsonElement>();

    var productId = created.GetProperty("id").GetInt32();

    using var queryResponse = await client.GetAsync(
        "/api/products"
        + $"?search={Uri.EscapeDataString(uniqueName)}"
        + "&minPrice=1000000.01&maxPrice=2000000");

    Assert.Equal(
        HttpStatusCode.OK,
        queryResponse.StatusCode);

    var result = await queryResponse.Content
        .ReadFromJsonAsync<JsonElement>();

    Assert.Equal(
        1,
        result.GetProperty("totalCount").GetInt32());

    var items = result.GetProperty("items");

    Assert.Equal(1, items.GetArrayLength());

    Assert.Equal(
        productId,
        items[0].GetProperty("id").GetInt32());

    Assert.Equal(
        1_500_000.00m,
        items[0].GetProperty("price").GetDecimal());
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
