# Excalibur.EventSourcing.DynamoDb

AWS DynamoDB event store implementation for Excalibur event sourcing.

## Features

- **Partition-aware event storage** using `{aggregateType}:{aggregateId}` as partition key
- **Transactional write support** for atomic event appending (up to 100 events)
- **DynamoDB Streams integration** for event streaming
- **Optimistic concurrency** with conditional writes
- **Auto-provisioning** of tables with configurable capacity modes

## Installation

```bash
dotnet add package Excalibur.EventSourcing.DynamoDb
```

## Configuration

```csharp
services.AddDynamoDbEventStore(options =>
{
    options.EventsTableName = "Events";
    options.PartitionKeyAttribute = "pk";
    options.SortKeyAttribute = "sk";
    options.UseTransactionalWrite = true;
    options.MaxBatchSize = 100;
    options.StreamsPollIntervalMs = 1000;
    options.CreateTableIfNotExists = true;
    options.UseOnDemandCapacity = true;
    options.EnableStreams = true;
});
```

Or via configuration:

```csharp
services.AddDynamoDbEventStore(configuration.GetSection("DynamoDbEventStore"));
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
        var result = await _eventStore.AppendAsync(
            aggregateId,
            "MyAggregate",
            events,
            expectedVersion,
            ct);

        if (!result.Success)
        {
            // Handle concurrency conflict or failure
        }
    }
}
```

## DynamoDB Streams Subscription

Subscribe to event changes for projections or other event handlers:

```csharp
var subscription = await _eventStore.SubscribeToChangesAsync(null, ct);
await subscription.StartAsync(ct);

await foreach (var change in subscription.ReadChangesAsync(ct))
{
    // Process event change
    Console.WriteLine($"Event: {change.Document?.EventType}");
}
```

## Table Schema

The event store uses the following DynamoDB table schema:

| Attribute | Type | Description |
|-----------|------|-------------|
| pk | String (Partition Key) | Stream ID: `{aggregateType}:{aggregateId}` |
| sk | Number (Sort Key) | Event version |
| eventId | String | Unique event identifier |
| aggregateId | String | Aggregate identifier |
| aggregateType | String | Aggregate type name |
| eventType | String | Event type name |
| version | Number | Event version |
| timestamp | String | ISO 8601 timestamp |
| eventData | String | Base64-encoded event data |
| metadata | String | JSON-encoded metadata |
| isDispatched | Boolean | Outbox dispatch status |

## Requirements

- .NET 9.0+
- AWS DynamoDB access (local or cloud)
- `Excalibur.Data.DynamoDb` for AWS SDK setup

## License

See LICENSE files in repository root.
