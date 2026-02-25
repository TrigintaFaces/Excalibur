---
sidebar_position: 10
title: Message Mapping
description: Transform messages between transport formats for multi-transport scenarios
---

# Message Mapping

When using [multiple transports](multi-transport.md), messages may need transformation between transport-specific formats. Message mapping handles transport-level property translation (headers, routing keys, partition keys) as messages move between different messaging systems.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [multi-transport routing](./multi-transport.md) and [choosing a transport](./choosing-a-transport.md)

## Core Concepts

| Component | Purpose |
|-----------|---------|
| `IMessageMapper` | Transforms a message context from one transport format to another |
| `IMessageMapper<TSource, TTarget>` | Strongly-typed mapper between specific message types |
| `IMessageMapperRegistry` | Registry for looking up mappers by source/target transport |
| `IMessageMappingBuilder` | Fluent builder for configuring per-message-type mappings |
| `IOutboxMessageMapper` | Bridges outbox messages to transport-specific contexts |

## Quick Start

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Configure message mapping for multi-transport
    dispatch.WithMessageMapping(mapping =>
    {
        mapping.UseDefaultMappers();
    });

    dispatch.UseRouting(routing =>
    {
        routing.Transport
            .Route<OrderCreatedEvent>().To("kafka")
            .Default("rabbitmq");
    });
});
```

## IMessageMapper

The base mapper interface transforms transport message contexts:

```csharp
public interface IMessageMapper
{
    string Name { get; }
    string SourceTransport { get; }
    string TargetTransport { get; }
    bool CanMap(string sourceTransport, string targetTransport);
    ITransportMessageContext Map(ITransportMessageContext source, string targetTransportName);
}
```

## IMessageMapper&lt;TSource, TTarget&gt;

The typed mapper transforms message payloads between types:

```csharp
public interface IMessageMapper<in TSource, out TTarget>
{
    TTarget Map(TSource source, ITransportMessageContext context);
}
```

Use this for converting between internal and external message formats, version upgrades/downgrades, or enriching messages with additional data.

## IMessageMapperRegistry

The registry provides lookup by transport combination:

```csharp
public interface IMessageMapperRegistry
{
    void Register(IMessageMapper mapper);
    IMessageMapper? GetMapper(string sourceTransport, string targetTransport);
    IEnumerable<IMessageMapper> GetAllMappers();
    bool HasMapper(string sourceTransport, string targetTransport);
}
```

## Built-In Mappers

Dispatch provides built-in mappers for common transport pairs:

| Mapper | Source | Target | What It Maps |
|--------|--------|--------|-------------|
| `RabbitMqToKafkaMapper` | RabbitMQ | Kafka | Routing keys -> partition keys, AMQP headers -> Kafka headers |
| `KafkaToRabbitMqMapper` | Kafka | RabbitMQ | Partition keys -> routing keys, Kafka headers -> AMQP headers |
| `DefaultMessageMapper` | Any | Any | Pass-through with basic header mapping |

### Enable Built-In Mappers

```csharp
dispatch.WithMessageMapping(mapping =>
{
    mapping.UseDefaultMappers(); // Registers RabbitMQ<->Kafka mappers
});
```

## Fluent Mapping Builder

The `IMessageMappingBuilder` provides per-message-type transport configuration:

```csharp
dispatch.WithMessageMapping(mapping =>
{
    mapping.MapMessage<OrderCreatedEvent>()
        .ToRabbitMq(ctx =>
        {
            ctx.Exchange = "orders";
            ctx.RoutingKey = "orders.created";
            ctx.DeliveryMode = 2; // persistent
        })
        .ToKafka(ctx =>
        {
            ctx.Topic = "orders";
            ctx.Key = "order-created";
        });
    mapping.MapMessage<PaymentProcessedEvent>()
        .ToAzureServiceBus(ctx =>
        {
            ctx.TopicOrQueueName = "payments";
            ctx.SessionId = "payment-session";
        });
    mapping.ConfigureDefaults(defaults => defaults
        .ForRabbitMq(ctx => ctx.DeliveryMode = 2)
        .ForKafka(ctx => ctx.Topic = "default-topic"));
});
```

### Builder Methods

| Method | Description |
|--------|-------------|
| `MapMessage<T>()` | Begin mapping configuration for a message type |
| `RegisterMapper(mapper)` | Register a custom mapper instance |
| `RegisterMapper<T>()` | Register a custom mapper type via DI |
| `UseDefaultMappers()` | Register built-in mappers for common transport pairs |
| `ConfigureDefaults(...)` | Set global default mappings for all message types |

## Transport-Specific Mapping Contexts

Each transport exposes a context interface for fine-grained control over message properties.

### RabbitMQ — `IRabbitMqMappingContext`

| Property | Type | Description |
|----------|------|-------------|
| `Exchange` | `string?` | Exchange name |
| `RoutingKey` | `string?` | Routing key |
| `Priority` | `byte?` | Message priority (0-255) |
| `ReplyTo` | `string?` | Reply-to queue name |
| `Expiration` | `string?` | Expiration in milliseconds |
| `DeliveryMode` | `byte?` | 1 = non-persistent, 2 = persistent |
| `SetHeader(key, value)` | method | Set a custom AMQP header |

### Kafka — `IKafkaMappingContext`

| Property | Type | Description |
|----------|------|-------------|
| `Topic` | `string?` | Topic name |
| `Key` | `string?` | Message key (partitioning) |
| `Partition` | `int?` | Target partition (null = auto) |
| `SchemaId` | `int?` | Schema registry ID |
| `SetHeader(key, value)` | method | Set a custom Kafka header |

### Azure Service Bus — `IAzureServiceBusMappingContext`

| Property | Type | Description |
|----------|------|-------------|
| `TopicOrQueueName` | `string?` | Topic or queue name |
| `SessionId` | `string?` | Session ID for session-enabled entities |
| `PartitionKey` | `string?` | Partition key |
| `ReplyToSessionId` | `string?` | Reply-to session ID |
| `TimeToLive` | `TimeSpan?` | Message TTL |
| `ScheduledEnqueueTime` | `DateTimeOffset?` | Scheduled delivery time |
| `SetProperty(key, value)` | method | Set a custom application property |

### AWS SQS — `IAwsSqsMappingContext`

| Property | Type | Description |
|----------|------|-------------|
| `QueueUrl` | `string?` | Queue URL |
| `MessageGroupId` | `string?` | Group ID (FIFO queues) |
| `MessageDeduplicationId` | `string?` | Dedup ID (FIFO queues) |
| `DelaySeconds` | `int?` | Visibility delay |
| `SetAttribute(name, value, dataType)` | method | Set a message attribute |

### AWS SNS — `IAwsSnsMappingContext`

| Property | Type | Description |
|----------|------|-------------|
| `TopicArn` | `string?` | Topic ARN |
| `MessageGroupId` | `string?` | Group ID (FIFO topics) |
| `MessageDeduplicationId` | `string?` | Dedup ID (FIFO topics) |
| `Subject` | `string?` | Subject for email endpoints |
| `SetAttribute(name, value, dataType)` | method | Set a message attribute |

### Google Pub/Sub — `IGooglePubSubMappingContext`

| Property | Type | Description |
|----------|------|-------------|
| `TopicName` | `string?` | Topic name |
| `OrderingKey` | `string?` | Ordering key for ordered delivery |
| `SetAttribute(key, value)` | method | Set a custom attribute |

### gRPC — `IGrpcMappingContext`

| Property | Type | Description |
|----------|------|-------------|
| `MethodName` | `string?` | Service method name |
| `Deadline` | `TimeSpan?` | Call deadline |
| `SetHeader(key, value)` | method | Set a custom call header |

### Custom Transport

For transports not covered by built-in contexts:

```csharp
.ToTransport("my-custom-transport", ctx =>
{
    ctx.SetHeader("custom-key", "custom-value");
})
```

## Custom Mappers

Implement `IMessageMapper` for custom transport mapping. The easiest approach is to extend `DefaultMessageMapper`:

```csharp
public class RabbitMqToServiceBusMapper : DefaultMessageMapper
{
    public RabbitMqToServiceBusMapper()
        : base("RabbitMqToServiceBus", "rabbitmq", "azureservicebus")
    {
    }

