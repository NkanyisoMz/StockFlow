using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using StockFlow.Api.Security;

namespace StockFlow.Api.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("stockflow_tests")
            .WithUsername("stockflow_test")
            .WithPassword("stockflow_test_password")
            .Build();

    private string _adminToken = string.Empty;

    public StockFlowApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        Factory = new StockFlowApiFactory(
            _database.GetConnectionString());

        await using var scope = Factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<StockFlowDbContext>();

        await db.Database.MigrateAsync();

        await InitializeAdminAsync();
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (Factory is not null)
            {
                await Factory.DisposeAsync();
            }
        }
        finally
        {
            await _database.DisposeAsync();
        }
    }

    private async Task InitializeAdminAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<IdentityUser>>();

        await RoleInitializer.InitializeAsync(roleManager);

        const string email = "test-admin@example.com";
        const string password = "Test-Only-Password123!";

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
            AppRoles.Admin);

        Assert.True(
            roleResult.Succeeded,
            string.Join(
                "; ",
                roleResult.Errors.Select(error => error.Description)));

        using var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        // Exercise the actual login endpoint to obtain the test token.
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { email, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        _adminToken = result.GetProperty("accessToken").GetString()!;

        Assert.False(string.IsNullOrWhiteSpace(_adminToken));
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _adminToken);

        return client;
    }
}
