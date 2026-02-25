# Excalibur.Outbox.CosmosDb

Azure Cosmos DB implementation of the cloud-native outbox pattern for reliable message delivery.

## Features

- **Transactional outbox pattern** with partition-aware storage
- **Change feed subscription** for push-based message processing
- **Cost-aware operations** with RU tracking
- **Automatic TTL cleanup** for published messages
- **Serverless-friendly** - designed for Azure Functions triggers

## Installation

```bash
dotnet add package Excalibur.Outbox.CosmosDb
```

## Configuration

```csharp
services.AddCosmosDbOutboxStore(options =>
{
    options.ConnectionString = "AccountEndpoint=...;AccountKey=...";
    options.DatabaseName = "mydb";
    options.ContainerName = "outbox";
    options.DefaultTimeToLiveSeconds = 604800; // 7 days
    options.CreateContainerIfNotExists = true;
});
```

Or via configuration:

```csharp
services.AddCosmosDbOutboxStore(configuration.GetSection("CosmosDbOutbox"));
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

### Processing Messages with Change Feed

```csharp
// Subscribe to new outbox messages
var subscription = await _outbox.SubscribeToNewMessagesAsync(
    new ChangeFeedOptions { StartPosition = ChangeFeedStartPosition.Now },
    ct);

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

### Using with Azure Functions

For serverless scenarios, use the Cosmos DB change feed trigger:

```csharp
[FunctionName("OutboxProcessor")]
public async Task ProcessOutboxMessages(
    [CosmosDBTrigger(
        databaseName: "mydb",
        containerName: "outbox",
        Connection = "CosmosDbConnection",
        LeaseContainerName = "leases",
        CreateLeaseContainerIfNotExists = true)]
    IReadOnlyList<OutboxDocument> documents,
    CancellationToken ct)
{
    foreach (var doc in documents.Where(d => !d.IsPublished))
    {
        await _messageBroker.PublishAsync(doc);
        await _outbox.MarkAsPublishedAsync(doc.Id, new PartitionKey(doc.PartitionKey), ct);
    }
}
```

## Document Schema

The outbox uses the following Cosmos DB document schema:

| Field | Type | Description |
|-------|------|-------------|
| id | String | Unique message identifier |
| partitionKey | String | Partition key value |
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
| ttl | Number | Time-to-live in seconds |

## Requirements

- .NET 9.0+
- Azure Cosmos DB account or emulator
- `Excalibur.Data.Abstractions` for cloud-native interfaces
- `Excalibur.Data.CosmosDb` for Cosmos DB SDK setup

## License

See LICENSE files in repository root.
