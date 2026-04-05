---
sidebar_position: 3
title: Kafka Transport
description: Apache Kafka transport for high-throughput event streaming
---

# Kafka Transport
Apache Kafka transport for high-throughput event streaming with configurable ordering and delivery guarantees.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- A running Apache Kafka cluster (or Docker: `docker run -p 9092:9092 confluentinc/cp-kafka`)
- Familiarity with [transport concepts](./index.md) and [choosing a transport](./choosing-a-transport.md)

## Installation
```bash
dotnet add package Excalibur.Dispatch.Transport.Kafka
```

:::tip One-Line Setup with Metapackage
For the fastest setup, use the **`Excalibur.Dispatch.Kafka`** experience metapackage. It bundles the Kafka transport with Polly resilience and OpenTelemetry observability in a single call:

```bash
dotnet add package Excalibur.Dispatch.Kafka
```

```csharp
services.AddDispatchKafka(kafka =>
{
    kafka.BootstrapServers("localhost:9092")
         .ConfigureConsumer(c => c.GroupId("order-service"));
});
```

`AddDispatchKafka` calls `AddDispatch` internally and configures `UseKafka`, `UseResilience`, and `UseObservability`. Pass an optional second parameter (`Action<IDispatchBuilder>`) for additional pipeline configuration. See [Package Guide](../package-guide.md#experience-metapackages) for details.

Note: The Kafka transport uses `[RequiresUnreferencedCode]` and `[RequiresDynamicCode]` attributes due to schema registry serialization requirements.
:::

## Quick Start

### Using the Dispatch Builder (Recommended)
```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseKafka(kafka =>
    {
        kafka.BootstrapServers("localhost:9092")
             .ConfigureConsumer(consumer => consumer.GroupId("order-service"))
             .MapTopic<OrderCreatedEvent>("dispatch.events");
    });
});
```

### Standalone Registration
```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

services.AddKafkaTransport(kafka =>
{
    kafka.BootstrapServers("localhost:9092")
         .ConfigureConsumer(consumer => consumer.GroupId("order-service"))
         .MapTopic<OrderCreatedEvent>("dispatch.events");
});
```

Kafka registers a keyed `IMessageBus` named `kafka`:
```csharp
var bus = serviceProvider.GetRequiredKeyedService<IMessageBus>("kafka");
```

## Configuration

### Fluent Builder Configuration

:::tip Start simple
For most applications, the Quick Start above is all you need. The fluent builder below is for production tuning (acknowledgment levels, compression, partitioning strategy).
:::

Configure producer, consumer, CloudEvents, and topic settings using the fluent builder:

```csharp
services.AddKafkaTransport(kafka =>
{
    kafka.BootstrapServers("broker1:9092,broker2:9092")
         .ConfigureProducer(producer =>
         {
             producer.ClientId("dispatch-producer")
                     .Acks(KafkaAckLevel.All)
                     .CompressionType(KafkaCompressionType.Snappy)
                     .EnableIdempotence(true);
         })
         .ConfigureConsumer(consumer =>
         {
             consumer.GroupId("order-service")
                     .AutoOffsetReset(KafkaOffsetReset.Latest);
         })
         .ConfigureCloudEvents(ce =>
         {
             ce.PartitioningStrategy = KafkaPartitioningStrategy.CorrelationId;
             ce.AcknowledgmentLevel = KafkaAckLevel.All;
             ce.EnableIdempotentProducer = true;
         })
         .MapTopic<OrderCreatedEvent>("dispatch.events");
});
```

### Consumer Options
Configure the underlying Kafka client via `KafkaOptions`:

```csharp
services.Configure<KafkaOptions>(options =>
{
    options.BootstrapServers = "broker1:9092,broker2:9092";
    options.ConsumerGroup = "order-service";
    options.Topic = "dispatch.events";

    options.EnableAutoCommit = false;
    options.AutoCommitIntervalMs = 5000;
    options.SessionTimeoutMs = 30000;
    options.MaxPollIntervalMs = 300000;
    options.AutoOffsetReset = "latest";

    options.AdditionalConfig["client.rack"] = "us-east-1";
});
```

### CloudEvents Options
Configure CloudEvents via `ConfigureCloudEvents()` on the transport builder for delivery guarantees, partitioning, and topic creation:

```csharp
services.AddKafkaTransport(kafka =>
{
    kafka.BootstrapServers("localhost:9092")
         .ConfigureCloudEvents(ce =>
         {
             ce.DefaultTopic = "dispatch.events";
             ce.PartitioningStrategy = KafkaPartitioningStrategy.CorrelationId;
             ce.AcknowledgmentLevel = KafkaAckLevel.All;
             ce.EnableIdempotentProducer = true;
             ce.EnableTransactions = true;
             ce.TransactionalId = "dispatch-orders";
             ce.AutoCreateTopics = true;
             ce.DefaultPartitionCount = 3;
             ce.DefaultReplicationFactor = 2;
         });
});
```

Alternatively, use the standalone extension method:

```csharp
services.UseCloudEventsForKafka(options =>
{
    options.PartitioningStrategy = KafkaPartitioningStrategy.CorrelationId;
    options.AcknowledgmentLevel = KafkaAckLevel.All;
});
```

### Transactions (Exactly-Once)
Enable transactional publishing for exactly-once semantics:

```csharp
services.AddKafkaTransport(kafka =>
{
    kafka.BootstrapServers("localhost:9092")
         .ConfigureProducer(producer =>
         {
             producer.Acks(KafkaAckLevel.All)
                     .EnableIdempotence(true)
                     .EnableTransactions("orders-producer");
         });
});
```

## Topic Resolution
Dispatch uses `KafkaOptions.Topic` when set; otherwise it falls back to
`KafkaCloudEventOptions.DefaultTopic`. Set one of them explicitly to avoid runtime
errors.

## Health Checks
When using transport adapters, register aggregate health checks (for message bus-only usage, implement a custom check around the Kafka client):

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
        // Spans for produce/consume operations
    })
    .WithMetrics(metrics =>
    {
        metrics.AddDispatchMetrics();
        // Metrics for produced/consumed counts and latency
    });
