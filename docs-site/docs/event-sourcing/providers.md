---
sidebar_position: 8
title: Event Store Providers
description: Per-provider event store setup for SQL Server, PostgreSQL, MongoDB, Cosmos DB, DynamoDB, and Firestore.
---

# Event Store Providers

Each event store provider implements `IEventStore` with database-specific optimizations. SQL Server is the primary provider; cloud-native and document providers offer alternatives for specific deployment targets.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the provider package for your database (see below)
- Familiarity with [event sourcing concepts](./concepts.md) and [event store setup](../configuration/event-store-setup.md)

## SQL Server

The primary event store provider with full transaction support.

### Installation

```bash
dotnet add package Excalibur.EventSourcing.SqlServer
```

### Setup

```csharp
using Microsoft.Extensions.DependencyInjection;

// Recommended: Builder-integrated registration
services.AddExcaliburEventSourcing(es =>
{
    es.UseSqlServer(connectionString)
      .AddRepository<OrderAggregate, Guid>();
});

// Or with detailed options
services.AddExcaliburEventSourcing(es =>
{
    es.UseSqlServer(options =>
    {
        options.ConnectionString = connectionString;
        options.EventStoreSchema = "es";
        options.SnapshotStoreSchema = "es";
        options.OutboxSchema = "es";
    });
});

// Alternative: Direct IServiceCollection registration
services.AddSqlServerEventSourcing(connectionString);

// Individual stores
services.AddSqlServerEventStore(connectionString);
services.AddSqlServerSnapshotStore(connectionString);
services.AddSqlServerOutboxStore(connectionString);

// With connection factory
services.AddSqlServerEventStore(() => new SqlConnection(connectionString));
services.AddSqlServerSnapshotStore(() => new SqlConnection(connectionString));
services.AddSqlServerOutboxStore(() => new SqlConnection(connectionString));
```

---

## PostgreSQL

Open-source alternative with Npgsql-based access.

### Installation

```bash
dotnet add package Excalibur.EventSourcing.Postgres
```

### Setup

```csharp
// Recommended: Builder-integrated registration
services.AddExcaliburEventSourcing(es =>
{
    es.UsePostgres(connectionString)
      .AddRepository<OrderAggregate, Guid>();
});

// Or with options
services.AddExcaliburEventSourcing(es =>
{
    es.UsePostgres(options =>
    {
        options.ConnectionString = connectionString;
        options.RegisterHealthChecks = true;
    });
});

// Alternative: Direct IServiceCollection registration
services.AddPostgresEventStore(connectionString, options =>
{
    options.SchemaName = "events";
    options.EventsTableName = "event_store_events";  // Default
});

// With connection factory (receives IServiceProvider)
services.AddPostgresEventStore(
    sp => new NpgsqlConnection(configuration.GetConnectionString("Postgres")),
    options =>
    {
        options.SchemaName = "events";
    });
```

---

## Azure Cosmos DB

Globally distributed event store with partition-based scaling.

### Installation

```bash
dotnet add package Excalibur.EventSourcing.CosmosDb
```

### Setup

```csharp
// Recommended: Builder-integrated registration
services.AddExcaliburEventSourcing(es =>
{
    es.UseCosmosDb(options =>
    {
        options.ConnectionString = connectionString;
        options.DatabaseName = "events";
        options.ContainerName = "event-store";
    })
    .AddRepository<OrderAggregate, Guid>();
});

// Or with IConfiguration binding
services.AddExcaliburEventSourcing(es =>
{
    es.UseCosmosDb(configuration.GetSection("CosmosDb"));
});

// Alternative: Direct registration
services.AddCosmosDbEventStore(options =>
{
    options.EventsContainerName = "events";
    options.PartitionKeyPath = "/streamId";  // Default
});
```

### Partition Strategy

Cosmos DB event stores partition by aggregate ID. Each aggregate's events are stored in a single logical partition for transactional consistency.

---

## Amazon DynamoDB

Serverless event store for AWS workloads.

### Installation

```bash
dotnet add package Excalibur.EventSourcing.DynamoDb
```

### Setup

