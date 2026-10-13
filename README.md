# StockFlow

StockFlow is an **Order and Inventory Management REST API** built with **C#, ASP.NET Core (.NET 10), Entity Framework Core, and PostgreSQL**.

The project is designed to manage products, inventory, suppliers, customers, and orders while demonstrating practical backend development concepts such as RESTful API design, layered architecture, dependency injection, database persistence, input validation, business logic, and data integrity.

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
- Return appropriate HTTP status codes and error responses

### Inventory Management

- Increase available product stock through Stock-In operations
- Decrease available product stock through Stock-Out operations
- Prevent stock-out operations when insufficient stock is available
- Record successful inventory movements with product ID, movement type, quantity, reason, and timestamp
- Maintain stock movement records in PostgreSQL
- Associate inventory movements with products through a one-to-many relationship
- Validate stock movement requests using Data Annotations
- Return inventory movement response DTOs without exposing EF Core navigation properties
- Save stock updates and movement records atomically using EF Core's transactional `SaveChangesAsync()` behavior
- Return appropriate HTTP responses for successful operations, missing products, invalid requests, and insufficient stock

### Database and Data Integrity

- PostgreSQL database persistence using Entity Framework Core
- Code-first database migrations
- Unique database index on product SKU
- Maximum product name length of 100 characters
- Maximum SKU length of 50 characters
- Database CHECK constraints preventing negative prices and stock quantities
- Database CHECK constraint requiring positive inventory movement quantities
- Foreign key relationship between inventory movements and products
- Restrictive delete behavior to preserve inventory movement history
- Database-level handling of unique SKU violations

### Application Architecture

- Controller-based REST API
- Service layer for business logic and data access
- Dependency injection
- Data Transfer Objects (DTOs) for request validation and inventory responses
- Asynchronous database operations
- Reusable generic pagination response using `PagedResult<T>`
- Centralized database configuration through `StockFlowDbContext`
- Custom exceptions for duplicate SKUs and insufficient stock
- EF Core Change Tracking for updating existing entities

## Tech Stack

| Technology | Purpose |
|---|---|
| C# | Programming language |
| .NET 10 | Application framework |
| ASP.NET Core Web API | REST API development |
| Entity Framework Core | Object-relational mapping and migrations |
| PostgreSQL | Relational database |
| Npgsql | PostgreSQL provider for EF Core |
| Git and GitHub | Version control |

## API Endpoints

### Products

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/products` | Retrieve products with optional filtering and pagination |
| GET | `/api/products/{id}` | Retrieve a product by ID |
| POST | `/api/products` | Create a product |
| PUT | `/api/products/{id}` | Update a product |
| DELETE | `/api/products/{id}` | Delete a product |

### Inventory

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/inventory/stock-in` | Increase product stock and record an inventory movement |
| POST | `/api/inventory/stock-out` | Decrease product stock and record an inventory movement |

### Search, Filtering, and Pagination

The GET products endpoint supports the following optional query parameters:

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
      "name": "Updated Keyboard",
      "sku": "KEY-LOG-MX",
      "price": 1600,
      "quantityInStock": 18
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

### Stock-In

**POST** `/api/inventory/stock-in`

Increases the available quantity of a product and records the stock movement.

Example request:

```json
{
  "productId": 3,
  "quantity": 15,
  "reason": "New shipment received"
}
```

Example response:

```json
{
  "id": 1,
  "productId": 3,
  "type": 0,
  "quantity": 15,
  "reason": "New shipment received",
  "createdAt": "2026-10-13T02:42:04.5769783Z"
}
```

### Stock-Out

**POST** `/api/inventory/stock-out`

Decreases the available quantity of a product and records the stock movement. Requests exceeding available stock are rejected.

Example request:

```json
{
  "productId": 3,
  "quantity": 5,
  "reason": "Customer purchase"
}
```

Example response:

```json
{
  "id": 2,
  "productId": 3,
  "type": 1,
  "quantity": 5,
  "reason": "Customer purchase",
  "createdAt": "2026-10-13T19:10:10.6308567Z"
}
```

**Movement Types:**

- `0` — StockIn
- `1` — StockOut

Inventory movements are stored in PostgreSQL to preserve a record of stock changes.

## Validation and Error Handling

StockFlow validates requests before performing database operations.

| HTTP Status | Meaning |
|---|---|
| `200 OK` | Successful retrieval, deletion, or inventory movement |
| `201 Created` | Product created successfully |
| `204 No Content` | Product updated successfully |
| `400 Bad Request` | Invalid request data or query parameters |
| `404 Not Found` | Requested product does not exist |
| `409 Conflict` | Duplicate product SKU or insufficient stock |

### Product Validation

- Required product name and SKU
- Maximum name and SKU lengths
- Non-negative price and stock quantity
- Unique product SKUs
- Valid price filtering ranges
- Valid pagination parameters

### Inventory Validation

- Positive product ID
- Positive movement quantity
- Required movement reason with a maximum length of 200 characters
- Product existence verification
- Insufficient-stock validation before stock deduction
- Database constraints preventing zero or negative movement quantities

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
    │   └── InsufficientStockException.cs
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

Configure the `ConnectionStrings:DefaultConnection` setting through local development configuration or .NET user secrets. Use the connection-string key configured in `Program.cs`.

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

### Inventory Management

- [x] InventoryMovement entity and MovementType enum
- [x] Product-to-inventory movement relationship
- [x] Inventory database migration
- [x] Stock-In endpoint
- [x] Stock-Out endpoint
- [x] Insufficient-stock validation
- [x] Inventory movement persistence
- [x] Inventory request and response DTOs
- [ ] Inventory movement history endpoint
- [ ] Stock limits and overflow protection
- [ ] Concurrency protection for simultaneous inventory updates
- [ ] Restrict direct stock quantity changes through Product CRUD

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

The **Product Management module** is implemented, including CRUD operations, PostgreSQL persistence, request validation, database constraints, search, filtering, and pagination.

The **Inventory Management module** is partially implemented. Stock-In and Stock-Out operations are functional and have been tested against PostgreSQL. Successful operations update product quantities and persist corresponding inventory movement records. Insufficient-stock requests are rejected with an HTTP `409 Conflict` response.

The next development steps focus on inventory movement history, stock consistency, concurrency protection, and automated testing.

Subsequent phases will introduce supplier and customer management, order processing, authentication and authorization, a React frontend, and deployment.
