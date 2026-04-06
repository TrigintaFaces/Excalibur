# Versioning API Reference

This section documents the public APIs for Universal Message Upcasting in Excalibur.Dispatch.

## Overview

Universal Message Upcasting provides schema evolution support for all message types through a unified, high-performance pipeline with automatic path finding.

## Interfaces (Excalibur.Dispatch.Abstractions)

| Interface | Description |
|-----------|-------------|
| [IVersionedMessage](./IVersionedMessage.md) | Marker interface for versioned messages |
| [IMessageUpcaster<TOld, TNew>](./IMessageUpcaster.md) | Type-safe transformation between versions |
| [IUpcastingPipeline](./IUpcastingPipeline.md) | Orchestrates multi-hop upcasting with BFS |

## Classes (Dispatch)

| Class | Description |
|-------|-------------|
| [UpcastingBuilder](./UpcastingBuilder.md) | Fluent builder for configuring upcasters |
| [UpcastingPipeline](./IUpcastingPipeline.md#implementation-upcastingpipeline) | Thread-safe implementation with path caching |

## Extension Methods

| Method | Description |
|--------|-------------|
| [AddMessageUpcasting](./AddMessageUpcasting.md) | DI registration extension |

## Quick Reference

### Register Upcasters

```csharp
services.AddMessageUpcasting(builder =>
{
    // Option 1: Manual registration
    builder.RegisterUpcaster(new UserEventV1ToV2());

    // Option 2: Assembly scanning
    builder.ScanAssembly(typeof(Program).Assembly);

    // Option 3: With DI
    builder.RegisterUpcaster<AddressV1, AddressV2>(sp =>
        new AddressV1ToV2(sp.GetRequiredService<IMapper>()));
});
```

### Implement Upcaster

```csharp
public class UserEventV1ToV2 : IMessageUpcaster<UserEventV1, UserEventV2>
{
    public int FromVersion => 1;
    public int ToVersion => 2;

    public UserEventV2 Upcast(UserEventV1 old) => new UserEventV2
    {
        EventId = old.EventId,
        AggregateId = old.AggregateId,
        // ... transform properties
    };
}
```

### Use Pipeline

```csharp
var pipeline = serviceProvider.GetRequiredService<IUpcastingPipeline>();
var latest = pipeline.Upcast(oldEvent);
```

## Performance

- **~1ns passthrough** for non-versioned messages
- **~13-18ns path lookup** with O(1) cached access
- **~90-105ns per transformation** (includes message allocation)
- **Zero allocations** in path lookup hot path

## See Also

- [Developer Guide](../../versioning/universal-upcasting-guide.md) - Step-by-step tutorial
- [ADR-068](../../../management/architecture/ADR-068-Universal-Message-Upcasting-Architecture.md) - Architecture decision
