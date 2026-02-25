---
sidebar_position: 2
title: Repositories
description: Load and save aggregates with event-sourced repositories
---

# Event-Sourced Repositories

Repositories provide a high-level API for loading and saving event-sourced aggregates. They handle event store interactions, snapshot optimization, and concurrency control.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.EventSourcing
  dotnet add package Excalibur.Domain
  ```
- Familiarity with [event stores](./event-store.md) and [domain modeling](../domain-modeling/entities.md)

## The Repository Interface

```csharp
public interface IEventSourcedRepository<TAggregate, TKey>
    where TAggregate : class, IAggregateRoot<TKey>, IAggregateSnapshotSupport
    where TKey : notnull
{
    // Load aggregate by ID (rehydrates from events/snapshot)
    Task<TAggregate?> GetByIdAsync(TKey aggregateId, CancellationToken cancellationToken);

    // Persist uncommitted events
    Task SaveAsync(TAggregate aggregate, CancellationToken cancellationToken);

    // Save with ETag concurrency check (null skips validation)
    Task SaveAsync(TAggregate aggregate, string? expectedETag, CancellationToken cancellationToken);

    // Check if aggregate exists
    Task<bool> ExistsAsync(TKey aggregateId, CancellationToken cancellationToken);

    // Soft-delete via tombstone event
    Task DeleteAsync(TAggregate aggregate, CancellationToken cancellationToken);

    // Query for multiple aggregates
    Task<IReadOnlyList<TAggregate>> QueryAsync<TQuery>(TQuery query, CancellationToken cancellationToken)
        where TQuery : IAggregateQuery<TAggregate>;

    // Find single aggregate matching criteria
    Task<TAggregate?> FindAsync<TQuery>(TQuery query, CancellationToken cancellationToken)
        where TQuery : IAggregateQuery<TAggregate>;
}
```

## Basic Usage

### Loading Aggregates

```csharp
public class OrderService
{
    private readonly IEventSourcedRepository<Order, Guid> _repository;

    public async Task<OrderDto?> GetOrderAsync(Guid orderId, CancellationToken ct)
    {
        var order = await _repository.GetByIdAsync(orderId, ct);

        if (order is null)
            return null;

        return new OrderDto
        {
            Id = order.Id,
            Status = order.Status,
            Total = order.Total,
            Lines = order.Lines.Select(MapLine).ToList()
        };
    }
}
```

### Saving Aggregates

```csharp
public async Task CreateOrderAsync(CreateOrderCommand cmd, CancellationToken ct)
{
    // Create new aggregate (raises OrderCreated event)
    var order = new Order(cmd.OrderId, cmd.CustomerId);

    // Add lines (raises OrderLineAdded events)
    foreach (var line in cmd.Lines)
    {
        order.AddLine(line.ProductId, line.Quantity, line.UnitPrice);
    }

    // Save persists all uncommitted events
    await _repository.SaveAsync(order, ct);
}
```

### Modifying Aggregates

```csharp
public async Task AddLineToOrderAsync(Guid orderId, LineDto line, CancellationToken ct)
{
    // Load current state
    var order = await _repository.GetByIdAsync(orderId, ct)
        ?? throw new OrderNotFoundException(orderId);

    // Make changes (raises events)
    order.AddLine(line.ProductId, line.Quantity, line.UnitPrice);

    // Save new events
    await _repository.SaveAsync(order, ct);
}
```

## Optimistic Concurrency

### Using ETags

ETags prevent lost updates when multiple processes modify the same aggregate:

```csharp
public async Task UpdateOrderAsync(Guid orderId, UpdateRequest request, CancellationToken ct)
{
    var order = await _repository.GetByIdAsync(orderId, ct)
        ?? throw new OrderNotFoundException(orderId);

    // Capture ETag before modifications
    var originalETag = order.ETag;

    // Make changes
    order.UpdateShippingAddress(request.Address);

    try
    {
        // Save with ETag check
        await _repository.SaveAsync(order, originalETag, ct);
    }
    catch (ConcurrencyException)
    {
        // Another process modified the order
        throw new ConflictException("Order was modified by another process");
    }
}
```

### Automatic Concurrency (Version-Based)

The repository can use version numbers for concurrency:

```csharp
// Repository internally uses:
await eventStore.AppendAsync(
    aggregateId: order.Id.ToString(),
    aggregateType: "Order",
    events: order.GetUncommittedEvents(),
    expectedVersion: order.Version,  // Fails if version mismatch
    cancellationToken: ct);
