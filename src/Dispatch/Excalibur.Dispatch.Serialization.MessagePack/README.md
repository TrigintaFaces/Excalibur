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
        .WithSecurity(MessagePackSecurity.UntrustedData)
        .WithCompression(MessagePackCompression.Lz4BlockArray));
```

## Package Dependencies

- `MessagePack` 3.1.9 - MessagePack binary serialization runtime
- `Excalibur.Dispatch.Abstractions` and `Excalibur.Dispatch` - Framework contracts and integration

## Untrusted Input

The no-options registration uses `MessagePackSecurity.UntrustedData`. When supplying
custom options, select the security profile explicitly, as in the example above.
The 3.1.9 dependency includes the upstream fix for nested arrays reusing unread bytes
to justify excessive allocation ([GHSA-qhrr-8q5h-9q3h](https://github.com/MessagePack-CSharp/MessagePack-CSharp/security/advisories/GHSA-qhrr-8q5h-9q3h)).
Depth and message-size limits remain useful, but are not substitutes for the patched runtime.

## AOT Compatibility

**Not validated under Native AOT.** MessagePack-CSharp resolves formatters through runtime code
generation (`StandardResolver`), so this package does not declare ahead-of-time compatibility.

## Performance Characteristics

- **Compression**: Built-in LZ4 support for bandwidth optimization
- **Measurements**: Throughput and latency depend on payload, resolver and options.
  The framework benchmark refresh is in progress; current matched measurements are
  required before publishing a JSON comparison or latency guarantee.

## Architecture Compliance

This package follows the Dispatch serialization policy:

- **Core** (`Excalibur.Dispatch`) - System.Text.Json (default)
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
- You want a .NET-focused binary serializer
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

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
