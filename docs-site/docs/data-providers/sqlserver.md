---
sidebar_position: 3
title: SQL Server
description: SQL Server data provider with Dapper-based data access, persistence, and resilience.
---

# SQL Server Provider

The SQL Server provider offers full relational database support with Dapper-based query execution, connection pooling, health checks, and built-in resilience.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A SQL Server instance (local, Azure SQL, or AWS RDS)
- Familiarity with [data access](../data-access/index.md) and [IDb interface](../data-access/idb-interface.md)

## Installation

```bash
dotnet add package Excalibur.Data.SqlServer
```

**Dependencies:** `Excalibur.Data.Abstractions`, `Microsoft.Data.SqlClient`, `Dapper`

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddSqlServerPersistence(opts =>
{
    opts.ConnectionString = "Server=localhost;Database=MyApp;Trusted_Connection=true;";
});
```

## Registration Methods

| Method | What It Registers | Key Options |
|--------|-------------------|-------------|
| `AddSqlServerPersistence(opts)` | Core data executors + persistence | `ConnectionString`, `CommandTimeout` |
| `AddSqlServerPersistenceWithRetry(opts)` | Persistence + automatic retry | `Resiliency.MaxRetryAttempts` |
| `AddSqlServerPersistenceWithEncryption(opts)` | Persistence + Always Encrypted | `Security.ColumnEncryptionSetting` |
| `AddSqlServerPersistenceReadOnly(opts)` | Read-only replica connection | `ConnectionString` |
| `AddSqlServerPersistenceHighAvailability(opts)` | Failover support | `ConnectionString` |
| `AddSqlServerDeadLetterStore(opts)` | `IDeadLetterStore` | `TableName` |
| `AddSqlServerProjectionStore<T>(opts)` | `IProjectionStore<T>` | `ConnectionString`, `TableName` |
| `AddSqlServerTransactionScope(...)` | Transaction scope | `IsolationLevel`, `Timeout` |

All methods also accept `IConfiguration` binding: `AddSqlServerPersistence(configuration, sectionName: "SqlServerPersistence")`.

### Batch Projection Registration

Register multiple projections sharing the same connection:

```csharp
services.AddSqlServerProjections(connectionString, projections =>
{
    projections.Add<OrderSummary>();
    projections.Add<CustomerProfile>(o => o.TableName = "CustomerViews");
    projections.Add<InventoryView>(o => o.TableName = "InventoryViews");
});
```

### Health Checks

```csharp
services.AddHealthChecks()
    .AddSqlServerPersistenceHealthCheck();
```

## Data Request Pattern

Define queries as reusable data request objects:

```csharp
public class GetOrderByIdRequest : DataRequest<Order?>
{
    public GetOrderByIdRequest(Guid orderId)
    {
        Command = new CommandDefinition(
            "SELECT * FROM Orders WHERE Id = @Id",
            new { Id = orderId });
    }
}

// Execute via persistence provider
var order = await provider.ExecuteAsync(new GetOrderByIdRequest(orderId), cancellationToken);
```

## Batch Operations

```csharp
var requests = new[]
{
    new InsertOrderRequest(order1),
    new InsertOrderRequest(order2),
    new InsertOrderRequest(order3)
};

await sqlProvider.ExecuteBatchInTransactionAsync(requests, scope, cancellationToken);
```

## Configuration

```json
{
  "SqlServerPersistence": {
    "ConnectionString": "Server=localhost;Database=MyApp;Trusted_Connection=true;",
    "CommandTimeout": 30,
    "EnableConnectionResiliency": true,
    "MaxRetryAttempts": 3,
    "RetryDelayMilliseconds": 1000,
    "EnableDetailedLogging": false,
    "EnableMetrics": true
  }
}
```

## Dead Letter Store

For messages that fail processing:

```csharp
services.AddSqlServerDeadLetterStore(options =>
{
    options.ConnectionString = connectionString;
    options.TableName = "DeadLetters";
});
```

## See Also

- [Data Providers Overview](./index.md) — Architecture and core abstractions
- [PostgreSQL Provider](./postgres.md) — Open-source SQL alternative
- [Event Sourcing](../event-sourcing/index.md) — SQL Server event store integration
