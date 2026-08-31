---
sidebar_position: 2
title: Operational Resilience
description: Transient error handling, retry policies, and recovery strategies across providers
---

# Operational Resilience

Excalibur providers are designed for operational resilience, handling transient failures, connection disruptions, and recovery scenarios automatically. This guide covers the retry policies, transient error catalogs, and recovery options available across all supported providers.

## Before You Start

- **.NET 10.0**
- Install the packages for your data provider:
  ```bash
  dotnet add package Excalibur.Data.SqlServer  # or Excalibur.Data.Postgres
  ```
- Familiarity with [data access](../data-access/idb-interface.md) and [Polly resilience](./resilience-polly.md) concepts

## Retry Policies

### SQL Server

The `SqlServerRetryPolicy` handles transient failures with exponential backoff automatically. Configure SQL Server stores with their storage-specific options:

```csharp
// Event sourcing - configure storage options via builder
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseSqlServer(options =>
    {
        options.ConnectionString = connectionString;
        options.EventStoreSchema = "dbo";
        options.EventStoreTable = "EventStoreEvents";
    });
}));

// Outbox - configure storage and processing via fluent builder
services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
{
    outbox.UseSqlServer(opts => opts.ConnectionString(connectionString))
          .WithProcessing(p => p.MaxRetryCount(5)
                                .RetryDelay(TimeSpan.FromMinutes(5)))
          .EnableBackgroundProcessing();
}));
```

**Transient Error Codes:**

| Error Code | Description | Impact |
|------------|-------------|--------|
| 596 | Session killed by backup/restore | Critical for CDC processors |
| 9001, 9002 | Transaction log unavailable/full | Write operations fail |
| 3960, 3961 | Snapshot isolation conflicts | Concurrent write conflicts |
| 1204, 1205, 1222 | Lock/deadlock errors | Transaction conflicts |
| 40143, 40613, 40501 | Azure SQL service errors | Service unavailable |
| 49918-49920 | Resource governance | Throttling |
| 20, 64, 233 | Connection errors | Network issues |
| -2, 2, 53 | Network errors | Connectivity loss |

**Recovery Behavior:**
- Automatic retry with exponential backoff (1s, 2s, 4s, 8s...)
- Connection recreation on retry
- Logging of retry attempts for observability

### PostgreSQL

`PostgresDataRequestRetryPolicy` retries PostgreSQL data requests that fail transiently. It is the policy the Postgres persistence provider uses unless you supply your own, so it is what you get from the registration below without any extra configuration:

```csharp
// Event sourcing with PostgreSQL - configure storage options
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
    es.UsePostgres(pg => pg
        .ConnectionString(connectionString)
        .EventStoreSchema("public")
        .EventStoreTable("event_store_events"))));
```

**How a failure is classified as transient:**

`PostgresDataRequestRetryPolicy` does **not** classify by SQLSTATE. It asks two questions, in order:

| Exception | Retried? | Decided by |
|-----------|----------|------------|
| `NpgsqlException` with `IsTransient == true` | Yes | **Npgsql**, not this framework |
| Any other `DbException` (including a non-transient `NpgsqlException`) | Only if its **message** contains one of `TIMEOUT`, `CONNECTION`, `NETWORK`, `DEADLOCK`, `LOCK TIMEOUT`, `CONNECTION RESET`, `BROKEN PIPE` (case-insensitive) | This framework |
| `TimeoutException`, `SocketException`, `IOException` | Yes | This framework |
| `OperationCanceledException` | No — cancellation is never retried | This framework |
| Everything else | No | This framework |

:::caution Two limits worth knowing before you rely on this

**The SQLSTATE set is Npgsql's, and it is not reproduced here.** Which server error codes make
`NpgsqlException.IsTransient` return `true` is decided entirely by the Npgsql driver version you
have referenced, and it can change between driver releases. Consult Npgsql's own documentation for
the authoritative list — a copy published here would be a snapshot of one driver version presented as
a framework contract.

**The second rule matches on the exception *message*, not on an error code.** A `DbException` that
is not an `NpgsqlException` — or an `NpgsqlException` that Npgsql does not consider transient — is
retried only when its message text contains one of the substrings above. Message text is not part of
any stable contract: it varies with server locale and can be reworded by a server or driver upgrade,
so a genuinely transient failure whose message does not contain one of those words is **not**
retried, and an unrelated failure that happens to contain one **is**. Do not depend on this second
rule for correctness — make the operation idempotent and treat retry as an optimisation.
:::

**Recovery behaviour:** up to `MaxRetryAttempts` retries (default **3**) with exponential backoff
from `RetryDelayMilliseconds` (default **1000 ms**) — so 1s, 2s, 4s — capped by
`MaxRetryDelayMilliseconds` (default **30000 ms**), plus up to 10% random jitter. A fresh connection
is obtained on every attempt. All three values are configurable through the persistence options'
`Resilience` section.

### Cloud Providers

