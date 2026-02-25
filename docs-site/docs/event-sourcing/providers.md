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

// Combined registration (event store + snapshot store + outbox)
services.AddSqlServerEventSourcing(connectionString);

// Or with options
services.AddSqlServerEventSourcing(options =>
{
    options.ConnectionString = connectionString;
    options.EventStoreSchema = "es";
    options.SnapshotStoreSchema = "es";
    options.OutboxSchema = "es";
});

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
dotnet add package Excalibur.Data.Postgres
```

### Setup

```csharp
// With connection string
services.AddPostgresEventStore(connectionString, options =>
{
    options.SchemaName = "public";  // Default
});

// With options
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

## MongoDB

Document-based event store with flexible schema.

### Installation

```bash
dotnet add package Excalibur.Data.MongoDB
```

### Setup

```csharp
// With connection string and database name
services.AddMongoDbEventStore(
    "mongodb://localhost:27017",
    "EventStore",
    options =>
    {
        options.CollectionName = "events";
    });

// With options callback
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

---

## Azure Cosmos DB

Globally distributed event store with partition-based scaling.

### Installation

```bash
dotnet add package Excalibur.EventSourcing.CosmosDb
```

### Setup

```csharp
// With options callback
services.AddCosmosDbEventStore(options =>
{
    options.EventsContainerName = "events";
    options.PartitionKeyPath = "/streamId";  // Default
});

// From configuration
services.AddCosmosDbEventStore(configuration);
services.AddCosmosDbEventStore(configuration, sectionName: "CosmosDbEventStore");
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
// With options callback
services.AddDynamoDbEventStore(options =>
{
    options.EventsTableName = "Events";  // Default
});

// From configuration
services.AddDynamoDbEventStore(configuration);
services.AddDynamoDbEventStore(configuration, sectionName: "DynamoDbEventStore");
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
// With options callback
services.AddFirestoreEventStore(options =>
{
    options.ProjectId = "my-gcp-project";
    options.EventsCollectionName = "events";
});

// From configuration
services.AddFirestoreEventStore(configuration);
services.AddFirestoreEventStore(configuration, sectionName: "FirestoreEventStore");
```

### Collection Structure

Firestore event stores use subcollections under aggregate documents, leveraging Firestore's hierarchical document model.

---

## In-Memory (Testing)

For unit and integration tests:

```csharp
services.AddInMemoryEventStore();
```

---

## Provider Comparison

| Provider | Package | Transaction Support | Scaling Model |
|----------|---------|-------------------|---------------|
| SQL Server | `Excalibur.EventSourcing.SqlServer` | Full ACID | Vertical + read replicas |
| PostgreSQL | `Excalibur.Data.Postgres` | Full ACID | Vertical + read replicas |
| MongoDB | `Excalibur.Data.MongoDB` | Document-level | Sharding |
| Cosmos DB | `Excalibur.EventSourcing.CosmosDb` | Partition-scoped | Global distribution |
| DynamoDB | `Excalibur.EventSourcing.DynamoDb` | Item-level | On-demand / provisioned |
| Firestore | `Excalibur.EventSourcing.Firestore` | Document-level | Automatic |
| In-Memory | `Excalibur.EventSourcing.InMemory` | None | Single process |

## See Also

- [Event Sourcing Overview](./index.md) — Architecture and core abstractions
- [Event Store](./event-store.md) — `IEventStore` interface details
- [Snapshots](./snapshots.md) — Snapshot store configuration
- [Change Data Capture](../patterns/cdc.md) — CDC patterns and provider support