```

## Repository Registration

### Using the Builder (Recommended)

```csharp
services.AddExcaliburEventSourcing(builder =>
{
    // With explicit factory
    builder.AddRepository<Order, Guid>(id => new Order());

    // Or use static Create method (aggregate must implement IAggregateRoot<TAggregate, TKey>)
    builder.AddRepository<Order, Guid>();  // Uses Order.Create(id)

    // String-keyed aggregates
    builder.AddRepository<Customer>(id => new Customer(id));
});
```

### Generic Registration

For simple scenarios without factories:

```csharp
builder.Services.AddSingleton(typeof(IEventSourcedRepository<,>),
    typeof(EventSourcedRepository<,>));
```

## Advanced Operations

### Checking Existence

```csharp
public async Task<bool> CanCreateOrderAsync(Guid orderId, CancellationToken ct)
{
    // Check without loading full aggregate
    return !await _repository.ExistsAsync(orderId, ct);
}
```

### Querying Aggregates

The repository supports querying via `IAggregateQuery<TAggregate>`:

```csharp
// Define a query
public class DraftOrdersQuery : IAggregateQuery<Order>
{
    public OrderStatus Status { get; init; } = OrderStatus.Draft;
}

// Usage - QueryAsync returns multiple matches
var draftOrders = await _repository.QueryAsync(new DraftOrdersQuery(), ct);

// Usage - FindAsync returns first match or null
var firstDraft = await _repository.FindAsync(new DraftOrdersQuery(), ct);
```

> **Note:** Query implementation depends on the underlying event store's capabilities.
> Some stores may require projections for efficient querying.

### Soft Delete

```csharp
public async Task CancelOrderAsync(Guid orderId, string reason, CancellationToken ct)
{
    var order = await _repository.GetByIdAsync(orderId, ct)
        ?? throw new OrderNotFoundException(orderId);

    // Cancel raises OrderCancelled event
    order.Cancel(reason);

    await _repository.SaveAsync(order, ct);
}

// In aggregate:
public void Cancel(string reason)
{
    if (Status == OrderStatus.Shipped)
        throw new InvalidOperationException("Cannot cancel shipped order");

    RaiseEvent(new OrderCancelled(Id, reason, DateTime.UtcNow));
}
```

## Working with the Event Store Directly

For advanced scenarios, access the event store:

```csharp
public class OrderHistoryService
{
    private readonly IEventStore _eventStore;

    public async Task<IReadOnlyList<EventInfo>> GetOrderHistoryAsync(
        Guid orderId,
        CancellationToken ct)
    {
        var events = await _eventStore.LoadAsync(
            orderId.ToString(),
            "Order",
            ct);

        return events.Select(e => new EventInfo
        {
            Version = e.Version,
            EventType = e.EventType,
            Timestamp = e.Timestamp,
            EventData = e.EventData
        }).ToList();
    }
}
```

## Repository Patterns

### Unit of Work Pattern

Coordinate multiple aggregate changes:

```csharp
public class OrderPlacementService
{
    private readonly IEventSourcedRepository<Order, Guid> _orderRepo;
    private readonly IEventSourcedRepository<Inventory, string> _inventoryRepo;
    private readonly IUnitOfWork _unitOfWork;

