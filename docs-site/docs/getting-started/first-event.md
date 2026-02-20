---
sidebar_position: 2
title: Your First Event
description: Create and handle domain events with Dispatch - learn event-driven patterns with multiple handlers
---

# Your First Event

Events represent something that has happened in your system. Unlike actions (commands), events can have **multiple handlers** - perfect for decoupling concerns like notifications, analytics, and integrations.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Complete the [getting started guide](./) and understand [actions and handlers](../core-concepts/actions-and-handlers.md)

## Prerequisites

Make sure you've completed [Getting Started](./) and understand actions and handlers.

## Events vs Actions

| Aspect | Action | Event |
|--------|--------|-------|
| Intent | "Do something" | "Something happened" |
| Handlers | One handler | Multiple handlers |
| Return value | Optional | None |
| Naming | `CreateOrderAction` | `OrderCreatedEvent` |

## Step 1: Define an Event

Events implement `IDispatchEvent` and describe what happened:

```csharp
using Excalibur.Dispatch.Abstractions;

// Event describing what happened
public record OrderCreatedEvent(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    DateTime CreatedAt) : IDispatchEvent;

// Event with rich domain information
public record OrderShippedEvent(
    Guid OrderId,
    string TrackingNumber,
    string Carrier,
    DateTime ShippedAt) : IDispatchEvent;
```

## Step 2: Create Event Handlers

Use `IEventHandler<TEvent>` to handle events. Multiple handlers can process the same event:

```csharp
using Excalibur.Dispatch.Abstractions.Delivery;

// Handler 1: Send confirmation email
public class OrderCreatedEmailHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ICustomerRepository _customers;

    public OrderCreatedEmailHandler(
        IEmailService emailService,
        ICustomerRepository customers)
    {
        _emailService = emailService;
        _customers = customers;
    }

    public async Task HandleAsync(
        OrderCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        var customer = await _customers.GetByIdAsync(
            @event.CustomerId, cancellationToken);

        await _emailService.SendOrderConfirmationAsync(
            customer.Email,
            @event.OrderId,
            @event.TotalAmount,
            cancellationToken);
    }
}

// Handler 2: Update analytics
public class OrderCreatedAnalyticsHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IAnalyticsService _analytics;

    public OrderCreatedAnalyticsHandler(IAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    public async Task HandleAsync(
        OrderCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        await _analytics.TrackOrderAsync(
            @event.OrderId,
            @event.TotalAmount,
            @event.CreatedAt,
            cancellationToken);
    }
}

// Handler 3: Sync to external system
public class OrderCreatedIntegrationHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IExternalOrderSystem _external;

    public OrderCreatedIntegrationHandler(IExternalOrderSystem external)
    {
        _external = external;
    }

    public async Task HandleAsync(
        OrderCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        await _external.SyncOrderAsync(
            @event.OrderId,
            @event.CustomerId,
            cancellationToken);
    }
}
```

## Step 3: Register and Dispatch

Register handlers and dispatch events:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// All three handlers will be discovered automatically
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Register dependencies
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IExternalOrderSystem, ExternalOrderSystem>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();

var app = builder.Build();
```

## Step 4: Publish Events

Publish events from your action handlers or services:

```csharp
public class CreateOrderHandler : IActionHandler<CreateOrderAction, Guid>
{
    private readonly IOrderRepository _orders;
    private readonly IDispatcher _dispatcher;

    public CreateOrderHandler(
        IOrderRepository orders,
        IDispatcher dispatcher)
    {
        _orders = orders;
        _dispatcher = dispatcher;
    }

    public async Task<Guid> HandleAsync(
        CreateOrderAction action,
        CancellationToken cancellationToken)
    {
        // Create the order
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = action.CustomerId,
            Items = action.Items,
            TotalAmount = action.Items.Sum(i => i.Price),
            CreatedAt = DateTime.UtcNow
        };

        await _orders.SaveAsync(order, cancellationToken);

        // Dispatch the event - all handlers will be invoked
        var @event = new OrderCreatedEvent(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.CreatedAt);

        await _dispatcher.DispatchAsync(@event, cancellationToken);

