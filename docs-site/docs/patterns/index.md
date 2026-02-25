---
sidebar_position: 1
title: Patterns
description: Implement common messaging patterns like Outbox, Inbox, Claim Check, and Dead Letter handling.
---

# Messaging Patterns

Dispatch provides implementations of common messaging patterns for building reliable distributed systems.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [handlers](../handlers.md) and [pipeline concepts](../pipeline/index.md)

## Start Here

If you are new to reliable messaging, start with these two guides:

1. **[Idempotent Consumer Guide](idempotent-consumer.md)** — Why messages get duplicated and how the Outbox and Inbox patterns give you effective exactly-once processing.
2. **[Error Handling & Recovery Guide](error-handling.md)** — What happens when a message fails: retries, circuit breakers, dead letter queues, and recovery.

## Available Patterns

| Pattern | Purpose | When to Use |
|---------|---------|-------------|
| [Idempotent Consumer Guide](idempotent-consumer.md) | Understanding reliable messaging | New to distributed systems or Dispatch patterns |
| [Error Handling & Recovery Guide](error-handling.md) | Understanding failure handling | Need to understand retries, DLQ, and recovery |
| [Outbox](outbox.md) | Reliable message publishing | Atomic writes with database transactions |
| [Inbox](inbox.md) | Idempotent message processing | Prevent duplicate processing |
| [Claim Check](claim-check.md) | Large message handling | Messages exceed transport limits |
| [Dead Letter](dead-letter.md) | Failed message handling | Message processing fails repeatedly |
| [Routing](routing.md) | Message routing rules | Route messages to different handlers/transports |
| [Streaming](streaming.md) | Async stream processing | Large datasets, positional awareness |

## Outbox Pattern

The outbox pattern ensures messages are published reliably by storing them in the same transaction as your domain changes.

```csharp
// Configure outbox with the fluent builder
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});
services.AddExcaliburOutbox(outbox =>
{
    outbox.UseSqlServer(connectionString)
          .WithProcessing(p => p.PollingInterval(TimeSpan.FromSeconds(5)))
          .EnableBackgroundProcessing();
});

// In your handler
public class CreateOrderHandler : IActionHandler<CreateOrderAction>
{
    private readonly IOutbox _outbox;
    private readonly IDbContext _db;

    public async Task HandleAsync(CreateOrderAction action, CancellationToken ct)
    {
        using var transaction = await _db.BeginTransactionAsync(ct);

        // Save domain changes
        var order = new Order { /* ... */ };
        await _db.Orders.AddAsync(order, ct);

        // Store message in outbox (same transaction)
        await _outbox.AddAsync(new OrderCreatedEvent(order.Id), ct);

        await transaction.CommitAsync(ct);
        // Message will be published by background processor
    }
}
```

**Learn more:** [Outbox Pattern](outbox.md)

## Inbox Pattern

The inbox pattern provides idempotent message processing by tracking processed messages.

```csharp
// Register inbox store
services.AddSqlServerInboxStore(connectionString);
// Or with options:
services.AddSqlServerInboxStore(options =>
{
    options.ConnectionString = connectionString;
    options.SchemaName = "messaging";
    options.TableName = "inbox_messages";
});

// Messages are automatically deduplicated
public class OrderCreatedHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        // This handler is guaranteed to run only once per message ID
        await _emailService.SendOrderConfirmationAsync(@event.OrderId, ct);
    }
}
```

**Learn more:** [Inbox Pattern](inbox.md)

## Claim Check Pattern

Store large message payloads externally and pass references:

```csharp
// Register claim check with a provider
services.AddClaimCheck<AzureBlobClaimCheckProvider>(options =>
{
    options.ConnectionString = blobConnectionString;
    options.ContainerName = "large-messages";
    options.PayloadThreshold = 256_000; // 256 KB
});

// Large payloads automatically stored in blob storage
public record ProcessReportAction(
    Guid ReportId,
    byte[] LargePayload) : IDispatchAction;

// The framework handles claim check automatically:
// 1. If payload > threshold, store in blob, replace with reference
// 2. On receive, retrieve from blob, restore payload
```

