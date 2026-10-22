using Microsoft.EntityFrameworkCore;
using StockFlow.Api.DTOs;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;
using StockFlow.Api.Exceptions;

public class OrderService
{
    private readonly StockFlowDbContext _db;

    public OrderService(StockFlowDbContext db)
    {
        _db = db;
    }

    public async Task<Order> CreateAsync(CreateOrderDto dto)
    {

        if (dto.CustomerId <= 0)
        {
            throw new InvalidOrderException("Customer ID must be greater than zero.");
        }

        var customerExists = await _db.Customers
            .AnyAsync(c => c.Id == dto.CustomerId);

        if (!customerExists)
        {
            throw new CustomerNotFoundException($"Cannot place order. Customer with ID {dto.CustomerId} does not exist.");
        }

        if (dto.Items is null || dto.Items.Count == 0)
        {
            throw new InvalidOrderException(
                "An order must contain at least one item.");
        }

        if (dto.Items.Any(i => i is null || i.ProductId <= 0 || i.Quantity <= 0))
        {
            throw new InvalidOrderException(
                "Every order item must have a valid product ID and a positive quantity.");
        }

        var requestedProductIds = dto.Items.Select(i => i.ProductId).ToList();

        if (requestedProductIds.Distinct().Count() != requestedProductIds.Count)
        {
            throw new InvalidOrderException("An order cannot contain duplicate line items for the same product. Combine the quantities instead.");
        }


        var products = await _db.Products
            .AsNoTracking()
            .Where(p => requestedProductIds.Contains(p.Id))
            .ToListAsync();



        if (products.Count != requestedProductIds.Count)
        {
            var foundProductIds = products.Select(p => p.Id).ToHashSet();
            var missingIds = requestedProductIds.Where(id => !foundProductIds.Contains(id));

            throw new ProductNotFoundException($"The following product IDs do not exist in inventory: {string.Join(", ", missingIds)}");
        }

        var productDictionary = products.ToDictionary(p => p.Id);

        var order = new Order
        {
            CustomerId = dto.CustomerId,
            Status = OrderStatus.Pending,
            TotalAmount = 0m
        };

        decimal runningTotal = 0m;

        checked
        {
            foreach (var itemDto in dto.Items)
            {
                var dbProduct = productDictionary[itemDto.ProductId];

                decimal unitPrice = dbProduct.Price;
                decimal lineTotal = itemDto.Quantity * unitPrice;

                var orderItem = new OrderItem
                {
                    ProductId = itemDto.ProductId,
                    Quantity = itemDto.Quantity,
                    UnitPrice = unitPrice
                };

                order.OrderItems.Add(orderItem);
                runningTotal += lineTotal;
            }
        }

        if (runningTotal > 9999999999999999.99m)
        {
            throw new InvalidOrderException("The calculated order total exceeds maximum allowed system transaction limits.");
        }

        order.TotalAmount = runningTotal;


         _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return order;

    }
}
