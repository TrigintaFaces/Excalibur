---
sidebar_position: 2
title: Telemetry Configuration
description: Configure OpenTelemetry metrics and tracing for Excalibur.Dispatch
---

# Telemetry Configuration

Configure OpenTelemetry integration for metrics collection and distributed tracing across the Excalibur framework.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch.Observability
  dotnet add package OpenTelemetry.Extensions.Hosting
  ```
- Familiarity with [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/) concepts
- An OpenTelemetry-compatible backend (Prometheus, OTLP, Azure Monitor, etc.)

## Quick Start

### Register All Metrics and Tracing

The simplest approach registers all framework meters and activity sources at once:

```csharp
builder.Services.AddOpenTelemetry()
    .AddAllDispatchMetrics()
    .AddAllDispatchTracing();
```

These extension methods are defined in `OpenTelemetryExtensions` from the `Excalibur.Dispatch.Observability` package.

### With Exporters

Combine with your preferred exporter:

```csharp
builder.Services.AddOpenTelemetry()
    .AddAllDispatchMetrics()
    .AddAllDispatchTracing()
    .WithMetrics(metrics => metrics
        .AddPrometheusExporter())
    .WithTracing(tracing => tracing
        .AddOtlpExporter());
```

## Metrics Registration

### Register All Meters (Recommended)

`AddAllDispatchMetrics()` registers all known meters across the entire Excalibur framework:

```csharp
// On IOpenTelemetryBuilder (simplest)
builder.Services.AddOpenTelemetry()
    .AddAllDispatchMetrics();

// On MeterProviderBuilder (within WithMetrics)
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddAllDispatchMetrics()
        .AddPrometheusExporter());
```

This registers the following meters:

| Meter Name | Package | Description |
|------------|---------|-------------|
| `Excalibur.Dispatch.Core` | Dispatch | Core message processing counters and histograms |
| `Excalibur.Dispatch.Pipeline` | Dispatch | Pipeline execution metrics |
| `Excalibur.Dispatch.TimePolicy` | Dispatch | Time policy metrics |
| `Excalibur.Dispatch.BatchProcessor` | Dispatch | Batch processing metrics |
| `Excalibur.Dispatch.Transport` | Transport.Abstractions | Transport send/receive/error counters |
| `Excalibur.Dispatch.DeadLetterQueue` | Observability | Dead letter queue metrics |
| `Excalibur.Dispatch.CircuitBreaker` | Observability | Circuit breaker state and operation metrics |
| `Excalibur.Dispatch.Streaming` | Dispatch | Streaming document handler metrics |
| `Excalibur.Dispatch.Compliance` | Compliance | Compliance monitoring metrics |
| `Excalibur.Dispatch.Compliance.Erasure` | Compliance | GDPR erasure request metrics |
| `Excalibur.Dispatch.Encryption` | Compliance | Encryption operation metrics |
| `Excalibur.Dispatch.BackgroundServices` | Outbox | Background service processing metrics |
| `Excalibur.Dispatch.Sagas` | Saga | Saga orchestration metrics |
| `Excalibur.Dispatch.WriteStores` | Data.Abstractions | Write store operation metrics |
| `Excalibur.Dispatch.Observability.Context` | Observability | Context flow tracking metrics |
| `Excalibur.EventSourcing.MaterializedViews` | EventSourcing | Materialized view rebuild metrics |
| `Excalibur.Data.Cdc` | Data.SqlServer | Change data capture metrics |
| `Excalibur.Data.Audit` | Data.ElasticSearch | Security audit metrics |
| `Excalibur.LeaderElection` | LeaderElection | Leader election acquisition metrics |

### Per-Package Opt-In Registration

For fine-grained control, register individual meters:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        // Core dispatch only
        metrics.AddDispatchMetrics();

        // Transport only
        metrics.AddTransportMetrics();

        // Or use AddMeter with specific names
        metrics.AddMeter("Excalibur.Dispatch.CircuitBreaker");
        metrics.AddMeter("Excalibur.LeaderElection");
    });
```

