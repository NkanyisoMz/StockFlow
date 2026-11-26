# StockFlow

StockFlow is an Order and Inventory Management System under active development. Its backend is a REST API built with C#, ASP.NET Core (.NET 10), Entity Framework Core, and PostgreSQL.

The application manages products, inventory, customers, and orders. It demonstrates controller/service separation, dependency injection, relational modelling, input validation, database constraints, transactions, and concurrency control.

The React + TypeScript frontend supports internal product, inventory, customer, and order workflows.

**Current status:** Core backend and frontend workflows, authentication, and local Docker packaging are implemented. The latest local test run passed all 105 integration test cases.

**Next milestone:** GitHub Actions CI/CD and cloud deployment.

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
| React + TypeScript | Internal management frontend |
| GitHub Actions | Planned CI/CD |

Docker runs integration-test databases and the complete local application stack. Cloud deployment remains planned.

## Authentication and Authorization

StockFlow uses ASP.NET Core Identity for account storage and password
hashing, with short-lived JWT bearer tokens for API authentication.

Login users are separate from business customers.

### Permissions

| Operation | Staff | Admin |
|---|---|---|
| Read products, inventory, customers, and orders | Yes | Yes |
| Create and update customers | Yes | Yes |
| Receive and issue stock | Yes | Yes |
| Create, confirm, and cancel orders | Yes | Yes |
| Create, update, and delete products | No | Yes |

Business endpoints require an Admin or Staff role. Login is anonymous.

### Configuration

Non-secret JWT settings are in appsettings.json:

- Jwt:Issuer
- Jwt:Audience
- Jwt:ExpirationMinutes — currently 15

Configure Jwt:Key through development user secrets or deployment secret
configuration. Use a cryptographically random key and never commit it.

### Account Provisioning

Apply migrations before provisioning accounts.

Initialize roles:

```powershell
dotnet run --project StockFlow.Api -- --initialize-roles
```

For administrator creation, supply BootstrapAdmin:Email and
BootstrapAdmin:Password through configuration, then run:

```powershell
dotnet run --project StockFlow.Api -- --create-admin
```

For Staff creation, supply BootstrapStaff:Email and
BootstrapStaff:Password, then run:

```powershell
dotnet run --project StockFlow.Api -- --create-staff
```

Development user secrets are loaded in the Development environment.
Remove bootstrap credentials after successful setup.

Provisioning refuses to modify an existing account automatically.
If account creation succeeds but role assignment fails, inspect the
account before retrying.

