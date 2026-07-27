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

- **.NET 10.0**
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

## Two dead-letter families — check which one you are installing

This page documents **two independent dead-letter surfaces**. They are not alternative providers of one abstraction; they are separate types with different guarantees, and the registration you call decides which one you get.

| | `IDeadLetterStore` (poison-message) | `IDeadLetterQueue` (outbox/transport) |
|---|---|---|
| Installed by | `AddPoisonMessageHandling()`, `UseInMemoryDeadLetterStore()`, `AddSqlServerDeadLetterStore()` | `AddSqlServerOutbox(...)`, `AddKafkaDeadLetterQueue()` and the other transport registrations |
| Admin surface | `IDeadLetterStoreAdmin` | `IDeadLetterQueueAdmin` |
| Stores the failed message body | Yes | Yes |
| Tenant term | Every operation keys by the **ambient** tenant | Keys by the **ambient** tenant for reads and replay; **purge is estate-wide by design** (admin surface) |

**Every tenancy statement on this page names the family it binds.** A warning that names one family says nothing about the other. Both families now scope their tenant-facing reads to the ambient tenant; they differ on the admin surface, where `IDeadLetterQueueAdmin` purges across every tenant deliberately.

:::info `IDeadLetterStore` scopes every operation to the ambient tenant

All seven operations on `IDeadLetterStore` — store, fetch-by-id, list, mark-replayed, delete, count, and the retention cleanup — key by the tenant in scope when you call them. Rows carry a tenant column, written on store and required on every subsequent match, so one tenant's listing, count, delete and cleanup cannot reach another tenant's entries.

**The tenant is ambient, not a parameter.** No method takes a tenant argument; each resolves the tenant from the scope the call runs in. Two obligations follow for you:

- **Establish the tenant scope before you call.** A call made with no scope resolves under the untenanted partition — it does not fall back to "all tenants", so a forgotten scope loses sight of entries rather than exposing them, but it will look empty and it is not an error.
- **A single-tenant host registers no tenant context at all.** That is supported: every row lands in the untenanted partition and every operation matches it consistently. Nothing to configure.

Entries still contain the full failed-message body, so ordinary care about who may call these operations applies — but a correctly-scoped tenant call no longer discloses another tenant's content.
:::

## Basic Configuration

The registrations in this section install the **`IDeadLetterStore` (poison-message) family** — see the disclosure above for what that means in a multi-tenant host.

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
    dispatch.UsePoisonMessageHandling(options =>
    {
        options.MaxRetryAttempts = 5;
        options.DeadLetterRetentionPeriod = TimeSpan.FromDays(30);
        options.EnableAutoCleanup = true;
    });

    // Or use in-memory dead letter store
    dispatch.UseInMemoryDeadLetterStore();
});
```

### Auto-Dead-Letter on Retry Exhaustion

For in-process dispatches, the opt-in `DeadLetterOnExhaustionMiddleware` automatically routes a message
to the dead-letter queue once it exhausts **every** retry attempt, without writing a custom poison
detector. Register it with `AddDeadLetterOnExhaustion()`:

```csharp
using Microsoft.Extensions.DependencyInjection;

// Opt in to auto-dead-letter on retry exhaustion.
builder.Services.AddDeadLetterOnExhaustion();

// Place the middleware UPSTREAM of the retry middleware so the retry middleware runs as its
// `next` delegate and the decorator can observe the retry-exhaustion terminal it returns.
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseMiddleware<DeadLetterOnExhaustionMiddleware>();
    // ... retry middleware registered after this ...
});
```

- The decorator dead-letters with reason `DeadLetterReason.MaxRetriesExceeded` **only** — it composes with
  `PoisonMessageMiddleware` (which owns `PoisonMessage` / `DeserializationFailed`) rather than duplicating it.
- **An `IDeadLetterQueue` is required — there is no default.** The middleware routes messages that have
  exhausted every retry attempt, so a host without a store would drop them. Register one (for example
  `AddSqlServerOutbox(...)`, which registers `SqlServerDeadLetterQueue`) before building the host.

:::warning The failure surfaces at first resolve, not at startup

If no `IDeadLetterQueue` is registered, `BuildServiceProvider()` **returns normally** — the registration is
a factory, so the failure appears the first time the middleware is resolved:

```
System.InvalidOperationException

