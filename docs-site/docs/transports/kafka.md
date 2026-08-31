---
sidebar_position: 3
title: Kafka Transport
description: Apache Kafka transport for high-throughput event streaming
---

# Kafka Transport
Apache Kafka transport for high-throughput event streaming with configurable ordering and delivery guarantees.

## Before You Start

- **.NET 10.0**
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
             .RequireTls(false) // local broker has no TLS listener -- see Security below
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
    kafka.BootstrapServers("broker1:9093,broker2:9093")
         .UseSecurityProtocol(SecurityProtocol.SaslSsl)
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

### Security

The transport refuses to build any Kafka client -- producer, consumer, admin, or dead-letter client --
whose security protocol would carry credentials and message payloads in the clear. This is the default:
a deployment that configures nothing does not silently connect in plaintext, it fails where it is wired.

```csharp
services.AddKafkaTransport(kafka =>
{
    kafka.BootstrapServers("broker:9093")
         .UseSecurityProtocol(SecurityProtocol.SaslSsl);
});
```

| Setting | Default | Effect |
|---------|---------|--------|
| `UseSecurityProtocol(protocol)` | unset | The protocol every client for this transport connects with. |
| `RequireTls(bool)` | `true` | When set, a protocol that does not carry TLS is refused at client construction. |

`SecurityProtocol.Ssl` and `SecurityProtocol.SaslSsl` carry TLS; `Plaintext` and `SaslPlaintext` do not.
An unset protocol is plaintext at the wire and is treated as such.

#### Connecting to a broker without TLS

A local broker or a test container usually has no TLS listener. Turn the requirement off explicitly:

```csharp
kafka.BootstrapServers("localhost:9092").RequireTls(false);
```

:::warning
`RequireTls(false)` permits credentials and message payloads to travel in the clear. Use it for local
brokers and test fixtures, never for anything holding real data.
:::

#### Setting the protocol twice

The protocol has two spellings: `UseSecurityProtocol(...)` and the raw `security.protocol` key in
`AdditionalConfig`. Setting both to different values is **refused**, not resolved -- neither wins,
because a silent winner between two spellings of a security control is how an intended TLS posture
becomes a plaintext connection. Set one or the other. An unrecognized raw value is refused for the
same reason.

Credentials and certificate paths continue to travel through the raw configuration keys
(`sasl.username`, `sasl.password`, `ssl.ca.location`, ...):

```csharp
kafka.BootstrapServers("broker:9093")
     .UseSecurityProtocol(SecurityProtocol.SaslSsl)
     .ConfigureProducer(producer => producer
         .WithConfig("sasl.mechanism", "PLAIN")
         .WithConfig("sasl.username", username)
         .WithConfig("sasl.password", password));
```

### Consumer Options
Configure the underlying Kafka client via `KafkaOptions`:

```csharp
services.Configure<KafkaOptions>(options =>
{
    options.BootstrapServers = "broker1:9092,broker2:9092";
    options.ConsumerGroup = "order-service";
    options.Topic = "dispatch.events";

    options.Consumer.EnableAutoCommit = false;
    options.Consumer.AutoCommitIntervalMs = 5000;
    options.Consumer.SessionTimeoutMs = 30000;
    options.Consumer.MaxPollIntervalMs = 300000;
    options.Consumer.AutoOffsetReset = "latest";

    options.AdditionalConfig["client.rack"] = "us-east-1";

    // Consumer tuning (batching, offset management, partition assignment)
    options.Consumer.PartitionAssignmentStrategy =
        Confluent.Kafka.PartitionAssignmentStrategy.CooperativeSticky;
});
```

#### Partition Assignment Strategy

The consumer's partition assignment strategy is configurable via `options.Consumer.PartitionAssignmentStrategy` (type `Confluent.Kafka.PartitionAssignmentStrategy?`). It defaults to `CooperativeSticky`, which performs incremental rebalances so partitions that are not being reassigned keep consuming during a rebalance. The consumer commits offsets for partitions as they are revoked, so a cooperative rebalance does not lose committed progress. Set the value to `null` to defer to the broker/client default. This setting is ignored when the KIP-848 `consumer` group protocol is used, where assignment is performed server-side.

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

:::note Trimming and Native AOT
The CloudEvents mapper bundled with this transport serializes the message payload with
reflection-based JSON, so these registrations carry `[RequiresUnreferencedCode]` and
`[RequiresDynamicCode]`. A host that trims or publishes ahead of time gets a warning at the
call. To compose without the requirement, register your own `ICloudEventMapper<TTransportMessage>`
backed by a source-generated serializer.
:::

```csharp
services.AddCloudEventsForKafka(options =>
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
- [ ] Set `UseSecurityProtocol()` to `Ssl` or `SaslSsl`, and leave `RequireTls` on

## Next Steps
- [RabbitMQ Transport](rabbitmq.md) -- Flexible routing patterns
- [Multi-Transport Routing](multi-transport.md) -- Combine Kafka with other transports

## See Also

- [Choosing a Transport](./choosing-a-transport.md) -- Compare Kafka against other transports to find the best fit
- [Message Mapping](./message-mapping.md) -- Configure how message types map to Kafka topics
- [Multi-Transport Routing](./multi-transport.md) -- Route different message types across Kafka and other transports
- [Metrics Reference](../observability/metrics-reference.md) -- Dispatch metrics for produce/consume throughput and latency
