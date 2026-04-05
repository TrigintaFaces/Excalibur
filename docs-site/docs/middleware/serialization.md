---
sidebar_position: 6
title: Serialization
description: Message serialization options for JSON, MemoryPack, and custom formats
---

# Serialization

Dispatch supports multiple serialization formats for messages. Choose based on your needs for performance, interoperability, and debugging.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- For alternative serializers, install the provider package (e.g., `Excalibur.Dispatch.Serialization.MemoryPack`)
- Familiarity with [middleware concepts](./index.md) and [pipeline stages](../pipeline/index.md)

## Two-Layer Serialization Architecture

Dispatch uses two independent serialization layers. Understanding this distinction helps you choose the right configuration:

| Layer | Interface | What It Serializes | Default | Consumer Attributes Needed? |
|-------|-----------|-------------------|---------|---------------------------|
| **Event storage** | `IEventSerializer` | Your domain events (POCOs) | `JsonEventSerializer` (System.Text.Json) | No |
| **Envelope transport** | `ISerializer` + `IBinaryEnvelopeDeserializer` | Internal `OutboxEnvelope` / `InboxEnvelope` wrappers | None (JSON fallback) | No |

**How the layers work together:**

```
Your Event (plain POCO)
  → IEventSerializer.Serialize() → byte[] payload
    → wrapped in [MemoryPackable] OutboxEnvelope { Payload = byte[] }
      → ISerializer (MemoryPack) serializes the envelope → stored/transmitted
```

The envelope layer is an internal implementation detail. Your consumer event types never need `[MemoryPackable]`, `[MessagePackObject]`, or any serializer-specific attributes -- they remain plain POCOs regardless of which serializer you choose.

`AddDispatch()` auto-registers `JsonEventSerializer` as `IEventSerializer` via `TryAddSingleton`. Calling `services.AddMemoryPackSerializer()` registers MemoryPack for the envelope layer only -- `IEventSerializer` stays as JSON unless you explicitly override it.

## Serializers

| Serializer | Package | Best For |
|------------|---------|----------|
| System.Text.Json (default) | Built-in | Cross-language, debugging |
| MemoryPack | `Excalibur.Dispatch.Serialization.MemoryPack` | .NET-only, max performance |
| MessagePack | `Excalibur.Dispatch.Serialization.MessagePack` | Cross-language, compact |
| Protobuf | `Excalibur.Dispatch.Serialization.Protobuf` | Schema-based, gRPC compat |
| Avro | `Excalibur.Dispatch.Serialization.Avro` | Schema-based, Kafka/Hadoop |

## Configuration

### Default (System.Text.Json)

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Default: JSON (System.Text.Json) is used for serialization.
// No extra configuration needed -- works with any POCO event type.
```

### Custom JSON Options

```csharp
services.AddJsonSerialization(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = false;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
```

### MemoryPack (Fastest)

```bash
dotnet add package Excalibur.Dispatch.Serialization.MemoryPack
```

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// One call registers MemoryPack, adds it to the serializer registry, and sets it as current
services.AddMemoryPackSerializer();
```

:::info No attributes needed on your events
Consumer event types do **not** need `[MemoryPackable]` or any serializer-specific attributes. Only the internal envelope wrapper uses MemoryPack attributes. Your domain events remain plain POCOs.
:::

### MessagePack

```bash
dotnet add package Excalibur.Dispatch.Serialization.MessagePack
```

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// One call does everything: DI registration, serializer registry, set as current
services.AddMessagePackSerializer();
```

### Protobuf

```bash
dotnet add package Excalibur.Dispatch.Serialization.Protobuf
```

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// One call does everything: DI registration, serializer registry, set as current
services.AddProtobufSerializer();
```

See [Serialization Providers](serialization-providers.md) for detailed provider configuration including Avro, native options, and custom option overloads.

## Compression

### Enable Compression

Compression can be configured via serialization options:

```csharp
services.AddJsonSerialization(options =>
{
    // Configure compression settings on JSON options
    options.SerializerOptions.WriteIndented = false; // Compact output
});

// Or configure compression at the transport level via the builder
services.AddDispatch(dispatch =>
{
    dispatch.UseKafka(kafka =>
    {
        kafka.CompressionType(CompressionType.Gzip);
    });
});
```

## Encryption

### Transport Encryption

Encryption is handled by the `Excalibur.Dispatch.Security` package, not the serializer:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseMiddleware<MessageEncryptionMiddleware>();

    // Configure encryption via IConfiguration
    dispatch.UseSecurity(configuration);
});
```

### Field-Level Encryption

Field-level encryption uses the `[EncryptedField]` attribute on `byte[]` properties. For string data, serialize to bytes first:

```csharp
using Excalibur.Dispatch.Compliance;

