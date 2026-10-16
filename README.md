# StockFlow

StockFlow is an **Order and Inventory Management REST API** built with **C#, ASP.NET Core (.NET 10), Entity Framework Core, and PostgreSQL**.

The project is designed to manage products, inventory, suppliers, customers, and orders while demonstrating practical backend development concepts such as RESTful API design, layered architecture, dependency injection, database persistence, input validation, business logic, data integrity, transactions, and concurrency control.

**Project Status:** Active Development

## Features

### Product Management

- Create, retrieve, update, and delete products
- Retrieve individual products by ID
- Search products by name (case-insensitive)
- Filter products by minimum and maximum price
- Combine search and price filters
- Paginate product results with configurable page number and page size
- Return pagination metadata including total count and total pages
- Validate incoming requests using Data Annotations
- Validate price ranges using `IValidatableObject`
- Prevent duplicate product SKUs
- Initialize new products with zero stock
- Restrict product updates to name, SKU, and price
- Require inventory operations for stock quantity changes
- Return appropriate HTTP status codes and error responses

### Inventory Management

- Increase available product stock through Stock-In operations
- Decrease available product stock through Stock-Out operations
- Prevent Stock-Out when insufficient inventory is available
- Enforce a maximum stock quantity of **100,000 units per product**
- Prevent integer overflow during Stock-In by validating available capacity before addition
- Reject Stock-In requests that would exceed the maximum stock limit
- Record successful inventory movements with product ID, movement type, quantity, reason, and timestamp
- Maintain inventory movement history in PostgreSQL
- Retrieve product-specific movement history in reverse chronological order
- Return an empty history for existing products without movements
- Return `404 Not Found` when requesting history for a nonexistent product
- Associate inventory movements with products through a foreign key relationship
- Validate stock movement requests using Data Annotations
- Return inventory movement response DTOs without exposing EF Core navigation properties
- Use explicit database transactions for stock updates and movement creation
- Use PostgreSQL row-level locking to coordinate concurrent inventory operations
- Return `409 Conflict` for insufficient stock or exceeded stock limits

### Concurrency and Transaction Safety

StockFlow uses PostgreSQL transactions and row-level locking to protect inventory consistency during simultaneous requests.

Both Stock-In and Stock-Out operations follow this sequence:

1. Begin a database transaction.
2. Retrieve and lock the product using `SELECT ... FOR UPDATE`.
3. Validate the requested stock operation.
4. Update the product's available quantity.
5. Create the corresponding inventory movement.
6. Persist changes using EF Core.
7. Commit the transaction.

This prevents concurrent inventory operations using these service methods from independently modifying the same product based on stale quantities.

If validation fails, the uncommitted transaction is rolled back when disposed.

**Concurrency test performed:**

A test product was initialized with five units. Two independent PowerShell jobs attempted to remove four units each.

Observed results:

- One Stock-Out request succeeded with HTTP `200 OK`.
- The other request was rejected with HTTP `409 Conflict`.
- Only one Stock-Out movement was recorded.
- The product's final stock quantity was one unit.

This manual integration test passed. Automated tests that deliberately create concurrent database contention are planned.

### Database and Data Integrity

- PostgreSQL persistence using Entity Framework Core
- Code-first database migrations
- Unique database index on product SKU
- Maximum product name length of 100 characters
- Maximum SKU length of 50 characters
- Database CHECK constraint preventing negative product prices
- Database CHECK constraint enforcing product stock between 0 and 100,000 units
- Database CHECK constraint requiring positive inventory movement quantities
- Foreign key relationship between inventory movements and products
- Restrictive delete behavior to preserve inventory movement history
- Database-level handling of unique SKU violations
- Explicit transactions and PostgreSQL row-level locking for inventory operations

### Application Architecture

- Controller-based REST API
- Service layer for business logic and data access
- Dependency injection
- Data Transfer Objects (DTOs) for request validation and inventory responses
- Asynchronous database operations
- LINQ queries for filtering, sorting, and DTO projection
- Reusable generic pagination response using `PagedResult<T>`
- Centralized database configuration through `StockFlowDbContext`
- Custom exceptions for duplicate SKUs, insufficient stock, and exceeded stock limits
- EF Core change tracking for updating existing entities
- Explicit transaction management using EF Core

## Tech Stack

| Technology | Purpose |
|---|---|
| C# | Programming language |
| .NET 10 | Application framework |
| ASP.NET Core Web API | REST API development |
| Entity Framework Core | ORM, database migrations, and transactions |
| PostgreSQL | Relational database and concurrency control |
| Npgsql | PostgreSQL provider for EF Core |
| Git and GitHub | Version control |
| PowerShell | Manual API and concurrency testing |

## API Endpoints

