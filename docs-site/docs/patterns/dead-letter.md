---
sidebar_position: 6
title: Dead Letter Handling
description: Handle messages that fail processing repeatedly with dead letter queues
---

# Dead Letter Handling

:::tip Start with the guide
For a narrative walkthrough of how retries, circuit breakers, and dead letter queues compose together, see the **[Error Handling & Recovery Guide](error-handling.md)**.
:::

Dead Letter Handling captures messages that fail processing repeatedly, allowing them to be analyzed, fixed, and reprocessed without blocking the main message flow.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch.Transport.Abstractions
  ```
- Familiarity with [transports](../transports/index.md) and [error handling](./error-handling.md)

## When to Use

- Messages fail processing after exhausting retry attempts
- You need to audit and analyze failed messages
- Business processes require manual intervention for certain failures
- You want to prevent poison messages from blocking queues
- You need to track processing failures for debugging and alerting

## How It Works

```
Handler                     Retry Policy                Dead Letter Queue
   |                            |                              |
   | --- Process message --->   |                              |
   | <--- Failure ----------    |                              |
   |                            |                              |
   | --- Retry attempt 1 --->   |                              |
   | <--- Failure ----------    |                              |
   |                            |                              |
   | --- Retry attempt N --->   |                              |
   | <--- Failure ----------    |                              |
   |                            |                              |
   |                            | --- Move to DLQ ------------>|
```

## Installation

```bash
# Core Dispatch (includes in-memory DLQ)
dotnet add package Excalibur.Dispatch

# SQL Server dead letter store (production)
dotnet add package Excalibur.Data.SqlServer

# Elasticsearch dead letter store (analytics/audit)
dotnet add package Excalibur.Data.ElasticSearch
```

## Basic Configuration

```csharp
using Microsoft.Extensions.DependencyInjection;

// Add poison message handling with default options
builder.Services.AddPoisonMessageHandling();

// Or configure with options
builder.Services.AddPoisonMessageHandling(options =>
{
    options.MaxRetryAttempts = 3;
    options.DeadLetterRetentionPeriod = TimeSpan.FromDays(30);
    options.EnableAutoCleanup = true;
    options.AutoCleanupInterval = TimeSpan.FromDays(1);
});
```

### Fluent Configuration via DispatchBuilder

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Add poison message handling via the dispatch builder
    dispatch.AddPoisonMessageHandling(options =>
    {
        options.MaxRetryAttempts = 5;
        options.DeadLetterRetentionPeriod = TimeSpan.FromDays(30);
        options.EnableAutoCleanup = true;
    });

    // Or use in-memory dead letter store
    dispatch.UseInMemoryDeadLetterStore();
});
```

## IDeadLetterQueue Interface

```csharp
public interface IDeadLetterQueue
{
    /// <summary>
    /// Enqueues a message to the dead letter queue.
    /// </summary>
    Task<Guid> EnqueueAsync<T>(
        T message,
        DeadLetterReason reason,
        CancellationToken cancellationToken,
        Exception? exception = null,
        IDictionary<string, string>? metadata = null);

    /// <summary>
    /// Retrieves dead letter entries based on filter criteria.
    /// </summary>
    Task<IReadOnlyList<DeadLetterEntry>> GetEntriesAsync(
        CancellationToken cancellationToken,
        DeadLetterQueryFilter? filter = null,
        int limit = 100);

    /// <summary>
    /// Retrieves a specific dead letter entry by its ID.
    /// </summary>
    Task<DeadLetterEntry?> GetEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replays a dead letter entry, re-submitting it for processing.
    /// </summary>
    Task<bool> ReplayAsync(
        Guid entryId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Replays multiple dead letter entries that match the specified filter.
    /// </summary>
    Task<int> ReplayBatchAsync(
        DeadLetterQueryFilter filter,
        CancellationToken cancellationToken);

    /// <summary>
    /// Purges (permanently deletes) a dead letter entry.
    /// </summary>
    Task<bool> PurgeAsync(
        Guid entryId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Purges all dead letter entries older than the specified age.
    /// </summary>
    Task<int> PurgeOlderThanAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current count of entries in the dead letter queue.
    /// </summary>
    Task<long> GetCountAsync(
        CancellationToken cancellationToken,
        DeadLetterQueryFilter? filter = null);
}
```

