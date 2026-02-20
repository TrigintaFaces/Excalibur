---
sidebar_position: 2
title: Event Application Pattern
description: Understanding the RaiseEvent/Apply pattern for event-sourced aggregates
---

# Event Application Pattern

Excalibur uses a specific naming convention for applying events in event-sourced aggregates. This guide explains the pattern, why it was chosen, and how to use it effectively.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Domain
  ```
- Familiarity with [aggregates](./aggregates.md) and [domain events](./domain-events.md)

## The Pattern

Excalibur uses the **RaiseEvent/Apply** pattern:

```csharp
public class Order : AggregateRoot<Guid>
{
    public OrderStatus Status { get; private set; }

    // Command method: validates business rules and raises events
    public void Submit()
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Only draft orders can be submitted");

        RaiseEvent(new OrderSubmitted(Id, DateTime.UtcNow));
    }

    // Event routing: dispatches to type-specific handlers
    protected override void ApplyEventInternal(IDomainEvent @event)
    {
        switch (@event)
        {
            case OrderCreated e: Apply(e); break;
            case OrderSubmitted e: Apply(e); break;
            default: throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}");
        }
    }

    // State mutation: updates aggregate state
    private void Apply(OrderCreated e)
    {
        Id = e.OrderId;
        Status = OrderStatus.Draft;
    }

    private void Apply(OrderSubmitted e)
    {
        Status = OrderStatus.Submitted;
    }
}
```

## Method Responsibilities

| Method | Purpose | When to Call |
|--------|---------|--------------|
| `RaiseEvent(event)` | Emit a new domain event | In command methods after validation |
| `ApplyEventInternal(event)` | Route events to handlers | Never call directly (framework calls it) |
| `Apply(SpecificEvent)` | Mutate state for event type | Never call directly (called via switch) |

### RaiseEvent: Emitting New Events

`RaiseEvent` is called from your command methods to emit new business events:

```csharp
public void AddLine(string productId, int quantity, decimal unitPrice)
{
    // 1. Validate business rules
    if (Status != OrderStatus.Draft)
        throw new InvalidOperationException("Cannot modify submitted orders");

    if (quantity <= 0)
        throw new ArgumentException("Quantity must be positive");

    // 2. Raise event after all validations pass
    RaiseEvent(new OrderLineAdded(Id, productId, quantity, unitPrice));
}
```

When you call `RaiseEvent`:
1. The event is applied to update aggregate state (via `ApplyEventInternal`)
2. The event is added to the uncommitted events collection
3. The event will be persisted when you save the aggregate

### ApplyEventInternal: Routing Events

`ApplyEventInternal` is an abstract method you must implement. It routes events to the correct `Apply` method using pattern matching:

```csharp
protected override void ApplyEventInternal(IDomainEvent @event)
{
    switch (@event)
    {
        case OrderCreated e: Apply(e); break;
        case OrderLineAdded e: Apply(e); break;
        case OrderSubmitted e: Apply(e); break;
        case OrderCancelled e: Apply(e); break;
        default: throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}");
    }
}
```

:::tip Performance Optimization
This pattern-matching approach achieves **under 10 nanoseconds** per event application because:
- No reflection is used
- No dictionary lookups
- The JIT compiler can inline the switch
- Pattern matching compiles to efficient IL
:::

### Apply: Mutating State

Each `Apply` method handles a specific event type and updates the aggregate's state:

```csharp
private void Apply(OrderCreated e)
{
    Id = e.OrderId;
    CustomerId = e.CustomerId;
    Status = OrderStatus.Draft;
    CreatedAt = e.OccurredAt;
}

private void Apply(OrderLineAdded e)
{
    _lines.Add(new OrderLine(e.ProductId, e.Quantity, e.UnitPrice));
    RecalculateTotal();
}
```

## Why This Pattern?

### Clear Intent

`RaiseEvent` clearly communicates "I am raising a new business event":

```csharp
// Clear: raising a new event
RaiseEvent(new OrderSubmitted(Id, DateTime.UtcNow));

// Alternative pattern - less clear
Apply(new OrderSubmitted(Id, DateTime.UtcNow));  // Is this new or replay?
```

### Separation of Concerns

The pattern separates three distinct responsibilities:

1. **Validation** (command methods) - Enforces business rules
2. **Routing** (`ApplyEventInternal`) - Dispatches to correct handler
3. **State mutation** (`Apply`) - Updates aggregate state

### Optimal Performance

Pattern matching compiles to efficient IL:

```csharp
// This switch statement
switch (@event)
{
    case OrderCreated e: Apply(e); break;
    case OrderSubmitted e: Apply(e); break;
    default: throw ...;
}

// Compiles to type checks and jumps (no reflection)
```

## Comparing to Other Patterns

You may encounter other naming conventions in DDD literature:

### When/Apply Pattern

Some frameworks and books use `When` for event handlers:

```csharp
// Alternative "When" pattern (not used in Excalibur)
private void When(OrderCreated e)
{
    Id = e.OrderId;
    Status = OrderStatus.Draft;
}
```

Both patterns are valid. Excalibur chose `Apply` because:
- It's more idiomatic in C#
- `When` originates from functional languages (F#, Erlang)
- `Apply` aligns with other .NET event sourcing frameworks

### Emit/Apply Pattern

Some frameworks use `Emit` instead of `RaiseEvent`:

```csharp
// Alternative naming
Emit(new OrderCreated(...));  // Same concept as RaiseEvent
```

This is semantically equivalent to our `RaiseEvent`.

## Complete Example

Here's a complete aggregate showing all aspects of the pattern:

```csharp
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

public class ShoppingCart : AggregateRoot<Guid>
{
    private readonly List<CartItem> _items = new();

