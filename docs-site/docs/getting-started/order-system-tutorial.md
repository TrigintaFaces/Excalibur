---
sidebar_position: 6
title: "Tutorial: Build an Order System in 15 Minutes"
description: Step-by-step tutorial building a complete order management system with commands, queries, events, validation, and middleware using Excalibur.Dispatch.
---

# Build an Order System in 15 Minutes

This hands-on tutorial walks you through building a working order management API with Excalibur.Dispatch. You'll implement commands, queries, domain events, validation, and middleware — all running in-process with no external dependencies.

:::tip Prerequisites
- .NET 8.0+ SDK installed
- A code editor (VS Code, Rider, or Visual Studio)
- Basic familiarity with ASP.NET Core minimal APIs
:::

## What You'll Build

A REST API that can:
- **Create orders** (command)
- **Get an order by ID** (query with return value)
- **List orders by customer** (query with return value)
- **Cancel an order** (command with domain event)
- **React to order events** (event handler)

## Step 1: Create the Project

```bash
dotnet new web -n OrderSystem
cd OrderSystem
dotnet add package Excalibur.Dispatch
dotnet add package Excalibur.Dispatch.Hosting.AspNetCore
```

Two packages: `Excalibur.Dispatch` for the messaging core, and `Excalibur.Dispatch.Hosting.AspNetCore` for fluent minimal API result mapping. `Excalibur.Dispatch.Abstractions` is included as a transitive dependency.

## Step 2: Define Your Domain

Create a simple in-memory order model. In a real application, you'd use a database — but this tutorial focuses on the messaging patterns.

```csharp title="Domain/Order.cs"
namespace OrderSystem.Domain;

public class Order
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string CustomerId { get; init; } = string.Empty;
    public List<OrderLine> Lines { get; init; } = [];
    public decimal Total => Lines.Sum(l => l.Price * l.Quantity);
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public record OrderLine(string ProductId, string ProductName, decimal Price, int Quantity);

public enum OrderStatus { Pending, Confirmed, Cancelled }
```

Add a simple in-memory store:

```csharp title="Domain/OrderStore.cs"
using System.Collections.Concurrent;

namespace OrderSystem.Domain;

public class OrderStore
{
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();

    public void Add(Order order) => _orders[order.Id] = order;
    public Order? Get(Guid id) => _orders.GetValueOrDefault(id);
    public IReadOnlyList<Order> GetByCustomer(string customerId) =>
        _orders.Values.Where(o => o.CustomerId == customerId).ToList();
}
```

## Step 3: Define Messages

Excalibur.Dispatch uses three message types:
- **`IDispatchAction`** — commands (no return value)
- **`IDispatchAction<TResponse>`** — queries (with return value)
- **`IDispatchEvent`** — domain events (fan-out to multiple handlers)

```csharp title="Messages/OrderMessages.cs"
using Excalibur.Dispatch.Abstractions;

namespace OrderSystem.Messages;

// Commands
public record CreateOrderAction(
    string CustomerId,
    List<OrderLineRequest> Lines) : IDispatchAction;

public record CancelOrderAction(Guid OrderId, string Reason) : IDispatchAction;

// Queries
public record GetOrderQuery(Guid OrderId) : IDispatchAction<OrderDto?>;
public record GetCustomerOrdersQuery(string CustomerId) : IDispatchAction<IReadOnlyList<OrderDto>>;

// Events
public record OrderCreatedEvent(Guid OrderId, string CustomerId, decimal Total) : IDispatchEvent;
public record OrderCancelledEvent(Guid OrderId, string Reason) : IDispatchEvent;

// DTOs
public record OrderLineRequest(string ProductId, string ProductName, decimal Price, int Quantity);
public record OrderDto(Guid Id, string CustomerId, decimal Total, string Status, DateTime CreatedAt);
```

## Step 4: Implement Handlers

### Command Handlers

