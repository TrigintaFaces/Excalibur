---
sidebar_position: 7
title: Google Pub/Sub Transport
description: Google Cloud Pub/Sub transport for GCP-native cloud messaging
---

# Google Pub/Sub Transport
Google Cloud Pub/Sub transport for scalable, GCP-native messaging with global availability.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A Google Cloud project with Pub/Sub API enabled
- Familiarity with [choosing a transport](./choosing-a-transport.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Installation
```bash
dotnet add package Excalibur.Dispatch.Transport.GooglePubSub
```

## Quick Start

### Using the Dispatch Builder (Recommended)
```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseGooglePubSub(pubsub =>
    {
        pubsub.ProjectId("my-gcp-project")
              .TopicId("dispatch-events")
              .SubscriptionId("dispatch-events-sub");
    });
});
```

### Standalone Registration
```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

services.AddGooglePubSubTransport("dispatch-events", pubsub =>
{
    pubsub.ProjectId("my-gcp-project")
          .TopicId("dispatch-events")
          .SubscriptionId("dispatch-events-sub")
          .MapTopic<OrderCreated>("orders-topic");
});
```

Google Pub/Sub registers a keyed `IMessageBus` named `GooglePubSub` or
`GooglePubSub:{TopicId}` when `TopicId` is set:
```csharp
var bus = serviceProvider.GetRequiredKeyedService<IMessageBus>("GooglePubSub:dispatch-events");
```

## Configuration

### Pub/Sub Options
Configure core transport settings with `GooglePubSubOptions`:

```csharp
services.Configure<GooglePubSubOptions>(options =>
{
    options.ProjectId = "my-gcp-project";
    options.TopicId = "dispatch-events";
    options.SubscriptionId = "dispatch-events-sub";

    options.MaxPullMessages = 100;
    options.AckDeadlineSeconds = 60;
    options.EnableAutoAckExtension = true;
    options.MaxConcurrentAcks = 10;
    options.MaxConcurrentMessages = 0; // Uses Environment.ProcessorCount * 2
});
```

### CloudEvents Configuration

#### Via Transport Builder
Configure CloudEvents settings directly on the transport builder:

```csharp
services.AddGooglePubSubTransport("events", pubsub =>
{
    pubsub.ProjectId("my-gcp-project")
          .TopicId("dispatch-events")
          .SubscriptionId("dispatch-events-sub")
          .ConfigureCloudEvents(ce =>
          {
              ce.UseOrderingKeys = true;
              ce.UseExactlyOnceDelivery = true;
              ce.EnableDeduplication = true;
              ce.EnableCompression = true;
              ce.CompressionThreshold = 1024 * 1024; // 1MB
          });
});
```

#### Standalone CloudEvents Registration
Use `UseCloudEventsForPubSub` for standalone CloudEvents configuration:

```csharp
services.UseCloudEventsForPubSub(options =>
{
    options.UseOrderingKeys = true;
    options.UseExactlyOnceDelivery = true;
    options.DefaultTopic = "dispatch-events";
    options.DefaultSubscription = "dispatch-events-sub";
});
```

When `UseOrderingKeys` is enabled, CloudEvents use the partition key as the Pub/Sub
ordering key to preserve ordering for related messages.

## Message Compression

Configure compression for large messages using `PubSubCompressionOptions`:

```csharp
services.AddGooglePubSubTransport("events", pubsub =>
{
    pubsub.ProjectId("my-gcp-project")
          .TopicId("dispatch-events")
          .ConfigureOptions(options =>
          {
              // Enable compression
              options.Compression.Enabled = true;

              // Choose algorithm: Gzip (best ratio) or Snappy (fastest)
              options.Compression.Algorithm = CompressionAlgorithm.Snappy;

              // Only compress messages larger than threshold
              options.Compression.ThresholdBytes = 1024; // 1 KB

              // Auto-detect compressed messages on receive
              options.Compression.EnableAutoDetection = true;

              // Control whether to compress already-compressed content types
              options.Compression.CompressAlreadyCompressedContent = false;

              // Add custom content types to the compressed list
              options.Compression.CompressedContentTypes.Add("application/x-custom-compressed");
          });
});
```

### Compression Algorithm Comparison

| Algorithm | Speed | Ratio | Use Case |
|-----------|-------|-------|----------|
| `Gzip` | Slower | Better | Large payloads, bandwidth-constrained |
| `Snappy` | Faster | Good | High throughput, latency-sensitive |
| `Brotli` | Slowest | Best | Pre-compressed static content |
| `Deflate` | Moderate | Good | Balance of speed and ratio |

### Compression Requirements

- **Snappy** requires the `Snappier` NuGet package (v1.2.0+)
- Auto-detection uses magic bytes to identify Gzip/Deflate streams (Snappy/Brotli cannot be auto-detected)
- Messages below `ThresholdBytes` are sent uncompressed
- Already-compressed content types (images, videos, archives) are skipped by default

```bash
# For Snappy compression
dotnet add package Snappier
```

## Dead Letter Topics

Configure dead letter handling via the transport options:

```csharp
services.AddGooglePubSubTransport("events", pubsub =>
{
    pubsub.ProjectId("my-gcp-project")
          .TopicId("dispatch-events")
          .EnableDeadLetter("dispatch-events-dlq");
});
```

## Health Checks
When using transport adapters, register aggregate health checks:

```csharp
services.AddHealthChecks()
    .AddTransportHealthChecks();
```

## Observability
```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Excalibur.Dispatch.Observability");
        tracing.AddGoogleCloudTraceExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddDispatchMetrics();
    });
```

Configure telemetry options via `GooglePubSubOptions`:
```csharp
services.Configure<GooglePubSubOptions>(options =>
{
    options.EnableOpenTelemetry = true;
    options.ExportToCloudMonitoring = true;
    options.TracingSamplingRatio = 0.1; // 10% sampling
    options.EnableTracePropagation = true;
});
```

## Production Checklist
- [ ] Use Workload Identity or managed credentials
- [ ] Configure `UseExactlyOnceDelivery` for critical streams
- [ ] Enable ordering keys for strict ordering requirements
- [ ] Set ack deadlines and auto-extend for long handlers
- [ ] Configure dead letter topics for failed messages
- [ ] Enable OpenTelemetry and Cloud Monitoring

## Next Steps
- [Multi-Transport Routing](multi-transport.md) — Combine Pub/Sub with other transports
- [In-Memory Transport](in-memory.md) — For local development

## See Also

- [Choosing a Transport](./choosing-a-transport.md) — Compare Google Pub/Sub against other transports
- [Google Cloud Functions Deployment](../deployment/google-cloud-functions.md) — Run Dispatch handlers in Cloud Functions with Pub/Sub triggers
- [Multi-Transport Routing](./multi-transport.md) — Route different message types across Pub/Sub and other transports
- [Google Cloud Monitoring](../observability/google-cloud-monitoring.md) — Configure GCP-native observability for Dispatch
