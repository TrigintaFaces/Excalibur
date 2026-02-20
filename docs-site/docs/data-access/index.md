---
sidebar_position: 5
title: Data Access
description: Repository patterns and data access abstractions for Excalibur applications
---

# Data Access

Excalibur provides data access abstractions that work alongside event sourcing and domain modeling. These patterns use Dapper for all SQL operations, avoiding Entity Framework to keep dependencies minimal and performance optimal.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Data.Abstractions
  ```
- Familiarity with [dependency injection](../core-concepts/dependency-injection.md) and [domain modeling](../domain-modeling/index.md)

## In This Section

| Topic | Description |
|-------|-------------|
| [IDb Interface](idb-interface.md) | Database connection abstraction with Dapper |

## Key Abstractions

| Interface | Purpose |
|-----------|---------|
| `IDb` | Database connection abstraction |
| `IDataRequest` | Query abstraction for data access |
| `IUnitOfWork` | Transaction management |

## Choosing Between Direct Dapper and IDataRequest

Excalibur supports two approaches for data access:

| Approach | Best For |
|----------|----------|
| **Direct Dapper** | Simple queries, quick ad-hoc access, projections |
| **IDataRequest** | Complex queries, testability, retry policies, correlation tracking |

```csharp
// Direct Dapper: Simple and quick
var order = await _db.Connection.QuerySingleOrDefaultAsync<Order>(
    "SELECT * FROM Orders WHERE Id = @Id", new { Id = orderId });

// IDataRequest: Encapsulated and testable
var request = new GetOrderByIdRequest(orderId);
var order = await request.ResolveAsync(_db.Connection);
```

See [IDb Interface](idb-interface.md) for detailed examples of both patterns.

## Important Notes

- **No Entity Framework**: Excalibur uses Dapper for all SQL operations
- Data access is separate from event sourcing - use `IEventSourcedRepository<T>` for event-sourced aggregates
- `ResourceException` and `ConcurrencyException` are thrown for data access errors

## Related Documentation

- [Event Sourcing](/docs/event-sourcing/) - Event-sourced aggregate persistence
- [Domain Modeling](/docs/domain-modeling/) - Aggregate design

## See Also

- [IDb Interface](./idb-interface.md) — Database connection abstraction with Dapper integration and IDataRequest pattern
- [Data Providers Overview](../data-providers/index.md) — Unified data access layer with pluggable providers for SQL, NoSQL, and cloud databases
- [SQL Server Provider](../data-providers/sqlserver.md) — Enterprise SQL Server data provider implementation