## Dead Letter Reasons

Messages can be dead lettered for various reasons:

| Reason | Description |
|--------|-------------|
| `MaxRetriesExceeded` | Message exceeded the maximum number of retry attempts |
| `CircuitBreakerOpen` | Circuit breaker was open, preventing delivery |
| `DeserializationFailed` | Message could not be deserialized |
| `HandlerNotFound` | No handler was registered for the message type |
| `ValidationFailed` | Message failed validation |
| `ManualRejection` | Handler explicitly rejected the message |
| `MessageExpired` | Message TTL expired before processing |
| `AuthorizationFailed` | Authorization check failed |
| `UnhandledException` | Unhandled exception during processing |
| `PoisonMessage` | Message detected as poison (repeatedly causing failures) |

## DeadLetterEntry Structure

```csharp
public sealed class DeadLetterEntry
{
    public Guid Id { get; init; }
    public required string MessageType { get; init; }
    public required byte[] Payload { get; init; }
    public DeadLetterReason Reason { get; init; }
    public string? ExceptionMessage { get; init; }
    public string? ExceptionStackTrace { get; init; }
    public DateTimeOffset EnqueuedAt { get; init; }
    public int OriginalAttempts { get; init; }
    public IDictionary<string, string>? Metadata { get; init; }
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public string? SourceQueue { get; init; }
    public bool IsReplayed { get; init; }
    public DateTimeOffset? ReplayedAt { get; init; }
}
```

## Usage Examples

### Viewing Dead Letter Entries

```csharp
public class DeadLetterMonitorService
{
    private readonly IDeadLetterQueue _dlq;
    private readonly ILogger<DeadLetterMonitorService> _logger;

    public DeadLetterMonitorService(
        IDeadLetterQueue dlq,
        ILogger<DeadLetterMonitorService> logger)
    {
        _dlq = dlq;
        _logger = logger;
    }

    public async Task ListPendingEntriesAsync(CancellationToken ct)
    {
        // Get all pending (non-replayed) entries
        var entries = await _dlq.GetEntriesAsync(
            ct,
            DeadLetterQueryFilter.PendingOnly(),
            limit: 100);

        foreach (var entry in entries)
        {
            _logger.LogInformation(
                "DLQ Entry: {Id} | Type: {Type} | Reason: {Reason} | At: {Time}",
                entry.Id,
                entry.MessageType,
                entry.Reason,
                entry.EnqueuedAt);
        }
    }
}
```

### Filtering by Reason

```csharp
// Get entries that failed due to max retries
var retriesExceeded = await _dlq.GetEntriesAsync(
    ct,
    DeadLetterQueryFilter.ByReason(DeadLetterReason.MaxRetriesExceeded));

// Get entries for a specific message type
var orderFailures = await _dlq.GetEntriesAsync(
    ct,
    DeadLetterQueryFilter.ByMessageType("OrderCreatedEvent"));

// Get entries from a date range
var lastWeek = await _dlq.GetEntriesAsync(
    ct,
    DeadLetterQueryFilter.ByDateRange(
        DateTimeOffset.UtcNow.AddDays(-7),
        DateTimeOffset.UtcNow));
```

### Advanced Filtering

```csharp
var filter = new DeadLetterQueryFilter
{
    Reason = DeadLetterReason.UnhandledException,
    FromDate = DateTimeOffset.UtcNow.AddDays(-1),
    IsReplayed = false,
    SourceQueue = "orders-queue",
    MinAttempts = 3,
    Skip = 0  // For pagination
};

var entries = await _dlq.GetEntriesAsync(ct, filter, limit: 50);
```

### Replaying Messages

