# Excalibur.Data.CosmosDb

Azure Cosmos DB data provider implementation for Excalibur cloud-native data access.

## Installation

```bash
dotnet add package Excalibur.Data.CosmosDb
```

## Quick Start

### Configuration

```csharp
// In Program.cs or Startup.cs
services.AddCosmosDb(options =>
{
    options.AccountEndpoint = "https://your-account.documents.azure.com:443/";
    options.AccountKey = "your-account-key";
    options.DatabaseName = "your-database";
    options.DefaultContainerName = "your-container";
});

// Or using configuration
services.AddCosmosDb(configuration.GetSection("CosmosDb"));
```

### appsettings.json

```json
{
  "CosmosDb": {
    "AccountEndpoint": "https://your-account.documents.azure.com:443/",
    "AccountKey": "your-account-key",
    "DatabaseName": "your-database",
    "DefaultContainerName": "your-container",
    "DefaultPartitionKeyPath": "/tenantId",
    "UseDirectMode": true,
    "MaxRetryAttempts": 9
  }
}
```

## Features

### CRUD Operations

```csharp
public class OrderService
{
    private readonly CosmosDbPersistenceProvider _provider;

    public OrderService(CosmosDbPersistenceProvider provider)
    {
        _provider = provider;
    }

    public async Task<Order?> GetOrderAsync(string orderId, string tenantId)
    {
        var partitionKey = new PartitionKey(tenantId, "/tenantId");
        return await _provider.GetByIdAsync<Order>(orderId, partitionKey);
    }

    public async Task<CloudOperationResult<Order>> CreateOrderAsync(Order order)
    {
        var partitionKey = new PartitionKey(order.TenantId, "/tenantId");
        return await _provider.CreateAsync(order, partitionKey);
    }

    public async Task<CloudOperationResult<Order>> UpdateOrderAsync(Order order, string etag)
    {
        var partitionKey = new PartitionKey(order.TenantId, "/tenantId");
        return await _provider.UpdateAsync(order, partitionKey, etag);
    }
}
```

### Queries

```csharp
public async Task<IReadOnlyList<Order>> GetOrdersByStatusAsync(
    string tenantId,
    string status)
{
    var partitionKey = new PartitionKey(tenantId, "/tenantId");
    var result = await _provider.QueryAsync<Order>(
        "SELECT * FROM c WHERE c.status = @status",
        partitionKey,
        new Dictionary<string, object> { ["status"] = status });

    return result.Documents;
}
```

### Transactional Batches

```csharp
public async Task<CloudBatchResult> ProcessOrderBatchAsync(
    string tenantId,
    Order order,
    OrderLine[] lines)
{
    var partitionKey = new PartitionKey(tenantId, "/tenantId");
    var operations = new List<ICloudBatchOperation>
    {
        new CloudBatchCreateOperation(order.Id, order)
    };

    foreach (var line in lines)
    {
        operations.Add(new CloudBatchCreateOperation(line.Id, line));
    }

    return await _provider.ExecuteBatchAsync(partitionKey, operations);
}
```

### Change Feed

```csharp
public async Task SubscribeToChangesAsync(CancellationToken cancellationToken)
{
    var subscription = await _provider.CreateChangeFeedSubscriptionAsync<Order>(
        "orders",
        new ChangeFeedOptions
        {
            StartPosition = ChangeFeedStartPosition.Now,
            MaxBatchSize = 100
        },
        cancellationToken);

    await foreach (var change in subscription.ReadChangesAsync(cancellationToken))
    {
        Console.WriteLine($"Change detected: {change.DocumentId}, Type: {change.EventType}");
        // Process the change
    }
}
```

### Session Consistency

```csharp
public async Task<Order?> ReadYourWriteAsync(Order order)
{
    var partitionKey = new PartitionKey(order.TenantId, "/tenantId");

    // Create returns session token
    var createResult = await _provider.CreateAsync(order, partitionKey);

    // Read with session consistency
    var consistencyOptions = ConsistencyOptions.WithSession(createResult.SessionToken!);
    return await _provider.GetByIdAsync<Order>(
        order.Id,
        partitionKey,
        consistencyOptions);
}
```

### Health Checks

```csharp
services.AddHealthChecks()
    .AddCosmosDb(
        name: "cosmosdb",
        tags: new[] { "database", "azure" });
```

## Configuration Options

### Connection Settings

