---
sidebar_position: 8
title: Metrics Reference
description: Complete catalog of OpenTelemetry metrics exposed by Excalibur.Dispatch
---

# Metrics Reference

Complete catalog of all OpenTelemetry metrics exposed by Excalibur framework components.

## Before You Start

- **.NET 10.0**
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
| `dispatch.messages.processed` | Counter | `{messages}` | Total number of messages processed |
| `dispatch.messages.published` | Counter | `{messages}` | Total number of messages published |
| `dispatch.messages.failed` | Counter | `{messages}` | Total number of messages that failed |
| `dispatch.messages.duration` | Histogram | `ms` | Message processing duration |
| `dispatch.sessions.active` | Gauge | `{sessions}` | Number of active sessions |

**Tags:** `message_type`, `handler`, `result`

---

### Excalibur.Dispatch.CircuitBreakerMiddleware

State-transition and rejection counters emitted directly by `CircuitBreakerMiddleware` whenever the
middleware is in the pipeline — **no opt-in observability service required**. Subscribe to this meter to
alert on breaker trips out of the box.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.circuit_breaker.transitions` | Counter | `{transitions}` | Circuit breaker state transitions (emitted only on an actual state change) |
| `dispatch.circuit_breaker.rejections` | Counter | `{rejections}` | Requests rejected because the circuit was open |

**Tags:**
- `dispatch.circuit_breaker.transitions` — `circuit.key`, `from_state`, `to_state` (state values: `closed`, `open`, `half_open`)
- `dispatch.circuit_breaker.rejections` — `circuit.key`

---

### Excalibur.Dispatch.PoisonMessage.Middleware

Dead-letter routing counter emitted directly by `PoisonMessageMiddleware` whenever a poison message is
moved to the dead-letter queue — **no opt-in observability service required**. Subscribe to this meter to
alert on dead-letter volume out of the box.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.poison.dead_lettered` | Counter | `{messages}` | Poison messages moved to the dead-letter queue |

**Tags:** `poison.detector`, `poison.reason`

---

### Excalibur.Dispatch.Sagas

Saga orchestration metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.saga.started_total` | Counter | `{sagas}` | Sagas initiated |
| `dispatch.saga.completed_total` | Counter | `{sagas}` | Sagas completed successfully |
| `dispatch.saga.failed_total` | Counter | `{sagas}` | Sagas that failed |
| `dispatch.saga.compensated_total` | Counter | `{sagas}` | Sagas that triggered compensation |
| `dispatch.saga.duration` | Histogram | `ms` | Total saga execution duration |
| `dispatch.saga.handler_duration` | Histogram | `ms` | Individual handler execution duration |
| `dispatch.saga.active` | ObservableGauge | `{sagas}` | Currently active sagas |

**Tags:** `saga_type`, `state`, `handler`

---

### Excalibur.Dispatch.BackgroundServices

Background processor metrics (outbox, inbox, CDC).

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `excalibur.background_service.processing_cycles` | Counter | `{cycles}` | Processing cycles executed |
| `excalibur.background_service.messages_processed` | Counter | `{messages}` | Messages processed |
| `excalibur.background_service.messages_failed` | Counter | `{messages}` | Messages that failed processing |
| `excalibur.background_service.processing_duration` | Histogram | `ms` | Processing cycle duration |
| `excalibur.background_service.processing_errors` | Counter | `{errors}` | Processing cycle errors |

**Tags:** `service_type` (outbox, inbox, cdc), `operation`

---

## Transport Metrics

### Excalibur.Dispatch.Transport

Common transport layer metrics (all transports).

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.transport.messages.sent` | Counter | `{messages}` | Messages sent |
| `dispatch.transport.messages.send_failed` | Counter | `{messages}` | Message send failures |
| `dispatch.transport.messages.received` | Counter | `{messages}` | Messages received |
| `dispatch.transport.messages.acknowledged` | Counter | `{messages}` | Messages acknowledged |
| `dispatch.transport.messages.rejected` | Counter | `{messages}` | Messages rejected |
| `dispatch.transport.messages.dead_lettered` | Counter | `{messages}` | Messages routed to dead letter queue |
| `dispatch.transport.messages.requeued` | Counter | `{messages}` | Messages requeued for redelivery |
| `dispatch.transport.send.duration` | Histogram | `ms` | Send operation duration |
| `dispatch.transport.receive.duration` | Histogram | `ms` | Receive operation duration |
| `dispatch.transport.batch.size` | Histogram | `{messages}` | Batch sizes |
| `dispatch.transport.handler.errors` | Counter | `{errors}` | Handler errors during subscriber processing |
| `dispatch.transport.handler.duration` | Histogram | `ms` | Subscriber handler invocation duration |