```csharp
public class DeadLetterRecoveryService
{
    private readonly IDeadLetterQueue _dlq;

    public DeadLetterRecoveryService(IDeadLetterQueue dlq) => _dlq = dlq;

    // Replay a single entry
    public async Task<bool> ReplayEntryAsync(Guid entryId, CancellationToken ct)
    {
        return await _dlq.ReplayAsync(entryId, ct);
    }

    // Batch replay all validation failures (after fixing validation logic)
    public async Task<int> ReplayValidationFailuresAsync(CancellationToken ct)
    {
        var filter = DeadLetterQueryFilter.ByReason(DeadLetterReason.ValidationFailed);
        return await _dlq.ReplayBatchAsync(filter, ct);
    }

    // Replay all pending entries for a specific message type
    public async Task<int> ReplayByTypeAsync(string messageType, CancellationToken ct)
    {
        var filter = new DeadLetterQueryFilter
        {
            MessageType = messageType,
            IsReplayed = false
        };
        return await _dlq.ReplayBatchAsync(filter, ct);
    }
}
```

### Cleanup and Purging

```csharp
public class DeadLetterCleanupService
{
    private readonly IDeadLetterQueue _dlq;

    public DeadLetterCleanupService(IDeadLetterQueue dlq) => _dlq = dlq;

    // Purge a single entry
    public async Task<bool> PurgeEntryAsync(Guid entryId, CancellationToken ct)
    {
        return await _dlq.PurgeAsync(entryId, ct);
    }

    // Purge entries older than 30 days
    public async Task<int> PurgeOldEntriesAsync(CancellationToken ct)
    {
        return await _dlq.PurgeOlderThanAsync(TimeSpan.FromDays(30), ct);
    }

    // Get count of pending entries
    public async Task<long> GetPendingCountAsync(CancellationToken ct)
    {
        return await _dlq.GetCountAsync(
            DeadLetterQueryFilter.PendingOnly(),
            ct);
    }
}
```

## Configuration Options

### DeadLetterOptions

```csharp
public sealed class DeadLetterOptions
{
    // Maximum processing attempts before dead lettering (default: 3)
    public int MaxAttempts { get; set; } = 3;

    // Name of the dead letter queue (default: "deadletter")
    public string QueueName { get; set; } = "deadletter";

    // Preserve original message metadata (default: true)
    public bool PreserveMetadata { get; set; } = true;

    // Include exception details (default: true)
    public bool IncludeExceptionDetails { get; set; } = true;

    // Enable automatic recovery processing (default: false)
    public bool EnableRecovery { get; set; }

    // Recovery processing interval (default: 1 hour)
    public TimeSpan RecoveryInterval { get; set; } = TimeSpan.FromHours(1);
}
```

### PoisonMessageOptions

```csharp
public sealed class PoisonMessageOptions
{
    // Enable poison message detection (default: true)
    public bool Enabled { get; set; } = true;

    // Max retries before marking as poison (default: 3)
    public int MaxRetryAttempts { get; set; } = 3;

    // Max processing time before poison (default: 5 min)
    public TimeSpan MaxProcessingTime { get; set; } = TimeSpan.FromMinutes(5);

    // Retention period for dead letters (default: 30 days)
    public TimeSpan DeadLetterRetentionPeriod { get; set; } = TimeSpan.FromDays(30);

    // Enable automatic cleanup (default: true)
    public bool EnableAutoCleanup { get; set; } = true;

    // Cleanup interval (default: 1 day)
    public TimeSpan AutoCleanupInterval { get; set; } = TimeSpan.FromDays(1);

    // Capture full exception details (default: true)
    public bool CaptureExceptionDetails { get; set; } = true;

    // Exception types that immediately poison (non-retryable)
    public HashSet<Type> PoisonExceptionTypes { get; }

    // Exception types that are transient (retryable)
    public HashSet<Type> TransientExceptionTypes { get; }

    // Enable metrics collection (default: true)
    public bool EnableMetrics { get; set; } = true;

    // Enable alerting (default: true)
    public bool EnableAlerting { get; set; } = true;

    // Alert threshold count (default: 10)
    public int AlertThreshold { get; set; } = 10;

    // Time window for alert calculation (default: 15 min)
    public TimeSpan AlertTimeWindow { get; set; } = TimeSpan.FromMinutes(15);
}
```

## Poison Message Detection

Dispatch includes multiple poison message detectors that run as middleware:

