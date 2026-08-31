# Outbox Pattern Sample

This sample demonstrates the **transactional outbox pattern** for reliable message delivery with the Excalibur framework.

## What This Sample Shows

1. **Guaranteed Delivery** - Messages stored atomically with business data
2. **At-Least-Once Semantics** - Automatic retry on failure
3. **Configurable Policies** - Batch size, retry, and parallelism options
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

Uses the builder pattern to configure storage and processing:

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
4. **Plan for Retention**: Sent outbox entries are retained until you remove them - automatic cleanup is not implemented, so schedule your own deletion job and size storage for the volume you keep
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
-- Outbox table (default: dbo.OutboxMessages)
CREATE TABLE dbo.OutboxMessages (
    Id               NVARCHAR(255)  NOT NULL PRIMARY KEY,
    MessageType      NVARCHAR(500)  NOT NULL,
    Payload          VARBINARY(MAX) NOT NULL,
    Headers          NVARCHAR(MAX)  NULL,
    Destination      NVARCHAR(255)  NOT NULL,
    CreatedAt        DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    ScheduledAt      DATETIMEOFFSET NULL,
    SentAt           DATETIMEOFFSET NULL,
    Status           INT            NOT NULL DEFAULT 0,
    RetryCount       INT            NOT NULL DEFAULT 0,
    LastError        NVARCHAR(MAX)  NULL,
    LastAttemptAt    DATETIMEOFFSET NULL,
    CorrelationId    NVARCHAR(255)  NULL,
    CausationId      NVARCHAR(255)  NULL,
    TenantId         NVARCHAR(64) COLLATE Latin1_General_BIN2  NOT NULL DEFAULT '__untenanted__',
    Priority         INT            NOT NULL DEFAULT 0,
    TargetTransports NVARCHAR(MAX)  NULL,
    IsMultiTransport BIT            NOT NULL DEFAULT 0,
    LeasedAt         DATETIMEOFFSET NULL,
    LeasedBy         NVARCHAR(255)  NULL,
    PartitionKey     NVARCHAR(256)  NULL,   -- ordered delivery: per-partition FIFO
    GroupKey         NVARCHAR(256)  NULL,   -- logical message grouping
    SequenceNumber   BIGINT         NOT NULL DEFAULT 0, -- monotonic ordering key
    NextAttemptAt    DATETIMEOFFSET NULL,   -- retry backoff: not re-claimed until this time
    FencingToken     BIGINT         NULL,   -- token of the leader that claimed the row; drain/mark SQL name it unconditionally
    INDEX IX_OutboxMessages_Status_CreatedAt (Status, CreatedAt),
    INDEX IX_OutboxMessages_Claim (Status, NextAttemptAt, PartitionKey, SequenceNumber)
);

-- Leadership-fence control table (default: dbo.OutboxFence) — REQUIRED, including for a
-- single-instance outbox that never elects a leader.
--
-- The drain statement names this table unconditionally, and SQL Server resolves object names when
-- it compiles the statement — not when a predicate happens to evaluate. So if the table is absent
-- the drain fails on every attempt with "Msg 208, Invalid object name 'dbo.OutboxFence'", and
-- nothing is ever delivered. Create it even if you do not run leader-fenced processing.
--
-- It holds ONE row per outbox table (keyed by the qualified outbox table name) recording the highest
-- fencing token ever accepted. It is deliberately separate from OutboxMessages so routine cleanup,
-- which deletes sent token-bearing rows, can never lower the recorded high-water mark — a superseded
-- leader's stale token stays rejected after cleanup. Cleanup must not reference this table.
CREATE TABLE dbo.OutboxFence (
    OutboxTable    NVARCHAR(512) NOT NULL PRIMARY KEY,
    HighWaterToken BIGINT        NOT NULL
);

-- Inbox table (default: dbo.inbox_messages) — SINGLE-TENANT schema.
--
-- DEPLOYMENT MODE: the inbox is column-agnostic by deployment. This sample is single-tenant, so
-- the dedup/claim key is the pair (MessageId, HandlerType) and there is NO TenantId column — a
-- single-tenant consumer pays nothing for a tenant discriminator it never uses. If you opt into
-- multi-tenancy (AddMultiTenancy()), use the MULTI-TENANT inbox schema instead — key
-- (MessageId, HandlerType, TenantId), TenantId NOT NULL — shipped as the provider's
-- 001_CreateInboxSchema.MultiTenant.sql. The store verifies the physical schema matches the
-- registered mode at startup and fails fast on a mismatch.
CREATE TABLE dbo.inbox_messages (
    MessageId     NVARCHAR(255)  NOT NULL,
    HandlerType   NVARCHAR(500)  NOT NULL,
    MessageType   NVARCHAR(500)  NOT NULL,
    Payload       VARBINARY(MAX) NOT NULL,
    Metadata      NVARCHAR(MAX)  NULL,
    ReceivedAt    DATETIMEOFFSET NOT NULL,
    ProcessedAt   DATETIMEOFFSET NULL,
    Status        INT            NOT NULL DEFAULT 0,
    LeaseExpiresAtUtc DATETIMEOFFSET NULL,         -- REQUIRED: backs the atomic lease-based claim
    LastError     NVARCHAR(MAX)  NULL,
    RetryCount    INT            NOT NULL DEFAULT 0,
    LastAttemptAt DATETIMEOFFSET NULL,
    NextAttemptAt DATETIMEOFFSET NULL,
    CorrelationId NVARCHAR(255)  NULL,
    Source        NVARCHAR(255)  NULL,
    -- Single-tenant: the dedup/claim key is the pair. No TenantId column.
    CONSTRAINT PK_inbox_messages PRIMARY KEY CLUSTERED (MessageId, HandlerType)
);

-- To grow into multi-tenancy later (single-tenant -> multi-tenant), run the provider's
-- 002_MigrateToMultiTenant.sql: it adds TenantId NOT NULL DEFAULT '__untenanted__' (anchoring
-- existing rows to the reserved sentinel) and rebuilds the key as (MessageId, HandlerType, TenantId).
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
