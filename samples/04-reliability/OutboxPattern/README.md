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

### Middleware Pipeline

The recommended approach uses dispatch middleware for outbox and inbox integration:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    // Add inbox middleware first -- deduplicates before processing
    dispatch.UseInbox();

    // Add outbox middleware -- stages integration events for reliable delivery
    dispatch.UseOutbox();

    // Register handlers from this assembly
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});
```

### Outbox Configuration

Uses the builder pattern to configure storage, processing, and cleanup:

```csharp
builder.Services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
{
    outbox.UseInMemory()                                    // In-memory for demo; use UseSqlServer() in production
        .WithProcessing(processing =>
        {
            processing.BatchSize(50)                        // Process 50 messages per batch
                .PollingInterval(TimeSpan.FromSeconds(2))   // Check for messages every 2 seconds
                .MaxRetryCount(3)                           // Retry failed messages up to 3 times
                .RetryDelay(TimeSpan.FromSeconds(10));      // Wait 10 seconds between retries
        })
        .WithCleanup(cleanup =>
        {
            cleanup.EnableAutoCleanup(true)
                .RetentionPeriod(TimeSpan.FromHours(1))     // Keep messages for 1 hour
                .CleanupInterval(TimeSpan.FromMinutes(5));  // Run cleanup every 5 minutes
        })
        .EnableBackgroundProcessing();                      // Start the background processor hosted service
}));
```

### Inbox Configuration

```csharp
builder.Services.AddExcaliburInbox(inbox =>
{
    inbox.UseInMemory(); // In-memory for demo; use UseSqlServer() in production
});
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
services.AddExcalibur(excalibur => excalibur
    .AddOutbox(outbox => outbox.UseSqlServer(sql => sql.ConnectionString(connectionString))));
services.AddExcaliburInbox(inbox => inbox.UseSqlServer(sql => sql.ConnectionString(connectionString)));
```

### Required Tables

The SQL Server implementation does **not** auto-create tables. You must create them before starting the application:

```sql
-- Outbox table (dbo.OutboxMessages)
CREATE TABLE dbo.OutboxMessages (
    Id              NVARCHAR(256)     NOT NULL PRIMARY KEY,
    MessageType     NVARCHAR(500)     NOT NULL,
    Payload         VARBINARY(MAX)    NOT NULL,
    Headers         NVARCHAR(MAX)     NULL,
    Destination     NVARCHAR(500)     NOT NULL,
    CreatedAt       DATETIMEOFFSET    NOT NULL,
    ScheduledAt     DATETIMEOFFSET    NULL,
    Status          INT               NOT NULL DEFAULT 0,
    RetryCount      INT               NOT NULL DEFAULT 0,
    CorrelationId   NVARCHAR(256)     NULL,
    CausationId     NVARCHAR(256)     NULL,
    TenantId        NVARCHAR(256)     NULL,
    Priority        INT               NOT NULL DEFAULT 0,
    TargetTransports NVARCHAR(MAX)    NULL,
    IsMultiTransport BIT              NOT NULL DEFAULT 0,
    PartitionKey    NVARCHAR(256)     NULL,
    GroupKey        NVARCHAR(256)     NULL,
    SequenceNumber  BIGINT            NOT NULL DEFAULT 0,
    NextAttemptAt   DATETIMEOFFSET    NULL,
    ProcessedAt     DATETIMEOFFSET    NULL,
    Error           NVARCHAR(MAX)     NULL
);
-- Supports the claim predicate (Status + NextAttemptAt retry visibility) and partition-ordered delivery.
CREATE INDEX IX_OutboxMessages_Claim ON dbo.OutboxMessages (Status, NextAttemptAt, PartitionKey, SequenceNumber);

-- Inbox table (dbo.InboxMessages)
CREATE TABLE dbo.InboxMessages (
    MessageId   NVARCHAR(256)     NOT NULL PRIMARY KEY,
    ProcessedAt DATETIMEOFFSET    NOT NULL
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
    OrderHandlers.cs  # Event handlers
```

## Related Samples

- [Saga Orchestration](../SagaOrchestration/) - Coordinated multi-step workflows
- [Retry and Circuit Breaker](../RetryAndCircuitBreaker/) - Resilience patterns