### Login

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "YOUR_EMAIL",
  "password": "YOUR_PASSWORD"
}
```

Success returns accessToken and expiresAtUtc.

Send the token on business requests:

```http
Authorization: Bearer YOUR_ACCESS_TOKEN
```

Invalid credentials return 401. Invalid request fields return 400.
After five failed password attempts, the account is locked for five minutes.

Protected requests without a valid token return 401. Authenticated
callers without the required role receive 403.

### Scope and Limitations

This implementation is a closed-system learning design with locally
issued JWTs. Standards-based OAuth/OIDC issuance should be reviewed
before production deployment.

Public registration, refresh tokens, password recovery, and immediate
token revocation are not implemented.

Tokens normally remain valid until expiration even after an account's
password or roles change. Account lockout prevents new login; it does
not revoke an existing token.

JWT payloads are readable. Passwords and sensitive business information
must not be placed in tokens.

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
    ├── AuthorizationTests.cs
    ├── AuthTests.cs
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

## Run the Complete Application with Docker

### Configuration

Docker Desktop must be running with Linux containers. From the repository
root, copy `.env.example` to `.env` and replace both placeholders with
separate random values. In Git Bash, run `openssl rand -hex 32` twice.
Do not commit `.env`; verify with `git check-ignore .env`.

- `POSTGRES_PASSWORD`: container database password.
- `JWT_KEY`: API signing key, distinct from the database password.

### Build and Migrate

```bash
docker compose build
docker compose up -d db
docker compose run --rm api --migrate
```

The container database is separate from your original development database.
Local records and accounts are not copied automatically. No original data
is deleted by switching to Docker.

### Account Provisioning

Use an interactive PowerShell terminal from the repository root:

```powershell
$email = Read-Host "Administrator email"
$password = Read-Host "Administrator password" -AsSecureString
$credential = [PSCredential]::new($email, $password)
$env:BootstrapAdmin__Email = $email
$env:BootstrapAdmin__Password = $credential.GetNetworkCredential().Password
try {
    docker compose run --rm -e BootstrapAdmin__Email -e BootstrapAdmin__Password api --create-admin
}
finally {
    Remove-Item Env:BootstrapAdmin__Email -ErrorAction SilentlyContinue
    Remove-Item Env:BootstrapAdmin__Password -ErrorAction SilentlyContinue
    Remove-Variable email, password, credential
}
```

For Staff, use `BootstrapStaff__Email`, `BootstrapStaff__Password`, and
`--create-staff` in the same process, with a separate email. These commands
initialize roles and refuse to modify existing accounts automatically.
There are no default account passwords. Environment credentials can be
inspected while the one-off container exists; use deployment secret
management when hosting externally.

### Optional Demo Data

Stop application writes before running the explicitly enabled seed command:

```bash
docker compose stop web api
docker compose run --rm -e DemoData__Enabled=true api --seed-demo
```

It adds three products, two synthetic customers, three opening receipts,
and one pending order with captured prices. All inserts share one transaction.
Existing Identity accounts remain intact. If business records already exist,
seeding is skipped: no records are deleted, merged, or overwritten. Run only
one seed process at a time. Seeding never runs during normal startup.

### Start and Verify

```bash
docker compose up -d api web
docker compose ps
```

Open [StockFlow locally](http://localhost:8080). Nginx serves the compiled
frontend and forwards `/api` to the API; Vite is not used in these containers.
Only Nginx is published, bound to `127.0.0.1`; API and database ports remain internal.

Verify login for Admin and Staff, then create a product, receive stock,
create a customer/order, confirm it, and inspect the balance and history.

### Persistence and Maintenance

```bash
docker compose down
docker compose up -d
```

Records persist in the named PostgreSQL volume. Do not use
`docker compose down -v` unless intentionally deleting the container database.
Changing `POSTGRES_PASSWORD` in `.env` does not update an already initialized
database password.

After code changes:

```bash
docker compose build
docker compose run --rm api --migrate
docker compose up -d
```

For future schema changes, plan compatibility and stop application writes
when needed. Migrations run explicitly, not on every API startup.

Troubleshoot with:

```bash
docker compose logs --tail=100 api web
docker compose logs --tail=100 db
```

Do not publish credentials or tokens. `docker compose config` can expose
interpolated secrets. The development-only password-reset command is
unavailable in this stack's Production environment.

### Hosting Scope

This is a local HTTP stack with API HTTPS redirection explicitly disabled.
Cloud deployment still requires HTTPS, secret management, appropriate
trusted-proxy settings, backups, and a delivery workflow.

## Testing

The baseline integration-testing milestone is complete. The latest
local run passed all 101 test cases with no failures or skipped tests.

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
- [x] Local suite passing with 105 test cases

### Authentication and Authorization

- [x] User-account and password management
- [x] JWT authentication
- [x] Role-based authorization
- [x] Authentication and permission integration tests


### React + TypeScript Frontend

- [x] Application setup and login
- [x] Product management
- [x] Inventory operations and movement history
- [x] Customer management
- [x] Order creation, listing, details, confirmation, and cancellation

### Packaging and Deployment

- [x] Application Docker images and local Compose setup
- [ ] GitHub Actions build and test pipeline
- [ ] Cloud deployment
- [ ] Deployment configuration, backups, and operational documentation
- [ ] Final API documentation and portfolio screenshots

Supplier management remains a possible future extension outside the
current scope.

## Current Development Priority

Core frontend workflows, JWT authentication, integration tests, and local
Docker packaging are implemented. Remaining milestones are GitHub Actions
CI/CD, cloud deployment, and final operational documentation.

StockFlow remains an internal product, customer, inventory, and order
management system; storefronts and payments are outside its scope.
