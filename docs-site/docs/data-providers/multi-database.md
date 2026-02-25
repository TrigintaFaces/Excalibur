---
sidebar_position: 12
title: Multi-Database Support
description: Use typed IDb interfaces to register separate database connections for event stores, sagas, outbox, and projections.
---

# Multi-Database Support

Excalibur provides **typed marker interfaces** derived from `IDb` that let you register separate database connections for different stores. This is useful when your event store, saga state, outbox messages, and read-side projections live on different databases.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Two or more data provider packages installed
- Familiarity with [IDb interface](../data-access/idb-interface.md) and [dependency injection](../core-concepts/dependency-injection.md)

## The Problem

In a simple setup, all stores share a single database connection:

```csharp
builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new SqlConnection(connectionString)));
```

This works when everything is in one database. But in larger deployments you may want:

- **Write database** for domain events and snapshots
- **Read database** for projections (CQRS read side)
- **Separate database** for saga state (isolate long-running processes)
- **Separate database** for the outbox (isolate transactional messaging)

Without typed interfaces, you'd have to register multiple `IDbConnection` instances and somehow distinguish them — leading to error-prone string-keyed or factory-based approaches.

## Typed IDb Interfaces

Excalibur solves this with marker interfaces that extend `IDb`. Each interface is an empty type marker used purely for DI resolution:

| Interface | Package | Purpose | Used By |
|-----------|---------|---------|---------|
| `IDomainDb` | `Excalibur.Data` | Domain event store, snapshot store | `SqlServerEventStore`, `SqlServerSnapshotStore` |
| `ISagaDb` | `Excalibur.Data` | Saga state persistence | `SqlServerSagaStore`, `PostgresSagaStore` |
| `IOutboxDb` | `Excalibur.Data` | Transactional outbox | `SqlServerOutboxStore` |
| `IProjectionDb` | `Excalibur.Data` | SQL read-side projections | `SqlServerProjectionStore`, `PostgresProjectionStore` |
| `IDataProcessorDb` | `Excalibur.Data.DataProcessing` | Data processor persistence | Data processing pipeline |
| `IDataToProcessDb` | `Excalibur.Data.DataProcessing` | Records awaiting processing | Data processing pipeline |
| `IDocumentDb` | `Excalibur.Data.Abstractions` | Cloud-native document databases | CosmosDB, DynamoDB, MongoDB, Firestore |

All SQL-based interfaces inherit from `IDb`:

```csharp
public interface IDomainDb : IDb;    // Domain events + snapshots
public interface ISagaDb : IDb;      // Saga state
public interface IOutboxDb : IDb;    // Outbox messages
public interface IProjectionDb : IDb; // SQL read-side projections
```

