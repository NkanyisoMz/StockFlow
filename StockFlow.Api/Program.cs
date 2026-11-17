using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using StockFlow.Api.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not found."
    );

builder.Services.AddDbContext<StockFlowDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

builder.Services
    .AddIdentityCore<IdentityUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<StockFlowDbContext>();

builder.Services.AddScoped<ProductService>();

builder.Services.AddScoped<InventoryService>();

builder.Services.AddScoped<CustomerService>();

builder.Services.AddScoped<OrderService>();

var app = builder.Build();

if (args.Contains("--initialize-roles"))
{
    await using var scope = app.Services.CreateAsyncScope();

    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    await RoleInitializer.InitializeAsync(roleManager);

    Console.WriteLine("StockFlow roles initialized.");
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

// Allows integration tests to reference the application's entry point.
public partial class Program { }
