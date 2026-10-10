using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly ProductService _productService;

    // Inject the service instead of the DbContext
    public ProductsController(ProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<Product>>> GetProducts([FromQuery] ProductQueryDto query)
    {
        var products = await _productService.GetAllAsync(query);
        return Ok(products);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await _productService.GetByIdAsync(id);

        if (product is null) return NotFound($"Product with ID {id} was not found.");

        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct(CreateProductDto dto)
    {

        try
        {
            var product = await _productService.CreateAsync(dto);

            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }
        catch (DuplicateSkuException ex)
        {
            return Conflict(new {message = ex.Message});
        }

    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduct(int id, UpdateProductDto dto)
    {

        try
        {
            var updatedProduct = await _productService.UpdateAsync(id, dto);
            if (updatedProduct is null)
            {
                return NotFound($"Product with ID {id} doesn't exist to be updated.");
            }

            return NoContent();

        }
        catch (DuplicateSkuException ex)
        {
            return Conflict(new {message = ex.Message});
        }

    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<Product>> DeleteProduct(int id)
    {
        var deletedProduct = await _productService.DeleteAsync(id);

        if (deletedProduct is null) return NotFound($"Product with ID {id} doesn't exist to be deleted.");

        return Ok(deletedProduct);
    }
}
