# Excalibur.Dispatch.Serialization.MessagePack

Opt-in MessagePack serialization support for Excalibur framework.

## Purpose

Provides high-performance MessagePack binary serialization for:
- Ultra-low latency scenarios
- Bandwidth-constrained environments
- Cross-language interoperability (C++, Python, Rust, etc.)

## Usage

### Registration (Single Call)

One call does everything -- DI registration, serializer registry entry, and setting MessagePack as the current serializer:

```csharp
services.AddMessagePackSerializer();
```

### With Custom Options

Pass native `MessagePackSerializerOptions` for custom configuration:

```csharp
services.AddMessagePackSerializer(
    MessagePackSerializerOptions.Standard
        .WithCompression(MessagePackCompression.Lz4BlockArray));
```

## Package Dependencies

- `MessagePack` - MessagePack binary serialization runtime
- `Excalibur.Dispatch.Abstractions` - Core contracts only (no Core dependency)

## AOT Compatibility

**Native AOT compatible** with source-generated formatters and resolvers.

## Performance Characteristics

- **Throughput**: ~2-5x faster than JSON for binary payloads
- **Compression**: Built-in LZ4 support for bandwidth optimization
- **Latency**: Sub-microsecond serialization for small messages

## Architecture Compliance

This package follows the Dispatch serialization policy (R0.14):

- **Core** (`Excalibur.Dispatch`) - System.Text.Json (default, ADR-295)
- **Public Edges** (`Excalibur.Dispatch.Hosting.Web`, etc.) - System.Text.Json with source-gen
- **Opt-In Alternatives** (`Excalibur.Dispatch.Serialization.*`) - Pay-for-play binary serializers

MessagePack is **not** included in `Excalibur.Dispatch` to avoid transitive bloat. Consumers must explicitly opt-in by:
1. Adding `<PackageReference Include="Excalibur.Dispatch.Serialization.MessagePack" />`
2. Calling `services.AddMessagePackSerializer()`

## When to Use

**Use MessagePack when:**
- You need maximum throughput and minimum latency
- You're sending binary data across services
- You need cross-language serialization (non-.NET clients)
- You want bandwidth-efficient wire formats

**Use System.Text.Json when:**
- You need human-readable message formats
- You're exposing HTTP APIs to external consumers
- You require JSON Schema or OpenAPI compatibility

**Use MemoryPack when:**
- You're building internal .NET-to-.NET communication
- You need the absolute fastest .NET binary serialization
- You control both producer and consumer code

## Pluggable Serialization Integration

MessagePack is assigned **Serializer ID 3** in the pluggable serialization system.
The magic byte `0x03` prefixes all MessagePack-serialized payloads.

**Migration Support**: When switching from another serializer (e.g., MemoryPack):
1. Call `services.AddMessagePackSerializer()` -- old data remains readable via magic byte
2. New messages use MessagePack
3. No data migration needed for backward compatibility

## See Also

- [Excalibur.Dispatch.Serialization.Protobuf](../Excalibur.Dispatch.Serialization.Protobuf/README.md) - Protocol Buffers opt-in package
- [MessagePack Specification](https://msgpack.org/)
