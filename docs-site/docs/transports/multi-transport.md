---
sidebar_position: 8
title: Multi-Transport Routing
description: Route different message types to different transports
---

# Multi-Transport Routing
Dispatch supports routing messages to multiple transports based on type or predicate rules. All transport routing is configured through the unified `UseRouting()` API.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install Excalibur.Dispatch plus two or more transport packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Dispatch.Transport.Kafka
  dotnet add package Excalibur.Dispatch.Transport.RabbitMQ
  ```
- Familiarity with [choosing a transport](./choosing-a-transport.md) and [routing patterns](../patterns/routing.md)

## Quick Start
```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    dispatch.UseRouting(routing =>
    {
        routing.Transport
            .Route<OrderCreatedEvent>().To("kafka")
            .Route<PaymentProcessedEvent>().To("rabbitmq")
            .Default("rabbitmq");
    });
});

// Register each transport with fluent builder pattern
builder.Services.AddKafkaTransport(kafka =>
{
    kafka.BootstrapServers("localhost:9092")
         .MapTopic<OrderCreatedEvent>("dispatch-events");
});

builder.Services.AddRabbitMQTransport(rmq =>
{
    rmq.HostName("localhost")
       .Port(5672)
       .Credentials("guest", "guest")
       .ConfigureExchange(exchange => exchange.Name("dispatch-events"));
});
```

## Transport Names

Each transport registers with a default name. Use these names in routing rules:

| Transport | Default Name | Registration Method |
|-----------|-------------|---------------------|
| Kafka | `"kafka"` | `AddKafkaTransport()` |
| RabbitMQ | `"rabbitmq"` | `AddRabbitMQTransport()` |
| Azure Service Bus | `"azure-servicebus"` | `AddAzureServiceBusTransport()` |
| AWS SQS | `"aws-sqs"` | `AddAwsSqsTransport()` |
| Google Pub/Sub | `"google-pubsub"` | `AddGooglePubSubTransport()` |
| In-Memory | `"inmemory"` | `AddInMemoryTransport()` |

You can also register named instances:
```csharp
builder.Services.AddKafkaTransport("kafka-orders", kafka =>
{
    kafka.BootstrapServers("localhost:9092")
         .MapTopic<OrderCreatedEvent>("orders");
});

builder.Services.AddKafkaTransport("kafka-analytics", kafka =>
{
    kafka.BootstrapServers("localhost:9092")
         .MapTopic<AnalyticsEvent>("analytics");
});
```

## Routing Strategies

:::info IIntegrationEvent Constraint
Transport routing only accepts `IIntegrationEvent` types. Commands and domain events cannot be routed to transports — they are handled locally. See [Routing](../patterns/routing.md#transport-routing) for details.
:::

### Route by Message Type
```csharp
dispatch.UseRouting(routing =>
{
    routing.Transport
        .Route<OrderCreatedEvent>().To("kafka")
        .Route<PaymentReceivedEvent>().To("kafka")
        .Route<NotificationSentEvent>().To("rabbitmq")
        .Default("rabbitmq");
});
```

### Conditional Routing

Route messages based on message content using `.When()` predicates:

```csharp
dispatch.UseRouting(routing =>
{
    routing.Transport
        // Route high-value orders to priority queue
        .Route<OrderCreatedEvent>()
            .When(order => order.TotalAmount > 10000).To("rabbitmq-priority")
        // Route standard orders to regular queue
        .Route<OrderCreatedEvent>().To("rabbitmq")
        .Default("rabbitmq");
});
```

Context-aware predicates have access to `IMessageContext`:

```csharp
dispatch.UseRouting(routing =>
{
    routing.Transport
        .Route<OrderCreatedEvent>()
            .When((msg, ctx) => ctx.Items.ContainsKey("express")).To("kafka")
        .Route<OrderCreatedEvent>().To("rabbitmq")
        .Default("rabbitmq");
});
```

### Default Transport
```csharp
dispatch.UseRouting(routing =>
{
    routing.Transport
        // Specific routes evaluated first
        .Route<OrderCreatedEvent>().To("kafka")
        // Default for all unmatched message types
        .Default("rabbitmq");
});
```

## Route Evaluation Order

Routes are evaluated in this order:
1. **Type-specific routes** — `Route<T>()` matching the exact message type
2. **Conditional routes** — `.When()` predicates evaluated in registration order
3. **Default transport** — `.Default()`

The first matching route determines the target transport.

## Real-World Scenarios

### High-Volume Events to Kafka, Default to RabbitMQ
```csharp
dispatch.UseRouting(routing =>
{
    routing.Transport
        // High-volume integration events -> Kafka for streaming/analytics
        .Route<OrderCreatedEvent>().To("kafka")
        .Route<PaymentProcessedEvent>().To("kafka")
        .Route<InventoryUpdatedEvent>().To("kafka")
        // Default -> RabbitMQ for all other integration events
        .Default("rabbitmq");
});
```

### Environment-Specific Routing
```csharp
dispatch.UseRouting(routing =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Local: everything in-memory
        routing.Transport.Default("inmemory");
    }
    else
    {
        routing.Transport
            .Route<OrderCreatedEvent>().To("kafka")
            .Default("rabbitmq");
    }
});
```

## Combined Transport and Endpoint Routing

`UseRouting()` provides both transport selection and endpoint routing in a single configuration:

```csharp
dispatch.UseRouting(routing =>
{
    // Tier 1: Which message bus?
    routing.Transport
        .Route<OrderCreatedEvent>().To("rabbitmq")
        .Route<AnalyticsEvent>().To("kafka")
        .Default("rabbitmq");

    // Tier 2: Which services receive this?
    routing.Endpoints
        .Route<OrderCreatedEvent>()
            .To("billing-service", "inventory-service");

    routing.Fallback.To("dead-letter-queue");
});
```

See [Message Routing](../patterns/routing.md) for the full `UseRouting()` API including endpoint routing and fallback configuration.

## Google Pub/Sub Routing

Google Pub/Sub uses the default name `"google-pubsub"`:

```csharp
builder.Services.AddGooglePubSubTransport("dispatch-events", pubsub =>
{
    pubsub.ProjectId("my-gcp-project")
          .TopicId("dispatch-events")
          .SubscriptionId("dispatch-events-sub");
});

dispatch.UseRouting(routing =>
{
    routing.Transport
        .Route<OrderCreatedEvent>().To("google-pubsub")
        .Default("rabbitmq");
});
```

## Diagnostics
```csharp
var router = serviceProvider.GetRequiredService<IDispatchRouter>();

// Check available routes for a message
var routes = router.GetAvailableRoutes(message, context);

// Check if a destination is configured for a message type
var canRoute = router.CanRouteTo(message, "billing-service");
```

## Next Steps
- [Kafka Transport](kafka.md) — High-throughput streaming
- [RabbitMQ Transport](rabbitmq.md) — Flexible routing patterns
- [Azure Service Bus](azure-service-bus.md) — Azure-native messaging
- [Message Routing](../patterns/routing.md) — Full UseRouting() API reference

## See Also

- [Transports Overview](index.md) - All available transport providers
- [Choosing a Transport](choosing-a-transport.md) - Comparison guide for transport selection
- [Message Mapping](message-mapping.md) - Transform messages per transport
