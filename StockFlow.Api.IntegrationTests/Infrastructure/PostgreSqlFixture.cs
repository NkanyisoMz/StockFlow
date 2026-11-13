using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace StockFlow.Api.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database =
        new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("stockflow_tests")
            .WithUsername("stockflow_test")
            .WithPassword("stockflow_test_password")
            .Build();

    public StockFlowApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        Factory = new StockFlowApiFactory(
            _database.GetConnectionString());

        await using var scope = Factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<StockFlowDbContext>();

        // Apply real migrations so tests use the application's database schema.
        await db.Database.MigrateAsync();
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
            // Release the container even if application cleanup fails.
            await _database.DisposeAsync();
        }
    }
}
