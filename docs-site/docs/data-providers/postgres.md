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

## Registration Options

### Data Executors

```csharp
services.AddPostgresDataExecutors(() => new NpgsqlConnection(connectionString));
```

### Event Store

```csharp
// With connection string
services.AddPostgresEventStore(connectionString);

// With connection string and options
services.AddPostgresEventStore(connectionString, options =>
{
    options.SchemaName = "events";
});
```

### Snapshot Store

```csharp
services.AddPostgresSnapshotStore(connectionString);
```

### Inbox / Outbox

```csharp
services.AddPostgresInboxStore(options =>
{
    options.ConnectionString = connectionString;
});
```

### Change Data Capture

```csharp
services.AddPostgresCdc(options =>
{
    options.ConnectionString = connectionString;
    options.PublicationName = "my_publication";
    options.ReplicationSlotName = "my_slot";
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

