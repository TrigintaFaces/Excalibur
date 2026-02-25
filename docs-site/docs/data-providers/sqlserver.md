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

services.AddSqlServerPersistence("Server=localhost;Database=MyApp;Trusted_Connection=true;");
```

## Registration Options

### Basic Registration

```csharp
// From connection string
services.AddSqlServerPersistence("Server=...;Database=...;");

// From configuration
services.AddSqlServerPersistence(configuration, sectionName: "SqlServerPersistence");

// With options callback
services.AddSqlServerPersistence(options =>
{
    options.ConnectionString = "Server=...;Database=...;";
    options.CommandTimeout = 60;
    options.EnableConnectionResiliency = true;
});
```

### Specialized Registration

```csharp
// With automatic retry (Polly-based)
services.AddSqlServerPersistenceWithRetry(
    connectionString,
    maxRetryAttempts: 3,
    retryDelayMilliseconds: 1000);

// With Always Encrypted column support
services.AddSqlServerPersistenceWithEncryption(
    connectionString,
    SqlConnectionColumnEncryptionSetting.Enabled);

// Read-only replica connection
services.AddSqlServerPersistenceReadOnly(connectionString);

// High-availability with failover support
services.AddSqlServerPersistenceHighAvailability(connectionString);

// With Change Data Capture integration
services.AddSqlServerPersistenceWithCdc(configuration, typeof(Program).Assembly);
```

### Health Checks

```csharp
services.AddHealthChecks()
    .AddSqlServerPersistenceHealthCheck();
```

### Transaction Scope

```csharp
services.AddSqlServerTransactionScope(
    IsolationLevel.ReadCommitted,
    defaultTimeout: TimeSpan.FromSeconds(30));
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
services.AddSqlServerDeadLetterStore(connectionString);

// Or with options
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
