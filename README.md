# Excalibur Framework

**Excalibur** is a modular framework designed to simplify **Domain-Driven Design (DDD)** implementations. It provides tools and abstractions for building robust, scalable, and testable applications. It provides tools, abstractions, and helpers for efficient data integration, multi-database support, and hosting flexibility, whether as web services or background jobs.

## Key Features

### 1. Domain-Driven Design (DDD)

- **Aggregates and Repositories**:
  - Base classes for managing domain aggregates.
  - Extensible repositories for CRUD and custom operations.
- **Value Objects**:
  - Immutability and equality mechanisms to align with DDD principles.
- **Query Providers**:
  - Abstractions for encapsulating domain-specific SQL logic into reusable and testable components.

### 2. Database Abstractions

- **Data Queries**:
  - Encapsulation of SQL queries into modular `DataQuery<T>` classes.
  - Separation of SQL logic from business logic for enhanced testability.
- **Multi-Database Support**:
  - Out-of-the-box support for PostgreSQL and SQL Server.
  - Easily extensible for other databases by implementing query providers.
- **Query Providers**:
  - Interfaces and database-specific implementations (e.g., PostgreSQL, SQL Server).
  - Abstract SQL queries into reusable, testable components.

### 3. Data Integration

- Synchronization helpers for integrating external APIs with your database.
- Structured logging and caching mechanisms for optimized data handling.

### 4. Hosting Flexibility

- Helpers for deploying services as:
  - **Web Services**: Build RESTful APIs or GraphQL endpoints using ASP.NET Core.
  - **Job Services**: Background job support for tasks such as syncing or processing data.

### 5. Multi-Database Support

- Support for multiple database types (PostgreSQL, SQL Server).
- Plug-and-play query providers for easy extensibility.

## Installation and Setup

### Prerequisites

- **.NET SDK 8.0 or higher**: [Download .NET SDK](https://dotnet.microsoft.com/download)
- **Database**: PostgreSQL or SQL Server.
- **NuGet Packages**:
  - [Dapper](https://www.nuget.org/packages/Dapper)
  - [Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection)
  - [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json)

## Hosting Options

### Web Services

1. Configure the framework in an ASP.NET Core application.

### Job Services

1. Implement background jobs for syncing or processing data.

## Testing

   1. **Unit Tests**:
      - Use `xUnit` with `shouldly` for assertions and `FakeItEasy` to mock dependencies in clear well defined tests.
      - Focus on services and repositories in isolation.
   2. **Integration Tests**:
      - Test `DataQuery` classes with real databases (PostgreSQL or SQL Server).
      - Validate query provider correctness.
   3. **Functional Tests**:
      - Simulate end-to-end flows for APIs or job services.

------

## Contributing

We welcome contributions! Check out our [Contributing Guidelines](CONTRIBUTING.md) to get started.
