---
sidebar_position: 3
title: Aggregates
description: Build event-sourced aggregates that enforce business rules
---

# Aggregates

Aggregates are the core building blocks of event-sourced systems. They enforce business rules and emit events when state changes.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Domain
  dotnet add package Excalibur.EventSourcing
  ```
- Familiarity with [event sourcing concepts](./index.md) and [domain events](./domain-events.md)

## Basic Structure

```csharp
public class Order : AggregateRoot<Guid>
{
    // State properties
    public string CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    private readonly List<OrderLine> _lines = [];
    public IReadOnlyList<OrderLine> Lines => _lines;

    // Required: private parameterless constructor for rehydration
    private Order() { }

    // Factory method for creation
    public static Order Create(Guid orderId, string customerId)
    {
        var order = new Order();
        order.RaiseEvent(new OrderCreated(orderId, customerId));
        return order;
    }

    // Command methods that enforce business rules
    public void AddLine(string productId, int quantity, decimal unitPrice)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Cannot modify a non-draft order");

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive", nameof(quantity));

        RaiseEvent(new OrderLineAdded(Id, productId, quantity, unitPrice));
    }

    public void Submit()
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Only draft orders can be submitted");

        if (!_lines.Any())
            throw new InvalidOperationException("Cannot submit an empty order");

        RaiseEvent(new OrderSubmitted(Id, TotalAmount, DateTime.UtcNow));
    }

    // Event application - uses pattern matching, no reflection
    protected override void ApplyEventInternal(IDomainEvent @event)
    {
        switch (@event)
        {
            case OrderCreated e:
                Id = e.OrderId;
                CustomerId = e.CustomerId;
                Status = OrderStatus.Draft;
                TotalAmount = 0;
                break;

            case OrderLineAdded e:
                _lines.Add(new OrderLine(e.ProductId, e.Quantity, e.UnitPrice));
                TotalAmount += e.Quantity * e.UnitPrice;
                break;

            case OrderSubmitted:
                Status = OrderStatus.Submitted;
                break;
        }
    }
}
```

## Key Components

### AggregateRoot Base Class

```csharp
public abstract class AggregateRoot<TKey> : IAggregateRoot<TKey>
{
    // Identity
    public TKey Id { get; protected set; }

    // Optimistic concurrency
    public long Version { get; protected set; }
    public string? ETag { get; set; }

    // Pending changes (method, not property)
    public IReadOnlyList<IDomainEvent> GetUncommittedEvents();

    // Raise a new event
    protected void RaiseEvent(IDomainEvent @event);

    // Replay historical events
    public void LoadFromHistory(IEnumerable<IDomainEvent> history);

    // Clear after persistence
    public void MarkEventsAsCommitted();

    // Implement this using pattern matching
    protected abstract void ApplyEventInternal(IDomainEvent @event);
}
```

### Factory Methods

Use factory methods instead of public constructors:

```csharp
public class Order : AggregateRoot<Guid>
{
    // Private constructor prevents direct instantiation
    private Order() { }

    // Factory method validates and creates
    public static Order Create(Guid orderId, string customerId)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("OrderId required", nameof(orderId));
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("CustomerId required", nameof(customerId));

        var order = new Order();
        order.RaiseEvent(new OrderCreated(orderId, customerId));
        return order;
    }
}
```

### Command Methods

Command methods enforce business rules before raising events:

```csharp
public void Ship(string trackingNumber, string carrier)
{
    // Guard clauses - enforce business rules
    if (Status != OrderStatus.Submitted)
        throw new InvalidOperationException($"Cannot ship order in {Status} status");

    if (string.IsNullOrWhiteSpace(trackingNumber))
        throw new ArgumentException("Tracking number required", nameof(trackingNumber));

    // All validation passed - raise the event
    RaiseEvent(new OrderShipped(Id, trackingNumber, carrier, DateTime.UtcNow));
}

public void Cancel(string reason)
{
    // Complex business rule
    if (Status == OrderStatus.Shipped)
        throw new InvalidOperationException("Cannot cancel a shipped order");

    if (Status == OrderStatus.Delivered)
        throw new InvalidOperationException("Cannot cancel a delivered order");

    RaiseEvent(new OrderCancelled(Id, reason, DateTime.UtcNow));
}
```

### Event Application

Use pattern matching for type-safe, reflection-free event application:

```csharp
protected override void ApplyEventInternal(IDomainEvent @event)
{
    switch (@event)
    {
        case OrderCreated e:
            Id = e.OrderId;
            CustomerId = e.CustomerId;
            Status = OrderStatus.Draft;
            break;

        case OrderLineAdded e:
            _lines.Add(new OrderLine(e.ProductId, e.Quantity, e.UnitPrice));
            TotalAmount += e.Quantity * e.UnitPrice;
            break;

        case OrderLineRemoved e:
            var line = _lines.First(l => l.ProductId == e.ProductId);
            _lines.Remove(line);
            TotalAmount -= line.Quantity * line.UnitPrice;
            break;

        case OrderSubmitted:
            Status = OrderStatus.Submitted;
            break;

        case OrderShipped e:
            Status = OrderStatus.Shipped;
            TrackingNumber = e.TrackingNumber;
            break;

        case OrderDelivered:
            Status = OrderStatus.Delivered;
            break;

        case OrderCancelled e:
            Status = OrderStatus.Cancelled;
            CancellationReason = e.Reason;
            break;

        // Important: Don't throw on unknown events
        // This allows for forward compatibility
    }
}
```

## Working with Aggregates

### Loading and Saving

```csharp
public class ShipOrderHandler : IActionHandler<ShipOrderAction>
{
    private readonly IEventSourcedRepository<Order, Guid> _repository;

