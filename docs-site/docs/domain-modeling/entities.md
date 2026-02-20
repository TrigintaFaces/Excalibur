---
sidebar_position: 2
title: Entities
description: Model objects with identity within aggregates
---

# Entities

Entities are domain objects that have a distinct identity that runs through time and different states. Unlike value objects, entities are defined by their identity, not their attributes.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required package:
  ```bash
  dotnet add package Excalibur.Domain
  ```
- Familiarity with [domain modeling concepts](./index.md) and [aggregates](./aggregates.md)

## Key Characteristics

| Characteristic | Description |
|----------------|-------------|
| **Identity** | Unique identifier that persists through state changes |
| **Mutability** | Can change attributes while maintaining identity |
| **Equality** | Two entities are equal if they have the same identity |
| **Lifecycle** | Created, modified, and potentially deleted over time |

## The EntityBase Class

Excalibur provides `EntityBase<TKey>` for entities within aggregates:

```csharp
using Excalibur.Domain.Model;

public class OrderLine : EntityBase<Guid>
{
    public Guid Id { get; }
    public string ProductId { get; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; }

    // Required: Implement Key property
    public override Guid Key => Id;

    public OrderLine(string productId, int quantity, decimal unitPrice)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public void UpdateQuantity(int newQuantity)
    {
        if (newQuantity <= 0)
            throw new ArgumentException("Quantity must be positive");

        Quantity = newQuantity;
    }
}
```

## Entity Identity

Entities are compared by their key, not their attributes:

```csharp
var line1 = new OrderLine("PROD-1", 2, 10.00m);
var line2 = new OrderLine("PROD-1", 5, 15.00m);

// Different instances with different IDs (Guid.NewGuid() called in each)
line1.Equals(line2); // false - different identities

// Same entity retrieved twice
var retrieved1 = order.Lines.First(l => l.Id == someId);
var retrieved2 = order.Lines.First(l => l.Id == someId);
retrieved1.Equals(retrieved2); // true - same identity
```

## Entities Within Aggregates

Entities exist within the boundary of an aggregate and are accessed through the aggregate root:

```csharp
public class Order : AggregateRoot<Guid>
{
    private readonly List<OrderLine> _lines = new();
    public IReadOnlyList<OrderLine> Lines => _lines;

    public void AddLine(string productId, int quantity, decimal unitPrice)
    {
        // Entity created within aggregate boundary
        var line = new OrderLine(productId, quantity, unitPrice);

        // Changes go through aggregate root
        RaiseEvent(new OrderLineAdded(Id, line.Id, productId, quantity, unitPrice));
    }

    public void UpdateLineQuantity(Guid lineId, int newQuantity)
    {
        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException($"Line {lineId} not found");

        // Validation at aggregate level
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Can only modify draft orders");

        RaiseEvent(new OrderLineQuantityUpdated(Id, lineId, newQuantity));
    }

    private bool Apply(OrderLineQuantityUpdated e)
    {
        var line = _lines.First(l => l.Id == e.LineId);
        line.UpdateQuantity(e.NewQuantity);
        return true;
    }
}
```

## Entity Design Patterns

### Encapsulated Modification

Keep modification methods on the entity but validate at the aggregate:

```csharp
// Entity provides modification logic
public class OrderLine : EntityBase<Guid>
{
    public int Quantity { get; private set; }

    internal void SetQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive");
        Quantity = quantity;
    }
}

// Aggregate controls when modifications are allowed
public class Order : AggregateRoot<Guid>
{
    public void UpdateLineQuantity(Guid lineId, int quantity)
    {
        // Business rule: only draft orders can be modified
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Order is not editable");

        var line = FindLine(lineId);

        // Delegate to entity for field-level validation
        line.SetQuantity(quantity);

        RaiseEvent(new OrderLineUpdated(Id, lineId, quantity));
    }
}
```

### Entity Collections

Use read-only collection interfaces and internal lists:

```csharp
public class Order : AggregateRoot<Guid>
{
    private readonly List<OrderLine> _lines = new();

    // Expose as read-only - modifications through aggregate methods only
    public IReadOnlyList<OrderLine> Lines => _lines;
    public int LineCount => _lines.Count;
    public bool HasLines => _lines.Count > 0;

    // Find operations
    public OrderLine? FindLine(Guid lineId) =>
        _lines.FirstOrDefault(l => l.Id == lineId);

    public OrderLine? FindLineByProduct(string productId) =>
        _lines.FirstOrDefault(l => l.ProductId == productId);
}
```

