using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StockFlow.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace StockFlow.Api.IntegrationTests;

public sealed class AuthTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public AuthTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenAndExpiry()
    {
        using var client = CreateClient();

        var email = $"login-{Guid.NewGuid():N}@example.com";
        const string password = "Test-Only-Password123!";

        await CreateUserAsync(email, password);

        var beforeLogin = DateTime.UtcNow;

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.False(string.IsNullOrWhiteSpace(
            result.GetProperty("accessToken").GetString()));

        var expiresAt = result.GetProperty("expiresAtUtc").GetDateTime();

        Assert.InRange(
            expiresAt,
            beforeLogin.AddMinutes(15),
            DateTime.UtcNow.AddMinutes(15));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        var email = $"login-{Guid.NewGuid():N}@example.com";

        await CreateUserAsync(email, "Test-Only-Password123!");

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email,
                password = "Incorrect-Password123!"
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Invalid email or password.",
            result.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = $"missing-{Guid.NewGuid():N}@example.com",
                password = "Test-Only-Password123!"
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Invalid email or password.",
            result.GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("invalid-email", "Test-Only-Password123!")]
    [InlineData("valid@example.com", null)]
    [InlineData("valid@example.com", "")]
    [InlineData(null, "Test-Only-Password123!")]
    public async Task Login_WithInvalidInput_ReturnsBadRequest(
        string? email,
        string? password)
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    private async Task CreateUserAsync(string email, string password)
    {
        await using var scope = _fixture.Factory.Services
            .CreateAsyncScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        var result = await userManager.CreateAsync(
            new IdentityUser
            {
                UserName = email,
                Email = email
            },
            password);

        Assert.True(
            result.Succeeded,
            string.Join(
                "; ",
                result.Errors.Select(error => error.Description)));
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_RejectsCorrectPassword()
    {
        using var client = CreateClient();

        var email = $"lockout-{Guid.NewGuid():N}@example.com";
        const string password = "Test-Only-Password123!";

        await CreateUserAsync(email, password);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failedResponse = await client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    email,
                    password = "Incorrect-Password123!"
                });

            Assert.Equal(
                HttpStatusCode.Unauthorized,
                failedResponse.StatusCode);
        }

        using var lockedResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            lockedResponse.StatusCode);

        var result = await lockedResponse.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            "Invalid email or password.",
            result.GetProperty("message").GetString());

        await using var scope = _fixture.Factory.Services
            .CreateAsyncScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        var user = await userManager.FindByEmailAsync(email);

        Assert.NotNull(user);
        Assert.True(await userManager.IsLockedOutAsync(user));
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
