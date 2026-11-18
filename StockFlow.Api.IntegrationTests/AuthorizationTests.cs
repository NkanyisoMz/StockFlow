using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using StockFlow.Api.IntegrationTests.Infrastructure;
using Xunit;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.Api.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace StockFlow.Api.IntegrationTests;

public sealed class AuthorizationTests
    : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public AuthorizationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/customers")]
    [InlineData("/api/orders")]
    [InlineData("/api/inventory/products/1/history")]
    public async Task BusinessEndpoint_WithoutToken_ReturnsUnauthorized(
        string path)
    {
        // Deliberately use an anonymous client.
        using var client = _fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        using var response = await client.GetAsync(path);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    private async Task<HttpClient> CreateStaffClientAsync()
    {
        var email = $"staff-{Guid.NewGuid():N}@example.com";
        const string password = "Test-Only-Password123!";

        await using var scope = _fixture.Factory.Services
            .CreateAsyncScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser
        {
            UserName = email,
            Email = email
        };

        var createResult = await userManager.CreateAsync(user, password);

        Assert.True(
            createResult.Succeeded,
            string.Join(
                "; ",
                createResult.Errors.Select(error => error.Description)));

        var roleResult = await userManager.AddToRoleAsync(
            user,
            AppRoles.Staff);

        Assert.True(
            roleResult.Succeeded,
            string.Join(
                "; ",
                roleResult.Errors.Select(error => error.Description)));

        var client = _fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        try
        {
            using var loginResponse = await client.PostAsJsonAsync(
                "/api/auth/login",
                new { email, password });

            Assert.Equal(
                HttpStatusCode.OK,
                loginResponse.StatusCode);

            var result = await loginResponse.Content
                .ReadFromJsonAsync<JsonElement>();

            var token = result.GetProperty("accessToken").GetString();

            Assert.False(string.IsNullOrWhiteSpace(token));

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    [Fact]
    public async Task GetProducts_AsStaff_ReturnsOk()
    {
        using var client = await CreateStaffClientAsync();

        using var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task ProductWrite_AsStaff_ReturnsForbidden(string method)
    {
        using var adminClient = _fixture.CreateAuthenticatedClient();

        var productId = await TestData.CreateProductAsync(
            adminClient,
            price: 100.00m,
            initialStock: 0);

        using var staffClient = await CreateStaffClientAsync();

        var path = method == "POST"
            ? "/api/products"
            : $"/api/products/{productId}";

        using var request = new HttpRequestMessage(
            new HttpMethod(method),
            path);

        if (method != "DELETE")
        {
            request.Content = JsonContent.Create(new
            {
                name = "Forbidden Product Change",
                sku = $"FORBIDDEN-{Guid.NewGuid():N}",
                price = 200.00m
            });
        }

        using var response = await staffClient.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            response.StatusCode);

        // Verify that the existing product was not changed or deleted.
        using var productResponse = await adminClient.GetAsync(
            $"/api/products/{productId}");

        Assert.Equal(
            HttpStatusCode.OK,
            productResponse.StatusCode);

        var product = await productResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Order Test Product",
            product.GetProperty("name").GetString());

        Assert.Equal(
            100.00m,
            product.GetProperty("price").GetDecimal());
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("expired")]
    [InlineData("wrong-signature")]
    public async Task GetProducts_WithInvalidToken_ReturnsUnauthorized(
        string scenario)
    {
        var configuration = _fixture.Factory.Services
            .GetRequiredService<IConfiguration>();

        var token = "not-a-jwt";

        if (scenario != "malformed")
        {
            var key = scenario == "wrong-signature"
                ? "Different-test-signing-key-not-used-by-the-API-123456789"
                : configuration["Jwt:Key"]!;

            var now = DateTime.UtcNow;

            // Expire well beyond the configured 30-second clock tolerance.
            var expires = scenario == "expired"
                ? now.AddMinutes(-5)
                : now.AddMinutes(5);

            var jwt = new JwtSecurityToken(
                issuer: configuration["Jwt:Issuer"],
                audience: configuration["Jwt:Audience"],
                claims: new[]
                {
                    new Claim("sub", "token-validation-test"),
                    new Claim("role", AppRoles.Admin)
                },
                notBefore: now.AddMinutes(-10),
                expires: expires,
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    SecurityAlgorithms.HmacSha256));

            token = new JwtSecurityTokenHandler().WriteToken(jwt);
        }

        using var client = _fixture.Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/products");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateCustomer_AsStaff_PersistsCustomer()
    {
        using var client = await CreateStaffClientAsync();

        var email = $"staff-created-{Guid.NewGuid():N}@example.com";

        using var response = await client.PostAsJsonAsync(
            "/api/customers",
            new
            {
                name = "Staff Created Customer",
                email
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        var customerId = created.GetProperty("id").GetInt32();

        using var savedResponse = await client.GetAsync(
            $"/api/customers/{customerId}");

        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);

        var saved = await savedResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(email, saved.GetProperty("email").GetString());
    }
}
