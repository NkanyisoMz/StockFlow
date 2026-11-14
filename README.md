# StockFlow

StockFlow is an Order and Inventory Management System under active development. Its backend is a REST API built with C#, ASP.NET Core (.NET 10), Entity Framework Core, and PostgreSQL.

The application manages products, inventory, customers, and orders. It demonstrates controller/service separation, dependency injection, relational modelling, input validation, database constraints, transactions, and concurrency control.

A React + TypeScript frontend is planned.

**Current status:** Core backend functionality and the baseline automated testing milestone are complete. The latest local test run passed all 81 integration test cases.

**Next milestone:** JWT authentication and role-based authorization.

## Features

### Product Management

- Create, retrieve, update, and delete products.
- Search product names without case sensitivity.
- Combine name search with minimum and maximum price filters.
- Paginate results with total-count and total-page metadata.
- Enforce unique SKUs.
- Limit product names to 100 characters and SKUs to 50 characters.
- Create products with zero stock.
- Update name, SKU, and price without directly changing stock.
- Validate prices as non-negative values with at most two decimal places,
  within the supported `numeric(18,2)` range.
- Reject deletion of products referenced by inventory movements or order
  items with `409 Conflict`.

### Inventory Management

- Receive stock through Stock-In and issue stock through Stock-Out.
- Prevent deductions exceeding the available balance.
- Enforce a maximum of 100,000 units per product.
- Record movement type, quantity, reason, product ID, and timestamp.
- Retrieve product-specific movement history, newest first.
- Return an empty history for existing products without movements.
- Coordinate stock changes using transactions and PostgreSQL row locks.

Movement types:

| Value | Meaning |
|---|---|
| `0` | StockIn |
| `1` | StockOut |

### Customer Management

- Create, list, retrieve, and update customers.
- Store customer name, email, and creation timestamp.
- Normalize emails to lowercase and enforce uniqueness.
- Validate required fields, field lengths, and email format.
- Associate orders with existing customers.

### Order Management

- Create pending orders containing one or more products.
- Validate customer and product references.
- Reject non-positive quantities and duplicate product IDs.
- Capture unit prices when orders are created.
- Calculate totals from quantities and captured prices.
- Retrieve individual orders with their items.
- Filter orders by customer and status.
- Paginate results and sort newest first, with descending order ID as
  the secondary sort key.
- Confirm pending orders and deduct inventory transactionally.
- Cancel pending orders without changing inventory.
- Reject confirmation or cancellation of orders that are no longer pending.
- Coordinate competing confirmations and cancellations through row locks.

Order statuses:

| Value | Meaning |
|---|---|
| `0` | Pending |
| `1` | Confirmed |
| `2` | Cancelled |

Creating an order does not reserve or deduct stock. Stock is deducted only
when confirmation succeeds.

## Architecture

StockFlow currently uses one API project and a separate integration-test
project.

The main request flow is:

```text
HTTP request
    → Controller
    → Service
    → StockFlowDbContext
    → PostgreSQL
    → HTTP response
```

- Controllers handle routes, request binding, and HTTP responses.
- DTOs define accepted inputs and selected response shapes.
- Services implement business rules and database operations.
- EF Core entities represent persisted data.
- StockFlowDbContext configures tables, relationships, and constraints.
- Custom exceptions represent business failures and are translated by
  controllers into HTTP responses.
- Migrations version the database schema.

Product and customer endpoints currently return entities. Order and
inventory endpoints use response DTOs.

## Transaction and Concurrency Handling

### Stock-In and Stock-Out

1. Begin a transaction.
2. Lock the product row using `SELECT ... FOR UPDATE`.
3. Validate available stock or remaining capacity.
4. Update the balance and insert an inventory movement.
5. Save changes and commit.

### Order Confirmation

1. Begin a transaction.
2. Lock the order row.
3. Require the order to be pending.
4. Load order items.
5. Lock distinct product rows in ascending product ID order.
6. Aggregate required quantities and check every product balance.
7. Deduct stock and create stock-out movements.
8. Mark the order confirmed.
9. Save changes and commit.

