# StockFlow

StockFlow is an Order and Inventory Management REST API built with ASP.NET Core.

The project is being developed as a backend system for managing products, inventory, suppliers, customers, and orders while applying RESTful API design and backend development practices.

## Current Features

- Product model
- Create products
- Retrieve all products
- Retrieve a product by ID
- Update products
- Delete products
- RESTful HTTP status codes
- In-memory data storage

## Current API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/products` | Retrieve all products |
| GET | `/api/products/{id}` | Retrieve a product by ID |
| POST | `/api/products` | Create a product |
| PUT | `/api/products/{id}` | Update a product |
| DELETE | `/api/products/{id}` | Delete a product |

## Tech Stack

- C#
- .NET
- ASP.NET Core
- Minimal APIs

## Project Status

StockFlow is currently under active development.

The current implementation uses in-memory storage while the API fundamentals are being established. Persistence, application architecture, inventory management, order processing, authentication, testing, and deployment will be introduced as the project develops.