| Detector | Description |
|----------|-------------|
| `RetryCountPoisonDetector` | Marks as poison after max retry attempts |
| `ExceptionTypePoisonDetector` | Marks as poison for specific exception types |
| `TimespanPoisonDetector` | Marks as poison if processing exceeds time limit |
| `CompositePoisonDetector` | Combines multiple detectors |

### Custom Poison Detector

```csharp
public class CustomPoisonDetector : IPoisonMessageDetector
{
    public Task<PoisonDetectionResult> IsPoisonMessageAsync(
        IDispatchMessage message,
        IMessageContext context,
        MessageProcessingInfo processingInfo,
        Exception? exception = null)
    {
        // Custom logic to determine if message is poison
        if (exception is MyBusinessException businessEx && !businessEx.IsRetryable)
        {
            return Task.FromResult(PoisonDetectionResult.Poison(
                reason: "Business exception marked as non-retryable",
                detectorName: nameof(CustomPoisonDetector)));
        }

        // Check retry count
        if (processingInfo.AttemptCount >= 5)
        {
            return Task.FromResult(PoisonDetectionResult.Poison(
                reason: $"Exceeded {processingInfo.AttemptCount} attempts",
                detectorName: nameof(CustomPoisonDetector)));
        }

        return Task.FromResult(PoisonDetectionResult.NotPoison());
    }
}

// Register the custom detector
builder.Services.AddPoisonMessageDetector<CustomPoisonDetector>();

// Or via the dispatch builder
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddPoisonDetector<CustomPoisonDetector>();
});
```

### Configure Exception Types

```csharp
builder.Services.AddPoisonMessageHandling(options =>
{
    // Immediately poison these exceptions (no retry)
    options.PoisonExceptionTypes.Add(typeof(InvalidOperationException));
    options.PoisonExceptionTypes.Add(typeof(ArgumentNullException));
    options.PoisonExceptionTypes.Add(typeof(BusinessRuleViolationException));

    // Always retry these exceptions
    options.TransientExceptionTypes.Add(typeof(TimeoutException));
    options.TransientExceptionTypes.Add(typeof(HttpRequestException));
    options.TransientExceptionTypes.Add(typeof(SqlException));
});
```

## Adding Custom Metadata to Dead Letters

You can add custom metadata when enqueuing messages to the dead letter queue via the `metadata` parameter:

```csharp
public class CustomDeadLetterHandler
{
    private readonly IDeadLetterQueue _dlq;
    private readonly ICurrentUserService _currentUser;

    public CustomDeadLetterHandler(
        IDeadLetterQueue dlq,
        ICurrentUserService currentUser)
    {
        _dlq = dlq;
        _currentUser = currentUser;
    }

    public async Task HandleFailedMessageAsync<T>(
        T message,
        Exception exception,
        CancellationToken ct)
    {
        // Add custom metadata when dead-lettering
        var metadata = new Dictionary<string, string>
        {
            ["ProcessedBy"] = Environment.MachineName,
            ["UserId"] = _currentUser.UserId ?? "system",
            ["Timestamp"] = DateTimeOffset.UtcNow.ToString("O")
        };

        await _dlq.EnqueueAsync(
            message,
            DeadLetterReason.UnhandledException,
            exception,
            metadata,
            ct);
    }
}
```

## Supported Providers

| Provider | Package | Use Case |
|----------|---------|----------|
| In-Memory | `Dispatch` (included) | Testing, development, single-node |
| SQL Server | `Excalibur.Data.SqlServer` | SQL Server production |
| Elasticsearch | `Excalibur.Data.ElasticSearch` | Analytics, search, audit |

### SQL Server Provider

```csharp
using Microsoft.Extensions.DependencyInjection;

// Simple registration with connection string
builder.Services.AddSqlServerDeadLetterStore(connectionString);

// Or with full configuration
builder.Services.AddSqlServerDeadLetterStore(options =>
{
    options.ConnectionString = connectionString;
    options.TableName = "DeadLetterMessages";  // default
    options.SchemaName = "dbo";  // default
});
```

## Best Practices

### 1. Set Appropriate Retention

```csharp
options.DeadLetterRetentionPeriod = TimeSpan.FromDays(30);
options.EnableAutoCleanup = true;
```

