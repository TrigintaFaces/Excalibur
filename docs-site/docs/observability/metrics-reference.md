---
sidebar_position: 8
title: Metrics Reference
description: Complete catalog of OpenTelemetry metrics exposed by Excalibur.Dispatch
---

# Metrics Reference

Complete catalog of all OpenTelemetry metrics exposed by Excalibur.Dispatch and Excalibur framework components.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch.Observability
  dotnet add package OpenTelemetry.Extensions.Hosting
  ```
- Familiarity with [OpenTelemetry](https://opentelemetry.io/docs/languages/dotnet/) and [health checks](./health-checks.md)

## Quick Start

Enable metrics collection with OpenTelemetry:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        // Core Dispatch metrics
        metrics.AddMeter("Excalibur.Dispatch.*");

        // Data layer metrics
        metrics.AddMeter("Excalibur.Data.*");

        // Event sourcing metrics
        metrics.AddMeter("Excalibur.EventSourcing.*");
    });
```

## Naming Convention

All meters follow the pattern: `Excalibur.{Namespace}.{Component}`

| Prefix | Description |
|--------|-------------|
| `Excalibur.Dispatch.*` | Core messaging, transport, and pipeline metrics |
| `Excalibur.Data.*` | Data access and persistence metrics |
| `Excalibur.EventSourcing.*` | Event store and snapshot metrics |

---

## Core Metrics

### Excalibur.Dispatch.Core

Core message dispatching metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.messages.processed` | Counter | count | Total number of messages processed |
| `dispatch.messages.published` | Counter | count | Total number of messages published |
| `dispatch.messages.failed` | Counter | count | Total number of messages that failed |
| `dispatch.messages.duration` | Histogram | ms | Message processing duration |
| `dispatch.sessions.active` | Gauge | count | Number of active sessions |

**Tags:** `message_type`, `handler`, `result`

---

### Excalibur.Dispatch.CircuitBreaker

Circuit breaker state and operations.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.circuitbreaker.state_changes` | Counter | count | Circuit breaker state transitions |
| `dispatch.circuitbreaker.rejections` | Counter | count | Requests rejected due to open circuit |
| `dispatch.circuitbreaker.failures` | Counter | count | Failed requests tracked by circuit |
| `dispatch.circuitbreaker.successes` | Counter | count | Successful requests tracked by circuit |
| `dispatch.circuitbreaker.state` | ObservableGauge | state | Current circuit state (0=Closed, 1=Open, 2=HalfOpen) |

**Tags:** `circuit_name`, `state`

---

### Excalibur.Dispatch.DeadLetterQueue

Dead letter queue operations.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.dlq.enqueued` | Counter | count | Messages added to dead letter queue |
| `dispatch.dlq.replayed` | Counter | count | Messages replayed from dead letter queue |
| `dispatch.dlq.purged` | Counter | count | Messages purged from dead letter queue |
| `dispatch.dlq.depth` | ObservableGauge | messages | Current dead letter queue depth |

**Tags:** `reason`, `message_type`

---

### Excalibur.Dispatch.Sagas

Saga orchestration metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.saga.started_total` | Counter | sagas | Sagas initiated |
| `dispatch.saga.completed_total` | Counter | sagas | Sagas completed successfully |
| `dispatch.saga.failed_total` | Counter | sagas | Sagas that failed |
| `dispatch.saga.compensated_total` | Counter | sagas | Sagas that triggered compensation |
| `dispatch.saga.duration_ms` | Histogram | ms | Total saga execution duration |
| `dispatch.saga.handler_duration_ms` | Histogram | ms | Individual handler execution duration |
| `dispatch.saga.active` | ObservableGauge | sagas | Currently active sagas |

**Tags:** `saga_type`, `state`, `handler`

---

### Excalibur.Dispatch.BackgroundServices

Background processor metrics (outbox, inbox, CDC).

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `excalibur.background_service.processing_cycles` | Counter | cycles | Processing cycles executed |
| `excalibur.background_service.messages_processed` | Counter | messages | Messages processed |
| `excalibur.background_service.messages_failed` | Counter | messages | Messages that failed processing |
| `excalibur.background_service.processing_duration` | Histogram | ms | Processing cycle duration |
| `excalibur.background_service.processing_errors` | Counter | errors | Processing cycle errors |

**Tags:** `service_type` (outbox, inbox, cdc), `operation`

---

## Transport Metrics

### Excalibur.Dispatch.Transport

