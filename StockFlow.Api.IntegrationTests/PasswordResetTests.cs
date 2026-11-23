using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using StockFlow.Api.IntegrationTests.Infrastructure;
using StockFlow.Api.Security;
using Xunit;

namespace StockFlow.Api.IntegrationTests;

public sealed class PasswordResetTests : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;
    public PasswordResetTests(PostgreSqlFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("Development", "Replacement-Password123!", true)]
    [InlineData("Development", "weak", false)]
    [InlineData("Production", "Replacement-Password123!", false)]
    public async Task Reset_PreservesAccountAndEnforcesRules(string environment, string password, bool succeeds)
    {
        var email = $"reset-{Guid.NewGuid():N}@example.com";
        const string oldPassword = "Original-Password123!";
        string id;
        await using (var scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = new IdentityUser { UserName = email, Email = email, PhoneNumber = "123456789" };
            Assert.True((await manager.CreateAsync(user, oldPassword)).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, AppRoles.Staff)).Succeeded);
            id = user.Id;
            var result = await PasswordResetCommand.ResetAsync(manager, new TestEnvironment(environment), email, password);
            Assert.Equal(succeeds, result.Succeeded);
        }
        await using var verification = _fixture.Factory.Services.CreateAsyncScope();
        var savedManager = verification.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var saved = await savedManager.FindByEmailAsync(email);
        Assert.NotNull(saved);
        Assert.Equal(id, saved.Id);
        Assert.Equal(email, saved.Email);
        Assert.Equal(email, saved.UserName);
        Assert.Equal("123456789", saved.PhoneNumber);
        Assert.Equal(AppRoles.Staff, Assert.Single(await savedManager.GetRolesAsync(saved)));
        Assert.Equal(succeeds, await savedManager.CheckPasswordAsync(saved, password));
        Assert.Equal(!succeeds, await savedManager.CheckPasswordAsync(saved, oldPassword));
    }

    [Fact]
    public async Task Reset_MissingAccount_DoesNotCreateUser()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var email = $"missing-{Guid.NewGuid():N}@example.com";
        var result = await PasswordResetCommand.ResetAsync(manager, new TestEnvironment("Development"), email, "Replacement-Password123!");
        Assert.False(result.Succeeded);
        Assert.Null(await manager.FindByEmailAsync(email));
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "StockFlow.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