### 2. Monitor Dead Letter Counts

```csharp
public class DeadLetterHealthCheck : IHealthCheck
{
    private readonly IDeadLetterQueue _dlq;
    private readonly int _threshold = 100;

    public DeadLetterHealthCheck(IDeadLetterQueue dlq) => _dlq = dlq;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct)
    {
        var count = await _dlq.GetCountAsync(
            DeadLetterQueryFilter.PendingOnly(), ct);

        if (count > _threshold)
        {
            return HealthCheckResult.Degraded(
                $"Dead letter queue has {count} pending entries");
        }

        return HealthCheckResult.Healthy();
    }
}
```

### 3. Alert on Thresholds

```csharp
builder.Services.AddPoisonMessageHandling(options =>
{
    options.EnableAlerting = true;
    options.AlertThreshold = 10;  // Alert if 10+ failures
    options.AlertTimeWindow = TimeSpan.FromMinutes(15);
});
```

### 4. Review Before Replay

Always review dead letter entries before replaying to understand the root cause:

```csharp
var entry = await _dlq.GetEntryAsync(entryId, ct);
if (entry is not null)
{
    _logger.LogInformation(
        "Reviewing entry {Id}: Reason={Reason}, Exception={Exception}",
        entry.Id,
        entry.Reason,
        entry.ExceptionMessage);

    // Only replay if the underlying issue is fixed
    if (IsIssueeFixed(entry))
    {
        await _dlq.ReplayAsync(entryId, ct);
    }
}
```

### 5. Categorize by Reason

Use filtering to handle different failure categories appropriately:

```csharp
// Validation failures: Review and fix data
var validationFailures = await _dlq.GetEntriesAsync(
    ct, DeadLetterQueryFilter.ByReason(DeadLetterReason.ValidationFailed));

// Transient failures: Usually safe to replay
var transientFailures = await _dlq.GetEntriesAsync(
    ct, DeadLetterQueryFilter.ByReason(DeadLetterReason.MaxRetriesExceeded));

// Handler not found: Missing handler registration
var missingHandlers = await _dlq.GetEntriesAsync(
    ct, DeadLetterQueryFilter.ByReason(DeadLetterReason.HandlerNotFound));
```

## Transport-Native Dead Letter Queues

In addition to the application-level `IDeadLetterQueue` described above, each transport can implement `IDeadLetterQueueManager` from `Excalibur.Dispatch.Transport.Abstractions` for native DLQ support:

| Transport | DLQ Mechanism | Status | Registration |
|-----------|--------------|--------|--------------|
| Google Pub/Sub | Subscription-based | Available | Built-in |
| AWS SQS | Queue-based (native redrive via `IAmazonSQS`) | Available | Built-in |
| Kafka | Topic-based (`{topic}.dead-letter`) | Available | `AddKafkaDeadLetterQueue()` |
| Azure Service Bus | Native `$DeadLetterQueue` subqueue | Available | `AddServiceBusDeadLetterQueue()` |
| RabbitMQ | Dead letter exchange (DLX) | Available | `AddRabbitMqDeadLetterQueue()` |

All five transports implement the `IDeadLetterQueueManager` interface from `Excalibur.Dispatch.Transport.Abstractions`, providing a consistent API for move, retrieve, reprocess, statistics, and purge operations regardless of transport choice.

See the [Transports](../transports/index.md#dead-letter-queue-support) page for configuration details and code examples.

## Related Patterns

- [Outbox Pattern](outbox.md) - Reliable message publishing
- [Inbox Pattern](inbox.md) - Idempotent message processing
- [Claim Check Pattern](claim-check.md) - Handle large payloads

## See Also

- [Error Handling & Recovery Guide](error-handling.md) -- End-to-end walkthrough of retries, circuit breakers, and DLQ composition
- [Polly Resilience](../operations/resilience-polly.md) -- Configure retry policies, circuit breakers, timeouts, and bulkheads
- [Health Checks](../observability/health-checks.md) -- Monitor DLQ depth and processing health
- [Recovery Runbooks](../operations/recovery-runbooks.md) -- Operational procedures for replaying and recovering failed messages