    public async Task PlaceOrderAsync(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var order = new Order(cmd.OrderId, cmd.CustomerId);

        foreach (var line in cmd.Lines)
        {
            // Reserve inventory
            var inventory = await _inventoryRepo.GetByIdAsync(line.ProductId, ct)
                ?? throw new ProductNotFoundException(line.ProductId);

            inventory.Reserve(line.Quantity, cmd.OrderId);

            // Add line to order
            order.AddLine(line.ProductId, line.Quantity, line.UnitPrice);
        }

        order.Submit();

        // Save all changes atomically
        await _unitOfWork.CommitAsync(ct);
    }
}
```

### Specification Pattern

Encapsulate query logic using `IAggregateQuery<T>`:

```csharp
public class DraftOrdersForCustomer : IAggregateQuery<Order>
{
    public string CustomerId { get; init; }
    public OrderStatus Status { get; init; } = OrderStatus.Draft;
}

// Usage
var query = new DraftOrdersForCustomer { CustomerId = "CUST-123" };
var orders = await _repository.QueryAsync(query, ct);
```

## Error Handling

### Common Exceptions

```csharp
try
{
    await _repository.SaveAsync(order, ct);
}
catch (ConcurrencyException ex)
{
    // Version mismatch - aggregate was modified
    _logger.LogWarning("Concurrency conflict for order {OrderId}", order.Id);
    throw new ConflictException("Order was modified");
}
catch (AggregateNotFoundException ex)
{
    // Aggregate doesn't exist
    throw new NotFoundException($"Order {ex.AggregateId} not found");
}
catch (EventStoreException ex)
{
    // Storage error
    _logger.LogError(ex, "Failed to save order {OrderId}", order.Id);
    throw;
}
```

### Retry Logic

```csharp
public async Task AddLineWithRetryAsync(
    Guid orderId,
    LineDto line,
    CancellationToken ct)
{
    var maxRetries = 3;

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            var order = await _repository.GetByIdAsync(orderId, ct)
                ?? throw new OrderNotFoundException(orderId);

            order.AddLine(line.ProductId, line.Quantity, line.UnitPrice);
            await _repository.SaveAsync(order, ct);
            return;
        }
        catch (ConcurrencyException) when (attempt < maxRetries)
        {
            _logger.LogWarning(
                "Concurrency conflict, attempt {Attempt} of {MaxRetries}",
                attempt, maxRetries);
            await Task.Delay(100 * attempt, ct);
        }
    }
}
```

## Performance Considerations

### Snapshot Integration

Repositories automatically use snapshots when available:

```csharp
// Repository internally:
// 1. Try to load latest snapshot
var snapshot = await _snapshotStore.GetLatestSnapshotAsync(aggregateId);

// 2. Load events since snapshot
var events = snapshot != null
    ? await _eventStore.LoadAsync(aggregateId, aggregateType, snapshot.Version, ct)
    : await _eventStore.LoadAsync(aggregateId, aggregateType, ct);

// 3. Apply snapshot, then remaining events
if (snapshot != null)
    aggregate.LoadFromSnapshot(snapshot);
aggregate.LoadFromHistory(events);
```

### Batch Loading

For scenarios requiring multiple aggregates:

```csharp
public async Task<IReadOnlyList<Order>> GetOrdersAsync(
    IEnumerable<Guid> orderIds,
    CancellationToken ct)
{
    var tasks = orderIds.Select(id => _repository.GetByIdAsync(id, ct));
    var orders = await Task.WhenAll(tasks);
    return orders.Where(o => o != null).ToList()!;
}
```

## Next Steps

- **[Snapshots](snapshots.md)** - Optimize loading for long-lived aggregates
- **[Projections](projections.md)** - Build read models from events
- **[Core Concepts](concepts.md)** - Event sourcing fundamentals

## See Also

- [Event Store](./event-store.md) — Understand how events are persisted and loaded
- [Aggregates](./aggregates.md) — Build the aggregates that repositories load and save
- [Repository Testing](../testing/repository-testing.md) — Test patterns for event-sourced repositories
- [Snapshots](./snapshots.md) — How repositories use snapshots to optimize aggregate loading
