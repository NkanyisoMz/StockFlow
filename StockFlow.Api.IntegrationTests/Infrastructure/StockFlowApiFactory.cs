using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace StockFlow.Api.IntegrationTests.Infrastructure;

public sealed class StockFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public StockFlowApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(configuration =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        _connectionString,
                    ["Jwt:Issuer"] = "StockFlow.Tests",
                    ["Jwt:Audience"] = "StockFlow.Tests.Client",
                    ["Jwt:Key"] =
                        "StockFlow-integration-tests-only-key-not-for-deployment-123456789",
                    ["Jwt:ExpirationMinutes"] = "15"
                });
        });

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
