---
sidebar_position: 21
title: Observability
description: Monitor Dispatch and Excalibur applications with comprehensive observability
---

# Observability

Monitor Dispatch and Excalibur applications with OpenTelemetry, health checks, and integrations.

:::tip Start here
The **[Production Observability Guide](production-observability.md)** explains which metrics matter, what to alert on, and how to build dashboards that tell you something useful.
:::

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- For cloud-specific instrumentation, install the provider package (e.g., `Excalibur.Dispatch.Observability`)
- Familiarity with [OpenTelemetry](https://opentelemetry.io/) concepts and [Dispatch pipeline](../pipeline/index.md)

## Monitoring Integrations

| Platform | Guide | Description |
|----------|-------|-------------|
| [Telemetry Configuration](telemetry-configuration.md) | Setup | Configure OpenTelemetry meters and tracing |
| [Metric Naming Conventions](metric-naming-conventions.md) | Reference | Meter and instrument naming patterns |
| [Metrics Reference](metrics-reference.md) | Reference | Complete metrics catalog |
| [Health Checks](health-checks.md) | Built-in | Application health monitoring |
| [Azure Monitor](azure-monitor.md) | Cloud | Azure Application Insights |
| [AWS CloudWatch](aws-cloudwatch.md) | Cloud | AWS monitoring and logging |
| [Google Cloud](google-cloud-monitoring.md) | Cloud | GCP monitoring |
| [Datadog](datadog-integration.md) | APM | Datadog integration |
| [Grafana](grafana-dashboards.md) | Dashboards | Pre-built dashboards |

## OpenTelemetry

Dispatch provides native OpenTelemetry support for distributed tracing and metrics. Use the `UseOpenTelemetry()` convenience method to enable both:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseOpenTelemetry(); // Enables tracing + metrics (recommended)
});
```

Or enable them individually for more control:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseTracing();  // Distributed tracing only
    dispatch.UseMetrics();  // Metrics only
});
```

This registers:
- **TracingMiddleware** - Creates OpenTelemetry spans for each message
- **MetricsMiddleware** - Records processing duration, success/failure counts
- **IDispatchMetrics** - Meter definitions for `Excalibur.Dispatch.Core`

### Meter Registration

Register all framework meters at once using the convenience methods:

```csharp
builder.Services.AddOpenTelemetry()
    .AddAllDispatchMetrics()
    .AddAllDispatchTracing();
```

Or register selectively using meter name patterns:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("Excalibur.Dispatch.*");          // Core, circuit breaker, streaming
        metrics.AddMeter("Excalibur.Dispatch.Transport.*"); // Transport-layer metrics (Kafka, RabbitMQ, etc.)
        metrics.AddMeter("Excalibur.Data.*");              // Persistence, CDC, audit metrics
        metrics.AddMeter("Excalibur.EventSourcing.*");     // Event store metrics
    });
```

See [Telemetry Configuration](telemetry-configuration.md) for the full setup guide and [Metrics Reference](metrics-reference.md) for the complete catalog of 100+ metrics.

## Related Documentation

- [Deployment](../deployment/index.md) - Deployment guides with monitoring setup
- [Security](../security/index.md) - Security monitoring

## See Also

- [Telemetry Configuration](./telemetry-configuration.md) - Configure OpenTelemetry metrics and tracing registration
- [Metric Naming Conventions](./metric-naming-conventions.md) - Meter and instrument naming patterns across packages
- [Metrics Reference](./metrics-reference.md) - Complete catalog of 100+ available metrics
- [Health Checks](./health-checks.md) - Application health monitoring for load balancers and orchestrators
- [Production Observability Guide](./production-observability.md) - Which metrics matter, what to alert on, and dashboard patterns
