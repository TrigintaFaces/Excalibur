# Excalibur Framework
<!-- badges -->
[![github-release-badge]][github-release]
[![github-release-date-badge]][github-release-date]
[![github-downloads-badge]][github-downloads]
<!-- badges -->

**Excalibur** is a modular framework designed to simplify **Domain-Driven Design (DDD)** implementations. It provides tools and abstractions for building robust, scalable, and testable applications. It provides tools, abstractions, and helpers for efficient data integration, multi-database support, and hosting flexibility, whether as web services or background jobs.

## Nuget Packages

<!-- nuget packages -->
| Package ID                           | Latest Version                    | Downloads                           |
| ------------------------------------ | --------------------------------- | ----------------------------------- |
| Excalibur.A3                         | [![nuget-p1-version]][nuget-p1]   | [![nuget-p1-downloads]][nuget-p1]   |
| Excalibur.A3.Postgres                | [![nuget-p2-version]][nuget-p2]   | [![nuget-p2-downloads]][nuget-p2]   |
| Excalibur.A3.SqlServer               | [![nuget-p3-version]][nuget-p3]   | [![nuget-p3-downloads]][nuget-p3]   |
| Excalibur.Application                | [![nuget-p4-version]][nuget-p4]   | [![nuget-p4-downloads]][nuget-p4]   |
| Excalibur.Core                       | [![nuget-p5-version]][nuget-p5]   | [![nuget-p5-downloads]][nuget-p5]   |
| Excalibur.Data                       | [![nuget-p6-version]][nuget-p6]   | [![nuget-p6-downloads]][nuget-p6]   |
| Excalibur.DataAccess                 | [![nuget-p7-version]][nuget-p7]   | [![nuget-p7-downloads]][nuget-p7]   |
| Excalibur.DataAccess.DataProcessing  | [![nuget-p8-version]][nuget-p8]   | [![nuget-p8-downloads]][nuget-p8]   |
| Excalibur.DataAccess.ElasticSearch   | [![nuget-p9-version]][nuget-p9]   | [![nuget-p9-downloads]][nuget-p9]   |
| Excalibur.DataAccess.SqlServer       | [![nuget-p10-version]][nuget-p10] | [![nuget-p10-downloads]][nuget-p10] |
| Excalibur.DataAccess.SqlServer.Cdc   | [![nuget-p11-version]][nuget-p11] | [![nuget-p11-downloads]][nuget-p11] |
| Excalibur.Domain                     | [![nuget-p12-version]][nuget-p12] | [![nuget-p12-downloads]][nuget-p12] |
| Excalibur.Hosting                    | [![nuget-p13-version]][nuget-p13] | [![nuget-p13-downloads]][nuget-p13] |
| Excalibur.Hosting.Jobs               | [![nuget-p14-version]][nuget-p14] | [![nuget-p14-downloads]][nuget-p14] |
| Excalibur.Hosting.Web                | [![nuget-p15-version]][nuget-p15] | [![nuget-p15-downloads]][nuget-p15] |
| Excalibur.Jobs                       | [![nuget-p16-version]][nuget-p16] | [![nuget-p16-downloads]][nuget-p16] |
| Excalibur.Jobs.Quartz.Cdc            | [![nuget-p17-version]][nuget-p17] | [![nuget-p17-downloads]][nuget-p17] |
| Excalibur.Jobs.Quartz.DataProcessing | [![nuget-p18-version]][nuget-p18] | [![nuget-p18-downloads]][nuget-p18] |
| Excalibur.Jobs.Quartz.Outbox         | [![nuget-p19-version]][nuget-p19] | [![nuget-p19-downloads]][nuget-p19] |
<!-- nuget packages -->

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

<!-- references -->
[github-release]: https://github.com/TrigintaFaces/Excalibur/releases/latest
[github-release-badge]: https://img.shields.io/github/v/release/TrigintaFaces/Excalibur?color=brightgreen&logo=github&style=flat-square "Latest Release"

[github-release-date]: https://github.com/TrigintaFaces/Excalibur/releases/latest
[github-release-date-badge]: https://img.shields.io/github/release-date/TrigintaFaces/Excalibur?style=flat-square "Release Date"

[github-downloads]: https://github.com/TrigintaFaces/Excalibur/releases/latest
[github-downloads-badge]: https://img.shields.io/github/downloads/TrigintaFaces/Excalibur/latest/total?logo=github&style=flat-square "Downloads"

