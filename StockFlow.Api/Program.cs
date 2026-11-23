using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using StockFlow.Api.Security;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StockFlow.Api.Services;

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
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<StockFlowDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<ProductService>();

builder.Services.AddScoped<InventoryService>();

builder.Services.AddScoped<CustomerService>();

builder.Services.AddScoped<OrderService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var issuer = builder.Configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "Jwt:Issuer is required.");

        var audience = builder.Configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "Jwt:Audience is required.");

        var key = builder.Configuration["Jwt:Key"]
            ?? throw new InvalidOperationException(
                "Jwt:Key is required.");

        if (Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key must contain at least 32 bytes.");
        }

        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,

            ValidateAudience = true,
            ValidAudience = audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(key)),

            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,

            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },

            NameClaimType = "sub",
            RoleClaimType = "role",

            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<JwtTokenService>();

var app = builder.Build();

if (args.Contains("--reset-password"))
{
    if (!app.Environment.IsDevelopment())
    {
        Console.Error.WriteLine("Password reset is available only in Development.");
        Environment.ExitCode = 1;
        return;
    }
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Environment.ExitCode = await PasswordResetCommand.RunAsync(manager, app.Environment);
    }
    catch (Exception)
    {
        // Avoid printing tokens, credentials, or database exception details.
        Console.Error.WriteLine("Password reset could not complete. Check local database availability and configuration.");
        Environment.ExitCode = 1;
    }
    return;
}
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

if (args.Contains("--create-staff"))
{
    var email = app.Configuration["BootstrapStaff:Email"];
    var password = app.Configuration["BootstrapStaff:Password"];

    if (string.IsNullOrWhiteSpace(email)
        || string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException(
            "Configure BootstrapStaff:Email and BootstrapStaff:Password.");
    }

    await using var scope = app.Services.CreateAsyncScope();

    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<IdentityUser>>();

    await RoleInitializer.InitializeAsync(roleManager);

    await StaffInitializer.InitializeAsync(
        userManager,
        email.Trim(),
        password);

    Console.WriteLine("Staff account created.");
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Allows integration tests to reference the application's entry point.
public partial class Program { }
