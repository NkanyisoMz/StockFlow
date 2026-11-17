using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StockFlow.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace StockFlow.Api.IntegrationTests;

public sealed class DatabaseConstraintTests
    : IClassFixture<PostgreSqlFixture>
{
    private readonly PostgreSqlFixture _fixture;

    public DatabaseConstraintTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_001)]
    public async Task SaveProduct_WithInvalidStock_DatabaseRejectsWrite(
        int quantity)
    {
        await using var scope = _fixture.Factory.Services
            .CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<StockFlowDbContext>();

        db.Products.Add(new Product
        {
            Name = "Constraint Test Product",
            Sku = $"TEST-{Guid.NewGuid():N}",
            Price = 100.00m,
            QuantityInStock = quantity
        });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(
            exception.InnerException);

        Assert.Equal(
            PostgresErrorCodes.CheckViolation,
            postgresException.SqlState);

        Assert.Equal(
            "CK_Products_QuantityInStock_Range",
            postgresException.ConstraintName);
    }

    [Fact]
    public async Task SaveProduct_WithDuplicateSku_DatabaseRejectsWrite()
    {
        await using var scope = _fixture.Factory.Services
            .CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<StockFlowDbContext>();

        var sku = $"TEST-{Guid.NewGuid():N}";

        db.Products.Add(new Product
        {
            Name = "Original Product",
            Sku = sku,
            Price = 100.00m,
            QuantityInStock = 0
        });

        await db.SaveChangesAsync();

        // Bypass the service's uniqueness check.
        db.Products.Add(new Product
        {
            Name = "Duplicate Product",
            Sku = sku,
            Price = 200.00m,
            QuantityInStock = 0
        });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(
            exception.InnerException);

        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            postgresException.SqlState);

        Assert.Equal(
            "IX_Products_Sku",
            postgresException.ConstraintName);
    }

    [Fact]
    public async Task DeleteProduct_WithInventoryHistory_DatabaseRejectsWrite()
    {
        using var client = _fixture.Factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var productId = await TestData.CreateProductAsync(
            client,
            price: 100.00m,
            initialStock: 5);

        await using var scope = _fixture.Factory.Services
            .CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<StockFlowDbContext>();

        var product = await db.Products.SingleAsync(
            product => product.Id == productId);

        db.Products.Remove(product);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(
            exception.InnerException);

        Assert.Equal(
            PostgresErrorCodes.ForeignKeyViolation,
            postgresException.SqlState);

        Assert.Equal(
            "FK_InventoryMovements_Products_ProductId",
            postgresException.ConstraintName);
    }
}
