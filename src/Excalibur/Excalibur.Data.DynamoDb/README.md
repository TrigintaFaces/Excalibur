# Excalibur.Data.DynamoDb

AWS DynamoDB data provider implementation for Excalibur cloud-native data access.

## Overview

This package provides a cloud-native data access implementation for AWS DynamoDB, implementing `ICloudNativePersistenceProvider` from `Excalibur.Data.Abstractions`.

## Features

- **Document Operations**: Full CRUD support for DynamoDB documents
- **Batch Operations**: Efficient transactional batch creates and replaces using `TransactWriteItems`
- **Query Support**: Partition key and sort key queries with pagination
- **Change Feed**: DynamoDB Streams integration for real-time change data capture
- **Health Checks**: Built-in health check for monitoring connectivity
- **Resilience**: Configurable retry policies for transient failures

## Installation

```bash
dotnet add package Excalibur.Data.DynamoDb
```

## Configuration

### Using Options Action

```csharp
services.AddDynamoDb(options =>
{
    options.Region = "us-east-1";
    options.DefaultTableName = "MyTable";
    options.DefaultPartitionKeyAttribute = "pk";
    options.DefaultSortKeyAttribute = "sk";
});
```

### Using Configuration Section

```csharp
services.AddDynamoDb(Configuration.GetSection("DynamoDb"));
```

### appsettings.json

```json
{
  "DynamoDb": {
    "Region": "us-east-1",
    "DefaultTableName": "MyTable",
    "DefaultPartitionKeyAttribute": "pk",
    "DefaultSortKeyAttribute": "sk",
    "UseConsistentReads": false,
    "TimeoutInSeconds": 30,
    "MaxRetryAttempts": 3
  }
}
```

### Using Existing Client

If you already have an `IAmazonDynamoDB` client configured:

```csharp
services.AddSingleton<IAmazonDynamoDB>(sp => new AmazonDynamoDBClient());
services.AddDynamoDbWithClient(options =>
{
    options.DefaultTableName = "MyTable";
});
```

## Configuration Options

### Connection Settings

| Option | Default | Description |
|--------|---------|-------------|
| `Name` | `DynamoDb` | Provider instance name for identification |
| `Region` | null | AWS region (e.g., "us-east-1") |
| `ServiceUrl` | null | Custom service URL (for local development) |
| `AccessKey` | null | AWS access key (optional, uses default credentials if not set) |
| `SecretKey` | null | AWS secret key (optional, uses default credentials if not set) |
| `DefaultTableName` | Required | Default table for operations |

### Table Key Settings

| Option | Default | Description |
|--------|---------|-------------|
| `DefaultPartitionKeyAttribute` | `pk` | Partition key attribute name |
| `DefaultSortKeyAttribute` | `sk` | Sort key attribute name |

### Performance Settings

| Option | Default | Description |
|--------|---------|-------------|
| `UseConsistentReads` | `false` | Enable strongly consistent reads by default |
| `TimeoutInSeconds` | `30` | Request timeout |
| `MaxRetryAttempts` | `3` | Maximum retry attempts for transient failures |
| `ReadCapacityUnits` | null | On-demand scaling hint for reads |
| `WriteCapacityUnits` | null | On-demand scaling hint for writes |

### DynamoDB Streams Settings

| Option | Default | Description |
|--------|---------|-------------|
| `EnableStreams` | `false` | Enable DynamoDB Streams for change data capture |
| `StreamViewType` | `NEW_AND_OLD_IMAGES` | What data to include in stream records |

**Stream View Types:**
- `KEYS_ONLY` - Only the key attributes
- `NEW_IMAGE` - The entire item after modification
- `OLD_IMAGE` - The entire item before modification
- `NEW_AND_OLD_IMAGES` - Both the new and old item images

## Usage

### Basic Operations

```csharp
public class MyService
{
    private readonly ICloudNativePersistenceProvider _provider;

    public MyService(ICloudNativePersistenceProvider provider)
    {
        _provider = provider;
    }

    public async Task<Order?> GetOrderAsync(string customerId, string orderId)
    {
        return await _provider.GetByIdAsync<Order>(
            orderId,
            new PartitionKey(customerId));
    }

    public async Task CreateOrderAsync(Order order)
    {
        await _provider.CreateAsync(
            order,
            new PartitionKey(order.CustomerId));
    }

    public async Task<IReadOnlyList<Order>> GetCustomerOrdersAsync(string customerId)
    {
        return await _provider.QueryAsync<Order>(new PartitionKey(customerId));
    }
}
```

### Batch Operations

