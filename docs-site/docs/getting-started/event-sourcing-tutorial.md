---
sidebar_position: 7
title: "Tutorial: Event-Sourced Order System in 30 Minutes"
description: Extend the order system tutorial with AggregateRoot, event store, and projections using Excalibur.EventSourcing and SQL Server.
---

# Event-Sourced Order System in 30 Minutes

This tutorial extends the [Order System Tutorial](./order-system-tutorial.md) with event sourcing. Instead of storing current state, you'll store the sequence of events that produced the state — enabling full audit trails, temporal queries, and rebuild-from-history.

:::tip Prerequisites
- Completed the [Order System Tutorial](./order-system-tutorial.md)
- SQL Server available (LocalDB, Docker, or remote instance)
- Familiarity with domain-driven design concepts (aggregates, events)
:::

## What You'll Add

- **`AggregateRoot`** — an order aggregate that enforces business rules and raises domain events
- **`IEventStore`** — SQL Server persistence for event streams
- **`IEventSourcedRepository`** — load and save aggregates via their event history
- **Read model projection** — a denormalized query view built from events

## Step 1: Add Packages

```bash
cd OrderSystem
dotnet add package Excalibur.Dispatch.SqlServer
```

One metapackage bundles Dispatch + Domain + EventSourcing + SqlServer.

## Step 2: Define Domain Events

Domain events are immutable records that describe what happened. Extend the `DomainEvent` base record — it auto-generates `EventId`, `OccurredAt`, `Version`, and `EventType` for you.

```csharp title="Domain/Events.cs"
using Excalibur.Dispatch.Abstractions;

namespace OrderSystem.Domain;

public record OrderCreated(
    Guid OrderId,
    string CustomerId,
    List<OrderLineData> Lines) : DomainEvent
{
    public override string AggregateId => OrderId.ToString();
}

public record OrderLineAdded(
    Guid OrderId,
    string ProductId,
    string ProductName,
    decimal Price,
    int Quantity) : DomainEvent
{
    public override string AggregateId => OrderId.ToString();
}

public record OrderConfirmed(Guid OrderId, decimal Total) : DomainEvent
{
    public override string AggregateId => OrderId.ToString();
}

public record OrderCancelled(Guid OrderId, string Reason) : DomainEvent
{
    public override string AggregateId => OrderId.ToString();
}

// Shared data record for order lines (not an event)
public record OrderLineData(string ProductId, string ProductName, decimal Price, int Quantity);
```

## Step 3: Build the Aggregate

The aggregate is the consistency boundary. It validates commands, raises events, and applies them to update internal state via pattern matching.

```csharp title="Domain/OrderAggregate.cs"
using Excalibur.Dispatch.Abstractions;
using Excalibur.Domain.Model;

namespace OrderSystem.Domain;

public class OrderAggregate : AggregateRoot<Guid>
{
    private readonly List<OrderLineData> _lines = [];

    public string CustomerId { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public decimal Total { get; private set; }
    public IReadOnlyList<OrderLineData> Lines => _lines;

    // Required: parameterless constructor for hydration from events
    public OrderAggregate() { }

    // Convenience constructor for new aggregates
    public OrderAggregate(Guid id) : base(id) { }

    // --- Commands (public methods that enforce invariants and raise events) ---

    public void Create(string customerId, List<OrderLineData> lines)
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Order already created.");

        if (lines.Count == 0)
            throw new ArgumentException("Order must have at least one line.", nameof(lines));

        RaiseEvent(new OrderCreated(Id, customerId, lines));

        foreach (var line in lines)
        {
            RaiseEvent(new OrderLineAdded(Id, line.ProductId, line.ProductName, line.Price, line.Quantity));
        }
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Cannot confirm order in {Status} status.");

        RaiseEvent(new OrderConfirmed(Id, Total));
    }

    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Cancelled)
            throw new InvalidOperationException("Order is already cancelled.");

        RaiseEvent(new OrderCancelled(Id, reason));
    }

    // --- Event application (pattern matching, no business logic here) ---

    protected override void ApplyEventInternal(IDomainEvent @event) => _ = @event switch
    {
        OrderCreated e => Apply(e),
        OrderLineAdded e => Apply(e),
        OrderConfirmed e => Apply(e),
        OrderCancelled e => Apply(e),
        _ => throw new InvalidOperationException($"Unknown event: {@event.GetType().Name}")
    };

    private bool Apply(OrderCreated e)
    {
        Id = e.OrderId;
        CustomerId = e.CustomerId;
        Status = OrderStatus.Pending;
        return true;
    }

    private bool Apply(OrderLineAdded e)
    {
        _lines.Add(new OrderLineData(e.ProductId, e.ProductName, e.Price, e.Quantity));
        Total += e.Price * e.Quantity;
        return true;
    }

    private bool Apply(OrderConfirmed _)
    {
        Status = OrderStatus.Confirmed;
        return true;
    }

    private bool Apply(OrderCancelled _)
    {
        Status = OrderStatus.Cancelled;
        return true;
    }
}

public enum OrderStatus { Pending, Confirmed, Cancelled }
```

