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

## Registration Options

```csharp
// With options callback
services.AddFirestore(options =>
{
    options.ProjectId = "my-gcp-project";
    options.EmulatorHost = "localhost:8080"; // Local development
});

// From configuration
services.AddFirestore(configuration);
services.AddFirestore(configuration, sectionName: "Firestore");

// With specific database
services.AddFirestoreWithDatabase(options =>
{
    options.ProjectId = "my-gcp-project";
    options.DatabaseId = "my-database";
});
```

### Snapshot Store

```csharp
services.AddFirestoreSnapshotStore(options =>
{
    options.CollectionName = "snapshots";
});
```

### Inbox Store

```csharp
services.AddFirestoreInboxStore(options =>
{
    options.CollectionName = "inbox";
});
```

### Change Data Capture

```csharp
services.AddFirestoreCdc(options =>
{
    options.CollectionPath = "orders";
});

services.AddFirestoreCdcStateStore(options =>
{
    options.CollectionName = "cdc-state";
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