        return order.Id;
    }
}
```

## Complete Example

Here's a minimal working example:

```csharp title="Program.cs"
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

var app = builder.Build();

app.MapPost("/orders", async (
    CreateOrderRequest request,
    IDispatcher dispatcher,
    CancellationToken ct) =>
{
    var action = new CreateOrderAction(request.CustomerId, request.Items);
    var result = await dispatcher.DispatchAsync<CreateOrderAction, Guid>(action, ct);

    return result.IsSuccess
        ? Results.Created($"/orders/{result.ReturnValue}", new { Id = result.ReturnValue })
        : Results.BadRequest(result.ErrorMessage);
});

app.Run();

// Request DTO
public record CreateOrderRequest(string CustomerId, List<OrderItem> Items);
public record OrderItem(string ProductId, decimal Price);

// Action
public record CreateOrderAction(string CustomerId, List<OrderItem> Items)
    : IDispatchAction<Guid>;

// Event
public record OrderCreatedEvent(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    DateTime CreatedAt) : IDispatchEvent;

// Action Handler
public class CreateOrderHandler : IActionHandler<CreateOrderAction, Guid>
{
    private readonly IDispatcher _dispatcher;

    public CreateOrderHandler(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task<Guid> HandleAsync(CreateOrderAction action, CancellationToken ct)
    {
        var orderId = Guid.NewGuid();
        var total = action.Items.Sum(i => i.Price);

        // In real app: save to database here

        // Dispatch event to all handlers
        await _dispatcher.DispatchAsync(
            new OrderCreatedEvent(orderId, action.CustomerId, total, DateTime.UtcNow),
            ct);

        return orderId;
    }
}

// Event Handler 1: Log the order
public class OrderCreatedLogHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedLogHandler> _logger;

    public OrderCreatedLogHandler(ILogger<OrderCreatedLogHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        _logger.LogInformation(
            "Order {OrderId} created for customer {CustomerId}, total: {Total:C}",
            @event.OrderId, @event.CustomerId, @event.TotalAmount);
        return Task.CompletedTask;
    }
}

// Event Handler 2: Track metrics
public class OrderCreatedMetricsHandler : IEventHandler<OrderCreatedEvent>
{
    public Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        // In real app: increment counters, update dashboards
        Console.WriteLine($"[Metrics] Order total: {@event.TotalAmount:C}");
        return Task.CompletedTask;
    }
}
```

## Handler Execution Order

By default, event handlers execute in **parallel** for maximum throughput. You can control execution behavior through pipeline profiles:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Configure a named pipeline for events
    dispatch.ConfigurePipeline("Events", pipeline =>
    {
        pipeline.ForMessageKinds(MessageKinds.All);
    });
});
```

## Error Handling

Handle errors gracefully in your handlers:

Handle errors gracefully:

```csharp
public class ResilientEventHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly ILogger<ResilientEventHandler> _logger;

    public ResilientEventHandler(ILogger<ResilientEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        try
        {
            await ProcessEventAsync(@event, ct);
        }
        catch (Exception ex)
        {
            // Log but don't rethrow - allow other handlers to continue
            _logger.LogError(ex, "Failed to process event {EventType}", @event.GetType().Name);
        }
    }

    private Task ProcessEventAsync(OrderCreatedEvent @event, CancellationToken ct)
    {
        // Your logic here
        return Task.CompletedTask;
    }
}
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| `IDispatchEvent` | Base interface for all events |
| `IEventHandler<TEvent>` | Handler interface for events |
| `DispatchAsync` | Dispatches event to all registered handlers |
| Multiple handlers | Same event can have many handlers |
| Parallel execution | Default behavior for throughput |

## What's Next

- [Project Templates](./project-templates.md) - Scaffold new projects quickly
- [Core Concepts](../core-concepts/index.md) - Understand pipelines and middleware
- [Patterns](../patterns/index.md) - Learn about Outbox for reliable event publishing

## See Also

- [Getting Started](index.md) - Installation and first project setup
- [Event Sourcing](../event-sourcing/index.md) - Full event sourcing with aggregates and projections
- [Domain Events](../event-sourcing/domain-events.md) - Domain event patterns and best practices
