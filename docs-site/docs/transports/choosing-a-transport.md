---
sidebar_position: 2
title: Choosing a Transport
description: How to select the right message transport for your system based on throughput, ordering, cloud provider, and feature needs
---

# Choosing a Transport

Excalibur supports five production transports plus two built-in ones for development and scheduling. Each transport has different strengths, and the right choice depends on your throughput needs, ordering requirements, cloud provider, and operational preferences.

This guide helps you make that decision and shows how to start simple and evolve.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the core package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Install your chosen transport package (e.g., `Excalibur.Dispatch.Transport.Kafka`)
- Familiarity with [getting started](../getting-started/index.md) and [pipeline concepts](../pipeline/index.md)

## The Quick Decision

If you are starting a new project and want a single recommendation:

- **Already on Azure?** Use **Azure Service Bus**.
- **Already on AWS?** Use **AWS SQS**.
- **Already on GCP?** Use **Google Pub/Sub**.
- **Need high-throughput event streaming?** Use **Kafka**.
- **Need complex routing patterns (topic, header, fanout)?** Use **RabbitMQ**.
- **Not sure yet?** Start with **In-Memory**, and switch when you need a real broker. The Dispatch transport abstraction makes this a configuration change, not a code change.

## Transport Comparison

### Feature Matrix

| Feature | Kafka | RabbitMQ | Azure Service Bus | AWS SQS | Google Pub/Sub |
|---------|:-----:|:--------:|:-----------------:|:-------:|:--------------:|
| **Throughput** | Very high | High | High | High | High |
| **Message ordering** | Per-partition | Per-queue | Per-session | FIFO queues | Per-key |
| **Message retention** | Configurable (days/weeks) | Until consumed | Until consumed | Up to 14 days | Until consumed |
| **Consumer groups** | Built-in | Manual (competing consumers) | Subscriptions | Built-in | Subscriptions |
| **Dead letter queue** | Topic-based | Exchange-based (DLX) | Native `$DeadLetterQueue` subqueue | Queue-based (native redrive) | Subscription-based |
| **Replay/rewind** | Yes (offset reset) | No | No | No | Yes (seek) |
| **Managed cloud option** | Confluent, MSK, etc. | CloudAMQP, etc. | Azure-native | AWS-native | GCP-native |
| **Self-hosted** | Yes | Yes | No | No | No |
| **AOT-safe** | Partial (SchemaRegistry needs reflection) | Yes | Yes | Yes | Yes |

### When to Choose Each

#### Kafka

Best for **high-throughput event streaming** and scenarios where you need to replay historical events.

**Choose Kafka when:**
- You need to process hundreds of thousands of messages per second
- You want to retain messages for days or weeks (log-style)
- Multiple independent consumers need to read the same events at their own pace
- You need replay capability (reprocess events from a point in time)
- You are building event sourcing with event streaming as the backbone

**Be aware:**
- Highest operational complexity (ZooKeeper/KRaft, partitions, replication)
- Ordering is per-partition, not global (design partition keys carefully)
- Schema Registry uses reflection (`Activator.CreateInstance`), which means AOT trimming requires `[RequiresUnreferencedCode]` annotations on Schema Registry paths

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseKafka(kafka =>
    {
        kafka.BootstrapServers("broker1:9092,broker2:9092")
             .ConfigureConsumer(c => c.GroupId("order-service"))
             .MapTopic<OrderCreatedEvent>("orders.events");
    });
});
```

#### RabbitMQ

Best for **traditional messaging** with rich routing patterns.

**Choose RabbitMQ when:**
- You need flexible routing: topic-based, header-based, fanout, or direct
- You want a simple, well-understood message broker
- You need request/reply (RPC) patterns
- You prefer self-hosted infrastructure
- Your message volume is moderate (thousands to tens of thousands per second)

**Be aware:**
- Messages are consumed and deleted (no replay)
- Scaling consumers requires manual queue/exchange configuration
- Clustering adds complexity for high availability

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseRabbitMQ(rmq =>
    {
        rmq.ConnectionString("amqp://guest:guest@localhost:5672/")
           .ConfigureExchange(ex => ex.Name("dispatch.events").Type(RabbitMqExchangeType.Topic));
    });
});
```

#### Azure Service Bus

Best for **Azure-native** applications that need enterprise messaging features.

**Choose Azure Service Bus when:**
- Your application runs on Azure
- You need session-based ordering (per-customer, per-order)
- You want Azure-managed dead letter subqueues
- You need scheduled message delivery
- You want Azure AD (Entra ID) authentication instead of connection strings

**Be aware:**
- Azure-only (no self-hosted option)
- Pricing is per-operation (can be expensive at very high throughput)
- Queue/topic limits depend on your Azure tier

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseAzureServiceBus(asb =>
    {
        asb.ConnectionString(configuration.GetConnectionString("ServiceBus")!)
           .ConfigureProcessor(p => p.DefaultEntity("orders-queue"));
    });
});
```

#### AWS SQS

Best for **AWS-native** applications with simple queue semantics.

**Choose AWS SQS when:**
- Your application runs on AWS
- You want a fully managed, serverless queue (no infrastructure to manage)
- You need native redrive policies for dead letter handling
- You are using AWS Lambda as consumers
- You want FIFO queues for strict ordering

**Be aware:**
- AWS-only
- Standard queues provide at-least-once delivery (not exactly-once)
- FIFO queues have a 300 msg/sec limit (3,000 with batching)
- Message size limit is 256 KB (use Claim Check for larger payloads)

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseAwsSqs(sqs =>
    {
        sqs.UseRegion("us-west-2")
           .ConfigureQueue(q => q.VisibilityTimeout(TimeSpan.FromMinutes(5)))
           .MapQueue<OrderCreated>("https://sqs.us-west-2.amazonaws.com/.../orders");
    });
});
```