```csharp title="Handlers/CreateOrderHandler.cs"
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using OrderSystem.Domain;
using OrderSystem.Messages;

namespace OrderSystem.Handlers;

public class CreateOrderHandler(OrderStore store, IDispatcher dispatcher) : IActionHandler<CreateOrderAction>
{
    public async Task HandleAsync(CreateOrderAction action, CancellationToken cancellationToken)
    {
        var order = new Order
        {
            CustomerId = action.CustomerId,
            Lines = action.Lines
                .Select(l => new OrderLine(l.ProductId, l.ProductName, l.Price, l.Quantity))
                .ToList(),
            Status = OrderStatus.Confirmed,
        };

        store.Add(order);

        // Dispatch a domain event to notify other parts of the system
        await dispatcher.DispatchAsync(
            new OrderCreatedEvent(order.Id, order.CustomerId, order.Total),
            cancellationToken);
    }
}
```

```csharp title="Handlers/CancelOrderHandler.cs"
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using OrderSystem.Domain;
using OrderSystem.Messages;

namespace OrderSystem.Handlers;

public class CancelOrderHandler(OrderStore store, IDispatcher dispatcher) : IActionHandler<CancelOrderAction>
{
    public async Task HandleAsync(CancelOrderAction action, CancellationToken cancellationToken)
    {
        var order = store.Get(action.OrderId)
            ?? throw new InvalidOperationException($"Order {action.OrderId} not found.");

        order.Status = OrderStatus.Cancelled;

        await dispatcher.DispatchAsync(
            new OrderCancelledEvent(order.Id, action.Reason),
            cancellationToken);
    }
}
```

### Query Handlers

```csharp title="Handlers/GetOrderHandler.cs"
using Excalibur.Dispatch.Abstractions.Delivery;
using OrderSystem.Domain;
using OrderSystem.Messages;

namespace OrderSystem.Handlers;

public class GetOrderHandler(OrderStore store) : IActionHandler<GetOrderQuery, OrderDto?>
{
    public Task<OrderDto?> HandleAsync(GetOrderQuery action, CancellationToken cancellationToken)
    {
        var order = store.Get(action.OrderId);
        return Task.FromResult(order is null ? null : ToDto(order));
    }

    private static OrderDto ToDto(Order o) =>
        new(o.Id, o.CustomerId, o.Total, o.Status.ToString(), o.CreatedAt);
}

public class GetCustomerOrdersHandler(OrderStore store)
    : IActionHandler<GetCustomerOrdersQuery, IReadOnlyList<OrderDto>>
{
    public Task<IReadOnlyList<OrderDto>> HandleAsync(
        GetCustomerOrdersQuery action, CancellationToken cancellationToken)
    {
        var orders = store.GetByCustomer(action.CustomerId)
            .Select(o => new OrderDto(o.Id, o.CustomerId, o.Total, o.Status.ToString(), o.CreatedAt))
            .ToList();
        return Task.FromResult<IReadOnlyList<OrderDto>>(orders);
    }
}
```

### Event Handler

```csharp title="Handlers/OrderEventHandlers.cs"
using Excalibur.Dispatch.Abstractions.Delivery;
using OrderSystem.Messages;

namespace OrderSystem.Handlers;

public class OrderEventHandlers :
    IEventHandler<OrderCreatedEvent>,
    IEventHandler<OrderCancelledEvent>
{
    public Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Event] Order {@event.OrderId} created for customer {@event.CustomerId} — total: {@event.Total:C}");
        return Task.CompletedTask;
    }

    public Task HandleAsync(OrderCancelledEvent @event, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[Event] Order {@event.OrderId} cancelled — reason: {@event.Reason}");
        return Task.CompletedTask;
    }
}
```

## Step 5: Wire It Up

