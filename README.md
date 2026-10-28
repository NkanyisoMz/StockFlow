# StockFlow

**StockFlow** is an Order and Inventory Management System under active development. Its backend is a REST API built with **C#, ASP.NET Core (.NET 10), Entity Framework Core, and PostgreSQL**. A **React + TypeScript** frontend is planned to provide a complete full-stack experience.

StockFlow manages products, inventory, customers, and orders. It demonstrates RESTful API design, controller/service separation, dependency injection, relational data modelling, input validation, business rules, database integrity, transactions, and concurrency control.

**Project status:** Active development — product management, inventory management, customer management, order creation, and transactional order confirmation are implemented. Order retrieval, cancellation, automated tests, authentication, frontend development, and deployment are upcoming.

## Features

### Product Management

- Create, retrieve, update, and delete products.
- Search products by name (case-insensitive), filter by minimum and maximum price, and combine filters.
- Paginate results with page number, page size, total count, and total pages.
- Enforce unique SKUs and maximum lengths (100 characters for names, 50 for SKUs).
- Create products with zero stock; update name, SKU, and price without directly changing stock.
- Validate prices as non-negative amounts with at most two decimal places and within the supported `numeric(18,2)` range.
- Return appropriate HTTP status codes for invalid requests, missing products, and SKU conflicts.

### Inventory Management

- Receive stock (`StockIn`) and issue stock (`StockOut`) through dedicated endpoints.
- Prevent insufficient-stock deductions and enforce a maximum of **100,000 units per product**.
- Validate capacity before adding stock to avoid integer overflow.
- Persist inventory movements with product ID, type, quantity, reason, and timestamp.
- Retrieve product-specific movement history in reverse chronological order.
- Return `200 OK` with an empty array for existing products without movements, or `404 Not Found` for nonexistent products.
- Use EF Core transactions and PostgreSQL `SELECT ... FOR UPDATE` row locks to coordinate concurrent stock changes.

### Customer Management

- Create, retrieve, and update customers.
- Store customer name, email, and creation timestamp.
- Enforce unique customer emails and validate customer input.
- Associate orders with existing customers through a foreign key.

### Order Management

- Create pending orders containing one or more products.
- Verify the customer and referenced products exist; reject invalid quantities and duplicate product IDs in order creation.
- Capture each product's unit price at order creation so later product-price changes do not alter the order's historical price.
- Calculate order totals from line quantities and captured unit prices.
- Persist orders and order items with relational constraints and decimal precision.
- Confirm pending orders through a transaction that checks available stock, deducts quantities, records stock-out movements, and changes the order status to `Confirmed`.
- Reject confirmation of already-confirmed or cancelled orders, nonexistent orders, and orders with insufficient stock.
- Preserve the order's `Pending` status and inventory balances when confirmation fails.

**Order statuses:** `0` = `Pending`, `1` = `Confirmed`, `2` = `Cancelled`.

**Important:** Creating an order **does not** reserve or deduct stock. Inventory is deducted only when an order is successfully confirmed.

## Transaction and Concurrency Safety

StockFlow uses explicit EF Core transactions and PostgreSQL row-level locks for inventory consistency.

**Stock-In and Stock-Out:**

1. Begin a transaction.
2. Lock the product row using `SELECT ... FOR UPDATE`.
3. Validate the requested movement.
4. Update the quantity and insert an inventory movement.
5. Save changes and commit.

**Order confirmation:**

1. Begin a transaction and lock the order row.
2. Ensure the order is still `Pending`.
3. Load order items, then lock distinct product rows in ascending product ID order.
4. Aggregate quantities per product and verify that all required stock is available.
5. Deduct stock and create one `StockOut` movement per distinct product.
6. Mark the order `Confirmed`, save changes, and commit.

If validation or saving fails before commit, the transaction is not committed and is rolled back on disposal. Locking the order prevents duplicate confirmation; locking product rows prevents competing confirmations and inventory operations from relying on stale stock balances.

