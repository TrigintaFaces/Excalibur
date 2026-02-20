---
sidebar_position: 3
title: Recovery Runbooks
description: Step-by-step recovery procedures for common operational failure scenarios
---

# Recovery Runbooks

This guide provides step-by-step procedures for recovering from common operational failures in Dispatch and Excalibur applications.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Access to your production or staging SQL Server instance
- Familiarity with [performance tuning](./performance-tuning.md) and [health checks](../observability/health-checks.md)

## SQL Server Recovery Scenarios

### Session Killed During CDC Processing (Error 596)

**Symptoms:**
- CDC processor stops processing events
- Log shows error 596: "Cannot continue the execution because the session is in the kill state"
- Projection updates stop

**Diagnosis:**
```sql
-- Check for active CDC sessions
SELECT session_id, status, start_time
FROM sys.dm_cdc_sessions
WHERE end_time IS NULL;

-- Check CDC capture position
SELECT * FROM cdc.lsn_time_mapping
ORDER BY tran_begin_time DESC;
```

**Recovery Steps:**
1. The retry policy automatically handles error 596
2. If processor doesn't recover within retry attempts:
   ```bash
   # Restart the application
   systemctl restart myapp
   ```
3. If position is invalid after restart:
   ```csharp
   // Configure recovery options
   options.Recovery = new CdcRecoveryOptions
   {
       RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest
   };
   ```

**Prevention:**
- Configure CDC recovery options in application startup
- Monitor CDC processor health via metrics
- Alert on repeated retry attempts

---

### Database Backup/Restore Invalidates LSN Position

**Symptoms:**
- CDC processor fails with "Invalid LSN" error
- Event store cannot find expected position
- Projection processor stuck

**Diagnosis:**
```sql
-- Check current CDC min LSN
SELECT name, min_lsn, max_lsn
FROM cdc.change_tables;

-- Compare with saved position
SELECT * FROM [dbo].[CdcState]
WHERE ProcessorName = 'YourProcessor';
```

**Recovery Steps:**
1. **Automatic (Recommended):** Configure recovery options:
   ```csharp
   options.Recovery = new CdcRecoveryOptions
   {
       RecoveryStrategy = StalePositionRecoveryStrategy.FallbackToEarliest
   };
   ```

2. **Manual Reset:** If automatic recovery fails:
   ```sql
   -- Reset CDC state to earliest available position
   UPDATE [dbo].[CdcState]
   SET Position = (SELECT MIN(min_lsn) FROM cdc.change_tables)
   WHERE ProcessorName = 'YourProcessor';
   ```

3. Restart the processor and verify events are processing

**Prevention:**
- Use `FallbackToEarliest` strategy for data consistency
- Monitor position age vs CDC retention
- Schedule backups during low-activity periods

---

### Connection Pool Corruption

**Symptoms:**
- Intermittent connection failures
- "Connection is broken" errors
- Some operations succeed, others fail

**Diagnosis:**
```sql
-- Check for orphaned connections
SELECT session_id, login_name, status, last_request_end_time
FROM sys.dm_exec_sessions
WHERE program_name LIKE '%YourApp%'
ORDER BY last_request_end_time;
```

**Recovery Steps:**
1. Clear all connection pools:
   ```csharp
   SqlConnection.ClearAllPools();
   ```

2. If in Kubernetes, rolling restart:
   ```bash
   kubectl rollout restart deployment/myapp
   ```

3. Monitor for recurring issues

**Prevention:**
- Configure connection lifetime limits
- Implement health checks that test connections
- Use Azure SQL maintenance windows

---

## PostgreSQL Recovery Scenarios

### Broken Pipe / Connection Lost (08xxx Errors)

**Symptoms:**
- "Connection broken" or "57P01 admin_shutdown" errors
- Operations fail mid-transaction
- CDC processor stops

**Diagnosis:**
```sql
-- Check active connections
SELECT pid, usename, application_name, state, query_start
FROM pg_stat_activity
WHERE application_name LIKE '%YourApp%';

-- Check for terminated backends
SELECT * FROM pg_stat_activity WHERE state = 'idle in transaction';
```

**Recovery Steps:**
1. Retry policy handles transient errors automatically
2. If persistent, clear connection pool:
   ```csharp
   NpgsqlConnection.ClearAllPools();
   ```

3. Check PostgreSQL logs for root cause:
   ```bash
   tail -100 /var/log/postgresql/postgresql-15-main.log
   ```

**Prevention:**
- Configure `tcp_keepalives_idle` in connection string
- Monitor `pg_stat_activity` for long-running transactions
- Use connection pool health checks

---

### Deadlock Detection (40P01)

**Symptoms:**
- Operations fail with "deadlock detected" error
- Concurrent writes to same aggregates

