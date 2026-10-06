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

    return CreatedAtAction(nameof(GetProduct),new { id = product.Id },product);

    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await _db.Products.FindAsync(id);

        if (product is null)
        {
        return NotFound($"Product with ID {id} was not found.");
        }

        return Ok(product);
    }
}