public class CustomerProjection
{
    public string Id { get; set; }
    public string Name { get; set; }

    [EncryptedField]
    public byte[] SocialSecurityNumber { get; set; }

    [EncryptedField(Purpose = "pci-data")]
    public byte[] CreditCardData { get; set; }
}
```

See [Security](../security/encryption-architecture.md) for encryption architecture details.

## AOT Compatibility

### Source Generators

```csharp
// Enable source generation for AOT
[JsonSerializable(typeof(CreateOrderAction))]
[JsonSerializable(typeof(OrderCreatedEvent))]
public partial class AppJsonContext : JsonSerializerContext { }

services.AddJsonSerialization(options =>
{
    options.SerializerOptions.TypeInfoResolver = AppJsonContext.Default;
});
```

### MemoryPack AOT

MemoryPack is AOT-friendly via source generation. The internal envelope uses `[MemoryPackable]`; your consumer event types do not need any attributes.

## Multi-Format Support

### Per-Transport Serialization

Use transport routing with the pluggable serialization system to route different message types to transports that use different serializers:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    dispatch.UseRouting(routing =>
    {
        routing.Transport
            // Kafka: high-volume events
            .Route<HighVolumeEvent>().To("kafka")
            // External API: interop events
            .Route<ExternalEvent>().To("http")
            .Default("rabbitmq");
    });
});

// Register the serializer you want for internal persistence
services.AddMessagePackSerializer();
```

## Custom Serializers

### Implementing ISerializer

```csharp
using System.Buffers;
using Excalibur.Dispatch.Abstractions.Serialization;

public class CustomSerializer : ISerializer
{
    public string Name => "CustomFormat";
    public string Version => "1.0";
    public string ContentType => "application/x-custom";

    public void Serialize<T>(T value, IBufferWriter<byte> bufferWriter)
    {
        var bytes = CustomFormat.Serialize(value);
        bufferWriter.Write(bytes);
    }

    public T Deserialize<T>(ReadOnlySpan<byte> data)
    {
        return CustomFormat.Deserialize<T>(data);
    }

    public byte[] SerializeObject(object value, Type type)
    {
        return CustomFormat.Serialize(value, type);
    }

    public object DeserializeObject(ReadOnlySpan<byte> data, Type type)
    {
        return CustomFormat.Deserialize(data, type);
    }
}
```

### Registration

For custom serializers, use the `AddPluggableSerializer` extension method to register with the serializer registry:

```csharp
services.AddPluggableSerializer(200, new MyCustomSerializer(), setAsCurrent: true);
```

## Pool-Backed Serialization

For high-throughput event sourcing scenarios, `IEventSerializer` provides Span-based overloads via `EventSerializerExtensions` that use `ArrayPool<byte>` to minimize allocations during serialization.

### When to Use

| Scenario | Recommendation |
|----------|----------------|
| High-volume event stores | Use Span-based extensions |
| Event replay/projection | Use Span-based extensions |
| Standard message dispatch | Standard serializers sufficient |
| Cross-language systems | Use standard JSON |

### EventSerializerExtensions

Span-based overloads are extension methods on `IEventSerializer`:

```csharp
public static class EventSerializerExtensions
{
    // Serialize to pre-allocated span (pool-backed, low-allocation)
    public static int SerializeEvent(this IEventSerializer s, IDomainEvent evt, Span<byte> buffer);

    // Deserialize from span
    public static IDomainEvent DeserializeEvent(this IEventSerializer s, ReadOnlySpan<byte> data, Type type);

    // Get serialized size for buffer allocation
    public static int GetEventSize(this IEventSerializer s, IDomainEvent evt);
}
```

### Usage Pattern

```csharp
// 1. Get the serializer
var serializer = serviceProvider.GetRequiredService<IEventSerializer>();

// 2. Estimate buffer size
var size = serializer.GetEventSize(domainEvent);

// 3. Rent buffer from pool (pool-backed, low-allocation)
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // 4. Serialize to rented buffer
    var written = serializer.SerializeEvent(domainEvent, buffer);

    // 5. Use the serialized data
    await eventStore.AppendAsync(buffer.AsSpan(0, written), ct);
}
finally
{
    // 6. Return buffer to pool
    ArrayPool<byte>.Shared.Return(buffer);
}
```

