# Excalibur.Dispatch.Serialization.MemoryPack

High-performance MemoryPack binary serialization for Excalibur framework.

## Purpose

Provides the **default** and fastest binary serialization for:
- Internal persistence (Outbox, Inbox, Event Store)
- Maximum throughput scenarios
- .NET-to-.NET communication
- AOT/NativeAOT deployment

## Key Features

- **Auto-Registered**: Enabled by default when using `AddDispatch()`
- **Serializer ID 1**: Magic byte `0x01` in persisted payloads
- **Zero-Allocation**: ReadOnlySpan-based deserialization
- **AOT-Compatible**: Full NativeAOT and trimming support

## Usage

### Default (Zero Configuration)

MemoryPack is automatically registered and used as the default serializer:

```csharp
services.AddDispatch();
// That's it! MemoryPack is auto-registered and set as current.
```

### Explicit Registration

For clarity or migration scenarios:

```csharp
services.AddDispatch()
    .ConfigurePluggableSerialization(config =>
    {
        config.RegisterMemoryPack();  // Register (ID: 1)
        config.UseMemoryPack();       // Set as current
    });
```

### Disable Auto-Registration

When you want explicit control:

```csharp
services.AddDispatch()
    .ConfigurePluggableSerialization(config =>
    {
        config.DisableMemoryPackAutoRegistration();
        config.RegisterSystemTextJson();
        config.UseSystemTextJson();
    });
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

## When to Use MemoryPack (Default)

**Best For:**
- Internal .NET-to-.NET communication
- Maximum performance requirements
- Event sourcing and Outbox persistence
- AOT/NativeAOT deployments

**Consider Alternatives When:**
- Cross-language consumers (use MessagePack - ID: 3)
- Debugging/human-readable storage (use System.Text.Json - ID: 2)
- Schema-based contracts (use Protobuf - ID: 4)

## Migration

### From MemoryPack to Another Serializer

```csharp
services.AddDispatch()
    .ConfigurePluggableSerialization(config =>
    {
        // MemoryPack (ID: 1) auto-registered - keep for existing data
        config.RegisterMessagePack();     // Add new serializer
        config.UseMessagePack();          // Use for new messages
    });
```

Old MemoryPack data remains readable; new data uses MessagePack.

### To MemoryPack from Another Serializer

```csharp
services.AddDispatch()
    .ConfigurePluggableSerialization(config =>
    {
        config.RegisterSystemTextJson(); // Keep for existing data
        config.UseMemoryPack();          // Switch to MemoryPack
    });
```

## Package Dependencies

- `MemoryPack` - Core MemoryPack serialization runtime
- `Excalibur.Dispatch.Abstractions` - Core contracts

## AOT Compatibility

**Full NativeAOT support** with source-generated serializers.

Ensure all serializable types have `[MemoryPackable]` attribute with `partial` class declaration.

## See Also

- [MemoryPack Documentation](https://github.com/Cysharp/MemoryPack)
