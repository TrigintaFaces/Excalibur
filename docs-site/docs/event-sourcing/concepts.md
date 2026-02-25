---
sidebar_position: 1
title: Core Concepts
description: Event sourcing fundamentals and patterns
---

# Event Sourcing Concepts

Event sourcing is a pattern where state changes are stored as a sequence of immutable events. The current state is derived by replaying these events from the beginning of time.

## Before You Start

- Familiarity with basic domain-driven design concepts
- Understanding of [domain modeling](../domain-modeling/index.md) is helpful but not required
- No packages needed — this is a conceptual overview

## Fundamental Principles

### 1. Events Are Facts

Events represent things that have happened - they cannot be changed or deleted:

```csharp
// Events use past tense - they describe completed actions
// Events extend DomainEventBase which provides EventId, AggregateId, Version, OccurredAt
public record OrderCreated(Guid OrderId, string CustomerId) : DomainEventBase
{
    public override string AggregateId => OrderId.ToString();
}

public record OrderLineAdded(Guid OrderId, string ProductId, int Quantity) : DomainEventBase
{
    public override string AggregateId => OrderId.ToString();
}
```

### 2. State Is Derived

Current state is computed by replaying events in order:

```csharp
// Initial state
var order = new Order();  // Empty state

// Replay events to build current state using LoadFromHistory
var events = new IDomainEvent[]
{
    new OrderCreated(orderId, "CUST-123", t1),
    new OrderLineAdded(orderId, "PROD-1", 2),
    new OrderSubmitted(orderId, t2),
    new OrderShipped(orderId, "TRACK-456")
};

order.LoadFromHistory(events);
// Final state: Status=Shipped, TrackingNumber="TRACK-456"

// LoadFromHistory (from IAggregateSnapshotSupport) calls ApplyEvent for each event
```

### 3. Append-Only Storage

Events are only ever appended, never updated or deleted:

```
Event Stream: order-123
┌─────────────────────────────────────────────────────────────┐
│ Version 1: OrderCreated      { CustomerId: "CUST-123" }     │
│ Version 2: OrderLineAdded    { ProductId: "PROD-1" }        │
│ Version 3: OrderLineAdded    { ProductId: "PROD-2" }        │
│ Version 4: OrderSubmitted    { SubmittedAt: "2024-01-15" }  │
│ Version 5: OrderShipped      { TrackingNumber: "TRACK-456" }│
└─────────────────────────────────────────────────────────────┘
```

## Event Anatomy

Every domain event should extend `DomainEventBase` (from `Excalibur.Domain.Model`) which provides standard properties:

```csharp
public record OrderCreated(Guid OrderId, string CustomerId) : DomainEventBase
{
    public override string AggregateId => OrderId.ToString();
}

// DomainEventBase provides these properties automatically:
// - EventId: Auto-generated GUID string
// - AggregateId: Override in derived records to link to aggregate
// - Version: Set by infrastructure during event sourcing (default 0)
// - OccurredAt: DateTimeOffset.UtcNow at construction time
// - EventType: Derived type name (e.g. "OrderCreated")
// - Metadata: Optional cross-cutting concerns (null by default)
```

### Event Naming Conventions

| Convention | Example | Use When |
|------------|---------|----------|
| Past tense | `OrderCreated` | Standard - describes what happened |
| Noun + Past participle | `PaymentReceived` | Describes received/completed actions |
| Domain term | `InventoryReserved` | Uses ubiquitous language |

## Event Streams

Events are organized into streams, typically one per aggregate instance:

```
Streams:
├── order-001
│   ├── OrderCreated (v1)
│   ├── OrderLineAdded (v2)
│   └── OrderSubmitted (v3)
├── order-002
│   ├── OrderCreated (v1)
│   └── OrderCancelled (v2)
└── customer-123
    ├── CustomerRegistered (v1)
    ├── AddressUpdated (v2)
    └── PreferencesChanged (v3)
```

### Stream Identity

In Excalibur, a stream is identified by **two values** — `AggregateType` and `AggregateId` — passed as separate parameters to the event store:

```csharp
// IEventStore uses both values to identify a stream:
ValueTask<IReadOnlyList<StoredEvent>> LoadAsync(
    string aggregateId,      // e.g., "order-123"
    string aggregateType,    // e.g., "OrderAggregate"
    CancellationToken cancellationToken);
```

In SQL Server, these are stored as separate indexed columns (`WHERE AggregateId = @id AND AggregateType = @type`). Other providers use the same composite key (CosmosDB partition key, DynamoDB hash key, Firestore collection path).

### Default Stream Naming

`AggregateType` defaults to the class name via `GetType().Name`:

```csharp
// In AggregateRoot<TKey>:
public virtual string AggregateType => GetType().Name;
// e.g., OrderAggregate → "OrderAggregate"
```

The `EventSourcedRepository` reads this automatically when loading or saving:

```csharp
var aggregate = aggregateFactory(aggregateId);
var aggregateType = aggregate.AggregateType;  // "OrderAggregate"
var storedEvents = await eventStore.LoadAsync(aggregateId, aggregateType, ...);
```

### Customizing Stream Names

Override the `AggregateType` property on your aggregate to control the stream name:

```csharp
public class OrderAggregate : AggregateRoot<Guid>
{
    // Use a shorter, stable name instead of the class name
    public override string AggregateType => "Order";
}

public class CustomerAggregate : AggregateRoot<Guid>
{
    // Add a bounded context prefix
    public override string AggregateType => "Sales.Customer";
}
```

:::tip Use stable names
If you rename your aggregate class, the default `GetType().Name` changes — breaking existing streams. Override `AggregateType` with a fixed string to decouple storage from class names.
:::

