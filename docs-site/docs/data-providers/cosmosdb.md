---
sidebar_position: 5
title: Azure Cosmos DB
description: Cloud-native Cosmos DB provider with partition keys, consistency levels, and change feed support.
---

# Azure Cosmos DB Provider

The Cosmos DB provider implements `ICloudNativePersistenceProvider` for globally distributed, multi-model database access with partition key management, tunable consistency, and change feed subscriptions.

## Before You Start

- **.NET 10.0**
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

services.AddExcaliburCosmosDb(cosmos =>
{
    cosmos.ConnectionString("AccountEndpoint=https://myaccount.documents.azure.com:443/;AccountKey=<your-auth-key>")
          .DatabaseName("MyDatabase");
});
```

## Registration

The builder API (`ICosmosDbDataBuilder`) supports 4 canonical connection overloads:

```csharp
// Connection string
services.AddExcaliburCosmosDb(cosmos =>
    cosmos.ConnectionString(connectionString).DatabaseName("MyDatabase"));

// Existing CosmosClient instance
services.AddExcaliburCosmosDb(cosmos =>
    cosmos.Client(existingClient).DatabaseName("MyDatabase"));

// Client factory (for custom configuration)
services.AddExcaliburCosmosDb(cosmos =>
    cosmos.ClientFactory(() => new CosmosClient(connectionString, clientOptions))
          .DatabaseName("MyDatabase"));

// IConfiguration binding
services.AddExcaliburCosmosDb(cosmos =>
    cosmos.BindConfiguration("CosmosDb"));
```

All registrations include `ValidateOnStart` for options validation.

### Batch Projection Registration

Register multiple projections sharing the same Cosmos DB account:

```csharp
services.AddCosmosDbProjections(connectionString, "MyDatabase", projections =>
{
    projections.Add<OrderSummary>();
    projections.Add<CustomerProfile>(o => o.ContainerName = "customers");
});
```

Projections are stored flat at the document root. Framework metadata is isolated under a `_projection` nested object to avoid collisions with your projection properties. The `projectionType` partition key and Cosmos DB `id` remain at root level as required by the database engine. See [Projections — Document Storage Format](../event-sourcing/projections.md#document-storage-format) for details.

### Change Data Capture

```csharp
services.AddCdcProcessor(cdc =>
{
    cdc.UseCosmosDb(cosmos =>
    {
        cosmos.ConnectionString(connectionString)
              .DatabaseName("MyApp")
              .ContainerName("orders");
    })
    .TrackTable("orders", t => t.MapAll<OrderChangedEvent>())
    .EnableBackgroundProcessing();
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

### Durable Change Feed Continuation

By default, pull-model change-feed subscriptions track their continuation token **in memory**. On a
process restart that progress is lost and the feed is re-read from the configured start position,
reprocessing already-handled changes. The default store
(`InMemoryChangeFeedCheckpointStore`) preserves this non-durable behavior and emits a **one-time
startup warning** so the trade-off is never silent.

To survive restarts, register the durable Cosmos-backed checkpoint store. Call it **after**
`AddExcaliburCosmosDb` — it replaces the in-memory default, so every change-feed subscription created
by the provider flows through the durable store:

```csharp
services.AddExcaliburCosmosDb(cosmos =>
{
    cosmos.ConnectionString(connectionString)
          .DatabaseName("myapp")
          .ContainerName("orders");
});

// Persist change-feed continuation tokens to a Cosmos container.
// The caller owns the container; partition key path must be /subscriptionId.
services.AddCosmosDbChangeFeedCheckpointStore(sp =>
    sp.GetRequiredService<CosmosClient>()
      .GetContainer("myapp", "changefeed-checkpoints"));
```

The continuation token is checkpointed per subscription (keyed by the subscription/lease name) and
reloaded on restart so processing resumes from the last persisted position.

To plug in a different backing store (e.g. SQL Server or Redis), implement `IChangeFeedCheckpointStore`:

```csharp
public interface IChangeFeedCheckpointStore
{
    // Returns the last persisted token, or null if none checkpointed yet.
    Task<string?> LoadAsync(string subscriptionId, CancellationToken cancellationToken);

    // Persists the latest token, overwriting any prior value.
    Task SaveAsync(string subscriptionId, string continuationToken, CancellationToken cancellationToken);
}
```

:::note Scope
Durable continuation covers the **pull-model** change-feed subscriptions (the data provider, outbox,
and event-store subscriptions). The push-model `AllVersionsAndDeletes` processor path is not yet
covered — it is gated on the Cosmos SDK `ChangeFeedItem<T>` API reaching GA.
:::

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