Common transport layer metrics (all transports).

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.transport.messages.sent` | Counter | count | Messages sent |
| `dispatch.transport.messages.send_failed` | Counter | count | Message send failures |
| `dispatch.transport.messages.received` | Counter | count | Messages received |
| `dispatch.transport.messages.acknowledged` | Counter | count | Messages acknowledged |
| `dispatch.transport.messages.rejected` | Counter | count | Messages rejected |
| `dispatch.transport.messages.dead_lettered` | Counter | count | Messages routed to dead letter queue |
| `dispatch.transport.messages.requeued` | Counter | count | Messages requeued for redelivery |
| `dispatch.transport.send.duration` | Histogram | ms | Send operation duration |
| `dispatch.transport.receive.duration` | Histogram | ms | Receive operation duration |
| `dispatch.transport.batch.size` | Histogram | count | Batch sizes |
| `dispatch.transport.handler.errors` | Counter | count | Handler errors during subscriber processing |
| `dispatch.transport.handler.duration` | Histogram | ms | Subscriber handler invocation duration |

**Tags:** `transport`, `endpoint`, `result`

---

### Excalibur.Dispatch.Transport.GooglePubSub

Google Cloud Pub/Sub specific metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `pubsub.messages.enqueued` | Counter | messages | Messages enqueued for processing |
| `pubsub.messages.dequeued` | Counter | messages | Messages dequeued |
| `pubsub.messages.processed` | Counter | messages | Messages successfully processed |
| `pubsub.messages.failed` | Counter | messages | Messages that failed processing |
| `pubsub.messages.published` | Counter | count | Messages published |
| `pubsub.messages.acknowledged` | Counter | count | Messages acknowledged |
| `pubsub.messages.nacked` | Counter | count | Messages negatively acknowledged |
| `pubsub.batches.created` | Counter | batches | Batches created |
| `pubsub.batches.completed` | Counter | batches | Batches completed |
| `pubsub.connections.created` | Counter | connections | Connections created |
| `pubsub.connections.closed` | Counter | connections | Connections closed |
| `pubsub.message.queue_time` | Histogram | ms | Time messages spend in queue |
| `pubsub.message.processing_time` | Histogram | ms | Message processing time |
| `pubsub.batch.size` | Histogram | messages | Batch sizes |
| `pubsub.batch.duration` | Histogram | ms | Batch processing duration |
| `pubsub.flow_control.permits` | ObservableGauge | permits | Available flow control permits |
| `pubsub.flow_control.bytes` | ObservableGauge | bytes | Available flow control bytes |

**Tags:** `subscription`, `topic`, `result`

---

### Azure Storage Queues

Azure Storage Queue metrics (part of Azure Service Bus transport).

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `azurequeue.messages.processed` | Counter | count | Messages processed |
| `azurequeue.processing.duration` | Histogram | ms | Processing duration |
| `azurequeue.batches.processed` | Counter | count | Batches processed |
| `azurequeue.batch.size` | Histogram | count | Batch sizes |
| `azurequeue.receive.operations` | Counter | count | Receive operations |
| `azurequeue.receive.duration` | Histogram | ms | Receive duration |
| `azurequeue.delete.operations` | Counter | count | Delete operations |
| `azurequeue.visibility.updates` | Counter | count | Visibility timeout updates |
| `azurequeue.queue.depth` | Gauge | count | Current queue depth |

**Tags:** `queue_name`, `result`

---

## Data Layer Metrics

### Excalibur.Data.Persistence

Generic data persistence metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `persistence.queries` | Counter | count | Queries executed |
| `persistence.commands` | Counter | count | Commands executed |
| `persistence.errors` | Counter | count | Persistence errors |
| `persistence.rows_affected` | Counter | count | Rows affected by operations |
| `persistence.cache.hits` | Counter | count | Cache hits |
| `persistence.cache.misses` | Counter | count | Cache misses |
| `persistence.query.duration` | Histogram | ms | Query execution duration |
| `persistence.transaction.duration` | Histogram | ms | Transaction duration |
| `persistence.connections.active` | ObservableGauge | count | Active connections |
| `persistence.connections.idle` | ObservableGauge | count | Idle connections |

**Tags:** `operation`, `entity`, `result`

---

### Excalibur.Data.SqlServer.Persistence

SQL Server specific persistence metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `sqlserver.connections.created` | Counter | count | Connections created |
| `sqlserver.queries.executed` | Counter | count | Queries executed |
| `sqlserver.commands.executed` | Counter | count | Commands executed |
| `sqlserver.transactions.started` | Counter | count | Transactions started |
| `sqlserver.transactions.committed` | Counter | count | Transactions committed |
| `sqlserver.transactions.rolledback` | Counter | count | Transactions rolled back |
| `sqlserver.retries` | Counter | count | Retry operations |
| `sqlserver.errors` | Counter | count | Error count |
| `sqlserver.deadlocks` | Counter | count | Deadlock count |
| `sqlserver.query.duration` | Histogram | ms | Query duration |
| `sqlserver.command.duration` | Histogram | ms | Command duration |
| `sqlserver.transaction.duration` | Histogram | ms | Transaction duration |
| `sqlserver.connection.wait` | Histogram | ms | Connection wait time |
| `sqlserver.batch.size` | Histogram | count | Batch sizes |
| `sqlserver.connections.active` | ObservableGauge | count | Active connections |
| `sqlserver.transactions.active` | ObservableGauge | count | Active transactions |
| `sqlserver.cdc.events.processed` | Counter | count | CDC events processed |
| `sqlserver.cdc.processing.duration` | Histogram | ms | CDC processing duration |
| `sqlserver.cdc.lag` | ObservableGauge | seconds | CDC lag |
| `sqlserver.cache.hits` | Counter | count | Cache hits |
| `sqlserver.cache.misses` | Counter | count | Cache misses |
| `sqlserver.cache.hit_ratio` | ObservableGauge | ratio | Cache hit ratio |

**Tags:** `database`, `operation`, `result`

---

### Excalibur.Data.Postgres.Persistence

PostgreSQL specific persistence metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `postgres.queries.total` | Counter | count | Total queries |
| `postgres.commands.total` | Counter | count | Total commands |
| `postgres.transactions.total` | Counter | count | Total transactions |
| `postgres.queries.failed` | Counter | count | Failed queries |
| `postgres.commands.failed` | Counter | count | Failed commands |
| `postgres.transactions.failed` | Counter | count | Failed transactions |
| `postgres.connection.errors` | Counter | count | Connection errors |
| `postgres.timeouts` | Counter | count | Timeout count |
| `postgres.deadlocks` | Counter | count | Deadlock count |
| `postgres.cache.hits` | Counter | count | Cache hits |
| `postgres.cache.misses` | Counter | count | Cache misses |
| `postgres.query.duration` | Histogram | ms | Query duration |
| `postgres.command.duration` | Histogram | ms | Command duration |
| `postgres.transaction.duration` | Histogram | ms | Transaction duration |
| `postgres.connection.acquisition` | Histogram | ms | Connection acquisition time |
| `postgres.connections.active` | ObservableGauge | count | Active connections |
| `postgres.connections.idle` | ObservableGauge | count | Idle connections |
| `postgres.pool.size` | ObservableGauge | count | Pool size |
| `postgres.pool.utilization` | ObservableGauge | ratio | Pool utilization |
| `postgres.prepared_statements` | ObservableGauge | count | Prepared statement count |

**Tags:** `database`, `operation`, `result`

---

### Excalibur.Data.Postgres.Outbox

PostgreSQL outbox store metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `postgres.outbox.save.duration` | Histogram | ms | Save messages duration |
| `postgres.outbox.reserve.duration` | Histogram | ms | Reserve messages duration |
| `postgres.outbox.unreserve.duration` | Histogram | ms | Unreserve messages duration |
| `postgres.outbox.delete.duration` | Histogram | ms | Delete record duration |
| `postgres.outbox.increase_attempts.duration` | Histogram | ms | Increase attempts duration |
| `postgres.outbox.move_to_dlq.duration` | Histogram | ms | Move to DLQ duration |
| `postgres.outbox.batch_delete.duration` | Histogram | ms | Batch delete duration |
| `postgres.outbox.batch_increase_attempts.duration` | Histogram | ms | Batch increase attempts duration |
| `postgres.outbox.batch_move_to_dlq.duration` | Histogram | ms | Batch move to DLQ duration |
| `postgres.outbox.messages.processed` | Counter | count | Messages processed |
| `postgres.outbox.operations.completed` | Counter | count | Operations completed |

**Tags:** `operation`, `result`

---

## Compliance Metrics

### Excalibur.Dispatch.Compliance

Security and compliance metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.compliance.key_rotations` | Counter | count | Key rotations performed |
| `dispatch.compliance.key_rotation_failures` | Counter | count | Key rotation failures |
| `dispatch.compliance.encryption_latency` | Histogram | ms | Encryption operation latency |
| `dispatch.compliance.encryption_operations` | Counter | count | Encryption operations |
| `dispatch.compliance.encryption_bytes_processed` | Counter | bytes | Bytes encrypted/decrypted |
| `dispatch.compliance.audit_events_logged` | Counter | count | Audit events logged |
| `dispatch.compliance.audit_integrity_checks` | Counter | count | Integrity checks performed |
| `dispatch.compliance.audit_integrity_violations` | Counter | count | Integrity violations detected |
| `dispatch.compliance.audit_integrity_check_duration` | Histogram | ms | Integrity check duration |
| `dispatch.compliance.key_usage_operations` | Counter | count | Key usage operations |

