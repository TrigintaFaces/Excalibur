---
sidebar_position: 1
title: Transports
description: Configure message transports for Kafka, RabbitMQ, Azure Service Bus, AWS SQS, and Google Pub/Sub.
---

# Transports

Dispatch supports multiple message transports for distributed messaging. You can use a single transport or route messages to different transports based on rules.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the core package plus your transport:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Dispatch.Transport.Kafka  # or RabbitMQ, AzureServiceBus, etc.
  ```
- Familiarity with [handlers](../handlers.md) and [pipeline concepts](../pipeline/index.md)

:::tip Not sure which transport to use?
Start with the **[Choosing a Transport](choosing-a-transport.md)** guide for a decision matrix and trade-off comparison across all five production transports.
:::

## Supported Transports

| Transport | Package | Use Case |
|-----------|---------|----------|
| [In-Memory](in-memory.md) | Built-in | Testing, development |
| [Cron Timer](cron-timer.md) | Built-in | Scheduled jobs, background tasks |
| [Kafka](kafka.md) | `Excalibur.Dispatch.Transport.Kafka` | High-throughput event streaming |
| [RabbitMQ](rabbitmq.md) | `Excalibur.Dispatch.Transport.RabbitMQ` | Traditional messaging, routing patterns |
| [Azure Service Bus](azure-service-bus.md) | `Excalibur.Dispatch.Transport.AzureServiceBus` | Azure-native messaging |
| [AWS SQS](aws-sqs.md) | `Excalibur.Dispatch.Transport.AwsSqs` | AWS-native messaging |
| [Google Pub/Sub](google-pubsub.md) | `Excalibur.Dispatch.Transport.GooglePubSub` | GCP-native messaging |

## Quick Start

### Single Transport

Configure transports through the `AddDispatch()` builder using `Use{Transport}()` methods:

```csharp
// Install the transport package
// dotnet add package Excalibur.Dispatch.Transport.RabbitMQ

services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Configure transport through the builder (recommended)
    dispatch.UseRabbitMQ(rmq => rmq.HostName("localhost"));
});
```

All five transports follow the same `Use` prefix pattern:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseKafka(kafka => kafka.BootstrapServers("localhost:9092"));
    dispatch.UseRabbitMQ(rmq => rmq.HostName("localhost"));
    dispatch.UseAzureServiceBus(asb => asb.ConnectionString("..."));
    dispatch.UseAwsSqs(sqs => sqs.Region("us-east-1"));
    dispatch.UseGooglePubSub(pubsub => pubsub.ProjectId("my-project"));
});
```

### Multi-Transport Routing

Route different message types to different transports:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Register transports through the builder
    dispatch.UseKafka(kafka => kafka.BootstrapServers("localhost:9092"));
    dispatch.UseRabbitMQ(rmq => rmq.HostName("localhost"));

    dispatch.UseRouting(routing =>
    {
        routing.Transport
            // High-volume events to Kafka
            .Route<OrderCreatedEvent>().To("kafka")
            // Payment events to RabbitMQ
            .Route<PaymentProcessedEvent>().To("rabbitmq")
            // Default transport
            .Default("rabbitmq");
    });
});
```

## Transport Interfaces

Dispatch provides minimal transport interfaces inspired by [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.ai.ichatclient) (`IChatClient`), [Azure.Messaging.ServiceBus](https://learn.microsoft.com/en-us/dotnet/api/azure.messaging.servicebus.servicebusclient) (`ServiceBusClient`), and `HttpClientFactory` (`DelegatingHandler`).

### ITransportSender / ITransportReceiver

Each transport implements two minimal interfaces — 3 methods each plus `GetService()` for raw SDK access:

```csharp
public interface ITransportSender : IAsyncDisposable
{
    string Destination { get; }
    Task<SendResult> SendAsync(TransportMessage message, CancellationToken cancellationToken);
    Task<BatchSendResult> SendBatchAsync(IReadOnlyList<TransportMessage> messages, CancellationToken cancellationToken);
    Task FlushAsync(CancellationToken cancellationToken);
    object? GetService(Type serviceType) => null;
}