If an operation exits before commit, the uncommitted transaction is
rolled back on disposal.

### Order Cancellation

1. Begin a transaction.
2. Lock the order row.
3. Require the order to be pending.
4. Set its status to cancelled.
5. Save changes and commit.

Cancellation does not restore stock because pending orders have not
consumed or reserved inventory.

Confirmation and cancellation lock the same order row before checking
status. Inventory operations and confirmation lock product rows before
changing balances.

### Historical Manual Verification

Manual checks were performed using PowerShell HTTP requests and
PostgreSQL queries before the automated suite was introduced.

| Scenario | Observed result |
|---|---|
| Two stock-outs each requesting 4 units from 5 available | One success, one conflict; final stock 1; one deduction |
| Confirmation requesting 10 units from 9 available | Conflict; order remained pending; stock and movements unchanged |
| Two confirmations of the same order | One success, one conflict; stock deducted once |
| Two orders each requesting 5 units from 7 available | One confirmed, one pending; final stock 2 |
| Confirmation competing with cancellation | One transition succeeded; inventory matched the resulting status |
| Cancellation of a pending order | Cancelled status; no inventory change |
| Repeated cancellation or cancellation of a confirmed order | Conflict |
| Cancellation of a nonexistent order | Not found |

Automated concurrent-request coverage now complements these historical
checks. Its scope and limitations are described under Testing.

## Database and Data Integrity

- PostgreSQL persistence through EF Core and Npgsql.
- Code-first migrations.
- Unique indexes for product SKU and customer email.
- Foreign-key relationships between customers, orders, order items,
  products, and inventory movements.
- `numeric(18,2)` precision for product prices, order totals, and
  order-item unit prices.
- CHECK constraints for non-negative prices and totals, positive item
  and movement quantities, and stock between 0 and 100,000.
- Restricted deletion of referenced customer and product records.
- Order items cascade when their parent order is deleted at the
  database level.

Database constraints reinforce application validation, including when
writes bypass the API.

## Tech Stack

| Technology | Purpose |
|---|---|
| C# / .NET 10 | Backend language and runtime |
| ASP.NET Core Web API | Controllers and HTTP endpoints |
| Entity Framework Core | Persistence, migrations, and transactions |
| PostgreSQL | Relational storage, constraints, and row locks |
| Npgsql | EF Core PostgreSQL provider |
| xUnit | Automated test framework |
| WebApplicationFactory | In-process API integration-test host |
| Testcontainers | Disposable PostgreSQL test containers |
| Docker Desktop | Container runtime for local integration tests |
| Git / GitHub | Version control |
| PowerShell / psql | Manual API and database checks |
| React + TypeScript | Planned frontend |
| GitHub Actions | Planned CI/CD |

Docker is currently used for integration-test databases. Application
containerization and deployment are still planned.

## API Endpoints

