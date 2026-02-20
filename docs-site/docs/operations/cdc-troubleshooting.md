---
sidebar_position: 2
title: CDC Troubleshooting
description: Troubleshoot Change Data Capture stale positions and recovery
---

# CDC Troubleshooting

Change Data Capture (CDC) issues can cause projection lag, missed events, and data inconsistency. This guide covers common problems and recovery procedures.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
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
    cdc.UseSqlServer(connectionString)
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

When CDC recovery requires projection rebuild:

```csharp
public class ProjectionRebuildService
{
    public async Task RebuildAsync(
        string projectionName,
        long fromSequence,
        CancellationToken ct)
    {
        _logger.LogWarning(
            "Rebuilding projection {Name} from sequence {Sequence}",
            projectionName, fromSequence);

        // 1. Clear existing projection data
        await _projectionStore.ClearAsync(projectionName, ct);

        // 2. Replay events from the event store
        var events = await _eventStore.LoadAsync(
            aggregateId: "*",
            aggregateType: projectionName,
            fromVersion: fromSequence,
            ct);

        foreach (var @event in events)
        {
            var projector = _projectorFactory.GetProjector(projectionName);
            await projector.ApplyAsync(@event, ct);
        }

        // 3. Update rebuild metadata
        await _projectionStore.SetLastRebuiltAsync(
            projectionName,
            DateTime.UtcNow,
            ct);

        _logger.LogInformation(
            "Projection {Name} rebuild complete",
            projectionName);
    }
}
```

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

## Prevention Best Practices

| Practice | Benefit |
|----------|---------|
| Enable automatic recovery | Reduces manual intervention |
| Set adequate log retention | Prevents truncation issues |
| Monitor CDC lag | Early warning of problems |
| Regular position validation | Detect issues before impact |
| Checkpoint frequently | Faster recovery |
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