### Products

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/products` | Retrieve products with optional filtering and pagination |
| GET | `/api/products/{id}` | Retrieve a product by ID |
| POST | `/api/products` | Create a product with zero initial stock |
| PUT | `/api/products/{id}` | Update product name, SKU, and price |
| DELETE | `/api/products/{id}` | Delete a product |

Product creation and update requests do not accept `quantityInStock` as a writable DTO property. Stock quantities are managed through the inventory endpoints.

### Inventory

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/inventory/stock-in` | Increase product stock and record a movement |
| POST | `/api/inventory/stock-out` | Decrease product stock and record a movement |
| GET | `/api/inventory/products/{productId}/history` | Retrieve movement history for a product |

### Search, Filtering, and Pagination

The GET products endpoint supports these optional query parameters:

| Parameter | Description | Default |
|---|---|---|
| `search` | Case-insensitive product name search | None |
| `minPrice` | Minimum product price | None |
| `maxPrice` | Maximum product price | None |
| `page` | Page number | 1 |
| `pageSize` | Number of products per page (maximum 100) | 10 |

Example requests:

```http
GET /api/products?search=keyboard
GET /api/products?minPrice=500&maxPrice=2000
GET /api/products?page=1&pageSize=10
GET /api/products?search=keyboard&minPrice=1000&maxPrice=2000&page=1&pageSize=5
```

### Example Paginated Response

```json
{
  "items": [
    {
      "id": 3,
      "name": "Logitech Keyboard MX",
      "sku": "KEY-LOG-MX",
      "price": 1750,
      "quantityInStock": 18
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

### Create Product

**POST** `/api/products`

New products are created with zero stock. Inventory is subsequently received using Stock-In.

Example request:

```json
{
  "name": "USB-C Hub",
  "sku": "HUB-USB-C-001",
  "price": 450
}
```

Example response:

```json
{
  "id": 12,
  "name": "USB-C Hub",
  "sku": "HUB-USB-C-001",
  "price": 450,
  "quantityInStock": 0
}
```

**Success:** `201 Created`

### Stock-In

**POST** `/api/inventory/stock-in`

Increases the available quantity of a product and records the stock movement.

Example request:

```json
{
  "productId": 12,
  "quantity": 10,
  "reason": "Initial stock received"
}
```

Example response:

```json
{
  "id": 3,
  "productId": 12,
  "type": 0,
  "quantity": 10,
  "reason": "Initial stock received",
  "createdAt": "2026-10-15T21:00:02.568857Z"
}
```

The resulting stock must not exceed 100,000 units.

**Success:** `200 OK`

**Stock limit exceeded:** `409 Conflict`

### Stock-Out

**POST** `/api/inventory/stock-out`

Decreases the available quantity of a product and records the stock movement. Requests exceeding available stock are rejected.

Example request:

```json
{
  "productId": 13,
  "quantity": 4,
  "reason": "Concurrent stock-out test"
}
```

Example response:

```json
{
  "id": 7,
  "productId": 13,
  "type": 1,
  "quantity": 4,
  "reason": "Concurrent stock-out test",
  "createdAt": "2026-10-16T15:40:45.44423Z"
}
```

**Success:** `200 OK`

**Insufficient stock:** `409 Conflict`

### Inventory Movement History

**GET** `/api/inventory/products/{productId}/history`

Retrieves inventory movements for a specific product, ordered by the most recent movement first.

Example request:

```http
GET /api/inventory/products/13/history
```

Example response:

```json
[
  {
    "id": 7,
    "productId": 13,
    "type": 1,
    "quantity": 4,
    "reason": "Concurrent stock-out test",
    "createdAt": "2026-10-16T15:40:45.44423Z"
  },
  {
    "id": 6,
    "productId": 13,
    "type": 0,
    "quantity": 5,
    "reason": "Initial concurrency test stock",
    "createdAt": "2026-10-16T15:37:56.869308Z"
  }
]
```

**Response behavior:**

- `200 OK` — Product exists and movement history is returned
- `200 OK` with `[]` — Product exists but has no recorded movements
- `404 Not Found` — Product does not exist

**Movement types:**

- `0` — StockIn
- `1` — StockOut

## Validation and Error Handling

StockFlow validates incoming requests and applies business rules before committing database changes.

| HTTP Status | Meaning |
|---|---|
| `200 OK` | Successful retrieval, deletion, or inventory movement |
| `201 Created` | Product created successfully |
| `204 No Content` | Product updated successfully |
| `400 Bad Request` | Invalid request data or query parameters |
| `404 Not Found` | Requested product does not exist |
| `409 Conflict` | Duplicate SKU, insufficient stock, or stock limit exceeded |

### Product Validation

- Required product name and SKU
- Maximum name and SKU lengths
- Non-negative price
- Unique product SKUs
- Valid price filtering ranges
- Valid pagination parameters
- Initial stock quantity set to zero
- No direct stock quantity changes through product update requests

### Inventory Validation

- Positive product ID
- Positive movement quantity
- Required movement reason with a maximum length of 200 characters
- Product existence verification
- Insufficient-stock validation before deduction
- Maximum stock limit of 100,000 units
- Safe capacity calculation before increasing stock
- Database constraints preventing invalid stock quantities
- Database constraints preventing zero or negative movement quantities
- Row-level locking during stock changes

Database constraints provide an additional layer of protection, including when data is modified outside the API.

## Project Structure

```text
StockFlow/
├── StockFlow.slnx
└── StockFlow.Api/
    ├── Common/
    │   └── PagedResult.cs
    ├── Controllers/
    │   ├── ProductsController.cs
    │   └── InventoryController.cs
    ├── Data/
    │   └── StockFlowDbContext.cs
    ├── DTOs/
    │   ├── CreateProductDto.cs
    │   ├── UpdateProductDto.cs
    │   ├── ProductQueryDto.cs
    │   ├── StockMovementDto.cs
    │   └── InventoryMovementResponseDto.cs
    ├── Exceptions/
    │   ├── DuplicateSkuException.cs
    │   ├── InsufficientStockException.cs
    │   └── StockLimitExceededException.cs
    ├── Migrations/
    ├── Models/
    │   ├── Product.cs
    │   ├── InventoryMovement.cs
    │   └── MovementType.cs
    ├── Services/
    │   ├── ProductService.cs
    │   └── InventoryService.cs
    ├── Program.cs
    └── appsettings.json
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL
- EF Core CLI tools (`dotnet-ef`)

