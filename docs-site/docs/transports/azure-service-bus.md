---
sidebar_position: 5
title: Azure Service Bus Transport
description: Azure Service Bus transport for Azure-native cloud messaging
---

# Azure Service Bus Transport
Azure Service Bus transport for enterprise-grade messaging with Azure-native integration.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- An Azure Service Bus namespace with connection string
- Familiarity with [transport concepts](./index.md) and [choosing a transport](./choosing-a-transport.md)

## Installation
```bash
dotnet add package Excalibur.Dispatch.Transport.AzureServiceBus
```

## Quick Start

### Using the Dispatch Builder (Recommended)
```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseAzureServiceBus(asb =>
    {
        asb.ConnectionString(builder.Configuration.GetConnectionString("ServiceBus")!)
           .ConfigureProcessor(processor => processor.DefaultEntity("orders-queue"));
    });
});
```

### Standalone Registration (Message Bus)
Register the Service Bus message bus and a keyed `IMessageBus`:

```csharp
services.Configure<AzureServiceBusOptions>(options =>
{
    options.ConnectionString = builder.Configuration
        .GetConnectionString("ServiceBus");
    options.QueueName = "orders-queue";
});

services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value;
    var client = new ServiceBusClient(options.ConnectionString);
    return new AzureServiceBusMessageBus(
        client,
        sp.GetRequiredService<IPayloadSerializer>(),
        options,
        sp.GetRequiredService<ILogger<AzureServiceBusMessageBus>>());
});

services.AddRemoteMessageBus(
    "servicebus",
    sp => sp.GetRequiredService<AzureServiceBusMessageBus>());
```

## Managed Identity (Recommended)
```csharp
services.Configure<AzureServiceBusOptions>(options =>
{
    options.Namespace = "mynamespace.servicebus.windows.net";
    options.QueueName = "orders-queue";
});

services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<AzureServiceBusOptions>>().Value;
    var credential = new DefaultAzureCredential();
    var client = new ServiceBusClient(options.Namespace, credential);
    return new AzureServiceBusMessageBus(
        client,
        sp.GetRequiredService<IPayloadSerializer>(),
        options,
        sp.GetRequiredService<ILogger<AzureServiceBusMessageBus>>());
});

services.AddRemoteMessageBus(
    "servicebus",
    sp => sp.GetRequiredService<AzureServiceBusMessageBus>());
```

## CloudEvents Entity Defaults
CloudEvents options are applied when the Service Bus broker auto-creates
topics/subscriptions. Configure them via `ConfigureCloudEvents()` on the transport builder:

```csharp
services.AddAzureServiceBusTransport(sb =>
{
    sb.ConnectionString("Endpoint=sb://...")
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

```csharp
services.UseCloudEventsForServiceBus(options =>
{
    options.EnableDuplicateDetection = true;
    options.MaxDeliveryCount = 10;
});
```

## Session Support for Ordered CloudEvents

Use `SessionServiceBusConsumer` for FIFO-ordered message processing:

```csharp
services.UseCloudEventsForServiceBus(options =>
{
    options.UseSessionsForOrdering = true;
    options.DefaultSessionId = "orders";
});

// The SessionServiceBusConsumer handles session lifecycle automatically:
// - Acquires session locks
// - Processes messages in order within each session
// - Releases sessions on idle timeout
// - Event IDs 24320-24326 for session-specific logging
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
      .ConfigureProcessor(processor => processor.MaxConcurrentCalls(10))
      .MapEntity<OrderCreated>("orders-queue");
});
```

## Health Checks
`AzureServiceBusHealthChecker` implements `IHealthCheck` directly. Register it with the health checks builder:

```csharp
services.AddHealthChecks()
    .AddCheck<AzureServiceBusHealthChecker>("servicebus");
```

For custom health check logic, inject the checker:

```csharp
public class MyHealthCheck(AzureServiceBusHealthChecker checker) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        var result = await checker.CheckHealthAsync(context, cancellationToken);
        // Add custom logic as needed
        return result;
    }
}
```

## Observability
```csharp
services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Excalibur.Dispatch.Observability");
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

- [Choosing a Transport](./choosing-a-transport.md) — Compare Azure Service Bus against other transports
- [Azure Functions Deployment](../deployment/azure-functions.md) — Run Dispatch handlers in Azure Functions
- [Multi-Transport Routing](./multi-transport.md) — Route different message types across Azure Service Bus and other transports
- [Azure Monitor Integration](../observability/azure-monitor.md) — Configure Azure-native observability for Dispatch
