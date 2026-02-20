# Excalibur.Data.Firestore

Google Cloud Firestore data provider implementation for Excalibur cloud-native data access.

## Installation

```bash
dotnet add package Excalibur.Data.Firestore
```

## Quick Start

### Configuration

```csharp
// In Program.cs or Startup.cs
services.AddFirestore(options =>
{
    options.ProjectId = "your-gcp-project-id";
    options.DatabaseId = "(default)"; // Optional, defaults to (default)
    options.DefaultCollection = "your-collection";
});

// Or using configuration
services.AddFirestore(configuration.GetSection("Firestore"));
```

### appsettings.json

```json
{
  "Firestore": {
    "ProjectId": "your-gcp-project-id",
    "DatabaseId": "(default)",
    "DefaultCollection": "your-collection",
    "UseEmulator": false,
    "EmulatorHost": "localhost:8080"
  }
}
```

## Features

### CRUD Operations

```csharp
public class OrderService
{
    private readonly FirestorePersistenceProvider _provider;

    public OrderService(FirestorePersistenceProvider provider)
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

    // Firestore uses collection path as partition key
    var result = await _provider.QueryAsync<Order>(
        "status = @status",
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

### Real-time Change Listener

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

    await subscription.StartAsync(cancellationToken);

    await foreach (var change in subscription.ReadChangesAsync(cancellationToken))
    {
        Console.WriteLine($"Change detected: {change.DocumentId}, Type: {change.EventType}");
        // Process the change
    }
}
```

### Optimistic Concurrency

```csharp
public async Task<CloudOperationResult<Order>> SafeUpdateAsync(
    Order order,
    string etag)
{
    var partitionKey = new PartitionKey(order.TenantId, "/tenantId");

    // ETag is derived from Firestore UpdateTime
    var result = await _provider.UpdateAsync(order, partitionKey, etag);

    if (!result.Success && result.StatusCode == 412)
    {
        // Precondition failed - document was modified since etag
        Console.WriteLine("Concurrent modification detected");
    }

    return result;
}
```

### Health Checks

```csharp
services.AddHealthChecks()
    .AddCheck<FirestoreHealthCheck>(
        name: "firestore",
        tags: new[] { "database", "gcp" });
```

## Configuration Options

### Connection Settings

| Option | Description | Default |
|--------|-------------|---------|
| `Name` | Provider instance name for identification | `Firestore` |
| `ProjectId` | GCP project ID | Required* |
| `DefaultCollection` | Default collection name for operations | - |
| `CredentialsPath` | Path to service account JSON file | - |
| `CredentialsJson` | Service account JSON content (for containers) | - |

*Required unless using `EmulatorHost`.

### Performance Settings

| Option | Description | Default |
|--------|-------------|---------|
| `TimeoutInSeconds` | Operation timeout | `30` |
| `MaxRetryAttempts` | Maximum retry attempts for transient failures | `3` |

### Emulator Settings

| Option | Description | Default |
|--------|-------------|---------|
| `EmulatorHost` | Firestore emulator host:port (e.g., "localhost:8080") | - |

## Error Handling

The provider returns `CloudOperationResult<T>` with status codes for error handling:

```csharp
var result = await _provider.UpdateAsync(order, partitionKey, etag);

if (!result.Success)
{
    switch (result.StatusCode)
    {
        case 404:
            Console.WriteLine("Document not found");
            break;
        case 409:
            Console.WriteLine("Document already exists");
            break;
        case 412:
            Console.WriteLine("Precondition failed - document was modified (ETag mismatch)");
            break;
        case 429:
            Console.WriteLine("Resource exhausted - too many requests");
            break;
        case 503:
            Console.WriteLine("Service unavailable - retry with backoff");
            break;
    }
}
```

### Firestore-Specific Errors

| gRPC Status | HTTP Code | Description |
|-------------|-----------|-------------|
| `NOT_FOUND` | 404 | Document or collection not found |
| `ALREADY_EXISTS` | 409 | Document already exists (on create) |
| `FAILED_PRECONDITION` | 412 | Precondition failed (ETag mismatch) |
| `RESOURCE_EXHAUSTED` | 429 | Quota exceeded |
| `ABORTED` | 409 | Transaction aborted (retry) |
| `UNAVAILABLE` | 503 | Service temporarily unavailable |

## Authentication

Firestore uses Google Cloud authentication. Configure one of:

### Application Default Credentials

```bash
# Local development
gcloud auth application-default login

# In GCP (GKE, Cloud Run, etc.)
# Automatically uses service account
```

### Service Account Key

```csharp
services.AddFirestore(options =>
{
    options.ProjectId = "your-project";
    options.CredentialPath = "/path/to/service-account.json";
});
```

### Environment Variable

```bash
export GOOGLE_APPLICATION_CREDENTIALS="/path/to/service-account.json"
```

### Container/Kubernetes Deployment

For containerized environments where file paths are impractical:

```csharp
services.AddFirestore(options =>
{
    options.ProjectId = "your-project";
    // JSON content from Kubernetes secret or environment variable
    options.CredentialsJson = Environment.GetEnvironmentVariable("GCP_CREDENTIALS_JSON");
});
```

## Collection Structure

Firestore uses a hierarchical document model:

```csharp
// Root collection
var partitionKey = new PartitionKey("orders", "/collection");

// Subcollection
var subPartitionKey = new PartitionKey("orders/order-123/items", "/collection");
```

## Best Practices

1. **Use Composite Indexes** for complex queries (required for multiple equality/range filters)
2. **Monitor Read/Write Quotas** via GCP Console - Firestore has per-second limits
3. **Use Transactions** for atomic operations across documents
4. **Enable Firestore Emulator** for local development and testing
5. **Configure Security Rules** in production - never rely on client-side validation
6. **Design Document Structure Carefully** - Firestore charges per document read
7. **Use Batch Writes** for multiple operations (max 500 per batch)
8. **Prefer Shallow Hierarchies** - deep subcollections increase complexity
9. **Cache Frequently-Read Data** - Firestore charges per read operation
10. **Use Appropriate Data Types** - leverage Firestore's native types (timestamps, geo points)

## Emulator Support

For local development and testing:

```bash
# Start Firestore emulator
gcloud emulators firestore start --host-port=localhost:8080
```

```csharp
services.AddFirestore(options =>
{
    options.ProjectId = "test-project";
    options.UseEmulator = true;
    options.EmulatorHost = "localhost:8080";
});
```

## License

See LICENSE files in the repository root.