    public async Task HandleAsync(ShipOrderAction action, CancellationToken ct)
    {
        // Load aggregate from event store
        var order = await _repository.GetByIdAsync(action.OrderId, ct);

        if (order is null)
            throw new OrderNotFoundException(action.OrderId);

        // Execute command (may raise events)
        order.Ship(action.TrackingNumber, action.Carrier);

        // Save (appends uncommitted events to store)
        await _repository.SaveAsync(order, ct);
    }
}
```

### Concurrency Handling

The repository handles optimistic concurrency:

```csharp
public async Task HandleAsync(UpdateOrderAction action, CancellationToken ct)
{
    var order = await _repository.GetByIdAsync(action.OrderId, ct);

    order.UpdateShippingAddress(action.NewAddress);

    try
    {
        await _repository.SaveAsync(order, ct);
    }
    catch (ConcurrencyException ex)
    {
        // Another process modified the aggregate
        // Options: retry, merge, or fail
        _logger.LogWarning(ex, "Concurrency conflict for order {OrderId}", action.OrderId);
        throw new OrderModifiedByAnotherUserException(action.OrderId);
    }
}
```

## Value Objects

Use value objects for complex properties:

```csharp
public record Address(
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country)
{
    public static Address Create(string street, string city, string state, string postalCode, string country)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(street))
            throw new ArgumentException("Street required", nameof(street));
        // ... more validation

        return new Address(street, city, state, postalCode, country);
    }
}

public record Money(decimal Amount, string Currency)
{
    public static Money USD(decimal amount) => new(amount, "USD");
    public static Money EUR(decimal amount) => new(amount, "EUR");

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add different currencies");
        return new Money(Amount + other.Amount, Currency);
    }
}
```

## Entities Within Aggregates

For complex aggregates, use entities:

```csharp
public class Order : AggregateRoot<Guid>
{
    private readonly List<OrderLine> _lines = [];
    public IReadOnlyList<OrderLine> Lines => _lines;

    public void AddLine(string productId, int quantity, decimal unitPrice)
    {
        var lineId = Guid.NewGuid();
        RaiseEvent(new OrderLineAdded(Id, lineId, productId, quantity, unitPrice));
    }

    public void UpdateLineQuantity(Guid lineId, int newQuantity)
    {
        var line = _lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new OrderLineNotFoundException(lineId);

        if (newQuantity <= 0)
        {
            RaiseEvent(new OrderLineRemoved(Id, lineId));
        }
        else
        {
            RaiseEvent(new OrderLineQuantityUpdated(Id, lineId, newQuantity));
        }
    }

    protected override void ApplyEventInternal(IDomainEvent @event)
    {
        switch (@event)
        {
            case OrderLineAdded e:
                _lines.Add(new OrderLine(e.LineId, e.ProductId, e.Quantity, e.UnitPrice));
                break;

            case OrderLineRemoved e:
                _lines.RemoveAll(l => l.Id == e.LineId);
                break;

            case OrderLineQuantityUpdated e:
                var line = _lines.First(l => l.Id == e.LineId);
                line.UpdateQuantity(e.NewQuantity);
                break;
        }
    }
}

public class OrderLine
{
    public Guid Id { get; }
    public string ProductId { get; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; }

    public OrderLine(Guid id, string productId, int quantity, decimal unitPrice)
    {
        Id = id;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    internal void UpdateQuantity(int newQuantity) => Quantity = newQuantity;
}
```

## Best Practices

### Do

- Keep aggregates small and focused
- Use factory methods for creation
- Validate in command methods before raising events
- Use pattern matching in ApplyEventInternal
- Make state changes only in ApplyEventInternal
- Use value objects for complex properties

### Don't

- Put business logic in ApplyEventInternal
- Throw exceptions in ApplyEventInternal
- Make ApplyEventInternal async
- Reference other aggregates directly
- Store derived data that can be computed

## Next Steps

- [Event Store](event-store.md) — Persist aggregate events
- [Snapshots](snapshots.md) — Optimize loading for long-lived aggregates
- [Domain Events](domain-events.md) — Define rich domain events

## See Also

- [Repositories](./repositories.md) — Load and save aggregates through the repository pattern
- [Aggregates (Domain Modeling)](../domain-modeling/aggregates.md) — Aggregate design principles from the domain modeling perspective
- [Event Application Pattern](./event-application-pattern.md) — Detailed guide to the pattern-matching event application approach
- [Domain Modeling Entities](../domain-modeling/entities.md) — Entity design patterns used within aggregates
