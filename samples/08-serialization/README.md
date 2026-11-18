# Serialization Samples

Serialization alternatives with different contract, interoperability and deployment requirements. Compare performance using the actual payload and API configuration.

## Choosing a Serializer

The speed and size figures in this earlier table have no verified matched sample
payload/API/runtime provenance. They are retained as unverified historical claims,
not current rankings or guarantees. Compare the actual workload before choosing.

| Serializer | Best For | Speed | Size | Cross-Language | Native AOT |
|------------|----------|-------|------|----------------|------------|
| **[Protobuf](Protobuf/)** | Cross-language systems, schema contracts | 2-3x JSON | ~60% smaller | Yes (all languages) | Verify configuration and published application |
| **[MessagePack](MessagePackSample/)** | High throughput, .NET + other languages | 3-5x JSON | ~55% smaller | Yes (many languages) | Verify configuration and published application |
| **[MemoryPack](MemoryPackSample/)** | .NET binary communication | 10-100x JSON | ~65% smaller | No (.NET only) | Verify configuration and published application |

## Samples Overview

| Sample | What It Demonstrates | Local Dev Ready |
|--------|---------------------|-----------------|
| [Protobuf](Protobuf/) | Google.Protobuf IMessage implementation, tagged fields and cross-language contracts | Yes |
| [MessagePack](MessagePackSample/) | LZ4 compression, union types, polymorphism | Yes |
| [MemoryPack](MemoryPackSample/) | Span input, immutable types, source generation | Yes |

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

An option for MessagePack interoperability; measure encoding cost, payload size and configured compression.

### MemoryPack (.NET Binary Serialization)

```bash
cd samples/08-serialization/MemoryPackSample
dotnet run
```

An option for source-generated .NET-to-.NET binary serialization.

## Performance Benchmarks

This historical illustration is not verified measurement evidence for these
samples. Its units and size column do not establish a matched 1 KB workload.

| Serializer | Serialize (ns/1KB) | Deserialize (ns/1KB) | Size (bytes) |
|------------|--------------------|--------------------- |--------------|
| JSON | 15,200 | 18,700 | 245 |
| Protobuf | 5,100 | 4,800 | 98 |
| MessagePack | 3,800 | 3,200 | 112 |
| MessagePack+LZ4 | 4,500 | 4,000 | 85 |
| **MemoryPack** | **150** | **120** | **86** |

The [October envelope verification report](../../docs/performance/memorypack-2026-10-16.md)
records scoped observations, nonzero allocations and unresolved failures for
different framework benchmark payloads/APIs. It does not validate this table's ratios.

## When to Use Each

| Scenario | Recommended |
|----------|-------------|
| Default / development | JSON (human-readable) |
| Cross-language (Python, Go, Java) | **Protobuf** |
| Throughput-sensitive cross-language workloads | Compare MessagePack and alternatives using the actual workload |
| .NET binary communication | Compare MemoryPack with alternatives using the actual workload |
| Schema contracts / versioning | **Protobuf** |
| Bandwidth-constrained | Compare actual encoded sizes with the selected compression and payload |
| NativeAOT deployment | MemoryPack source-generated serialization; verify the complete NativeAOT deployment |

## Serialization Patterns Comparison

### Attribute Styles

| Serializer | Attribute | Key/Tag System |
|------------|-----------|----------------|
| Protobuf | Google.Protobuf `IMessage<T>` contract, typically generated from `.proto` | Preserve field numbers and reserve removed fields |
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

The [Protobuf sample](Protobuf/) uses a manual `Google.Protobuf.IMessage<T>` implementation for illustration; see [OrderEvents.cs](Protobuf/Messages/OrderEvents.cs). Application schemas commonly generate these message types from `.proto` files. The framework wrapper expects Google.Protobuf messages; protobuf-net's `[ProtoContract]` and `[ProtoMember]` attributes do not provide that interface.

### MessagePack: Union Types for Polymorphism

```csharp
[Union(0, typeof(OrderPlacedEvent))]
[Union(1, typeof(OrderCancelledEvent))]
public interface IOrderEvent { }
```

### MemoryPack: Source-Generated Deserialization

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
// Choose Protobuf; message contracts must implement Google.Protobuf.IMessage
builder.Services.AddProtobufSerializer();

// Alternatively choose MessagePack; configure the required contracts/resolvers
builder.Services.AddMessagePackSerializer();

// Alternatively choose MemoryPack; supply generated formatters for payload types
builder.Services.AddMemoryPackSerializer();
```

## Prerequisites

| Sample | Requirements |
|--------|-------------|
| Protobuf | Repository-pinned SDK; project targets net10.0 |
| MessagePack | Repository-pinned SDK; project targets net10.0 |
| MemoryPack | Repository-pinned SDK; project targets net10.0 |

## Related Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Dispatch.Serialization.Protobuf` | Protocol Buffers (Google.Protobuf) |
| `Excalibur.Dispatch.Serialization.MessagePack` | MessagePack-CSharp with LZ4 |
| `Excalibur.Dispatch.Serialization.MemoryPack` | Cysharp MemoryPack |

## Related Samples

- [RabbitMQ](../02-messaging-transports/RabbitMQ/) - Transport with serialization
- [Kafka](../02-messaging-transports/Kafka/) - Partitioned event-log transport

## Learn More

- [Protocol Buffers](https://protobuf.dev/)
- [MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp)
- [MemoryPack](https://github.com/Cysharp/MemoryPack)

---

*Category: Serialization*
