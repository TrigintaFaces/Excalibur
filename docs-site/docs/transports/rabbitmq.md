---
sidebar_position: 4
title: RabbitMQ Transport
description: RabbitMQ transport for flexible routing and traditional messaging patterns
---

# RabbitMQ Transport
RabbitMQ transport for flexible message routing, work queues, and traditional pub/sub patterns.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A running RabbitMQ server (or Docker: `docker run -p 5672:5672 -p 15672:15672 rabbitmq:management`)
- Familiarity with [transport concepts](./index.md) and [choosing a transport](./choosing-a-transport.md)

## Installation
```bash
dotnet add package Excalibur.Dispatch.Transport.RabbitMQ
```

## Quick Start

### Using the Dispatch Builder (Recommended)
```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseRabbitMQ(rmq =>
    {
        rmq.ConnectionString("amqp://guest:guest@localhost:5672/")
           .ConfigureExchange(exchange => exchange.Name("dispatch.events").Type(RabbitMqExchangeType.Topic))
           .ConfigureCloudEvents(ce => ce.EnablePublisherConfirms = true);
    });
});
```

### Standalone Registration
```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

services.AddRabbitMQTransport(rmq =>
{
    rmq.ConnectionString("amqp://guest:guest@localhost:5672/")
       .ConfigureExchange(exchange => exchange.Name("dispatch.events").Type(RabbitMqExchangeType.Topic))
       .ConfigureCloudEvents(ce => ce.EnablePublisherConfirms = true);
});
```

RabbitMQ registers a keyed `IMessageBus` named `rabbitmq`:
```csharp
var bus = serviceProvider.GetRequiredKeyedService<IMessageBus>("rabbitmq");
```

## Configuration

### Fluent Builder Configuration
Configure RabbitMQ transport using the fluent builder:

```csharp
services.AddRabbitMQTransport(rmq =>
{
    rmq.ConnectionString("amqp://user:pass@rabbitmq:5672/vhost")
       .ConfigureExchange(exchange =>
       {
           exchange.Name("dispatch.events")
                   .Type(RabbitMqExchangeType.Topic)
                   .Durable(true)
                   .AutoDelete(false);
       })
       .ConfigureQueue(queue =>
       {
           queue.Name("order-handlers")
                .Durable(true)
                .PrefetchCount(100);
       })
       .ConfigureBinding(binding =>
       {
           binding.Exchange("dispatch.events")
                  .Queue("order-handlers")
                  .RoutingKey("orders.*");
       })
       .ConfigureCloudEvents(ce =>
       {
           ce.ExchangeType = RabbitMqExchangeType.Topic;
           ce.Persistence = RabbitMqPersistence.Persistent;
           ce.RoutingStrategy = RabbitMqRoutingStrategy.EventType;
       });
});
```

### Broker Options
Configure low-level broker behavior via `RabbitMqOptions`:

```csharp
services.Configure<RabbitMqOptions>(options =>
{
    options.ConnectionString = "amqp://user:pass@rabbitmq:5672/vhost";
    options.Exchange = "dispatch.events";
    options.RoutingKey = "orders.#";
    options.QueueName = "orders-processing";

    // Queue behavior
    options.QueueDurable = true;
    options.QueueExclusive = false;
    options.QueueAutoDelete = false;
    options.QueueArguments["x-message-ttl"] = 86400000; // 24 hours

    // Consumer behavior
    options.PrefetchCount = 100;
    options.PrefetchGlobal = false;
    options.AutoAck = false;
    options.RequeueOnReject = false;
    options.MaxBatchSize = 50;
    options.MaxBatchWaitMs = 500;
    options.ConsumerTag = "order-service";

    // Dead letter exchange (non-CloudEvents)
    options.EnableDeadLetterExchange = true;
    options.DeadLetterExchange = "dispatch.dlx";
    options.DeadLetterRoutingKey = "failed";

    // Connection resilience
    options.AutomaticRecoveryEnabled = true;
    options.ConnectionTimeoutSeconds = 30;
    options.NetworkRecoveryIntervalSeconds = 10;
});
```

### CloudEvents Options
Use `RabbitMqCloudEventOptions` for CloudEvents-specific features:

