# Outbox Pattern Sample

This sample demonstrates the **transactional outbox pattern** for reliable message delivery with the Excalibur framework.

## What This Sample Shows

1. **Guaranteed Delivery** - Messages stored atomically with business data
2. **At-Least-Once Semantics** - Automatic retry on failure
3. **Configurable Policies** - Batch size, retry, cleanup options
4. **Inbox Deduplication** - Prevent duplicate message processing

## The Transactional Outbox Pattern

The outbox pattern solves a common distributed systems problem: how to reliably publish events when your database transaction commits.

### Without Outbox (Unreliable)

```
1. Save order to database
2. Publish OrderPlacedEvent
   -> If this fails, event is lost!
   -> If DB commit fails after publish, event is orphaned!
```

### With Outbox (Reliable)

```
1. BEGIN TRANSACTION
2. Save order to database
3. Save OrderPlacedEvent to outbox table
4. COMMIT TRANSACTION

5. Background processor:
   - Reads pending messages from outbox
   - Publishes each message
   - Marks as processed (or retries on failure)
```

## Key Concepts

### Outbox Configuration

```csharp
builder.Services.AddExcaliburOutbox(options =>
{
    // Processing
    options.BatchSize = 100;                              // Messages per batch
    options.PollingInterval = TimeSpan.FromSeconds(5);    // Check interval

    // Retry policy
    options.MaxRetryCount = 3;                            // Max retries
    options.RetryDelay = TimeSpan.FromMinutes(5);         // Retry delay

    // Cleanup
    options.EnableAutomaticCleanup = true;
    options.MessageRetentionPeriod = TimeSpan.FromDays(7);
    options.CleanupInterval = TimeSpan.FromHours(1);
});
```

### Store Registration

```csharp
// Development: In-memory stores
builder.Services.AddOutbox<InMemoryOutboxStore>();
builder.Services.AddInbox<InMemoryInboxStore>();

// Production: Durable stores
// builder.Services.AddSqlServerOutboxStore(connectionString);
// builder.Services.AddSqlServerInboxStore(connectionString);
```

### Background Services

```csharp
// Process outbox messages
builder.Services.AddOutboxHostedService();

// Deduplicate inbox messages
builder.Services.AddInboxHostedService();
```

## Running the Sample

```bash
cd samples/04-reliability/OutboxPattern
dotnet run
```

## Expected Output

```
Starting Outbox Pattern Sample...

=== Transactional Outbox Pattern Demo ===

The outbox pattern ensures reliable message delivery:
  1. Save message to outbox (same transaction as business data)
  2. Background processor publishes messages
  3. Retry on failure with configurable policy
  4. Auto-cleanup of old messages

Placing order: ORD-20260121-001
  -> OrderPlacedEvent dispatched to outbox
[Handler] Order placed: ORD-20260121-001 for customer CUST-12345, Total: $299.99

=== Chained Events Demo ===
  -> PaymentProcessedEvent dispatched
  -> InventoryReservedEvent dispatched
[Handler] Payment processed for order ORD-20260121-001: Transaction TXN-...
[Handler] Inventory reserved for order ORD-20260121-001: 2x WIDGET-001

=== Batch Dispatching Demo ===
  -> Order ORD-20260121-002 dispatched
  -> Order ORD-20260121-003 dispatched
  -> Order ORD-20260121-004 dispatched
  -> Order ORD-20260121-005 dispatched
```

## Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `BatchSize` | 100 | Messages processed per batch |
| `PollingInterval` | 5 seconds | Time between processing cycles |
| `MaxRetryCount` | 3 | Maximum retry attempts |
| `RetryDelay` | 5 minutes | Delay between retries |
| `MessageRetentionPeriod` | 7 days | How long to keep processed messages |
| `EnableAutomaticCleanup` | true | Auto-delete old messages |
| `CleanupInterval` | 1 hour | Time between cleanup runs |
| `EnableParallelProcessing` | false | Process messages in parallel |
| `MaxDegreeOfParallelism` | 4 | Max parallel message handlers |

## Inbox Deduplication

The inbox pattern prevents duplicate message processing:

```
Message arrives -> Check inbox for MessageId
  - If exists: Skip (duplicate)
  - If new: Process and record in inbox
```

This is essential for at-least-once delivery systems where messages may be redelivered.

## Best Practices

1. **Atomic Transactions**: Always save outbox messages in the same transaction as business data
2. **Idempotent Handlers**: Design handlers to be safe for repeated execution
3. **Monitor Queue Depth**: Watch outbox size to detect delivery issues
4. **Configure Retention**: Balance compliance needs with storage costs
5. **Use Parallel Processing**: Enable for high-throughput scenarios

## Production Deployment

### SQL Server Implementation

```csharp
// Use durable SQL Server stores
services.AddSqlServerOutboxStore(connectionString);
services.AddSqlServerInboxStore(connectionString);
```

### Required Tables

The SQL Server implementation creates these tables:

```sql
-- Outbox table
CREATE TABLE dbo.OutboxMessages (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    MessageType NVARCHAR(500) NOT NULL,
    Payload NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    ProcessedAt DATETIMEOFFSET NULL,
    RetryCount INT NOT NULL DEFAULT 0,
    Error NVARCHAR(MAX) NULL
);

-- Inbox table
CREATE TABLE dbo.InboxMessages (
    MessageId UNIQUEIDENTIFIER PRIMARY KEY,
    ProcessedAt DATETIMEOFFSET NOT NULL
);
```

## Project Structure

```
OutboxPattern/
OutboxPattern.csproj       # Project file
Program.cs                 # Main sample demonstrating outbox pattern
appsettings.json           # Configuration for logging and outbox
README.md                  # This file
Messages/
   OrderEvents.cs          # Event classes
Handlers/
    OrderEventHandlers.cs  # Event handlers
```

## Related Samples

- [Saga Orchestration](../SagaOrchestration/) - Coordinated multi-step workflows
- [Retry and Circuit Breaker](../RetryAndCircuitBreaker/) - Resilience patterns
