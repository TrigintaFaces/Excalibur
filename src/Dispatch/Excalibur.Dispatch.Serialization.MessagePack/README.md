# Excalibur.Dispatch.Serialization.MessagePack

Opt-in MessagePack serialization support for Dispatch framework.

## Purpose

Provides high-performance MessagePack binary serialization for:
- Ultra-low latency scenarios
- Bandwidth-constrained environments
- Cross-language interoperability (C++, Python, Rust, etc.)
- Zero-allocation serialization with `MessagePackZeroCopySerializer`

## Requirements Alignment

- **R0.14**: Serializer segregation (opt-in package)
- **R0.5**: Transitive bloat guard (pay-for-play)
- **R9.44**: Internal wire uses MemoryPack (Core unchanged)
- **R9.46**: Opt-in binary alternatives
- **R9.47**: AOT/trim safety

## Usage

### Pluggable Serialization (Recommended)

Use the pluggable serialization builder for internal persistence:

```csharp
services.AddDispatch()
    .ConfigurePluggableSerialization(config =>
    {
        config.RegisterMessagePack();  // Register MessagePack (ID: 3)
        config.UseMessagePack();       // Use for new messages
    });
```

### Standalone Registration

```csharp
services.AddDispatch()
    .ConfigureServices(s => s.AddMessagePackSerialization()); // Opt-in
```

### With Custom Options

```csharp
services.AddDispatch()
    .ConfigureServices(s => s.AddMessagePackSerialization(options =>
    {
        options.UseLz4Compression = true;
    }));
```

### Using Specific Serializer Implementation

```csharp
// Use AOT-compatible serializer
services.AddMessagePackSerialization<AotMessagePackSerializer>();

// Use general-purpose serializer
services.AddMessagePackSerialization<DispatchMessagePackSerializer>();

// Use options-based serializer
services.AddMessagePackSerialization<MessagePackMessageSerializer>(options =>
{
    options.UseLz4Compression = true;
    options.IncludePrivateMembers = false;
});
```

## Serializer Implementations

| Serializer | Purpose | Performance | AOT Support |
|------------|---------|-------------|-------------|
| `MessagePackZeroCopySerializer` | **Default** - zero allocations | Fastest | Full |
| `AotMessagePackSerializer` | AOT-optimized with source-gen | Fast | Full |
| `DispatchMessagePackSerializer` | General-purpose | Standard | Full |
| `MessagePackMessageSerializer` | Options-based configuration | Standard | Full |

## Package Dependencies

- `MessagePack` - MessagePack binary serialization runtime
- `Excalibur.Dispatch.Abstractions` - Core contracts only (no Core dependency)

## AOT Compatibility

**Native AOT compatible** with source-generated formatters and resolvers.

Use `[MessagePackObject]` and `[Key]` attributes with source generators for optimal performance.

## Performance Characteristics

- **Zero-allocation mode**: No GC pressure in hot paths
- **Throughput**: ~2-5x faster than JSON for binary payloads
- **Compression**: Built-in LZ4 support for bandwidth optimization
- **Latency**: Sub-microsecond serialization for small messages

## Architecture Compliance

This package follows the Dispatch serialization policy (R0.14):

- **Core** (`Excalibur.Dispatch`) - MemoryPack only (internal wire format)
- **Public Edges** (`Excalibur.Dispatch.Hosting.Web`, etc.) - System.Text.Json with source-gen
- **Opt-In Alternatives** (`Excalibur.Dispatch.Serialization.*`) - Pay-for-play binary serializers

MessagePack is **not** included in `Excalibur.Dispatch` to avoid transitive bloat. Consumers must explicitly opt-in by:
1. Adding `<PackageReference Include="Excalibur.Dispatch.Serialization.MessagePack" />`
2. Calling `services.AddMessagePackSerialization()`

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

## Migration from Excalibur.Dispatch

**Before (R0.14 violation):**
```csharp
// Old code - MessagePack was in Excalibur.Dispatch
services.AddDispatch()
    .Serialization()
    .UseMessagePack();
```

**After (R0.14 compliant):**
```csharp
// New code - MessagePack is opt-in
services.AddDispatch()
    .ConfigureServices(s => s.AddMessagePackSerialization());
```

## Pluggable Serialization Integration

MessagePack is assigned **Serializer ID 3** in the pluggable serialization system.
The magic byte `0x03` prefixes all MessagePack-serialized payloads.

**Migration Support**: When switching from another serializer (e.g., MemoryPack):
1. Register MessagePack in addition to existing serializer
2. Switch to MessagePack for new messages
3. Old messages remain readable via their magic byte

## See Also

- [Excalibur.Dispatch.Serialization.Protobuf](../Excalibur.Dispatch.Serialization.Protobuf/README.md) - Protocol Buffers opt-in package
- [MessagePack Specification](https://msgpack.org/)
