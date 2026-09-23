---
sidebar_position: 2
title: CDC Troubleshooting
description: Troubleshoot Change Data Capture stale positions and recovery
---

# CDC Troubleshooting

Change Data Capture (CDC) issues can cause projection lag, missed events, and data inconsistency. This guide covers common problems and recovery procedures.

## Before You Start

- **.NET 10.0**
- A running CDC deployment with SQL Server CDC enabled
- Familiarity with [CDC patterns](../patterns/cdc.md) and [recovery runbooks](./recovery-runbooks.md)

## Common CDC Issues

| Issue | Symptoms | Severity |
|-------|----------|----------|
| **Stale position** | Projection lag increasing | High |
| **Missing events** | Data gaps in projections | Critical |
| **Position corruption** | CDC processor errors | Critical |
| **Log truncation** | Events unavailable | Critical |

## Diagnosing Stale Positions

### Check CDC Position

```csharp
// Check CDC processor position via provider-specific processor
// (e.g., IPostgresCdcProcessor, ISqlServerCdcProcessor)
var position = await _cdcProcessor.GetCurrentPositionAsync(ct);
_logger.LogInformation("CDC position: {Position}", position);
```

### SQL Server CDC Status

```sql
-- Check CDC is enabled
SELECT name, is_cdc_enabled FROM sys.databases WHERE name = DB_NAME();

-- Check capture instance
SELECT * FROM cdc.change_tables;

-- Check current LSN vs max available LSN
SELECT
    sys.fn_cdc_get_min_lsn('EventSourcing_Events') AS MinLsn,
    sys.fn_cdc_get_max_lsn() AS MaxLsn;

-- Check for stale position
SELECT
    capture_instance,
    start_lsn,
    DATEDIFF(MINUTE, create_date, GETDATE()) AS MinutesSinceStart
FROM cdc.lsn_time_mapping
ORDER BY create_date DESC;
```

### PostgreSQL Replication Status

```sql
-- Check replication slot
SELECT * FROM pg_replication_slots WHERE slot_name = 'excalibur_cdc';

-- Check replication lag
SELECT
    slot_name,
    pg_size_pretty(pg_wal_lsn_diff(pg_current_wal_lsn(), restart_lsn)) AS lag
FROM pg_replication_slots;
```

## Stale Position Recovery

### Automatic Recovery

Excalibur includes automatic stale position detection:

```csharp
services.AddCdcProcessor(cdc =>
{
    cdc.UseSqlServer(sql => sql.ConnectionString(connectionString))
       .WithRecovery(recovery =>
       {
           recovery.Strategy(StalePositionRecoveryStrategy.FallbackToEarliest)
                   .MaxAttempts(5)
                   .AttemptDelay(TimeSpan.FromSeconds(30));
       })
       .EnableBackgroundProcessing();
});
```

### Recovery Strategies

| Strategy | When to Use | Data Impact |
|----------|-------------|-------------|
| `FallbackToEarliest` | Data consistency priority | Reprocesses events from earliest available |
| `FallbackToLatest` | Data gaps acceptable | Skips missed events |
| `Throw` | Manual intervention required | Fails with detailed error |
| `InvokeCallback` | Complex scenarios | Custom handling via callback |

### SQL Error 313: Insufficient Arguments

SQL Server may raise error 313 ("An insufficient number of arguments were supplied for the procedure or function `cdc.fn_cdc_get_all_changes_*`") when the CDC table-valued function receives an LSN outside the valid range. This is a boundary condition variant of the more common errors 22037/22029.

**Symptoms:**
- `SqlException` with `Number = 313` in CDC processor logs
- Processing loop stops advancing for affected capture instances

**Resolution:** The framework detects error 313 automatically via `CdcStalePositionDetector` and maps it to `StalePositionReasonCodes.TvfInsufficientArguments`. If you have a recovery strategy configured (e.g., `FallbackToEarliest`), the position resets and processing resumes. If using the default `Throw` strategy, you will see the exception in logs and must reset the position manually.

