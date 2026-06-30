---
sidebar_position: 1
---

# Migrating from MediatR

A comprehensive guide for migrating from MediatR to Excalibur.Dispatch, covering API differences, feature mapping, and migration strategies.

:::caution Trademark and non-affiliation notice
"MediatR" is a trademark of the MediatR project and its respective owner(s).
Excalibur.Dispatch and the `Excalibur.Dispatch.Compat.MediatR` compatibility package are
**independent** and are **not affiliated with, sponsored by, or endorsed by** the MediatR
project or its owner(s). The compatibility surface exists solely to assist migration and
interoperability; it is **not** a redistribution of MediatR's source code. Nothing here is
legal advice — you remain solely responsible for your own license compliance regarding any
third-party software you migrate from or to. See the full
[Trademark and Non-Affiliation Notice](./compat-mediatr-disclaimer.md).
:::

## Before You Start

- **.NET 10.0**
- An existing application using MediatR
- Familiarity with [getting started](../getting-started/index.md) and [actions and handlers](../core-concepts/actions-and-handlers.md)

## Overview

Excalibur is designed as a **production-ready alternative to MediatR** with enhanced features for event sourcing, domain-driven design, and reliable messaging. This guide helps you migrate smoothly while gaining new capabilities.

## Two migration paths

There are two supported ways to move off MediatR, and you can mix them per file:

1. **Drop-in compatibility shim (fastest).** Reference the `Excalibur.Dispatch.Compat.MediatR`
   package, swap `using MediatR;` → `using Excalibur.Dispatch.Compat.MediatR;`, and rename your
   registration call. Your existing `IRequest`/`IRequestHandler`/`INotification`/`IPipelineBehavior`
   code compiles unchanged against source-compatible shapes that forward to Excalibur.Dispatch. A
   bundled Roslyn analyzer + code-fix performs the mechanical edits for you. **Start here** — it gets
   you off the commercial MediatR package with the least churn.
