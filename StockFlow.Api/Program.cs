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

if (args.Contains("--create-admin"))
{
    var email = app.Configuration["BootstrapAdmin:Email"];
    var password = app.Configuration["BootstrapAdmin:Password"];

    if (string.IsNullOrWhiteSpace(email)
        || string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException(
            "Configure BootstrapAdmin:Email and BootstrapAdmin:Password.");
    }

    await using var scope = app.Services.CreateAsyncScope();

    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<IdentityUser>>();

    await RoleInitializer.InitializeAsync(roleManager);

    await AdminInitializer.InitializeAsync(
        userManager,
        email.Trim(),
        password);

    Console.WriteLine("Administrator created.");
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
