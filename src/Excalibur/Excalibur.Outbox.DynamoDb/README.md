# Excalibur.Outbox.DynamoDb

AWS DynamoDB implementation of the cloud-native outbox pattern for reliable message delivery.

## Features

- **Transactional outbox pattern** with partition-aware storage
- **DynamoDB Streams** for push-based message processing
- **Capacity-aware operations** with WCU/RCU tracking
- **Automatic TTL cleanup** for published messages
- **Serverless-friendly** - designed for AWS Lambda triggers

## Installation

```bash
dotnet add package Excalibur.Outbox.DynamoDb
```

## Configuration

```csharp
services.AddDynamoDbOutboxStore(options =>
{
    options.Region = "us-east-1";
    options.TableName = "outbox";
    options.DefaultTimeToLiveSeconds = 604800; // 7 days
    options.CreateTableIfNotExists = true;
    options.EnableStreams = true;
});
```

Or via configuration:

```csharp
services.AddDynamoDbOutboxStore(configuration.GetSection("DynamoDbOutbox"));
```

### Local Development (DynamoDB Local)

```csharp
services.AddDynamoDbOutboxStore(options =>
{
    options.ServiceUrl = "http://localhost:8000";
    options.TableName = "outbox";
    options.AccessKey = "local";
    options.SecretKey = "local";
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

### Processing Messages with DynamoDB Streams

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

### Using with AWS Lambda

For serverless scenarios, use DynamoDB Streams trigger:

```csharp
public class OutboxProcessor
{
    private readonly IMessageBroker _messageBroker;
    private readonly ICloudNativeOutboxStore _outbox;

    public async Task ProcessStreamRecordsAsync(
        DynamoDBEvent dynamoEvent,
        CancellationToken ct)
    {
        foreach (var record in dynamoEvent.Records)
        {
            if (record.EventName != "INSERT")
                continue;

            var newImage = record.Dynamodb.NewImage;

            // Check if unpublished
            if (newImage.TryGetValue("isPublished", out var isPublished)
                && isPublished.BOOL)
                continue;

            var messageId = newImage["sk"].S;
            var partitionKey = newImage["pk"].S;
            var payload = Convert.FromBase64String(newImage["payload"].S);

            // Publish to message broker
            await _messageBroker.PublishAsync(payload);

            // Mark as published
            await _outbox.MarkAsPublishedAsync(
                messageId,
                new PartitionKey(partitionKey),
                ct);
        }
    }
}
```

## Table Schema

The outbox uses the following DynamoDB table schema:

| Attribute | Type | Description |
|-----------|------|-------------|
| pk | String (HASH) | Partition key value |
| sk | String (RANGE) | Message ID (sort key) |
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
| ttl | Number | Unix timestamp for TTL expiration |

### Streams Configuration

The table is created with streams enabled:
- **StreamViewType**: `NEW_AND_OLD_IMAGES`
- Required for change feed subscription

### TTL Configuration

- TTL is enabled on the `ttl` attribute
- Only applied to published messages
- Default retention: 7 days (604800 seconds)

## Requirements

- .NET 9.0+
- AWS DynamoDB or DynamoDB Local
- `Excalibur.Data.Abstractions` for cloud-native interfaces
- `Excalibur.Data.DynamoDb` for DynamoDB SDK setup

## License

See LICENSE files in repository root.
