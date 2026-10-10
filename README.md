# StockFlow

StockFlow is an **Order and Inventory Management REST API** built with **C#, ASP.NET Core (.NET 10), Entity Framework Core, and PostgreSQL**.

The project is designed to manage products, inventory, suppliers, customers, and orders while demonstrating practical backend development concepts such as RESTful API design, layered architecture, dependency injection, database persistence, input validation, and data integrity.

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

### Database and Data Integrity

- PostgreSQL database persistence using Entity Framework Core
- Code-first database migrations
- Unique database index on product SKU
- Maximum product name length of 100 characters
- Maximum SKU length of 50 characters
- Database CHECK constraints preventing negative prices and stock quantities
- Database-level handling of unique SKU violations

### Application Architecture

- Controller-based REST API
- Service layer for business logic and data access
- Dependency injection
- Data Transfer Objects (DTOs) for request validation
- Asynchronous database operations
- Reusable generic pagination response using `PagedResult<T>`
- Centralized database configuration through `StockFlowDbContext`

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
      "quantityInStock": 8
    }
  ],
  "page": 1,
  "pageSize": 10,
  "totalCount": 1,
  "totalPages": 1
}
```

## Validation and Error Handling

StockFlow validates requests before performing database operations.

| HTTP Status | Meaning |
|---|---|
| `200 OK` | Successful retrieval or deletion |
| `201 Created` | Product created successfully |
| `204 No Content` | Product updated successfully |
| `400 Bad Request` | Invalid request data or query parameters |
| `404 Not Found` | Requested product does not exist |
| `409 Conflict` | Product SKU already exists |

Validation includes:

- Required product name and SKU
- Maximum name and SKU lengths
- Non-negative price and stock quantity
- Unique product SKUs
- Valid price filtering ranges
- Valid pagination parameters

Database constraints provide an additional layer of protection, including when data is modified outside the API.

## Project Structure

```text
StockFlow/
├── StockFlow.slnx
└── StockFlow.Api/
    ├── Common/
    │   └── PagedResult.cs
    ├── Controllers/
    │   └── ProductsController.cs
    ├── Data/
    │   └── StockFlowDbContext.cs
    ├── DTOs/
    │   ├── CreateProductDto.cs
    │   ├── UpdateProductDto.cs
    │   └── ProductQueryDto.cs
    ├── Exceptions/
    │   └── DuplicateSkuException.cs
    ├── Migrations/
    ├── Models/
    │   └── Product.cs
    ├── Services/
    │   └── ProductService.cs
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

Configure the `ConnectionStrings:DefaultConnection` setting through local development configuration or .NET user secrets.

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

- [x] Product CRUD endpoints
- [x] Controller and service architecture
- [x] PostgreSQL integration with EF Core
- [x] Database migrations
- [x] Request DTO validation
- [x] Unique SKU enforcement
- [x] Database integrity constraints
- [x] Product search and price filtering
- [x] Pagination and response metadata
- [ ] Inventory stock-in and stock-out operations
- [ ] Inventory movement history
- [ ] Supplier management
- [ ] Customer management
- [ ] Order creation and processing
- [ ] Transactional inventory updates
- [ ] Authentication and authorization
- [ ] Automated unit and integration tests
- [ ] Docker containerization
- [ ] CI/CD pipeline and deployment
- [ ] Final API documentation and portfolio screenshots

## Project Status

StockFlow is under active development.

The product management module is implemented, including persistent storage, validation, database constraints, search, filtering, and pagination.

The next development phase focuses on inventory management and stock movement tracking, followed by order processing, authentication, testing, and deployment.
