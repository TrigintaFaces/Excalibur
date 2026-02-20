---
sidebar_position: 1
---

# Migrating from MediatR

A comprehensive guide for migrating from MediatR to Excalibur.Dispatch, covering API differences, feature mapping, and migration strategies.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- An existing application using MediatR
- Familiarity with [getting started](../getting-started/index.md) and [actions and handlers](../core-concepts/actions-and-handlers.md)

## Overview

Excalibur is designed as a **production-ready alternative to MediatR** with enhanced features for event sourcing, domain-driven design, and reliable messaging. This guide helps you migrate smoothly while gaining new capabilities.

## Key Differences

| Feature | MediatR | Excalibur |
|---------|---------|-------------------|
| **Core Focus** | Simple mediator pattern | Messaging + Event Sourcing + DDD |
| **Request/Response** | `IRequest<T>` / `IRequestHandler<T>` | `IDispatchAction<T>` / `IActionHandler<T, R>` |
| **Notifications** | `INotification` / `INotificationHandler` | `IDomainEvent` / `IEventHandler` |
| **Pipeline Behaviors** | `IPipelineBehavior<TRequest, TResponse>` | `IDispatchMiddleware` |
| **Event Sourcing** | Not included | Built-in with `IEventStore`, `AggregateRoot` |
| **Outbox Pattern** | Not included | Built-in with `IOutboxStore`, `IOutboxProcessor` |
| **Metadata** | Limited | Rich metadata with `IMessageContext` |
| **Async Only** | Supports both sync/async | Async-only (modern best practice) |

## Side-by-Side Comparison

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
dotnet add package Excalibur.Dispatch.Abstractions
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
builder.Services.AddExcaliburOutbox(outbox =>
{
    outbox.UseSqlServer(connectionString)
          .WithProcessing(p => p.PollingInterval(TimeSpan.FromSeconds(5)));
});

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

Latest benchmark sources:
- `BenchmarkDotNet.Artifacts.FullRefresh-20260219/results/Excalibur.Dispatch.Benchmarks.Comparative.MediatRComparisonBenchmarks-report-github.md` (February 19, 2026)
- `BenchmarkDotNet.Artifacts.FullRefresh-20260219/results/Excalibur.Dispatch.Benchmarks.Comparative.RoutingFirstParityBenchmarks-report-github.md` (February 19, 2026)

| Scenario | MediatR | Excalibur | Relative Result |
|----------|---------|-------------------|-----------------|
| Single command handler | 40.92 ns | 118.79 ns | MediatR ~2.9x faster |
| Single command ultra-local API | 40.92 ns | 47.12 ns | MediatR ~1.2x faster |
| Notification to 3 handlers | 96.10 ns | 154.47 ns | MediatR ~1.6x faster |
| Query with return value | 49.29 ns | 126.63 ns | MediatR ~2.6x faster |
| Query ultra-local API | 49.29 ns | 66.94 ns | MediatR ~1.4x faster |
| 10 concurrent commands | 497.81 ns | 1,244.58 ns | MediatR ~2.5x faster |
| 100 concurrent commands | 4,797.88 ns | 12,107.20 ns | MediatR ~2.5x faster |

Routing-first replacement path (Dispatch local + transport-ready branch selection):

| Dispatch routing-first scenario | Mean |
|---------------------------------|------|
| Pre-routed local command | 106.0 ns |
| Pre-routed local query | 141.3 ns |
| Pre-routed remote event (AWS SQS) | 183.3 ns |
| Pre-routed remote event (Azure Service Bus) | 191.8 ns |
| Pre-routed remote event (Kafka) | 189.1 ns |
| Pre-routed remote event (RabbitMQ) | 184.1 ns |

Dispatch supports both:

- standard `DispatchAsync(...)` path (full middleware/context semantics),
- ultra-local/direct-local path for local command/query hot spots.

See [Ultra-Local Dispatch](../performance/ultra-local-dispatch.md) for eligibility and configuration details.

**Conclusion:** MediatR remains faster for raw in-process mediator microbenchmarks. Dispatch adds transport-aware routing, richer middleware/context semantics, and event-sourcing/outbox integration in the same programming model.

## Migration Checklist

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