:::info Why Pattern Matching?
`ApplyEventInternal` uses a `switch` expression — no reflection, no virtual dispatch overhead. This is a deliberate design choice for performance. The aggregate knows all its event types at compile time.
:::

## Step 4: Create the Read Model

The read model (projection) is a denormalized view optimized for queries. It's built from events, not from the aggregate directly.

```csharp title="ReadModels/OrderSummary.cs"
namespace OrderSystem.ReadModels;

public class OrderSummary
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Status { get; set; } = "Pending";
    public int LineCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CancellationReason { get; set; }
}
```

## Step 5: Create Command Handlers

Handlers load the aggregate from the repository, call command methods, and save.

```csharp title="Handlers/OrderCommandHandlers.cs"
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.EventSourcing.Abstractions;
using OrderSystem.Domain;

namespace OrderSystem.Handlers;

// CreateOrderAction and CancelOrderAction are defined in Messages/OrderActions.cs below
public class CreateOrderHandler(
    IEventSourcedRepository<OrderAggregate, Guid> repository)
    : IActionHandler<CreateOrderAction>
{
    public async Task HandleAsync(CreateOrderAction action, CancellationToken ct)
    {
        var order = new OrderAggregate(Guid.NewGuid());
        order.Create(action.CustomerId, action.Lines);
        order.Confirm();

        await repository.SaveAsync(order, ct);
    }
}

// GetOrderQuery and GetCustomerOrdersQuery are defined in Messages/OrderActions.cs below
public class CancelOrderHandler(
    IEventSourcedRepository<OrderAggregate, Guid> repository)
    : IActionHandler<CancelOrderAction>
{
    public async Task HandleAsync(CancelOrderAction action, CancellationToken ct)
    {
        var order = await repository.GetByIdAsync(action.OrderId, ct)
            ?? throw new InvalidOperationException($"Order {action.OrderId} not found.");

        order.Cancel(action.Reason);

        await repository.SaveAsync(order, ct);
    }
}
```

```csharp title="Messages/OrderActions.cs"
using Excalibur.Dispatch.Abstractions;
using OrderSystem.Domain;
using OrderSystem.ReadModels;

namespace OrderSystem.Handlers;

// Commands
public record CreateOrderAction(
    string CustomerId,
    List<OrderLineData> Lines) : IDispatchAction;

public record CancelOrderAction(Guid OrderId, string Reason) : IDispatchAction;

// Queries
public record GetOrderQuery(Guid OrderId) : IDispatchAction<OrderSummary?>;
public record GetCustomerOrdersQuery(string CustomerId) : IDispatchAction<IReadOnlyList<OrderSummary>>;
```

## Step 6: Create Query Handlers with Projections

Query handlers read from the projection store — a denormalized, query-optimized view of the data.

```csharp title="Handlers/OrderQueryHandlers.cs"
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.EventSourcing.Abstractions;
using OrderSystem.ReadModels;

namespace OrderSystem.Handlers;

public class GetOrderHandler(
    IProjectionStore<OrderSummary> projections)
    : IActionHandler<GetOrderQuery, OrderSummary?>
{
    public async Task<OrderSummary?> HandleAsync(GetOrderQuery action, CancellationToken ct)
    {
        return await projections.GetByIdAsync(action.OrderId.ToString(), ct);
    }
}

public class GetCustomerOrdersHandler(
    IProjectionStore<OrderSummary> projections)
    : IActionHandler<GetCustomerOrdersQuery, IReadOnlyList<OrderSummary>>
{
    public async Task<IReadOnlyList<OrderSummary>> HandleAsync(
        GetCustomerOrdersQuery action, CancellationToken ct)
    {
        return await projections.QueryAsync(
            new Dictionary<string, object> { ["CustomerId"] = action.CustomerId },
            null,
            ct);
    }
}
```