**Tip:** Pair recovery with [idempotency filtering](../patterns/cdc.md#idempotency-filtering) to safely reprocess events after a position reset without duplicate side effects.

### Manual Recovery Procedure

1. **Stop CDC processor**

```bash
kubectl scale deployment cdc-processor --replicas=0
```

2. **Determine recovery point**

```sql
-- Find safe starting position
SELECT MIN(SequenceNumber) AS SafeStart
FROM EventSourcing.Events
WHERE Timestamp > DATEADD(DAY, -1, GETDATE());
```

3. **Reset position**

```csharp
await _cdcPositionStore.SetPositionAsync(
    new CdcPosition { SequenceNumber = safeStart },
    CancellationToken.None);
```

4. **Rebuild affected projections** (if needed)

```csharp
await _projectionRebuildService.RebuildAsync(
    projectionName: "OrderSummary",
    fromSequence: safeStart,
    CancellationToken.None);
```

5. **Restart CDC processor**

```bash
kubectl scale deployment cdc-processor --replicas=1
```

## Log Truncation Issues

### SQL Server Log Truncation

CDC requires transaction log retention. If logs are truncated:

```sql
-- Check if CDC capture job is running
EXEC sys.sp_cdc_help_jobs;

-- Start capture job if stopped
EXEC sys.sp_cdc_start_job @job_type = N'capture';

-- Check retention period
EXEC sys.sp_cdc_change_job
    @job_type = N'cleanup',
    @retention = 4320;  -- 3 days in minutes
```

### Prevention

```sql
-- Set adequate retention
EXEC sys.sp_cdc_change_job
    @job_type = N'cleanup',
    @retention = 10080;  -- 7 days

-- Monitor log space
SELECT
    DB_NAME(database_id) AS DatabaseName,
    log_reuse_wait_desc
FROM sys.databases
WHERE database_id = DB_ID();
```

### PostgreSQL WAL Retention

```sql
-- Check replication slot status
SELECT * FROM pg_replication_slots;

-- If slot is lagging, may need to drop and recreate
SELECT pg_drop_replication_slot('excalibur_cdc');
SELECT pg_create_logical_replication_slot('excalibur_cdc', 'pgoutput');
```

## Position Validation

### Detect Invalid Position

```csharp
public class CdcPositionValidator
{
    public async Task<PositionValidation> ValidateAsync(CancellationToken ct)
    {
        var currentPosition = await _positionStore.GetPositionAsync(ct);
        var minAvailable = await _cdcSource.GetMinAvailableAsync(ct);
        var maxAvailable = await _cdcSource.GetMaxAvailableAsync(ct);

        if (currentPosition < minAvailable)
        {
            return new PositionValidation
            {
                IsValid = false,
                Issue = PositionIssue.BehindMinimum,
                CurrentPosition = currentPosition,
                MinAvailable = minAvailable,
                RecommendedAction = "Reset to minimum available position"
            };
        }

        if (currentPosition > maxAvailable)
        {
            return new PositionValidation
            {
                IsValid = false,
                Issue = PositionIssue.AheadOfMaximum,
                CurrentPosition = currentPosition,
                MaxAvailable = maxAvailable,
                RecommendedAction = "Reset to maximum available position"
            };
        }

        return new PositionValidation { IsValid = true };
    }
}
```

## Projection Rebuild

When CDC recovery requires a projection rebuild, use `IProjectionRebuildService`. Register it with
`services.AddProjectionRebuild()`. A rebuild replays every event through the projection's handlers and
replaces the projection's existing state.

```csharp
using Excalibur.EventSourcing.Projections;

public sealed class CdcProjectionRecovery(IProjectionRebuildService rebuild, ILogger<CdcProjectionRecovery> logger)
{
    public async Task RebuildOrdersAsync(CancellationToken cancellationToken)
    {
        await rebuild.RebuildAsync<OrderSummaryProjection>(cancellationToken);

        var status = await rebuild.GetStatusAsync<OrderSummaryProjection>(cancellationToken);
        if (status.State == ProjectionRebuildState.Failed)
        {
            logger.LogError("Rebuild of {Projection} failed", status.ProjectionName);
        }
    }
}
```

`GetStatusAsync<T>()` reports the projection's `State` (`Idle`, `Rebuilding`, `Completed` or `Failed`), its
`Progress`, and when it was last rebuilt; `GetAllStatusesAsync()` reports every projection. Reads of the
projection during a rebuild see partially rebuilt state, so schedule it when that is acceptable.

## Monitoring and Alerting

### Health Check

```csharp
public class CdcHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(ct);

        if (!validation.IsValid)
        {
            return HealthCheckResult.Unhealthy(
                $"CDC position invalid: {validation.Issue}");
        }

        var lag = await _cdcProcessor.GetLagAsync(ct);

        if (lag > TimeSpan.FromMinutes(5))
        {
            return HealthCheckResult.Degraded(
                $"CDC lag: {lag.TotalSeconds}s");
        }

        return HealthCheckResult.Healthy();
    }
}
```

### Alerting

```yaml
# Prometheus alert rules
groups:
  - name: cdc
    rules:
      - alert: CDCPositionStale
        expr: excalibur_cdc_lag_seconds > 300
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "CDC position is stale"
          runbook: "https://docs/operations/cdc-troubleshooting"

      - alert: CDCPositionInvalid
        expr: excalibur_cdc_position_valid == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "CDC position is invalid"
```

## Database Restore Handling

When a database is restored from a backup (common in development/staging environments), the CDC processor handles two scenarios automatically:

### During the Restore (Database Unavailable)

The CDC processor survives database unavailability without crashing:

- All DB operations are wrapped in a resilience policy (retry with exponential backoff + circuit breaker) via `IDataAccessPolicyFactory`
- The durable checkpoint advances **only after a change is successfully processed** — a fault never advances it past an unprocessed change (every processor routes its per-iteration decision through `CdcFatalGuard.Decide`). A **transient** fault (such as the database being temporarily unavailable) is logged and the loop reconnects and retries from the un-advanced checkpoint without terminating; a **fatal** (non-retryable) fault stops the loop loudly rather than silently swallowing the error. State-store save failures surface the original exception instead of masking it.
- The circuit breaker opens after sustained failure, reducing load on the recovering database
- The health check transitions through Degraded → Unhealthy as inactivity duration increases

**No operator intervention required** — the processor automatically resumes when the database comes back online.

### After the Restore (Data Replaced)

A restored database may have different CDC LSN ranges than what the processor has checkpointed:

| Scenario | What Happens | Resolution |
|----------|-------------|------------|
| Checkpoint LSN is within the restored range | Processing resumes normally | Automatic |
| Checkpoint LSN is outside the restored range (stale) | `CdcStalePositionException` is raised | Handled by recovery strategy |
| CDC tables were not restored | No change data available | Re-enable CDC on restored database |

Configure a recovery strategy to handle stale positions automatically:

```csharp
services.AddCdcProcessor(cdc =>
{
    cdc.UseSqlServer(sql => sql.ConnectionString(connectionString))
       .TrackTable("dbo.Orders", t => t.MapAll<OrderChangedEvent>())
       .WithRecovery(recovery =>
       {
           // FallbackToEarliest: resume from earliest available position
           // (may reprocess some events — handlers should be idempotent)
           recovery.Strategy(StalePositionRecoveryStrategy.FallbackToEarliest)
                   .MaxAttempts(5)
                   .AttemptDelay(TimeSpan.FromSeconds(30));
       })
       .EnableBackgroundProcessing();
});
```

:::warning Development Environments

In environments where databases are frequently restored from production backups, use `FallbackToEarliest` or `FallbackToLatest` instead of the default `Throw` strategy. Ensure your event handlers are idempotent to safely handle reprocessed events.
:::

## Stream-Based Providers: DynamoDB, Cosmos DB and MongoDB

The sections above diagnose log-based providers, where a position is a log offset you can inspect.
Stream-based providers fail differently: their most common symptom is **change delivery stopping with
no error at all**, because the stream itself is a moving structure rather than a fixed log.

### DynamoDB: delivery stops and nothing is logged

DynamoDB Streams rotates shards as a normal part of operation — a shard closes and a successor takes
over, with no notification. A subscription that enumerated its shards once at start-up drains the
shards it knows about, finds them closed, and completes.

**What you would see:** the processor is running and healthy, its last checkpoint is valid, and no new
changes arrive. Nothing is logged, because from the subscription's point of view it finished normally.

**What the framework does now:** shards are re-enumerated on every poll, closed shards are retired, and
successors are picked up automatically. Two behaviours are worth knowing because they look surprising:

- **A disabled stream now throws** instead of completing quietly. A stream that has been turned off and
  a stream with nothing to say used to be the same observation; they are now distinguishable.
- **Shards discovered after start-up open at `TRIM_HORIZON`**, even when the processor was configured
  to start from *now*. A successor shard carries every write since its parent closed, so opening it at
  the latest position would skip exactly the records the rotation was about to deliver.

:::note If you configured "start from now" and see older records after a rotation, this is why
That is the gap-avoidance rule above, working as intended. It applies only to shards that appear
*after* the processor started — the initial position is still honoured for the shards present at
start-up.
:::

### DynamoDB: a subset of changes arrives, consistently

A single `DescribeStream` or `Scan` returns one page. A processor that treats the first page as the
whole population silently ignores every shard, and every stored checkpoint, past that boundary — so a
deployment large enough to page loses an arbitrary, stable subset of its changes.

**What you would see:** some changes flow and others never do, reproducibly, with no error. Often it
correlates with table growth rather than with any deployment.

**What to check if you suspect it:** compare the number of open shards reported by
`DescribeStream` against the number your processor is polling, and the number of rows in the CDC state
table against the number of positions your processor enumerates. A gap in either, on a table large
enough to page, is this shape.

### MongoDB: the stream never advances past a dropped or renamed collection

A MongoDB change stream that receives an **invalidate** event is finished — its resume token cannot be
used with `resumeAfter` again. A processor that reopens from its previous checkpoint after a collection
drop, rename or database drop either fails to resume or resumes at a point it can never move past.

**What you would see:** changes stop at the moment of the schema operation, and a restart does not help
because the stored position is the one that cannot advance.

**What the framework does now:** the invalidate event is detected, its own token is stored with a resume
mode of `startAfter` — the only operator that can carry a stream past an invalidate — and the stream is
reopened after the configured reconnect interval. The mode is persisted with the token, so a cold
restart reopens correctly too.

:::warning An invalidate means the collection you were watching is gone
Recovery resumes the *stream*. It does not recreate the collection or replay changes that occurred
while it did not exist. Treat an invalidate as an operational event worth alerting on, not merely a
reconnect.
:::

### Cosmos DB: changes arrive but cannot be addressed to a partition

If a container uses a **numeric** partition key, or a **nested** partition-key path such as
`/payload/tenantId`, confirm your configured `PartitionKeyPath` matches the container's definition
exactly. Cosmos treats the number `42` and the string `"42"` as different partition keys, so a
mismatch produces changes that resolve to no partition rather than an error.

## Prevention Best Practices

| Practice | Benefit |
|----------|---------|
| Enable automatic recovery | Reduces manual intervention |
| Register `IDataAccessPolicyFactory` | Automatic retry and circuit breaker for all DB operations |
| Set adequate log retention | Prevents truncation issues |
| Monitor CDC lag | Early warning of problems |
| Regular position validation | Detect issues before impact |
| Checkpoint frequently | Faster recovery |
| Make event handlers idempotent | Safe reprocessing after restore or recovery |
| Test recovery procedures | Confidence in recovery |

## Quick Reference

### Recovery Commands

```bash
# Stop CDC processor
kubectl scale deployment cdc-processor --replicas=0

# Check current position
kubectl exec -it cdc-processor -- dotnet cdc position show

# Reset position
kubectl exec -it cdc-processor -- dotnet cdc position reset --to-latest

# Start CDC processor
kubectl scale deployment cdc-processor --replicas=1

# Trigger projection rebuild
kubectl exec -it cdc-processor -- dotnet projection rebuild OrderSummary
```

### SQL Server Quick Checks

```sql
-- CDC status
SELECT is_cdc_enabled FROM sys.databases WHERE name = DB_NAME();

-- Capture job status
EXEC sys.sp_cdc_help_jobs;

-- Available LSN range
SELECT
    sys.fn_cdc_get_min_lsn('EventSourcing_Events') AS Min,
    sys.fn_cdc_get_max_lsn() AS Max;
```

## See Also

- [Change Data Capture Pattern](../patterns/cdc.md) — Architecture and implementation of the CDC pattern
- [Recovery Runbooks](recovery-runbooks.md) — Step-by-step recovery procedures for common failure scenarios
- [Production Observability](../observability/production-observability.md) — Monitoring and alerting for production environments