Cloud providers (CosmosDB, DynamoDB, Firestore) primarily use SDK-managed retry policies:

```csharp
// CosmosDB - SDK handles 408, 503, 504, 429 automatically
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
{
    es.UseCosmosDb(cosmos =>
    {
        cosmos.ConnectionString(connectionString)
              .DatabaseName("events");
    });
}));
```

## CDC Position Recovery

CDC (Change Data Capture) processors maintain position state to resume after failures.

### SQL Server CDC Recovery

```csharp
// SQL Server CDC with fluent builder configuration
services.AddCdcProcessor(cdc =>
{
    cdc.UseSqlServer(sql =>
    {
        sql.ConnectionString(connectionString)
           .SchemaName("Cdc")
           .StateTableName("CdcProcessingState");
    })
    .WithRecovery(recovery =>
    {
        recovery.Strategy(StalePositionRecoveryStrategy.FallbackToEarliest)
                .MaxAttempts(3);
    })
    .EnableBackgroundProcessing();
});
```

**Recovery Strategies:**

| Strategy | Behavior | Use Case |
|----------|----------|----------|
| `FallbackToEarliest` | Resume from oldest available position | Data consistency priority |
| `FallbackToLatest` | Resume from current position | Skip missing events |
| `Throw` | Fail with detailed error | Manual intervention required |
| `InvokeCallback` | Custom handling via callback | Complex recovery scenarios |

### PostgreSQL CDC Recovery

```csharp
services.AddPostgresCdc(options =>
{
    options.ConnectionString = connectionString;
    options.PublicationName = "excalibur_cdc_publication";  // Default
    options.ReplicationSlotName = "excalibur_cdc_slot";     // Default
});

// Stale-position recovery is configured once for all CDC providers:
services.AddCdc(cdc =>
{
    cdc.RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest;
});
```

### CosmosDB CDC Recovery

```csharp
services.AddCosmosDbCdc(options =>
{
    options.ConnectionString = connectionString;
    options.DatabaseId = "mydb";
    options.ContainerId = "events";
    options.ProcessorName = "cdc-processor";  // Default
    options.Mode = CosmosDbCdcMode.LatestVersion;  // or AllVersionsAndDeletes
});
```

## Connection Recovery

### Long-Running Processors

CDC and projection processors use long-lived connections. Handle connection loss gracefully:

```csharp
public class ResilientProjectionProcessor : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _processor.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                _logger.LogWarning(ex, "Transient failure in projection processor, restarting...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
```

### Connection Pool Health

For SQL Server and PostgreSQL, ensure connection pool health after failures:

```csharp
// SQL Server - ClearAllPools after major failures
SqlConnection.ClearAllPools();

// PostgreSQL - Clear connection pool
NpgsqlConnection.ClearAllPools();
```

## Failure Mode Coverage

| Failure Mode | SQL Server | PostgreSQL | CosmosDB | DynamoDB | MongoDB |
|--------------|------------|------------|----------|----------|---------|
| Process restart | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full |
| Database restart | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full |
| Backup/restore (LSN rollback) | ✅ Full | ✅ Full | N/A | N/A | N/A |
| Killed session (error 596) | ✅ Full | ✅ Full | N/A | N/A | N/A |
| Network partition/timeout | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full |
| Throttling/rate limits | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full |
| Failover/replica promotion | ✅ Full | ✅ Full | ✅ Full | ✅ Full | ✅ Full |

**Legend:**
- ✅ Full - Automatic recovery with no manual intervention
- ⚠️ Partial - May require manual intervention
- N/A - Not applicable to provider

## Observability

### Metrics

All retry operations emit metrics via OpenTelemetry:

- `dispatch.write_store.operations_total` - Total number of write-side store operations (tagged by store, provider, operation, result)
- `dispatch.write_store.operation_duration` - Duration of write-side store operations in milliseconds

### Logging

Retry attempts are logged at Warning level:

```
SQL Server operation failed with transient error. Retry 1 after 1000ms
PostgreSQL operation failed with transient error. Retry 2 after 2000ms
```

## Best Practices

1. **Configure appropriate retry counts** - Balance between recovery and fail-fast
2. **Monitor retry metrics** - High retry rates indicate underlying issues
3. **Use CDC recovery options** - Configure stale position handling for your use case
4. **Implement circuit breakers** - Prevent cascade failures with Polly
5. **Clear connection pools** - After major failures, clear stale connections

## Related Documentation

- [Recovery Runbooks](recovery-runbooks.md) - Step-by-step recovery procedures
- [Observability](../observability/index.md) - Monitoring retry metrics
- [Event Store](../event-sourcing/event-store.md) - Event store operations

## See Also

- [Resilience with Polly](resilience-polly.md) - Polly integration for circuit breakers and retries
- [Operations Overview](index.md) - Operational guides and runbooks
- [Error Handling](../patterns/error-handling.md) - Error handling patterns and dead letter queues

