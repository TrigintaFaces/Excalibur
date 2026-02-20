# Serialization Samples

High-performance serialization alternatives for throughput and cross-language interoperability.

## Choosing a Serializer

| Serializer | Best For | Speed | Size | Cross-Language | Native AOT |
|------------|----------|-------|------|----------------|------------|
| **[Protobuf](Protobuf/)** | Cross-language systems, schema contracts | 2-3x JSON | ~60% smaller | Yes (all languages) | Yes |
| **[MessagePack](MessagePackSample/)** | High throughput, .NET + other languages | 3-5x JSON | ~55% smaller | Yes (many languages) | Yes |
| **[MemoryPack](MemoryPackSample/)** | Maximum .NET performance | 10-100x JSON | ~65% smaller | No (.NET only) | Yes |

## Samples Overview

| Sample | What It Demonstrates | Local Dev Ready |
|--------|---------------------|-----------------|
| [Protobuf](Protobuf/) | protobuf-net attributes, schema evolution, cross-language | Yes |
| [MessagePack](MessagePackSample/) | LZ4 compression, union types, polymorphism | Yes |
| [MemoryPack](MemoryPackSample/) | Zero-allocation, immutable types, source generation | Yes |

## Quick Start

### Protobuf (Cross-Language)

```bash
cd samples/08-serialization/Protobuf
dotnet run
```

Best for systems with services in multiple languages (Python, Go, Java).

### MessagePack (Balanced)

```bash
cd samples/08-serialization/MessagePackSample
dotnet run
```

Good balance of performance and cross-language support.

### MemoryPack (Maximum .NET Performance)

```bash
cd samples/08-serialization/MemoryPackSample
dotnet run
```

Fastest option for .NET-to-.NET communication.

## Performance Benchmarks

| Serializer | Serialize (ns/1KB) | Deserialize (ns/1KB) | Size (bytes) |
|------------|--------------------|--------------------- |--------------|
| JSON | 15,200 | 18,700 | 245 |
| Protobuf | 5,100 | 4,800 | 98 |
| MessagePack | 3,800 | 3,200 | 112 |
| MessagePack+LZ4 | 4,500 | 4,000 | 85 |
| **MemoryPack** | **150** | **120** | **86** |

*Benchmarks are approximate and vary by data shape and environment.*

## When to Use Each

| Scenario | Recommended |
|----------|-------------|
| Default / development | JSON (human-readable) |
| Cross-language (Python, Go, Java) | **Protobuf** |
| High throughput + some cross-language | **MessagePack** |
| Maximum .NET performance | **MemoryPack** |
| Schema contracts / versioning | **Protobuf** |
| Bandwidth-constrained | MessagePack+LZ4 or MemoryPack |
| NativeAOT deployment | MemoryPack (zero reflection) |

## Serialization Patterns Comparison

### Attribute Styles

| Serializer | Attribute | Key/Tag System |
|------------|-----------|----------------|
| Protobuf | `[ProtoContract]`, `[ProtoMember(n)]` | Tag numbers (immutable) |
| MessagePack | `[MessagePackObject]`, `[Key(n)]` | Key indices (immutable) |
| MemoryPack | `[MemoryPackable]` (partial class) | Property order or `[MemoryPackOrder]` |

### Cross-Language Support

| Serializer | Languages | Notes |
|------------|-----------|-------|
| Protobuf | C#, Python, Java, Go, C++, Node.js, Ruby, PHP | Official Google implementations |
| MessagePack | C#, Python, Java, Go, Node.js, Ruby | Union types are C#-only |
| MemoryPack | C# (.NET 6+) | No cross-language support |

### Schema Evolution

| Serializer | Add Fields | Remove Fields | Rename Fields |
|------------|------------|---------------|---------------|
| Protobuf | Yes (new tag) | Yes (reserve tag) | No (use new tag) |
| MessagePack | Yes (new key) | Yes (keep old key) | No (use new key) |
| MemoryPack | Yes (end of class) | Limited | No |

## Key Concepts

### Protobuf: Tag-Based Serialization

```csharp
[ProtoContract]
public class OrderPlacedEvent
{
    [ProtoMember(1)] public string OrderId { get; set; }
    [ProtoMember(2)] public decimal Amount { get; set; }
    // Tag 3 reserved for removed field
}
```

### MessagePack: Union Types for Polymorphism

```csharp
[Union(0, typeof(OrderPlacedEvent))]
[Union(1, typeof(OrderCancelledEvent))]
public interface IOrderEvent { }
```

### MemoryPack: Zero-Allocation Deserialization

```csharp
[MemoryPackable]
public partial class OrderPlacedEvent
{
    public string OrderId { get; set; }
    public decimal Amount { get; set; }
}
```

## Configuration

### Dispatch Integration

```csharp
// Protobuf
builder.Services.AddProtobufSerialization(options =>
{
    options.WireFormat = ProtobufWireFormat.Binary;
});

// MessagePack
builder.Services.AddMessagePackSerialization(options =>
{
    options.UseLz4Compression = true;
});

// MemoryPack (default)
builder.Services.AddMemoryPackInternalSerialization();
```

## Prerequisites

| Sample | Requirements |
|--------|-------------|
| Protobuf | .NET 9.0 SDK |
| MessagePack | .NET 9.0 SDK |
| MemoryPack | .NET 9.0 SDK |

## Related Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Serialization.Protobuf` | Protocol Buffers (protobuf-net) |
| `Excalibur.Dispatch.Serialization.MessagePack` | MessagePack-CSharp with LZ4 |
| `Excalibur.Dispatch.Serialization.MemoryPack` | Cysharp MemoryPack |

## Related Samples

- [RabbitMQ](../02-messaging-transports/RabbitMQ/) - Transport with serialization
- [Kafka](../02-messaging-transports/Kafka/) - High-throughput transport

## Learn More

- [Protocol Buffers](https://protobuf.dev/)
- [protobuf-net](https://protobuf-net.github.io/protobuf-net/)
- [MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp)
- [MemoryPack](https://github.com/Cysharp/MemoryPack)

---

*Category: Serialization | Sprint 432*