**Learn more:** [Claim Check Pattern](claim-check.md)

## Pattern Combination

Patterns can be combined for maximum reliability:

```csharp
// Register Dispatch with the builder
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Outbox for reliable publishing
services.AddExcaliburOutbox(outbox =>
{
    outbox.UseSqlServer(connectionString)
          .EnableBackgroundProcessing();
});

// Inbox for idempotent processing
services.AddSqlServerInboxStore(connectionString);

// Claim check for large messages
services.AddClaimCheck<AzureBlobClaimCheckProvider>(options =>
{
    options.ContainerName = "messages";
    options.PayloadThreshold = 256_000;
});
```

## Store Providers

Each pattern supports multiple storage backends:

### Outbox Stores

| Store | Package | Use Case |
|-------|---------|----------|
| SQL Server | `Excalibur.Outbox.SqlServer` | SQL Server databases |
| PostgreSQL | `Excalibur.Data.Postgres` | PostgreSQL databases |
| MongoDB | `Excalibur.Data.MongoDB` | MongoDB databases |
| CosmosDB | `Excalibur.Outbox.CosmosDb` | Azure Cosmos DB |
| DynamoDB | `Excalibur.Outbox.DynamoDb` | AWS DynamoDB |
| Firestore | `Excalibur.Outbox.Firestore` | Google Firestore |
| In-Memory | `Excalibur.Data.InMemory` | Testing only |

### Inbox Stores

| Store | Package | Use Case |
|-------|---------|----------|
| SQL Server | `Excalibur.Data.SqlServer` | SQL Server databases |
| PostgreSQL | `Excalibur.Data.Postgres` | PostgreSQL databases |
| MongoDB | `Excalibur.Data.MongoDB` | MongoDB databases |
| Redis | `Excalibur.Data.Redis` | Redis-backed deduplication |
| DynamoDB | `Excalibur.Data.DynamoDb` | AWS DynamoDB |
| CosmosDB | `Excalibur.Data.CosmosDb` | Azure Cosmos DB |
| In-Memory | `Excalibur.Data.InMemory` | Testing only |

### Claim Check Stores

| Store | Package | Use Case |
|-------|---------|----------|
| Azure Blob | `Excalibur.Dispatch.Patterns` | Azure Blob Storage |
| In-Memory | `Excalibur.Dispatch.Patterns` | Testing only |

## Monitoring Patterns

### Outbox Monitoring

```csharp
// Health check
services.AddHealthChecks()
    .AddOutboxHealthCheck();

// Metrics (via OpenTelemetry)
// - dispatch.outbox.pending_count
// - dispatch.outbox.processing_time
// - dispatch.outbox.retry_count
```

### Inbox Monitoring

```csharp
services.AddHealthChecks()
    .AddInboxHealthCheck();

// Metrics
// - dispatch.inbox.duplicate_count
// - dispatch.inbox.processed_count
// - dispatch.inbox.storage_size
```

## In This Section

- [Idempotent Consumer Guide](idempotent-consumer.md) — Understanding reliable exactly-once messaging
- [Error Handling & Recovery Guide](error-handling.md) — Retries, circuit breakers, dead letter queues, and recovery
- [Outbox](outbox.md) — Reliable message publishing
- [Inbox](inbox.md) — Idempotent message processing
- [Claim Check](claim-check.md) — Large message handling
- [Dead Letter](dead-letter.md) — Failed message handling
- [Routing](routing.md) — Message routing configuration
- [Streaming](streaming.md) — Async stream processing with positional awareness

## See Also

- [Transports](../transports/index.md) - Configure transport infrastructure for patterns like Outbox
- [Event Sourcing](../event-sourcing/index.md) - Event-sourced aggregates that use Outbox for publishing
- [Handlers](../handlers.md) - Action and event handler patterns

