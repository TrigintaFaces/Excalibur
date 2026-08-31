---
sidebar_position: 5
title: Azure Service Bus Transport
description: Azure Service Bus transport for Azure-native cloud messaging
---

# Azure Service Bus Transport
Azure Service Bus transport for enterprise-grade messaging with Azure-native integration.

## Before You Start

- **.NET 10.0**
- An Azure Service Bus namespace with connection string
- Familiarity with [transport concepts](./index.md) and [choosing a transport](./choosing-a-transport.md)

## Installation
```bash
dotnet add package Excalibur.Dispatch.Transport.AzureServiceBus
```

:::tip One-Line Setup with Metapackage

For the fastest setup, use the **`Excalibur.Dispatch.Azure`** experience metapackage. It bundles the Azure Service Bus transport with Polly resilience and OpenTelemetry observability in a single call:

```bash
dotnet add package Excalibur.Dispatch.Azure
```

```csharp
services.AddDispatchAzure(asb =>
{
    asb.ConnectionString(builder.Configuration.GetConnectionString("ServiceBus")!)
       .ConfigureSender(sender => sender.DefaultEntityName = "orders-queue");
});
```

`AddDispatchAzure` calls `AddDispatch` internally and configures `UseAzureServiceBus`, `UseResilience`, and `UseObservability`. Pass an optional second parameter (`Action<IDispatchBuilder>`) for additional pipeline configuration. See [Package Guide](../package-guide.md#experience-metapackages) for details.
:::

## Quick Start

### Using the Dispatch Builder (Recommended)
```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseAzureServiceBus(asb =>
    {
        asb.ConnectionString(builder.Configuration.GetConnectionString("ServiceBus")!)
           .ConfigureSender(sender => sender.DefaultEntityName = "orders-queue")
           .ConfigureProcessor(processor => processor.DefaultEntityName = "orders-queue");
    });
});
```

### Standalone Registration (Without the Dispatch Builder)
Register the transport directly on the service collection. This builds the `ServiceBusClient`,
the sender and the processor for you, and validates the options at startup:

```csharp
services.AddAzureServiceBusTransport(sb =>
{
    sb.ConnectionString(builder.Configuration.GetConnectionString("ServiceBus")!)
      .ConfigureSender(sender => sender.DefaultEntityName = "orders-queue");
});
```

Pass a name as the first argument to register more than one Service Bus namespace side by side.
Each name gets its own options, client and bus:

```csharp
services.AddAzureServiceBusTransport("payments", sb =>
{
    sb.ConnectionString(builder.Configuration.GetConnectionString("PaymentsBus")!)
      .ConfigureSender(sender => sender.DefaultEntityName = "payments-queue")
      .MapEntity<PaymentReceived>("payments-queue");
});
```

## Managed Identity (Recommended)
Call `FullyQualifiedNamespace` instead of `ConnectionString`. The transport then authenticates with
`DefaultAzureCredential`, so no secret is stored in configuration:

```csharp
services.AddAzureServiceBusTransport(sb =>
{
    sb.FullyQualifiedNamespace("mynamespace.servicebus.windows.net")
      .ConfigureSender(sender => sender.DefaultEntityName = "orders-queue");
});
```

The same call works on the dispatch builder:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseAzureServiceBus(sb =>
    {
        sb.FullyQualifiedNamespace("mynamespace.servicebus.windows.net")
          .ConfigureSender(sender => sender.DefaultEntityName = "orders-queue")
          .ConfigureProcessor(processor => processor.DefaultEntityName = "orders-queue");
    });
});
```

Supply exactly one of the two. Options are validated at startup (`ValidateOnStart`), so a
configuration with neither a connection string nor a fully-qualified namespace -- or one with no
`Sender.DefaultEntityName` -- fails when the host starts rather than on the first send.

## CloudEvents Entity Defaults
CloudEvents options are applied when the Service Bus broker auto-creates
topics/subscriptions. Configure them via `ConfigureCloudEvents()` on the transport builder:

```csharp
services.AddAzureServiceBusTransport(sb =>
{
    sb.ConnectionString("Endpoint=sb://...")
      .ConfigureSender(sender => sender.DefaultEntityName = "orders-topic")
      .ConfigureCloudEvents(ce =>
      {
          // Session support for ordered delivery
          ce.UseSessionsForOrdering = true;
          ce.DefaultSessionId = "orders";

          // Duplicate detection
          ce.EnableDuplicateDetection = true;
          ce.DuplicateDetectionWindow = TimeSpan.FromMinutes(10);

          // Dead-letter and delivery settings
          ce.EnableDeadLetterQueue = true;
          ce.MaxDeliveryCount = 10;
          ce.TimeToLive = TimeSpan.FromDays(14);
      });
});
```

Alternatively, use the standalone extension method:

:::note Trimming and Native AOT
The CloudEvents mapper bundled with this transport serializes the message payload with
reflection-based JSON, so these registrations carry `[RequiresUnreferencedCode]` and
`[RequiresDynamicCode]`. A host that trims or publishes ahead of time gets a warning at the
call. To compose without the requirement, register your own `ICloudEventMapper<TTransportMessage>`
backed by a source-generated serializer.
:::

```csharp
services.AddCloudEventsForServiceBus(options =>
{
    options.EnableDuplicateDetection = true;
    options.MaxDeliveryCount = 10;
});
```

## Session Support for Ordered CloudEvents

Turn on session-based ordering for FIFO message processing:

```csharp
services.AddCloudEventsForServiceBus(options =>
{
    options.UseSessionsForOrdering = true;
    options.DefaultSessionId = "orders";
});
```

Session lifecycle is handled for you once the option is set:

- Session locks are acquired and renewed
- Messages are processed in order within each session
- Sessions are released on idle timeout

Session activity logs under event IDs 24320-24326 (message received, acknowledged, rejected,
visibility modified, receive error, acknowledge error, lock lost).

Sessions must also be enabled on the queue or subscription itself. Set `RequiresSession` on the
processor options when the transport is configured against an entity that requires them:

```csharp
services.AddAzureServiceBusTransport(sb =>
{
    sb.FullyQualifiedNamespace("mynamespace.servicebus.windows.net")
      .ConfigureSender(sender => sender.DefaultEntityName = "orders-queue")
      .ConfigureProcessor(processor =>
      {
          processor.DefaultEntityName = "orders-queue";
          processor.RequiresSession = true;
      });
});
```

### Session Configuration Options

| Option | Default | Description |
|--------|---------|-------------|
| `UseSessionsForOrdering` | `false` | Enable session-based ordering |
| `DefaultSessionId` | `null` | Default session ID for messages |

### When to Use Sessions

- **Order-dependent workflows**: Invoice line items, step sequences
- **Aggregate streams**: Process events for one aggregate at a time
- **Customer isolation**: Process each customer's messages in order

## Transport Registration

Register Azure Service Bus using the standard single entry point pattern:

```csharp
services.AddAzureServiceBusTransport("orders", sb =>
{
    sb.FullyQualifiedNamespace("mynamespace.servicebus.windows.net")
      .ConfigureSender(sender => sender.DefaultEntityName = "orders-queue")
      .ConfigureProcessor(processor => processor.MaxConcurrentCalls = 10)
      .MapEntity<OrderCreated>("orders-queue");
});
```

## Health Checks
Register the built-in namespace-connectivity probe on the health checks builder:

```csharp
services.AddHealthChecks()
    .AddAzureServiceBusHealthCheck();
