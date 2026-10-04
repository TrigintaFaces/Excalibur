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

Read the configured SQL Server state store, which may be in a separate database. With the default schema:

```sql
SELECT DatabaseConnectionIdentifier, DatabaseName, TableName, LastProcessedLsn, LastProcessedSequenceValue
FROM [Cdc].[CdcProcessingState];
```

Compare each saved LSN with that capture instance's minimum and the source database's maximum.

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
SELECT TOP (20) start_lsn, tran_begin_time, tran_end_time
FROM cdc.lsn_time_mapping
ORDER BY start_lsn DESC;
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
`services.AddProjectionRebuild()`. A rebuild replays every event through the projection's handlers, folding
each event into the same projection id the live apply path would fold it into, and rewrites every id the
replay touched.

:::warning Stop the projection's processor first
A rebuild writes each id conditionally on the position that id held when the rebuild first read it. If a
live writer advances one of those rows while the replay is running, the write is **refused** and the
rebuild throws — naming the id it stopped on. Ids written before that one hold rebuilt state and the rest
do not, so the read model is left half-rebuilt. Re-running from scratch is the recovery and is always safe.

The read model is unavailable for the duration. Nothing in the framework can detect that a processor is
still running, so this is on you.
:::

A rebuild is **additive**: it rewrites every id the replayed stream produces, and leaves rows for ids the
stream no longer produces. That matters for erasure — a fully erased aggregate's events are all tombstones,
which the replay skips before deriving a key, so a rebuild produces no id for that subject and cannot clear
its row. Use `IProjectionRecovery.ReapplyAsync` for a single subject's own row.

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
`Progress`, and when it was last rebuilt; `GetAllStatusesAsync()` reports every projection. `RebuildAsync`
also **throws** on failure, so the status check above is for a caller that wants the reason without
catching; a caller that does not catch will see the exception.

Memory: a rebuild holds one instance of the projection per distinct id the stream produces, until it
writes. For a projection keyed per aggregate over a large store, that is one instance per aggregate.

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

Database unavailability is retried according to the configured policies:

- All DB operations are wrapped in a resilience policy (retry with exponential backoff + circuit breaker) via `IDataAccessPolicyFactory`
- Durable checkpoints advance only after successful handling. SQL Server batches surface failures after configured retries are exhausted. A Quartz firing then fails without refreshing its success heartbeat; a subsequent scheduled firing can try again.
- The circuit breaker opens after sustained failure, reducing load on the recovering database
- The health check transitions through Degraded → Unhealthy as inactivity duration increases

Restoring connectivity can allow the next firing to succeed. Missing CDC jobs, lost history, exhausted task attempts, or inconsistent downstream state still require operator reconciliation.

### After the Restore (Data Replaced)

A restored database may have different CDC LSN ranges than what the processor has checkpointed:

| Scenario | What Happens | Resolution |
|----------|-------------|------------|
| Checkpoint LSN is within the restored range | Bounds checks cannot distinguish overlapping replacement histories | Reconcile source identity and downstream state before resuming |
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

### Restoring a SQL Server source database

1. Pause triggers and drain active framework executions before loading a backup. Pausing triggers alone does not stop a running job. Record the source database, capture instances,
   checkpoint database, deduplication store, and projection or downstream databases involved.
2. After restoring, verify CDC enablement, capture instances, permissions, and SQL Server Agent jobs.
   `KEEP_CDC` preserves CDC settings when applicable; it does **not** recreate capture or cleanup jobs.
   Recreate missing jobs as part of the DBA restore procedure before resuming workers. See Microsoft's
   [CDC restore guidance](https://learn.microsoft.com/en-us/sql/relational-databases/track-changes/change-data-capture-and-other-sql-server-features).
3. Compare saved checkpoints with readable source bounds. The framework detects both checkpoints below
   retention and checkpoints above the current head. Automatic fallback must be configured deliberately.
   `FallbackToEarliest` replays available history; `FallbackToLatest` deliberately skips older available
   changes. `Throw` requires operator intervention. No strategy can recover changes absent from the backup
   and retained CDC history.
4. Reconcile downstream state and deduplication records together. A retained deduplication marker can
   suppress an effect lost when its destination database was restored. Conversely, clearing markers can
   repeat external effects. Do not clear all state automatically.
5. Resume workers and verify a new test change reaches its destination and advances its checkpoint.
   Watch reset notifications, capture lag, task attempts, and worker health.

Recovery is applied at a joined batch boundary. If bounds change while a batch is running, the processor
stops both workers before retrying with a fresh queue, durable state, and leadership. Recovery retries are
bounded by `MaxRecoveryAttempts` and delayed by `RecoveryAttemptDelay`. Callbacks run before replacement
positions are installed; a callback failure stops recovery. Zero or inconsistent bounds are an availability
error, not permission to erase history.

An LSN range is not a database-history identity. A replacement database can have overlapping valid LSNs,
which bounds checks cannot detect. Loading backups must remain a coordinated operation; rebuilding a
projection or deliberately establishing a new consumer identity can be necessary. Likewise, DataProcessing
can detect a missing task row, but cannot detect a restored row with the same identifier solely by existence.

### Quartz jobs

`CdcJob` is supplied by `Excalibur.Jobs.Cdc`; `DataProcessingJob` by `Excalibur.Jobs.DataProcessing`.
A failed firing reports `JobExecutionException` with `RefireImmediately = false` and records no success
heartbeat. The next configured cron firing remains eligible to run. This is separate from internal SQL
retry limits and DataProcessing task eligibility (`Attempts < MaxAttempts`). Successful independent tasks
may already have completed during a failed cycle. Cleanup-delete failures do not increment task attempts.
An existing heartbeat can remain healthy until it ages past the health threshold; monitor failed Quartz
executions as well. See [Quartz exception behavior](https://www.quartz-scheduler.net/documentation/best-practices.html#what-happens-when-a-job-throws).

For `CdcJob`, configure recovery on **each database** under `Jobs:CdcJob:DatabaseConfigs`.
Its processor factory receives that database's `RecoveryOptions`; configuring a separate
`AddCdcProcessor(...WithRecovery(...))` registration does not replace those per-job options.
For example, the database entry can contain:

```json
"RecoveryOptions": {
  "RecoveryStrategy": "FallbackToEarliest",
  "MaxRecoveryAttempts": 3,
  "RecoveryAttemptDelay": "00:00:01"
}
```

Choose this replay policy only when handlers and downstream reconciliation tolerate replay.
An omitted `RecoveryOptions` leaves stale-position failures for the operator. A recurring cron schedule
cannot recreate missing SQL Server Agent CDC jobs or reconstruct history absent from the backup.

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
