# Excalibur.Outbox.Firestore

Google Cloud Firestore implementation of the cloud-native outbox pattern for reliable message delivery.

## Features

- **Transactional outbox pattern** with partition-aware storage
- **Real-time listeners** for push-based message processing
- **Automatic TTL cleanup** for published messages (via scheduled Cloud Functions)
- **Serverless-friendly** - designed for Cloud Functions triggers

## Installation

```bash
dotnet add package Excalibur.Outbox.Firestore
```

## Configuration

```csharp
services.AddFirestoreOutboxStore(options =>
{
    options.ProjectId = "my-gcp-project";
    options.CollectionName = "outbox";
    options.DefaultTimeToLiveSeconds = 604800; // 7 days
});
```

Or via configuration:

```csharp
services.AddFirestoreOutboxStore(configuration.GetSection("FirestoreOutbox"));
```

### Local Development (Emulator)

```csharp
services.AddFirestoreOutboxStore(options =>
{
    options.EmulatorHost = "localhost:8080";
    options.CollectionName = "outbox";
});
```

### Service Account Credentials

```csharp
services.AddFirestoreOutboxStore(options =>
{
    options.ProjectId = "my-gcp-project";
    options.CredentialsPath = "/path/to/service-account.json";
    // OR
    options.CredentialsJson = "{ ... }";
});
```

## Usage

### Adding Messages to the Outbox

```csharp
public class MyService
{
    private readonly ICloudNativeOutboxStore _outbox;

    public MyService(ICloudNativeOutboxStore outbox)
    {
        _outbox = outbox;
    }

    public async Task PublishEventAsync(OrderPlaced @event, CancellationToken ct)
    {
        var message = new CloudOutboxMessage
        {
            MessageId = Guid.NewGuid().ToString(),
            MessageType = nameof(OrderPlaced),
            Payload = JsonSerializer.SerializeToUtf8Bytes(@event),
            AggregateId = @event.OrderId,
            AggregateType = "Order",
            PartitionKeyValue = @event.OrderId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var partitionKey = new PartitionKey(@event.OrderId);
        var result = await _outbox.AddAsync(message, partitionKey, ct);

        if (!result.Success)
        {
            // Handle failure
        }
    }
}
```

### Processing Messages with Real-time Listener

```csharp
// Subscribe to new outbox messages
var subscription = await _outbox.SubscribeToNewMessagesAsync(ct);

await foreach (var change in subscription.ReadChangesAsync(ct))
{
    if (change.Document != null)
    {
        // Publish to message broker
        await _messageBroker.PublishAsync(change.Document);

        // Mark as published
        await _outbox.MarkAsPublishedAsync(
            change.DocumentId,
            change.PartitionKey,
            ct);
    }
}
```

### Using with Cloud Functions

For serverless scenarios, use Firestore triggers:

```csharp
[FirestoreFunction("outbox")]
public async Task ProcessOutboxMessage(
    FirestoreEvent<OutboxDocument> firestoreEvent,
    CancellationToken ct)
{
    var doc = firestoreEvent.Value;

    // Only process unpublished messages
    if (doc.IsPublished)
        return;

    var payload = Convert.FromBase64String(doc.Payload);

    // Publish to message broker
    await _messageBroker.PublishAsync(payload);

    // Mark as published
    await _outbox.MarkAsPublishedAsync(
        doc.MessageId,
        new PartitionKey(doc.PartitionKey),
        ct);
}
```

## Document Schema

The outbox uses the following Firestore document schema:

| Field | Type | Description |
|-------|------|-------------|
| messageId | String | Unique message identifier |
| partitionKey | String | Partition key value for querying |
| messageType | String | Message type name |
| payload | String | Base64-encoded message payload |
| headers | String | JSON-encoded headers |
| aggregateId | String | Associated aggregate ID |
| aggregateType | String | Associated aggregate type |
| correlationId | String | Correlation ID for tracing |
| causationId | String | Causation ID linking to causing message |
| createdAt | String | ISO 8601 timestamp |
| publishedAt | String | ISO 8601 timestamp when published |
| isPublished | Boolean | Publication status |
| retryCount | Number | Number of retry attempts |
| lastError | String | Last error message |
| expireAt | Timestamp | TTL expiration timestamp |

### TTL Cleanup

Firestore does not have native TTL support. Use one of these approaches:

1. **Cloud Scheduler + Cloud Function**: Periodic cleanup function
2. **Firebase Extensions**: `firestore-bigquery-export` or custom extension
3. **Manual cleanup**: Call `CleanupOldMessagesAsync` periodically

Example Cloud Function for cleanup:

```csharp
[CloudSchedulerFunction("0 0 * * *")] // Daily at midnight
public async Task CleanupOutbox(CancellationToken ct)
{
    var partitions = await GetActivePartitionsAsync(ct);

    foreach (var partition in partitions)
    {
        await _outbox.CleanupOldMessagesAsync(
            new PartitionKey(partition),
            TimeSpan.FromDays(7),
            ct);
    }
}
```

## Requirements

- .NET 9.0+
- Google Cloud Firestore or Firestore Emulator
- `Excalibur.Data.Abstractions` for cloud-native interfaces
- `Excalibur.Data.Firestore` for Firestore SDK setup

## License

See LICENSE files in repository root.
