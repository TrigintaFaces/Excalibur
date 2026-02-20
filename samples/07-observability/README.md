# Observability Samples

Logging, tracing, and metrics for production monitoring with **Excalibur.Dispatch.Observability**.

## Choosing an Observability Pattern

| Pattern | Best For | Complexity | Dependencies |
|---------|----------|------------|--------------|
| **[OpenTelemetry](OpenTelemetry/)** | Distributed tracing, metrics | Medium | Jaeger (Docker) |
| **[Health Checks](HealthChecks/)** | Kubernetes probes, monitoring | Low | None |

## Samples Overview

| Sample | What It Demonstrates | Local Dev Ready |
|--------|---------------------|-----------------|
| [OpenTelemetry](OpenTelemetry/) | Distributed tracing, custom spans, OTLP export | Yes - Jaeger via Docker |
| [HealthChecks](HealthChecks/) | Liveness/readiness probes, custom checks | Yes - no dependencies |

## Quick Start

### Health Checks (Simplest)

```bash
cd samples/07-observability/HealthChecks
dotnet run

# Test endpoints
curl http://localhost:5000/health
curl http://localhost:5000/health/live
curl http://localhost:5000/health/ready
```

### OpenTelemetry (with Jaeger)

```bash
# Start Jaeger
docker run -d --name jaeger \
  -p 6831:6831/udp \
  -p 16686:16686 \
  -p 4317:4317 \
  jaegertracing/all-in-one:1.52

# Run sample
cd samples/07-observability/OpenTelemetry
dotnet run

# View traces: http://localhost:16686
```

## The Three Pillars of Observability

| Pillar | Purpose | Dispatch Support | Tools |
|--------|---------|------------------|-------|
| **Logs** | Discrete events | `ILogger` integration, correlation IDs | Serilog, Seq, ELK |
| **Metrics** | Aggregated measurements | `AddDispatchMetrics()`, `AddTransportMetrics()` | Prometheus, Grafana |
| **Traces** | Request flow | `DispatchActivitySource`, custom spans | Jaeger, Zipkin, App Insights |

## Observability Patterns Comparison

### When to Use Each Pillar

| Scenario | Logs | Metrics | Traces |
|----------|------|---------|--------|
| **Debugging errors** | Primary | Supporting | Primary |
| **Performance monitoring** | Supporting | Primary | Primary |
| **Capacity planning** | - | Primary | - |
| **Request flow visualization** | - | - | Primary |
| **Alerting** | Supporting | Primary | - |
| **Compliance/Audit** | Primary | - | Supporting |

### Health Check Categories

| Category | Kubernetes Use | What to Check |
|----------|---------------|---------------|
| **Liveness** | `livenessProbe` | Process alive, memory OK |
| **Readiness** | `readinessProbe` | Dependencies available, can handle traffic |
| **Startup** | `startupProbe` | Initial setup complete |

## Key Concepts

### Dispatch Instrumentation

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddSource(DispatchActivitySource.Name)  // Dispatch spans
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddDispatchMetrics()     // Core metrics
        .AddTransportMetrics()    // Transport metrics
        .AddOtlpExporter());
```

### Custom Spans

```csharp
using var activity = DispatchActivitySource.Source.StartActivity("ProcessOrder");
activity?.SetTag("order.id", orderId);
activity?.AddEvent(new ActivityEvent("ValidationComplete"));

try
{
    // Do work
    activity?.SetStatus(ActivityStatusCode.Ok);
}
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.RecordException(ex);
    throw;
}
```

### Health Check Registration

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<DispatchPipelineHealthCheck>("dispatch_pipeline", tags: ["ready"])
    .AddCheck<MemoryHealthCheck>("memory", tags: ["live"]);

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

## Dispatch Metrics

### Core Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `dispatch.messages.processed` | Counter | Total messages processed |
| `dispatch.messages.duration` | Histogram | Processing duration |
| `dispatch.messages.published` | Counter | Messages published |
| `dispatch.messages.failed` | Counter | Failed messages |
| `dispatch.sessions.active` | Gauge | Active sessions |

### Transport Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `dispatch.transport.messages_sent_total` | Counter | Messages sent |
| `dispatch.transport.messages_received_total` | Counter | Messages received |
| `dispatch.transport.errors_total` | Counter | Transport errors |
| `dispatch.transport.send_duration_ms` | Histogram | Send latency |

## Best Practices

### DO

- Use meaningful span names (verb + noun: "ProcessOrder", "ValidatePayment")
- Add relevant tags for filtering (order.id, customer.id)
- Record exceptions on error spans
- Keep liveness checks fast and simple
- Use readiness checks for dependencies
- Filter health check endpoints from traces

### DON'T

- Create spans for every method call (performance overhead)
- Add sensitive data as span tags
- Put database queries in liveness checks
- Use liveness to check external services
- Set very short probe intervals (adds load)

## Exporter Options

| Exporter | Package | Use Case |
|----------|---------|----------|
| Console | `OpenTelemetry.Exporter.Console` | Development |
| OTLP | `OpenTelemetry.Exporter.OpenTelemetryProtocol` | Production (Jaeger, etc.) |
| Zipkin | `OpenTelemetry.Exporter.Zipkin` | Zipkin backend |
| App Insights | `Azure.Monitor.OpenTelemetry.Exporter` | Azure |
| Prometheus | `OpenTelemetry.Exporter.Prometheus` | Kubernetes |

## Health Check Packages

| Package | Checks |
|---------|--------|
| `AspNetCore.HealthChecks.System` | Memory, Disk, Process |
| `AspNetCore.HealthChecks.Uris` | HTTP endpoints |
| `AspNetCore.HealthChecks.SqlServer` | SQL Server |
| `AspNetCore.HealthChecks.Redis` | Redis |
| `AspNetCore.HealthChecks.RabbitMQ` | RabbitMQ |
| `AspNetCore.HealthChecks.Kafka` | Kafka |

## Prerequisites

| Sample | Requirements |
|--------|-------------|
| OpenTelemetry | .NET 9.0 SDK, Docker (for Jaeger) |
| HealthChecks | .NET 9.0 SDK |

## Related Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Observability` | OpenTelemetry integration, metrics |
| `Microsoft.Extensions.Diagnostics.HealthChecks` | Health check framework |
| `OpenTelemetry.Extensions.Hosting` | OTEL host integration |

## Related Samples

- [Audit Logging](../06-security/AuditLogging/) - Compliance logging
- [Message Encryption](../06-security/MessageEncryption/) - Security patterns

## Learn More

- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
- [Jaeger Tracing](https://www.jaegertracing.io/)
- [W3C Trace Context](https://www.w3.org/TR/trace-context/)
- [ASP.NET Core Health Checks](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks)
- [Kubernetes Probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
- [Prometheus Best Practices](https://prometheus.io/docs/practices/naming/)

---

*Category: Observability | Sprint 431*
