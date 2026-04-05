---
sidebar_position: 7
title: Serialization Providers
description: Dedicated setup guides for MemoryPack, MessagePack, Protobuf, and Avro serialization providers.
---

# Serialization Providers

Dispatch supports pluggable serialization via dedicated provider packages. Each provider registers itself with a single DI call that handles everything: DI registration, serializer registry entry, and setting the serializer as current.

For an overview of all serialization options, see [Serialization](./serialization.md).

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the core package plus your chosen provider:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Dispatch.Serialization.MemoryPack  # or MessagePack / Protobuf / Avro
  ```
- Familiarity with [serialization overview](./serialization.md) and [middleware concepts](./index.md)

:::tip Two-layer architecture
Dispatch has two independent serialization layers: **event storage** (`IEventSerializer` -- JSON by default) and **envelope transport** (`ISerializer`). The providers below configure the envelope layer. Your domain events stay as plain POCOs -- no serializer-specific attributes needed. See [Serialization Architecture](./serialization.md#two-layer-serialization-architecture) for details.
:::

## When to Choose Each Provider

| Provider | Package | Binary | Schema | Cross-Language | Performance |
|----------|---------|--------|--------|---------------|-------------|
| System.Text.Json | Built-in | No (JSON) | No | Yes | Good |
| **MemoryPack** | `Excalibur.Dispatch.Serialization.MemoryPack` | Yes | No | .NET only | Fastest |
| **MessagePack** | `Excalibur.Dispatch.Serialization.MessagePack` | Yes | Optional | Yes | Very fast |
| **Protobuf** | `Excalibur.Dispatch.Serialization.Protobuf` | Yes | Required (.proto) | Yes | Fast |
| **Avro** | `Excalibur.Dispatch.Serialization.Avro` | Yes | Required | Yes | Fast |

**Decision guide:**
- **.NET-only, maximum throughput** -> MemoryPack
- **Cross-language, compact binary** -> MessagePack
- **Schema evolution, gRPC integration** -> Protobuf
- **Hadoop/Kafka ecosystem** -> Avro
- **Debugging, human-readable** -> System.Text.Json (default)

---

## MemoryPack

Zero-encoding, zero-allocation binary serializer optimized for .NET. Produces the smallest payloads and fastest serialization for .NET-to-.NET communication.

### Installation

```bash
dotnet add package Excalibur.Dispatch.Serialization.MemoryPack
```

### Registration

```csharp
using Microsoft.Extensions.DependencyInjection;

// One call does everything: registers ISerializer, IBinaryEnvelopeDeserializer,
// adds MemoryPack to the serializer registry, and sets it as current.
services.AddMemoryPackSerializer();
```

:::info No attributes needed on your events
Consumer event types do **not** need `[MemoryPackable]` or any serializer-specific attributes. Only the internal envelope wrapper uses MemoryPack attributes. Your domain events remain plain POCOs.
:::

### Considerations

- .NET-only -- cannot be deserialized by non-.NET consumers
- Ideal for internal event store serialization where all consumers are .NET

---

## MessagePack

Efficient binary serialization with cross-language support. Produces compact payloads while remaining readable by non-.NET consumers.

### Installation

```bash
dotnet add package Excalibur.Dispatch.Serialization.MessagePack
```

### Registration

```csharp
// One call does everything: DI registration, serializer registry, set as current
services.AddMessagePackSerializer();
```

With custom MessagePack options:

```csharp
services.AddMessagePackSerializer(
    MessagePackSerializerOptions.Standard
        .WithCompression(MessagePackCompression.Lz4BlockArray));
```

### Considerations

- Cross-language compatible (C#, Java, Python, Go, JavaScript)
- LZ4 compression option for further size reduction
- Key-based format enables schema evolution (add new fields with new keys)

---

## Protobuf

Google Protocol Buffers serialization for schema-based contracts and gRPC interoperability.

### Installation

```bash
dotnet add package Excalibur.Dispatch.Serialization.Protobuf
```

### Registration

```csharp
// One call does everything: DI registration, serializer registry, set as current
services.AddProtobufSerializer();
```

With custom options:

```csharp
services.AddProtobufSerializer(opts =>
{
    opts.WireFormat = ProtobufWireFormat.Json; // default: Binary
});
```

### Schema Definition

Define messages in `.proto` files:

```protobuf
syntax = "proto3";
package dispatch.events;

message OrderCreatedEvent {
  string event_id = 1;
  string aggregate_id = 2;
  int32 version = 3;
  string occurred_at = 4;
  string event_type = 5;
  map<string, string> metadata = 6;

  string product_name = 7;
  double price = 8;
}
```

### Considerations

- Schema-first contract definition
- Strong versioning and backward compatibility guarantees
- Native gRPC transport compatibility
- Requires `.proto` file management and code generation
- Supports both binary (default) and JSON wire formats via `ProtobufSerializationOptions.WireFormat`

---

## Avro

Apache Avro serialization for schema-based data exchange, commonly used in Kafka and Hadoop ecosystems.

### Installation

```bash
dotnet add package Excalibur.Dispatch.Serialization.Avro
```

### Registration

```csharp
// One call does everything: DI registration, serializer registry, set as current
services.AddAvroSerializer();
```

With custom options:

```csharp
services.AddAvroSerializer(opts =>
{
    opts.BufferSize = 8192; // default: 4096
});
```

### Considerations

- Schema-based contract definition
- Native integration with Kafka and Confluent Schema Registry
- Configurable buffer size for encoding operations (default: 4096 bytes)

---

## Mixing Serializers

You can register multiple serializers -- the last one registered wins as the "current" serializer for new writes. Old data remains readable via its magic byte regardless of which serializer is current.

```csharp
// MemoryPack for internal event store (fastest)
services.AddMemoryPackSerializer();

// If you later switch, old MemoryPack data is still readable
// because the magic byte tells the system which deserializer to use.
```

## See Also

- [Serialization Overview](./serialization.md) -- Core serialization concepts
- [Transports](../transports/index.md) -- Transport-level serialization configuration
