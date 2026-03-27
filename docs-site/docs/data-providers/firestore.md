---
sidebar_position: 7
title: Google Firestore
description: Cloud-native Firestore provider with real-time listeners, hierarchical collections, and batch writes.
---

# Google Firestore Provider

The Firestore provider implements `ICloudNativePersistenceProvider` for Google Cloud workloads with real-time document listeners, hierarchical collection paths, and batch write support.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A Google Cloud project with Firestore enabled
- Familiarity with [data access](../data-access/index.md) and [IDb interface](../data-access/idb-interface.md)

## Installation

```bash
dotnet add package Excalibur.Data.Firestore
```

**Dependencies:** `Excalibur.Data.Abstractions`, `Google.Cloud.Firestore`

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddFirestore(options =>
{
    options.ProjectId = "my-gcp-project";
});
```

## Registration Methods

| Method | What It Registers | Key Options |
|--------|-------------------|-------------|
| `AddFirestore(opts)` | Core persistence provider | `ProjectId`, `EmulatorHost` |
| `AddFirestoreWithDatabase(opts)` | Provider with specific database | `ProjectId`, `DatabaseId` |
| `AddFirestoreSnapshotStore(opts)` | `ISnapshotStore` | `CollectionName` |
| `AddFirestoreInboxStore(opts)` | `IInboxStore` | `CollectionName` |

All methods also accept `IConfiguration` binding: `AddFirestore(configuration, sectionName: "Firestore")`.

### Change Data Capture

```csharp
services.AddCdcProcessor(cdc =>
{
    cdc.UseFirestore(firestore =>
    {
        firestore.CollectionPath("orders")
                 .WithStateStore("state-project-id", state =>
                 {
                     state.TableName("cdc-checkpoints");
                 });
    })
    .TrackTable("orders", t => t.MapAll<OrderChangedEvent>())
    .EnableBackgroundProcessing();
});
```

## Collection Hierarchies

Firestore supports hierarchical document/collection paths:

```csharp
var key = new PartitionKey("users/user-123/orders", "/collection");
var orders = await provider.QueryAsync<Order>(
    queryText: "",
    key,
    parameters: null,
    consistencyOptions: null,
    cancellationToken: ct);
```

## See Also

- [Data Providers Overview](./index.md) — Architecture and core abstractions
- [Cosmos DB Provider](./cosmosdb.md) — Azure cloud-native alternative
- [DynamoDB Provider](./dynamodb.md) — AWS cloud-native alternative