AddDeadLetterOnExhaustion() requires an IDeadLetterQueue, and none is registered. The
middleware routes messages that exhaust every retry attempt, so a host without a store
would drop them. Register one before building the host - for example
AddSqlServerOutbox(...), which registers SqlServerDeadLetterQueue. If discarding
exhausted messages is genuinely intended, make that choice explicit with
services.AddSingleton<IDeadLetterQueue>(NullDeadLetterQueue.Instance); each discarded
message is then logged as discarded rather than reported as dead-lettered.
```

A successful `BuildServiceProvider()` is therefore not evidence that the queue is wired. To check it at
startup, resolve the middleware itself and let the failure surface:

```csharp
// Fails fast on a queue-less host, at startup, on your machine.
_ = provider.GetRequiredService<DeadLetterOnExhaustionMiddleware>();
```

Assert the same thing in an integration test. Resolve **this type specifically** — a check that only
constructs surrounding infrastructure is not equivalent, and a startup that completes without resolving
this middleware tells you nothing about whether a store is registered.

:::

- **Discarding is available, but only as an explicit choice.** If losing exhausted messages is genuinely
  intended, register the no-op queue yourself:

  ```csharp
  services.AddSingleton<IDeadLetterQueue>(NullDeadLetterQueue.Instance);
  ```

  The middleware then logs each message as **discarded** rather than reporting it as dead-lettered — the two
  paths emit different events, so a discard is never mistaken for storage in your telemetry.

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
    /// Gets the current count of entries in the dead letter queue.
    /// </summary>
    Task<long> GetCountAsync(
        CancellationToken cancellationToken,
        DeadLetterQueryFilter? filter = null);
}

/// <summary>
/// Admin operations (batch replay, purge) for the dead letter queue.
/// Every operation on this interface is estate-wide: it addresses entries in
/// every tenant, not only the ambient one.
/// </summary>
public interface IDeadLetterQueueAdmin
{
    /// <summary>
    /// Replays multiple dead letter entries that match the specified filter.
    /// Estate-wide: the selection is not narrowed by the ambient tenant. Each
    /// replayed message re-enters the tenant its entry was stored under.
    /// The result reports whether the batch was cut short -- see the note below.
    /// </summary>
    Task<ReplayBatchResult> ReplayBatchAsync(
        DeadLetterQueryFilter filter,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Purges (permanently deletes) a dead letter entry.
    /// Estate-wide: addresses the entry in whichever tenant holds it.
    /// </summary>
    Task<bool> PurgeAsync(
        Guid entryId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Purges all dead letter entries older than the specified age.
    /// Estate-wide and irreversible: deletes matching entries in every tenant
    /// on an age predicate alone. There is no tenant term in the selection.
    /// </summary>
    Task<int> PurgeOlderThanAsync(
        TimeSpan olderThan,
        CancellationToken cancellationToken);
}
```

:::danger `IDeadLetterQueueAdmin` is an operator surface, not a tenant surface

*Binds the `IDeadLetterQueue` (outbox/transport) family. The `IDeadLetterStore` family scopes every operation to the ambient tenant — see the top of this page. Do not carry this warning across to it.*

**Every operation on `IDeadLetterQueueAdmin` crosses tenant boundaries.** `PurgeOlderThanAsync` selects on an age predicate alone — there is no tenant term in the selection — so it permanently deletes matching entries in **every** tenant, and the deletion is not recoverable.

A multi-tenant host that resolves this interface into a tenant-facing request path lets one tenant delete another tenant's failed messages. Inject it only into operator tooling that is already authorized across the estate, and never behind an endpoint a tenant can reach.

Keep the two apart at your composition root — and read the note below before assuming the non-admin interface is safe to hand a tenant.

:::

:::info `IDeadLetterQueue` reads and replay are tenant-scoped; `IDeadLetterQueueAdmin` is estate-wide by design

*Binds the `IDeadLetterQueue` (outbox/transport) family. It is not a statement about the `IDeadLetterStore` family, which has no tenant term on any operation.*

The shipped `IDeadLetterQueue` implementation resolves its tenant-facing operations within the **ambient tenant**: `GetEntriesAsync`, `GetEntryAsync`, `ReplayAsync` and `GetCountAsync` each carry the ambient tenant scope into the query, so an entry stored under another tenant is not listed, not fetched, not replayable, and not counted from a caller's own context. Counting is included deliberately: a total taken across tenants would disclose another tenant's failure volume even though no entry of theirs is readable. Replay continues to re-enter the tenant the entry was **stored** under, never the caller's — those are two separate properties and both hold.

**Purging is deliberately not scoped, and is not on this interface.** `PurgeAsync` and `PurgeOlderThanAsync` are declared on `IDeadLetterQueueAdmin`, the privileged operator surface, and resolve entries across every tenant **on purpose** — an operator must be able to address any tenant's entry. Do not register the admin interface for tenant-facing injection, and keep it out of tenant-reachable code paths. That is a design boundary, not a gap.

**Verify before you rely on it.** This describes the shipped SQL Server implementation. A custom `IDeadLetterQueue` you write is scoped only insofar as you scope it, and the interface's own contract documentation is being brought in line with this behaviour — treat your own implementation's isolation as your responsibility to test.
:::

:::note `ReplayBatchAsync` tells you when the batch was cut short

The result reports three things, and the third is the one to branch on:

| Member | Meaning |
|---|---|
| `Enumerated` | How many entries the filter selected, up to your `limit`. |
| `Replayed` | How many were actually replayed. Lower than `Enumerated` when an individual entry could not be replayed. |
| `Truncated` | `true` when the limit was reached and **further entries may still match the filter** — the batch is incomplete. |

**Drive your drain loop off `Truncated`, not off the counts.** A batch that returns exactly `limit` entries is indistinguishable by count alone from one that happened to drain the queue; `Truncated` is what separates them.