| Option | Description | Default |
|--------|-------------|---------|
| `AccountEndpoint` | Cosmos DB account endpoint URI | Required* |
| `AccountKey` | Cosmos DB account key | Required* |
| `ConnectionString` | Alternative to endpoint + key | - |
| `DatabaseName` | Default database name | Required |
| `DefaultContainerName` | Default container name | - |
| `DefaultPartitionKeyPath` | Partition key path | `/id` |
| `Name` | Provider instance name for identification | `CosmosDb` |
| `ApplicationName` | Application name for diagnostics | - |

*Either `ConnectionString` OR both `AccountEndpoint` and `AccountKey` must be provided.

### Performance Settings

| Option | Description | Default |
|--------|-------------|---------|
| `UseDirectMode` | Use direct connection mode (lower latency) | `true` |
| `MaxConnectionsPerEndpoint` | Maximum connections per endpoint | `50` |
| `RequestTimeoutInSeconds` | Request timeout | `60` |
| `IdleConnectionTimeoutInSeconds` | Idle connection timeout | `600` |
| `EnableTcpConnectionEndpointRediscovery` | Enable TCP connection reuse | `true` |
| `AllowBulkExecution` | Enable bulk operations | `false` |
| `BulkExecutionMaxDegreeOfParallelism` | Bulk operation parallelism | `25` |

### Consistency and Reliability

| Option | Description | Default |
|--------|-------------|---------|
| `ConsistencyLevel` | Default consistency level | Account default |
| `MaxRetryAttempts` | Max retries on rate limiting (429) | `9` |
| `MaxRetryWaitTimeInSeconds` | Max wait between retries | `30` |
| `EnableContentResponseOnWrite` | Return document on write (set `false` to reduce RU) | `true` |
| `PreferredRegions` | Preferred Azure regions for geo-redundancy | - |

## Partition Key Strategies

### Tenant-Based Partitioning

```csharp
// Single value partition key
var pk = new PartitionKey("tenant-123", "/tenantId");

// Query within tenant
var orders = await _provider.QueryAsync<Order>(
    "SELECT * FROM c WHERE c.status = 'pending'",
    pk);
```

### Composite Partition Keys

```csharp
// For hierarchical partitioning
var pk = new CompositePartitionKey("/pk", "#", "TENANT", tenantId, "ORDER");
```

## Local Development

For local development with Azure Cosmos DB Emulator:

```bash
# Install and start Cosmos DB Emulator
# Windows: Use the installer from Azure Portal
# Docker: docker run -p 8081:8081 -p 10251-10255:10251-10255 mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
```

```csharp
services.AddCosmosDb(options =>
{
    options.AccountEndpoint = "https://localhost:8081/";
    options.AccountKey = "x";
    options.DatabaseName = "test-database";
});
```

> **Note**: The emulator uses a well-known key for development. Never use this in production.

## Error Handling

The provider returns `CloudOperationResult<T>` with status codes for handling errors:

```csharp
var result = await _provider.UpdateAsync(order, partitionKey, etag);

switch (result.StatusCode)
{
    case 200:
        Console.WriteLine($"Success. RU charge: {result.RequestCharge}");
        break;
    case 404:
        Console.WriteLine("Document not found");
        break;
    case 412:
        Console.WriteLine("Precondition failed - ETag mismatch (concurrent modification)");
        break;
    case 429:
        Console.WriteLine("Rate limited - retry with backoff");
        break;
}
```

### Common Status Codes

| Code | Description |
|------|-------------|
| 200 | Success |
| 201 | Created |
| 204 | No content (delete success) |
| 400 | Bad request (invalid query/document) |
| 404 | Document or container not found |
| 409 | Conflict (document already exists) |
| 412 | Precondition failed (ETag mismatch) |
| 429 | Rate limited (exceeded RU/s) |
| 503 | Service unavailable |

## Best Practices

1. **Use Direct Mode** for lowest latency in production
2. **Enable Session Consistency** for read-your-writes scenarios
3. **Monitor Request Charges** (RU/s) via `CloudOperationResult.RequestCharge`
4. **Use Transactional Batches** for atomic operations within a partition
5. **Configure Preferred Regions** for geo-redundant deployments
6. **Set `EnableContentResponseOnWrite = false`** to reduce RU consumption when you don't need the response
7. **Enable Bulk Execution** for high-throughput batch operations
8. **Use connection pooling** with appropriate `MaxConnectionsPerEndpoint` for your workload

## License

See LICENSE files in the repository root.
