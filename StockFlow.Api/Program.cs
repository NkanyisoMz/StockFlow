var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

List<Product> products= new()
{
    new Product(){
        Id = 1,
        Name = "Shirt",
        Sku = "TS-VNK-BLU-MED",
        Price = 500.00m,
        QuantityInStock = 2
    },
    new Product(){
         Id = 2,
        Name = "Coffee",
        Sku = "ETH-DRK-12Z-BG",
        Price = 30.00m,
        QuantityInStock = 1
    },
    new Product(){
        Id = 3,
        Name = "Phone",
        Sku = "PHN-APL-I15-256",
        Price = 890.00m,
        QuantityInStock = 1
    },
    new Product(){
        Id = 4,
        Name = "Shoes",
        Sku = "M-RUN-BLK-090",
        Price = 550.00m,
        QuantityInStock = 1
    }
};

app.MapGet("/api/products",()=>
{

    return products;
});


 app.MapPost("/api/products",(Product product) =>
 {
    if(products.Count == 0){
        product.Id = 1;
    }else
    {
        int  max = products.Max(p => p.Id);
        product.Id = max + 1;

    }

    products.Add(product);
    return Results.Created($"/api/products/{product.Id}", product);
 });

app.MapGet("/api/products/{id}",(int id) =>
{
    var product = products.FirstOrDefault(p => p.Id == id);

    if (product is null)
    {
    return Results.NotFound($"Product with ID {id} was not found.");
    }

    return Results.Ok(product);
});

app.MapDelete("/api/products/{id}",(int id) =>{
    var product = products.FirstOrDefault(p => p.Id == id);

    if(product is null){
        return Results.NotFound($"Product with ID {id} doesn't exit to be deleted.");
    }

    products.Remove(product);
    return Results.Ok(product);

});

app.MapPut("/api/products/{id}", (int id, Product updatedProduct) =>
{
    var product = products.FirstOrDefault(p => p.Id == id);

    if (product is null)
    {
        return Results.NotFound($"Product with ID {id} doesn't exist to be updated.");
    }

    product.Name = updatedProduct.Name;
    product.Sku = updatedProduct.Sku;
    product.Price = updatedProduct.Price;
    product.QuantityInStock = updatedProduct.QuantityInStock;

    return Results.NoContent();
});


app.Run();


