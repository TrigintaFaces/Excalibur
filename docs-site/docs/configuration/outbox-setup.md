---
sidebar_position: 3
title: Outbox Setup
description: Configure the transactional outbox pattern for reliable messaging
---

# Outbox Setup

The outbox pattern ensures reliable message delivery by storing messages in the same transaction as your domain changes. This guide covers configuration options for the Excalibur outbox.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Outbox
  dotnet add package Excalibur.Outbox.SqlServer  # or your provider
  ```
- Familiarity with [outbox pattern concepts](../patterns/outbox.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Why Use an Outbox?

Without an outbox:
```
1. Save aggregate ✅
2. Publish event  ❌ (network failure)
→ Inconsistent state: aggregate saved but event lost
```

With an outbox:
```
1. Save aggregate + outbox message (same transaction) ✅
2. Background processor publishes from outbox ✅
3. Mark message as processed ✅
→ Guaranteed delivery (at-least-once)
```

## Basic Setup

```csharp
services.AddExcaliburOutbox(outbox =>
{
    outbox.UseSqlServer(connectionString)
          .EnableBackgroundProcessing();
});
```

Alternatively, use the unified builder:

```csharp
services.AddExcalibur(excalibur =>
{
    excalibur.AddOutbox(outbox =>
    {
        outbox.UseSqlServer(connectionString)
              .EnableBackgroundProcessing();
    });
});
```

## Configuration Options

### Fluent Builder API

```csharp
services.AddExcaliburOutbox(outbox =>
{
    outbox.UseSqlServer(connectionString, sql =>
    {
        sql.SchemaName("Messaging")
           .TableName("OutboxMessages")
           .CommandTimeout(TimeSpan.FromSeconds(60));
    })
    .WithProcessing(processing =>
    {
        processing.BatchSize(100)
                  .PollingInterval(TimeSpan.FromSeconds(5))
                  .MaxRetryCount(5)
                  .RetryDelay(TimeSpan.FromMinutes(1))
                  .EnableParallelProcessing(4);
    })
    .WithCleanup(cleanup =>
    {
        cleanup.EnableAutoCleanup(true)
               .RetentionPeriod(TimeSpan.FromDays(14))
               .CleanupInterval(TimeSpan.FromHours(6));
    })
    .EnableBackgroundProcessing();
});
```

### Preset-Based API

Use presets for common scenarios:

```csharp
// High throughput (event streaming, analytics)
services.AddExcaliburOutbox(OutboxOptions.HighThroughput().Build());

// Balanced (most applications)
services.AddExcaliburOutbox(OutboxOptions.Balanced().Build());

// High reliability (financial, critical systems)
services.AddExcaliburOutbox(OutboxOptions.HighReliability().Build());
```

Customize presets:

```csharp
services.AddExcaliburOutbox(
    OutboxOptions.HighThroughput()
        .WithBatchSize(2000)
        .WithProcessorId("worker-1")
        .Build());
