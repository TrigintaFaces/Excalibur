---
sidebar_position: 7
title: Serialization Providers
description: Dedicated setup guides for MemoryPack, MessagePack, and Protobuf serialization providers.
---

# Serialization Providers

Dispatch supports pluggable serialization via dedicated provider packages. Each provider implements `IEventSerializer` and can be registered for internal serialization, transport serialization, or both.

For an overview of all serialization options, see [Serialization](./serialization.md).

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the core package plus your chosen provider:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Dispatch.Serialization.MemoryPack  # or MessagePack / Protobuf
  ```
- Familiarity with [serialization overview](./serialization.md) and [middleware concepts](./index.md)

## When to Choose Each Provider

| Provider | Package | Binary | Schema | Cross-Language | Performance |
|----------|---------|--------|--------|---------------|-------------|
| System.Text.Json | Built-in | No (JSON) | No | Yes | Good |
| **MemoryPack** | `Excalibur.Dispatch.Serialization.MemoryPack` | Yes | No | .NET only | Fastest |
| **MessagePack** | `Excalibur.Dispatch.Serialization.MessagePack` | Yes | Optional | Yes | Very fast |
| **Protobuf** | `Excalibur.Dispatch.Serialization.Protobuf` | Yes | Required (.proto) | Yes | Fast |

**Decision guide:**
- **.NET-only, maximum throughput** → MemoryPack
- **Cross-language, compact binary** → MessagePack
- **Schema evolution, gRPC integration** → Protobuf
- **Debugging, human-readable** → System.Text.Json (default)

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

// Register MemoryPack for internal serialization
services.AddMemoryPackInternalSerialization();
```

### Message Annotation

MemoryPack requires the `[MemoryPackable]` attribute on message types:

```csharp
using MemoryPack;

[MemoryPackable]
public partial class OrderCreatedEvent : IDomainEvent
{
    public Guid EventId { get; init; }
    public Guid AggregateId { get; init; }
    public int Version { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public string EventType => nameof(OrderCreatedEvent);
    public IDictionary<string, string>? Metadata { get; init; }

    public string ProductName { get; init; } = default!;
    public decimal Price { get; init; }
}
```

### Considerations

- .NET-only — cannot be deserialized by non-.NET consumers
- Requires `partial` class declarations for source generation
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
using Microsoft.Extensions.DependencyInjection;

// Basic registration
services.AddMessagePackSerialization();

// With custom resolver
services.AddMessagePackSerialization<MyCustomResolver>();

// Pluggable (replaces default IEventSerializer)
services.AddMessagePackPluggableSerialization();

// Pluggable with options
services.AddMessagePackPluggableSerialization(options =>
{
    options.EnableLZ4Compression = true;
});
```

### Message Annotation

MessagePack uses key-based attributes:

```csharp
using MessagePack;

[MessagePackObject]
public class OrderCreatedEvent : IDomainEvent
{
    [Key(0)] public Guid EventId { get; init; }
    [Key(1)] public Guid AggregateId { get; init; }
    [Key(2)] public int Version { get; init; }
    [Key(3)] public DateTimeOffset OccurredAt { get; init; }
    [Key(4)] public string EventType => nameof(OrderCreatedEvent);
    [Key(5)] public IDictionary<string, string>? Metadata { get; init; }

    [Key(6)] public string ProductName { get; init; } = default!;
    [Key(7)] public decimal Price { get; init; }
}
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
using Microsoft.Extensions.DependencyInjection;

services.AddProtobufSerialization();
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

---

## Mixing Serializers

You can use different serializers for different purposes:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// MemoryPack for internal event store (fastest)
services.AddMemoryPackInternalSerialization();

// MessagePack for transport serialization (cross-language)
services.AddMessagePackPluggableSerialization();
```

## See Also

- [Serialization Overview](./serialization.md) — Core serialization concepts
- [Transports](../transports/index.md) — Transport-level serialization configuration