The `AddDispatchMetrics()` and `AddTransportMetrics()` convenience methods are available on both `IOpenTelemetryBuilder` and `MeterProviderBuilder`.

### DI-Based Metrics Registration

Register metrics instrumentation services for dependency injection:

```csharp
// All observability metrics (core + circuit breaker + DLQ)
builder.Services.AddAllDispatchMetrics();

// Or individually
builder.Services.AddDispatchMetricsInstrumentation();
builder.Services.AddCircuitBreakerMetrics();
builder.Services.AddDeadLetterQueueMetrics();

// With configuration
builder.Services.AddDispatchMetricsInstrumentation(options =>
{
    options.MeterName = "MyApp.Dispatch";
});
```

These extension methods from `ObservabilityMetricsServiceCollectionExtensions` register the singleton metric classes (`DispatchMetrics`, `CircuitBreakerMetrics`, `DeadLetterQueueMetrics`) into the DI container.

### Transport-Specific Meters

Each transport registers its own meter automatically during `AddXxxTransport()`:

| Transport | Meter Name |
|-----------|------------|
| RabbitMQ | `Excalibur.Dispatch.Transport.RabbitMQ` |
| Kafka | `Excalibur.Dispatch.Transport.Kafka` |
| Azure Service Bus | `Excalibur.Dispatch.Transport.AzureServiceBus` |
| Google Pub/Sub | `Excalibur.Dispatch.Transport.GooglePubSub` |
| AWS SQS | `Excalibur.Dispatch.Transport.AwsSqs` |

These follow the pattern `Excalibur.Dispatch.Transport.{TransportName}` defined in `TransportTelemetryConstants.MeterName()`.

To subscribe to transport-specific meters:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        // All transports at once (wildcard)
        metrics.AddMeter("Excalibur.Dispatch.Transport.*");

        // Or specific transports
        metrics.AddMeter("Excalibur.Dispatch.Transport.Kafka");
        metrics.AddMeter("Excalibur.Dispatch.Transport.RabbitMQ");
    });
```

## Tracing Registration

### Register All Activity Sources (Recommended)

`AddAllDispatchTracing()` registers all known activity sources:

```csharp
// On IOpenTelemetryBuilder
builder.Services.AddOpenTelemetry()
    .AddAllDispatchTracing();

// On TracerProviderBuilder
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAllDispatchTracing()
        .AddOtlpExporter());
```

This registers the following activity sources:

| Activity Source Name | Package | Description |
|---------------------|---------|-------------|
| `Excalibur.Dispatch.Core` | Dispatch | Core message processing spans |
| `Excalibur.Dispatch.Pipeline` | Dispatch | Pipeline execution spans |
| `Excalibur.Dispatch.TimePolicy` | Dispatch | Time policy operation spans |
| `Excalibur.Dispatch.Streaming` | Dispatch | Streaming handler spans |
| `Excalibur.Dispatch.Compliance.Erasure` | Compliance | GDPR erasure operation spans |
| `Excalibur.Data.Cdc` | Data.SqlServer | CDC processing spans |
| `Excalibur.Data.Audit` | Data.ElasticSearch | Audit recording spans |
| `Excalibur.LeaderElection` | LeaderElection | Leader election acquisition spans |

### Per-Source Opt-In

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        // Only core dispatch tracing
        tracing.AddSource("Excalibur.Dispatch.Core");

        // Or use wildcards
        tracing.AddSource("Excalibur.Dispatch.*");
    });
```

## Pipeline-Level Telemetry

Enable OpenTelemetry middleware in the Dispatch pipeline:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseOpenTelemetry(); // Enables tracing + metrics (recommended)
});

