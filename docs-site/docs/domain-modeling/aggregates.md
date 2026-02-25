---
sidebar_position: 1
title: Aggregates
description: Model domain boundaries with aggregate roots
---

# Aggregates

Aggregates are the primary building blocks for domain modeling. An aggregate is a cluster of domain objects that can be treated as a single unit for data changes. Every aggregate has a root entity (the **Aggregate Root**) that controls all access to objects within the aggregate.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required package:
  ```bash
  dotnet add package Excalibur.Domain
  ```
- Familiarity with [domain modeling concepts](./index.md) and [event sourcing](../event-sourcing/index.md)

## The AggregateRoot Base Class

Excalibur provides `AggregateRoot<TId>` as the base class for all aggregates:

```csharp
using Excalibur.Domain.Model;

public class Order : AggregateRoot<Guid>
{
    // Your aggregate implementation
}
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `TId` | Unique identifier for the aggregate |
| `Version` | `long` | Current version (event count) |
| `ETag` | `string?` | Concurrency token for optimistic locking |
| `AggregateType` | `string` | Type name for serialization |

### Key Methods

| Method | Purpose |
|--------|---------|
| `RaiseEvent(event)` | Record a new domain event |
| `GetUncommittedEvents()` | Get events pending persistence |
| `MarkEventsAsCommitted()` | Clear uncommitted events after save |
| `LoadFromHistory(events)` | Rehydrate from stored events |
| `CreateSnapshot()` | Create state snapshot (optional) |

## Creating Aggregates

### Constructor Pattern

Always emit events in constructors rather than setting state directly:

```csharp
public class Order : AggregateRoot<Guid>
{
    public string CustomerId { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }

    // Parameterless constructor for rehydration
    public Order() { }

    // Domain constructor emits creation event
    public Order(Guid id, string customerId)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Order ID cannot be empty", nameof(id));

        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("Customer ID is required", nameof(customerId));

        RaiseEvent(new OrderCreated(id, customerId));
    }
}
```

### Domain Events

Define events as immutable records extending `DomainEventBase`:

```csharp
using Excalibur.Dispatch.Abstractions;

public record OrderCreated(Guid OrderId, string CustomerId) : DomainEventBase
{
    public override string AggregateId => OrderId.ToString();
}

public record OrderLineAdded(Guid OrderId, string ProductId, int Quantity, decimal UnitPrice) : DomainEventBase
{
    public override string AggregateId => OrderId.ToString();
}

public record OrderSubmitted(Guid OrderId) : DomainEventBase
{
    public override string AggregateId => OrderId.ToString();
}
```

The `DomainEventBase` abstract record automatically provides:
- `EventId` - UUID v7 for time-ordered uniqueness
- `AggregateId` - String identifier of the owning aggregate
- `Version` - Aggregate version for ordering
- `OccurredAt` - UTC timestamp (auto-generated)
- `EventType` - Type name for serialization
- `Metadata` - Dictionary for cross-cutting concerns

## Event Application

Excalibur uses the **RaiseEvent/Apply** pattern with pattern matching for event application, providing near-zero overhead (under 10ns per event). For a detailed explanation of why this pattern was chosen over alternatives like `When/Apply`, see the [Event Application Pattern](../event-sourcing/event-application-pattern.md) guide.

```csharp
protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
{
    OrderCreated e => Apply(e),
    OrderLineAdded e => Apply(e),
    OrderSubmitted e => Apply(e),
    _ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}")
};

private bool Apply(OrderCreated e)
{
    Id = e.OrderId;
    CustomerId = e.CustomerId;
    Status = OrderStatus.Draft;
    return true;
}

private bool Apply(OrderLineAdded e)
{
    _lines.Add(new OrderLine(e.ProductId, e.Quantity, e.UnitPrice));
    Total = _lines.Sum(l => l.Quantity * l.UnitPrice);
    return true;
}

