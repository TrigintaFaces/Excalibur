---
sidebar_position: 8
title: MongoDB
description: MongoDB document provider with aggregation pipelines, change streams, and projection store.
---

# MongoDB Provider

The MongoDB provider implements `IDocumentPersistenceProvider` for flexible document storage with aggregation pipeline support, change streams, and integrated projection/snapshot/outbox stores.

## Before You Start

- **.NET 10.0**
- A MongoDB instance (local or Atlas)
- Familiarity with [data access](../data-access/index.md) and [IDb interface](../data-access/idb-interface.md)

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
