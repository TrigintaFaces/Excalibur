# Excalibur.Dispatch.Serialization.MemoryPack

High-performance MemoryPack binary serialization for Excalibur framework.

## Purpose

Provides **opt-in** high-performance binary serialization for:
- Workloads where matched measurements favor this binary format
- .NET-to-.NET communication
- Internal persistence (Outbox, Inbox, Event Store)
- Deployments whose concrete payload and API compatibility has been verified

## Key Features

- **Opt-In**: JSON (System.Text.Json) is the default serializer. Install this package and register explicitly when its format suits your workload.
- **Serializer ID 1**: Magic byte `0x01` in persisted payloads
- **Span-Based Input**: ReadOnlySpan-based deserialization; output objects and envelope processing can allocate
- **Source-generated formatters**: Custom payload types normally need `[MemoryPackable]` generation or a registered custom formatter. Supported built-in types need not all carry attributes. Internal envelope attributes do not cover arbitrary consumer payloads.
- **NativeAOT verification required**: The framework serializer currently carries dynamic-code and trimming annotations. Generated formatters alone do not establish compatibility of every wrapper API or consumer type.

## Usage

### Registration

One call does everything -- DI registration, serializer registry entry, and setting MemoryPack as the current serializer:

```csharp
services.AddMemoryPackSerializer();
```

That is all you need. JSON is the default serializer; calling `AddMemoryPackSerializer()` opts you into MemoryPack for high-performance binary serialization.

## Performance Characteristics

The [October 16 verification report](../../../docs/performance/memorypack-2026-10-16.md)
records scoped first-pass results for the framework's envelope APIs, including
712–65,704 allocated bytes per accepted outbox-envelope deserialization operation.
Two cases failed and three finite sibling results remain withheld. These measurements
do not establish a general serializer ranking, zero-allocation guarantee, repeated
latency comparison or NativeAOT performance. Measure the actual payload and API
before selecting a serializer for a performance requirement.

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
- Payloads whose measured latency, allocations and size meet your requirements
- Event sourcing and Outbox persistence
- Concrete deployment paths verified with the selected payload types

**Consider Alternatives When:**
- Cross-language consumers (use MessagePack - ID: 3)
- Debugging/human-readable storage (use System.Text.Json - ID: 2, the default)
- Schema-based contracts (use Protobuf - ID: 4)

## Migration

### From JSON to MemoryPack

```csharp
// One call switches new messages to MemoryPack.
// Keep the JSON serializer registered to read old JSON data via its magic byte.
services.AddMemoryPackSerializer();
```

### From MemoryPack to Another Serializer

```csharp
// Switch to a different serializer. Old MemoryPack data remains
// readable only while its serializer remains registered; the magic byte selects it.
services.AddMessagePackSerializer();
```

## Package Dependencies

- `MemoryPack` - Core MemoryPack serialization runtime
- `Excalibur.Dispatch.Abstractions` - Core contracts

## AOT Compatibility

The internal envelope types use `[MemoryPackable]` with `partial` declarations. Custom consumer payload types also need compatible generated or registered formatters when passed directly to MemoryPack. See MemoryPack's [supported built-in types](https://github.com/Cysharp/MemoryPack#built-in-supported-types) and [external-type serialization](https://github.com/Cysharp/MemoryPack#serialize-external-types).

The framework wrapper currently declares `RequiresDynamicCode` and `RequiresUnreferencedCode`. Verify the exact consumer payloads and APIs in a native executable with trimming and AOT warnings enabled before making a compatibility claim. The managed performance observations above are not NativeAOT certification.

## See Also

- [MemoryPack Documentation](https://github.com/Cysharp/MemoryPack)

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
