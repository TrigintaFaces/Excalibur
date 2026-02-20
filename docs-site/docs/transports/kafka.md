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
        tracing.AddSource("Excalibur.Dispatch.Observability");
        // Spans for produce/consume operations
    })
    .WithMetrics(metrics =>
    {
        metrics.AddDispatchMetrics();
        // Metrics for produced/consumed counts and latency
    });
```

## Confluent Schema Registry Integration

The module provides fluent builder APIs for Confluent Schema Registry configuration. The `UseConfluentSchemaRegistry()` extension enables interoperability with Kafka systems using the standard Confluent wire format.

### Quick Start

```csharp
services.AddKafkaTransport("events", kafka =>
{
    kafka.BootstrapServers("localhost:9092")
         .UseConfluentSchemaRegistry(registry =>
         {
             registry.SchemaRegistryUrl("http://localhost:8081")
                     .SubjectNameStrategy(SubjectNameStrategy.TopicName)
                     .CompatibilityMode(CompatibilityMode.Backward)
                     .AutoRegisterSchemas(true)
                     .CacheSchemas(true);
         })
         .MapTopic<OrderCreated>("orders-topic");
});
```

### Builder Interfaces

#### IConfluentSchemaRegistryBuilder

The main builder interface provides Schema Registry configuration:

```csharp
public interface IConfluentSchemaRegistryBuilder
{
    // Connection
    IConfluentSchemaRegistryBuilder SchemaRegistryUrl(string url);
    IConfluentSchemaRegistryBuilder SchemaRegistryUrls(params string[] urls);

    // Authentication
    IConfluentSchemaRegistryBuilder BasicAuth(string username, string password);
    IConfluentSchemaRegistryBuilder ConfigureSsl(Action<ISchemaRegistrySslBuilder> configure);

    // Schema Behavior
    IConfluentSchemaRegistryBuilder SubjectNameStrategy(SubjectNameStrategy strategy);
    IConfluentSchemaRegistryBuilder SubjectNameStrategy<TStrategy>()
        where TStrategy : class, ISubjectNameStrategy, new();
    IConfluentSchemaRegistryBuilder CompatibilityMode(CompatibilityMode mode);
    IConfluentSchemaRegistryBuilder AutoRegisterSchemas(bool enable = true);
    IConfluentSchemaRegistryBuilder ValidateBeforeRegister(bool enable = true);

    // Caching
    IConfluentSchemaRegistryBuilder CacheSchemas(bool enable = true);
    IConfluentSchemaRegistryBuilder CacheCapacity(int capacity);

    // Timeouts
    IConfluentSchemaRegistryBuilder RequestTimeout(TimeSpan timeout);
}
```

| Method | Default | Description |
|--------|---------|-------------|
| `SchemaRegistryUrl()` | Required | Schema Registry URL |
| `SchemaRegistryUrls()` | - | Multiple URLs for high availability |
| `BasicAuth()` | None | HTTP Basic authentication |
| `ConfigureSsl()` | None | SSL/TLS configuration |
| `SubjectNameStrategy()` | TopicName | Subject naming strategy |
| `CompatibilityMode()` | Backward | Schema compatibility mode |
| `AutoRegisterSchemas()` | true | Auto-register schemas on first use |
| `ValidateBeforeRegister()` | true | Validate schemas locally first |
| `CacheSchemas()` | true | Enable local schema caching |
| `CacheCapacity()` | 1000 | Maximum cached schemas |
| `RequestTimeout()` | 30 seconds | HTTP request timeout |

#### ISchemaRegistrySslBuilder

Configures SSL/TLS for secure Schema Registry connections:

```csharp
public interface ISchemaRegistrySslBuilder
{
    ISchemaRegistrySslBuilder EnableCertificateVerification(bool enable = true);
    ISchemaRegistrySslBuilder CaCertificateLocation(string path);
    ISchemaRegistrySslBuilder ClientCertificateLocation(string path);
    ISchemaRegistrySslBuilder ClientKeyLocation(string path);
    ISchemaRegistrySslBuilder ClientKeyPassword(string password);
}
```

### Subject Naming Strategies

The subject name uniquely identifies a schema in the Schema Registry:

```csharp
public enum SubjectNameStrategy
{
    /// <summary>Subject: {topic}-value (default)</summary>
    TopicName,

    /// <summary>Subject: {namespace}.{type}</summary>
    RecordName,