**Tags:** `dispatch.transport.name`, `dispatch.transport.destination`, and `error.type` on the
failure counters. (Prometheus renders these as `dispatch_transport_name`,
`dispatch_transport_destination`, and `error_type`.)

---

### Excalibur.Dispatch.Transport.GooglePubSub

Google Cloud Pub/Sub metrics. All instruments below share the meter
`Excalibur.Dispatch.Transport.GooglePubSub`.

**Streaming pull** — emitted by the streaming-pull subscriber:

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.streaming_pull.messages.received` | Counter | `{messages}` | Messages received via streaming pull |
| `dispatch.streaming_pull.acks.sent` | Counter | `{acks}` | Acknowledgments sent |
| `dispatch.streaming_pull.nacks.sent` | Counter | `{nacks}` | Negative acknowledgments sent |
| `dispatch.streaming_pull.streams.opened` | Counter | `{streams}` | Streams opened |
| `dispatch.streaming_pull.streams.closed` | Counter | `{streams}` | Streams closed |
| `dispatch.streaming_pull.streams.reconnections` | Counter | `{reconnections}` | Stream reconnection attempts |
| `dispatch.streaming_pull.message.processing_duration` | Histogram | `ms` | Message processing duration |
| `dispatch.streaming_pull.ack.latency` | Histogram | `ms` | Acknowledgment latency |

**Batch receiving** — emitted when batch receiving is enabled:

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `pubsub.batch.messages.received` | Counter | `{messages}` | Messages received in batches |
| `pubsub.batch.messages.acknowledged` | Counter | `{messages}` | Messages acknowledged |
| `pubsub.batch.bytes.received` | Counter | `By` | Bytes received in batches |
| `pubsub.batch.count` | Counter | `{batches}` | Batches received |
| `pubsub.batch.size` | Histogram | `{messages}` | Size of received batches |
| `pubsub.batch.receive.duration` | Histogram | `ms` | Batch receive duration |
| `pubsub.batch.ack.duration` | Histogram | `ms` | Batch acknowledgment duration |
| `pubsub.batch.processors.active` | UpDownCounter | `{processors}` | Active batch processors |

:::caution Connection, flow-control, and queue-time instruments are not emitted
A broader Pub/Sub meter covering enqueue/dequeue counts, connection open/close, flow-control
permits, and queue time is defined in the package, but no subscriber path records to it. Only the
instruments listed above produce data points.
:::

---

### Azure Storage Queues

Azure Storage Queue support ships inside the `Excalibur.Dispatch.Transport.AzureServiceBus`
package.

:::caution No queue-specific metrics are emitted
The Storage Queue transport publishes no meter of its own, and there is no Storage Queue meter to
subscribe to.

Its registration also does not apply the common transport telemetry decorators, so the
[`Excalibur.Dispatch.Transport`](#excaliburdispatchtransport) instruments are not emitted for this
transport either. For Storage Queue throughput, instrument your queue handlers directly, or use the
queue metrics Azure Monitor publishes from the Storage account itself.
:::

---

## Data Layer Metrics

### Excalibur.Data.Persistence

:::caution No provider-agnostic persistence metrics are emitted
There is no provider-agnostic persistence meter. Nothing publishes an `Excalibur.Data.Persistence`
meter or activity source, so there is nothing to subscribe to at that level.

Use the provider-specific meters below instead. They are registered by the provider's own
`Add…` extension and are emitted in normal operation:

- [`Excalibur.Data.SqlServer.Persistence`](#excaliburdatasqlserverpersistence)
- [`Excalibur.Data.Postgres.Persistence`](#excaliburdatapostgrespersistence)

For connection-pool utilization, scrape the counters published by the underlying ADO.NET
provider (`Microsoft.Data.SqlClient` or `Npgsql`) rather than this framework.
:::

---

### Excalibur.Data.SqlServer.Persistence

SQL Server specific persistence metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `sqlserver.connections.created` | Counter | `{connections}` | Connections created |
| `sqlserver.queries.executed` | Counter | `{queries}` | Queries executed |
| `sqlserver.commands.executed` | Counter | `{commands}` | Commands executed |
| `sqlserver.transactions.started` | Counter | `{transactions}` | Transactions started |
| `sqlserver.transactions.committed` | Counter | `{transactions}` | Transactions committed |
| `sqlserver.transactions.rolledback` | Counter | `{transactions}` | Transactions rolled back |
| `sqlserver.retries` | Counter | `{retries}` | Retry operations |
| `sqlserver.errors` | Counter | `{errors}` | Error count |
| `sqlserver.deadlocks` | Counter | `{deadlocks}` | Deadlock count |
| `sqlserver.query.duration` | Histogram | `ms` | Query duration |
| `sqlserver.command.duration` | Histogram | `ms` | Command duration |
| `sqlserver.transaction.duration` | Histogram | `ms` | Transaction duration |
| `sqlserver.connection.wait_time` | Histogram | `ms` | Connection wait time |
| `sqlserver.batch.size` | Histogram | `{commands}` | Batch sizes |
| `sqlserver.connections.active` | ObservableGauge | `{connections}` | Active connections |
| `sqlserver.transactions.active` | ObservableGauge | `{transactions}` | Active transactions |
| `sqlserver.cdc.events.processed` | Counter | `{events}` | CDC events processed |
| `sqlserver.cdc.processing.duration` | Histogram | `ms` | CDC processing duration |
| `sqlserver.cdc.lag` | ObservableGauge | `s` | CDC lag |
| `sqlserver.cache.hits` | Counter | `{hits}` | Cache hits |
| `sqlserver.cache.misses` | Counter | `{misses}` | Cache misses |
| `sqlserver.cache.hit_ratio` | ObservableGauge | `1` | Cache hit ratio |

**Tags:** `database`, `operation`, `result`

---

### Excalibur.Data.Postgres.Persistence

PostgreSQL specific persistence metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `postgres.queries.total` | Counter | `{queries}` | Total queries |
| `postgres.commands.total` | Counter | `{commands}` | Total commands |
| `postgres.transactions.total` | Counter | `{transactions}` | Total transactions |
| `postgres.queries.failed` | Counter | `{queries}` | Failed queries |
| `postgres.commands.failed` | Counter | `{commands}` | Failed commands |
| `postgres.transactions.failed` | Counter | `{transactions}` | Failed transactions |
| `postgres.connections.errors` | Counter | `{errors}` | Connection errors |
| `postgres.timeouts.total` | Counter | `{timeouts}` | Timeout count |
| `postgres.deadlocks.total` | Counter | `{deadlocks}` | Deadlock count |
| `postgres.cache.hits` | Counter | `{hits}` | Cache hits |
| `postgres.cache.misses` | Counter | `{misses}` | Cache misses |
| `postgres.query.duration` | Histogram | `ms` | Query duration |
| `postgres.command.duration` | Histogram | `ms` | Command duration |
| `postgres.transaction.duration` | Histogram | `ms` | Transaction duration |
| `postgres.connection.acquisition.time` | Histogram | `ms` | Connection acquisition time |
| `postgres.prepared.statements` | ObservableGauge | `{statements}` | Prepared statement count |

**Tags:** `database`, `operation`, `result`

---

### Excalibur.Outbox.Postgres

> **Meter name:** `Excalibur.Outbox.Postgres`. Add it explicitly, or with the
> `Excalibur.Outbox.*` wildcard — the `Excalibur.Data.*` wildcard in the Quick Start does **not**
> match this meter.

PostgreSQL outbox store metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `excalibur.outbox.save_messages_duration` | Histogram | `ms` | Time taken to save outbox messages |
| `excalibur.outbox.reserve_messages_duration` | Histogram | `ms` | Time taken to reserve outbox messages |
| `excalibur.outbox.unreserve_messages_duration` | Histogram | `ms` | Time taken to unreserve outbox messages |
| `excalibur.outbox.delete_record_duration` | Histogram | `ms` | Time taken to delete an outbox record |
| `excalibur.outbox.increase_attempts_duration` | Histogram | `ms` | Time taken to increase message attempts |
| `excalibur.outbox.move_to_dead_letter_duration` | Histogram | `ms` | Time taken to move a message to dead letter |
| `excalibur.outbox.batch_delete_duration` | Histogram | `ms` | Time taken to delete multiple outbox records |
| `excalibur.outbox.batch_increase_attempts_duration` | Histogram | `ms` | Time taken to increase attempts for multiple messages |
| `excalibur.outbox.batch_move_to_dead_letter_duration` | Histogram | `ms` | Time taken to move multiple messages to dead letter |
| `excalibur.outbox.messages_processed_total` | Counter | `{messages}` | Total number of outbox messages processed |
| `excalibur.outbox.operations_completed_total` | Counter | `{operations}` | Total number of outbox operations completed |

**Tags:** `operation`, `result`

---

## Compliance Metrics

### Excalibur.Compliance

Security and compliance metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.compliance.key_rotations` | Counter | `{rotations}` | Key rotations performed |
| `dispatch.compliance.key_rotation_failures` | Counter | `{failures}` | Key rotation failures |
| `dispatch.compliance.encryption_latency` | Histogram | `ms` | Encryption operation latency |
| `dispatch.compliance.encryption_operations` | Counter | `{operations}` | Encryption operations |
| `dispatch.compliance.encryption_bytes_processed` | Counter | `By` | Bytes encrypted/decrypted |
| `dispatch.compliance.audit_events_logged` | Counter | `{events}` | Audit events logged |
| `dispatch.compliance.audit_integrity_checks` | Counter | `{checks}` | Integrity checks performed |
| `dispatch.compliance.audit_integrity_violations` | Counter | `{violations}` | Integrity violations detected |
| `dispatch.compliance.audit_integrity_check_duration` | Histogram | `ms` | Integrity check duration |
| `dispatch.compliance.key_usage_operations` | Counter | `{operations}` | Key usage operations |