### 1. Clone the Repository

```bash
git clone <repository-url>
cd StockFlow
```

Replace `<repository-url>` with the GitHub repository URL.

### 2. Configure PostgreSQL

Create a PostgreSQL database named `stockflow`.

Configure the `ConnectionStrings:DefaultConnection` setting through local development configuration or .NET user secrets. Confirm that the connection-string key matches the one used in `Program.cs`.

Example configuration:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=stockflow;Username=postgres;Password=YOUR_PASSWORD"
  }
}
```

Do not commit real database credentials to Git.

### 3. Apply Database Migrations

From the solution root:

```bash
dotnet ef database update --project StockFlow.Api --startup-project StockFlow.Api
```

### 4. Run the API

```bash
dotnet run --project StockFlow.Api
```

Use the local URL displayed in the terminal.

## Development Roadmap

### Product Management

- [x] Product CRUD endpoints
- [x] Controller and service architecture
- [x] PostgreSQL integration with EF Core
- [x] Database migrations
- [x] Request DTO validation
- [x] Unique SKU enforcement
- [x] Database integrity constraints
- [x] Product search and price filtering
- [x] Pagination and response metadata
- [x] Restrict direct stock quantity changes through Product CRUD

### Inventory Management

- [x] InventoryMovement entity and MovementType enum
- [x] Product-to-inventory movement relationship
- [x] Inventory database migration
- [x] Stock-In endpoint
- [x] Stock-Out endpoint
- [x] Insufficient-stock validation
- [x] Inventory movement persistence
- [x] Inventory request and response DTOs
- [x] Inventory movement history endpoint
- [x] Maximum stock limit and overflow-safe capacity validation
- [x] Database CHECK constraint enforcing stock limits
- [x] Explicit database transactions
- [x] PostgreSQL row-level locking for Stock-In and Stock-Out
- [x] Manual concurrent Stock-Out integration test
- [ ] Automated inventory concurrency and integration tests

### Order and Business Management

- [ ] Supplier management
- [ ] Customer management
- [ ] Order creation and processing
- [ ] Order items and total calculations
- [ ] Transactional order processing and inventory deduction

### Security and Testing

- [ ] Authentication and authorization
- [ ] Role-based access control
- [ ] Automated unit tests
- [ ] Integration tests
- [ ] API hardening and error handling improvements

### Frontend and Deployment

- [ ] React and TypeScript frontend
- [ ] Product management interface
- [ ] Inventory management interface
- [ ] Order management interface
- [ ] Dashboard
- [ ] Docker containerization
- [ ] CI/CD pipeline and deployment
- [ ] Final API documentation and portfolio screenshots

## Project Status

StockFlow is under active development.

The **Product Management module** is implemented, including CRUD operations, PostgreSQL persistence, request validation, database constraints, search, filtering, and pagination. Product creation initializes stock to zero, and product updates cannot directly change inventory quantities.

The **Inventory Management module** supports Stock-In, Stock-Out, and product-specific inventory movement history. Inventory operations enforce available stock and maximum stock limits, use explicit PostgreSQL transactions, and acquire row-level locks to coordinate concurrent modifications.

The database independently enforces stock quantity limits through CHECK constraints.

A manual concurrent Stock-Out test confirmed that two requests attempting to remove four units from a product containing five units resulted in one successful operation, one HTTP `409 Conflict`, and a final balance of one unit.

**The next development priority is Order Management**, including customers, order items, order totals, and transactional inventory deduction.

Subsequent phases will introduce supplier management, authentication and authorization, automated testing, a React and TypeScript frontend, Docker, CI/CD, and deployment.