### Manual Concurrency Tests Performed

| Scenario | Observed result |
|---|---|
| Two concurrent stock-out requests, each requesting 4 units from 5 available | One `200 OK`, one `409 Conflict`; final stock 1; one movement |
| Confirm an order requesting 10 units when only 9 are available | `409 Conflict`; order remains pending; stock remains 9; no confirmation movements |
| Two concurrent requests confirming the same order | One `200 OK`, one `409 Conflict`; stock deducted once; one movement |
| Two different orders each requesting 5 units when 7 are available | One `200 OK`, one `409 Conflict`; confirmed/pending statuses; final stock 2; one movement of 5 units |

These are **manual integration tests**. Automated integration and concurrency tests are planned.

## Database and Data Integrity

- PostgreSQL persistence through Entity Framework Core and code-first migrations.
- Unique indexes for product SKU and customer email.
- Relationships between customers, orders, order items, products, and inventory movements.
- Product, order-total, and order-item unit-price columns use `numeric(18,2)` precision.
- Database CHECK constraints enforce non-negative prices and totals, positive order-item and movement quantities, and product stock between 0 and 100,000.
- Restrictive foreign-key delete behavior protects referenced customer/product records; order items cascade with their parent order.
- Database constraints complement service-layer validation, including for writes outside the API.

## Application Architecture

- ASP.NET Core controller-based REST API.
- Service layer for business logic and EF Core operations.
- Dependency injection.
- Request and response DTOs to separate HTTP contracts from database entities.
- Asynchronous operations, LINQ filtering/projection, and EF Core change tracking.
- Reusable `PagedResult<T>` for product pagination.
- Custom exceptions for business-rule failures, mapped to HTTP responses by controllers.
- Explicit database transactions and PostgreSQL row locks for stock-changing operations.

## Tech Stack

| Technology | Purpose |
|---|---|
| C# / .NET 10 | Backend language and runtime |
| ASP.NET Core Web API | HTTP endpoints and controllers |
| Entity Framework Core | ORM, migrations, transactions |
| PostgreSQL | Relational database, constraints, concurrency control |
| Npgsql | EF Core PostgreSQL provider |
| Git / GitHub | Version control |
| PowerShell / psql | Manual API and database testing |
| React + TypeScript | **Planned** frontend |
| Docker / CI/CD | **Planned** packaging and deployment |

## API Endpoints