### Products

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/products` | Filter and paginate products |
| GET | `/api/products/{id}` | Retrieve a product |
| POST | `/api/products` | Create a product with zero stock |
| PUT | `/api/products/{id}` | Update name, SKU, and price |
| DELETE | `/api/products/{id}` | Delete an unreferenced product |

Product query parameters:

| Parameter | Description | Default |
|---|---|---|
| `search` | Case-insensitive name search | None |
| `minPrice` | Inclusive minimum price | None |
| `maxPrice` | Inclusive maximum price | None |
| `page` | Page number, minimum 1 | 1 |
| `pageSize` | Items per page, from 1 to 100 | 10 |

Price filters support values from 0 through 9,999,999,999,999,999.99.
The minimum cannot exceed the maximum.

```http
GET /api/products?search=keyboard
GET /api/products?minPrice=500&maxPrice=2000
GET /api/products?search=keyboard&minPrice=1000&maxPrice=2000&page=1&pageSize=5
```

Create-product body:

```json
{
  "name": "USB-C Hub",
  "sku": "HUB-USB-C-001",
  "price": 450.00
}
```

Success returns `201 Created`, including the generated ID and
`quantityInStock: 0`.

Example paginated response:

```json
{
  "items": [
    {
      "id": 3,
      "name": "Keyboard",
      "sku": "KEY-001",
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

### Inventory

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/inventory/stock-in` | Increase stock and record a movement |
| POST | `/api/inventory/stock-out` | Decrease stock and record a movement |
| GET | `/api/inventory/products/{productId}/history` | Retrieve movement history |

Stock-In body:

```json
{
  "productId": 12,
  "quantity": 10,
  "reason": "Initial stock received"
}
```

Stock-Out body:

```json
{
  "productId": 12,
  "quantity": 4,
  "reason": "Warehouse dispatch"
}
```

Successful movements return `200 OK` with the movement record.

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

Insufficient stock or exceeding capacity returns `409 Conflict`.

History returns movements newest first. An existing product without
movements returns `200 OK` with `[]`; a missing product returns `404`.

### Customers

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/customers` | Create a customer |
| GET | `/api/customers` | List customers |
| GET | `/api/customers/{id}` | Retrieve a customer |
| PUT | `/api/customers/{id}` | Update a customer |

Create and update body:

```json
{
  "name": "Test Customer",
  "email": "customer@example.com"
}
```

Creation returns `201 Created`; successful updates return `204 No Content`.
Duplicate emails return `409 Conflict`.

Names are limited to 100 characters and emails to 150 characters.
Email addresses must pass input-format validation.

### Orders

| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/orders` | Create a pending order |
| GET | `/api/orders` | Filter and paginate orders |
| GET | `/api/orders/{id}` | Retrieve an order with its items |
| POST | `/api/orders/{id}/confirm` | Confirm and deduct stock |
| POST | `/api/orders/{id}/cancel` | Cancel a pending order |

Create-order body:

```json
{
  "customerId": 1,
  "items": [
    { "productId": 3, "quantity": 1 },
    { "productId": 6, "quantity": 2 }
  ]
}
```

Creation returns `201 Created`.

Example order response:

```json
{
  "id": 4,
  "customerId": 1,
  "orderDate": "2026-10-22T22:47:39.995729Z",
  "status": 0,
  "totalAmount": 3349.98,
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
      "quantity": 2,
      "unitPrice": 799.99,
      "lineTotal": 1599.98
    }
  ]
}
```

Order query parameters:

| Parameter | Description | Default |
|---|---|---|
| `customerId` | Filter by a positive customer ID | None |
| `status` | Filter by `0`, `1`, or `2` | None |
| `page` | Page number, minimum 1 | 1 |
| `pageSize` | Items per page, from 1 to 100 | 10 |

```http
GET /api/orders?customerId=1
GET /api/orders?status=1
GET /api/orders?customerId=1&status=1&page=1&pageSize=10
```

Undefined numeric statuses and invalid query parameters return `400`.

Order lists use the same pagination envelope as product lists, with
order response objects in `items`.

Confirmation and cancellation return `200 OK` with the updated order.
Missing orders return `404`. Transitions from a non-pending status return
`409`. Insufficient stock during confirmation also returns `409`.

All IDs and response data in these examples are illustrative.

## Validation and Error Handling

| HTTP status | Typical meaning |
|---|---|
| `200 OK` | Successful retrieval, deletion, stock movement, or order transition |
| `201 Created` | Product, customer, or order created |
| `204 No Content` | Successful product or customer update |
| `400 Bad Request` | Invalid input or query parameters |
| `404 Not Found` | Requested resource or order reference does not exist |
| `409 Conflict` | Duplicate SKU/email, referenced-product deletion, stock violation, or invalid order transition |

Validation occurs through request DTOs and service rules, reinforced by
database constraints.

## Project Structure

The following highlights the main files and folders:

```text
StockFlow/
├── StockFlow.slnx
├── StockFlow.Api/
│   ├── Common/
│   │   └── PagedResult.cs
│   ├── Controllers/
│   ├── Data/
│   │   └── StockFlowDbContext.cs
│   ├── Dtos/
│   ├── Exceptions/
│   ├── Migrations/
│   ├── Models/
│   ├── Services/
│   ├── Product.cs
│   ├── Program.cs
│   ├── StockFlow.Api.csproj
│   ├── StockFlow.Api.http
│   └── appsettings.json
└── StockFlow.Api.IntegrationTests/
    ├── Infrastructure/
    │   ├── StockFlowApiFactory.cs
    │   ├── PostgreSqlFixture.cs
    │   └── TestData.cs
    ├── InfrastructureTests.cs
    ├── ProductInventoryTests.cs
    ├── ProductQueryTests.cs
    ├── CustomerTests.cs
    ├── InputValidationTests.cs
    ├── OrderTests.cs
    ├── OrderQueryTests.cs
    ├── InventoryConcurrencyTests.cs
    ├── OrderConcurrencyTests.cs
    └── DatabaseConstraintTests.cs
```

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL for local application development
- EF Core CLI (`dotnet-ef`) compatible with EF Core 10
- Docker Desktop with Linux containers for integration tests

### 1. Clone the Repository

```powershell
git clone https://github.com/NkanyisoMz/StockFlow.git
cd StockFlow
```

### 2. Configure the Development Database

Create a PostgreSQL database named `stockflow`.

Configure `ConnectionStrings:DefaultConnection` using .NET user secrets
or environment configuration. Do not commit real credentials.

Example user-secrets command; replace the placeholder password locally:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=stockflow;Username=postgres;Password=YOUR_PASSWORD" --project StockFlow.Api
```

### 3. Restore and Build

From the repository root:

```powershell
dotnet restore
dotnet build --no-restore
```

### 4. Apply Migrations

```powershell
dotnet ef database update --project StockFlow.Api --startup-project StockFlow.Api
```

### 5. Run the API

```powershell
dotnet run --project StockFlow.Api
```

Use the application URL printed in the terminal.

## Testing

The baseline integration-testing milestone is complete. The latest
local run passed all 81 test cases with no failures or skipped tests.

### Test Infrastructure

The suite uses:

- xUnit for test execution.
- WebApplicationFactory to host the real API in the test process.
- Testcontainers to start disposable PostgreSQL containers.
- EF Core migrations to create the actual database schema.

The fixture supplies the test connection string. Integration tests do
not require the development database or its credentials.

### Run the Suite

Ensure Docker Desktop is running with Linux containers:

```powershell
docker info --format '{{.OSType}}'
```

Expected output:

```text
linux
```

Run from the repository root:

```powershell
dotnet test StockFlow.Api.IntegrationTests
```

Alternatively, build first and run the compiled tests:

```powershell
dotnet build
dotnet test StockFlow.Api.IntegrationTests --no-build
```

The first run may take longer while packages and container images are
downloaded.

No manually created test container is required. The fixture starts
PostgreSQL, applies migrations, and removes the container during cleanup.

### Automated Coverage

| Area | Covered scenarios |
|---|---|
| Products | Creation, zero initial stock, updates preserving inventory, successful deletion, referenced-product deletion, duplicate SKU, and invalid prices |
| Customers | Email normalization and uniqueness, updates, update conflicts, required fields, email format, and length limits |
| Inventory | Successful receipt, insufficient-stock rejection, maximum-stock boundary, invalid quantities, and persisted movement history |
| Orders | Price capture, totals, unchanged stock on creation, confirmation, cancellation, invalid transitions, invalid items, and missing references |
| Queries | Combined filters, pagination metadata, result ordering, empty pages, and invalid parameters |
| Database constraints | Stock bounds, unique SKU, and product-reference deletion restrictions |
| Concurrent requests | Competing stock-outs, duplicate confirmation, competing orders, confirmation versus cancellation, and stock-out versus confirmation |

Tests check HTTP responses and persisted state where relevant.

### Business Rules Preserved

- Products start with zero stock.
- Product updates do not change stock.
- Stock remains between 0 and 100,000 units.
- Order items retain captured prices after product prices change.
- Pending orders do not reserve or deduct stock.
- Only pending orders can be confirmed or cancelled.
- Successful confirmation persists status, deductions, and movements together.
- Insufficient-stock confirmation leaves all product balances unchanged.
- Cancellation does not change inventory.
- Rejected transitions do not create additional deductions.

### Verified Edge Cases

| Scenario | Behavior |
|---|---|
| Delete a product referenced by inventory movements or order items | `409 Conflict`; product and references remain |
| Filter orders using an undefined numeric status | `400 Bad Request` |
| Filter products using prices above 1,000,000 | Accepted within the supported monetary range |
| Request a valid page beyond the available results | `200 OK`, empty items, and accurate pagination metadata |

### Isolation and Limitations

Each test class owns a disposable PostgreSQL container. Test methods
within a class share its database and create independent data using
unique values and resource-specific assertions.

Concurrent-request tests start competing requests before awaiting their
results. They verify consistent outcomes but do not force a specific
database interleaving or prove that lock waiting occurred.

Insufficient-stock tests verify that rejection preserves persisted
state. They do not inject a database failure after writes to independently
prove every rollback path.

The suite is baseline regression coverage, not exhaustive testing of
every endpoint, field combination, or failure mode. It currently
contains integration tests rather than a separate unit-test suite.

### Manual API Checks

Use `StockFlow.Api/StockFlow.Api.http` for the main workflow:

1. Create a product.
2. Receive stock.
3. Create a customer.
4. Create an order.
5. Confirm the order.
6. Check the saved order, stock balance, and movement history.

Separately, create and cancel a pending order and verify that inventory
does not change.

Replace example IDs with values returned by preceding requests.

## Development Roadmap

### Backend Foundation

- [x] Controller/service architecture and dependency injection
- [x] PostgreSQL persistence and EF Core migrations
- [x] Product management, filtering, and pagination
- [x] Inventory operations and movement history
- [x] Customer management and email uniqueness
- [x] Order creation and captured prices
- [x] Order retrieval, filtering, and pagination
- [x] Transactional confirmation and cancellation
- [x] Database constraints and row-level locking

### Baseline Automated Testing

- [x] xUnit integration-test project
- [x] Disposable PostgreSQL fixture and real migrations
- [x] Core API behavior and validation coverage
- [x] Regression tests for documented edge cases
- [x] Essential direct database-constraint checks
- [x] Concurrent-request outcome checks
- [x] Local suite passing with 81 test cases

### Authentication and Authorization — Next

- [ ] User-account and password management
- [ ] JWT authentication
- [ ] Role-based authorization
- [ ] Authentication and permission integration tests

Authentication and role-based access control are not implemented yet.

### React + TypeScript Frontend

- [ ] Application setup and login
- [ ] Product management
- [ ] Inventory operations and movement history
- [ ] Customer management
- [ ] Order creation, listing, details, confirmation, and cancellation

### Packaging and Deployment

- [ ] Application Docker images and local Compose setup
- [ ] GitHub Actions build and test pipeline
- [ ] Cloud deployment
- [ ] Deployment configuration, backups, and operational documentation
- [ ] Final API documentation and portfolio screenshots

Supplier management remains a possible future extension outside the
current scope.

## Current Development Priority

The baseline automated testing milestone is complete.

The next milestone is JWT authentication and role-based authorization.
Implementation will preserve the existing business rules, database
constraints, transaction boundaries, and row-locking behavior.

Subsequent milestones are the React + TypeScript frontend, application
containerization, GitHub Actions CI/CD, and cloud deployment.