## Optimistic Concurrency

Event sourcing uses version numbers for concurrency control:

```csharp
// Load aggregate - note current version
var order = await repository.GetByIdAsync(orderId);
// order.Version = 5

// Make changes
order.AddLine("PROD-3", 1, 25.00m);

// Save with expected version
await eventStore.AppendAsync(
    aggregateId: orderId,
    events: order.GetUncommittedEvents(),
    expectedVersion: 5);  // Must match current version

// If another process appended events, this throws ConcurrencyException
```

### Handling Concurrency Conflicts

```csharp
try
{
    await repository.SaveAsync(order);
}
catch (ConcurrencyException ex)
{
    // Option 1: Reload and retry
    var reloaded = await repository.GetByIdAsync(orderId);
    reloaded.AddLine("PROD-3", 1, 25.00m);
    await repository.SaveAsync(reloaded);

    // Option 2: Return conflict to caller
    throw new ConflictException("Order was modified");
}
```

## Temporal Queries

Event sourcing enables powerful temporal queries:

### State at a Point in Time

```csharp
// Load events up to a specific time
var events = await eventStore.LoadAsync(
    orderId,
    toTimestamp: new DateTime(2024, 1, 15));

// Replay to get state as of that date
var historicalOrder = new Order();
historicalOrder.LoadFromHistory(events);
```

### State at a Specific Version

```csharp
// Load only first N events
var events = await eventStore.LoadAsync(
    orderId,
    toVersion: 3);

// State after 3 events
var order = new Order();
order.LoadFromHistory(events);
```

### Event Timeline

```csharp
// Get all events for audit
var events = await eventStore.LoadAsync(orderId);

foreach (var e in events)
{
    Console.WriteLine($"{e.Version}: {e.EventType} at {e.Timestamp}");
}
// Output:
// 1: OrderCreated at 2024-01-10 10:00:00
// 2: OrderLineAdded at 2024-01-10 10:05:00
// 3: OrderSubmitted at 2024-01-10 10:10:00
```

## Event Versioning

As your domain evolves, events may need to change. Handle this with upcasters:

### Schema Evolution

```csharp
// V1: Original event
public record OrderCreatedV1(Guid OrderId, string CustomerId);

// V2: Added field with default
public record OrderCreated(
    Guid OrderId,
    string CustomerId,
    string Currency = "USD");  // New field with default
```

### Upcasting

```csharp
public class OrderCreatedUpcaster : IMessageUpcaster<OrderCreatedV1, OrderCreated>
{
    public OrderCreated Upcast(OrderCreatedV1 source)
    {
        return new OrderCreated(source.AggregateId, source.Version)
        {
            OrderId = source.OrderId,
            CustomerId = source.CustomerId,
            Currency = "USD"  // Default for historical events
        };
    }
}
```

## Common Patterns

### Idempotency

Handle duplicate event delivery:

```csharp
public class OrderLineAddedHandler
{
    public async Task Handle(OrderLineAdded @event, CancellationToken ct)
    {
        // Check if already processed using event ID
        if (await _processed.ContainsAsync(@event.EventId, ct))
            return;

        await ProcessEvent(@event, ct);
        await _processed.AddAsync(@event.EventId, ct);
    }
}
```

### Event Metadata

Attach contextual information:

```csharp
public class EventMetadata
{
    public string UserId { get; set; }
    public string CorrelationId { get; set; }
    public string CausationId { get; set; }
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string> Custom { get; set; }
}
```

### Domain Events vs Integration Events

| Domain Event | Integration Event |
|--------------|-------------------|
| Internal to bounded context | Crosses context boundaries |
| Contains domain details | Contains only necessary data |
| Not versioned for consumers | Should be versioned |
| Can change freely | Must maintain compatibility |

```csharp
// Domain event (internal)
public record OrderLineAdded(
    Guid OrderId,
    string ProductId,
    int Quantity,
    decimal UnitPrice,
    Guid LineId);  // Internal detail

// Integration event (published externally)
public record OrderLineAddedIntegrationEvent(
    Guid OrderId,
    string ProductId,
    int Quantity);  // Only essential data
```

## Best Practices

### 1. Keep Events Small

Include only what's needed:

```csharp
// Good: Focused event
public record OrderShipped(
    Guid OrderId,
    string TrackingNumber,
    DateTime ShippedAt);

// Avoid: Including too much
public record OrderShipped(
    Guid OrderId,
    string TrackingNumber,
    Order FullOrder,  // Don't include full objects
    List<OrderLine> AllLines);  // Redundant data
```

### 2. Use Domain Language

Events should use ubiquitous language:

```csharp
// Good: Domain language
public record InventoryReserved(Guid OrderId, string ProductId, int Quantity);
public record PaymentAuthorized(Guid OrderId, decimal Amount);

// Avoid: Technical language
public record InventoryTableUpdated(...);
public record PaymentRecordInserted(...);
```

### 3. Events Are Immutable

Never modify events after creation:

```csharp
public record OrderCreated
{
    public Guid OrderId { get; init; }  // init-only
    public string CustomerId { get; init; }  // init-only

    // No setters, no mutable collections
}
```

## Next Steps

- **[Event Application Pattern](event-application-pattern.md)** - Understanding RaiseEvent/Apply
- **[Repositories](repositories.md)** - Load and save aggregates
- **[Snapshots](snapshots.md)** - Optimize for long event streams
- **[Projections](projections.md)** - Build read models from events

## See Also

- [Event Sourcing Overview](./index.md) - Getting started with event sourcing in Excalibur
- [Event Store](./event-store.md) - Persisting events with append-only storage
- [Domain Events](./domain-events.md) - Defining and working with domain events