    protected override void CopyTransportProperties(
        ITransportMessageContext source,
        TransportMessageContext target)
    {
        base.CopyTransportProperties(source, target);

        // Transform RabbitMQ routing key to Service Bus subject header
        if (source.Headers.TryGetValue("routing-key", out var routingKey))
        {
            target.SetHeader("ServiceBus-Subject", routingKey);
        }
    }
}
```

Register custom mappers:

```csharp
dispatch.WithMessageMapping(mapping =>
{
    mapping.UseDefaultMappers();
    mapping.RegisterMapper(new RabbitMqToServiceBusMapper());
    mapping.RegisterMapper<MyOtherMapper>(); // resolved via DI
});
```

## IOutboxMessageMapper

The outbox mapper bridges the outbox pattern with the message mapping system, transforming outbound messages for their target transport:

```csharp
public interface IOutboxMessageMapper
{
    ITransportMessageContext CreateContext(OutboundMessage message, string targetTransport);

    ITransportMessageContext MapToTransport(
        OutboundMessage message,
        ITransportMessageContext sourceContext,
        string targetTransport);

    IReadOnlyCollection<string> GetTargetTransports(string messageType);
}
```

### How It Works

1. The outbox processor reads an `OutboundMessage` from the store
2. `GetTargetTransports()` determines which transports should receive the message
3. `CreateContext()` builds an initial transport context from the outbound message
4. `MapToTransport()` applies configured mappings to produce the final context
5. The message is published to each target transport with its mapped context

### End-to-End Cross-Transport Example

```csharp
// Configure: OrderCreated goes to both RabbitMQ and Kafka
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    dispatch.WithMessageMapping(mapping =>
    {
        mapping.MapMessage<OrderCreatedEvent>()
            .ToRabbitMq(ctx =>
            {
                ctx.Exchange = "orders";
                ctx.RoutingKey = "orders.created";
                ctx.DeliveryMode = 2;
            })
            .ToKafka(ctx =>
            {
                ctx.Topic = "orders-events";
                ctx.Key = "order-created";
            });
    });

    dispatch.UseRouting(routing =>
    {
        routing.Transport
            .Route<OrderCreatedEvent>().To("rabbitmq");
    });
});
```

When `OrderCreatedEvent` is dispatched:
1. Transport routing sends it to both RabbitMQ and Kafka
2. The RabbitMQ copy gets `Exchange = "orders"`, `RoutingKey = "orders.created"`, persistent delivery
3. The Kafka copy gets `Topic = "orders-events"`, `Key = "order-created"`

## Message Mapping vs Transport Routing

These are complementary systems:

| System | Purpose | API |
|--------|---------|-----|
| **Transport Routing** | Determines *which* transports receive a message | `UseRouting()` via `routing.Transport` |
| **Message Mapping** | Transforms *how* the message is formatted per transport | `WithMessageMapping()` |

Transport routing decides "send this to Kafka"; message mapping ensures the message has the correct Kafka-specific headers and properties.

## Next Steps

- [Multi-Transport Routing](multi-transport.md) — Route messages to different transports
- [Kafka Transport](kafka.md) — Kafka-specific configuration
- [RabbitMQ Transport](rabbitmq.md) — RabbitMQ-specific configuration

## See Also

- [Transports Overview](index.md) - All available transport providers
- [Multi-Transport Routing](multi-transport.md) - Route messages to different transports
- [Actions and Handlers](../core-concepts/actions-and-handlers.md) - Message types and handler patterns
