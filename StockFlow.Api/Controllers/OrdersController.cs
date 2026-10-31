using Microsoft.AspNetCore.Mvc;
using StockFlow.Api.DTOs;
using StockFlow.Api.Exceptions;
using StockFlow.Domain.Entities;

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

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderResponseDto>> GetOrderById(int id)
    {
        var order = await _orderService.GetByIdAsync(id);

        if (order is null)
        {
            return NotFound(new { message = $"Order with ID {id} not found." });
        }

        return Ok(MapToResponseDto(order));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<OrderResponseDto>>> GetOrders([FromQuery] OrderQueryDto filters)
    {
        var result = await _orderService.GetAllAsync(filters);

        var responseItems = result.Items
            .Select(order => MapToResponseDto(order))
            .ToList();

        return Ok(new PagedResult<OrderResponseDto>
        {
            Items = responseItems,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            TotalPages = result.TotalPages
        });
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponseDto>> CreateOrder(CreateOrderDto dto)
    {
        try
        {
            var order = await _orderService.CreateAsync(dto);

            var responseDto = MapToResponseDto(order);

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


    [HttpPost("{id}/confirm")]
    public async Task<ActionResult<OrderResponseDto>> ConfirmOrder(int id)
    {
        try
        {

            var order = await _orderService.ConfirmOrderAsync(id);


            var responseDto = MapToResponseDto(order);

            return Ok(responseDto);
        }
        catch (OrderNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ProductNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOrderException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InsufficientStockException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<ActionResult<OrderResponseDto>> CancelOrder(int id)
    {
        try
        {
            var order = await _orderService.CancelOrderAsync(id);

            var responseDto = MapToResponseDto(order);

            return Ok(responseDto);
        }
        catch (OrderNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOrderException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }


    private static OrderResponseDto MapToResponseDto(Order order)
    {
        return new OrderResponseDto
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
    }
}

