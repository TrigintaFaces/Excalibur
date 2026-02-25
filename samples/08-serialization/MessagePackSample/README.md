# MessagePack Serialization Sample

This sample demonstrates **MessagePack serialization** with the Excalibur framework using `Excalibur.Dispatch.Serialization.MessagePack`.

## What This Sample Shows

1. **High-Performance Binary Serialization** - MessagePack provides compact binary format
2. **LZ4 Compression** - Optional compression for reduced payload size
3. **Union Types** - Polymorphic serialization with type discrimination
4. **Attribute-Based Configuration** - Simple `[MessagePackObject]` and `[Key(n)]` attributes

## Key Concepts

### MessagePack Object Attributes

```csharp
[MessagePackObject]
public sealed class OrderPlacedEvent : IDispatchEvent
{
    [Key(0)]
    public Guid EventId { get; set; }

    [Key(1)]
    public string OrderId { get; set; }

    [Key(2)]
    public string CustomerId { get; set; }

    // Key indices must be unique and stable for backwards compatibility
}
```

### Union Types for Polymorphism

```csharp
[Union(0, typeof(OrderPlacedEvent))]
[Union(1, typeof(OrderCancelledEvent))]
[Union(2, typeof(OrderShippedEvent))]
public interface IOrderEvent : IDispatchEvent
{
    string OrderId { get; }
}
```

### LZ4 Compression

```csharp
// Configuration via DI
builder.Services.AddMessagePackSerialization(options =>
{
    options.UseLz4Compression = true;
});

// Direct usage
var lz4Options = MessagePackSerializerOptions.Standard
    .WithCompression(MessagePackCompression.Lz4BlockArray);
var compressed = MessagePackSerializer.Serialize(data, lz4Options);
```

## Running the Sample

```bash
cd samples/08-serialization/MessagePackSample
dotnet run
```

## Expected Output

```
Starting MessagePack Serialization Sample...

=== MessagePack Event Serialization Demo ===
Dispatching OrderPlacedEvent...
[Handler] Order placed: ORD-2026-001 for customer CUST-12345, Total: $299.97, Items: 2

=== MessagePack Binary Format with LZ4 Compression ===
Serialization Size Comparison:
  JSON text:             245 bytes
  MessagePack binary:    112 bytes (54.3% smaller)
  MessagePack + LZ4:     85 bytes (65.3% smaller)

=== MessagePack Deserialization Demo ===
Deserialized event from LZ4 compressed data:
  OrderId: ORD-2026-001
  CustomerId: CUST-12345
  TotalAmount: $299.97
  Items count: 2

=== Union Types for Polymorphism ===
MessagePack Union allows polymorphic serialization...
  Serialized OrderPlacedEvent (112 bytes) -> Deserialized as OrderPlacedEvent
  Serialized OrderCancelledEvent (45 bytes) -> Deserialized as OrderCancelledEvent
  Serialized OrderShippedEvent (52 bytes) -> Deserialized as OrderShippedEvent
```

## When to Use MessagePack

| Scenario | Recommendation |
|----------|----------------|
| High throughput required | ✅ MessagePack (3-5x faster than JSON) |
| Bandwidth-constrained | ✅ MessagePack + LZ4 compression |
| Cross-language systems | ✅ MessagePack (broad language support) |
| Polymorphic messages | ✅ Union types for C#-to-C# |
| Human-readable debugging | ❌ Use JSON instead |
| Schema evolution needed | ⚠️ Use Protobuf for complex evolution |

## Performance Comparison

| Serializer | Serialize (µs) | Deserialize (µs) | Size (bytes) |
|------------|----------------|------------------|--------------|
| JSON | 15.2 | 18.7 | 245 |
| Protobuf | 5.1 | 4.8 | 98 |
| MessagePack | 3.8 | 3.2 | 112 |
| MessagePack+LZ4 | 4.5 | 4.0 | 85 |
| MemoryPack | 1.2 | 0.8 | 86 |

## Cross-Language Support

MessagePack has implementations for:
- **Python**: `msgpack-python`
- **Java**: `msgpack-java`
- **Go**: `github.com/vmihailenco/msgpack`
- **Node.js**: `@msgpack/msgpack`
- **Ruby**: `msgpack-ruby`

**Note**: Union types are C# MessagePack-specific. For cross-language polymorphism, use a type discriminator field.

## Project Structure

```
MessagePackSample/
├── MessagePackSample.csproj  # Project file with MessagePack references
├── Program.cs                # Main sample demonstrating all features
├── appsettings.json          # Configuration for logging and serialization
├── README.md                 # This file
├── Messages/
│   └── OrderEvents.cs        # MessagePack-annotated event classes
└── Handlers/
    └── OrderEventHandlers.cs # Event handlers for processing
```

## Configuration Options

In `appsettings.json`:

```json
{
  "Serialization": {
    "MessagePack": {
      "UseLz4Compression": true
    }
  }
}
```

Options:
- `UseLz4Compression` - Enable LZ4 block compression for smaller payloads
- `IncludePrivateMembers` - Include private members during serialization
- `StringEncoding` - String encoding format (default: UTF8)

## Related Samples

- [Protobuf Serialization](../Protobuf/) - Schema-based binary serialization
- [MemoryPack Serialization](../MemoryPackSample/) - Zero-allocation .NET serialization

