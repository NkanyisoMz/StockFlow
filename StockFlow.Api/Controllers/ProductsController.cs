using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{

    private readonly StockFlowDbContext _db;

    public ProductsController(StockFlowDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct(CreateProductDto dto)
    {

    var product = new Product
    {
        Name = dto.Name,
        Sku = dto.Sku,
        Price = dto.Price,
        QuantityInStock = dto.QuantityInStock
    };


    _db.Products.Add(product);

    await _db.SaveChangesAsync();

    return Created($"/api/products/{product.Id}", product);

    }
}
