---
sidebar_position: 8
title: Message Routing
description: Route messages to transports and endpoints using the unified UseRouting() API
---

# Message Routing

Dispatch provides a unified routing system that controls **where messages go**. Configure all routing through a single `UseRouting()` entry point with a fluent builder API.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- At least one transport configured (see [choosing a transport](../transports/choosing-a-transport.md))
- Familiarity with [pipeline concepts](../pipeline/index.md) and [middleware](../middleware/index.md)

## Three-Layer Architecture

Getting a message from your code to a broker involves three distinct layers, each answering a different question:

| Layer | Question | Configured Via | Example |
|-------|----------|----------------|---------|
| **Transport** | "Which message bus?" | `routing.Transport.Route<T>().To("rabbitmq")` | `"rabbitmq"`, `"kafka"`, `"local"` |
| **Destination** | "Which queue/topic on that bus?" | `builder.MapTopic<T>("orders.events")` | `"orders.events"`, `"payments-queue"` |
| **Endpoint** | "Which logical services receive this?" | `routing.Endpoints.Route<T>().To("billing")` | `"billing-service"`, `"inventory"` |

- **Transport** is an infrastructure decision — which broker receives the message.
- **Destination** is a physical address — the actual queue or topic name on that broker. This is configured per-transport during DI registration (see [Where Does the Destination Come From?](#where-does-the-destination-come-from) below).
- **Endpoint** is a logical concern — which services should process the message. This is optional; if omitted, the transport delivers to its default destination.

`IDispatchRouter` combines transport selection and endpoint routing into a single `RoutingDecision`. Destination mapping is handled separately by each transport adapter.

## Message Flow: From Dispatch to Broker

When you call `dispatcher.DispatchAsync(message, ct)`, here is the runtime resolution chain:

```
1. DispatchAsync(message)
       │
2.     ▼  RoutingMiddleware
       │  Calls IDispatchRouter.RouteAsync()
       │
3.     ├── ITransportSelector.SelectTransportAsync()
       │       → evaluates Route<T>().To() rules
       │       → returns transport name: "rabbitmq"
       │
       ├── IEndpointRouter.RouteToEndpointsAsync()
       │       → returns endpoint names: ["billing-service"]
       │       → (empty list if no endpoint rules configured)
       │
       ▼
4. RoutingDecision { Transport="rabbitmq", Endpoints=["billing-service"] }
       │  stored on context.RoutingDecision
       │
5.     ▼  TransportRegistry.GetTransportAdapter("rabbitmq")
       │       → returns ITransportAdapter for that broker
       │
6.     ▼  ITransportAdapter creates ITransportSender
       │       → Destination = "orders-queue" (from MapQueue/MapTopic config)
       │
7.     ▼  ITransportSender.SendAsync(transportMessage)
               → message delivered to the physical queue/topic
```

**Key insight:** Transport routing (step 3) and destination mapping (step 6) are configured in different places. Transport routing is in `UseRouting()`. Destination mapping is in the transport's DI registration (e.g., `AddKafkaTransport()`).

## Where Does the Destination Come From?

Each transport maps message types to physical destinations (queues, topics, entities) during DI registration. This is separate from the `UseRouting()` configuration.

### Per-Transport Destination Mapping

```csharp
// Kafka: MapTopic maps message types to Kafka topics
services.AddKafkaTransport("kafka", kafka =>
{
    kafka.BootstrapServers("broker1:9092")
         .MapTopic<OrderCreatedEvent>("orders.events")
         .MapTopic<PaymentProcessedEvent>("payments.events")
         .WithTopicPrefix("prod-");  // Optional: prepends "prod-" to all topics
});

// RabbitMQ: MapExchange / MapQueue maps to exchanges or queues
services.AddRabbitMQTransport("rabbitmq", rmq =>
{
    rmq.ConnectionString("amqp://localhost")
       .MapExchange<OrderCreatedEvent>("orders-exchange")
       .MapQueue<SendNotificationCommand>("notifications-queue")
       .WithQueuePrefix("app-");  // Optional prefix
});

// Azure Service Bus: MapEntity maps to queues or topics
services.AddAzureServiceBusTransport("azure", asb =>
{
    asb.ConnectionString(connectionString)
       .MapEntity<OrderCreatedEvent>("orders-topic")
       .WithEntityPrefix("prod-");
});

// AWS SQS: MapQueue maps to full queue URLs
services.AddAwsSqsTransport("sqs", sqs =>
{
    sqs.UseRegion("us-east-1")
       .MapQueue<OrderCreatedEvent>(
           "https://sqs.us-east-1.amazonaws.com/123456789012/orders");
});

// Google Pub/Sub: MapTopic maps to Pub/Sub topic IDs
services.AddGooglePubSubTransport("pubsub", pubsub =>
{
    pubsub.ProjectId("my-gcp-project")
          .MapTopic<OrderCreatedEvent>("orders-topic");
});
```

### How It All Connects

Here is a complete example showing both layers configured together:

```csharp
// Step 1: Register transports with destination mappings
services.AddKafkaTransport("kafka", kafka =>
{
    kafka.BootstrapServers("broker:9092")
         .MapTopic<OrderCreatedEvent>("orders.events");
});

services.AddRabbitMQTransport("rabbitmq", rmq =>
{
    rmq.ConnectionString("amqp://localhost")
       .MapQueue<SendNotificationCommand>("notifications");
});

// Step 2: Configure routing rules (which transport gets which message)
services.AddDispatch(dispatch =>
{
    dispatch.UseRouting(routing =>
    {
        routing.Transport
            .Route<OrderCreatedEvent>().To("kafka")         // → kafka → "orders.events" topic
            .Route<SendNotificationCommand>().To("rabbitmq") // → rabbitmq → "notifications" queue
            .Default("rabbitmq");

        routing.Endpoints
            .Route<OrderCreatedEvent>()
                .To("billing-service", "inventory-service");
    });
});
```

For per-transport configuration details, see: [Kafka](../transports/kafka.md) | [RabbitMQ](../transports/rabbitmq.md) | [Azure Service Bus](../transports/azure-service-bus.md) | [AWS SQS](../transports/aws-sqs.md) | [Google Pub/Sub](../transports/google-pubsub.md)

## Quick Start

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseRouting(routing =>
    {
        // Tier 1: Transport selection - "which message bus?"
        routing.Transport
            .Route<OrderCreated>().To("rabbitmq")
            .Route<PaymentProcessed>().To("kafka")
            .Default("local");

        // Tier 2: Endpoint routing - "which services receive this?"
        routing.Endpoints
            .Route<OrderCreated>()
                .To("billing-service", "inventory-service")
                .When(msg => msg.Amount > 1000).AlsoTo("fraud-detection");

        // Cross-cutting
        routing.Fallback.To("dead-letter-queue");
    });
});
```

## Transport Routing

Transport routing selects the message bus for each message type. This is the first tier evaluated during routing.

:::info IIntegrationEvent Constraint
Transport routing is restricted to `IIntegrationEvent` types only. This enforces the DDD principle that only integration events should cross service boundaries:

- **Commands** are handled locally by a single handler (request-response semantics)
- **Domain events** stay within aggregate boundaries
- **Integration events** are designed for cross-service communication

```csharp
// Compiles - OrderCreated implements IIntegrationEvent
routing.Transport.Route<OrderCreated>().To("rabbitmq");

