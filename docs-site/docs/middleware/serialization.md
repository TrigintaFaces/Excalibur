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

## Serializers

| Serializer | Package | Best For |
|------------|---------|----------|
| System.Text.Json (default) | Built-in | Cross-language, debugging |
| MemoryPack | `Excalibur.Dispatch.Serialization.MemoryPack` | .NET-only, max performance |
| MessagePack | `Excalibur.Dispatch.Serialization.MessagePack` | Cross-language, compact |
| Protobuf | `Excalibur.Dispatch.Serialization.Protobuf` | Schema-based, gRPC compat |

## Configuration

### Default (System.Text.Json)

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Default: MemoryPack is used for internal serialization.
// To use System.Text.Json for patterns/hosting:
services.AddJsonSerialization();
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

// Register MemoryPack as internal serializer
services.AddMemoryPackInternalSerialization();

// Messages must be MemoryPack-compatible
[MemoryPackable]
public partial record CreateOrderAction(
    Guid OrderId,
    string CustomerId,
    List<OrderItem> Items) : IDispatchAction;
```

### MessagePack

```bash
dotnet add package Excalibur.Dispatch.Serialization.MessagePack
```

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Register MessagePack serialization
services.AddMessagePackSerialization(options =>
{
    options.SerializerOptions = MessagePackSerializerOptions.Standard
        .WithCompression(MessagePackCompression.Lz4BlockArray);
});

[MessagePackObject]
public class CreateOrderAction : IDispatchAction
{
    [Key(0)] public Guid OrderId { get; set; }
    [Key(1)] public string CustomerId { get; set; }
    [Key(2)] public List<OrderItem> Items { get; set; }
}
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

// Register Protobuf via the pluggable serialization system
services.AddPluggableSerialization();

// Define in .proto file
// message CreateOrderAction {
//   string order_id = 1;
//   string customer_id = 2;
//   repeated OrderItem items = 3;
// }
```

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
});

// Configure encryption via IConfiguration
services.AddDispatchSecurity(configuration);
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

```csharp
// MemoryPack is AOT-friendly by default
[MemoryPackable]
public partial record CreateOrderAction(...) : IDispatchAction;
```

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

// Register pluggable serialization with multiple formats
services.AddPluggableSerialization();
services.AddMessagePackPluggableSerialization();
```

### Pluggable Serialization Registry

```csharp
// Register multiple serializers via the pluggable system
services.AddPluggableSerialization();
services.AddMessagePackPluggableSerialization(setAsCurrent: true);

// Or register manually via ISerializerRegistry
var registry = services.GetRequiredService<ISerializerRegistry>();
registry.Register(SerializerIds.MessagePack,
    MessagePackSerializationExtensions.GetPluggableSerializer());
```

## Custom Serializers

### Implementing IMessageSerializer

```csharp
using Excalibur.Dispatch.Abstractions.Serialization;

public class CustomSerializer : IMessageSerializer
{
    public string SerializerName => "CustomFormat";
    public string SerializerVersion => "1.0";

    public byte[] Serialize<T>(T message)
    {
        // Your serialization logic
        return CustomFormat.Serialize(message);
    }

    public T Deserialize<T>(byte[] data)
    {
        // Your deserialization logic
        return CustomFormat.Deserialize<T>(data);
    }
}
```

### Registration

```csharp
// Register your custom serializer via DI
services.AddSingleton<IMessageSerializer, CustomSerializer>();
```

## Zero-Allocation Serialization

For high-throughput event sourcing scenarios, Dispatch provides `IZeroAllocEventSerializer` which uses `Span<byte>` and `ArrayPool<byte>` to eliminate allocations during serialization.

### When to Use

| Scenario | Recommendation |
|----------|----------------|
| High-volume event stores | Use ZeroAlloc |
| Event replay/projection | Use ZeroAlloc |
| Standard message dispatch | Standard serializers sufficient |
| Cross-language systems | Use standard JSON |

### IZeroAllocEventSerializer Interface

```csharp
public interface IZeroAllocEventSerializer : IEventSerializer
{
    // Span-based serialization (zero-allocation)
    int SerializeEvent(IDomainEvent domainEvent, Span<byte> buffer);
    IDomainEvent DeserializeEvent(ReadOnlySpan<byte> data, Type eventType);
    int GetEventSize(IDomainEvent domainEvent);

    // Snapshot support
    int SerializeSnapshot(object snapshot, Span<byte> buffer);
    object DeserializeSnapshot(ReadOnlySpan<byte> data, Type snapshotType);
    int GetSnapshotSize(object snapshot);
}
```

### Usage Pattern

```csharp
// 1. Get the serializer
var serializer = serviceProvider.GetRequiredService<IZeroAllocEventSerializer>();

// 2. Estimate buffer size
var size = serializer.GetEventSize(domainEvent);

// 3. Rent buffer from pool (zero allocation)
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

### Configuration

```csharp
using Excalibur.Dispatch.Serialization;

services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    // Register SpanEventSerializer (uses MemoryPack by default)
    dispatch.AddZeroAllocEventSerializer();
});

// Or with explicit pluggable serializer
services.AddSingleton<IZeroAllocEventSerializer>(sp =>
    new SpanEventSerializer(sp.GetRequiredService<ISerializerRegistry>()));
```

### SpanEventSerializer Implementation

The built-in `SpanEventSerializer` wraps the pluggable serialization infrastructure:

```csharp
// SpanEventSerializer prefers MemoryPack for best Span support
// Falls back to current configured serializer if MemoryPack unavailable
public SpanEventSerializer(ISerializerRegistry registry)
{
    _pluggable = registry.GetByName("MemoryPack")
        ?? registry.GetById(SerializerIds.MemoryPack)
        ?? registry.GetCurrent().Serializer;
}
```

### Performance Characteristics

| Operation | Standard IEventSerializer | IZeroAllocEventSerializer |
|-----------|---------------------------|---------------------------|
| Allocations per serialize | 1-3 byte[] | 0 (pooled) |
| Allocations per deserialize | 1 byte[] copy | 0 (span-based) |
| GC pressure | Moderate | Minimal |
| Best for | General use | High-throughput event stores |

### Integration with Event Stores

```csharp
public class HighPerformanceEventStore : IEventStore
{
    private readonly IZeroAllocEventSerializer _serializer;

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

### Backward Compatibility

`IZeroAllocEventSerializer` extends `IEventSerializer`, so it supports both patterns:

```csharp
IZeroAllocEventSerializer serializer = ...;

// New Span-based API (zero-allocation)
int written = serializer.SerializeEvent(evt, buffer.AsSpan());

// Legacy byte[] API (still works)
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

- [Transports](../transports/index.md) — Transport-specific serialization
- [Middleware](index.md) — Middleware pipeline
- [Event Sourcing](/docs/event-sourcing/) — Event store integration

## See Also

- [Serialization Providers](serialization-providers.md) - Detailed provider configuration for MemoryPack, MessagePack, Protobuf, and pluggable serialization
- [Middleware Overview](index.md) - How serialization middleware fits into the Dispatch pipeline