**Tags:** `key_id`, `algorithm`, `result`

---

## Caching Metrics

### Excalibur.Dispatch.Caching

Caching middleware metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.cache.hits` | Counter | count | Cache hits |
| `dispatch.cache.misses` | Counter | count | Cache misses |
| `dispatch.cache.timeouts` | Counter | count | Cache operation timeouts |
| `dispatch.cache.duration` | Histogram | ms | Cache operation latency |

**Tags:** `cache_name`, `operation`, `result`

---

## Context Flow Metrics

### Excalibur.Dispatch.Observability.Context

Message context flow and preservation metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.context.flow.snapshots` | Counter | count | Context snapshots taken |
| `dispatch.context.flow.mutations` | Counter | count | Context mutations |
| `dispatch.context.flow.errors` | Counter | count | Context errors |
| `dispatch.context.flow.validation_failures` | Counter | count | Validation failures |
| `dispatch.context.flow.cross_boundary_transitions` | Counter | count | Cross-boundary transitions |
| `dispatch.context.flow.preservation_success` | Counter | count | Successful context preservation |
| `dispatch.context.flow.field_loss` | Counter | count | Context field loss events |
| `dispatch.context.flow.size_threshold_exceeded` | Counter | count | Size threshold exceeded |
| `dispatch.context.flow.size_bytes` | Histogram | bytes | Context size distribution |
| `dispatch.context.flow.field_count` | Histogram | count | Field count distribution |
| `dispatch.context.flow.stage_latency_ms` | Histogram | ms | Pipeline stage latency |
| `dispatch.context.flow.serialization_latency_ms` | Histogram | ms | Serialization latency |
| `dispatch.context.flow.deserialization_latency_ms` | Histogram | ms | Deserialization latency |
| `dispatch.context.flow.active_contexts` | ObservableGauge | count | Active contexts |
| `dispatch.context.flow.preservation_rate` | ObservableGauge | ratio | Context preservation rate |
| `dispatch.context.flow.lineage_depth` | ObservableGauge | count | Lineage depth |