```csharp
ReplayBatchResult result;
do
{
    result = await dlqAdmin.ReplayBatchAsync(filter, limit: 500, ct);
    logger.LogInformation(
        "Replayed {Replayed} of {Enumerated} entries", result.Replayed, result.Enumerated);
}
while (result.Truncated);
```

`Replayed < Enumerated` means individual entries failed to replay and remain in the queue — it is not an error, and those entries are still selectable by the same filter on a later pass.

:::

## Dead Letter Reasons

Messages can be dead lettered for various reasons:

| Reason | Description |
|--------|-------------|
| `MaxRetriesExceeded` | Message exceeded the maximum number of retry attempts |
| `CircuitBreakerOpen` | Circuit breaker was open. **Note:** the built-in inbox/outbox processors no longer dead-letter on this condition — a transient open breaker leaves the message for retry (attempt count unchanged). This enum value is retained for compatibility and custom DLQ routing. |
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

The service below is **operator tooling**. Listing, fetching, replaying, and purging all select without a tenant term today (see the disclosure above), so a service shaped like this does not belong in a request path a tenant can reach.

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
    private readonly IDeadLetterQueueAdmin _dlqAdmin;

    public DeadLetterRecoveryService(
        IDeadLetterQueue dlq,
        IDeadLetterQueueAdmin dlqAdmin)
    {
        _dlq = dlq;
        _dlqAdmin = dlqAdmin;
    }

    // Replay a single entry
    public async Task<bool> ReplayEntryAsync(Guid entryId, CancellationToken ct)
    {
        return await _dlq.ReplayAsync(entryId, ct);
    }

    // Batch replay all validation failures (after fixing validation logic)
    public async Task<ReplayBatchResult> ReplayValidationFailuresAsync(CancellationToken ct)
    {
        var filter = DeadLetterQueryFilter.ByReason(DeadLetterReason.ValidationFailed);
        return await _dlqAdmin.ReplayBatchAsync(filter, limit: 500, ct);
    }

    // Replay all pending entries for a specific message type
    public async Task<ReplayBatchResult> ReplayByTypeAsync(string messageType, CancellationToken ct)
    {
        var filter = new DeadLetterQueryFilter
        {
            MessageType = messageType,
            IsReplayed = false
        };
        return await _dlqAdmin.ReplayBatchAsync(filter, limit: 500, ct);
    }
}
```

### Cleanup and Purging

The cleanup service below is **operator tooling**. `PurgeOlderThanAsync` deletes across every tenant on age alone, so a service like this belongs in an admin host or a scheduled job that is authorized estate-wide — not in a request path a tenant can reach.

```csharp
public class DeadLetterCleanupService
{
    private readonly IDeadLetterQueue _dlq;
    private readonly IDeadLetterQueueAdmin _dlqAdmin;

    public DeadLetterCleanupService(
        IDeadLetterQueue dlq,
        IDeadLetterQueueAdmin dlqAdmin)
    {
        _dlq = dlq;
        _dlqAdmin = dlqAdmin;
    }

    // Purge a single entry (admin operation)
    public async Task<bool> PurgeEntryAsync(Guid entryId, CancellationToken ct)
    {
        return await _dlqAdmin.PurgeAsync(entryId, ct);
    }

    // Purge entries older than 30 days (admin operation)
    public async Task<int> PurgeOldEntriesAsync(CancellationToken ct)
    {
        return await _dlqAdmin.PurgeOlderThanAsync(TimeSpan.FromDays(30), ct);
    }

    // Get count of pending entries
    public async Task<long> GetPendingCountAsync(CancellationToken ct)
    {
        return await _dlq.GetCountAsync(ct, DeadLetterQueryFilter.PendingOnly());
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
            ct,
            exception,
            metadata);
    }
}
```

## Supported Providers

These providers back the **`IDeadLetterStore` (poison-message) family**. The in-memory, SQL Server and PostgreSQL stores all key every operation by the ambient tenant, as described at the top of this page.

**The Elasticsearch entry is the exception, and it is not the same kind of thing.** `ElasticsearchDeadLetterHandler` does not implement `IDeadLetterStore`; it is a standalone handler that indexes a failed document, and it carries no tenant term of any kind — nothing written to the index identifies the tenant, and there is no scoped read to retrieve from it. In a multi-tenant host it writes every tenant's failed payloads into one unpartitioned index. Do not reach for it expecting the isolation the other three provide, and do not expose an index it writes to a tenant.

| Provider | Package | Use Case |
|----------|---------|----------|
| In-Memory | `Dispatch` (included) | Testing, development, single-node |
| SQL Server | `Excalibur.Data.SqlServer` | SQL Server production |
| Elasticsearch | `Excalibur.Data.ElasticSearch` | Analytics, search, audit |

### SQL Server Provider

```csharp
using Microsoft.Extensions.DependencyInjection;

// Simple registration with connection string
builder.Services.AddSqlServerDeadLetterStore(opts => opts.ConnectionString = connectionString);

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
            ct, DeadLetterQueryFilter.PendingOnly());

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