```

## Preset Comparison

| Setting | HighThroughput | Balanced | HighReliability |
|---------|----------------|----------|-----------------|
| BatchSize | 1000 | 100 | 10 |
| PollingInterval | 100ms | 1s | 5s |
| MaxRetryCount | 3 | 5 | 10 |
| RetryDelay | 30s | 1min | 5min |
| Parallelism | 8 | 4 | 1 |

## Database Providers

### SQL Server

```csharp
outbox.UseSqlServer(connectionString, sql =>
{
    sql.SchemaName("Outbox")
       .TableName("Messages")
       .UseRowLocking(true);  // For high concurrency
});
```

### Postgres

```csharp
outbox.UsePostgres(connectionString, pg =>
{
    pg.SchemaName("outbox")
      .TableName("messages");
});
```

### In-Memory (Testing)

```csharp
outbox.UseInMemory();  // No persistence - for tests only
```

## Processing Configuration

### Batch Size

Controls how many messages are processed per iteration:

```csharp
.WithProcessing(p => p.BatchSize(100))
```

| Scenario | Recommended Size |
|----------|------------------|
| Low latency | 10-50 |
| Standard workloads | 100-200 |
| High throughput | 500-1000 |
| Bulk operations | 1000+ |

### Polling Interval

How often the processor checks for new messages:

```csharp
.WithProcessing(p => p.PollingInterval(TimeSpan.FromSeconds(5)))
```

| Scenario | Recommended Interval |
|----------|---------------------|
| Real-time requirements | 100ms - 500ms |
| Standard applications | 1s - 5s |
| Batch processing | 10s - 60s |

### Parallel Processing

Enable concurrent message processing:

```csharp
.WithProcessing(p => p.EnableParallelProcessing(4))
```

### Retry Configuration

Configure retry behavior for failed messages:

```csharp
.WithProcessing(p =>
{
    p.MaxRetryCount(5)
     .RetryDelay(TimeSpan.FromMinutes(1));
})
```

## Cleanup Configuration

### Automatic Cleanup

Remove processed messages automatically:

```csharp
.WithCleanup(cleanup =>
{
    cleanup.EnableAutoCleanup(true)
           .RetentionPeriod(TimeSpan.FromDays(7))
           .CleanupInterval(TimeSpan.FromHours(1));
})
```

### Disable Auto-Cleanup

Disable automatic cleanup if you manage message retention externally (e.g., database maintenance jobs):

```csharp
.WithCleanup(c => c.EnableAutoCleanup(false))
```

## Background Processing

### Hosted Service

Enable automatic background processing:

```csharp
outbox.EnableBackgroundProcessing();
```

This registers an `IHostedService` that continuously processes the outbox.

### Manual Processing

For serverless or custom scenarios:

```csharp
// Don't enable background processing
outbox.UseSqlServer(connectionString);

// Manually trigger processing
var processor = services.GetRequiredService<IOutboxProcessor>();
await processor.DispatchPendingMessagesAsync(CancellationToken.None);
```

## Multi-Instance Deployment

### Processor ID

Assign unique IDs to prevent duplicate processing:

```csharp
OutboxOptions.Balanced()
    .WithProcessorId(Environment.MachineName)
    .Build()
```

## Health Checks

Monitor outbox health:

```csharp
services.AddHealthChecks()
    .AddCheck<OutboxHealthCheck>("outbox");
```

The health check reports:
- **Healthy**: Processing normally
- **Degraded**: High pending count or old messages
- **Unhealthy**: Processing failures

## Observability

### Metrics

```csharp
services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Excalibur.Outbox.*");
    });
```

Metrics exported:
- `excalibur.outbox.pending` — Pending message count
- `excalibur.outbox.processed` — Messages processed per interval
- `excalibur.outbox.failed` — Failed message count
- `excalibur.outbox.age_ms` — Age of oldest pending message

### Logging

Outbox operations are logged automatically. Configure log levels:

```json
{
  "Logging": {
    "LogLevel": {
      "Excalibur.Outbox": "Information"
    }
  }
}
```

## Best Practices

| Practice | Reason |
|----------|--------|
| Use presets | Tested configurations for common scenarios |
| Set processor ID | Prevent duplicate processing in multi-instance |
| Enable cleanup | Prevent unbounded table growth |
| Monitor pending count | Detect processing bottlenecks |
| Use appropriate batch size | Balance throughput vs. latency |

## Troubleshooting

### Messages not being processed

1. Verify `EnableBackgroundProcessing()` is called
2. Check logs for processing errors
3. Ensure database connection is valid

### High pending count

1. Increase batch size or parallelism
2. Check for slow downstream handlers
3. Monitor for retry storms

### Duplicate messages

Ensure your handlers are idempotent. The outbox guarantees at-least-once delivery.

```csharp
public class OrderCreatedHandler : IEventHandler<OrderCreated>
{
    public async Task HandleAsync(OrderCreated @event, CancellationToken ct)
    {
        // Idempotent: check if already processed
        if (await _store.ExistsAsync(@event.OrderId))
            return;

        // Process...
    }
}
```

## See Also

- [Outbox Pattern](../patterns/outbox.md) — Conceptual overview of the transactional outbox pattern
- [Inbox Pattern](../patterns/inbox.md) — Idempotent message processing with the inbox pattern
- [Event Store Setup](../configuration/event-store-setup.md) — Configure event stores and aggregate repositories
- [Worker Services](../deployment/worker-services.md) — Deploy dedicated background workers for outbox processing