**Tags:** `stage`, `boundary_type`, `result`

---

## Prometheus Query Examples

### Message Throughput

```promql
# Messages processed per second
rate(dispatch_messages_processed_total[5m])

# Error rate
rate(dispatch_messages_failed_total[5m]) / rate(dispatch_messages_processed_total[5m])
```

### Latency Percentiles

```promql
# P99 message processing latency
histogram_quantile(0.99, sum(rate(dispatch_messages_duration_bucket[5m])) by (le))

# P50 (median) processing latency
histogram_quantile(0.50, sum(rate(dispatch_messages_duration_bucket[5m])) by (le))
```

### Circuit Breaker Health

```promql
# Circuit breaker state (0=Closed/Healthy, 1=Open, 2=HalfOpen)
dispatch_circuitbreaker_state

# Circuit breaker rejection rate
rate(dispatch_circuitbreaker_rejections_total[5m])
```

### Database Performance

```promql
# Average query duration
rate(sqlserver_query_duration_sum[5m]) / rate(sqlserver_query_duration_count[5m])

# Connection pool utilization
postgres_pool_utilization
```

---

## Grafana Dashboard Templates

Pre-built Grafana dashboards are available in the [grafana-dashboards](./grafana-dashboards.md) documentation.

### Key Dashboards

| Dashboard | Description |
|-----------|-------------|
| Dispatch Overview | Message throughput, latency, error rates |
| Transport Health | Per-transport metrics and connection status |
| Circuit Breakers | Circuit states across all breakers |
| Database Performance | Query latency, connection pools, deadlocks |
| Compliance Monitoring | Encryption operations, key rotation, audit trail |

---

## Related Documentation

- [Health Checks](./health-checks.md) - Application health monitoring
- [Grafana Dashboards](./grafana-dashboards.md) - Pre-built visualization
- [Azure Monitor](./azure-monitor.md) - Azure Application Insights integration
- [AWS CloudWatch](./aws-cloudwatch.md) - AWS monitoring integration

## See Also

- [Production Observability](./production-observability.md) — Operational best practices for monitoring Dispatch in production environments
- [Health Checks](./health-checks.md) — Application health monitoring with built-in and custom health check endpoints
- [Grafana Dashboards](./grafana-dashboards.md) — Pre-built Grafana dashboard templates for visualizing Dispatch metrics
