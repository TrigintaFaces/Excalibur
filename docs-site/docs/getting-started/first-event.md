---
sidebar_position: 2
title: Your First Event
description: Create and handle domain events with Dispatch - learn event-driven patterns with multiple handlers
---

# Your First Event

Events represent something that has happened in your system. Unlike actions (commands), events can have **multiple handlers** - perfect for decoupling concerns like notifications, analytics, and integrations.

## Before You Start

- **.NET 10.0**
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
using Excalibur.Dispatch;

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
using Excalibur.Dispatch.Delivery;

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
builder.Services.AddDispatch();

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
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDispatch();

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
        : Results.Problem(result.ErrorMessage, statusCode: result.ProblemDetails?.Status);
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

:::caution Do not map a failed result to 400

A failed `IMessageResult` means your handler **ran** and reported a failure — not, by default, that
the caller sent a bad request. Hard-coding `Results.BadRequest(...)` reports a server-side fault to
the caller as their own mistake, so they retry a request that can never succeed, or abandon one that
would have.

`result.ProblemDetails.Status` carries the status the framework determined for that failure. With
the pipeline's exception mapping configured (`UseExceptionMapping()`), a validation failure arrives
as **400** and an authorization failure as **403**; a handler that threw with nothing mapping it
arrives as **500**. When no status was determined, `ProblemDetails` is `null` and `Results.Problem`
falls back to 500 — the safe direction, and never the caller's fault by accident.

The `Excalibur.Dispatch.Hosting.AspNetCore` package does the whole mapping in one call:
`return result.ToHttpResult();` — it honours an authorization failure (403) and a validation failure
(400) first, then `ProblemDetails.Status`, then falls back to 500. `ToNoContentResult()`,
`ToCreatedResult(location)` and the `Task`-chaining `ToApiResult()` cover the other success shapes.
:::

## Handler Execution Order

By default, event handlers execute in **parallel** for maximum throughput. You can control execution behavior through pipeline profiles:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Add global middleware (applies to all pipelines)
    dispatch.UseMiddleware<LoggingMiddleware>();

    // Configure a named pipeline for actions and events
    dispatch.ConfigurePipeline("Actions", pipeline =>
    {
        pipeline.ForMessageKinds(MessageKinds.Action)
                .Use<ValidationMiddleware>()
                .Use<AuthorizationMiddleware>();
    });

    dispatch.ConfigurePipeline("Events", pipeline =>
    {
        pipeline.ForMessageKinds(MessageKinds.Event);
    });
});
```

:::tip Planning to publish Native AOT or trimmed?

`AddHandlersFromAssembly` finds handlers by scanning the assembly with reflection
(`Assembly.GetTypes()`, then interface inspection). The trimmer cannot see a type that is only ever
reached that way, so under `PublishTrimmed=true` or `PublishAot=true` a handler with no other static
reference can be trimmed out of the app — and the failure is silent: the scan simply finds fewer
handlers, and the messages they would have handled go unhandled at run time.

This call does **not** raise a build warning. The single-assembly overload carries explicit
trim-analysis suppressions, so the toolchain stays quiet about a pattern it cannot actually verify.
Do not read the absence of a warning as a guarantee that trimming is safe here.

The AOT-safe equivalent registers the same handlers explicitly, discovered at compile time by the
source generator bundled in the `Excalibur.Dispatch` package — no reflection, and nothing for the
trimmer to remove:

```csharp
builder.Services.AddDispatch(dispatch => dispatch.AddDiscoveredHandlers());
```

Assembly scanning stays a fine choice for JIT-compiled apps, and it is the only option when handlers
live in an assembly the generator does not compile (a third-party or dynamically loaded plugin). See
[Native AOT](../advanced/native-aot.md).
:::

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

### What the publisher catches when a handler throws

Swallowing the exception, as above, is a choice — not a requirement. Every handler runs whether or not
its siblings fail, so letting the exception escape is safe, and it is what an exception mapper or a
typed exception handler needs in order to see the fault at all.

**Which exception the publisher catches does not depend on how many handlers you registered.** When one
handler fails, that handler's own exception is rethrown with its original stack trace — the same type
whether one handler is subscribed or ten. Only when **two or more** handlers fail for the same event do
you get an `AggregateException`, and its `InnerExceptions` carries every fault.

```csharp
try
{
    await bus.PublishAsync(new OrderCreatedEvent(...), context, ct);
}
catch (InventoryUnavailableException ex)
{
    // Reached whether one handler or several are subscribed, as long as only one failed.
}
catch (AggregateException ex)
{
    // Two or more handlers failed. ex.InnerExceptions has all of them.
}
```

That guarantee is why subscribing a second handler cannot silently break the first one's error handling:
a `catch`, a mapper, and a typed exception handler all select on the exception's type, so a sole fault
must not arrive wrapped.

## Key Concepts

| Concept | Description |
|---------|-------------|
| `IDispatchEvent` | Base interface for all events |
| `IEventHandler<TEvent>` | Handler interface for events |
| `DispatchAsync` | Dispatches event to all registered handlers |
| Multiple handlers | Same event can have many handlers |
| Parallel execution | Default behavior for throughput |

## Gotchas and Common Mistakes

### Dispatching from inside a handler? Just call `DispatchAsync`

When you dispatch a new message from within an existing handler, `DispatchAsync` automatically creates a child context that propagates correlation IDs, causation chains, and tenant information — there is nothing extra to call:

```csharp
public class CreateOrderHandler : IActionHandler<CreateOrderAction, Guid>
{
    private readonly IDispatcher _dispatcher;

    public async Task<Guid> HandleAsync(CreateOrderAction action, CancellationToken ct)
    {
        var orderId = Guid.NewGuid();

        // Called from within a handler, DispatchAsync automatically creates a
        // child context with proper lineage (fresh MessageId, CausationId set to
        // the parent's MessageId, propagating correlation/tenant/identity).
        await _dispatcher.DispatchAsync(
            new OrderCreatedEvent(orderId, action.CustomerId, 0m, DateTime.UtcNow), ct);

        // To deliberately reuse the parent context instead of childing, pass it
        // explicitly: DispatchAsync(message, context, ct).

        return orderId;
    }
}
```

Because `DispatchAsync` auto-childs when invoked from within a handler, the causal chain between parent and child messages is preserved automatically, keeping distributed tracing and debugging straightforward.

### Context is scoped per dispatch call

Each top-level `DispatchAsync` call gets its own `IMessageContext`. Items you set on the context in one handler are visible to middleware in that same pipeline, but **not** across separate dispatch calls:

```csharp
// Handler A sets a context item
context.SetItem("ProcessedBy", "HandlerA");

// A separate DispatchAsync call starts a NEW context --
// it will NOT see "ProcessedBy" from Handler A
await _dispatcher.DispatchAsync(new AnotherAction(), ct);
```

If you need to pass data between related dispatches, rely on `DispatchAsync`'s auto-child behavior (which copies correlation metadata when called from within a handler), or pass data explicitly through the message itself.

## What's Next

- [Project Templates](./project-templates.md) - Scaffold new projects quickly
- [Core Concepts](../core-concepts/index.md) - Understand pipelines and middleware
- [Patterns](../patterns/index.md) - Learn about Outbox for reliable event publishing

## See Also

- [Getting Started](index.md) - Installation and first project setup
- [Event Sourcing](../event-sourcing/index.md) - Full event sourcing with aggregates and projections
- [Domain Events](../event-sourcing/domain-events.md) - Domain event patterns and best practices
