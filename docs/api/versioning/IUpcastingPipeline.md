# IUpcastingPipeline Interface

**Namespace:** `Excalibur.Dispatch.Abstractions.Versioning`
**Assembly:** `Excalibur.Dispatch.Abstractions`

Manages automatic message version migration using registered upcasters.

## Definition

```csharp
public interface IUpcastingPipeline
{
    IDispatchMessage Upcast(IDispatchMessage message);
    IDispatchMessage UpcastTo(IDispatchMessage message, int targetVersion);
    void Register<TOld, TNew>(IMessageUpcaster<TOld, TNew> upcaster)
        where TOld : IDispatchMessage, IVersionedMessage
        where TNew : IDispatchMessage, IVersionedMessage;
    bool CanUpcast(string messageType, int fromVersion, int toVersion);
    int GetLatestVersion(string messageType);
}
```

## Methods

### Upcast

Upcasts a message to the latest registered version for its type.

```csharp
IDispatchMessage Upcast(IDispatchMessage message);
```

**Parameters:**
- `message`: The message to upcast (any version, any type)

**Returns:**
The message upcasted to the latest version, or the original if already latest.

**Remarks:**
- If no upcasters are registered for the message type, returns the original unchanged
- Non-versioned messages (not implementing `IVersionedMessage`) pass through unchanged

### UpcastTo

Upcasts a message to a specific target version.

```csharp
IDispatchMessage UpcastTo(IDispatchMessage message, int targetVersion);
```

**Parameters:**
- `message`: The message to upcast
- `targetVersion`: The desired version

**Returns:**
The message at the target version.

**Exceptions:**
- `InvalidOperationException`: When no path exists to the target version, or when attempting to downcast (target < source)

### Register

Registers a generic message upcaster for a specific version transition.

```csharp
void Register<TOld, TNew>(IMessageUpcaster<TOld, TNew> upcaster)
    where TOld : IDispatchMessage, IVersionedMessage
    where TNew : IDispatchMessage, IVersionedMessage;
```

**Parameters:**
- `upcaster`: The upcaster instance

**Remarks:**
- Registration should typically happen during application startup via dependency injection
- Multiple upcasters can be registered to form a chain (V1 → V2 → V3 → V4)
- After registration, paths are automatically computed using BFS

### CanUpcast

Checks if a path exists between two versions for a message type.

```csharp
bool CanUpcast(string messageType, int fromVersion, int toVersion);
```

**Parameters:**
- `messageType`: The logical message type name
- `fromVersion`: The source version
- `toVersion`: The target version

**Returns:**
`true` if a path exists (including multi-hop); otherwise `false`.

### GetLatestVersion

Gets the latest registered version for a message type.

```csharp
int GetLatestVersion(string messageType);
```

**Parameters:**
- `messageType`: The logical message type name

**Returns:**
The highest version number, or 0 if no versions registered.

## Implementation: UpcastingPipeline

**Namespace:** `Dispatch.Versioning`
**Assembly:** `Dispatch`

Thread-safe implementation with BFS path finding.

### Features

- **BFS Path Finding**: Finds shortest path between any two versions
- **Path Caching**: O(1) lookup after first computation
- **Thread Safety**: `ReaderWriterLockSlim` for concurrent reads
- **Negative Caching**: Caches "no path" results to avoid repeated computation

### Performance

| Operation | Time Complexity | Typical Performance |
|-----------|----------------|---------------------|
| Non-versioned passthrough | O(1) | ~1ns |
| Path lookup (cached) | O(1) | ~13-18ns |
| Single-hop transformation | O(1) | ~90-105ns (includes allocation) |
| Multi-hop (V1→V4) | O(hops) | ~270-315ns |
| Path computation (first time) | O(V + E) | Depends on graph size |
| Registration | O(1) | ~100ns |

### Thread Safety

- Read operations are concurrent (read lock)
- Write operations (registration) are exclusive (write lock)
- Path cache uses `ConcurrentDictionary` for lock-free reads

## Usage

### Basic Usage

```csharp
// Get pipeline from DI
var pipeline = serviceProvider.GetRequiredService<IUpcastingPipeline>();

// Upcast to latest version
var latestEvent = pipeline.Upcast(oldEvent);

// Upcast to specific version
var v2Event = pipeline.UpcastTo(v1Event, targetVersion: 2);

// Check if path exists
if (pipeline.CanUpcast("UserCreatedEvent", fromVersion: 1, toVersion: 4))
{
    // Safe to upcast
}

// Get latest version
int latest = pipeline.GetLatestVersion("UserCreatedEvent");
```

### Event Store Integration

```csharp
public class EventSourcedRepository<T> where T : AggregateRoot
{
    private readonly IEventStore _eventStore;
    private readonly IUpcastingPipeline _upcastingPipeline;

    public async Task<T> GetAsync(Guid aggregateId)
    {
        var events = await _eventStore.GetEventsAsync(aggregateId);

        // Upcast all events to latest versions
        var upcastedEvents = events
            .Select(e => (IDomainEvent)_upcastingPipeline.Upcast(e))
            .ToList();

        var aggregate = CreateAggregate();
        aggregate.LoadFromHistory(upcastedEvents);
        return aggregate;
    }
}
```

### Message Bus Integration

```csharp
public class UpcastingMessageBusDecorator : IMessageBus
{
    private readonly IMessageBus _inner;
    private readonly IUpcastingPipeline _pipeline;

    public async Task PublishAsync<TEvent>(TEvent @event)
        where TEvent : IIntegrationEvent
    {
        // Upcast before publishing
        var upcasted = _pipeline.Upcast(@event);
        await _inner.PublishAsync(upcasted);
    }
}
```

### Diagnostic Usage

```csharp
// Check registered versions
var latestUserEvent = pipeline.GetLatestVersion("UserCreatedEvent");
Console.WriteLine($"Latest UserCreatedEvent version: {latestUserEvent}");

// Verify paths exist
var canUpgrade = pipeline.CanUpcast("OrderPlacedEvent", 1, 4);
Console.WriteLine($"Can upcast OrderPlacedEvent V1→V4: {canUpgrade}");
```

## Algorithm Details

### BFS Path Finding

The pipeline uses breadth-first search to find the shortest path between versions:

```
Graph: UserCreatedEvent
  V1 ──→ V2 ──→ V3 ──→ V4
          │
          └──→ V3-alt (branching path)

BFS from V1 to V4:
  Visit V1, queue neighbors [V2]
  Visit V2, queue neighbors [V3, V3-alt]
  Visit V3, queue neighbors [V4]
  Visit V4 - FOUND! Path: [V2, V3, V4]
```

### Path Caching

After BFS computation, paths are cached:

```csharp
// Cache key: (messageType, fromVersion, toVersion)
// Cache value: List of version hops

// Example cached path for UserCreatedEvent V1→V4:
("UserCreatedEvent", 1, 4) → [2, 3, 4]

// Subsequent lookups are O(1)
```

## See Also

- [IVersionedMessage](./IVersionedMessage.md) - Message versioning marker
- [IMessageUpcaster](./IMessageUpcaster.md) - Transform between versions
- [UpcastingBuilder](./UpcastingBuilder.md) - Fluent registration API
- [AddMessageUpcasting](./AddMessageUpcasting.md) - DI extension method