#### Google Pub/Sub

Best for **GCP-native** applications with global messaging needs.

**Choose Google Pub/Sub when:**
- Your application runs on GCP
- You need global message delivery across regions
- You want subscription-based fan-out (multiple subscribers per topic)
- You need ordering keys for per-entity ordering
- You want native dead letter topic support

**Be aware:**
- GCP-only
- Pricing is per-data-volume (can be cost-effective or expensive depending on message size)
- Ordering is per-ordering-key, not global

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseGooglePubSub(pubsub =>
    {
        pubsub.ProjectId("my-gcp-project")
              .TopicId("dispatch-events")
              .SubscriptionId("dispatch-events-sub");
    });
});
```

## Starting Simple

You do not need to choose a production transport on day one. Dispatch's transport abstraction means your handlers, middleware, and routing logic are transport-independent.

### Development: In-Memory

Start with the built-in in-memory transport. No broker to install, no configuration to manage:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    // In-memory transport is the default — no UseXxx() call needed
});
```

### First Deployment: Single Transport

When you are ready for a real broker, add one transport:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseRabbitMQ(rmq => rmq.ConnectionString(connectionString));
});
```

Your handlers do not change. Only the registration code changes.

### Growing System: Multi-Transport

As your system grows, you may find that different message types have different needs. Route them to different transports:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // High-throughput events to Kafka
    dispatch.UseKafka(kafka => kafka.BootstrapServers("kafka:9092"));

    // Command processing to RabbitMQ
    dispatch.UseRabbitMQ(rmq => rmq.ConnectionString(rmqConnectionString));

    dispatch.UseRouting(routing =>
    {
        routing.Transport
            .Route<OrderCreatedEvent>().To("kafka")
            .Route<PaymentProcessedEvent>().To("kafka")
            .Route<SendNotificationCommand>().To("rabbitmq")
            .Default("rabbitmq");
    });
});
```

**Reference:** [Message Routing](../patterns/routing.md) for the full routing API.

## Serialization Considerations

All transports use the same serialization layer. The default is MemoryPack for maximum .NET performance, but if your consumers include non-.NET services, switch to a cross-platform format:

| Scenario | Recommended Serializer |
|----------|----------------------|
| .NET-only consumers | MemoryPack (default) |
| Mixed language consumers | System.Text.Json or Protobuf |
| Maximum compactness | MessagePack |
| Schema evolution needed | Protobuf |

```csharp
// Switch to JSON for cross-language compatibility
services.AddJsonSerialization();

// Or MessagePack for compact binary
services.AddMessagePackSerialization();
```

## Resilience Across Transports

Every transport supports the same resilience features, but each has transport-specific tuning:

| Concern | How It Works |
|---------|-------------|
| **Connection recovery** | All transports auto-reconnect on network failures |
| **Circuit breakers** | Per-transport isolation via `ITransportCircuitBreakerRegistry` |
| **Dead letter handling** | Application-level (`IDeadLetterQueue`) works with any transport. All 5 production transports implement `IDeadLetterQueueManager` for transport-native DLQ |
| **Health checks** | `services.AddHealthChecks().AddTransportHealthChecks()` monitors all registered transports |

**Reference:** [Error Handling & Recovery Guide](../patterns/error-handling.md) for the full resilience story.

## Summary

| Decision Factor | Kafka | RabbitMQ | Azure SB | AWS SQS | Google Pub/Sub |
|----------------|-------|----------|----------|---------|----------------|
| Start here if... | You need event streaming | You need routing flexibility | You're on Azure | You're on AWS | You're on GCP |
| Throughput ceiling | Millions/sec | Tens of thousands/sec | Hundreds of thousands/sec | Thousands/sec (FIFO) | Hundreds of thousands/sec |
| Operational burden | High (self-managed) | Medium | Low (managed) | Low (managed) | Low (managed) |
| Replay capability | Yes | No | No | No | Yes (seek) |
| Self-hosted | Yes | Yes | No | No | No |

The transport you choose today does not lock you in. Dispatch's abstractions mean you can switch or add transports without changing your handlers, middleware, or business logic.

## Next Steps

- [Kafka](kafka.md) -- Full Kafka transport configuration
- [RabbitMQ](rabbitmq.md) -- Full RabbitMQ transport configuration
- [Azure Service Bus](azure-service-bus.md) -- Full Azure Service Bus configuration
- [AWS SQS](aws-sqs.md) -- Full AWS SQS configuration
- [Google Pub/Sub](google-pubsub.md) -- Full Google Pub/Sub configuration
- [Multi-Transport Routing](multi-transport.md) -- Configure multiple transports with routing rules
- [Message Routing](../patterns/routing.md) -- Two-tier routing system

## See Also

- [In-Memory Transport](./in-memory.md) — Built-in transport for development and testing without a broker
- [Message Mapping](./message-mapping.md) — Configure how message types map to transport-specific destinations
- [Health Checks](../observability/health-checks.md) — Monitor transport connectivity and health