```

## Confluent Schema Registry Integration

Add schema validation, evolution, and Confluent wire format interoperability with `UseConfluentSchemaRegistry()`:

```csharp
services.AddKafkaTransport("events", kafka =>
{
    kafka.BootstrapServers("localhost:9092")
         .UseConfluentSchemaRegistry(registry =>
         {
             registry.SchemaRegistryUrl("http://localhost:8081")
                     .AutoRegisterSchemas(true)
                     .CacheSchemas(true);
         })
         .MapTopic<OrderCreated>("orders-topic");
});
```

Users who don't call `UseConfluentSchemaRegistry()` are unaffected -- standard JSON serialization is used.

See **[Kafka Schema Registry](./kafka-schema-registry.md)** for the full reference: builder API, subject naming strategies, compatibility modes, SSL/mTLS auth, wire format details, producer/consumer flows, and error handling.

## Production Checklist
- [ ] Set `KafkaCloudEventOptions.AcknowledgmentLevel` to `All` for durability
- [ ] Enable idempotent producer for exactly-once workflows
- [ ] Configure `TransactionalId` when `EnableTransactions` is true
- [ ] Set a default topic and partitioning strategy
- [ ] Enable compression for throughput/size balance
- [ ] Configure Schema Registry with `UseConfluentSchemaRegistry()` for Confluent interop
- [ ] Set appropriate `CompatibilityMode` for schema evolution
- [ ] Disable `AutoRegisterSchemas` in production (explicit schema management)
- [ ] Configure SSL/TLS for Schema Registry in production environments

## Next Steps
- [RabbitMQ Transport](rabbitmq.md) -- Flexible routing patterns
- [Multi-Transport Routing](multi-transport.md) -- Combine Kafka with other transports

## See Also

- [Choosing a Transport](./choosing-a-transport.md) -- Compare Kafka against other transports to find the best fit
- [Message Mapping](./message-mapping.md) -- Configure how message types map to Kafka topics
- [Multi-Transport Routing](./multi-transport.md) -- Route different message types across Kafka and other transports
- [Metrics Reference](../observability/metrics-reference.md) -- Dispatch metrics for produce/consume throughput and latency
