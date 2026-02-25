---
sidebar_position: 8
title: MongoDB
description: MongoDB document provider with aggregation pipelines, change streams, and projection store.
---

# MongoDB Provider

The MongoDB provider implements `IDocumentPersistenceProvider` for flexible document storage with aggregation pipeline support, change streams, and integrated projection/snapshot/outbox stores.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
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

services.AddMongoDbSnapshotStore(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "MyApp";
    options.CollectionName = "snapshots";
});
```

## Registration Options

MongoDB registration uses specialized store methods — there is no single base `AddMongoDB()` registration. Register only the stores your application needs:

### Snapshot Store

```csharp
services.AddMongoDbSnapshotStore(options =>
{
    options.CollectionName = "snapshots";
});
```

### Projection Store

```csharp
services.AddMongoDbProjectionStore<OrderProjection>(options =>
{
    options.CollectionName = "order-projections";
});
```

### Outbox Store

```csharp
services.AddMongoDbOutboxStore(options =>
{
    options.CollectionName = "outbox";
});
```

### Saga Store

```csharp
services.AddMongoDbSagaStore(options =>
{
    options.CollectionName = "sagas";
});
```

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
