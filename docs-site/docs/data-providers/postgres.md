---
sidebar_position: 4
title: PostgreSQL
description: PostgreSQL data provider with Npgsql-based data access, event sourcing, and CDC support.
---

# PostgreSQL Provider

The PostgreSQL provider offers full relational database support with Npgsql-based query execution, event sourcing integration, inbox/outbox patterns, and Change Data Capture.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A PostgreSQL instance (local or cloud-hosted)
- Familiarity with [data access](../data-access/index.md) and [IDb interface](../data-access/idb-interface.md)

## Installation

```bash
dotnet add package Excalibur.Data.Postgres
```

**Dependencies:** `Excalibur.Data.Abstractions`, `Npgsql`

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddPostgresDataExecutors(() => new NpgsqlConnection(connectionString));
```

## Registration Methods

| Method | What It Registers | Key Options |
|--------|-------------------|-------------|
| `data.UsePostgres(Action<IPostgresDataBuilder>)` | Core data executors | Builder with 5 connection overloads |
| `es.UsePostgres(Action<IPostgresEventSourcingBuilder>)` | `IEventStore` + `ISnapshotStore` | Builder with 5 connection overloads |
| `saga.UsePostgres(Action<IPostgresSagaBuilder>)` | `ISagaStore` | Builder with 5 connection + SchemaName, TableName |
| `inbox.UsePostgres(Action<IPostgresInboxBuilder>)` | `IInboxStore` | Builder with 5 connection + SchemaName, TableName, MaxRetryCount |
| `outbox.UsePostgres(Action<IPostgresOutboxBuilder>)` | `IOutboxStore` | Builder with 5+ connection + SchemaName, TableName, DeadLetterTableName |
| `le.UsePostgres(Action<IPostgresLeaderElectionBuilder>)` | `ILeaderElection` | Builder with 5 connection + LockKey |
| `cdc.UsePostgres(Action<IPostgresCdcBuilder>)` | CDC processor | Builder with 5 connection + PublicationName, ReplicationSlotName |
| `compliance.UsePostgres(Action<IPostgresComplianceBuilder>)` | Erasure + DataInventory + LegalHold | Builder with 5 connection overloads |
| `audit.UsePostgres(Action<IPostgresAuditLoggingBuilder>)` | `IAuditStore` | Builder with 5 connection + SchemaName, TableName |
| `AddPostgresProjectionStore<T>(opts)` | `IProjectionStore<T>` | `ConnectionString`, `TableName` |

### Batch Projection Registration

Register multiple projections sharing the same connection:

```csharp
services.AddPostgresProjections(connectionString, projections =>
{
    projections.Add<OrderSummary>();
    projections.Add<CustomerProfile>(o => o.TableName = "customer_views");
});
```

### Change Data Capture

```csharp
services.AddCdcProcessor(cdc =>
{
    cdc.UsePostgres(pg => pg.ConnectionString(connectionString))
       .TrackTable("public.orders", t => t.MapAll<OrderChangedEvent>());
});
```

## Data Request Pattern

```csharp
public class GetCustomerRequest : DataRequest<Customer?>
{
    public GetCustomerRequest(Guid customerId)
    {
        Command = new CommandDefinition(
            "SELECT * FROM customers WHERE id = @Id",
            new { Id = customerId });
    }
}
```

## See Also

- [Data Providers Overview](./index.md) — Architecture and core abstractions
- [SQL Server Provider](./sqlserver.md) — Microsoft SQL Server alternative
- [Event Sourcing](../event-sourcing/index.md) — PostgreSQL event store integration