// Does NOT compile - PlaceOrderCommand does not implement IIntegrationEvent
// routing.Transport.Route<PlaceOrderCommand>().To("rabbitmq");
// Error CS0311: constraint violation
```

Endpoint routing (`routing.Endpoints`) remains unrestricted and accepts any `IDispatchMessage` for local handler routing.
:::

### Basic Transport Selection

```csharp
routing.Transport
    .Route<OrderCreated>().To("rabbitmq")
    .Route<PaymentProcessed>().To("kafka")
    .Route<InternalEvent>().To("local")
    .Default("local");  // Fallback for unmatched types
```

### Conditional Transport Selection

Use `.When()` to add predicates for dynamic transport decisions:

```csharp
routing.Transport
    .Route<OrderCreated>()
        .When(msg => msg.IsHighPriority).To("kafka")   // Fast transport for priority orders
    .Route<OrderCreated>().To("rabbitmq")               // Standard transport otherwise
    .Default("local");
```

Context-aware predicates have access to `IMessageContext`:

```csharp
routing.Transport
    .Route<OrderCreated>()
        .When((msg, ctx) => ctx.Items.ContainsKey("express")).To("kafka");
```

### ITransportSelector Interface

```csharp
public interface ITransportSelector
{
    ValueTask<string> SelectTransportAsync(
        IDispatchMessage message,
        IMessageContext context,
        CancellationToken cancellationToken);

    IEnumerable<string> GetAvailableTransports(Type messageType);
}
```

## Endpoint Routing

Endpoint routing determines which services receive a message. This is the second tier, supporting multicast delivery.

### Basic Endpoint Routing

```csharp
routing.Endpoints
    .Route<OrderCreated>()
        .To("billing-service", "inventory-service");