### SpanEventSerializer Implementation

The built-in `SpanEventSerializer` implements `IEventSerializer` and wraps the pluggable serialization infrastructure:

```csharp
// SpanEventSerializer delegates to the configured ISerializer
// Prefers the current/default serializer (JSON-first per ADR-295),
// falls back to MemoryPack only if no current serializer is configured
public SpanEventSerializer(ISerializerRegistry registry)
{
    _serializer = registry.GetCurrent().Serializer
        ?? registry.GetByName("MemoryPack")
        ?? registry.GetById(SerializerIds.MemoryPack);
}
```

### Performance Characteristics

| Operation | Standard byte[] API | Span-based Extensions |
|-----------|---------------------|----------------------|
| Allocations per serialize | 1-3 byte[] | 0 (pooled) |
| Allocations per deserialize | 1 byte[] copy | 0 (span-based) |
| GC pressure | Moderate | Minimal |
| Best for | General use | High-throughput event stores |

### Integration with Event Stores

```csharp
public class HighPerformanceEventStore : IEventStore
{
    private readonly IEventSerializer _serializer;

    public async Task AppendAsync(
        Guid streamId,
        IReadOnlyList<IDomainEvent> events,
        CancellationToken ct)
    {
        foreach (var evt in events)
        {
            var size = _serializer.GetEventSize(evt);
            var buffer = ArrayPool<byte>.Shared.Rent(size);
            try
            {
                var written = _serializer.SerializeEvent(evt, buffer);
                await PersistAsync(streamId, buffer.AsSpan(0, written), ct);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
```

### Both APIs Available

`EventSerializerExtensions` adds Span-based methods alongside the core byte[] API:

```csharp
IEventSerializer serializer = ...;

// Span-based API via extension (pool-backed, low-allocation)
int written = serializer.SerializeEvent(evt, buffer.AsSpan());

// Core byte[] API (always available)
byte[] data = serializer.SerializeEvent(evt);
```

---

## Performance Comparison

| Serializer | Serialize | Deserialize | Size | Allocations |
|------------|-----------|-------------|------|-------------|
| ZeroAlloc + MemoryPack | 0.8x | 0.9x | 1x | 0 |
| MemoryPack | 1x | 1x | 1x | 1-2 |
| MessagePack | 2.5x | 2x | 1.1x | 2-3 |
| System.Text.Json | 4x | 3x | 1.5x | 3-5 |
| Newtonsoft.Json | 6x | 5x | 1.5x | 5-8 |
| Protobuf | 3x | 2.5x | 0.9x | 2-3 |

*Relative performance - lower is better. Actual results vary by payload.*

### ZeroAlloc Benchmark Suite

For detailed benchmarks comparing ZeroAlloc vs JSON serialization, run:

```bash
cd benchmarks/Excalibur.Dispatch.Benchmarks
dotnet run -c Release --filter *SpanEventSerializer*
```

**Benchmark Categories:**
- JSON vs ZeroAlloc comparison (small and large events)
- Buffer pooling allocation verification
- Event sourcing scenarios (100-event replay/append)
- Round-trip benchmarks

See the [Performance Best Practices](../performance/messagecontext-best-practices.md) for detailed optimization guidance.

## Best Practices

| Scenario | Recommendation |
|----------|----------------|
| High-throughput event stores | ZeroAlloc + MemoryPack |
| Event replay/projections | ZeroAlloc + MemoryPack |
| .NET-only, high performance | MemoryPack |
| Cross-language | System.Text.Json or MessagePack |
| Schema evolution | Protobuf |
| Debugging | System.Text.Json with WriteIndented |
| Large payloads | Enable compression |
| Sensitive data | Enable encryption |
| AOT deployment | MemoryPack or System.Text.Json with source gen |

## Next Steps

- [Transports](../transports/index.md) -- Transport-specific serialization
- [Middleware](index.md) -- Middleware pipeline
- [Event Sourcing](/docs/event-sourcing/) -- Event store integration

## See Also

- [Serialization Providers](serialization-providers.md) - Detailed provider configuration for MemoryPack, MessagePack, Protobuf, Avro, and pluggable serialization
- [Middleware Overview](index.md) - How serialization middleware fits into the Dispatch pipeline
