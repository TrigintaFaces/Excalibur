---
sidebar_position: 12
title: Kafka Schema Registry
description: Confluent Schema Registry integration for Kafka transport -- schema validation, subject naming, compatibility modes, and wire format.
---

# Kafka Schema Registry

The Kafka transport includes full Confluent Schema Registry integration via the `UseConfluentSchemaRegistry()` fluent builder. This enables schema validation, evolution, and interoperability with other Confluent-ecosystem consumers.

## Before You Start

- Kafka transport configured (see [Kafka Transport](./kafka.md))
- A running Schema Registry instance (local or Confluent Cloud)

## Quick Start

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

## Builder API

### IConfluentSchemaRegistryBuilder

| Method | Default | Description |
|--------|---------|-------------|
| `SchemaRegistryUrl()` | Required | Schema Registry URL |
| `SchemaRegistryUrls()` | -- | Multiple URLs for high availability |
| `BasicAuth()` | None | HTTP Basic authentication |
| `ConfigureSsl()` | None | SSL/TLS configuration |
| `SubjectNameStrategy()` | TopicName | Subject naming strategy |
| `CompatibilityMode()` | Backward | Schema compatibility mode |
| `AutoRegisterSchemas()` | true | Auto-register schemas on first use |
| `ValidateBeforeRegister()` | true | Validate schemas locally first |
| `CacheSchemas()` | true | Enable local schema caching |
| `CacheCapacity()` | 1000 | Maximum cached schemas |
| `RequestTimeout()` | 30 seconds | HTTP request timeout |

### ISchemaRegistrySslBuilder

```csharp
registry.ConfigureSsl(ssl =>
{
    ssl.EnableCertificateVerification(true)
       .CaCertificateLocation("/path/to/ca.crt")
       .ClientCertificateLocation("/path/to/client.crt")
       .ClientKeyLocation("/path/to/client.key")
       .ClientKeyPassword("secret");
});
```

## Subject Naming Strategies

| Strategy | Subject Format | Use Case |
|----------|----------------|----------|
| `TopicName` | `{topic}-value` | Single schema per topic (default Confluent behavior) |
| `RecordName` | `{namespace}.{type}` | Multiple schemas per topic, type-based lookup |
| `TopicRecordName` | `{topic}-{namespace}.{type}` | Maximum flexibility, different schemas per topic+type |

For custom strategies, implement `ISubjectNameStrategy`:

```csharp
registry.SubjectNameStrategy<MyCustomStrategy>();
```

## Compatibility Modes

| Mode | Direction | Description |
|------|-----------|-------------|
| `None` | -- | No compatibility checking |
| `Backward` | Consumer | New consumers can read old producer data |
| `Forward` | Producer | Old consumers can read new producer data |
| `Full` | Both | Consumers and producers can be upgraded independently |
| `*Transitive` | All versions | Checks against all registered versions |

## Configuration Examples

### High Availability

```csharp
registry.SchemaRegistryUrls(
    "http://registry1.example.com:8081",
    "http://registry2.example.com:8081",
    "http://registry3.example.com:8081"
);
```

### Production with Authentication

```csharp
registry.SchemaRegistryUrl("https://registry.example.com:8085")
        .BasicAuth("api-key", "api-secret")
        .ConfigureSsl(ssl => ssl.EnableCertificateVerification(true)
            .CaCertificateLocation("/path/to/ca.crt"))
        .AutoRegisterSchemas(false)  // Disable in production
        .CompatibilityMode(CompatibilityMode.Full);
```

### Multi-Event Topics with RecordName Strategy

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

## Wire Format

Messages produced with Schema Registry include a 5-byte Confluent header:

| Bytes | Content | Description |
|-------|---------|-------------|
| 0 | Magic byte | Always `0x00` |
| 1-4 | Schema ID | Big-endian int32 from Schema Registry |
| 5+ | Payload | Serialized message (JSON, Avro, Protobuf) |

The transport automatically detects and handles this format:
- **Producer**: Prepends schema ID header when Schema Registry is configured
- **Consumer**: Detects magic byte and extracts schema ID for deserialization; falls back to raw JSON if no header found

## DI Registration

When `UseConfluentSchemaRegistry()` is configured, these services are registered automatically:

| Service | Implementation | Lifetime |
|---------|----------------|----------|
| `ConfluentSchemaRegistryOptions` | Configuration | Singleton |
| `ConfluentSchemaRegistryClient` | Underlying Confluent client | Singleton |
| `ISchemaRegistryClient` | `CachingSchemaRegistryClient` decorator | Singleton |

Standalone registration (without the transport builder):

```csharp
services.AddConfluentSchemaRegistry(opts =>
{
    opts.Url = "http://localhost:8081";
    opts.MaxCachedSchemas = 1000;
    opts.Schema.AutoRegisterSchemas = true;
});
```

## Error Handling

| Error | Event ID | Behavior |
|-------|----------|----------|
| Schema registration failure | 22216 | `SchemaRegistryException` thrown |
| Schema retrieval error | 22214 | `SchemaRegistryException` thrown |
| Compatibility check failure | 22219 | `SchemaRegistryException` thrown |
| Network timeout | 22404 | Retry via Polly (if configured) |
| Invalid wire format | 22403 | Fall back to raw JSON |
| Type resolution failure | 22403 | `SchemaRegistryException` thrown |

## Backward Compatibility

Users who don't configure Schema Registry are unaffected:

```csharp
// No UseConfluentSchemaRegistry() = standard JSON format, no schema headers
services.AddKafkaTransport("events", kafka =>
{
    kafka.BootstrapServers("localhost:9092")
         .MapTopic<OrderCreated>("orders-topic");
});
```

## See Also

- [Kafka Transport](./kafka.md) -- Core Kafka configuration and consumer options
- [Choosing a Transport](./choosing-a-transport.md) -- Compare Kafka against other transports