```

### Conditional Endpoint Routing

Add endpoints conditionally based on message content:

```csharp
routing.Endpoints
    .Route<OrderCreated>()
        .To("billing-service", "inventory-service")
        .When(msg => msg.Amount > 1000).AlsoTo("fraud-detection")
        .When(msg => msg.IsInternational).AlsoTo("customs-service");
```

### Multiple Message Types

Chain routing rules for different message types:

```csharp
routing.Endpoints
    .Route<OrderCreated>()
        .To("billing-service", "inventory-service")
    .Route<PaymentProcessed>()
        .To("notification-service");
```

### IEndpointRouter Interface

```csharp
public interface IEndpointRouter
{
    ValueTask<IReadOnlyList<string>> RouteToEndpointsAsync(
        IDispatchMessage message,
        IMessageContext context,
        CancellationToken cancellationToken);

    bool CanRouteToEndpoint(IDispatchMessage message, string endpoint);

    IEnumerable<RouteInfo> GetEndpointRoutes(
        IDispatchMessage message,
        IMessageContext context);
}
```

## Unified Router

`IDispatchRouter` combines both tiers into a single routing call. The `RoutingMiddleware` uses this interface internally.

```csharp
public interface IDispatchRouter
{
    ValueTask<RoutingDecision> RouteAsync(
        IDispatchMessage message,
        IMessageContext context,
        CancellationToken cancellationToken);

    bool CanRouteTo(IDispatchMessage message, string destination);

    IEnumerable<RouteInfo> GetAvailableRoutes(
        IDispatchMessage message,
        IMessageContext context);
}
```

## RoutingDecision

The `RoutingDecision` record encapsulates both transport and endpoint decisions:

```csharp
public sealed record RoutingDecision
{
    public required string Transport { get; init; }
    public required IReadOnlyList<string> Endpoints { get; init; }
    public bool IsSuccess => !string.IsNullOrEmpty(Transport);
    public string? FailureReason { get; init; }
    public IReadOnlyList<string> MatchedRules { get; init; } = [];

    public static RoutingDecision Success(
        string transport,
        IReadOnlyList<string> endpoints,
        IReadOnlyList<string>? matchedRules = null);

    public static RoutingDecision Failure(string reason);
}
```

After routing, the decision is stored on `IMessageContext.RoutingDecision` and accessible in subsequent middleware and handlers.

## Fallback Configuration

Configure a fallback endpoint for messages that don't match any routing rules:

```csharp
routing.Fallback
    .To("dead-letter-queue")
    .WithReason("No matching routing rules");
```

When no endpoint rules match and a fallback is configured, the fallback endpoint is included in the routing decision. If routing fails entirely (no transport selected), `RoutingMiddleware` returns a 404 failure result.

## Error and Fallback Behavior

Understanding what happens when routing fails is critical for building reliable systems.

### When No Transport Matches

If `ITransportSelector` cannot match any rule (and no `.Default()` is configured), `DefaultDispatchRouter` returns:

```
RoutingDecision.Failure("No transport could be selected for the message")
```

`RoutingMiddleware` then returns a **404** failure result with `ProblemDetailsTypes.Routing`:

```csharp
// The result your code receives:
var result = await dispatcher.DispatchAsync(message, ct);
// result.IsSuccess == false
// result.ProblemDetails.Status == 404
// result.ProblemDetails.Detail == "Routing failed: No transport could be selected for the message"
```

### When a Transport Name Isn't Registered

If routing selects `"kafka"` but no `AddKafkaTransport("kafka", ...)` was called, `TransportRegistry` throws:

```
InvalidOperationException: Cannot set default transport to 'kafka': transport is not registered.
Available transports: rabbitmq, local
```

This typically surfaces at startup (not at runtime) when the transport registry validates its configuration.

### Prevention Checklist

1. **Always set `.Default()`** on `routing.Transport` to catch unmatched message types
2. **Ensure every transport name** used in `Route<T>().To("name")` has a matching `AddXxxTransport("name", ...)` registration
3. **Configure `routing.Fallback`** to catch messages with no matching endpoint rules
4. **Enable logging** — `RoutingMiddleware` emits `MiddlewareEventId.RoutingFailed` at Warning level

## RouteInfo

The `RouteInfo` class provides diagnostic information about available routes:

```csharp
public class RouteInfo
{
    public string Name { get; }
    public string Endpoint { get; }
    public int Priority { get; }
    public string? BusName { get; set; }
    public Dictionary<string, object?> Metadata { get; init; }
}
```

Lower priority values indicate higher precedence (evaluated first), following the ASP.NET Core routing convention.

## Diagnostics

### Discovering Available Routes

```csharp
var router = serviceProvider.GetRequiredService<IDispatchRouter>();
var routes = router.GetAvailableRoutes(message, context);