[nuget-p1]: https://www.nuget.org/packages/Excalibur.A3/
[nuget-p1-version]: https://img.shields.io/nuget/v/Excalibur.A3.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p1-downloads]: https://img.shields.io/nuget/dt/Excalibur.A3.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p2]: https://www.nuget.org/packages/Excalibur.A3.Postgres/
[nuget-p2-version]: https://img.shields.io/nuget/v/Excalibur.A3.Postgres.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p2-downloads]: https://img.shields.io/nuget/dt/Excalibur.A3.Postgres.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p3]: https://www.nuget.org/packages/Excalibur.A3.SqlServer/
[nuget-p3-version]: https://img.shields.io/nuget/v/Excalibur.A3.SqlServer.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p3-downloads]: https://img.shields.io/nuget/dt/Excalibur.A3.SqlServer.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p4]: https://www.nuget.org/packages/Excalibur.Application/
[nuget-p4-version]: https://img.shields.io/nuget/v/Excalibur.Application.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p4-downloads]: https://img.shields.io/nuget/dt/Excalibur.Application.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p5]: https://www.nuget.org/packages/Excalibur.Core/
[nuget-p5-version]: https://img.shields.io/nuget/v/Excalibur.Core.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p5-downloads]: https://img.shields.io/nuget/dt/Excalibur.Core.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p6]: https://www.nuget.org/packages/Excalibur.Data/
[nuget-p6-version]: https://img.shields.io/nuget/v/Excalibur.Data.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p6-downloads]: https://img.shields.io/nuget/dt/Excalibur.Data.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p7]: https://www.nuget.org/packages/Excalibur.DataAccess/
[nuget-p7-version]: https://img.shields.io/nuget/v/Excalibur.DataAccess.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p7-downloads]: https://img.shields.io/nuget/dt/Excalibur.DataAccess.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p8]: https://www.nuget.org/packages/Excalibur.DataAccess.DataProcessing/
[nuget-p8-version]: https://img.shields.io/nuget/v/Excalibur.DataAccess.DataProcessing.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p8-downloads]: https://img.shields.io/nuget/dt/Excalibur.DataAccess.DataProcessing.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p9]: https://www.nuget.org/packages/Excalibur.DataAccess.ElasticSearch/
[nuget-p9-version]: https://img.shields.io/nuget/v/Excalibur.DataAccess.ElasticSearch.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p9-downloads]: https://img.shields.io/nuget/dt/Excalibur.DataAccess.ElasticSearch.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p10]: https://www.nuget.org/packages/Excalibur.DataAccess.SqlServer/
[nuget-p10-version]: https://img.shields.io/nuget/v/Excalibur.DataAccess.SqlServer.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p10-downloads]: https://img.shields.io/nuget/dt/Excalibur.DataAccess.SqlServer.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p11]: https://www.nuget.org/packages/Excalibur.DataAccess.SqlServer.Cdc/
[nuget-p11-version]: https://img.shields.io/nuget/v/Excalibur.DataAccess.SqlServer.Cdc.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p11-downloads]: https://img.shields.io/nuget/dt/Excalibur.DataAccess.SqlServer.Cdc.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p12]: https://www.nuget.org/packages/Excalibur.Domain/
[nuget-p12-version]: https://img.shields.io/nuget/v/Excalibur.Domain.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p12-downloads]: https://img.shields.io/nuget/dt/Excalibur.Domain.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p13]: https://www.nuget.org/packages/Excalibur.Hosting/
[nuget-p13-version]: https://img.shields.io/nuget/v/Excalibur.Hosting.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p13-downloads]: https://img.shields.io/nuget/dt/Excalibur.Hosting.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p14]: https://www.nuget.org/packages/Excalibur.Hosting.Jobs/
[nuget-p14-version]: https://img.shields.io/nuget/v/Excalibur.Hosting.Jobs.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p14-downloads]: https://img.shields.io/nuget/dt/Excalibur.Hosting.Jobs.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p15]: https://www.nuget.org/packages/Excalibur.Hosting.Web/
[nuget-p15-version]: https://img.shields.io/nuget/v/Excalibur.Hosting.Web.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p15-downloads]: https://img.shields.io/nuget/dt/Excalibur.Hosting.Web.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p16]: https://www.nuget.org/packages/Excalibur.Jobs/
[nuget-p16-version]: https://img.shields.io/nuget/v/Excalibur.Jobs.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p16-downloads]: https://img.shields.io/nuget/dt/Excalibur.Jobs.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p17]: https://www.nuget.org/packages/Excalibur.Jobs.Quartz.Cdc/
[nuget-p17-version]: https://img.shields.io/nuget/v/Excalibur.Jobs.Quartz.Cdc.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p17-downloads]: https://img.shields.io/nuget/dt/Excalibur.Jobs.Quartz.Cdc.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p18]: https://www.nuget.org/packages/Excalibur.Jobs.Quartz.DataProcessing/
[nuget-p18-version]: https://img.shields.io/nuget/v/Excalibur.Jobs.Quartz.DataProcessing.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p18-downloads]: https://img.shields.io/nuget/dt/Excalibur.Jobs.Quartz.DataProcessing.svg?logo=nuget&style=flat-square "NuGet Downloads"

[nuget-p19]: https://www.nuget.org/packages/Excalibur.Jobs.Quartz.Outbox/
[nuget-p19-version]: https://img.shields.io/nuget/v/Excalibur.Jobs.Quartz.Outbox.svg?logo=nuget&style=flat-square "NuGet Version"
[nuget-p19-downloads]: https://img.shields.io/nuget/dt/Excalibur.Jobs.Quartz.Outbox.svg?logo=nuget&style=flat-square "NuGet Downloads"
<!-- references -->
