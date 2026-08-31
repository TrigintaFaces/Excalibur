---
sidebar_position: 7
title: Google Pub/Sub Transport
description: Google Cloud Pub/Sub transport for GCP-native cloud messaging
---

# Google Pub/Sub Transport
Google Cloud Pub/Sub transport for scalable, GCP-native messaging with global availability.

## Before You Start

- **.NET 10.0**
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
services.Configure<GooglePubSubOptions>("google-pubsub", options =>
{
    options.Connection.ProjectId = "my-gcp-project";
    options.Connection.TopicId = "dispatch-events";
    options.Connection.SubscriptionId = "dispatch-events-sub";

    options.Subscriber.MaxPullMessages = 100;
    options.Subscriber.AckDeadlineSeconds = 60;
    options.Subscriber.EnableAutoAckExtension = true;
    options.Subscriber.MaxConcurrentAcks = 10;
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
          .ConfigureOptions(options => options.Subscriber.EnableExactlyOnceDelivery = true)
          .ConfigureCloudEvents(ce =>
          {
              ce.UseOrderingKeys = true;
              ce.Transport.EnableCompression = true;
              ce.Transport.CompressionThreshold = 1024 * 1024; // 1MB
          });
});
```

#### Standalone CloudEvents Registration
Use `AddCloudEventsForPubSub` for standalone CloudEvents configuration:

:::note Trimming and Native AOT
The CloudEvents mapper bundled with this transport serializes the message payload with
reflection-based JSON, so these registrations carry `[RequiresUnreferencedCode]` and
`[RequiresDynamicCode]`. A host that trims or publishes ahead of time gets a warning at the
call. To compose without the requirement, register your own `ICloudEventMapper<TTransportMessage>`
backed by a source-generated serializer.
:::

```csharp
services.AddCloudEventsForPubSub(options => options.UseOrderingKeys = true);
```

When `UseOrderingKeys` is enabled, CloudEvents use the partition key as the Pub/Sub
ordering key to preserve ordering for related messages.

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

### Auto-Apply Dead Letter Policy

By default the transport only references the dead letter topic; attaching the dead letter policy to the subscription is normally an infrastructure-as-code concern. You can opt in to having the transport automatically apply the policy to the subscription at startup (a `GetSubscription` + `UpdateSubscription`) so it is actually honored rather than configured but never attached:

```csharp
services.AddGooglePubSubTransport("events", pubsub =>
{
    pubsub.ProjectId("my-gcp-project")
          .TopicId("dispatch-events")
          .SubscriptionId("dispatch-events-sub")
          .EnableDeadLetter("dispatch-events-dlq")
          .ConfigureOptions(options =>
          {
              options.AutoApplyDeadLetterPolicy = true;   // attach the policy at startup
              options.DeadLetterMaxDeliveryAttempts = 5;  // attempts before dead-lettering
          });
});
```

The policy is applied only when `AutoApplyDeadLetterPolicy` is enabled, a dead letter topic is configured, and a subscription id is set. `DeadLetterMaxDeliveryAttempts` controls how many delivery attempts occur before a message is dead-lettered.

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `AutoApplyDeadLetterPolicy` | `bool` | `false` | Attaches the configured dead letter policy to the subscription at startup. |
| `DeadLetterMaxDeliveryAttempts` | `int` | `5` | Delivery attempts before a message is dead-lettered (applies when auto-apply is enabled). |

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
        tracing.AddSource("Excalibur.Dispatch");
        tracing.AddGoogleCloudTraceExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddDispatchMetrics();
    });
```

Configure telemetry options via `GooglePubSubOptions`:
```csharp
services.Configure<GooglePubSubOptions>("google-pubsub", options =>
{
    options.Telemetry.EnableOpenTelemetry = true;
    options.Telemetry.ExportToCloudMonitoring = true;
    options.Telemetry.TracingSamplingRatio = 0.1; // 10% sampling
    options.Telemetry.EnableTracePropagation = true;
});
```

## Production Checklist
- [ ] Use Workload Identity or managed credentials
- [ ] Configure `Subscriber.EnableExactlyOnceDelivery` for critical streams
- [ ] Enable ordering keys for strict ordering requirements
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
