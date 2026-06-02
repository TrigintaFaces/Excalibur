---
sidebar_position: 6
title: Amazon DynamoDB
description: Cloud-native DynamoDB provider with partition/sort keys, streams, and batch operations.
---

# Amazon DynamoDB Provider

The DynamoDB provider implements `ICloudNativePersistenceProvider` for AWS serverless workloads with hash/sort key support, DynamoDB Streams integration, and transactional batch operations.

## Before You Start

- **.NET 10.0**
- An AWS account with DynamoDB access
- Familiarity with [data access](../data-access/index.md) and [IDb interface](../data-access/idb-interface.md)

## Installation

```bash
dotnet add package Excalibur.Data.DynamoDb
```

**Dependencies:** `Excalibur.Data.Abstractions`, `AWSSDK.DynamoDBv2`

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddExcaliburDynamoDb(dynamo =>
{
    dynamo.Region(RegionEndpoint.USEast1)
          .TablePrefix("MyApp_");
});
```

## Registration

The builder API (`IDynamoDBDataBuilder`) supports 4 canonical connection overloads plus configuration binding:

```csharp
// AWS Region (uses default credentials / IAM role)
services.AddExcaliburDynamoDb(dynamo =>
    dynamo.Region(RegionEndpoint.USEast1).TableName("MyTable"));

// Local DynamoDB / LocalStack
services.AddExcaliburDynamoDb(dynamo =>
    dynamo.ServiceUrl("http://localhost:8000").TableName("MyTable"));

// Existing IAmazonDynamoDB client
services.AddExcaliburDynamoDb(dynamo =>
    dynamo.Client(existingClient).TableName("MyTable"));

// Client factory (for custom configuration)
services.AddExcaliburDynamoDb(dynamo =>
    dynamo.ClientFactory(sp => new AmazonDynamoDBClient(credentials, region))
          .TableName("MyTable"));

// IConfiguration binding
services.AddExcaliburDynamoDb(dynamo =>
    dynamo.BindConfiguration("DynamoDb"));
```

All registrations include `ValidateOnStart` for options validation.

### Projection Store

Projections are stored flat at the document root. Framework metadata is isolated under a `_projection` nested object to avoid collisions with your projection properties. See [Projections — Document Storage Format](../event-sourcing/projections.md#document-storage-format) for details.

### Subsystem Registration

Each Excalibur subsystem supports DynamoDB via its own builder:

```csharp
services.AddExcalibur(excalibur =>
{
    // Event Sourcing
    excalibur.AddEventSourcing(es =>
        es.UseDynamoDb(dynamo =>
            dynamo.Region(RegionEndpoint.USEast1)
                  .TableName("Events")
                  .TablePrefix("MyApp_")));

    // Saga
    excalibur.AddSaga(saga =>
        saga.UseDynamoDb(dynamo =>
            dynamo.Region(RegionEndpoint.USEast1)
                  .TableName("Sagas")));

    // Inbox (idempotency)
    excalibur.AddInbox(inbox =>
        inbox.UseDynamoDb(dynamo =>
            dynamo.Region(RegionEndpoint.USEast1)
                  .TableName("Inbox")));

    // Outbox (reliable messaging)
    excalibur.AddOutbox(outbox =>
        outbox.UseDynamoDb(dynamo =>
            dynamo.Region(RegionEndpoint.USEast1)
                  .TableName("Outbox")));
});
```

### Change Data Capture

```csharp
services.AddCdcProcessor(cdc =>
{
    cdc.UseDynamoDb(dynamo =>
    {
        dynamo.TableName("Orders")
              .StreamArn("arn:aws:dynamodb:us-east-1:123456789:table/Orders/stream/2026-01-01T00:00:00.000")
              .ProcessorName("order-processor")
              .WithStateStore(
                  sp => new AmazonDynamoDBClient(),
                  state => state.TableName("CdcState"));
    })
    .TrackTable("Orders", t => t.MapAll<OrderChangedEvent>())
    .EnableBackgroundProcessing();
});
```

## Builder Methods

### Connection Methods

All 5 non-CDC subsystem builders share the same connection methods:

| Method | Description |
|--------|-------------|
| `ServiceUrl(string)` | Local DynamoDB or LocalStack endpoint URL |
| `Region(RegionEndpoint)` | AWS region (uses default credential chain) |
| `Client(IAmazonDynamoDB)` | Pre-configured client instance |
| `ClientFactory(Func<IServiceProvider, IAmazonDynamoDB>)` | Factory for custom client creation |
| `BindConfiguration(string)` | Bind from `IConfiguration` section |

Connection methods follow **last-wins** semantics — calling `Client()` after `Region()` replaces the region-based connection.

### Domain Methods

| Method | Description | Available On |
|--------|-------------|-------------|
| `TableName(string)` | DynamoDB table name | All 6 builders |
| `TablePrefix(string)` | Prefix for table names | 5 builders (not CDC) |
| `StreamArn(string)` | DynamoDB Streams ARN | CDC only |
| `ProcessorName(string)` | CDC processor name | CDC only |
| `WithStateStore(...)` | CDC state store configuration | CDC only |

## Partition and Sort Keys

DynamoDB uses hash keys and optional sort keys:

```csharp
var key = new PartitionKey("USER#user-123", "/pk");
var result = await provider.GetByIdAsync<UserProfile>(
    "PROFILE#main", key,
    consistencyOptions: null,
    cancellationToken: ct);
```

## Batch Operations

Execute multiple operations atomically:

```csharp
var batchResult = await provider.ExecuteBatchAsync(
    partitionKey,
    operations,
    cancellationToken: ct);
```

## Configuration Binding

Bind DynamoDB options from `appsettings.json`:

```json
{
  "DynamoDb": {
    "Connection": {
      "Region": "us-east-1",
      "ServiceUrl": null
    },
    "DefaultTableName": "MyTable",
    "DefaultPartitionKeyAttribute": "pk",
    "DefaultSortKeyAttribute": "sk",
    "UseConsistentReads": false
  }
}
```

```csharp
services.AddExcaliburDynamoDb(dynamo =>
    dynamo.BindConfiguration("DynamoDb"));
```

## See Also

- [Data Providers Overview](./index.md) — Architecture and core abstractions
- [Cosmos DB Provider](./cosmosdb.md) — Azure cloud-native alternative
- [Firestore Provider](./firestore.md) — Google Cloud alternative
