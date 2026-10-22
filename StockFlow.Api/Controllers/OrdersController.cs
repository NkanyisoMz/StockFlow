using Microsoft.AspNetCore.Mvc;
using StockFlow.Api.DTOs;
using StockFlow.Api.Exceptions;

namespace StockFlow.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;

    public OrdersController(OrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> CreateOrder(CreateOrderDto dto)
    {
        try
        {
            var order = await _orderService.CreateAsync(dto);

            var responseDto = new OrderResponseDto
            {
                Id = order.Id,
                CustomerId = order.CustomerId,
                OrderDate = order.OrderDate,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                Items = order.OrderItems.Select(oi => new OrderItemResponseDto
                {
                    Id = oi.Id,
                    ProductId = oi.ProductId,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice
                }).ToList()
            };

            return StatusCode(StatusCodes.Status201Created, responseDto);
        }
        catch (CustomerNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ProductNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOrderException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

