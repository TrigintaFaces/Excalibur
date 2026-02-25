---
sidebar_position: 2
title: Production Observability Guide
description: What to monitor, what to alert on, and how to instrument your Dispatch application for production
---

# Production Observability Guide

Knowing that your system _can_ emit metrics is different from knowing _which_ metrics matter. Excalibur exposes over 100 OpenTelemetry metrics, but in practice you need to watch about a dozen signals to know whether your system is healthy.

This guide explains what to monitor, what to alert on, and how to set up dashboards that tell you something useful.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Dispatch.Observability
  dotnet add package OpenTelemetry.Extensions.Hosting
  ```
- Familiarity with [OpenTelemetry](https://opentelemetry.io/) and [metrics reference](./metrics-reference.md)

## Enabling Observability

Before anything else, enable tracing and metrics:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseOpenTelemetry(); // Enables both tracing and metrics
});

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Excalibur.Dispatch.*");
        // Add your exporter (Jaeger, Zipkin, OTLP, etc.)
        tracing.AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Excalibur.Dispatch.*");
        metrics.AddMeter("Excalibur.Data.*");
        metrics.AddMeter("Excalibur.EventSourcing.*");
        // Add your exporter (Prometheus, OTLP, etc.)
        metrics.AddPrometheusExporter();
    });
```

## The Five Signals That Matter

Out of 100+ available metrics, these are the ones that tell you whether your system is working.

### 1. Message Processing Latency

**Metric:** `dispatch.messages.duration` (histogram, milliseconds)

This is the single most important metric. If processing latency increases, something is degrading -- slow database, overloaded handler, or resource contention.

**What to watch:**
- **p50** (median): Your typical processing time
- **p99**: Your worst-case processing time
- **p99 / p50 ratio**: If this ratio suddenly increases, you have a tail latency problem

**Alert when:** p99 exceeds 2x your normal baseline for 5+ minutes.

### 2. Message Failure Rate

**Metric:** `dispatch.messages.failed` (counter) vs `dispatch.messages.processed` (counter)

Calculate the failure rate: `failed / (processed + failed) * 100`. A healthy system should have a failure rate under 1%. Spikes indicate handler bugs, external service outages, or bad data.

**Alert when:** Failure rate exceeds 5% over a 5-minute window.

### 3. Dead Letter Queue Depth

**Metric:** `dispatch.dlq.depth` (gauge)

A growing DLQ means messages are failing faster than they are being reviewed and replayed. A flat, non-zero DLQ is normal (pending review). A continuously growing DLQ is an incident.

**Alert when:** DLQ depth increases by more than 50 entries in 15 minutes.

### 4. Circuit Breaker State

**Metric:** `dispatch.circuitbreaker.state` (gauge: 0=Closed, 1=Open, 2=HalfOpen)

An open circuit breaker means a downstream dependency is unhealthy and messages to that transport are being rejected. This is the circuit breaker doing its job, but you need to know about it.

**Alert when:** Any circuit breaker enters `Open` state.

### 5. Outbox Lag

**Metric:** `dispatch.transport.pending_messages` (gauge)

If you use the outbox pattern, this tells you how many messages are waiting to be published. A small, stable number is normal (outbox processor is keeping up). A growing number means the processor is falling behind.

**Alert when:** Pending count exceeds 1,000 for more than 10 minutes.

## What Traces Look Like

When `UseOpenTelemetry()` is enabled, each message creates a span that flows through the middleware pipeline:

```
[Excalibur.Dispatch.Pipeline] ProcessMessage OrderCreatedEvent
  ├── [Excalibur.Dispatch.Middleware] ValidationMiddleware (0.2ms)
  ├── [Excalibur.Dispatch.Middleware] AuthorizationMiddleware (0.1ms)
  ├── [Excalibur.Dispatch.Middleware] IdempotentHandlerMiddleware (1.5ms)
  │     └── [Excalibur.Dispatch.Inbox] CheckProcessed (1.2ms)
  ├── [Excalibur.Dispatch.Handler] ProcessOrderHandler (45ms)
  │     ├── [Database] INSERT Orders (12ms)
  │     └── [HTTP] POST /api/payments (30ms)
  └── [Excalibur.Dispatch.Middleware] MetricsMiddleware (0.1ms)

Total: 47ms
```

Each span carries tags that you can filter and group by:

| Tag | Example | Use For |
|-----|---------|---------|
| `message_type` | `OrderCreatedEvent` | Filter traces by message type |
| `handler` | `ProcessOrderHandler` | Identify slow handlers |
| `result` | `Success` / `Failure` | Filter for failures |
| `transport` | `kafka` / `rabbitmq` | Filter by transport |
| `dispatch.message_id` | `abc-123` | Trace a specific message |

## Health Check Setup

Health checks provide a quick binary signal for load balancers and orchestrators.

### Recommended Configuration

