# Excalibur.EventSourcing.CosmosDb

Azure Cosmos DB event store implementation for Excalibur event sourcing.

## Features

- **Partition-aware event storage** using `{aggregateType}:{aggregateId}` as partition key
- **Transactional batch support** for atomic event appending
- **Change feed integration** for event streaming
- **Optimistic concurrency** with version-based conflict detection
- **Auto-provisioning** of containers with configurable throughput

## Installation

```bash
dotnet add package Excalibur.EventSourcing.CosmosDb
```

## Configuration

```csharp
services.AddCosmosDbEventStore(options =>
{
    options.EventsContainerName = "events";
    options.PartitionKeyPath = "/streamId";
    options.UseTransactionalBatch = true;
    options.MaxBatchSize = 100;
    options.ChangeFeedPollIntervalMs = 1000;
    options.CreateContainerIfNotExists = true;
    options.ContainerThroughput = 400;
});
```

Or via configuration:

```csharp
services.AddCosmosDbEventStore(configuration.GetSection("CosmosDbEventStore"));
```

## Usage

The package registers both `IEventStore` and `ICloudNativeEventStore`:

```csharp
public class MyService
{
    private readonly IEventStore _eventStore;

    public MyService(IEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task AppendEventsAsync(
        string aggregateId,
        IEnumerable<IDomainEvent> events,
        long expectedVersion,
        CancellationToken ct)
    {
        var result = await _eventStore.AppendEventsAsync(
            aggregateId,
            "MyAggregate",
            events.ToList(),
            expectedVersion,
            ct);

        if (!result.Success)
        {
            // Handle concurrency conflict or failure
        }
    }
}
```

## Cloud-Native Features

For partition-aware operations, use `ICloudNativeEventStore`:

```csharp
public class CloudNativeService
{
    private readonly ICloudNativeEventStore _eventStore;

    public CloudNativeService(ICloudNativeEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task LoadWithPartitionAsync(CancellationToken ct)
    {
        var partition = new PartitionKey("MyAggregate", "aggregate-123");
        var result = await _eventStore.LoadEventsAsync(partition, 0, ct);
        // Process events...
    }
}
```

## Change Feed Subscription

Subscribe to event changes for projections or other event handlers:

```csharp
await using var subscription = await _eventStore.SubscribeToChangesAsync(ct);
await subscription.StartAsync(ct);

await foreach (var change in subscription.ReadChangesAsync(ct))
{
    // Process event change
    Console.WriteLine($"Event: {change.Document?.EventType}");
}
```

## Requirements

- .NET 9.0+
- Azure Cosmos DB account
- `Excalibur.Data.CosmosDb` for connection management

## License

See LICENSE files in repository root.
