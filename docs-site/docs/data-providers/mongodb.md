---
sidebar_position: 8
title: MongoDB
description: MongoDB document provider with aggregation pipelines, change streams, and projection store.
---

# MongoDB Provider

The MongoDB provider implements `IDocumentPersistenceProvider` for flexible document storage with aggregation pipeline support, change streams, and integrated projection/snapshot/outbox stores.

The event store writes each event's declared `[MessageName]` -- not its CLR type name -- to the stored event-type field, and resolves that name back to a CLR type through the registered event-type registry on read. See [Stable Message Names](../event-sourcing/domain-events.md#stable-message-names).

## Before You Start

- **.NET 10.0**
- A MongoDB instance (local or Atlas)
- Familiarity with [data access](../data-access/index.md) and [IDb interface](../data-access/idb-interface.md)

:::warning Event sourcing requires a replica set

If you use the MongoDB **event store**, MongoDB must run as a replica set — a single-node replica set is
enough, and no additional members are required.

Appending two or more events in one call commits them inside a transaction, so that a batch lands
all-or-nothing and a stream can never contain a partial prefix. MongoDB offers transactions only on a
replica set. On a standalone server the driver rejects the write and the append fails with:

```text
Standalone servers do not support transactions.
```

The failure is easy to miss in early development because it does not affect every write. Appending a
**single** event is one document, needs no transaction, and succeeds on a standalone server — so a
prototype that saves one event at a time works, and begins failing only once an aggregate raises two
events in a single operation.

MongoDB Atlas always runs as a replica set, so this affects self-hosted deployments. To convert a local
standalone server, start it with `--replSet rs0` and run `rs.initiate()` once.

The other MongoDB stores — snapshots, projections, saga, inbox, outbox, CDC — do not require this.

:::

## Installation

```bash
dotnet add package Excalibur.Data.MongoDB
```

**Dependencies:** `Excalibur.Data.Abstractions`, `MongoDB.Driver`

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;

// Data persistence provider (fluent builder)
services.AddExcaliburMongoDb(mongo =>
{
    mongo.ConnectionString("mongodb://localhost:27017")
         .DatabaseName("MyApp");
});
```

## Builder Registration (Recommended)

All MongoDB subsystems use the fluent builder pattern with 4 canonical connection overloads:

```csharp
// 1. Connection string (creates IMongoClient singleton internally)
mongo.ConnectionString("mongodb://localhost:27017");

// 2. Pre-configured IMongoClient instance
mongo.Client(existingMongoClient);

// 3. DI-aware client factory
mongo.ClientFactory(sp => sp.GetRequiredService<IMongoClient>());

// 4. Bind from appsettings.json section
mongo.BindConfiguration("MongoDB:Data");
```

### Subsystem Entry Points

| Subsystem | Entry Point | Builder Interface |
|-----------|-------------|-------------------|
| Data | `services.AddExcaliburMongoDb(mongo => ...)` | `IMongoDBDataBuilder` |
| Event Sourcing | `es.UseMongoDB(mongo => ...)` | `IMongoDBEventSourcingBuilder` |
| Saga | `saga.UseMongoDB(mongo => ...)` | `IMongoDBSagaBuilder` |
| Inbox | `inbox.UseMongoDB(mongo => ...)` | `IMongoDBInboxBuilder` |
| Outbox | `outbox.UseMongoDB(mongo => ...)` | `IMongoDBOutboxBuilder` |
| CDC | `cdc.UseMongoDB(mongo => ...)` | `IMongoDbCdcBuilder` |
| Leader Election | `le.UseMongoDB(resourceName, mongo => ...)` | `IMongoDBLeaderElectionBuilder` |

### Legacy Registration Methods

The following standalone methods are still available for snapshots and projections:

| Method | What It Registers | Key Options |
|--------|-------------------|-------------|
| `AddMongoDbSnapshotStore(opts)` | `ISnapshotStore` | `CollectionName` |
| `AddMongoDbProjectionStore<T>(connStr, dbName, opts?)` | `IProjectionStore<T>` | `CollectionName` |

### Batch Projection Registration

Register multiple projections sharing the same connection in a single call:

```csharp
services.AddMongoDbProjections("mongodb://localhost:27017", "MyApp", projections =>
{
    projections.Add<OrderSummary>();
    projections.Add<CustomerProfile>(o => o.CollectionName = "customers");
    projections.Add<InventoryView>(o => o.CollectionName = "inventory");
});
```

Projections are stored flat at the document root. Framework metadata is isolated under a `_projection` nested object to avoid collisions with your projection properties. See [Projections — Document Storage Format](../event-sourcing/projections.md#document-storage-format) for details.

This follows the same pattern as [`AddElasticSearchProjections()`](./elasticsearch.md).

## Aggregation Pipelines

MongoDB's aggregation framework is accessible through the document persistence provider:

```csharp
var result = await documentProvider.ExecuteAggregationAsync(aggregationRequest, cancellationToken);
```

## Index Management

```csharp
await documentProvider.ExecuteIndexOperationAsync(indexRequest, cancellationToken);
```

## See Also

- [Data Providers Overview](./index.md) — Architecture and core abstractions
- [Cosmos DB Provider](./cosmosdb.md) — Azure cloud-native document store
- [Elasticsearch Provider](./elasticsearch.md) — Full-text search and analytics