// Or enable individually
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseTracing();  // Distributed tracing only
    dispatch.UseMetrics();  // Metrics only
});
```

This registers:
- **TracingMiddleware** -- Creates OpenTelemetry spans for each dispatched message
- **MetricsMiddleware** -- Records processing duration, success/failure counts

## Custom Metric Filtering

### Filter by Meter Name

Use OpenTelemetry's view API to filter or customize metrics:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAllDispatchMetrics();

        // Drop high-cardinality tags from transport metrics
        metrics.AddView(
            instrumentName: "dispatch.transport.send.duration",
            new MetricStreamConfiguration
            {
                TagKeys = ["dispatch.transport.name"]
            });

        // Set histogram boundaries for processing duration
        metrics.AddView(
            instrumentName: "dispatch.messages.duration",
            new ExplicitBucketHistogramConfiguration
            {
                Boundaries = [1, 5, 10, 25, 50, 100, 250, 500, 1000]
            });
    });
```

### Suppress Specific Metrics

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAllDispatchMetrics();

        // Drop a specific metric instrument entirely
        metrics.AddView(
            instrumentName: "dispatch.circuitbreaker.state",
            MetricStreamConfiguration.Drop);
    });
```

## Telemetry Constants Reference

Each package exposes its telemetry names through constants classes. Use these when building custom instrumentation or filtering rules.

| Constants Class | Namespace | Contains |
|----------------|-----------|----------|
| `DispatchTelemetryConstants` | `Excalibur.Dispatch.Diagnostics` | Core meter/activity/tag names |
| `StreamingHandlerTelemetryConstants` | `Excalibur.Dispatch.Diagnostics` | Streaming meter/activity names |
| `TransportTelemetryConstants` | `Excalibur.Dispatch.Transport.Diagnostics` | Transport metric/tag names |
| `GooglePubSubTelemetryConstants` | `Excalibur.Dispatch.Transport.GooglePubSub` | Google Pub/Sub consolidated names |
| `ContextObservabilityTelemetryConstants` | `Excalibur.Dispatch.Observability.Diagnostics` | Context flow meter/activity names |
| `ErasureTelemetryConstants` | `Excalibur.Dispatch.Compliance.Diagnostics` | GDPR erasure meter/activity/metric names |
| `CdcTelemetryConstants` | `Excalibur.Data.SqlServer.Diagnostics` | CDC meter/activity/metric names |
| `AuditTelemetryConstants` | `Excalibur.Data.ElasticSearch.Diagnostics` | Audit meter/activity/metric names |
| `LeaderElectionTelemetryConstants` | `Excalibur.LeaderElection.Diagnostics` | Leader election meter/activity/metric names |
| `EventSourcingMeters` | `Excalibur.EventSourcing.Observability` | Event/snapshot store meter names |
| `EventSourcingActivitySources` | `Excalibur.EventSourcing.Observability` | Event/snapshot store activity source names |

## Complete Example

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Register Dispatch with pipeline telemetry
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseOpenTelemetry();
});

// 2. Register DI-based metrics instrumentation
builder.Services.AddAllDispatchMetrics();

// 3. Configure OpenTelemetry with all meters and activity sources
builder.Services.AddOpenTelemetry()
    .AddAllDispatchMetrics()
    .AddAllDispatchTracing()
    .WithMetrics(metrics =>
    {
        // Transport-specific meters (registered automatically by AddXxxTransport)
        metrics.AddMeter("Excalibur.Dispatch.Transport.*");

        // Event sourcing meters (if using ES)
        metrics.AddMeter("Excalibur.EventSourcing.*");

        // Export to Prometheus
        metrics.AddPrometheusExporter();
    })
    .WithTracing(tracing =>
    {
        // Event sourcing activity sources (if using ES)
        tracing.AddSource("Excalibur.EventSourcing.*");

        // Export to OTLP
        tracing.AddOtlpExporter();
    });

var app = builder.Build();

// Expose Prometheus scrape endpoint
app.MapPrometheusScrapingEndpoint();

app.Run();
```

## See Also

- [Metrics Reference](./metrics-reference.md) -- Complete catalog of 100+ available metrics
- [Metric Naming Conventions](./metric-naming-conventions.md) -- Naming patterns across packages
- [Health Checks](./health-checks.md) -- Application health monitoring
- [Production Observability Guide](./production-observability.md) -- Which metrics matter and what to alert on
