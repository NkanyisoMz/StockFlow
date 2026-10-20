using Microsoft.AspNetCore.Mvc;
using StockFlow.Api.DTOs;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly CustomerService _customerService;

    public CustomersController(CustomerService customerService)
    {
        _customerService = customerService;
    }



    [HttpPost]
    public async Task<ActionResult<Customer>> CreateCustomer(CreateCustomerDto dto)
    {
        try
        {
            var customer = await _customerService.CreateAsync(dto);

            return CreatedAtAction(nameof(GetCustomerById), new { id = customer.Id }, customer);
        }
        catch (DuplicateEmailException ex)
        {
             return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Customer>> GetCustomerById(int id)
    {
        var customer = await _customerService.GetByIdAsync(id);

        if (customer is null)
        {
            return NotFound($"Customer with ID {id} does not exist.");
        }

        return Ok(customer);
    }

    [HttpGet]
    public async Task<ActionResult<List<Customer>>> GetAllCustomers()
    {
        var customers = await _customerService.GetAllAsync();

        return Ok(customers);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCustomer(int id, UpdateCustomerDto dto)
    {
        try
        {
            var wasUpdated = await _customerService.UpdateAsync(id, dto);

        if (!wasUpdated)
        {
            return NotFound($"Customer with ID {id} does not exist to be updated.");
        }

        return NoContent();
        }
        catch (DuplicateEmailException ex)
        {
        return Conflict(new { message = ex.Message });
        }
    }
}
