# Excalibur.Dispatch.Serialization.MemoryPack

High-performance MemoryPack binary serialization for Excalibur framework.

## Purpose

Provides **opt-in** high-performance binary serialization for:
- Maximum throughput scenarios
- .NET-to-.NET communication
- Internal persistence (Outbox, Inbox, Event Store)
- AOT/NativeAOT deployment

## Key Features

- **Opt-In**: JSON (System.Text.Json) is the default serializer (ADR-295). Install this package and register explicitly when you need maximum .NET performance.
- **Serializer ID 1**: Magic byte `0x01` in persisted payloads
- **Zero-Allocation**: ReadOnlySpan-based deserialization
- **AOT-Compatible**: Full NativeAOT and trimming support via source generation

## Usage

### Registration

MemoryPack must be explicitly registered. JSON is the default serializer.

```csharp
services.AddDispatch(dispatch =>
    dispatch.WithSerialization(config =>
    {
        config.Register(new MemoryPackSerializer(), SerializerIds.MemoryPack);
        config.UseMemoryPack();
    }));
```

## Type Requirements

Types must have the `[MemoryPackable]` attribute:

```csharp
[MemoryPackable]
public partial class UserCreatedEvent
{
    public string UserId { get; set; }
    public string Name { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Important**: The `partial` keyword is required for source generation.

### Constructor Parameters

For immutable types:

```csharp
[MemoryPackable]
public partial class OrderPlacedEvent
{
    [MemoryPackConstructor]
    public OrderPlacedEvent(string orderId, decimal total)
    {
        OrderId = orderId;
        Total = total;
    }

    public string OrderId { get; }
    public decimal Total { get; }
}
```

## Performance Characteristics

| Metric | Value | Notes |
|--------|-------|-------|
| Serialization | ~150ns/1KB | Fastest .NET binary format |
| Deserialization | ~120ns/1KB | Zero-allocation span slicing |
| Payload Size | Smallest | Optimal binary encoding |
| Memory Pressure | Minimal | No GC allocations in hot path |

## Pluggable Serialization Integration

MemoryPack is assigned **Serializer ID 1** in the pluggable serialization system.
The magic byte `0x01` prefixes all MemoryPack-serialized payloads.

```
Stored Payload: [0x01][MemoryPack binary data...]
                  ^
                  Magic byte identifies serializer
```

## When to Use MemoryPack

**Best For:**
- Internal .NET-to-.NET communication
- Maximum performance requirements
- Event sourcing and Outbox persistence
- AOT/NativeAOT deployments

**Consider Alternatives When:**
- Cross-language consumers (use MessagePack - ID: 3)
- Debugging/human-readable storage (use System.Text.Json - ID: 2, the default)
- Schema-based contracts (use Protobuf - ID: 4)

## Migration

### From JSON to MemoryPack

```csharp
services.AddDispatch(dispatch =>
    dispatch.WithSerialization(config =>
    {
        // JSON (ID: 2) is already registered as default
        config.Register(new MemoryPackSerializer(), SerializerIds.MemoryPack);
        config.UseMemoryPack();  // Switch new messages to MemoryPack
    }));
```

Old JSON data remains readable; new data uses MemoryPack.

### From MemoryPack to Another Serializer

```csharp
services.AddDispatch(dispatch =>
    dispatch.WithSerialization(config =>
    {
        // Keep MemoryPack registered for reading existing data
        config.Register(new MemoryPackSerializer(), SerializerIds.MemoryPack);
        config.UseSystemTextJson();  // Switch new messages to JSON
    }));
```

## Package Dependencies

- `MemoryPack` - Core MemoryPack serialization runtime
- `Excalibur.Dispatch.Abstractions` - Core contracts

## AOT Compatibility

**Full NativeAOT support** with source-generated serializers.

Ensure all serializable types have `[MemoryPackable]` attribute with `partial` class declaration.

## See Also

- [MemoryPack Documentation](https://github.com/Cysharp/MemoryPack)
