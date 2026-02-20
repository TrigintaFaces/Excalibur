# Protobuf Serialization Sample

This sample demonstrates Protocol Buffers (Protobuf) serialization with `Excalibur.Dispatch.Serialization.Protobuf` for high-performance, cross-language messaging.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Quick Start

```bash
dotnet run
```

No external dependencies required - uses in-memory storage for demonstration.

## What This Sample Demonstrates

### protobuf-net Attribute-Based Serialization

Use C# attributes instead of `.proto` files:

```csharp
[ProtoContract]
public sealed class OrderPlacedEvent : IDispatchEvent
{
    [ProtoMember(1)]
    public Guid EventId { get; set; }

    [ProtoMember(2)]
    public string OrderId { get; set; }

    [ProtoMember(3)]
    public string CustomerId { get; set; }

    [ProtoMember(4)]
    public List<OrderItem> Items { get; set; }

    [ProtoMember(5)]
    public decimal TotalAmount { get; set; }

    [ProtoMember(6)]
    public DateTimeOffset OccurredAt { get; set; }
}
```

### Schema Evolution with Reserved Fields

Safely remove fields while maintaining backwards compatibility:

```csharp
[ProtoContract]
public sealed class OrderPlacedEvent
{
    // Active fields
    [ProtoMember(1)] public string OrderId { get; set; }
    [ProtoMember(2)] public string CustomerId { get; set; }

    // Reserved tags - these were previously used:
    // Tag 7 was 'ShippingAddress' (removed in v2)
    // Tag 8 was 'Notes' (removed in v2)
    // In .proto files: reserved 7, 8;
}
```

### Binary Format Efficiency

Protobuf provides significant size reduction compared to JSON:

```
Serialization Size Comparison:
  Protobuf binary: 98 bytes
  JSON text:       245 bytes
  Size reduction:  60%
```

### Dispatch Integration

Register Protobuf serialization with Dispatch:

```csharp
builder.Services.AddProtobufSerialization(options =>
{
    options.WireFormat = ProtobufWireFormat.Binary;
    options.IgnoreMissingFields = true;
});
```

## Project Structure

```
Protobuf/
├── Messages/
│   └── OrderEvents.cs          # [ProtoContract] annotated events
├── Handlers/
│   └── OrderEventHandlers.cs   # Event handlers
├── Program.cs                  # Demo scenarios
├── appsettings.json           # Configuration
└── README.md                  # This file
```

## Tag Number Best Practices

| Rule | Description |
|------|-------------|
| Unique | Each field must have a unique tag number within its type |
| Immutable | Once assigned, never reuse or change tag numbers |
| Low numbers | Tags 1-15 use 1 byte; 16-2047 use 2 bytes |
| Reserved range | Tags 19000-19999 are reserved by Protobuf |
| Document removed | Mark removed tags as reserved to prevent reuse |

## Cross-Language Interoperability

The protobuf-net attributes map directly to `.proto` schema:

```protobuf
syntax = "proto3";
package dispatch.samples;

message OrderPlacedEvent {
  string event_id = 1;
  string order_id = 2;
  string customer_id = 3;
  repeated OrderItem items = 4;
  double total_amount = 5;
  int64 occurred_at = 6;
  reserved 7, 8;
}

message OrderItem {
  string product_sku = 1;
  string product_name = 2;
  int32 quantity = 3;
  double unit_price = 4;
}
```

### Language Support

| Language | Package |
|----------|---------|
| Python | `protobuf` |
| Java | `protobuf-java` |
| Go | `google.golang.org/protobuf` |
| Node.js | `protobufjs` or `google-protobuf` |
| C++ | `libprotobuf` |

## Performance Comparison

| Serializer | Serialize (µs) | Deserialize (µs) | Size (bytes) |
|------------|----------------|------------------|--------------|
| JSON | 15.2 | 18.7 | 245 |
| **Protobuf** | **5.1** | **4.8** | **98** |
| MessagePack | 3.8 | 3.2 | 112 |
| MemoryPack | 1.2 | 0.8 | 86 |

*Benchmarks are approximate and vary by data shape and environment.*

## When to Use Protobuf

### Good Use Cases

- **Cross-language systems**: Services in different languages (Python ML, Go services)
- **GCP/AWS integration**: Native Protobuf support in cloud services
- **Schema evolution**: Need to add/remove fields over time
- **Network efficiency**: Bandwidth-constrained environments
- **External APIs**: Publishing APIs consumed by third parties

### Consider Alternatives

- **MemoryPack**: Fastest, but .NET-only
- **MessagePack**: Good cross-language support with better .NET performance
- **JSON**: Maximum compatibility, human-readable debugging

## Configuration Options

```json
{
  "Serialization": {
    "Protobuf": {
      "WireFormat": "Binary",
      "IgnoreMissingFields": true
    }
  }
}
```

| Option | Values | Description |
|--------|--------|-------------|
| `WireFormat` | `Binary`, `Text` | Binary is default and more efficient |
| `IgnoreMissingFields` | `true`/`false` | Ignore unknown fields during deserialization |

## Related Samples

- [MessagePack](../MessagePackSample/) - High-performance binary serialization
- [MemoryPack](../MemoryPackSample/) - Zero-copy serialization (.NET-only)

## Learn More

- [Protocol Buffers Documentation](https://protobuf.dev/)
- [protobuf-net Documentation](https://protobuf-net.github.io/protobuf-net/)
- [Excalibur.Dispatch.Serialization.Protobuf Package](../../../src/Dispatch/Excalibur.Dispatch.Serialization.Protobuf/)