## Step 7: Build the Projection from Events

An event handler listens to domain events and updates the read model. This is the "projection" -- it projects events into a queryable shape.

:::tip Inline projections for immediate consistency
This tutorial uses `IEventHandler<T>` for projections, which processes events through the Dispatch pipeline (eventually consistent). For **immediate read-after-write consistency**, use the `AddProjection<T>().Inline()` builder API instead -- see [Inline Projections](../event-sourcing/projections.md#inline-projections-projection-builder-api) and the [CQRS Program.cs template](./program-cs-templates.md#cqrs-with-event-sourcing-and-projections) for a complete example.
:::

```csharp title="Projections/OrderSummaryProjection.cs"
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.EventSourcing.Abstractions;
using OrderSystem.Domain;
using OrderSystem.ReadModels;

namespace OrderSystem.Projections;

public class OrderSummaryProjection(IProjectionStore<OrderSummary> store) :
    IEventHandler<OrderCreated>,
    IEventHandler<OrderLineAdded>,
    IEventHandler<OrderConfirmed>,
    IEventHandler<OrderCancelled>
{
    public async Task HandleAsync(OrderCreated @event, CancellationToken ct)
    {
        var summary = new OrderSummary
        {
            Id = @event.OrderId,
            CustomerId = @event.CustomerId,
            Status = "Pending",
            CreatedAt = @event.OccurredAt.UtcDateTime,
        };

        await store.UpsertAsync(@event.OrderId.ToString(), summary, ct);
    }

    public async Task HandleAsync(OrderLineAdded @event, CancellationToken ct)
    {
        var summary = await store.GetByIdAsync(@event.OrderId.ToString(), ct);
        if (summary is null) return;

        summary.LineCount++;
        summary.Total += @event.Price * @event.Quantity;

        await store.UpsertAsync(@event.OrderId.ToString(), summary, ct);
    }

    public async Task HandleAsync(OrderConfirmed @event, CancellationToken ct)
    {
        var summary = await store.GetByIdAsync(@event.OrderId.ToString(), ct);
        if (summary is null) return;

        summary.Status = "Confirmed";
        summary.Total = @event.Total;

        await store.UpsertAsync(@event.OrderId.ToString(), summary, ct);
    }

    public async Task HandleAsync(OrderCancelled @event, CancellationToken ct)
    {
        var summary = await store.GetByIdAsync(@event.OrderId.ToString(), ct);
        if (summary is null) return;

        summary.Status = "Cancelled";
        summary.CancellationReason = @event.Reason;

        await store.UpsertAsync(@event.OrderId.ToString(), summary, ct);
    }
}
```

## Step 8: Wire It Up

```csharp title="Program.cs"
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Hosting.AspNetCore;
using OrderSystem.Domain;
using OrderSystem.Handlers;
using OrderSystem.ReadModels;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("OrderDb")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=OrderSystem;Trusted_Connection=true;";

// Single call: Dispatch + SQL Server event sourcing (auto-discovers handlers)
builder.Services.AddDispatchWithSqlServer(connectionString);

// Register aggregate repository + projections
builder.Services.AddExcaliburEventSourcing(es =>
{
    es.AddRepository<OrderAggregate, Guid>(id => new OrderAggregate(id));
});
builder.Services.AddSqlServerProjectionStore<OrderSummary>(opts => opts.ConnectionString = connectionString);

var app = builder.Build();

// --- Endpoints using Railway-Oriented Programming ---

app.MapPost("/orders", (CreateOrderAction action, IDispatcher dispatcher, CancellationToken ct) =>
    dispatcher.DispatchAsync(action, ct)
        .ToApiResult());

app.MapGet("/orders/{id:guid}", (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
    dispatcher
        .DispatchAsync<GetOrderQuery, OrderSummary?>(new GetOrderQuery(id), ct)
        .Match(
            onSuccess: summary => summary is not null ? Results.Ok(summary) : Results.NotFound(),
            onFailure: problem => Results.Problem(detail: problem?.Detail)));

app.MapGet("/orders/customer/{customerId}",
    (string customerId, IDispatcher dispatcher, CancellationToken ct) =>
        dispatcher
            .DispatchAsync<GetCustomerOrdersQuery, IReadOnlyList<OrderSummary>>(
                new GetCustomerOrdersQuery(customerId), ct)
            .ToApiResult());

app.MapPost("/orders/{id:guid}/cancel",
    (Guid id, CancelRequest req, IDispatcher dispatcher, CancellationToken ct) =>
        dispatcher.DispatchAsync(new CancelOrderAction(id, req.Reason), ct)
            .ToNoContentResult());

app.Run();

record CancelRequest(string Reason);
```

## Step 9: Run and Test

```bash
dotnet run
```

```bash
# Create an order
curl -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust-001",
    "lines": [
      { "productId": "prod-1", "productName": "Widget", "price": 29.99, "quantity": 2 },
      { "productId": "prod-2", "productName": "Gadget", "price": 49.99, "quantity": 1 }
    ]
  }'

# Query the read model
curl http://localhost:5000/orders/customer/cust-001

# Cancel the order
curl -X POST http://localhost:5000/orders/{order-id}/cancel \
  -H "Content-Type: application/json" \
  -d '{ "reason": "Changed my mind" }'
```

## What's Different from the First Tutorial

| Aspect | Dispatch-Only Tutorial | Event-Sourced Tutorial |
|--------|----------------------|----------------------|
| **State storage** | In-memory `ConcurrentDictionary` | SQL Server event store |
| **Data model** | Current state (mutable) | Event stream (append-only) |
| **Aggregate** | None — handlers have all logic | `AggregateRoot<Guid>` enforces invariants |
| **Query model** | Same as write model | Separate `OrderSummary` projection |
| **Audit trail** | None | Complete — every event is preserved |
| **Business rules** | In handlers | In aggregate (single consistency boundary) |
| **Rebuild state** | Not possible | Replay events from any point in time |

## How Event Sourcing Works Here

```
CreateOrderAction (command)
    │
    ▼
CreateOrderHandler
    │ loads/creates aggregate
    ▼
OrderAggregate.Create(...)
    │ validates invariants
    │ raises OrderCreated, OrderLineAdded (x2)
    ▼
OrderAggregate.Confirm()
    │ raises OrderConfirmed
    ▼
repository.SaveAsync(order)
    │ appends events to IEventStore (SQL Server)
    │ dispatches events through pipeline
    ▼
OrderSummaryProjection (event handler)
    │ updates IProjectionStore<OrderSummary>
    ▼
GetOrderQuery → reads from projection store (fast, denormalized)
```

## Key Concepts

### Command vs Query Separation (CQRS)

- **Write side**: Commands go through the aggregate → events are appended to the event store
- **Read side**: Queries read from projections → denormalized views built from events
- The two sides are eventually consistent — projections update after events are dispatched

### Optimistic Concurrency

The repository uses `Version` to prevent concurrent writes:

```
Load: events [1, 2, 3] → aggregate at version 3
Save: "append events 4, 5 expecting version 3"
      → succeeds if no one else appended since version 3
      → throws ConcurrencyException if version has moved
```

### Event Replay

To rebuild an aggregate, the repository replays all events through `ApplyEventInternal`:

```
GetByIdAsync("order-123")
  → loads events [OrderCreated, OrderLineAdded, OrderConfirmed]
  → creates new OrderAggregate()
  → calls ApplyEventInternal for each event
  → returns fully hydrated aggregate at version 3
```

## Next Steps

| Want to... | Add this |
|-----------|----------|
| Snapshot large aggregates | Override `CreateSnapshot()` / `ApplySnapshot()` on your aggregate |
| Replay projections from scratch | Use `IMaterializedViewBuilder<T>` for batch rebuilds |
| Reliable event dispatch | Add the [Outbox Pattern](../patterns/outbox.md) for at-least-once delivery |
| Long-running workflows | Add [Sagas](../event-sourcing/index.md) for multi-aggregate coordination |
| Multiple read models | Register additional `IProjectionStore<T>` + event handlers per view |
| Authorization and audit logging | Continue to [Secure Order System in 45 Minutes](./secure-order-tutorial.md) — add grants, audit, and structured commands |

See [Event Sourcing Concepts](../event-sourcing/index.md) for the full architecture reference.