```

The name defaults to `azure-servicebus`. Override it, the failure status, or the tags to fit an
existing health-check layout:

```csharp
services.AddHealthChecks()
    .AddAzureServiceBusHealthCheck(
        name: "servicebus",
        failureStatus: HealthStatus.Degraded,
        tags: ["messaging", "ready"]);
```

For custom health-check logic, write your own `IHealthCheck` and register it alongside this one
rather than wrapping it -- the probe implementation is an internal detail of the transport and is
not part of the public surface.

## Observability
```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Excalibur.Dispatch");
        tracing.AddSource("Azure.Messaging.ServiceBus");
    })
    .WithMetrics(metrics =>
    {
        metrics.AddDispatchMetrics();
    });
```

## Production Checklist
- [ ] Use managed identity (no connection strings in production)
- [ ] Set `MaxConcurrentCalls` and `PrefetchCount` for throughput
- [ ] Enable CloudEvents options for ordering and deduplication
- [ ] Configure DLQ behavior and TTL defaults

## Next Steps
- [AWS SQS](aws-sqs.md) -- For AWS-native messaging
- [Multi-Transport Routing](multi-transport.md) -- Combine Azure Service Bus with other transports

## See Also

- [Choosing a Transport](./choosing-a-transport.md) -- Compare Azure Service Bus against other transports
- [Azure Functions Deployment](../deployment/azure-functions.md) -- Run Dispatch handlers in Azure Functions
- [Multi-Transport Routing](./multi-transport.md) -- Route different message types across Azure Service Bus and other transports
- [Azure Monitor Integration](../observability/azure-monitor.md) -- Configure Azure-native observability for Dispatch
