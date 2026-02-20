# MemoryPack Serialization Sample

This sample demonstrates **MemoryPack serialization** with the Excalibur framework using `Excalibur.Dispatch.Serialization.MemoryPack`.

## What This Sample Shows

1. **Zero-Allocation Serialization** - Fastest .NET binary serializer
2. **Source Generation** - No runtime reflection, full AOT support
3. **Immutable Types** - Constructor-based deserialization with `[MemoryPackConstructor]`
4. **NativeAOT Compatible** - Full trimming and AOT deployment support

## Key Concepts

### MemoryPackable Attribute

```csharp
[MemoryPackable]
public partial class OrderPlacedEvent : IDispatchEvent
{
    public Guid EventId { get; set; }
    public string OrderId { get; set; }
    public string CustomerId { get; set; }
    public List<OrderItem> Items { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
```

**Important**: The `partial` keyword is required for source generation.

### Immutable Types with Constructor

```csharp
[MemoryPackable]
public partial class OrderCompletedEvent : IDispatchEvent
{
    [MemoryPackConstructor]
    public OrderCompletedEvent(Guid eventId, string orderId, DateTimeOffset completedAt, decimal finalAmount)
    {
        EventId = eventId;
        OrderId = orderId;
        CompletedAt = completedAt;
        FinalAmount = finalAmount;
    }

    public Guid EventId { get; }
    public string OrderId { get; }
    public DateTimeOffset CompletedAt { get; }
    public decimal FinalAmount { get; }
}
```

### Zero-Allocation Deserialization

```csharp
// ReadOnlySpan-based deserialization - no heap allocations
ReadOnlySpan<byte> span = serializedBytes;
var evt = MemoryPackSerializer.Deserialize<OrderPlacedEvent>(span);
```

## Running the Sample

```bash
cd samples/08-serialization/MemoryPackSample
dotnet run
```

## Expected Output

```
Starting MemoryPack Serialization Sample...

=== MemoryPack Event Serialization Demo ===
Dispatching OrderPlacedEvent...
[Handler] Order placed: ORD-2026-001 for customer CUST-12345, Total: $299.97, Items: 2

=== MemoryPack Binary Format Performance ===
Serialization Size Comparison:
  JSON text:        245 bytes
  MemoryPack:       86 bytes (64.9% smaller)

=== Zero-Allocation Deserialization Demo ===
Deserialized event from binary data:
  OrderId: ORD-2026-001
  CustomerId: CUST-12345
  TotalAmount: $299.97
  Items count: 2

=== Immutable Type Serialization ===
Immutable event with [MemoryPackConstructor]:
  Original OrderId: ORD-2026-001
  Deserialized OrderId: ORD-2026-001
  Values match: True
```

## When to Use MemoryPack

| Scenario | Recommendation |
|----------|----------------|
| Maximum throughput | MemoryPack (10-100x faster than JSON) |
| .NET-to-.NET communication | MemoryPack (optimal) |
| NativeAOT deployment | MemoryPack (full support) |
| Memory-constrained | MemoryPack (zero allocations) |
| Cross-language systems | Use MessagePack or Protobuf instead |
| Human-readable debugging | Use JSON instead |
| Schema contracts | Use Protobuf instead |

## Performance Comparison

| Serializer | Serialize (ns/1KB) | Deserialize (ns/1KB) | Size (bytes) |
|------------|--------------------|--------------------- |--------------|
| JSON | 15,200 | 18,700 | 245 |
| Protobuf | 5,100 | 4,800 | 98 |
| MessagePack | 3,800 | 3,200 | 112 |
| MessagePack+LZ4 | 4,500 | 4,000 | 85 |
| **MemoryPack** | **150** | **120** | **86** |

MemoryPack is **100x faster** than JSON and produces the smallest payloads.

## Source Generation Requirements

MemoryPack uses C# source generators for maximum performance:

1. **`[MemoryPackable]` attribute** - Mark types for serialization
2. **`partial` keyword** - Required for source generator output
3. **`[MemoryPackConstructor]`** - For immutable types with constructors
4. **`[MemoryPackOrder]`** - Optional: explicit property ordering

## Project Structure

```
MemoryPackSample/
MemoryPackSample.csproj    # Project file with MemoryPack references
Program.cs                 # Main sample demonstrating all features
appsettings.json           # Configuration for logging
README.md                  # This file
Messages/
   OrderEvents.cs          # MemoryPackable event classes
Handlers/
    OrderEventHandlers.cs  # Event handlers for processing
```

## NativeAOT Deployment

MemoryPack is fully compatible with NativeAOT:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

All serialization code is source-generated at compile time - no runtime reflection required.

## Configuration

MemoryPack is the default serializer for Excalibur.Dispatch. It's automatically registered when using `AddDispatch()`:

```csharp
// MemoryPack is auto-registered as the default
services.AddDispatch();

// Or explicitly add internal serialization
services.AddMemoryPackInternalSerialization();
```

## Related Samples

- [Protobuf Serialization](../Protobuf/) - Schema-based binary serialization
- [MessagePack Serialization](../MessagePackSample/) - Cross-language binary serialization