    /// <summary>Subject: {topic}-{namespace}.{type}</summary>
    TopicRecordName
}
```

| Strategy | Subject Format | Use Case |
|----------|----------------|----------|
| `TopicName` | `{topic}-value` | Single schema per topic (default Confluent behavior) |
| `RecordName` | `{namespace}.{type}` | Multiple schemas per topic, type-based lookup |
| `TopicRecordName` | `{topic}-{namespace}.{type}` | Maximum flexibility, different schemas per topic+type |

For custom strategies, implement `ISubjectNameStrategy`:

```csharp
registry.SubjectNameStrategy<MyCustomStrategy>();
```

### Compatibility Modes

Schema Registry enforces compatibility when evolving schemas:

```csharp
public enum CompatibilityMode
{
    None,              // No compatibility checking
    Backward,          // New schema can read old data (default)
    BackwardTransitive, // New schema can read all previous versions
    Forward,           // Old schema can read new data
    ForwardTransitive,  // All old schemas can read new data
    Full,              // Both backward and forward compatible
    FullTransitive     // Full compatibility with all versions
}
```

| Mode | Direction | Description |
|------|-----------|-------------|
| `None` | - | No compatibility checking |
| `Backward` | Consumer | New consumers can read old producer data |
| `Forward` | Producer | Old consumers can read new producer data |
| `Full` | Both | Consumers and producers can be upgraded independently |
| `*Transitive` | All versions | Checks against all registered versions |

### Configuration Examples

#### Basic Configuration

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

#### High Availability with Multiple URLs

```csharp
registry.SchemaRegistryUrls(
    "http://registry1.example.com:8081",
    "http://registry2.example.com:8081",
    "http://registry3.example.com:8081"
);
```

#### Production with Authentication

```csharp
registry.SchemaRegistryUrl("https://registry.example.com:8085")
        .BasicAuth("api-key", "api-secret")
        .ConfigureSsl(ssl =>
        {
            ssl.EnableCertificateVerification(true)
               .CaCertificateLocation("/path/to/ca.crt");
        })
        .AutoRegisterSchemas(false)  // Disable in production
        .CompatibilityMode(CompatibilityMode.Full);
```

#### Mutual TLS Authentication

```csharp
registry.SchemaRegistryUrl("https://registry.example.com:8085")
        .ConfigureSsl(ssl =>
        {
            ssl.EnableCertificateVerification(true)
               .CaCertificateLocation("/path/to/ca.crt")
               .ClientCertificateLocation("/path/to/client.crt")
               .ClientKeyLocation("/path/to/client.key")
               .ClientKeyPassword("secret");
        });
```

#### Record Name Strategy for Multi-Event Topics

```csharp
services.AddKafkaTransport("events", kafka =>
{
    kafka.BootstrapServers("localhost:9092")
         .UseConfluentSchemaRegistry(registry =>
         {
             registry.SchemaRegistryUrl("http://localhost:8081")
                     .SubjectNameStrategy(SubjectNameStrategy.RecordName)
                     .CompatibilityMode(CompatibilityMode.Backward);
         })
         .MapTopic<OrderCreated>("domain-events")
         .MapTopic<OrderShipped>("domain-events")
         .MapTopic<OrderCancelled>("domain-events");
});
```

### Confluent Wire Format

Messages produced with Schema Registry include a 5-byte header:

| Bytes | Content | Description |
|-------|---------|-------------|
| 0 | Magic byte | Always `0x00` |
| 1-4 | Schema ID | Big-endian int32 from Schema Registry |
| 5+ | Payload | Serialized message (JSON, Avro, Protobuf) |

The transport automatically:
- **Producer**: Prepends schema ID header when Schema Registry is configured
- **Consumer**: Detects magic byte and extracts schema ID for deserialization

### Design Principles

| Principle | Description |
|----------|-------------|
| Fluent builder pattern | Wraps existing `ConfluentSchemaRegistryOptions` for discoverability |
| Extensible strategies | `SubjectNameStrategy` enum for common cases; `ISubjectNameStrategy` for custom |

---

## Serialization Wiring

The fluent builder configuration is wired to the serialization pipeline, enabling end-to-end Schema Registry functionality.

### DI Service Registration

When `UseConfluentSchemaRegistry()` is configured, the following services are automatically registered:

```csharp
// Standalone registration (without transport builder)
services.AddConfluentSchemaRegistry(opts =>
{
    opts.Url = "http://localhost:8081";
    opts.AutoRegisterSchemas = true;
    opts.MaxCachedSchemas = 1000;
    opts.SubjectNameStrategy = SubjectNameStrategy.TopicName;
    opts.DefaultCompatibility = CompatibilityMode.Backward;
});