```csharp
// Recommended: Builder-integrated registration
services.AddExcaliburEventSourcing(es =>
{
    es.UseDynamoDb(options =>
    {
        options.TableName = "event-store";
        options.Region = "us-east-1";
    })
    .AddRepository<OrderAggregate, Guid>();
});

// Or with IConfiguration binding
services.AddExcaliburEventSourcing(es =>
{
    es.UseDynamoDb(configuration.GetSection("DynamoDb"));
});

// Alternative: Direct registration
services.AddDynamoDbEventStore(options =>
{
    options.EventsTableName = "Events";
});
```

### Key Schema

DynamoDB event stores use the aggregate ID as the partition key and event version as the sort key, providing efficient sequential reads per aggregate.

---

## Google Firestore

Real-time event store for Google Cloud workloads.

### Installation

```bash
dotnet add package Excalibur.EventSourcing.Firestore
```

### Setup

```csharp
// Recommended: Builder-integrated registration
services.AddExcaliburEventSourcing(es =>
{
    es.UseFirestore(options =>
    {
        options.ProjectId = "my-gcp-project";
        options.CollectionName = "events";
    })
    .AddRepository<OrderAggregate, Guid>();
});

// Or with IConfiguration binding
services.AddExcaliburEventSourcing(es =>
{
    es.UseFirestore(configuration.GetSection("Firestore"));
});

// Alternative: Direct registration
services.AddFirestoreEventStore(options =>
{
    options.ProjectId = "my-gcp-project";
    options.EventsCollectionName = "events";
});
```

### Collection Structure

Firestore event stores use subcollections under aggregate documents, leveraging Firestore's hierarchical document model.

---

## MongoDB

Document-oriented event store with flexible schema and horizontal scaling via sharding.

### Installation

```bash
dotnet add package Excalibur.EventSourcing.MongoDB
```

### Setup

```csharp
// Recommended: Builder-integrated registration
services.AddExcaliburEventSourcing(es =>
{
    es.UseMongoDB(options =>
    {
        options.ConnectionString = "mongodb://localhost:27017";
        options.DatabaseName = "events";
    })
      .AddRepository<OrderAggregate, Guid>();
});

// Or with connection string shorthand
services.AddExcaliburEventSourcing(es =>
{
    es.UseMongoDB("mongodb://localhost:27017", "events")
      .AddRepository<OrderAggregate, Guid>();
});

// Alternative: Direct IServiceCollection registration
services.AddMongoDbEventStore(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "EventStore";
    options.CollectionName = "events";
});

// With custom client factory (receives IServiceProvider)
services.AddMongoDbEventStore(
    sp => sp.GetRequiredService<IMongoClient>(),
    options =>
    {
        options.DatabaseName = "EventStore";
    });
```

### Document Model

MongoDB event stores use a single collection per aggregate type with the aggregate ID as the document key. Events are stored as embedded arrays within the aggregate document.

---

## In-Memory (Testing)

For unit and integration tests:

```csharp
// Recommended: Builder-integrated registration
services.AddExcaliburEventSourcing(es =>
{
    es.UseInMemory()
      .AddRepository<OrderAggregate, Guid>();
});

// Alternative: Direct registration
services.AddInMemoryEventStore();
```

---

## Provider Comparison

| Provider | Package | Transaction Support | Scaling Model |
|----------|---------|-------------------|---------------|
| SQL Server | `Excalibur.EventSourcing.SqlServer` | Full ACID | Vertical + read replicas |
| PostgreSQL | `Excalibur.EventSourcing.Postgres` | Full ACID | Vertical + read replicas |
| MongoDB | `Excalibur.EventSourcing.MongoDB` | Document-level | Sharding |
| Cosmos DB | `Excalibur.EventSourcing.CosmosDb` | Partition-scoped | Global distribution |
| DynamoDB | `Excalibur.EventSourcing.DynamoDb` | Item-level | On-demand / provisioned |
| Firestore | `Excalibur.EventSourcing.Firestore` | Document-level | Automatic |
| In-Memory | `Excalibur.EventSourcing.InMemory` | None | Single process |

## See Also

- [Event Sourcing Overview](./index.md) — Architecture and core abstractions
- [Event Store](./event-store.md) — `IEventStore` interface details
- [Snapshots](./snapshots.md) — Snapshot store configuration
- [Change Data Capture](../patterns/cdc.md) — CDC patterns and provider support

