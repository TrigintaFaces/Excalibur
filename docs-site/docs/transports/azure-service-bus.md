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
call. To compose without the requirement, register your own `ICloudEventEncoder<TOutbound>`
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

## Scheduled Delivery

A scheduled message is held by the broker until its due time. The same scheduled time is honoured
whether the message is sent on its own, inside a batch, or on the individual fallback a batch uses when
a message does not fit — the delivery you get does not depend on how many other messages happened to be
in flight.

**Declare when a message is due; don't hand-write the property.** `UseScheduling` takes a selector and
stamps the scheduling property for you:

```csharp
builder.UseScheduling(message => message.DueAt);
```

This is the supported way, and it is the one to reach for: the property name is a `const` the compiler
holds, so it cannot be misspelled. A hand-written key that is slightly wrong does not fail — the lookup
simply misses and the message goes out immediately.

:::caution If you set the property yourself, the key must be exact
The sender reads `TransportTelemetryConstants.PropertyKeys.ScheduledTime`, whose value is
`dispatch.scheduled.time` — **dots, not hyphens**. A near-miss key is silently ignored: the message is
not scheduled, no exception is raised, and the property is stripped by the `dispatch.`-prefix filter on
the way out, so it does not even appear on the message in Service Bus Explorer.
:::

```csharp
var message = new TransportMessage
{
    Body = payload,
    Properties = new Dictionary<string, object>(StringComparer.Ordinal)
    {
        // Prefer TransportTelemetryConstants.PropertyKeys.ScheduledTime over a string literal.
        ["dispatch.scheduled.time"] = DateTimeOffset.UtcNow.AddHours(2).ToString("O"),
    },
};

await sender.SendAsync(message, cancellationToken);
```

The value must be a round-trippable timestamp (`"O"` format). A value that cannot be parsed is ignored
and the message is delivered immediately, rather than failing the send.

:::note Cancelling a scheduled message needs a sequence number, and only individual sends return one
Azure Service Bus cancels a scheduled message by its broker sequence number. That number is returned by
the individual send API and **not** by the batch send API, so `SendResult.SequenceNumber` is populated
only for messages that were sent individually. A scheduled message that travelled inside a batch is
delivered on time but cannot be cancelled by sequence number afterwards. If you need to be able to
cancel a scheduled message, send it with `SendAsync` rather than as part of a batch.
:::

## Batch Results

`SendBatchAsync` returns a `BatchSendResult` whose `Results` list is **positional**: entry `i` is the
outcome of the input message at index `i`. That holds regardless of how the message travelled — inside
the batch, or on the individual fallback used when a message does not fit the batch's size limit — and
regardless of whether it succeeded. Every entry also carries its own `MessageId`.

This is what lets you retry precisely the messages that failed:

```csharp
var result = await sender.SendBatchAsync(messages, cancellationToken);

// Entry i describes messages[i], so the failed INPUTS can be selected directly —
// which is what makes a partial failure retryable without redelivering the successes.
var toRetry = result.Results
    .Select((outcome, i) => (outcome, message: messages[i]))
    .Where(x => !x.outcome.IsSuccess && x.outcome.Error?.IsRetryable == true)
    .Select(x => x.message)
    .ToList();

await sender.SendBatchAsync(toRetry, cancellationToken);
```

Because the association between an input and its result is preserved, a partial failure never forces
you to retry the whole batch — which would redeliver the messages that already succeeded.

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
