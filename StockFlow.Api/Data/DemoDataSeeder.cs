using Microsoft.EntityFrameworkCore;
using StockFlow.Domain.Entities;
using StockFlow.Domain.Enums;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(StockFlowDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (await db.Products.AnyAsync() || await db.Customers.AnyAsync()
            || await db.Orders.AnyAsync() || await db.OrderItems.AnyAsync()
            || await db.InventoryMovements.AnyAsync())
        {
            Console.WriteLine("Demo data was not added: business data already exists.");
            return;
        }

        var products = new[]
        {
            new Product { Name = "USB-C Hub", Sku = "DEMO-HUB-001", Price = 450m, QuantityInStock = 10 },
            new Product { Name = "Wireless Keyboard", Sku = "DEMO-KEY-001", Price = 750m, QuantityInStock = 15 },
            new Product { Name = "Wireless Mouse", Sku = "DEMO-MOU-001", Price = 250m, QuantityInStock = 20 }
        };
        var customer = new Customer { Name = "Demo Office Supplies", Email = "office@example.com" };
        db.Products.AddRange(products);
        db.Customers.AddRange(customer, new Customer
        {
            Name = "Demo Training Centre", Email = "training@example.com"
        });
        await db.SaveChangesAsync();

        // Synthetic opening balances always have matching receipts.
        foreach (var product in products)
            db.InventoryMovements.Add(new InventoryMovement
            {
                ProductId = product.Id, Type = MovementType.StockIn,
                Quantity = product.QuantityInStock, Reason = "Demo opening stock"
            });

        var order = new Order
        {
            CustomerId = customer.Id, Status = OrderStatus.Pending,
            TotalAmount = products[0].Price * 2 + products[2].Price
        };
        order.OrderItems.Add(new OrderItem
        {
            ProductId = products[0].Id, Quantity = 2, UnitPrice = products[0].Price
        });
        order.OrderItems.Add(new OrderItem
        {
            ProductId = products[2].Id, Quantity = 1, UnitPrice = products[2].Price
        });
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        Console.WriteLine("Demo data added: 3 products, 2 customers, 3 stock receipts, and 1 pending order.");
    }
}