public interface ITransportReceiver : IAsyncDisposable
{
    string Source { get; }
    Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken);
    Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken);
    Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken);
    object? GetService(Type serviceType) => null;
}
```

### ITransportSubscriber (Push-Based)

For transports with native push semantics (Kafka consumer groups, RabbitMQ `BasicConsume`, Azure Event Hubs, Google Pub/Sub streaming pull), `ITransportSubscriber` provides a push-based alternative to the pull-based `ITransportReceiver`:

```csharp
public interface ITransportSubscriber : IAsyncDisposable
{
    string Source { get; }
    Task SubscribeAsync(
        Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler,
        CancellationToken cancellationToken);
    object? GetService(Type serviceType) => null;
}

public enum MessageAction { Acknowledge, Reject, Requeue }
```

The handler callback returns a `MessageAction` telling the transport what to do with the message. `DelegatingTransportSubscriber` provides the decorator base class.

| Interface | Pattern | Use When |
|-----------|---------|----------|
| `ITransportReceiver` | Pull | You control the polling loop, batch receive |
| `ITransportSubscriber` | Push | Transport drives delivery, handler reacts |

All 5 transports implement `ITransportSubscriber`:

| Transport | Push Model | SDK API |
|-----------|------------|---------|
| Azure Service Bus | Native push | `ServiceBusProcessor` events |
| RabbitMQ | Native push | `AsyncEventingBasicConsumer` + `BasicConsumeAsync` |
| Google Pub/Sub | Native push | `SubscriberClient.StartAsync()` streaming pull |
| Kafka | Continuous poll | `IConsumer<string,byte[]>.Consume()` in loop |
| AWS SQS | Long poll | `ReceiveMessageAsync` with 20s wait |

**MessageAction settlement** maps to transport-native operations:

| MessageAction | Azure SB | RabbitMQ | Kafka | AWS SQS | Google Pub/Sub |
|---------------|----------|----------|-------|---------|----------------|
| `Acknowledge` | Complete | BasicAck | Commit offset | Delete | Ack |
| `Reject` | Dead-letter | Nack (no requeue) | Commit (DLQ via decorator) | Delete (DLQ via redrive) | Nack |
| `Requeue` | Abandon | Nack (requeue) | Seek back | Visibility timeout = 0 | Nack |

**GetService** exposes the underlying subscriber client:

| Transport | Subscriber Returns |
|-----------|-------------------|
| Azure Service Bus | `ServiceBusProcessor` |
| RabbitMQ | `IChannel` |
| Kafka | `IConsumer<string, byte[]>` |
| AWS SQS | `IAmazonSQS` |
| Google Pub/Sub | `SubscriberClient` |

### TransportMessage (Slim)

`TransportMessage` is a slim message type (9 properties). Transport-specific hints flow via the `Properties` dictionary with well-known keys:

```csharp
var message = new TransportMessage
{
    Body = Encoding.UTF8.GetBytes(payload),
    ContentType = "application/json",
    MessageType = "OrderCreated",
    CorrelationId = correlationId,
};

// Transport hints via Properties dictionary
message.Properties[TransportTelemetryConstants.PropertyKeys.OrderingKey] = orderId;
message.Properties[TransportTelemetryConstants.PropertyKeys.PartitionKey] = customerId;
```

### Decorator Pattern

Cross-cutting concerns (telemetry, ordering, deduplication, scheduling, CloudEvents, DLQ routing) are composable decorators built on `DelegatingTransportSender` / `DelegatingTransportReceiver`:

```csharp
var sender = new TransportSenderBuilder(nativeSender)
    .Use(inner => new TelemetryTransportSender(inner, meter, activitySource, "Kafka"))
    .Use(inner => new OrderingTransportSender(inner, msg => msg.Subject))
    .Build();
```

| Decorator | Direction | Purpose |
|-----------|-----------|---------|
| `TelemetryTransportSender` | Send | OpenTelemetry metrics + traces |
| `TelemetryTransportReceiver` | Receive | OpenTelemetry metrics + traces |
| `OrderingTransportSender` | Send | Set ordering key from message |
| `DeduplicationTransportSender` | Send | Set deduplication ID |
| `SchedulingTransportSender` | Send | Scheduled delivery time |
| `CloudEventsTransportSender` | Send | CloudEvents envelope |
| `CloudEventsTransportReceiver` | Receive | CloudEvents unwrapping |
| `DeadLetterTransportReceiver` | Receive | Route failures to DLQ |

### GetService() — Raw SDK Access

Access the underlying transport SDK client for advanced scenarios:

```csharp
// Kafka: get the native IProducer
var producer = sender.GetService(typeof(IProducer<string, byte[]>))
    as IProducer<string, byte[]>;