private bool Apply(OrderSubmitted e)
{
    Status = OrderStatus.Submitted;
    return true;
}
```

## Enforcing Invariants

Aggregates are the natural place to enforce business rules:

```csharp
public void AddLine(string productId, int quantity, decimal unitPrice)
{
    // Invariant: Can only modify draft orders
    if (Status != OrderStatus.Draft)
        throw new InvalidOperationException("Can only add lines to draft orders");

    // Invariant: Positive quantities
    if (quantity <= 0)
        throw new ArgumentException("Quantity must be positive", nameof(quantity));

    // Invariant: Maximum lines limit
    if (_lines.Count >= 100)
        throw new InvalidOperationException("Order cannot have more than 100 lines");

    // Invariant: No duplicate products
    if (_lines.Any(l => l.ProductId == productId))
        throw new InvalidOperationException($"Product {productId} already in order");

    // All validations passed - emit event
    RaiseEvent(new OrderLineAdded(Id, productId, quantity, unitPrice));
}
```

## ID Types

Excalibur supports multiple ID types:

### String Keys (Default)

```csharp
public class Customer : AggregateRoot
{
    // Id is string by default
}
```

### GUID Keys

```csharp
public class Order : AggregateRoot<Guid>
{
    // Id is Guid
}
```

### Strongly-Typed IDs

For maximum type safety, use custom ID types:

```csharp
// Define the ID type
public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

// Use in aggregate
public class Order : AggregateRoot<OrderId>
{
    public Order(OrderId id, string customerId)
    {
        RaiseEvent(new OrderCreated(id, customerId));
    }
}

// Usage
var orderId = OrderId.New();
var order = new Order(orderId, "CUST-123");
```

## Working with Uncommitted Events

After modifying an aggregate, uncommitted events are available for persistence:

```csharp
// Create and modify aggregate
var order = new Order(Guid.NewGuid(), "CUST-123");
order.AddLine("PROD-1", 2, 29.99m);
order.Submit();

// Get events to persist
var events = order.GetUncommittedEvents();
// Returns: [OrderCreated, OrderLineAdded, OrderSubmitted]

// After saving to event store
order.MarkEventsAsCommitted();
// Events cleared, version incremented
```

## Rehydrating from History

Load an aggregate from stored events:

```csharp
// Events from event store
var events = new IDomainEvent[]
{
    new OrderCreated(orderId, "CUST-123", createdAt),
    new OrderLineAdded(orderId, "PROD-1", 2, 29.99m),
    new OrderSubmitted(orderId, submittedAt)
};

// Create empty aggregate and load history
var order = new Order();
order.LoadFromHistory(events);

// State is now:
// order.Status == OrderStatus.Submitted
// order.Lines.Count == 1
// order.Version == 3
```

## Design Guidelines

### Keep Aggregates Small

Include only what's needed for invariant enforcement:

```csharp
// Good: Order contains only what it needs
public class Order : AggregateRoot<Guid>
{
    public string CustomerId { get; private set; }  // Reference by ID
    private readonly List<OrderLine> _lines = new();  // Part of order invariants
}

// Avoid: Don't embed entire related aggregates
public class Order : AggregateRoot<Guid>
{
    public Customer Customer { get; }  // Wrong: Customer is its own aggregate
}
```

### Reference Other Aggregates by ID

```csharp
public class Order : AggregateRoot<Guid>
{
    public string CustomerId { get; private set; }      // ID reference
    public Guid ShippingAddressId { get; private set; } // ID reference

    // Load related data through services when needed
}
```

### Protect Internal State

Use private setters and expose read-only collections:

```csharp
public class Order : AggregateRoot<Guid>
{
    private readonly List<OrderLine> _lines = new();

    // Expose as read-only
    public IReadOnlyList<OrderLine> Lines => _lines;

    // All modifications through methods
    public void AddLine(string productId, int quantity, decimal unitPrice)
    {
        // Validation and event emission
    }
}
```

## Next Steps

- **[Entities](entities.md)** - Model objects with identity within aggregates
- **[Value Objects](value-objects.md)** - Model immutable concepts
- **[Event Sourcing](../event-sourcing/index.md)** - Persist aggregate state as events

## See Also

- [Entities](./entities.md) - Model objects with identity that live within aggregate boundaries
- [Value Objects](./value-objects.md) - Immutable domain concepts compared by their attributes
- [Event Sourcing Aggregates](../event-sourcing/aggregates.md) - Persisting and loading aggregates with event sourcing
