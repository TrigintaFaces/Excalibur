---
sidebar_position: 7
title: Google Firestore
description: Cloud-native Firestore provider with real-time listeners, hierarchical collections, and batch writes.
---

# Google Firestore Provider

The Firestore provider implements `ICloudNativePersistenceProvider` for Google Cloud workloads with real-time document listeners, hierarchical collection paths, and batch write support.

## Before You Start

- **.NET 10.0**
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

services.AddExcaliburFirestore(firestore =>
{
    firestore.ProjectId("my-gcp-project");
});
```

## Registration

The builder API (`IFirestoreDataBuilder`) supports multiple connection approaches matching GCP SDK patterns:

```csharp
// Project ID with Application Default Credentials (ADC)
services.AddExcaliburFirestore(firestore =>
    firestore.ProjectId("my-gcp-project"));

// Project ID with explicit credentials
services.AddExcaliburFirestore(firestore =>
    firestore.ProjectId("my-gcp-project")
             .CredentialsPath("/path/to/service-account.json"));

// Project ID with inline JSON credentials
services.AddExcaliburFirestore(firestore =>
    firestore.ProjectId("my-gcp-project")
             .CredentialsJson(jsonString));

// Emulator (local development)
services.AddExcaliburFirestore(firestore =>
    firestore.ProjectId("test-project")
             .EmulatorHost("localhost:8080"));

// Existing FirestoreDb instance
services.AddExcaliburFirestore(firestore =>
    firestore.Client(existingFirestoreDb));

// Client factory (for custom configuration)
services.AddExcaliburFirestore(firestore =>
    firestore.ClientFactory(sp => CreateFirestoreDb(sp)));

// IConfiguration binding
services.AddExcaliburFirestore(firestore =>
    firestore.BindConfiguration("Firestore"));
```

All registrations include `ValidateOnStart` for options validation.

:::tip Additive Credentials

Unlike other providers where connection methods are strictly last-wins, Firestore credential methods are **additive** — `ProjectId()` and `CredentialsPath()`/`CredentialsJson()` can coexist. Only `Client()` and `ClientFactory()` clear all other connection state (last-wins).
:::

### Subsystem Registration

Each Excalibur subsystem supports Firestore via its own builder:

```csharp
services.AddExcalibur(excalibur =>
{
    // Event Sourcing
    excalibur.AddEventSourcing(es =>
        es.UseFirestore(firestore =>
            firestore.ProjectId("my-project")
                     .CollectionName("events")));

    // Saga
    excalibur.AddSaga(saga =>
        saga.UseFirestore(firestore =>
            firestore.ProjectId("my-project")
                     .CollectionName("sagas")));

    // Inbox (idempotency)
    excalibur.AddInbox(inbox =>
        inbox.UseFirestore(firestore =>
            firestore.ProjectId("my-project")
                     .CollectionName("inbox")));

    // Outbox (reliable messaging)
    excalibur.AddOutbox(outbox =>
        outbox.UseFirestore(firestore =>
            firestore.ProjectId("my-project")
                     .CollectionName("outbox")));
});
```

### Change Data Capture

```csharp
services.AddCdcProcessor(cdc =>
{
    cdc.UseFirestore(firestore =>
    {
        firestore.ProjectId("my-project")
                 .CollectionPath("orders")
                 .ProcessorName("order-processor")
                 .MaxBatchSize(100)
                 .PollInterval(TimeSpan.FromSeconds(5))
                 .WithStateStore(state =>
                     state.TableName("cdc-checkpoints"));
    })
    .TrackTable("orders", t => t.MapAll<OrderChangedEvent>())
    .EnableBackgroundProcessing();
});
```

## Builder Methods

### Connection Methods

All 6 subsystem builders share GCP-specific connection methods:

| Method | Description | Behavior |
|--------|-------------|----------|
| `ProjectId(string)` | GCP project ID | Additive with credentials |
| `CredentialsPath(string)` | Path to service account JSON | Additive with ProjectId |
| `CredentialsJson(string)` | Inline service account JSON | Additive with ProjectId |
| `EmulatorHost(string)` | Firestore emulator endpoint | Additive with ProjectId |
| `Client(FirestoreDb)` | Pre-configured instance | Last-wins (clears all) |
| `ClientFactory(Func<IServiceProvider, FirestoreDb>)` | Factory for custom creation | Last-wins (clears all) |
| `BindConfiguration(string)` | Bind from `IConfiguration` section | Last-wins (clears all) |

### Domain Methods

| Method | Description | Available On |
|--------|-------------|-------------|
| `CollectionName(string)` | Firestore collection name | 5 builders (not CDC) |
| `CollectionPath(string)` | Hierarchical collection path | CDC only |
| `ProcessorName(string)` | CDC processor name | CDC only |
| `MaxBatchSize(int)` | CDC batch size | CDC only |
| `PollInterval(TimeSpan)` | CDC polling interval | CDC only |
| `WithStateStore(Action<ICdcStateStoreBuilder>)` | CDC state store config | CDC only |

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

## Configuration Binding

Bind Firestore options from `appsettings.json`:

```json
{
  "Firestore": {
    "ProjectId": "my-gcp-project",
    "CredentialsPath": "/path/to/service-account.json",
    "EmulatorHost": null,
    "DefaultCollection": "default",
    "TimeoutInSeconds": 30,
    "MaxRetryAttempts": 3
  }
}
```

```csharp
services.AddExcaliburFirestore(firestore =>
    firestore.BindConfiguration("Firestore"));
```

## See Also

- [Data Providers Overview](./index.md) — Architecture and core abstractions
- [Cosmos DB Provider](./cosmosdb.md) — Azure cloud-native alternative
- [DynamoDB Provider](./dynamodb.md) — AWS cloud-native alternative
