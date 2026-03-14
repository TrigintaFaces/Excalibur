# Protobuf Serialization Sample

This sample demonstrates Protocol Buffers (Protobuf) serialization with `Excalibur.Dispatch.Serialization.Protobuf` for high-performance, cross-language messaging using Google.Protobuf.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Quick Start

```bash
dotnet run
```

No external dependencies required - uses in-memory storage for demonstration.

## What This Sample Demonstrates

### Google.Protobuf IMessage Implementation

Events implement `IMessage<T>` from Google.Protobuf for wire-format serialization:

```csharp
public sealed class OrderPlacedEvent : IMessage<OrderPlacedEvent>, IDispatchEvent
{
    private static readonly MessageParser<OrderPlacedEvent> _parser = new(() => new OrderPlacedEvent());

    public static MessageParser<OrderPlacedEvent> Parser => _parser;

    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public float TotalAmount { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }

    public int CalculateSize() { /* wire format size calculation */ }
    public void WriteTo(CodedOutputStream output) { /* field-by-field serialization */ }
    public void MergeFrom(CodedInputStream input) { /* tag-based deserialization */ }
    public void MergeFrom(OrderPlacedEvent other) { /* merge non-default values */ }
    public OrderPlacedEvent Clone() => new() { /* copy all fields */ };
}
```

> In production, you would typically use `protoc` to generate these classes from `.proto` files. This sample uses manual implementation for educational purposes.

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
});
```

## Project Structure

```
Protobuf/
├── Messages/
│   └── OrderEvents.cs          # IMessage<T> implemented events
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

The manual `IMessage<T>` implementation maps to this `.proto` schema:

```protobuf
syntax = "proto3";
package dispatch.samples;

message OrderPlacedEvent {
  string event_id = 1;
  string order_id = 2;
  string customer_id = 3;
  float total_amount = 4;
  string product_name = 5;
  int32 quantity = 6;
}

message OrderCancelledEvent {
  string event_id = 1;
  string order_id = 2;
  string reason = 3;
  string cancelled_by = 4;
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
- [Google.Protobuf NuGet](https://www.nuget.org/packages/Google.Protobuf)
- [Excalibur.Dispatch.Serialization.Protobuf Package](../../../src/Dispatch/Excalibur.Dispatch.Serialization.Protobuf/)
