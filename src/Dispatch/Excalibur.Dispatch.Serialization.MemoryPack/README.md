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
- **No consumer attributes needed**: Consumer event types do NOT need `[MemoryPackable]`. Only the internal envelope wrapper uses MemoryPack attributes.

## Usage

### Registration

One call does everything -- DI registration, serializer registry entry, and setting MemoryPack as the current serializer:

```csharp
services.AddMemoryPackSerializer();
```

That is all you need. JSON is the default serializer; calling `AddMemoryPackSerializer()` opts you into MemoryPack for high-performance binary serialization.

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
// One call switches new messages to MemoryPack.
// Old JSON data remains readable via its magic byte.
services.AddMemoryPackSerializer();
```

### From MemoryPack to Another Serializer

```csharp
// Switch to a different serializer. Old MemoryPack data remains
// readable because the magic byte tells the system which deserializer to use.
services.AddMessagePackSerializer();
```

## Package Dependencies

- `MemoryPack` - Core MemoryPack serialization runtime
- `Excalibur.Dispatch.Abstractions` - Core contracts

## AOT Compatibility

**Full NativeAOT support** with source-generated serializers.

The internal envelope types use `[MemoryPackable]` with `partial` class declarations for source generation. Consumer event types do not need any attributes.

## See Also

- [MemoryPack Documentation](https://github.com/Cysharp/MemoryPack)
