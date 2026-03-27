---
sidebar_position: 6
title: Amazon DynamoDB
description: Cloud-native DynamoDB provider with partition/sort keys, streams, and batch operations.
---

# Amazon DynamoDB Provider

The DynamoDB provider implements `ICloudNativePersistenceProvider` for AWS serverless workloads with hash/sort key support, DynamoDB Streams integration, and transactional batch operations.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
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

services.AddDynamoDb(options =>
{
    options.Region = "us-east-1";
    options.TablePrefix = "MyApp_";
});
```

## Registration Methods

| Method | What It Registers | Key Options |
|--------|-------------------|-------------|
| `AddDynamoDb(opts)` | Core persistence provider | `Region`, `ServiceUrl`, `TablePrefix` |
| `AddDynamoDbWithClient(client)` | Provider with pre-configured client | N/A |
| `AddDynamoDbSnapshotStore(opts)` | `ISnapshotStore` | `TableName` |
| `AddDynamoDbAuthorization(opts)` | Authorization store | `TableName` |

All methods also accept `IConfiguration` binding: `AddDynamoDb(configuration, sectionName: "DynamoDb")`.

### Change Data Capture

```csharp
services.AddCdcProcessor(cdc =>
{
    cdc.UseDynamoDb(dynamo =>
    {
        dynamo.TableName("Orders")
              .StreamArn("arn:aws:dynamodb:...")
              .WithStateStore(
                  sp => new AmazonDynamoDBClient(stateRegionEndpoint),
                  state => state.TableName("CdcState"));
    })
    .TrackTable("Orders", t => t.MapAll<OrderChangedEvent>())
    .EnableBackgroundProcessing();
});
```

## Partition and Sort Keys

DynamoDB uses hash keys and optional sort keys:

```csharp
var key = new PartitionKey("USER#user-123", "/pk");
var result = await provider.GetByIdAsync<UserProfile>("PROFILE#main", key, consistencyOptions: null, cancellationToken: ct);
```

## Batch Operations

Execute multiple operations atomically:

```csharp
var batchResult = await provider.ExecuteBatchAsync(
    partitionKey,
    operations,
    cancellationToken: ct);
```

## See Also

- [Data Providers Overview](./index.md) — Architecture and core abstractions
- [Cosmos DB Provider](./cosmosdb.md) — Azure cloud-native alternative
- [Firestore Provider](./firestore.md) — Google Cloud alternative