// Azure Service Bus: get the native ServiceBusSender
var sbSender = sender.GetService(typeof(ServiceBusSender))
    as ServiceBusSender;
```

| Transport | Sender Returns | Receiver Returns | Subscriber Returns |
|-----------|---------------|-----------------|-------------------|
| Kafka | `IProducer<string, byte[]>` | `IConsumer<string, byte[]>` | `IConsumer<string, byte[]>` |
| RabbitMQ | `IChannel` | `IChannel` | `IChannel` |
| Azure Service Bus | `ServiceBusSender` | `ServiceBusReceiver` | `ServiceBusProcessor` |
| AWS SQS | `IAmazonSQS` | `IAmazonSQS` | `IAmazonSQS` |
| Google Pub/Sub | `PublisherServiceApiClient` | `SubscriberServiceApiClient` | `SubscriberClient` |

## Transport Selection Guide

| Requirement | Recommended Transport |
|-------------|----------------------|
| High throughput (>100k msg/sec) | Kafka |
| Complex routing patterns | RabbitMQ |
| Azure-native integration | Azure Service Bus |
| AWS-native integration | AWS SQS |
| GCP-native integration | Google Pub/Sub |
| Scheduled jobs / cron tasks | Cron Timer |
| Local development/testing | In-Memory |

## Common Configuration

### Connection Resilience

Configure resilience per transport via the options classes:

```csharp
services.Configure<RabbitMqOptions>(options =>
{
    options.AutomaticRecoveryEnabled = true;
    options.NetworkRecoveryIntervalSeconds = 10;
});
```

### Health Checks

Register health checks for monitoring transport adapters:

```csharp
services.AddHealthChecks()
    .AddTransportHealthChecks();

app.MapHealthChecks("/health");
```

### Observability

All transports emit OpenTelemetry traces and metrics via the `TelemetryTransportSender` / `TelemetryTransportReceiver` decorators:

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder.AddSource("Excalibur.Dispatch.Observability");
        // Transport-specific traces: Excalibur.Dispatch.Transport.{Name}
        builder.AddSource("Excalibur.Dispatch.Transport.Kafka");
        builder.AddSource("Excalibur.Dispatch.Transport.RabbitMQ");
    })
    .WithMetrics(builder =>
    {
        builder.AddDispatchMetrics();
    });
```

Standard transport metric names follow `dispatch.transport.*` convention:

| Metric | Type | Description |
|--------|------|-------------|
| `dispatch.transport.messages.sent` | Counter | Messages sent successfully |
| `dispatch.transport.messages.send_failed` | Counter | Send failures |
| `dispatch.transport.messages.received` | Counter | Messages received |
| `dispatch.transport.messages.acknowledged` | Counter | Messages acknowledged |
| `dispatch.transport.messages.rejected` | Counter | Messages rejected |
| `dispatch.transport.send.duration` | Histogram | Send operation duration (ms) |
| `dispatch.transport.receive.duration` | Histogram | Receive operation duration (ms) |
| `dispatch.transport.batch.size` | Histogram | Batch operation message count |

## Message Serialization

By default, messages are serialized using MemoryPack. You can configure different serializers:

```csharp
// Use System.Text.Json for cross-language compatibility
services.AddJsonSerialization();

// Or MessagePack for compact binary format
services.AddMessagePackSerialization();
```

| Serializer | Package | Best For |
|------------|---------|----------|
| MemoryPack (default) | Built-in | .NET-only, maximum performance |
| System.Text.Json | Built-in | Cross-language, debugging |
| MessagePack | `Excalibur.Dispatch.Serialization.MessagePack` | Cross-language, compact |
| Protobuf | `Excalibur.Dispatch.Serialization.Protobuf` | Schema-based, cross-language |

## Dead Letter Queue Support

Each transport can implement `IDeadLetterQueueManager` from `Excalibur.Dispatch.Transport.Abstractions` for transport-native dead letter handling:

| Transport | DLQ Support | Mechanism | Registration |
|-----------|:-----------:|-----------|--------------|
| Google Pub/Sub | Yes | Subscription-based | Built-in |
| AWS SQS | Yes | Queue-based (native redrive) | Built-in |
| Kafka | Yes | Topic-based (`{topic}.dead-letter`) | `services.AddKafkaDeadLetterQueue()` |
| Azure Service Bus | Yes | Native `$DeadLetterQueue` subqueue | `services.AddServiceBusDeadLetterQueue()` |
| RabbitMQ | Yes | Dead letter exchange (DLX) | `services.AddRabbitMqDeadLetterQueue()` |