```csharp
// Batch create
var items = new[] { order1, order2, order3 };
await _provider.BatchCreateAsync(
    items,
    item => new PartitionKey(item.CustomerId));

// Batch replace with optimistic concurrency
await _provider.BatchReplaceAsync(
    items,
    item => new PartitionKey(item.CustomerId),
    item => item.ETag);
```

### Change Feed (DynamoDB Streams)

```csharp
var subscription = await _provider.CreateChangeFeedSubscriptionAsync<Order>(
    "Orders",
    new ChangeFeedOptions
    {
        StartPosition = ChangeFeedStartPosition.Beginning,
        MaxBatchSize = 100
    });

await foreach (var change in subscription.ReadChangesAsync(cancellationToken))
{
    Console.WriteLine($"{change.EventType}: {change.DocumentId}");

    if (change.Document != null)
    {
        // Process the changed document
    }
}
```

> **Note**: DynamoDB Streams must be enabled on the table for change feed to work.

### Health Check

```csharp
services.AddHealthChecks()
    .AddCheck<DynamoDbHealthCheck>("dynamodb");
```

## Local Development

For local development with DynamoDB Local:

```csharp
services.AddDynamoDb(options =>
{
    options.ServiceUrl = "http://localhost:8000";
    options.DefaultTableName = "LocalTable";
});
```

## Key Patterns

This provider follows the single-table design pattern commonly used with DynamoDB:

- **Partition Key (pk)**: Groups related items together for efficient access
- **Sort Key (sk)**: Provides ordering within a partition and enables range queries

Documents are serialized to/from DynamoDB using JSON, with partition and sort keys automatically managed.

## Transactional Support

Batch operations use DynamoDB's `TransactWriteItems` for atomic, all-or-nothing operations:

```csharp
// All items are created atomically
await _provider.BatchCreateAsync(items, item => new PartitionKey(item.Id));
```

## Error Handling

The provider maps DynamoDB exceptions to `CloudOperationResult<T>`:

```csharp
var result = await _provider.UpdateAsync(order, partitionKey, etag);

if (!result.Success)
{
    switch (result.StatusCode)
    {
        case 400:
            Console.WriteLine("Validation error or bad request");
            break;
        case 404:
            Console.WriteLine("Item not found");
            break;
        case 409:
            Console.WriteLine("Conditional check failed (item already exists)");
            break;
        case 412:
            Console.WriteLine("Precondition failed (ETag/version mismatch)");
            break;
        case 429:
            Console.WriteLine("Provisioned throughput exceeded - retry with backoff");
            break;
    }
}
```

### DynamoDB-Specific Exceptions

| Exception | HTTP Status | Description |
|-----------|-------------|-------------|
| `ConditionalCheckFailedException` | 409/412 | Condition expression evaluated to false |
| `ProvisionedThroughputExceededException` | 429 | Exceeded provisioned capacity |
| `ResourceNotFoundException` | 404 | Table or item not found |
| `TransactionCanceledException` | 409 | Transaction failed (conflicts) |
| `ItemCollectionSizeLimitExceededException` | 400 | Item collection too large (10GB limit) |

## Authentication

DynamoDB supports multiple authentication methods:

### IAM Role (Recommended for AWS)

```csharp
// Uses default credentials chain (EC2 instance profile, ECS task role, etc.)
services.AddDynamoDb(options =>
{
    options.Region = "us-east-1";
    options.DefaultTableName = "MyTable";
});
```

### Explicit Credentials

```csharp
services.AddDynamoDb(options =>
{
    options.Region = "us-east-1";
    options.AccessKey = "YOUR_ACCESS_KEY";
    options.SecretKey = "YOUR_SECRET_KEY";
    options.DefaultTableName = "MyTable";
});
```

### Environment Variables

```bash
export AWS_ACCESS_KEY_ID="your-access-key"
export AWS_SECRET_ACCESS_KEY="your-secret-key"
export AWS_REGION="us-east-1"
```

## Best Practices

1. **Use Single-Table Design** - Store related entities in one table using partition/sort key patterns
2. **Design for Access Patterns** - Model your data based on query requirements, not relational concepts
3. **Enable Consistent Reads Only When Needed** - Eventually consistent reads use half the RCUs
4. **Use Transactional Batches** for atomic operations across items
5. **Monitor Consumed Capacity** - Track `ConsumedCapacity` in responses
6. **Enable Streams for Event-Driven** architectures and change data capture
7. **Use Exponential Backoff** for `ProvisionedThroughputExceededException`
8. **Consider On-Demand Mode** for unpredictable workloads

## License

See LICENSE files in the repository root.
