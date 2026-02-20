# AtLeastOnce with Inbox Deduplication

This example demonstrates the **AtLeastOnce** delivery guarantee with inbox-based deduplication.

## Overview

- **DeliveryGuarantee**: `AtLeastOnce` (default)
- **Behavior**: Messages are marked as sent after the entire batch is published
- **Throughput**: Highest (1 DB round-trip per batch)
- **Failure Window**: Entire batch may be redelivered on crash

## When to Use

Choose AtLeastOnce when:
- Your handlers are **idempotent** (safe to process multiple times)
- You have **inbox deduplication** to filter duplicates
- **Throughput** is more important than minimizing duplicates

## Configuration

```csharp
services.Configure<OutboxOptions>(options =>
{
    options.DeliveryGuarantee = OutboxDeliveryGuarantee.AtLeastOnce;
    options.BatchSize = 100;
    options.PollingInterval = TimeSpan.FromSeconds(5);
});
```

## Inbox Deduplication Pattern

```csharp
public class OrderHandler : IMessageHandler<OrderCreated>
{
    private readonly IInboxStore _inbox;

    public async Task HandleAsync(OrderCreated message, CancellationToken ct)
    {
        // Check if already processed
        if (await _inbox.IsProcessedAsync(message.MessageId, nameof(OrderHandler), ct))
        {
            return; // Skip duplicate
        }

        // Process the message
        await ProcessOrder(message, ct);

        // Mark as processed
        await _inbox.MarkProcessedAsync(message.MessageId, nameof(OrderHandler), ct);
    }
}
```

## Running the Example

```bash
cd samples/BackgroundServices/AtLeastOnceWithInbox
dotnet run
```
