using Microsoft.EntityFrameworkCore;

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/api/products",async (StockFlowDbContext db)=>
{

    return await db.Products.ToListAsync();
});


// app.MapPost("/api/products", async (CreateProductDto dto, StockFlowDbContext db) =>
// {

//     var product = new Product
//     {
//         Name = dto.Name,
//         Sku = dto.Sku,
//         Price = dto.Price,
//         QuantityInStock = dto.QuantityInStock
//     };


//     db.Products.Add(product);

//     await db.SaveChangesAsync();

//     return Results.Created($"/api/products/{product.Id}", product);
// });

// app.MapGet("/api/products/{id}", async (int id, StockFlowDbContext db) =>
// {
//     var product = await db.Products.FindAsync(id);

//     if (product is null)
//      {
//      return Results.NotFound($"Product with ID {id} was not found.");
//      }

//     return Results.Ok(product);
// });


app.MapDelete("/api/products/{id}",async (int id, StockFlowDbContext db) =>{

    var product = await db.Products.FindAsync(id);

    if(product is null){
        return Results.NotFound($"Product with ID {id} doesn't exit to be deleted.");
    }

    db.Remove(product);

    await db.SaveChangesAsync();

    return Results.Ok(product);

});


app.MapPut("/api/products/{id}", async (int id, Product updatedProduct, StockFlowDbContext db) =>
{

    var product = await db.Products.FindAsync(id);

    if (product is null)
     {
        return Results.NotFound($"Product with ID {id} doesn't exist to be updated.");
     }

     product.Name = updatedProduct.Name;
     product.Sku = updatedProduct.Sku;
     product.Price = updatedProduct.Price;
     product.QuantityInStock = updatedProduct.QuantityInStock;

    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapControllers();



app.Run();