foreach (var route in routes)
{
    Console.WriteLine($"Route: {route.Name} -> {route.Endpoint} (Priority: {route.Priority})");
}
```

### Pre-Flight Validation

```csharp
if (router.CanRouteTo(message, "billing-service"))
{
    // Endpoint is configured for this message type
}
```

### Middleware Logging

`RoutingMiddleware` emits structured logs:

| Event ID | Level | Message |
|----------|-------|---------|
| `MiddlewareEventId.MessageRouted` | Information | `"Message routed to: {Target}"` |
| `MiddlewareEventId.UnifiedRoutingComplete` | Debug | `"Routing completed: transport={Transport}, endpoints={EndpointCount}"` |
| `MiddlewareEventId.RoutingFailed` | Warning | `"Routing failed: {Reason}"` |

## Observability

Enable OpenTelemetry tracing and metrics for routing decisions:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseOpenTelemetry();  // Enables both tracing and metrics
    dispatch.UseRouting(routing => { /* ... */ });
});
```

## DI Registration

`UseRouting()` registers three services as singletons:

| Service | Implementation |
|---------|---------------|
| `ITransportSelector` | `ConfiguredTransportSelector` |
| `IEndpointRouter` | `ConfiguredEndpointRouter` |
| `IDispatchRouter` | `DefaultDispatchRouter` |

All implementations are internal. Consumers interact through the public abstractions only.

## Best Practices

### Separate the Three Layers

Keep transport, destination, and endpoint concerns distinct:

```csharp
// Good: Clear separation
routing.Transport.Route<OrderCreated>().To("rabbitmq");
routing.Endpoints.Route<OrderCreated>().To("billing-service");

// Avoid: Mixing concerns in predicates
```

### Set a Default Transport

Always configure a default transport for unmatched message types:

```csharp
routing.Transport.Default("local");
```

### Use Composable Predicates

Express routing logic through `.When()` predicates instead of complex configuration:

```csharp
// Content-based routing
.When(msg => msg.Amount > 1000)

// Header/context-based routing
.When((msg, ctx) => ctx.Items["tenant"] is "premium")

// Geographic routing
.When(msg => msg.Region == "EU")

// Time-based routing
.When(msg => DateTime.UtcNow.Hour is >= 9 and < 17)
```

### Configure Fallback Routing

Ensure messages always have a destination:

```csharp
routing.Fallback.To("dead-letter-queue");
```

## Source Code Reference

| Component | Location |
|-----------|----------|
| `IDispatchRouter` | `src/Dispatch/Excalibur.Dispatch.Abstractions/Routing/IDispatchRouter.cs` |
| `ITransportSelector` | `src/Dispatch/Excalibur.Dispatch.Abstractions/Routing/ITransportSelector.cs` |
| `IEndpointRouter` | `src/Dispatch/Excalibur.Dispatch.Abstractions/Routing/IEndpointRouter.cs` |
| `RoutingDecision` | `src/Dispatch/Excalibur.Dispatch.Abstractions/Routing/RoutingDecision.cs` |
| `RouteInfo` | `src/Dispatch/Excalibur.Dispatch.Abstractions/Routing/RouteInfo.cs` |
| `RoutingMiddleware` | `src/Dispatch/Excalibur.Dispatch/Routing/RoutingMiddleware.cs` |
| `TransportRegistry` | `src/Dispatch/Excalibur.Dispatch/Transport/TransportRegistry.cs` |
| Builder interfaces | `src/Dispatch/Excalibur.Dispatch/Routing/Builder/` |

## See Also

- [Choosing a Transport](../transports/choosing-a-transport.md) - Select the right transport and see per-transport destination mapping
- [Patterns Overview](./index.md) - All messaging and integration patterns
- [Multi-Transport Configuration](../transports/multi-transport.md) - Configure and manage multiple transports
- [Middleware Overview](../middleware/index.md) - Middleware reference and pipeline composition

## Related Patterns

- [Outbox Pattern](outbox.md) - Reliable message publishing
- [Inbox Pattern](inbox.md) - Idempotent message processing
- [Dead Letter](dead-letter.md) - Handle failed messages
- [Claim Check](claim-check.md) - Handle large payloads