### IDeadLetterQueueManager Interface

All transport DLQ implementations share the same base interface:

```csharp
public interface IDeadLetterQueueManager
{
    Task<string> MoveToDeadLetterAsync(
        TransportMessage message, string reason,
        Exception? exception,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
        int maxMessages,
        CancellationToken cancellationToken);

    Task<ReprocessResult> ReprocessDeadLetterMessagesAsync(
        IEnumerable<DeadLetterMessage> messages,
        ReprocessOptions options,
        CancellationToken cancellationToken);

    Task<DeadLetterStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken);

    Task<int> PurgeDeadLetterQueueAsync(
        CancellationToken cancellationToken);
}
```

### Kafka DLQ Example

```csharp
services.AddKafkaTransport("events", kafka => { /* ... */ });

services.AddKafkaDeadLetterQueue(dlq =>
{
    dlq.TopicSuffix = ".dead-letter";       // Default
    dlq.ConsumerGroupId = "dlq-processor";  // Default
    dlq.MaxDeliveryAttempts = 5;            // Default
    dlq.MessageRetentionPeriod = TimeSpan.FromDays(14);
    dlq.AutoCreateTopics = true;
});
```

### AWS SQS DLQ

AWS SQS DLQ support is built into the `DlqProcessor` class, which implements both `IDlqManager` (SQS-specific) and `IDeadLetterQueueManager` (transport-agnostic). Configure through `DlqOptions`:

```csharp
services.Configure<DlqOptions>(options =>
{
    options.DeadLetterQueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/...");
});
```

### Azure Service Bus DLQ

Azure Service Bus uses the native `$DeadLetterQueue` subqueue. The manager accesses it via `ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter }` — no admin API package required.

```csharp
services.AddServiceBusDeadLetterQueue(dlq =>
{
    dlq.EntityPath = "orders";           // Queue or subscription path
    dlq.MaxBatchSize = 100;              // Batch size for purge/retrieve (default: 100)
    dlq.ReceiveWaitTime = TimeSpan.FromSeconds(5);  // Wait time (default: 5s)
    dlq.StatisticsPeekCount = 1000;      // Max messages to peek for stats (default: 1000)
    dlq.IncludeStackTrace = true;        // Include stack traces (default: true)
});
```

### RabbitMQ DLQ

RabbitMQ uses dead letter exchanges (DLX). Messages are published to the DLX exchange with `dlq_reason` and `dlq_original_source` headers. Peek semantics use `BasicGet(autoAck: false)` + `Nack(requeue: true)`.

```csharp
services.AddRabbitMqDeadLetterQueue(dlq =>
{
    dlq.Exchange = "dead-letters";         // DLX exchange name (default: "dead-letters")
    dlq.QueueName = "dead-letter-queue";   // DLQ queue name (default: "dead-letter-queue")
    dlq.RoutingKey = "#";                  // Routing key (default: "#")
    dlq.IncludeStackTrace = true;          // Include stack traces (default: true)
    dlq.MaxBatchSize = 100;               // Batch size for stats (default: 100)
});
```

For more details, see [Dead Letter Handling](../patterns/dead-letter.md).

### Poison Message Handling

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddPoisonMessageHandling(options =>
    {
        options.MaxRetryAttempts = 5;
        options.EnableAlerting = true;
        options.AlertThreshold = 10;
    });
});
```

## In This Section

- [In-Memory](in-memory.md) — Built-in transport for testing
- [Cron Timer](cron-timer.md) — Scheduled jobs and recurring tasks
- [Kafka](kafka.md) — Apache Kafka transport
- [RabbitMQ](rabbitmq.md) — RabbitMQ transport
- [Azure Service Bus](azure-service-bus.md) — Azure Service Bus transport
- [AWS SQS](aws-sqs.md) — AWS SQS transport
- [Google Pub/Sub](google-pubsub.md) — Google Pub/Sub transport
- [Multi-Transport Routing](multi-transport.md) — Route to multiple transports

## See Also

- [Patterns](../patterns/index.md) - Outbox, inbox, and dead-letter patterns for reliable messaging
- [Middleware](../middleware/index.md) - Transport-aware middleware components
- [Deployment](../deployment/index.md) - Deploy transport-backed applications