2. **Rewrite to the canonical API (idiomatic).** Replace MediatR shapes with the native
   `IDispatchAction`/`IActionHandler`/`IDomainEvent`/`IDispatchMiddleware` types. This is more edits up
   front but unlocks the full Excalibur programming model (richer `IMessageContext`, transport-aware
   routing, event-sourcing integration). The [side-by-side comparison](#side-by-side-comparison) and
   [step-by-step migration](#step-by-step-migration) below cover this path.

A common strategy is to run the shim first to get compiling on Excalibur quickly, then rewrite
high-value handlers to the canonical API over time.

## Drop-in compatibility shim

:::info Package
The compatibility surface ships as a separate, isolated package — `Excalibur.Dispatch.Compat.MediatR`
— that depends on `Excalibur.Dispatch`. The canonical packages never depend on it, so the compat
surface stays opt-in and isolated from the core framework.
:::

### Step 1: Add the packages

```bash
dotnet add package Excalibur.Dispatch.Compat.MediatR
```

The bundled analyzer + code-fix packages (`Excalibur.Dispatch.Migration.Analyzers` and
`Excalibur.Dispatch.Migration.CodeFixes`) are referenced as analyzers and surface the `EXMIG####`
migration diagnostics in your IDE and build output.

### Step 2: Swap the namespace

The shim provides the same interface shapes MediatR-based code references, in a new namespace:

```diff
- using MediatR;
+ using Excalibur.Dispatch.Compat.MediatR;
```

Diagnostic **[EXMIG0003](../diagnostics/EXMIG0003.md)** flags every `using MediatR;` directive and its
code-fix performs the swap idempotently.

The shim provides source-compatible shapes for the published MediatR contract:

| Compatibility type | Notes |
|--------------------|-------|
| `IRequest`, `IRequest<TResponse>` | Marker interfaces for requests. |
| `IRequestHandler<TRequest, TResponse>`, `IRequestHandler<TRequest>` | Handler method is `Handle(...)` — MediatR's name is preserved. |
| `INotification`, `INotificationHandler<TNotification>` | Many handlers per notification are supported. |
| `IPipelineBehavior<TRequest, TResponse>`, `RequestHandlerDelegate<TResponse>` | Behaviors nest around the handler in registration order. |
| `IStreamRequest<TResponse>`, `IStreamRequestHandler<TRequest, TResponse>` | Streaming requests via `IAsyncEnumerable<T>`. |
| `IMediator`, `ISender`, `IPublisher` | Inject any of these; `Send`, `Publish`, and `CreateStream` keep MediatR's names. |
| `Unit` | The MediatR void-result type, including `Unit.Value` and `Unit.Task`. |

### Step 3: Rename the registration call

**Before (MediatR):**
```csharp
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

**After (compat):**
```csharp
builder.Services.AddMediatRCompat(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

Diagnostic **[EXMIG0001](../diagnostics/EXMIG0001.md)** flags `AddMediatR(...)` calls and its code-fix
rewrites them to `AddMediatRCompat(...)`, preserving the assembly-scan arguments. `AddMediatRCompat`
self-bootstraps the Dispatch core (it calls `AddDispatch()` internally, idempotently), validates its
options at startup, and accepts the familiar configuration entry points:

```csharp
builder.Services.AddMediatRCompat(cfg =>
{
    // Assembly registration (any of these)
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.RegisterServicesFromAssemblies(asmA, asmB);
    cfg.RegisterServicesFromAssemblyContaining<SomeHandler>();

    // Handler lifetime (default: Transient)
    cfg.HandlerLifetime = ServiceLifetime.Scoped;

    // Pipeline behaviors — run in registration order, nested around the handler
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddBehavior<IPipelineBehavior<Ping, Pong>, PingPongBehavior>();
});
```

:::note Compile-time handler discovery
Handler registration is **source-generated** at compile time (no reflection scan on the consumer
path), which keeps the shim AOT-safe. `RegisterServicesFrom*` selects which of your assemblies the
generated registrations apply to.
:::

### Step 4: Resolve the remaining diagnostics

The analyzer surfaces anything the swap cannot mechanically rewrite, so nothing is silently skipped:

| Diagnostic | Meaning | Fix |
|------------|---------|-----|
| **[EXMIG0001](../diagnostics/EXMIG0001.md)** | `AddMediatR(...)` registration is portable. | Code-fix → `AddMediatRCompat(...)`. |
| **[EXMIG0002](../diagnostics/EXMIG0002.md)** | A construct outside the compat contract (pre/post processors, exception handlers/actions, stream pipeline behaviors). | Manual migration step — see [unsupported constructs](#unsupported-mediatr-constructs). |
| **[EXMIG0003](../diagnostics/EXMIG0003.md)** | A `using MediatR;` directive. | Code-fix → `using Excalibur.Dispatch.Compat.MediatR;`. |
| **[EXMIG0004](../diagnostics/EXMIG0004.md)** | A handler method name differs from the compat shape's `Handle`. | Code-fix renames a deterministic delta (e.g. `HandleAsync` → `Handle`); other deltas are described for manual change. |

### Runtime behavior

- **Requests** resolve to exactly one handler within the registered assemblies. A second handler for
  the same request type fails fast at registration with `DuplicateRequestHandlerException`.
- A request with no registered handler throws `HandlerNotFoundException` when sent.
- **Notifications** may have many handlers; `Publish` to a notification with no handlers is a no-op.
- **Pipeline behaviors** execute in registration order, nested around the handler (A → B → handler →
  B → A), matching MediatR's ordering semantics.

### Unsupported MediatR constructs

The shim covers the published MediatR contract that consumer code references. Constructs flagged by
**EXMIG0002** are *not* part of the compat surface and have no automatic rewrite:

- `IRequestPreProcessor<TRequest>` / `IRequestPostProcessor<TRequest, TResponse>`
- `IRequestExceptionHandler<,,>` / `IRequestExceptionAction<,>`
- `IStreamPipelineBehavior<TRequest, TResponse>`

Re-express these using Excalibur.Dispatch middleware (`IDispatchMiddleware`) on the canonical path —
pre/post processing and exception handling map naturally onto middleware stages. See
[Pipeline Behaviors](#pipeline-behaviors) below.

## Key Differences

| Feature | MediatR | Excalibur |
|---------|---------|-------------------|
| **Core Focus** | Simple mediator pattern | High-performance messaging framework with optional event sourcing and DDD capabilities |
| **Request/Response** | `IRequest<T>` / `IRequestHandler<T>` | `IDispatchAction<T>` / `IActionHandler<T, R>` |
| **Notifications** | `INotification` / `INotificationHandler` | `IDomainEvent` / `IEventHandler` |
| **Pipeline Behaviors** | `IPipelineBehavior<TRequest, TResponse>` | `IDispatchMiddleware` |
| **Event Sourcing** | Not included | Built-in with `IEventStore`, `AggregateRoot` |
| **Outbox Pattern** | Not included | Built-in with `IOutboxStore`, `IOutboxProcessor` |
| **Metadata** | Limited | Rich metadata with `IMessageContext` |
| **Async Only** | Supports both sync/async | Async-only (modern best practice) |

## Side-by-Side Comparison

The patterns below show the **canonical-API rewrite** path — replacing MediatR shapes with native
Excalibur.Dispatch types. If you started with the [drop-in shim](#drop-in-compatibility-shim), adopt
these idiomatic patterns incrementally, handler by handler.

### Request/Response Pattern

**MediatR:**
```csharp
// Request
public record CreateOrderRequest(string CustomerId, List<OrderItem> Items)
    : IRequest<CreateOrderResponse>;

// Handler
public class CreateOrderHandler
    : IRequestHandler<CreateOrderRequest, CreateOrderResponse>
{
    public async Task<CreateOrderResponse> Handle(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        // Create order
        var orderId = Guid.NewGuid().ToString();

        return new CreateOrderResponse(orderId);
    }
}

// Usage
var response = await _mediator.Send(new CreateOrderRequest(customerId, items));
```

**Excalibur.Dispatch:**
```csharp
// Command
public record CreateOrderCommand(string CustomerId, List<OrderItem> Items)
    : IDispatchAction<CreateOrderResult>;

// Handler
public class CreateOrderCommandHandler
    : IActionHandler<CreateOrderCommand, CreateOrderResult>
{
    public async Task<CreateOrderResult> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        // Create order
        var orderId = Guid.NewGuid().ToString();

        return new CreateOrderResult(orderId);
    }
}

// Usage
var result = await _dispatcher.DispatchAsync(
    new CreateOrderCommand(customerId, items),
    cancellationToken);
```

**Key Changes:**
- `IRequest<T>` → `IDispatchAction<T>` (clearer intent)
- `IRequestHandler<T, R>` → `IActionHandler<T, R>`
- `Handle()` → `HandleAsync()` (explicit async)
- `Send()` → `DispatchAsync()` (consistent async naming)

### Notification Pattern

**MediatR:**
```csharp
// Notification
public record OrderCreatedNotification(string OrderId, decimal TotalValue)
    : INotification;

// Handler
public class OrderCreatedEmailHandler
    : INotificationHandler<OrderCreatedNotification>
{
    public async Task Handle(
        OrderCreatedNotification notification,
        CancellationToken cancellationToken)
    {
        // Send email
    }
}

// Usage
await _mediator.Publish(new OrderCreatedNotification(orderId, total));
```

**Excalibur.Dispatch:**
```csharp
// Domain Event
public record OrderCreatedEvent(
    string OrderId,
    decimal TotalValue,
    string CustomerId) : IDomainEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public string AggregateId { get; init; } = OrderId;
    public long Version { get; init; } = 1;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType { get; init; } = nameof(OrderCreatedEvent);
    public Dictionary<string, object> Metadata { get; init; } = new();
}

// Handler
public class OrderCreatedEmailHandler
    : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(
        OrderCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        // Send email
    }
}

// Usage
await _dispatcher.DispatchAsync(
    new OrderCreatedEvent(orderId, total, customerId),
    cancellationToken);
```

**Key Changes:**
- `INotification` → `IDomainEvent` (richer interface with metadata)
- `INotificationHandler<T>` → `IEventHandler<T>`
- Events include: `EventId`, `AggregateId`, `Version`, `OccurredAt`, `EventType`, `Metadata`
- Better support for event sourcing and auditing

### Pipeline Behaviors

**MediatR:**
```csharp
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {RequestName}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Handled {RequestName}", typeof(TRequest).Name);
        return response;
    }
}

// Registration
services.AddTransient(
    typeof(IPipelineBehavior<,>),
    typeof(LoggingBehavior<,>));
```

**Excalibur.Dispatch:**
```csharp
public class LoggingMiddleware : IDispatchMiddleware
{
    private readonly ILogger<LoggingMiddleware> _logger;

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {MessageType}", message.GetType().Name);
        var result = await nextDelegate(message, context, cancellationToken);
        _logger.LogInformation("Handled {MessageType}", message.GetType().Name);
        return result;
    }
}

// Registration
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseMiddleware<LoggingMiddleware>();
});
```

**Key Changes:**
- `IPipelineBehavior<TRequest, TResponse>` → `IDispatchMiddleware` (unified middleware)
- Middleware has a `Stage` property for pipeline ordering
- Uses `DispatchRequestDelegate` instead of `RequestHandlerDelegate`
- Fluent configuration via `AddDispatch()` with `UseMiddleware<T>()`

## Migration Strategies

### Strategy 1: Side-by-Side (Recommended)

Run both frameworks in parallel during migration:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Keep MediatR for existing code
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// Add Dispatch for new code
builder.Services.AddDispatch(typeof(Program).Assembly);

var app = builder.Build();
```

**Gradual Migration:**
1. New features use Dispatch
2. Migrate high-value endpoints first
3. Leave low-touch code on MediatR until convenient
4. Remove MediatR once migration complete

### Strategy 2: Adapter Pattern

Wrap MediatR handlers in Dispatch handlers:

```csharp
// Adapter for MediatR requests
public class MediatRCommandAdapter<TCommand, TResponse>
    : IActionHandler<TCommand, TResponse>
    where TCommand : IDispatchAction<TResponse>, IRequest<TResponse>
{
    private readonly IMediator _mediator;

    public MediatRCommandAdapter(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<TResponse> HandleAsync(
        TCommand command,
        CancellationToken cancellationToken)
    {
        // Delegate to MediatR
        return await _mediator.Send(command, cancellationToken);
    }
}

// Usage: Commands implement both interfaces during migration
public record CreateOrderCommand(string CustomerId)
    : IDispatchAction<CreateOrderResult>,
      IRequest<CreateOrderResult>;
```

**Benefits:**
- Migrate interface first, implementation later
- Test Dispatch pipeline with existing handlers
- Minimal code changes initially

### Strategy 3: Big Bang (Not Recommended)

Replace MediatR entirely in one release. Only viable for small codebases.

## Step-by-Step Migration

### Step 1: Install Excalibur.Dispatch

```bash
dotnet add package Excalibur.Dispatch
```

### Step 2: Update Registration

**Before (MediatR):**
```csharp
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

**After (Dispatch):**
```csharp
builder.Services.AddDispatch(typeof(Program).Assembly);
```

### Step 3: Update Commands

**Before:**
```csharp
public record PlaceOrderCommand(string CustomerId, List<OrderItem> Items)
    : IRequest<PlaceOrderResult>;
```

**After:**
```csharp
public record PlaceOrderCommand(string CustomerId, List<OrderItem> Items)
    : IDispatchAction<PlaceOrderResult>;
```

**Changes:**
- `IRequest<T>` → `IDispatchAction<T>`

### Step 4: Update Handlers

**Before:**
```csharp
public class PlaceOrderHandler : IRequestHandler<PlaceOrderCommand, PlaceOrderResult>
{
    public async Task<PlaceOrderResult> Handle(
        PlaceOrderCommand request,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

**After:**
```csharp
public class PlaceOrderCommandHandler : IActionHandler<PlaceOrderCommand, PlaceOrderResult>
{
    public async Task<PlaceOrderResult> HandleAsync(
        PlaceOrderCommand command,
        CancellationToken cancellationToken)
    {
        // Implementation (same code)
    }
}
```

**Changes:**
- `IRequestHandler<T, R>` → `IActionHandler<T, R>`
- `Handle()` → `HandleAsync()`
- `request` → `command` (naming convention)

### Step 5: Update Events

**Before:**
```csharp
public record OrderPlacedNotification(string OrderId) : INotification;

public class OrderPlacedHandler : INotificationHandler<OrderPlacedNotification>
{
    public async Task Handle(
        OrderPlacedNotification notification,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

**After:**
```csharp
public record OrderPlacedEvent(string OrderId) : IDomainEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    public string AggregateId { get; init; } = OrderId;
    public long Version { get; init; } = 1;
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string EventType { get; init; } = nameof(OrderPlacedEvent);
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public class OrderPlacedEventHandler : IEventHandler<OrderPlacedEvent>
{
    public async Task HandleAsync(
        OrderPlacedEvent @event,
        CancellationToken cancellationToken)
    {
        // Implementation (same code)
    }
}
```

**Changes:**
- `INotification` → `IDomainEvent` (with required properties)
- `INotificationHandler<T>` → `IEventHandler<T>`
- Add event metadata properties

### Step 6: Update Pipeline Behaviors

**Before:**
```csharp
public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Validate
        var response = await next();
        return response;
    }
}
```

**After:**
```csharp
public class ValidationMiddleware : IDispatchMiddleware
{
    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        // Validate (same code)
        var result = await nextDelegate(message, context, cancellationToken);
        return result;
    }
}
```

**Changes:**
- `IPipelineBehavior<TRequest, TResponse>` → `IDispatchMiddleware`
- Uses `Stage` property for pipeline ordering
- `Handle()` → `InvokeAsync()` with `DispatchRequestDelegate`

### Step 7: Update Usage

**Before:**
```csharp
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    [HttpPost]
    public async Task<IActionResult> PlaceOrder(
        PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        var command = new PlaceOrderCommand(request.CustomerId, request.Items);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
```

**After:**
```csharp
public class OrdersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    [HttpPost]
    public async Task<IActionResult> PlaceOrder(
        PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        var command = new PlaceOrderCommand(request.CustomerId, request.Items);
        var result = await _dispatcher.DispatchAsync(command, cancellationToken);
        return Ok(result);
    }
}
```

**Changes:**
- `IMediator` → `IDispatcher`
- `Send()` → `DispatchAsync()`

## New Capabilities in Dispatch

:::info Optional Features

The capabilities below are optional add-ons available in separate packages. They are **not** required for a successful MediatR migration. Many teams use Excalibur.Dispatch exclusively for messaging without any of these.
:::

### Event Sourcing

Dispatch includes built-in event sourcing support:

```csharp
// Aggregate Root
public class Order : AggregateRoot
{
    public string CustomerId { get; private set; } = string.Empty;
    public decimal TotalValue { get; private set; }

    // Factory method
    public static Order Create(string orderId, string customerId, List<OrderItem> items)
    {
        var order = new Order { Id = orderId };
        order.RaiseEvent(new OrderCreatedEvent(orderId, customerId, items));
        return order;
    }

    // Event application
    protected override void ApplyEventInternal(IDomainEvent @event)
    {
        switch (@event)
        {
            case OrderCreatedEvent e:
                Id = e.OrderId;
                CustomerId = e.CustomerId;
                TotalValue = e.Items.Sum(i => i.Price * i.Quantity);
                break;
        }
    }
}

// Repository
public class OrderCommandHandler : IActionHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IEventSourcedRepository<Order> _repository;

    public async Task<CreateOrderResult> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = Order.Create(
            Guid.NewGuid().ToString(),
            command.CustomerId,
            command.Items);

        await _repository.SaveAsync(order, cancellationToken);

        return new CreateOrderResult(order.Id);
    }
}
```

**MediatR equivalent:** None - requires custom implementation

### Outbox Pattern

Reliable message publishing with transactional outbox:

```csharp
// Register handlers
builder.Services.AddDispatch(typeof(Program).Assembly);

// Add outbox with SQL Server storage and processing options
builder.Services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
{
    outbox.UseSqlServer(opts => opts.ConnectionString = connectionString)
          .WithProcessing(p => p.PollingInterval(TimeSpan.FromSeconds(5)));
}));

// Events are automatically stored in outbox
public class OrderCommandHandler : IActionHandler<CreateOrderCommand>
{
    private readonly IEventSourcedRepository<Order> _repository;

    public async Task HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = Order.Create(command.OrderId, command.CustomerId);

        // Events saved to outbox atomically with aggregate
        await _repository.SaveAsync(order, cancellationToken);
    }
}

// Background processor publishes events reliably
```

**MediatR equivalent:** None - requires custom implementation

### Message Context

Rich metadata for every message:

```csharp
public class AuditMiddleware : IDispatchMiddleware
{
    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate nextDelegate,
        CancellationToken cancellationToken)
    {
        // Access context
        var correlationId = context.CorrelationId;
        var causationId = context.CausationId;
        var userId = context.UserId;

        // Set custom data in Items dictionary
        context.SetItem("audit.timestamp", DateTimeOffset.UtcNow);

        var result = await nextDelegate(message, context, cancellationToken);
        return result;
    }
}
```

**MediatR equivalent:** Limited - requires custom context injection

## Testing Migration

### Unit Tests

**Before (MediatR):**
```csharp
[Fact]
public async Task Handle_ShouldCreateOrder()
{
    // Arrange
    var handler = new CreateOrderHandler(_repository);
    var request = new CreateOrderRequest("customer-1", items);

    // Act
    var result = await handler.Handle(request, CancellationToken.None);

    // Assert
    result.OrderId.ShouldNotBeNullOrEmpty();
}
```

**After (Dispatch):**
```csharp
[Fact]
public async Task HandleAsync_ShouldCreateOrder()
{
    // Arrange
    var handler = new CreateOrderCommandHandler(_repository);
    var command = new CreateOrderCommand("customer-1", items);

    // Act
    var result = await handler.HandleAsync(command, CancellationToken.None);

    // Assert
    result.OrderId.ShouldNotBeNullOrEmpty();
}
```

**Changes:** Minimal - just method name and interface

### Integration Tests

**Before (MediatR):**
```csharp
[Fact]
public async Task Send_ShouldCreateOrder()
{
    // Arrange
    var command = new CreateOrderCommand("customer-1", items);

    // Act
    var result = await _mediator.Send(command);

    // Assert
    result.OrderId.ShouldNotBeNullOrEmpty();
}
```

**After (Dispatch):**
```csharp
[Fact]
public async Task DispatchAsync_ShouldCreateOrder()
{
    // Arrange
    var command = new CreateOrderCommand("customer-1", items);

    // Act
    var result = await _dispatcher.DispatchAsync(command, CancellationToken.None);

    // Assert
    result.OrderId.ShouldNotBeNullOrEmpty();
}
```

**Changes:** `Send()` → `DispatchAsync()`, add `CancellationToken`

## Common Migration Issues

### Issue 1: Missing CancellationToken

**Problem:**
```csharp
// MediatR allowed this
var result = await _mediator.Send(command);
```

**Solution:**
```csharp
// Dispatch requires explicit cancellation token
var result = await _dispatcher.DispatchAsync(command, cancellationToken);
```

### Issue 2: Synchronous Handlers

**Problem:**
```csharp
// MediatR supported sync handlers
public class MyHandler : IRequestHandler<MyRequest>
{
    public Unit Handle(MyRequest request)
    {
        // Sync implementation
        return Unit.Value;
    }
}
```

**Solution:**
```csharp
// Dispatch is async-only
public class MyCommandHandler : IActionHandler<MyCommand>
{
    public Task HandleAsync(MyCommand command, CancellationToken cancellationToken)
    {
        // Convert to async
        return Task.CompletedTask;
    }
}
```

### Issue 3: Notification Ordering

**Problem:** MediatR notifications are unordered by default.

**Solution:** Dispatch events maintain order within aggregate:

```csharp
// Events applied in order
public class Order : AggregateRoot
{
    protected override void ApplyEventInternal(IDomainEvent @event)
    {
        switch (@event)
        {
            case OrderCreatedEvent e:
                ApplyEvent(e);
                break;
            case OrderItemAddedEvent e:
                ApplyEvent(e);
                break;
            case OrderSubmittedEvent e:
                ApplyEvent(e);
                break;
        }
    }

    // Private helper methods for each event type
    private void ApplyEvent(OrderCreatedEvent e) { /* set state */ }
    private void ApplyEvent(OrderItemAddedEvent e) { /* set state */ }
    private void ApplyEvent(OrderSubmittedEvent e) { /* set state */ }
}
```

### Issue 4: Pipeline Behavior Registration Order

**Problem:** Behaviors execute in registration order.

**Solution:** Use explicit ordering:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.UseMiddleware<LoggingMiddleware>();        // First
    dispatch.UseMiddleware<ValidationMiddleware>();     // Second
    dispatch.UseMiddleware<AuthorizationMiddleware>();  // Third
    dispatch.UseMiddleware<TransactionMiddleware>();    // Last
});
```

## Performance Comparison

Latest benchmark sources (20260420 epoch):
- `benchmarks/baselines/net10.0/dispatch-comparative-20260420/results/Excalibur.Dispatch.Benchmarks.Comparative.MediatRComparisonBenchmarks-report-github.md` (April 20, 2026)
- `benchmarks/baselines/net10.0/dispatch-comparative-20260420/results/Excalibur.Dispatch.Benchmarks.Comparative.MediatRWarmPathComparisonBenchmarks-report-github.md` (April 20, 2026)
- `benchmarks/baselines/net10.0/dispatch-comparative-20260420/results/Excalibur.Dispatch.Benchmarks.Comparative.RoutingFirstParityBenchmarks-report-github.md` (April 20, 2026)

Latest comparative validation run:
- Date: April 20, 2026
- BenchmarkDotNet 0.15.8 on .NET 10.0.6 / SDK 10.0.202
- Result: 16 reports captured (8 Comparative + 8 WarmPath), GREEN intra-report (Dispatch leads every competitor row), methodology divergence vs prior `20260302` baseline per BDN 0.15.4→0.15.8 shift
- Summaries: benchmark matrix summary + warm-path matrix summary (April 20, 2026)

| Scenario | MediatR | Excalibur | Relative Result |
|----------|---------|-------------------|-----------------|
| Single command handler | 40.59 ns | 78.24 ns | MediatR ~1.9x faster |
| Single command ultra-local API | 40.59 ns | 29.72 ns | **Dispatch ~1.4x faster** |
| Notification to 3 handlers | 88.71 ns | 127.07 ns | MediatR ~1.4x faster |
| Query with return value | 46.47 ns | 81.75 ns | MediatR ~1.8x faster |
| Query ultra-local API | 46.47 ns | 49.35 ns | MediatR ~1.1x faster |
| 10 concurrent commands | 497.09 ns | 921.98 ns | MediatR ~1.9x faster |
| 100 concurrent commands | 4,987.21 ns | 8,282.24 ns | MediatR ~1.7x faster |

Routing-first replacement path (Dispatch local + transport-ready branch selection):

| Dispatch routing-first scenario | Mean |
|---------------------------------|------|
| Pre-routed local command | 78.17 ns |
| Pre-routed local query | 93.86 ns |
| Pre-routed remote event (AWS SQS) | 157.17 ns |
| Pre-routed remote event (Azure Service Bus) | 167.66 ns |
| Pre-routed remote event (Kafka) | 163.22 ns |
| Pre-routed remote event (RabbitMQ) | 159.09 ns |

Dispatch supports both:

- standard `DispatchAsync(...)` path (full middleware/context semantics),
- ultra-local/direct-local path for local command/query hot spots.

See [Ultra-Local Dispatch](../performance/ultra-local-dispatch.md) for eligibility and configuration details.

**Conclusion:** MediatR remains faster for raw in-process mediator microbenchmarks. Dispatch adds transport-aware routing, richer middleware/context semantics, and event-sourcing/outbox integration in the same programming model.

## Migration Checklist

**Fast path — drop-in shim:**

- [ ] Add the `Excalibur.Dispatch.Compat.MediatR` package
- [ ] Apply the **EXMIG0003** code-fix to swap `using MediatR;` → `using Excalibur.Dispatch.Compat.MediatR;`
- [ ] Apply the **EXMIG0001** code-fix to rewrite `AddMediatR(...)` → `AddMediatRCompat(...)`
- [ ] Resolve **EXMIG0002** (unsupported constructs) and **EXMIG0004** (handler signature) diagnostics
- [ ] Remove the MediatR package reference; build and run the conformance/integration suite

**Idiomatic path — canonical rewrite:**

- [ ] Install Dispatch packages
- [ ] Add Dispatch registration alongside MediatR
- [ ] Migrate commands: `IRequest<T>` → `IDispatchAction<T>`
- [ ] Migrate handlers: `IRequestHandler` → `IActionHandler`
- [ ] Update method names: `Handle()` → `HandleAsync()`
- [ ] Migrate events: `INotification` → `IDomainEvent`
- [ ] Migrate event handlers: `INotificationHandler` → `IEventHandler`
- [ ] Migrate pipeline behaviors (non-generic interface)
- [ ] Update dependency injection: `IMediator` → `IDispatcher`
- [ ] Update usage: `Send()` → `DispatchAsync()`
- [ ] Add `CancellationToken` to all dispatch calls
- [ ] Update unit tests
- [ ] Update integration tests
- [ ] Remove MediatR package (once migration complete)

## Getting Help

- **Documentation**: [Dispatch Introduction](../intro.md)
- **GitHub Issues**: [Report Migration Issues](https://github.com/TrigintaFaces/Excalibur/issues)
- **Examples**: See [samples/](https://github.com/TrigintaFaces/Excalibur/tree/main/samples)

## Next Steps

1. Review [Handlers and Actions](../handlers.md)
2. Learn [Event Sourcing Patterns](/docs/event-sourcing)
3. Implement [Outbox Pattern](../patterns/outbox.md)
4. Explore [Domain Modeling](/docs/domain-modeling)
5. Set up [Monitoring and Observability](../observability/health-checks.md)

## See Also

- [Migration Overview](index.md) - All migration guides
- [From MassTransit](from-masstransit.md) - MassTransit migration guide
- [Getting Started](../getting-started/index.md) - New project setup from scratch