**Diagnosis:**
```sql
-- Find blocking queries
SELECT blocked_locks.pid AS blocked_pid,
       blocking_locks.pid AS blocking_pid,
       blocked_activity.query AS blocked_statement
FROM pg_catalog.pg_locks blocked_locks
JOIN pg_catalog.pg_stat_activity blocked_activity ON blocked_activity.pid = blocked_locks.pid
JOIN pg_catalog.pg_locks blocking_locks ON blocking_locks.locktype = blocked_locks.locktype;
```

**Recovery Steps:**
1. Automatic retry handles deadlocks
2. If frequent, review aggregate design:
   - Reduce aggregate scope
   - Implement optimistic concurrency
   - Use advisory locks for coordination

**Prevention:**
- Design aggregates to minimize contention
- Use consistent lock ordering
- Monitor deadlock frequency via metrics

---

## Cloud Provider Recovery Scenarios

### CosmosDB Rate Limiting (429)

**Symptoms:**
- Operations fail with 429 "Request rate too large"
- Throughput drops dramatically
- SDK retries exhausted

**Recovery Steps:**
1. SDK handles 429 automatically with backoff
2. If persistent, increase RU/s:
   ```bash
   az cosmosdb sql container throughput update \
     --account-name myaccount \
     --database-name mydb \
     --name mycontainer \
     --throughput 10000
   ```

3. Enable autoscale:
   ```bash
   az cosmosdb sql container throughput migrate \
     --account-name myaccount \
     --database-name mydb \
     --name mycontainer \
     --resource-group mygroup \
     --throughput-type autoscale
   ```

**Prevention:**
- Enable autoscale for variable workloads
- Monitor RU consumption via Azure Monitor
- Implement bulk operations for high-throughput scenarios

---

### DynamoDB Throttling

**Symptoms:**
- Operations fail with ProvisionedThroughputExceededException
- Latency spikes

**Recovery Steps:**
1. SDK handles throttling automatically
2. If persistent, increase capacity:
   ```bash
   aws dynamodb update-table \
     --table-name MyTable \
     --provisioned-throughput ReadCapacityUnits=100,WriteCapacityUnits=100
   ```

**Prevention:**
- Enable on-demand capacity for unpredictable workloads
- Monitor consumed capacity via CloudWatch
- Implement exponential backoff in application code

---

## General Recovery Procedures

### Event Store Recovery

**When to use:** Event store corruption or position drift

1. **Verify event store integrity:**
   ```sql
   -- SQL Server
   SELECT StreamId, COUNT(*) as EventCount, MAX(Version) as MaxVersion
   FROM [dbo].[Events]
   GROUP BY StreamId
   HAVING COUNT(*) != MAX(Version) + 1;
   ```

2. **Rebuild projections if needed:**
   ```csharp
   await projectionRebuilder.RebuildAsync<MyProjection>(cancellationToken);
   ```

3. **Verify projection state:**
   ```sql
   SELECT * FROM [dbo].[ProjectionCheckpoints]
   WHERE ProjectionName = 'MyProjection';
   ```

### Outbox Recovery

**When to use:** Messages stuck in outbox, duplicate delivery suspected

1. **Check outbox status:**
   ```sql
   SELECT Status, COUNT(*) as Count
   FROM [dbo].[OutboxMessages]
   GROUP BY Status;
   ```

2. **Reprocess stuck messages:**
   ```sql
   UPDATE [dbo].[OutboxMessages]
   SET Status = 'Pending', RetryCount = 0
   WHERE Status = 'Failed' AND CreatedAt > DATEADD(hour, -24, GETUTCDATE());
   ```

3. **Monitor for successful delivery**

---

## Monitoring and Alerting

### Key Metrics to Monitor

| Metric | Warning Threshold | Critical Threshold |
|--------|-------------------|-------------------|
| Retry rate | > 5% of operations | > 20% of operations |
| CDC lag | > 1 minute | > 5 minutes |
| Connection errors | > 1/minute | > 10/minute |
| Deadlock rate | > 1/hour | > 10/hour |

### Recommended Alerts

```yaml
# Example Prometheus alerting rules
groups:
  - name: excalibur-resilience
    rules:
      - alert: HighRetryRate
        expr: rate(dispatch_write_store_retry_count_total[5m]) > 0.05
        for: 5m
        labels:
          severity: warning

      - alert: CdcLagHigh
        expr: dispatch_cdc_lag_seconds > 300
        for: 5m
        labels:
          severity: critical
```

## Related Documentation

- [Operational Resilience](resilience.md) - Retry policies and configuration
- [Observability](../observability/index.md) - Monitoring setup
- [Health Checks](../observability/health-checks.md) - Application health monitoring

## See Also

- [CDC Troubleshooting](cdc-troubleshooting.md) — Diagnose and recover from Change Data Capture issues
- [Performance Tuning](performance-tuning.md) — Optimize event store, outbox, and projection throughput
- [Health Checks](../observability/health-checks.md) — Application health monitoring and diagnostics
- [Dead Letter Pattern](../patterns/dead-letter.md) — Handling failed messages with dead letter queues
