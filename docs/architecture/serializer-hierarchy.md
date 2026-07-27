# Serializer Interface Hierarchy

## 5 Serializer Interfaces

| Interface | Layer | Format | Use Case |
|-----------|-------|--------|----------|
| `IEventSerializer` | EventSourcing | JSON/Binary | Event store persistence, event deserialization during replay |
| `ISerializer` | Dispatch core | Binary (MemoryPack) | High-performance binary envelope serialization |
| `DispatchJsonSerializer` | Dispatch core | JSON (STJ) | Standard JSON serialization for messages and metadata |
| `CompositeAotJsonSerializer` | Dispatch AOT | JSON (STJ source-gen) | AOT-safe serialization using JsonSerializerContext |
| `ICloudEventSerializer` | Transport | CloudEvents JSON | CloudEvents envelope serialization for transport interop |

## Which to Use

- **Storing events?** -> `IEventSerializer`
- **Hot-path binary?** -> `ISerializer` (MemoryPack)
- **Standard JSON?** -> `DispatchJsonSerializer`
- **AOT deployment?** -> `CompositeAotJsonSerializer`
- **CloudEvents transport?** -> `ICloudEventSerializer`
