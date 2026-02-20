---
sidebar_position: 2
title: Domain Modeling
description: Build domain models with aggregates, entities, and value objects
---

# Domain Modeling

Excalibur provides building blocks for domain-driven design (DDD): **aggregates**, **entities**, and **value objects**. These patterns help you model complex business domains while maintaining clean boundaries and enforcing invariants.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Domain
  dotnet add package Excalibur.Dispatch.Abstractions
  ```
- Familiarity with [event sourcing](../event-sourcing/index.md) concepts if using event-sourced aggregates

## Quick Navigation

| Concept | When to Use | Key Characteristic |
|---------|-------------|-------------------|
| [Aggregates](aggregates.md) | Root entity that controls access to related objects | Has identity, owns entities |
| [Entities](entities.md) | Objects within an aggregate that have identity | Identity over attributes |
| [Value Objects](value-objects.md) | Objects compared by their attributes | Immutable, no identity |

## The DDD Building Blocks

```
┌─────────────────────────────────────────────────────┐
│                    Aggregate                        │
│  ┌─────────────────────────────────────────────┐   │
│  │           Aggregate Root (Entity)           │   │
│  │  • Has unique identity                      │   │
│  │  • Enforces business invariants             │   │
│  │  • Controls access to children              │   │
│  └─────────────────────────────────────────────┘   │
│                        │                            │
│         ┌──────────────┼──────────────┐            │
│         ▼              ▼              ▼            │
│    ┌─────────┐   ┌─────────┐   ┌─────────────┐    │
│    │ Entity  │   │ Entity  │   │Value Object │    │
│    │(has ID) │   │(has ID) │   │ (no ID)     │    │
│    └─────────┘   └─────────┘   └─────────────┘    │
└─────────────────────────────────────────────────────┘
```

## Key Principles

### 1. Aggregates Define Consistency Boundaries

Each aggregate is a transactional boundary. All changes within an aggregate must be consistent:

```csharp
public class Order : AggregateRoot<Guid>
{
    public void AddLine(string productId, int quantity, decimal price)
    {
        // All validation happens within the aggregate
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Can only add lines to draft orders");

        // State change through events
        RaiseEvent(new OrderLineAdded(Id, productId, quantity, price));
    }
}
```

### 2. Reference Other Aggregates by ID

Never embed one aggregate inside another:

```csharp
// Correct: Reference by ID
public class Order : AggregateRoot<Guid>
{
    public string CustomerId { get; private set; }  // Just the ID
}

// Wrong: Don't embed aggregates
public class Order : AggregateRoot<Guid>
{
    public Customer Customer { get; }  // Never do this
}
```

### 3. Use Events for State Changes

All state changes happen through domain events. Events extend the `DomainEventBase` abstract record:

```csharp
// 1. Validate and raise event
public void Submit()
{
    if (Status != OrderStatus.Draft)
        throw new InvalidOperationException("Order must be Draft");

    RaiseEvent(new OrderSubmitted(Id));
}

// 2. Apply event to change state
private bool Apply(OrderSubmitted e)
{
    Status = OrderStatus.Submitted;
    return true;
}
```

## Example: Complete Order Aggregate

```csharp
using Excalibur.Domain.Model;
using Excalibur.Dispatch.Abstractions;

public class Order : AggregateRoot<Guid>
{
    public string CustomerId { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }
    public decimal Total { get; private set; }
    private readonly List<OrderLine> _lines = new();
    public IReadOnlyList<OrderLine> Lines => _lines;

    // Required for rehydration
    public Order() { }

    public Order(Guid id, string customerId)
    {
        RaiseEvent(new OrderCreated(id, customerId));
    }

    public void AddLine(string productId, int quantity, decimal unitPrice)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Can only add lines to draft orders");

        RaiseEvent(new OrderLineAdded(Id, productId, quantity, unitPrice));
    }

    public void Submit()
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Order must be in Draft status");

        if (_lines.Count == 0)
            throw new InvalidOperationException("Order must have at least one line");

        RaiseEvent(new OrderSubmitted(Id));
    }

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
}

// Domain Events extend DomainEventBase abstract record
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

## Design Guidelines

### Keep Aggregates Small

Only include what's necessary for consistency:

| Include | Exclude |
|---------|---------|
| Entities needed for invariant enforcement | Related aggregates |
| Value objects that describe aggregate state | Large collections that could be separate |
| State required for business rules | Historical data that's rarely needed |

### Choose ID Types Wisely

```csharp
// String keys (most common, simple)
public class Customer : AggregateRoot { }

// Guid keys (distributed systems)
public class Order : AggregateRoot<Guid> { }

// Strongly-typed IDs (type safety)
public readonly record struct OrderId(Guid Value);
public class Order : AggregateRoot<OrderId> { }
```

## Next Steps

- **[Aggregates](aggregates.md)** - Deep dive into aggregate patterns
- **[Entities](entities.md)** - Working with entities within aggregates
- **[Value Objects](value-objects.md)** - Immutable objects for domain concepts
- **[Event Sourcing](../event-sourcing/index.md)** - Persist and replay domain events

## See Also

- [Aggregates](./aggregates.md) - Complete guide to aggregate roots and consistency boundaries
- [Entities](./entities.md) - Objects with identity that live within aggregate boundaries
- [Value Objects](./value-objects.md) - Immutable domain concepts with structural equality