    public string CustomerId { get; private set; } = string.Empty;
    public IReadOnlyList<CartItem> Items => _items;
    public decimal Total { get; private set; }
    public CartStatus Status { get; private set; }

    // Parameterless constructor for rehydration
    public ShoppingCart() { }

    // Domain constructor
    public ShoppingCart(Guid id, string customerId)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Cart ID required", nameof(id));
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("Customer ID required", nameof(customerId));

        RaiseEvent(new CartCreated(id, customerId));
    }

    // Command: Add item
    public void AddItem(string productId, int quantity, decimal unitPrice)
    {
        if (Status != CartStatus.Active)
            throw new InvalidOperationException("Cart is not active");
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));

        var existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing != null)
        {
            RaiseEvent(new CartItemQuantityChanged(Id, productId, existing.Quantity + quantity));
        }
        else
        {
            RaiseEvent(new CartItemAdded(Id, productId, quantity, unitPrice));
        }
    }

    // Command: Remove item
    public void RemoveItem(string productId)
    {
        if (Status != CartStatus.Active)
            throw new InvalidOperationException("Cart is not active");

        var existing = _items.FirstOrDefault(i => i.ProductId == productId);
        if (existing == null)
            throw new InvalidOperationException($"Product {productId} not in cart");

        RaiseEvent(new CartItemRemoved(Id, productId));
    }

    // Command: Checkout
    public void Checkout()
    {
        if (Status != CartStatus.Active)
            throw new InvalidOperationException("Cart is not active");
        if (_items.Count == 0)
            throw new InvalidOperationException("Cannot checkout empty cart");

        RaiseEvent(new CartCheckedOut(Id, Total));
    }

    // Event routing
    protected override void ApplyEventInternal(IDomainEvent @event)
    {
        switch (@event)
        {
            case CartCreated e: Apply(e); break;
            case CartItemAdded e: Apply(e); break;
            case CartItemQuantityChanged e: Apply(e); break;
            case CartItemRemoved e: Apply(e); break;
            case CartCheckedOut e: Apply(e); break;
            default: throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}");
        }
    }

    // State mutations
    private void Apply(CartCreated e)
    {
        Id = e.CartId;
        CustomerId = e.CustomerId;
        Status = CartStatus.Active;
    }

    private void Apply(CartItemAdded e)
    {
        _items.Add(new CartItem(e.ProductId, e.Quantity, e.UnitPrice));
        RecalculateTotal();
    }

    private void Apply(CartItemQuantityChanged e)
    {
        var item = _items.First(i => i.ProductId == e.ProductId);
        _items.Remove(item);
        _items.Add(item with { Quantity = e.NewQuantity });
        RecalculateTotal();
    }

    private void Apply(CartItemRemoved e)
    {
        _items.RemoveAll(i => i.ProductId == e.ProductId);
        RecalculateTotal();
    }

    private void Apply(CartCheckedOut e)
    {
        Status = CartStatus.CheckedOut;
    }

    private void RecalculateTotal()
    {
        Total = _items.Sum(i => i.Quantity * i.UnitPrice);
    }
}

// Supporting types
public record CartItem(string ProductId, int Quantity, decimal UnitPrice);

public enum CartStatus { Active, CheckedOut, Abandoned }

// Events - extend DomainEventBase abstract record
public record CartCreated(Guid CartId, string CustomerId) : DomainEventBase
{
    public override string AggregateId => CartId.ToString();
}

public record CartItemAdded(Guid CartId, string ProductId, int Quantity, decimal UnitPrice) : DomainEventBase
{
    public override string AggregateId => CartId.ToString();
}

public record CartItemQuantityChanged(Guid CartId, string ProductId, int NewQuantity) : DomainEventBase
{
    public override string AggregateId => CartId.ToString();
}

public record CartItemRemoved(Guid CartId, string ProductId) : DomainEventBase
{
    public override string AggregateId => CartId.ToString();
}

public record CartCheckedOut(Guid CartId, decimal Total) : DomainEventBase
{
    public override string AggregateId => CartId.ToString();
}
```

## Best Practices

### 1. Keep Apply Methods Simple

Apply methods should only mutate state - no validation or side effects:

```csharp
// Good: Simple state mutation
private void Apply(OrderShipped e)
{
    Status = OrderStatus.Shipped;
    ShippedAt = e.ShippedAt;
    TrackingNumber = e.TrackingNumber;
}

// Avoid: Validation in Apply (too late!)
private void Apply(OrderShipped e)
{
    if (Status != OrderStatus.Confirmed)  // Wrong place for validation
        throw new InvalidOperationException();
    ...
}
```

### 2. Handle All Events

Always include a default case to catch unhandled events:

```csharp
protected override void ApplyEventInternal(IDomainEvent @event)
{
    switch (@event)
    {
        case OrderCreated e: Apply(e); break;
        case OrderSubmitted e: Apply(e); break;
        // Don't forget new events!
        default: throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}");
    }
}
```

### 3. Events Are Past Tense

Events describe things that happened:

```csharp
// Good: Past tense
public record OrderSubmitted(...);
public record PaymentReceived(...);
public record InventoryReserved(...);

// Avoid: Present tense or commands
public record SubmitOrder(...);  // This is a command, not an event
public record ReceivePayment(...);
```

## Next Steps

- **[Aggregates](../domain-modeling/aggregates.md)** - Complete aggregate implementation guide
- **[Repositories](repositories.md)** - Persisting and loading aggregates
- **[Snapshots](snapshots.md)** - Optimizing long event streams

## See Also

- [Event Sourcing Aggregates](./aggregates.md) - Persisting and loading aggregates with event sourcing
- [Domain Events](./domain-events.md) - Defining events that drive state changes
- [Event Sourcing Overview](./index.md) - Core concepts and getting started guide
