# DynamoDB Data Provider Sample

Demonstrates all framework DynamoDB capabilities provided by the `Excalibur.Data.DynamoDb` package.

## Capabilities Demonstrated

| # | Capability | API |
|---|-----------|-----|
| 1 | DI Registration | `AddDynamoDb(Action<DynamoDbOptions>)`, `AddDynamoDb(IConfiguration)`, `AddDynamoDb(IConfiguration, string)`, `AddDynamoDbWithClient(Action<DynamoDbOptions>)` |
| 2 | CRUD Operations | `CreateAsync`, `GetByIdAsync`, `UpdateAsync`, `DeleteAsync` via `ICloudNativePersistenceProvider` |
| 3 | Consistent Reads | `DynamoDbOptions.UseConsistentReads` + `ConsistencyOptions.Strong` per-operation override |
| 4 | DynamoDB Streams | `DynamoDbOptions.EnableStreams`, `StreamViewType`, `CreateChangeFeedSubscriptionAsync` |
| 5 | DAX Caching | `AddDynamoDbDaxCaching(Action<DaxCacheOptions>)` with `IDaxCacheProvider` |
| 6 | Health Checks | `DynamoDbHealthCheck` implementing `IHealthCheck` |
| 7 | Local Development | `DynamoDbConnectionOptions.ServiceUrl` pointing to DynamoDB Local |
| 8 | Query & Batch | `QueryAsync` with KeyConditionExpression, `ExecuteBatchAsync` with TransactWriteItems |
| 9 | GetService Escape Hatch | `ICloudNativePersistenceQueryOperations`, `ICloudNativePersistenceBatchOperations`, `ICloudNativePersistenceChangeFeed` |

## Prerequisites

### DynamoDB Local via Docker

```bash
docker run -d --name dynamodb-local -p 8000:8000 amazon/dynamodb-local
```

### Create the sample table

```bash
aws dynamodb create-table \
  --table-name ExcaliburSample \
  --attribute-definitions \
    AttributeName=pk,AttributeType=S \
    AttributeName=sk,AttributeType=S \
  --key-schema \
    AttributeName=pk,KeyType=HASH \
    AttributeName=sk,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST \
  --endpoint-url http://localhost:8000
```

## Running

```bash
dotnet run --project samples/14-data-providers/DynamoDb
```

## Configuration

The sample uses `appsettings.json` for configuration binding. Key settings:

```json
{
  "DynamoDb": {
    "DefaultTableName": "ExcaliburSample",
    "UseConsistentReads": true,
    "Connection": {
      "ServiceUrl": "http://localhost:8000",
      "Region": "us-east-1"
    }
  }
}
```

### For AWS deployment

Remove `ServiceUrl` and provide credentials via IAM role or explicit keys:

```json
{
  "DynamoDb": {
    "DefaultTableName": "MyTable",
    "Connection": {
      "Region": "us-east-1"
    }
  }
}
```

## DynamoDB Streams

When `EnableStreams` is `true`, the provider creates a DynamoDB Streams client alongside the main client. Stream view types:

- `NEW_AND_OLD_IMAGES` -- Both old and new item images (default)
- `NEW_IMAGE` -- Only the new item image
- `OLD_IMAGE` -- Only the old item image
- `KEYS_ONLY` -- Only the key attributes

## DAX Caching

DAX (DynamoDB Accelerator) provides microsecond-latency caching for DynamoDB reads. The framework registers an `IDaxCacheProvider` with configurable TTL and consistency levels.
