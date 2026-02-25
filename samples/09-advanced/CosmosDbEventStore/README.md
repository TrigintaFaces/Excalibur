# Cosmos DB Event Store Sample

This sample demonstrates how to use **Azure Cosmos DB** as an event store with **Excalibur.EventSourcing**.

## Features

- **Cosmos DB Event Store** - Persistent event storage with global distribution
- **Partition Key Strategy** - Events partitioned by stream ID for optimal performance
- **Transactional Batch** - Atomic writes for multiple events
- **Change Feed Support** - Subscribe to event changes for projections
- **Local Development** - Works with Cosmos DB Emulator

## Prerequisites

### Option 1: Cosmos DB Emulator (Local Development)

```bash
# Install the emulator
winget install Microsoft.Azure.CosmosEmulator

# Or download from:
# https://aka.ms/cosmosdb-emulator
```

The emulator runs at `https://localhost:8081/` with a well-known key.

### Option 2: Azure Cosmos DB (Cloud)

1. Create a Cosmos DB account in the Azure Portal
2. Select "NoSQL" API
3. Copy the connection string to `appsettings.json`

## Running the Sample

```bash
# Build
dotnet build

# Run (will use emulator if no connection string configured)
dotnet run
```

## Configuration

### appsettings.json

```json
{
  "CosmosDb": {
    "ConnectionString": "your-connection-string",
    "DatabaseName": "events",
    "ContainerName": "events"
  }
}
```

### Environment Variables

```bash
# Override connection string
export CosmosDb__ConnectionString="AccountEndpoint=https://..."
```

## Partition Key Strategy

Events are partitioned by **stream ID** (`aggregateType:aggregateId`):

```
/streamId = "BankAccountAggregate:12345678-1234-1234-1234-123456789abc"
```

### Why Stream ID?

| Benefit | Description |
|---------|-------------|
| **Strong Consistency** | All events for an aggregate are in one partition |
| **Efficient Queries** | Loading an aggregate reads one partition |
| **Transactional Writes** | Multiple events can be written atomically |
| **Optimistic Concurrency** | Version conflicts detected per stream |

### Alternative: Partition by Aggregate Type

```csharp
options.PartitionKeyPath = "/aggregateType";
```

| Benefit | Trade-off |
|---------|-----------|
| Better for cross-aggregate queries | Weaker consistency per aggregate |
| Useful for reporting projections | Transactions limited to same type |

## Code Walkthrough

### 1. Configure Cosmos DB Client

```csharp
services.AddSingleton(_ =>
{
    var options = new CosmosClientOptions
    {
        ConnectionMode = ConnectionMode.Direct,
        SerializerOptions = new CosmosSerializationOptions
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        }
    };

    return new CosmosClient(connectionString, options);
});
```

### 2. Add Cosmos DB Event Store

```csharp
services.AddCosmosDbEventStore(options =>
{
    options.EventsContainerName = "events";
    options.PartitionKeyPath = "/streamId";
    options.CreateContainerIfNotExists = true;
    options.ContainerThroughput = 400;
    options.UseTransactionalBatch = true;
});
```

### 3. Register Aggregates

```csharp
services.AddExcaliburEventSourcing(builder =>
{
    builder.AddRepository<BankAccountAggregate, Guid>(
        id => new BankAccountAggregate(id));
});
```

### 4. Use the Repository

```csharp
var repository = provider.GetRequiredService<
    IEventSourcedRepository<BankAccountAggregate, Guid>>();

// Create aggregate
var account = BankAccountAggregate.Open(
    Guid.NewGuid(), "Jane Doe", "Savings", 1000m);
await repository.SaveAsync(account, cancellationToken);

// Load and modify
var loaded = await repository.LoadAsync(accountId, cancellationToken);
loaded.Deposit(500m, "Paycheck");
await repository.SaveAsync(loaded, cancellationToken);
```

## Event Document Structure

Events are stored as JSON documents:

```json
{
  "id": "BankAccountAggregate:abc123:1",
  "streamId": "BankAccountAggregate:abc123",
  "eventId": "evt-456",
  "aggregateId": "abc123",
  "aggregateType": "BankAccountAggregate",
  "eventType": "AccountOpened",
  "version": 1,
  "timestamp": "2026-01-22T12:00:00Z",
  "eventData": "...",
  "metadata": "...",
  "isDispatched": false,
  "_etag": "..."
}
```

## Change Feed Subscription

For building projections from events:

```csharp
var eventStore = provider.GetRequiredService<ICloudNativeEventStore>();

await using var subscription = await eventStore.SubscribeToChangesAsync();

await foreach (var events in subscription.GetChangesAsync(cancellationToken))
{
    foreach (var evt in events)
    {
        // Update read model / projection
        Console.WriteLine($"Event: {evt.EventType} v{evt.Version}");
    }
}
```

## Performance Considerations

### Request Units (RU/s)

| Operation | Typical RU Cost |
|-----------|-----------------|
| Point read (by ID) | 1 RU |
| Write single event | 5-10 RU |
| Query by stream ID | 2-5 RU |
| Cross-partition query | 50+ RU |

### Throughput Recommendations

| Scenario | Throughput |
|----------|------------|
| Development | 400 RU/s (minimum) |
| Low traffic | 1,000 RU/s |
| Production | 10,000+ RU/s or autoscale |

### Best Practices

1. **Use Direct mode** in production for lower latency
2. **Enable autoscale** for variable workloads
3. **Set TTL** for temporary data (e.g., saga state)
4. **Monitor RU consumption** in Azure Portal

## Troubleshooting

### "Unable to connect to localhost:8081"

The emulator is not running. Start it from:
- Windows: Start Menu > Azure Cosmos DB Emulator
- Command line: `%ProgramFiles%\Azure Cosmos DB Emulator\CosmosDB.Emulator.exe`

### "Request rate is large"

You've exceeded the provisioned throughput. Options:
1. Increase RU/s in Azure Portal
2. Enable autoscale
3. Add retry logic with backoff

### "Certificate error"

The emulator uses a self-signed certificate. For development:
```csharp
options.HttpClientFactory = () =>
{
    var handler = new HttpClientHandler();
    handler.ServerCertificateCustomValidationCallback =
        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return new HttpClient(handler);
};
```

## Related Samples

- [SQL Server Event Store](../SqlServerEventStore/) - Alternative provider
- [Snapshot Strategies](../SnapshotStrategies/) - Performance optimization
- [Event Upcasting](../EventUpcasting/) - Schema evolution
- [ExcaliburCqrs](../../01-getting-started/ExcaliburCqrs/) - CQRS basics

## Learn More

- [Azure Cosmos DB Documentation](https://learn.microsoft.com/azure/cosmos-db/)
- [Cosmos DB Emulator](https://learn.microsoft.com/azure/cosmos-db/local-emulator)
- [Event Sourcing Patterns](https://learn.microsoft.com/azure/architecture/patterns/event-sourcing)