:::note
The typed `IDb` interfaces are for **SQL databases** that use `IDbConnection` (SQL Server, Postgres). Document databases (Elasticsearch, CosmosDB, MongoDB, Firestore) use their own SDK clients and are configured through `IOptions<T>` instead — see [Document Database Projections](#document-database-projections) below.
:::

## Single Database (Default)

When all stores share one database, register only `IDomainDb`:

```csharp
var connectionString = builder.Configuration.GetConnectionString("Default");

builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new SqlConnection(connectionString)));
```

Stores that accept a `Func<SqlConnection>` factory will use this connection:

```csharp
builder.Services.AddScoped(sp =>
{
    var db = sp.GetRequiredService<IDomainDb>();
    return new SqlServerEventStore(
        () => (SqlConnection)db.Connection,
        sp.GetRequiredService<ILogger<SqlServerEventStore>>());
});
```

## Multi-Database Setup

Register each typed interface pointing to a different connection string:

```csharp
var writeDb = builder.Configuration.GetConnectionString("WriteDb");
var readDb = builder.Configuration.GetConnectionString("ReadDb");
var sagaDb = builder.Configuration.GetConnectionString("SagaDb");
var outboxDb = builder.Configuration.GetConnectionString("OutboxDb");

// Write side: domain events and snapshots
builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new SqlConnection(writeDb)));

// Read side: projections (CQRS pattern)
builder.Services.AddScoped<IProjectionDb>(_ =>
    new ProjectionDb(new SqlConnection(readDb)));

// Saga state: separate for isolation
builder.Services.AddScoped<ISagaDb>(_ =>
    new SagaDb(new SqlConnection(sagaDb)));

// Outbox: separate for transactional messaging
builder.Services.AddScoped<IOutboxDb>(_ =>
    new OutboxDb(new SqlConnection(outboxDb)));
```

Then wire up each store to its typed interface:

```csharp
// Event store uses IDomainDb
builder.Services.AddScoped(sp =>
{
    var db = sp.GetRequiredService<IDomainDb>();
    return new SqlServerEventStore(
        () => (SqlConnection)db.Connection,
        sp.GetRequiredService<ILogger<SqlServerEventStore>>());
});

// Projection store uses IProjectionDb
builder.Services.AddScoped(sp =>
{
    var db = sp.GetRequiredService<IProjectionDb>();
    return new SqlServerProjectionStore<OrderReadModel>(
        () => (SqlConnection)db.Connection,
        sp.GetRequiredService<ILogger<SqlServerProjectionStore<OrderReadModel>>>());
});

// Saga store uses ISagaDb
builder.Services.AddScoped(sp =>
{
    var db = sp.GetRequiredService<ISagaDb>();
    return new SqlServerSagaStore(
        () => (SqlConnection)db.Connection,
        sp.GetRequiredService<ILogger<SqlServerSagaStore>>());
});

// Outbox store uses IOutboxDb
builder.Services.AddScoped(sp =>
{
    var db = sp.GetRequiredService<IOutboxDb>();
    return new SqlServerOutboxStore(
        () => (SqlConnection)db.Connection,
        sp.GetRequiredService<ILogger<SqlServerOutboxStore>>());
});
```

### Configuration

```json
{
  "ConnectionStrings": {
    "WriteDb": "Server=write-primary;Database=Domain;...",
    "ReadDb": "Server=read-replica;Database=Projections;...",
    "SagaDb": "Server=write-primary;Database=Sagas;...",
    "OutboxDb": "Server=write-primary;Database=Outbox;..."
  }
}
```

## Adapter Pattern

Each concrete class (`DomainDb`, `SagaDb`, `OutboxDb`, `ProjectionDb`) extends the abstract `Db` base class, which manages connection lifecycle:

```csharp
// All adapters follow this pattern
public class SagaDb(IDbConnection connection) : Db(connection), ISagaDb { }
public class OutboxDb(IDbConnection connection) : Db(connection), IOutboxDb { }
public class ProjectionDb(IDbConnection connection) : Db(connection), IProjectionDb { }
```

The `Db` base class:
- Ensures the connection is in a ready (open) state before returning it via `Connection`
- Implements `IDisposable` to close and dispose the connection
- Handles null-guard validation

## Data Processing Databases

For data processing pipelines, two additional typed interfaces exist in `Excalibur.Data.DataProcessing`:

```csharp
// Records awaiting processing
builder.Services.AddScoped<IDataToProcessDb>(sp =>
    new DataToProcessDb(sp.GetRequiredService<IDomainDb>()));

// Data processor persistence
builder.Services.AddScoped<IDataProcessorDb>(sp =>
    new DataProcessorDb(sp.GetRequiredService<IDomainDb>()));
```

The `DataToProcessDb` and `DataProcessorDb` classes use the **delegation pattern** — they wrap any `IDb` instance rather than extending `Db` directly. This lets you point them at any existing typed connection.

## Common Patterns

### CQRS: Separate Read and Write

The most common multi-database pattern separates the write side (events) from the read side (projections):

```csharp
// Write side
builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new SqlConnection(writeConnectionString)));

// Read side (can be a read replica or entirely separate database)
builder.Services.AddScoped<IProjectionDb>(_ =>
    new ProjectionDb(new SqlConnection(readConnectionString)));
```

### Isolated Saga State

Long-running sagas can generate significant load. Isolating saga state prevents contention with the main event store:

```csharp
builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new SqlConnection(domainConnectionString)));

builder.Services.AddScoped<ISagaDb>(_ =>
    new SagaDb(new SqlConnection(sagaConnectionString)));
```

### Dedicated Outbox Database

When outbox throughput is high, a separate database prevents lock contention with domain writes:

```csharp
builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new SqlConnection(domainConnectionString)));

builder.Services.AddScoped<IOutboxDb>(_ =>
    new OutboxDb(new SqlConnection(outboxConnectionString)));
```

## Postgres

The same pattern works with Npgsql for Postgres:

```csharp
using Npgsql;

builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new NpgsqlConnection(writeConnectionString)));

builder.Services.AddScoped<IProjectionDb>(_ =>
    new ProjectionDb(new NpgsqlConnection(readConnectionString)));
```

## Document Database Projections

When projections target a **document database** (Elasticsearch, CosmosDB, MongoDB), the typed `IDb` interfaces don't apply — these stores use SDK clients, not `IDbConnection`. Each document-based projection store is configured through its own `IOptions<T>`:

### Elasticsearch

```csharp
builder.Services.Configure<ElasticSearchProjectionStoreOptions>(options =>
{
    options.ConnectionString = "https://search-cluster:9200";
    options.IndexPrefix = "projections";
});

builder.Services.AddScoped<IProjectionStore<OrderReadModel>,
    ElasticSearchProjectionStore<OrderReadModel>>();
```

### Azure Cosmos DB

```csharp
builder.Services.Configure<CosmosDbProjectionStoreOptions>(options =>
{
    options.ConnectionString = "AccountEndpoint=https://...";
    options.DatabaseName = "Projections";
    options.ContainerName = "read-models";
});

builder.Services.AddScoped<IProjectionStore<OrderReadModel>,
    CosmosDbProjectionStore<OrderReadModel>>();
```

### MongoDB

```csharp
builder.Services.Configure<MongoDbProjectionStoreOptions>(options =>
{
    options.ConnectionString = "mongodb://read-cluster:27017";
    options.DatabaseName = "Projections";
    options.CollectionName = "read-models";
});

builder.Services.AddScoped<IProjectionStore<OrderReadModel>,
    MongoDbProjectionStore<OrderReadModel>>();
```

### Mixed: SQL Events + Document Projections

A common architecture uses SQL Server for the write side (event store, outbox, sagas) and a document database for the read side (projections). In this case, use `IDomainDb` for SQL stores and `IOptions<T>` for the document projection store:

```csharp
// Write side: SQL Server for events, outbox, sagas
builder.Services.AddScoped<IDomainDb>(_ =>
    new DomainDb(new SqlConnection(writeConnectionString)));

// Read side: Elasticsearch for projections (no IProjectionDb needed)
builder.Services.Configure<ElasticSearchProjectionStoreOptions>(options =>
{
    options.ConnectionString = "https://search-cluster:9200";
    options.IndexPrefix = "projections";
});

builder.Services.AddScoped<IProjectionStore<OrderReadModel>,
    ElasticSearchProjectionStore<OrderReadModel>>();
```

`IProjectionDb` is only needed when projections use a **SQL database** (SQL Server or Postgres) that requires a separate `IDbConnection` from the write side.

## Package Reference

| Package | Types |
|---------|-------|
| `Excalibur.Data.Abstractions` | `IDb`, `Db`, `IDocumentDb` |
| `Excalibur.Data` | `IDomainDb`, `DomainDb`, `ISagaDb`, `SagaDb`, `IOutboxDb`, `OutboxDb`, `IProjectionDb`, `ProjectionDb` |
| `Excalibur.Data.DataProcessing` | `IDataProcessorDb`, `DataProcessorDb`, `IDataToProcessDb`, `DataToProcessDb` |

## See Also

- [Data Providers Overview](./index.md) — Unified data access layer with all available provider implementations
- [IDb Interface](../data-access/idb-interface.md) — Database connection abstraction and IDataRequest pattern used by typed IDb interfaces
- [SQL Server Provider](./sqlserver.md) — Enterprise SQL Server provider with transaction scope and retry support