// With custom caching options
services.AddConfluentSchemaRegistry(
    opts => { opts.Url = "http://localhost:8081"; },
    cacheOpts => { cacheOpts.MaxCacheSize = 2000; });

// Without caching decorator (uses Confluent SDK internal caching only)
services.AddConfluentSchemaRegistryWithoutCaching(opts =>
{
    opts.Url = "http://localhost:8081";
});
```

### Registered Services

| Service | Implementation | Lifetime |
|---------|----------------|----------|
| `ConfluentSchemaRegistryOptions` | Configuration | Singleton |
| `ConfluentSchemaRegistryClient` | Underlying Confluent client | Singleton |
| `ISchemaRegistryClient` | `CachingSchemaRegistryClient` decorator | Singleton |
| `CachingSchemaRegistryOptions` | Cache configuration | Singleton |

### Producer Flow

When producing messages with Schema Registry:

```
Message<T> -> ConfluentJsonSerializer.SerializeAsync()
    |
    +-> JsonSchemaGenerator.Generate(T)     // Generate JSON Schema
    |
    +-> ISchemaRegistryClient              // Register/retrieve schema ID
    |   .GetSchemaIdAsync(subject, schema)
    |
    +-> ConfluentWireFormat.WriteHeader()  // Prepend 5-byte header
    |
    +-> [0x00][SchemaID:4bytes][JSON payload] -> Kafka
```

**Behavior:**
1. On first message of type `T`, generate JSON Schema from type metadata
2. Register schema with Registry (if `AutoRegisterSchemas` is true) or retrieve existing ID
3. Cache schema ID for subsequent messages of same type
4. Serialize message to JSON using `System.Text.Json`
5. Prepend 5-byte Confluent wire format header

### Consumer Flow

When consuming messages with Schema Registry:

```
Kafka -> [0x00][SchemaID:4bytes][JSON payload]
    |
    +-> ConfluentWireFormat.IsWireFormat()  // Detect magic byte
    |   (Falls back to raw JSON if not Confluent format)
    |
    +-> ConfluentWireFormat.ReadSchemaId()  // Extract schema ID
    |
    +-> ISchemaTypeResolver.ResolveTypeAsync()  // Resolve .NET type
    |
    +-> ConfluentWireFormat.GetPayload()    // Extract JSON payload
    |
    +-> JsonSerializer.Deserialize() -> Message<T>
```

**Behavior:**
1. Check for magic byte `0x00` (Confluent wire format indicator)
2. If not Confluent format, fall back to raw JSON deserialization
3. Extract 4-byte big-endian schema ID from bytes 1-4
4. Resolve .NET type from schema ID via `ISchemaTypeResolver`
5. Deserialize JSON payload to resolved type

### Backward Compatibility

**Critical:** Users who don't configure Schema Registry are unaffected:

```csharp
// This still works - no schema headers, raw JSON serialization
services.AddKafkaTransport("events", kafka =>
{
    kafka.BootstrapServers("localhost:9092")
         .MapTopic<OrderCreated>("orders-topic");
    // No UseConfluentSchemaRegistry() = standard JSON format
});
```

Schema Registry services are only registered when `UseConfluentSchemaRegistry()` is explicitly called.

### Error Handling

| Error | Event ID | Behavior |
|-------|----------|----------|
| Schema registration failure | 22216 | `SchemaRegistryException` thrown |
| Schema retrieval error | 22214 | `SchemaRegistryException` thrown |
| Compatibility check failure | 22219 | `SchemaRegistryException` thrown |
| Network timeout | 22404 | Retry via Polly (if configured) |
| Invalid wire format | 22403 | Fall back to raw JSON |
| Type resolution failure | 22403 | `SchemaRegistryException` thrown |
| Invalid JSON | - | `SchemaRegistryException` with inner `JsonException` |

### Design Principle

Conditional serializer registration ensures Schema Registry services are only registered when `UseConfluentSchemaRegistry()` is called.

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

- [Choosing a Transport](./choosing-a-transport.md) — Compare Kafka against other transports to find the best fit
- [Message Mapping](./message-mapping.md) — Configure how message types map to Kafka topics
- [Multi-Transport Routing](./multi-transport.md) — Route different message types across Kafka and other transports
- [Metrics Reference](../observability/metrics-reference.md) — Dispatch metrics for produce/consume throughput and latency