**Tags:** `key_id`, `algorithm`, `result`

---

## Caching Metrics

### Excalibur.Dispatch.Caching

Caching middleware metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.cache.hits` | Counter | - | Cache hits |
| `dispatch.cache.misses` | Counter | - | Cache misses |
| `dispatch.cache.timeouts` | Counter | - | Cache operation timeouts |
| `dispatch.cache.duration` | Histogram | `ms` | Cache operation latency |

**Tags:** `cache_name`, `operation`, `result`

---

## Context Flow Metrics

### Excalibur.Dispatch.Observability.Context

Message context flow and preservation metrics.

| Metric | Type | Unit | Description |
|--------|------|------|-------------|
| `dispatch.context.flow.snapshots` | Counter | `{snapshots}` | Context snapshots taken |
| `dispatch.context.flow.mutations` | Counter | `{mutations}` | Context mutations |
| `dispatch.context.flow.errors` | Counter | `{errors}` | Context errors |
| `dispatch.context.flow.validation_failures` | Counter | `{failures}` | Validation failures |
| `dispatch.context.flow.cross_boundary_transitions` | Counter | `{transitions}` | Cross-boundary transitions |
| `dispatch.context.flow.preservation_success` | Counter | `{successes}` | Successful context preservation |
| `dispatch.context.flow.field_loss` | Counter | `{losses}` | Context field loss events |
| `dispatch.context.flow.size_threshold_exceeded` | Counter | `{events}` | Size threshold exceeded |
| `dispatch.context.flow.size_bytes` | Histogram | `By` | Context size distribution |
| `dispatch.context.flow.field_count` | Histogram | `{fields}` | Field count distribution |
| `dispatch.context.flow.stage_latency` | Histogram | `ms` | Pipeline stage latency |
| `dispatch.context.flow.serialization_latency` | Histogram | `ms` | Serialization latency |
| `dispatch.context.flow.deserialization_latency` | Histogram | `ms` | Deserialization latency |
| `dispatch.context.flow.active_contexts` | ObservableGauge | `{contexts}` | Active contexts |
| `dispatch.context.flow.preservation_rate` | ObservableGauge | `1` | Context preservation rate |
| `dispatch.context.flow.lineage_depth` | ObservableGauge | `{depth}` | Lineage depth |

**Tags:** `stage`, `boundary_type`, `result`

---

## Prometheus Query Examples

### Deriving the Prometheus series name

The tables above list **OpenTelemetry instrument names**. The Prometheus exporter renames every
instrument on the way out, so the series you query is never the instrument name verbatim. With
`OpenTelemetry.Exporter.Prometheus.AspNetCore`, the rules are applied in this order:

1. **Sanitize** — every character outside `[A-Za-z0-9:]` becomes `_`, and runs of `_` collapse to one.
   So `dispatch.circuit_breaker.rejections` becomes `dispatch_circuit_breaker_rejections`.
2. **Append the unit** — the instrument's unit is appended as a suffix unless the name already ends
   with it. UCUM abbreviations are expanded (`ms` becomes `milliseconds`, `By` becomes `bytes`,
   `s` becomes `seconds`); the dimensionless unit `1` is dropped; and a UCUM **annotation** — a unit
   written in braces, such as `{messages}` — is stripped entirely, so a count of things keeps its
   meaning in the Unit column without adding anything to the series name.
3. **Append `_total`** to counters whose name does not already end in `_total`.
4. **Histograms** additionally expose `_bucket`, `_sum`, and `_count` series.

Label keys are sanitized the same way, so the `circuit.key` tag is queried as `circuit_key`.

Every instrument in the tables above declares a UCUM unit, so step 2 never appends a stray word:
`dispatch.messages.processed` carries the annotation `{messages}` and exports as
`dispatch_messages_processed_total`, and a duration carrying `ms` exports with a single
`_milliseconds` suffix. Histograms whose unit is a duration are therefore the only series that gain
a unit word.

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
histogram_quantile(0.99, sum(rate(dispatch_messages_duration_milliseconds_bucket[5m])) by (le))

# P50 (median) processing latency
histogram_quantile(0.50, sum(rate(dispatch_messages_duration_milliseconds_bucket[5m])) by (le))
```

### Circuit Breaker Health

There is no circuit-breaker *state* gauge — the middleware emits state **transitions**, so track
trips and rejections rather than reading a current state:

```promql
# Trips into the open state, per second, by circuit
sum by (circuit_key) (rate(dispatch_circuit_breaker_transitions_total{to_state="open"}[5m]))

# Requests rejected because the circuit was open
sum by (circuit_key) (rate(dispatch_circuit_breaker_rejections_total[5m]))
```

### Database Performance

```promql
# Average query duration (milliseconds)
rate(sqlserver_query_duration_milliseconds_sum[5m])
  / rate(sqlserver_query_duration_milliseconds_count[5m])

# Deadlocks per second
rate(postgres_deadlocks_total[5m])
```

:::note Connection pools
There is no connection-pool utilization instrument in this framework. `Npgsql` and
`Microsoft.Data.SqlClient` publish their own pool counters — scrape those.
:::

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