```csharp title="Program.cs"
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Hosting.AspNetCore;
using OrderSystem.Domain;
using OrderSystem.Messages;

var builder = WebApplication.CreateBuilder(args);

// Register Dispatch — auto-discovers all handlers in the entry assembly
builder.Services.AddDispatch();

// Register the in-memory store as a singleton
builder.Services.AddSingleton<OrderStore>();

var app = builder.Build();

// --- Endpoints using Railway-Oriented Programming ---

// Create order — 202 Accepted on success, auto ProblemDetails on failure
app.MapPost("/orders", (CreateOrderAction action, IDispatcher dispatcher, CancellationToken ct) =>
    dispatcher.DispatchAsync(action, ct)
        .ToApiResult());

// Get order — Map to DTO, Match for custom 404 handling
app.MapGet("/orders/{id:guid}", (Guid id, IDispatcher dispatcher, CancellationToken ct) =>
    dispatcher
        .DispatchAsync<GetOrderQuery, OrderDto?>(new GetOrderQuery(id), ct)
        .Match(
            onSuccess: dto => dto is not null ? Results.Ok(dto) : Results.NotFound(),
            onFailure: problem => Results.Problem(detail: problem?.Detail)));

// List by customer — 200 OK with value
app.MapGet("/orders/customer/{customerId}",
    (string customerId, IDispatcher dispatcher, CancellationToken ct) =>
        dispatcher
            .DispatchAsync<GetCustomerOrdersQuery, IReadOnlyList<OrderDto>>(
                new GetCustomerOrdersQuery(customerId), ct)
            .ToApiResult());

// Cancel order — 204 No Content on success
app.MapPost("/orders/{id:guid}/cancel",
    (Guid id, CancelRequest req, IDispatcher dispatcher, CancellationToken ct) =>
        dispatcher.DispatchAsync(new CancelOrderAction(id, req.Reason), ct)
            .ToNoContentResult());

app.Run();

record CancelRequest(string Reason);
```

:::tip Railway-Oriented Programming
Notice the endpoints don't use `if/else` or manual `result.IsSuccess` checks. Instead:
- **`.ToApiResult()`** — converts success to 200/202 and failure to ProblemDetails automatically
- **`.ToNoContentResult()`** — converts success to 204 No Content
- **`.Match()`** — gives you full control over both success and failure paths
- **`.Map()`** / **`.Tap()`** — transform values or add side effects mid-chain

Failures automatically produce RFC 7807 Problem Details responses. See [Results and Errors](../core-concepts/results-and-errors.md) for the full API.
:::

## Step 6: Run and Test

```bash
dotnet run
```

Test with curl (or your preferred HTTP client):

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

# Get the order (use the ID from the console output)
curl http://localhost:5000/orders/{order-id}

# List orders by customer
curl http://localhost:5000/orders/customer/cust-001

# Cancel an order
curl -X POST http://localhost:5000/orders/{order-id}/cancel \
  -H "Content-Type: application/json" \
  -d '{ "reason": "Customer changed their mind" }'
```

You should see event handler output in the console:

```
[Event] Order 3f2504e0-... created for customer cust-001 — total: $109.97
[Event] Order 3f2504e0-... cancelled — reason: Customer changed their mind
```

## What's Happening Under the Hood

1. **`AddDispatch()`** registers the dispatcher, message context factory, pipeline infrastructure, and auto-discovers all `IActionHandler<T>`, `IActionHandler<T, TResult>`, and `IEventHandler<T>` implementations from the entry assembly.
3. **`DispatchAsync()`** routes the message to the correct handler through the pipeline. Commands go to a single handler; events fan out to all registered handlers.
4. **Result extensions** (`.ToApiResult()`, `.Match()`, `.ToNoContentResult()`) convert `IMessageResult` to HTTP responses using railway-oriented programming — success flows forward, failures automatically produce RFC 7807 Problem Details.
5. **Event dispatch from handlers** — handlers can inject `IDispatcher` and dispatch events, enabling decoupled domain event workflows.

## Next Steps

You now have a working order system. Here's how to extend it:

| Want to... | Add this |
|-----------|----------|
| Validate input before handlers run | [`Excalibur.Dispatch.Validation.FluentValidation`](../middleware/built-in.md) |
| Add retry and circuit breaking | [`Excalibur.Dispatch.Resilience.Polly`](../middleware/built-in.md) |
| Trace requests with OpenTelemetry | [`Excalibur.Dispatch.Observability`](../observability/index.md) |
| Send messages to RabbitMQ/Kafka | `Excalibur.Dispatch.Transport.*` |
| Persist events with event sourcing | Follow the [Event-Sourced Order System](./event-sourcing-tutorial.md) tutorial — adds `AggregateRoot`, event store, and projections to this same order system |
| Use aggregates and DDD patterns | Same tutorial above — `Excalibur.Domain` provides `AggregateRoot<TKey>` |

See the [Dispatch Only](./dispatch-only.md) reference for a complete list of optional enhancements.