### Rich Entity Behavior

Entities can have behavior beyond simple property access:

```csharp
public class OrderLine : EntityBase<Guid>
{
    public Guid Id { get; }
    public string ProductId { get; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal? DiscountPercent { get; private set; }

    public override Guid Key => Id;

    // Calculated properties
    public decimal LineTotal => CalculateTotal();

    public decimal CalculateTotal()
    {
        var subtotal = Quantity * UnitPrice;
        if (DiscountPercent.HasValue)
        {
            return subtotal * (1 - DiscountPercent.Value / 100);
        }
        return subtotal;
    }

    // Business logic
    public bool CanApplyDiscount(decimal percent)
    {
        // Can't discount below cost
        return percent <= 50 && UnitPrice > 0;
    }

    internal void ApplyDiscount(decimal percent)
    {
        if (!CanApplyDiscount(percent))
            throw new InvalidOperationException("Invalid discount");
        DiscountPercent = percent;
    }
}
```

## Entity vs Value Object Decision

Use this decision tree:

```
Does the object need a unique identity that persists
through changes to its attributes?
    │
    ├── YES → Use Entity
    │         Examples: OrderLine, Attendee, LineItem
    │
    └── NO → Does the object represent a concept that's
             compared by its attributes?
                │
                ├── YES → Use Value Object
                │         Examples: Money, Address, DateRange
                │
                └── NO → Use a simple type or record
```

## Entity Lifecycle

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│   Created   │───▶│  Modified   │───▶│   Removed   │
│  (new ID)   │    │ (same ID)   │    │ (tombstone) │
└─────────────┘    └─────────────┘    └─────────────┘
                         │
                         ▼
                   ┌─────────────┐
                   │  Modified   │
                   │  (same ID)  │
                   └─────────────┘
```

Entities maintain their identity throughout their lifecycle:

```csharp
public class Order : AggregateRoot<Guid>
{
    public void RemoveLine(Guid lineId)
    {
        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException("Line not found");

        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Cannot remove from submitted order");

        // Line identity preserved in event for audit
        RaiseEvent(new OrderLineRemoved(Id, lineId, line.ProductId));
    }

    private bool Apply(OrderLineRemoved e)
    {
        _lines.RemoveAll(l => l.Id == e.LineId);
        Total = _lines.Sum(l => l.Quantity * l.UnitPrice);
        return true;
    }
}
```

## Best Practices

### 1. Keep Entities Focused

Entities should represent a single concept:

```csharp
// Good: Focused entity
public class OrderLine : EntityBase<Guid>
{
    public Guid Id { get; }
    public string ProductId { get; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; }
}

// Avoid: Mixing concerns
public class OrderLine : EntityBase<Guid>
{
    public Product Product { get; }  // Don't embed other aggregates
    public Customer LastModifiedBy { get; }  // Audit concerns belong elsewhere
}
```

### 2. Entity Changes Through Aggregate

Never modify entities directly from outside the aggregate:

```csharp
// Wrong: Direct entity modification
var line = order.Lines.First();
line.Quantity = 5;  // Bypasses aggregate invariants

// Correct: Through aggregate methods
order.UpdateLineQuantity(lineId, 5);
```

### 3. Use Internal Setters

Make modification methods internal or private:

```csharp
public class OrderLine : EntityBase<Guid>
{
    public int Quantity { get; private set; }

    // Only accessible within the assembly (by aggregate)
    internal void SetQuantity(int quantity) { ... }
}
```

## Next Steps

- **[Aggregates](aggregates.md)** - Parent containers for entities
- **[Value Objects](value-objects.md)** - Objects defined by attributes
- **[Event Sourcing](../event-sourcing/index.md)** - Persist entity changes as events

## See Also

- [Aggregates](./aggregates.md) - Aggregate roots that own and control access to entities
- [Value Objects](./value-objects.md) - Immutable objects compared by attributes rather than identity
- [Domain Modeling Overview](./index.md) - Introduction to DDD building blocks in Excalibur
