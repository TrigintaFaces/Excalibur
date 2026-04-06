# IVersionedMessage Interface

**Namespace:** `Excalibur.Dispatch.Abstractions.Versioning`
**Assembly:** `Excalibur.Dispatch.Abstractions`

Marker interface for messages that support explicit versioning.

## Definition

```csharp
public interface IVersionedMessage
{
    int Version { get; }
    string MessageType { get; }
}
```

## Properties

### Version

Gets the schema version of this message.

```csharp
int Version { get; }
```

**Remarks:**
- Version numbers should start at 1 and increment sequentially
- Breaking changes require a version increment
- Non-breaking changes (additive) don't require version changes

### MessageType

Gets the logical message type name (version-independent).

```csharp
string MessageType { get; }
```

**Remarks:**
- This should remain constant across all versions of the same message
- Used by `IUpcastingPipeline` to identify which upcasters apply
- Convention: Use the base name without version suffix

**Examples:**
- `"UserCreatedEvent"` for `UserCreatedEventV1`, `UserCreatedEventV2`, `UserCreatedEventV3`
- `"OrderPlacedEvent"` for `OrderPlacedEventV1`, `OrderPlacedEventV2`

## Usage

### Basic Implementation

```csharp
public record UserCreatedEventV1 : IDomainEvent, IVersionedMessage
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid AggregateId { get; init; }
    public int AggregateVersion { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    // IVersionedMessage
    public int Version => 1;
    public string MessageType => "UserCreatedEvent";

    // V1 Properties
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}

public record UserCreatedEventV2 : IDomainEvent, IVersionedMessage
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public Guid AggregateId { get; init; }
    public int AggregateVersion { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    // IVersionedMessage
    public int Version => 2;
    public string MessageType => "UserCreatedEvent";

    // V2 Properties (split Name into FirstName/LastName)
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}
```

### Message Type Naming Convention

The `MessageType` property should follow these conventions:

| Message Type | Versioned Classes | MessageType Value |
|--------------|-------------------|-------------------|
| Domain Event | `UserCreatedEventV1`, `UserCreatedEventV2` | `"UserCreatedEvent"` |
| Integration Event | `OrderShippedEventV1`, `OrderShippedEventV2` | `"OrderShippedEvent"` |

### Automatic Type Detection

The `UpcastingPipeline` can automatically derive the message type from class names that follow the `*V{N}` naming convention:

- `UserCreatedEventV1` → `"UserCreatedEvent"`
- `OrderPlacedEventV2` → `"OrderPlacedEvent"`
- `ProductUpdatedV10` → `"ProductUpdated"`

However, explicitly implementing `MessageType` is recommended for clarity and control.

## Design Notes

- This interface works with ALL message types: `IDomainEvent`, `IIntegrationEvent`, `ICommand`, and `IQuery<TResult>`
- Messages not implementing this interface pass through the upcasting pipeline unchanged
- The interface has no methods, only properties - it's purely declarative

## See Also

- [IMessageUpcaster](./IMessageUpcaster.md) - Transform between versions
- [IUpcastingPipeline](./IUpcastingPipeline.md) - Orchestrate multi-hop upcasting
- [Developer Guide](../versioning/universal-upcasting-guide.md) - Step-by-step tutorial