```csharp
services.UseCloudEventsForRabbitMq(options =>
{
    options.ExchangeType = RabbitMqExchangeType.Topic;
    options.RoutingStrategy = RabbitMqRoutingStrategy.EventType;
    options.Persistence = RabbitMqPersistence.Persistent;

    // Quorum queues + delivery guarantees
    options.UseQuorumQueues = true;
    options.EnablePublisherConfirms = true;
    options.MandatoryPublishing = true;

    // CloudEvents dead-letter + retry
    options.EnableDeadLetterExchange = true;
    options.DeadLetterExchange = "cloudevents.dlx";
    options.MaxRetryAttempts = 3;
    options.RetryDelay = TimeSpan.FromSeconds(30);
});
```

## Consumer Handling Options

Configure advanced consumer behavior with `RabbitMqConsumerOptions` via CloudEvents options:

```csharp
services.UseCloudEventsForRabbitMq(options =>
{
    // Acknowledgment mode
    options.Consumer.AckMode = AckMode.Manual; // Auto, Manual, or Batch

    // Retry policy for failed messages
    options.Consumer.RetryPolicy = RetryPolicy.Exponential(
        maxRetries: 3,
        initialDelay: TimeSpan.FromSeconds(1),
        maxDelay: TimeSpan.FromMinutes(5));

    // Dead letter exchange for failed messages
    options.Consumer.DeadLetterExchange = "dlx.exchange";
    options.Consumer.DeadLetterRoutingKey = "failed";
});
```

### AckMode Options

| Mode | Description | Use Case |
|------|-------------|----------|
| `Auto` | Automatic acknowledgment on receive | Non-critical, fire-and-forget |
| `Manual` | Explicit ack after processing (default) | Guaranteed delivery |
| `Batch` | Grouped acknowledgments | High throughput scenarios |

### RetryPolicy Factory Methods

```csharp
// No retry - fail immediately
RetryPolicy.None()

// Fixed delay between retries
RetryPolicy.Fixed(maxRetries: 3, delay: TimeSpan.FromSeconds(5))

// Exponential backoff with jitter
RetryPolicy.Exponential(
    maxRetries: 5,
    initialDelay: TimeSpan.FromSeconds(1),
    maxDelay: TimeSpan.FromMinutes(5))
```

## Publisher Confirms

Enable publisher confirms for guaranteed delivery:

```csharp
services.UseCloudEventsForRabbitMq(options =>
{
    options.Publisher.EnableConfirms = true;
    options.Publisher.ConfirmTimeout = TimeSpan.FromSeconds(5);
    options.Publisher.MandatoryPublishing = true;
});
```

## Acknowledgment Behavior (Legacy)

For non-CloudEvents usage, configure via `RabbitMqOptions`:

```csharp
services.Configure<RabbitMqOptions>(options =>
{
    options.AutoAck = false;      // Manual ack after successful processing
    options.RequeueOnReject = false; // Reject goes to DLQ if enabled
});
```

## Health Checks
When using transport adapters, register aggregate health checks (for message bus-only usage, implement a custom check around the RabbitMQ client):

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
        // Traces: publish, consume, ack, reject
    })
    .WithMetrics(metrics =>
    {
        metrics.AddDispatchMetrics();
        // Metrics: message rates, consumer lag
    });
```

## Production Checklist
- [ ] Use durable queues and exchanges
- [ ] Enable publisher confirms for critical messages
- [ ] Configure dead letter exchange and retry policy
- [ ] Set prefetch count based on handler throughput
- [ ] Enable automatic recovery for transient network failures
- [ ] Use TLS (`amqps://`) in production

## Next Steps
- [Kafka Transport](kafka.md) — High-throughput streaming
- [Multi-Transport Routing](multi-transport.md) — Combine RabbitMQ with other transports

## See Also

- [Choosing a Transport](./choosing-a-transport.md) — Compare RabbitMQ against other transports to find the best fit
- [Message Mapping](./message-mapping.md) — Configure how message types map to exchanges and queues
- [Dead Letter Handling](../patterns/dead-letter.md) — Strategies for managing failed messages with DLX
- [Multi-Transport Routing](./multi-transport.md) — Route different message types across RabbitMQ and other transports