```csharp
builder.Services.AddHealthChecks()
    // Pipeline health
    .AddCheck("self", () => HealthCheckResult.Healthy())
    // Transport connectivity
    .AddTransportHealthChecks()
    // Dead letter queue depth
    .AddCheck<DeadLetterHealthCheck>("dlq");

var app = builder.Build();

// Kubernetes-style endpoints
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    // Liveness: is the process running?
    Predicate = check => check.Name == "self"
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    // Readiness: can we process messages?
    Predicate = _ => true
});
```

### Custom Dead Letter Health Check

```csharp
public class DeadLetterHealthCheck : IHealthCheck
{
    private readonly IDeadLetterQueue _dlq;

    public DeadLetterHealthCheck(IDeadLetterQueue dlq) => _dlq = dlq;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct)
    {
        var count = await _dlq.GetCountAsync(DeadLetterQueryFilter.PendingOnly(), ct);

        return count switch
        {
            > 100 => HealthCheckResult.Degraded($"{count} messages in DLQ"),
            > 0 => HealthCheckResult.Healthy($"{count} messages in DLQ"),
            _ => HealthCheckResult.Healthy("DLQ empty")
        };
    }
}
```

## Dashboard Patterns

### The Overview Dashboard

A single dashboard that answers "is the system healthy?" at a glance:

| Panel | Metric | Visualization |
|-------|--------|--------------|
| Messages/sec | `rate(dispatch.messages.processed)` | Time series |
| Failure rate | `failed / (processed + failed) * 100` | Gauge (0-100%) |
| p99 latency | `dispatch.messages.duration` p99 | Time series |
| DLQ depth | `dispatch.dlq.depth` | Single stat |
| Circuit breakers | `dispatch.circuitbreaker.state` per transport | Status map |
| Outbox lag | `dispatch.transport.pending_messages` | Single stat |

### The Debug Dashboard

When something is wrong, switch to the debug dashboard to drill down:

| Panel | Metric | Purpose |
|-------|--------|---------|
| Latency by handler | `dispatch.messages.duration` grouped by `handler` | Find the slow handler |
| Failures by type | `dispatch.messages.failed` grouped by `message_type` | Find the failing message |
| DLQ by reason | `dispatch.dlq.enqueued` grouped by `reason` | Understand why messages fail |
| Circuit breaker timeline | `dispatch.circuitbreaker.state_changes` | Correlate outages with circuit trips |
| Retry rate | `dispatch.messages.failed` by `retry_attempt` | Measure transient failure frequency |

## Alert Thresholds

Start with these thresholds and tune based on your system's baseline:

| Alert | Condition | Severity | Action |
|-------|-----------|----------|--------|
| High failure rate | >5% failures over 5 min | Warning | Check handler logs |
| Very high failure rate | >20% failures over 5 min | Critical | Potential outage |
| Latency spike | p99 > 2x baseline for 5 min | Warning | Check dependency health |
| DLQ growing | +50 entries in 15 min | Warning | Review DLQ entries |
| Circuit breaker open | Any breaker in Open state | Warning | Check downstream service |
| Outbox backlog | >1,000 pending for 10 min | Warning | Check outbox processor |
| Health check failing | Readiness probe fails | Critical | Service cannot process |

## Common Troubleshooting Scenarios

### "Latency suddenly spiked"

1. Check `dispatch.messages.duration` grouped by `handler` to find the slow handler
2. Check that handler's traces to find the slow operation (database? API call?)
3. Check `dispatch.circuitbreaker.state` -- is a downstream circuit open, causing retries?

### "DLQ is growing"

1. Check `dispatch.dlq.enqueued` grouped by `reason` -- what's failing?
2. If `MaxRetriesExceeded`: transient failures, check downstream health
3. If `DeserializationFailed`: schema mismatch, check message publishers
4. If `ValidationFailed`: bad data, check upstream systems

### "Messages are processing but nothing happens"

1. Check `dispatch.transport.pending_messages` -- is the outbox processor running?
2. Check transport health checks -- is the broker reachable?
3. Check `dispatch.messages.processed` -- are messages actually being consumed?

## Next Steps

- [Metrics Reference](metrics-reference.md) -- Full catalog of 100+ metrics
- [Health Checks](health-checks.md) -- Detailed health check configuration
- [Azure Monitor](azure-monitor.md) -- Azure Application Insights setup
- [Datadog](datadog-integration.md) -- Datadog APM integration
- [Grafana Dashboards](grafana-dashboards.md) -- Pre-built Grafana dashboards
- [Error Handling & Recovery Guide](../patterns/error-handling.md) -- How failures flow through the system

## See Also

- [Observability Overview](./index.md) - Monitor Dispatch applications with OpenTelemetry, health checks, and integrations
- [Health Checks](./health-checks.md) - Application health monitoring for load balancers and orchestrators
- [Performance Tuning](../operations/performance-tuning.md) - Optimize throughput and latency for production workloads