### Products

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/products` | List products with filtering and pagination |
| GET | `/api/products/{id}` | Get product by ID |
| POST | `/api/products` | Create product with zero initial stock |
| PUT | `/api/products/{id}` | Update name, SKU, and price |
| DELETE | `/api/products/{id}` | Delete product, subject to database relationships |

**Product query parameters**

| Parameter | Description | Default |
|---|---|---|
| `search` | Case-insensitive product name search | None |
| `minPrice` | Minimum product price | None |
| `maxPrice` | Maximum product price | None |
| `page` | Page number | 1 |
| `pageSize` | Items per page (maximum 100) | 10 |

```http
GET /api/products?search=keyboard
GET /api/products?minPrice=500&maxPrice=2000
GET /api/products?page=1&pageSize=10
GET /api/products?search=keyboard&minPrice=1000&maxPrice=2000&page=1&pageSize=5
```

Example paginated response (illustrative data):

```json
{
  "items": [
    {
      "id": 3,
      "name": "Logitech Keyboard MX",
      "sku": "KEY-LOG-MX",
      "price": 1750.00,
      "quantityInStock": 17
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

**Create product** — `POST /api/products`

```json
{
  "name": "USB-C Hub",
  "sku": "HUB-USB-C-001",
  "price": 450.00
}
```

A successful creation returns `201 Created` and the created product, including `quantityInStock: 0`. Stock is subsequently received through Stock-In.

### Inventory

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/inventory/stock-in` | Increase stock and record a movement |
| POST | `/api/inventory/stock-out` | Decrease stock and record a movement |
| GET | `/api/inventory/products/{productId}/history` | Get a product's movement history |

**Stock-In** — `POST /api/inventory/stock-in`

```json
{
  "productId": 12,
  "quantity": 10,
  "reason": "Initial stock received"
}
```

**Stock-Out** — `POST /api/inventory/stock-out`

```json
{
  "productId": 12,
  "quantity": 4,
  "reason": "Warehouse dispatch"
}
```

Successful movements return `200 OK` and a movement response similar to:

```json
{
  "id": 7,
  "productId": 12,
  "type": 1,
  "quantity": 4,
  "reason": "Warehouse dispatch",
  "createdAt": "2026-10-16T15:40:45.44423Z"
}
```

Movement types: `0` = `StockIn`, `1` = `StockOut`. Insufficient stock or exceeding the stock limit returns `409 Conflict`.

**Inventory history** — `GET /api/inventory/products/12/history`

Returns an array of movements, most recent first. Existing products without movements return `200 OK` with `[]`; nonexistent products return `404 Not Found`.

### Customers

The customer module supports customer creation, retrieval, and updates. The exact customer endpoint routes and payload examples can be added after checking the current `CustomersController` so the documentation matches the implementation precisely.

### Orders

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/orders` | Create a pending order |
| POST | `/api/orders/{id}/confirm` | Confirm a pending order and deduct inventory atomically |

**Create order** — `POST /api/orders`

```json
{
  "customerId": 1,
  "items": [
    { "productId": 3, "quantity": 1 },
    { "productId": 6, "quantity": 1 }
  ]
}
```

Success: `201 Created`. The response includes order ID, customer ID, date, status, total amount, and order items with captured unit prices and calculated line totals.

Example response (illustrative order ID):

```json
{
  "id": 4,
  "customerId": 1,
  "orderDate": "2026-10-22T22:47:39.995729Z",
  "status": 0,
  "totalAmount": 2549.99,
  "items": [
    {
      "id": 7,
      "productId": 3,
      "quantity": 1,
      "unitPrice": 1750.00,
      "lineTotal": 1750.00
    },
    {
      "id": 8,
      "productId": 6,
      "quantity": 1,
      "unitPrice": 799.99,
      "lineTotal": 799.99
    }
  ]
}
```

Invalid order input returns `400 Bad Request`; a nonexistent customer or referenced product returns `404 Not Found`.

**Confirm order** — `POST /api/orders/{id}/confirm`

```http
POST /api/orders/4/confirm
```

Success: `200 OK`, with the updated order response and `status: 1` (`Confirmed`). Stock deductions and inventory movement records are committed together.

| Situation | Response |
|---|---|
| Order confirmed | `200 OK` |
| Order does not exist | `404 Not Found` |
| Referenced product does not exist | `404 Not Found` |
| Order is already confirmed or cancelled | `409 Conflict` |
| Insufficient stock | `409 Conflict` |

**Not yet implemented:** `GET /api/orders`, `GET /api/orders/{id}`, and order cancellation are planned for the next milestone.

## Validation and Error Handling

| HTTP Status | Typical meaning |
|---|---|
| `200 OK` | Successful retrieval, stock movement, order confirmation, or other successful operation |
| `201 Created` | Product, customer, or order created |
| `204 No Content` | Successful update where no response body is returned |
| `400 Bad Request` | Invalid request, invalid quantities/prices, or invalid query parameters |
| `404 Not Found` | Requested product, customer, or order not found |
| `409 Conflict` | Duplicate SKU/email, stock-limit or insufficient-stock violation, or invalid order status for confirmation |

Validation occurs at the request/service layers and is reinforced by database constraints. For example, product stock cannot be modified through product update DTOs; it must be changed using inventory operations or a successful order confirmation.

## Project Structure

The following is a **representative structure**, highlighting the principal modules rather than every file:

```text
StockFlow/
├── StockFlow.slnx
└── StockFlow.Api/
    ├── Common/
    │   └── PagedResult.cs
    ├── Controllers/
    │   ├── ProductsController.cs
    │   ├── InventoryController.cs
    │   ├── CustomersController.cs
    │   └── OrdersController.cs
    ├── Data/
    │   └── StockFlowDbContext.cs
    ├── DTOs/
    │   ├── ... product DTOs
    │   ├── ... inventory DTOs
    │   ├── ... customer DTOs
    │   ├── CreateOrderDto.cs
    │   ├── CreateOrderItemDto.cs
    │   ├── OrderResponseDto.cs
    │   └── OrderItemResponseDto.cs
    ├── Exceptions/
    │   └── ... business exceptions
    ├── Migrations/
    ├── Models/
    │   ├── Product.cs
    │   ├── InventoryMovement.cs
    │   ├── MovementType.cs
    │   ├── Customer.cs
    │   ├── Order.cs
    │   ├── OrderItem.cs
    │   └── OrderStatus.cs
    ├── Services/
    │   ├── ProductService.cs
    │   ├── InventoryService.cs
    │   ├── CustomerService.cs
    │   └── OrderService.cs
    ├── Program.cs
    └── appsettings.json
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL
- EF Core CLI (`dotnet-ef`)

### 1. Clone the repository

```bash
git clone <repository-url>
cd StockFlow
```

Replace `<repository-url>` with the repository's actual GitHub URL.

### 2. Configure PostgreSQL

Create a database named `stockflow`. Configure `ConnectionStrings:DefaultConnection` through local development configuration or .NET user secrets, and confirm that the key matches the one used by `Program.cs`.

Example configuration (**do not commit real credentials**):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=stockflow;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

### 3. Apply migrations

From the solution root:

```bash
dotnet ef database update --project StockFlow.Api --startup-project StockFlow.Api
```

### 4. Run the API

```bash
dotnet run --project StockFlow.Api
```

Use the local URL displayed in the terminal (for example, `http://localhost:5230`).

## Development Roadmap

### Backend Foundation and Products

- [x] ASP.NET Core controller/service architecture and dependency injection
- [x] PostgreSQL integration and EF Core migrations
- [x] Product CRUD, DTOs, validation, and unique SKU enforcement
- [x] Product search, price filtering, and pagination
- [x] Database constraints and price precision
- [x] Restrict direct stock changes through product CRUD

### Inventory Management

- [x] Inventory movement model and history
- [x] Stock-In and Stock-Out endpoints
- [x] Stock limits and insufficient-stock validation
- [x] Explicit transactions and PostgreSQL row-level locking
- [x] Manual concurrent Stock-Out test
- [ ] Automated inventory integration/concurrency tests

### Customers and Orders

- [x] Customer management and unique email enforcement
- [x] Order and order-item schema, relationships, and migrations
- [x] Pending order creation, item-price capture, and total calculation
- [x] Transactional order confirmation and inventory deductions
- [x] Order confirmation movements and duplicate-confirmation protection
- [x] Manual insufficient-stock, rollback, and concurrency tests
- [ ] Retrieve individual orders
- [ ] List/filter/paginate orders
- [ ] Order cancellation and inventory-restoration policy

### Security and Quality

- [ ] Automated unit and integration tests (including concurrency scenarios)
- [ ] Authentication and JWT authorization
- [ ] Role-based access control
- [ ] Additional API hardening and documentation

### React + TypeScript Frontend

- [ ] React + TypeScript application setup
- [ ] Product management interface
- [ ] Inventory management and movement history
- [ ] Customer and order management interfaces
- [ ] Dashboard with inventory and order summaries

### Deployment and Portfolio

- [ ] Docker containerization
- [ ] CI/CD pipeline
- [ ] Cloud deployment
- [ ] Final README refinements, API documentation, and portfolio screenshots

**Possible future extension:** Supplier management (not part of the currently completed modules).

## Current Development Priority

**Next milestone: Order retrieval and cancellation.** After completing the remaining order workflows, the focus will shift to automated testing, authentication, the React + TypeScript frontend, and deployment.
