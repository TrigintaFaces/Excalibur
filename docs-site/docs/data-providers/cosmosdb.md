---
sidebar_position: 5
title: Azure Cosmos DB
description: Cloud-native Cosmos DB provider with partition keys, consistency levels, and change feed support.
---

# Azure Cosmos DB Provider

The Cosmos DB provider implements `ICloudNativePersistenceProvider` for globally distributed, multi-model database access with partition key management, tunable consistency, and change feed subscriptions.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- An Azure Cosmos DB account and database
- Familiarity with [data access](../data-access/index.md) and [IDb interface](../data-access/idb-interface.md)

## Installation

```bash
dotnet add package Excalibur.Data.CosmosDb
```

**Dependencies:** `Excalibur.Data.Abstractions`, `Microsoft.Azure.Cosmos`

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddCosmosDb(options =>
{
    options.AccountEndpoint = "https://myaccount.documents.azure.com:443/";
    options.AccountKey = "<your-auth-key>";
    options.DatabaseName = "MyDatabase";
});
```

## Registration Options

```csharp
// With options callback
services.AddCosmosDb(options =>
{
    options.AccountEndpoint = "https://...";
    options.AccountKey = "...";
    options.DatabaseName = "MyDatabase";
});

// From configuration section
services.AddCosmosDb(configuration);
services.AddCosmosDb(configuration, sectionName: "CosmosDb");
```

### Snapshot Store

```csharp
services.AddCosmosDbSnapshotStore(options =>
{
    options.ContainerName = "snapshots";
});
```

### Change Data Capture

```csharp
services.AddCosmosDbCdc(options =>
{
    options.LeaseContainerName = "leases";
});

services.AddCosmosDbCdcStateStore(options =>
{
    options.ContainerName = "cdc-state";
});
```

## Partition Keys

Cosmos DB requires partition keys for data distribution:

```csharp
// Simple partition key
var key = new PartitionKey("tenant-123", "/tenantId");
var order = await provider.GetByIdAsync<Order>("order-1", key, consistencyOptions: null, cancellationToken: ct);

// Composite partition key
var compositeKey = new CompositePartitionKey(
    path: "/pk",
    separator: "|",
    "region-us", "tenant-123");
```

## Consistency Options

Tune consistency per operation:

```csharp
// Strong consistency
var result = await provider.QueryAsync<Order>(
    "SELECT * FROM c WHERE c.status = 'pending'",
    partitionKey,
    parameters: null,
    consistencyOptions: ConsistencyOptions.Strong,
    cancellationToken: ct);

// Session consistency (read-your-own-writes)
var sessionOptions = ConsistencyOptions.WithSession(sessionToken);

// Bounded staleness
var boundedOptions = ConsistencyOptions.WithBoundedStaleness(
    maxStaleness: TimeSpan.FromSeconds(5),
    maxVersionLag: 100);
```

## Change Feed

Subscribe to real-time document changes:

```csharp
var subscription = await provider.CreateChangeFeedSubscriptionAsync<Order>(
    containerName: "orders",
    options: new ChangeFeedOptions
    {
        StartPosition = ChangeFeedStartPosition.Beginning,
        MaxBatchSize = 100,
        PollingInterval = TimeSpan.FromSeconds(5)
    },
    cancellationToken: ct);

await subscription.StartAsync(ct);

await foreach (var change in subscription.ReadChangesAsync(ct))
{
    // Process change event
}
```

## Batch Operations

Execute multiple operations in a single transaction within a partition:

```csharp
var result = await provider.ExecuteBatchAsync(
    partitionKey,
    new ICloudBatchOperation[]
    {
        // batch operations
    },
    cancellationToken: ct);

if (result.Success)
{
    Console.WriteLine($"Request charge: {result.RequestCharge} RU");
}
```

## ETag-Based Concurrency

`UpdateAsync` accepts an optional ETag for optimistic concurrency. It returns a `CloudOperationResult<TDocument>` with conflict detection:

```csharp
// Update with ETag check (etag obtained from a previous write or Cosmos DB SDK)
var updateResult = await provider.UpdateAsync(
    modifiedOrder,
    partitionKey,
    etag: previousEtag,
    cancellationToken: ct);

if (updateResult.IsConcurrencyConflict)
{
    // Handle conflict — another process modified the document (HTTP 412)
}

// Successful updates return the new ETag for subsequent operations
var newEtag = updateResult.ETag;
```

## See Also

- [Data Providers Overview](./index.md) — Architecture and core abstractions
- [DynamoDB Provider](./dynamodb.md) — AWS cloud-native alternative
- [Firestore Provider](./firestore.md) — Google Cloud alternative
