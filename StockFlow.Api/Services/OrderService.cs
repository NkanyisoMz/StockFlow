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

        try
        {
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

        }
        catch (OverflowException)
        {
            throw new InvalidOrderException(
                "Order amount exceeds the supported monetary range.");
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

    public async Task<Order> ConfirmOrderAsync(int orderId)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        var order = await _db.Orders
            .FromSqlInterpolated(
            $"SELECT * FROM \"Orders\" WHERE \"Id\" = {orderId} FOR UPDATE")
            .SingleOrDefaultAsync();

        if (order is null)
        {
            throw new OrderNotFoundException(
                $"Order with ID {orderId} was not found.");
        }

        if (order.Status != OrderStatus.Pending)
        {
            throw new InvalidOrderException(
                $"Order {orderId} cannot be confirmed because its status is {order.Status}.");
        }

        order.OrderItems = await _db.OrderItems
            .Where(item => item.OrderId == orderId)
            .OrderBy(item => item.ProductId)
            .ToListAsync();

        if (order.OrderItems.Count == 0)
        {
            throw new InvalidOrderException(
                $"Order {orderId} cannot be confirmed because it contains no items.");
        }

        var sortedProductIds = order.OrderItems
            .Select(item => item.ProductId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();


        var lockedProducts = new Dictionary<int, Product>();

        foreach (var productId in sortedProductIds)
        {
            var product = await _db.Products
                .FromSqlInterpolated(
                $"SELECT * FROM \"Products\" WHERE \"Id\" = {productId} FOR UPDATE")
                .SingleOrDefaultAsync();

            if (product is null)
            {
                throw new ProductNotFoundException(
                    $"Product with ID {productId} was not found.");
            }

            lockedProducts.Add(product.Id, product);
        }

        if (order.OrderItems.Any(item => item.Quantity <= 0))
        {
            throw new InvalidOrderException(
                $"Order {orderId} contains invalid item quantities.");
        }

        var requiredQuantities = order.OrderItems
            .GroupBy(item => item.ProductId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => item.Quantity)
            );

        foreach (var (productId, requiredQuantity) in requiredQuantities)
        {
            var product = lockedProducts[productId];

            if (requiredQuantity > product.QuantityInStock)
            {
                throw new InsufficientStockException(
                    $"Insufficient stock for product {productId}. " +
                    $"Available: {product.QuantityInStock}, " +
                    $"Requested: {requiredQuantity}.");
            }
        }

        foreach (var (productId, requiredQuantity) in requiredQuantities)
        {
            var product = lockedProducts[productId];

            product.QuantityInStock -= requiredQuantity;

            var movement = new InventoryMovement
            {
                ProductId = productId,
                Type = MovementType.StockOut,
                Quantity = requiredQuantity,
                Reason = $"Order #{orderId} confirmed"
            };

            _db.InventoryMovements.Add(movement);
        }

        order.Status = OrderStatus.Confirmed;

        await _db.SaveChangesAsync();

        await transaction.CommitAsync();

        return order;
    }
}
