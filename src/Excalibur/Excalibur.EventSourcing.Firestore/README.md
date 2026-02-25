# Excalibur.EventSourcing.Firestore

Google Cloud Firestore event store implementation for Excalibur event sourcing.

## Features

- **Document-based event storage** using `{aggregateType}:{aggregateId}` as stream ID
- **Transactional writes** for atomic event appending with optimistic concurrency
- **Real-time listeners** for event change subscriptions
- **Optimistic concurrency** with version-based conflict detection
- **Emulator support** for local development

## Installation

```bash
dotnet add package Excalibur.EventSourcing.Firestore
```

## Configuration

```csharp
services.AddFirestoreEventStore(options =>
{
    options.ProjectId = "my-gcp-project";
    options.EventsCollectionName = "events";
    options.CredentialsPath = "/path/to/credentials.json";
    options.UseBatchedWrites = true;
    options.MaxBatchSize = 500;
    options.CreateCollectionIfNotExists = true;
});
```

Or via configuration:

```csharp
services.AddFirestoreEventStore(configuration.GetSection("FirestoreEventStore"));
```

### Using the Emulator

For local development with the Firestore emulator:

```csharp
services.AddFirestoreEventStore(options =>
{
    options.EmulatorHost = "localhost:8080";
    options.EventsCollectionName = "events";
});
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

## Real-time Subscription

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

## Document Schema

The event store uses the following Firestore document schema:

| Field | Type | Description |
|-------|------|-------------|
| streamId | String | Stream ID: `{aggregateType}:{aggregateId}` |
| eventId | String | Unique event identifier |
| aggregateId | String | Aggregate identifier |
| aggregateType | String | Aggregate type name |
| eventType | String | Event type name |
| version | Number | Event version |
| timestamp | String | ISO 8601 timestamp |
| eventData | String | Base64-encoded event data |
| metadata | String | Base64-encoded metadata |
| isDispatched | Boolean | Outbox dispatch status |

## Requirements

- .NET 9.0+
- Google Cloud Firestore access (or local emulator)
- `Excalibur.Data.Firestore` for GCP SDK setup

## License

See LICENSE files in repository root.
