# Excalibur.Dispatch.Serialization.Protobuf

Opt-in Protocol Buffers serialization support for the Excalibur framework.

## Purpose

Provides Protocol Buffers (Protobuf) serialization for:
- Google Cloud Platform (GCP) interoperability
- AWS service integration requiring Protobuf
- High-performance binary protocols
- External systems using `.proto` schema definitions

## Requirements Alignment

This package satisfies the following framework requirements:

- **R0.14**: Serializer segregation (opt-in package, NOT in Excalibur.Dispatch)
- **R0.5**: Transitive bloat guard (pay-for-play; only referenced when explicitly needed)
- **R9.44**: Internal wire uses MemoryPack (Core unchanged; Protobuf is opt-in only)
- **R9.46**: Opt-in binary alternatives (this package provides Protobuf as opt-in)
- **R9.47**: AOT/trim safety (source-generated Protobuf types are trim-safe)

## Installation

```bash
dotnet add package Excalibur.Dispatch.Serialization.Protobuf
```

## Usage

### Basic Registration

```csharp
using Excalibur.Dispatch.Serialization.Protobuf;

// In your service configuration (Program.cs or Startup.cs):
services.AddDispatch()
    .AddProtobufSerialization(); // Opt-in to Protobuf support
```

### Configure Protobuf Options

```csharp
services.AddProtobufSerialization(options =>
{
    // Wire format: Binary (default) or JSON
    options.WireFormat = ProtobufWireFormat.Binary;

    // Ignore missing fields during deserialization (default: true)
    options.IgnoreMissingFields = true;

    // Preserve unknown fields (default: false)
    options.PreserveUnknownFields = false;
});
```

### Message Definition

Your message types must implement `Google.Protobuf.IMessage` and have source-generated parsers:

```csharp
using Google.Protobuf;

// Example: Generated from .proto file
public partial class UserCreatedEvent : IMessage<UserCreatedEvent>
{
    // Source-generated Protobuf code
}
```

### Serialization Example

```csharp
using Excalibur.Dispatch.Abstractions.Serialization;

public class UserEventHandler
{
    private readonly IMessageSerializer _serializer;

    public UserEventHandler(IMessageSerializer serializer)
    {
        _serializer = serializer;
    }

    public async Task HandleAsync(UserCreatedEvent evt, CancellationToken ct)
    {
        // Serialize to binary Protobuf
        byte[] data = _serializer.Serialize(evt);

        // Deserialize from binary
        var deserialized = _serializer.Deserialize<UserCreatedEvent>(data);
    }
}
```

## Package Dependencies

- **Google.Protobuf** - Protocol Buffers runtime
- **Excalibur.Dispatch.Abstractions** - Core contracts only (no Excalibur.Dispatch dependency)
- **MemoryPack** - Used internally per R9.44 (envelope/headers remain MemoryPack)

## AOT Compatibility

✅ **Native AOT compatible** with source-generated Protobuf types.

Ensure your `.proto` files are compiled with `protoc` to generate C# code, and the generated types will be trim-safe.

## Wire Formats

### Binary (Default - Recommended)

```csharp
options.WireFormat = ProtobufWireFormat.Binary;
```

- Compact binary format
- Fastest serialization/deserialization
- Best for internal/transport use

### JSON

```csharp
options.WireFormat = ProtobufWireFormat.Json;
```

- Human-readable JSON representation
- Useful for debugging or external API boundaries
- Slower than binary format

## When to Use This Package

**Use Protobuf serialization when:**
- Integrating with GCP services (Cloud Pub/Sub, Cloud Functions, etc.)
- AWS services require Protobuf (EventBridge, Kinesis with Protobuf schema)
- External systems expose Protobuf schemas (`.proto` files)
- You need schema evolution with backward/forward compatibility

**Do NOT use Protobuf when:**
- Internal Excalibur.Dispatch messaging (use MemoryPack - the default)
- Public HTTP/REST APIs (use System.Text.Json per R9.45)
- No Protobuf schema requirements exist

## Architecture Notes

Per Excalibur framework requirements:

1. **Excalibur.Dispatch MUST NOT reference this package** (R0.14 compliance)
2. **This package is pay-for-play** (R0.5: no transitive bloat)
3. **MemoryPack remains the internal wire format** (R9.44)
4. **System.Text.Json is used for public edges** (R9.45)
5. **This package is opt-in only** (R9.46)

## Performance Characteristics

- **Serialization**: ~100-500 ns/op (binary), ~1-5 μs/op (JSON)
- **Allocations**: Minimal (reuses buffers where possible)
- **Throughput**: ~1-5 million msg/sec (binary), ~200k-1M msg/sec (JSON)

Benchmark results available in: `management/perf/perf-baselines.json`

## Schema Evolution

Protobuf supports schema evolution via:

- **Field numbers**: Never reuse field numbers
- **Reserved fields**: Mark removed fields as `reserved`
- **Optional fields**: Use `optional` for nullable fields
- **Unknown fields**: Preserved if `PreserveUnknownFields = true`

Example `.proto`:

```protobuf
syntax = "proto3";

message UserCreatedEvent {
  string user_id = 1;
  string email = 2;
  reserved 3; // Removed field
  // New field with number 4
  string full_name = 4;
}
```

## License

This package is licensed under the same licenses as the Excalibur framework:
- Excalibur License 1.0
- GNU Affero General Public License v3.0 or later (AGPL-3.0)
- Server Side Public License v1.0 (SSPL-1.0)
- Apache License 2.0

See LICENSE files in project root for details.

## Support

For issues, questions, or contributions, visit:
- GitHub: https://github.com/TrigintaFaces/Excalibur
- Documentation: (link to docs when published)
