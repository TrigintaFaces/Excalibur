---
sidebar_position: 1
title: Actions and Handlers
description: Learn how to define actions and implement handlers in Excalibur.Dispatch.
---

# Actions and Handlers

Actions represent work to be done, and handlers contain the business logic to process them. This is the core pattern of Excalibur.Dispatch.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Dispatch.Abstractions
  ```
- Familiarity with [dependency injection](./dependency-injection.md) and [getting started](../getting-started/index.md)

## Actions

An action is a simple data class that implements either `IDispatchAction` or `IDispatchAction<TResult>`.

### Actions Without Return Value

Use `IDispatchAction` for commands that don't return data:

```csharp
using Excalibur.Dispatch.Abstractions;

public record CreateOrderAction(
    string CustomerId,
    List<string> Items,
    string? Notes = null) : IDispatchAction;

public record CancelOrderAction(Guid OrderId, string Reason) : IDispatchAction;

public record UpdateInventoryAction(string Sku, int Quantity) : IDispatchAction;
```

### Actions With Return Value

Use `IDispatchAction<TResult>` for queries or commands that return data:

```csharp
public record GetOrderAction(Guid OrderId) : IDispatchAction<Order>;

public record GetOrdersAction(string CustomerId) : IDispatchAction<IReadOnlyList<Order>>;

public record CreateOrderWithIdAction(
    string CustomerId,
    List<string> Items) : IDispatchAction<Guid>; // Returns the created order ID
```

### Action Best Practices

1. **Use records** - Immutability and equality come free
2. **Keep actions small** - Only include data needed for the operation
3. **Use meaningful names** - End with `Action` for clarity
4. **Validate in handlers** - Actions are just data containers

```csharp
// Good: Focused, immutable, clear name
public record ProcessPaymentAction(
    Guid OrderId,
    decimal Amount,
    string Currency) : IDispatchAction<PaymentResult>;

// Avoid: Too much logic in the action itself
public record ProcessPaymentAction : IDispatchAction
{
    public Guid OrderId { get; set; } // Mutable - avoid
    public void Validate() { } // Logic in action - avoid
}
```

## Handlers

Handlers contain the business logic that processes actions.

### Action Handler Without Return

```csharp
using Excalibur.Dispatch.Abstractions.Delivery;

public class CreateOrderHandler : IActionHandler<CreateOrderAction>
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(
        IOrderRepository repository,
        ILogger<CreateOrderHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task HandleAsync(
        CreateOrderAction action,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating order for customer {CustomerId}", action.CustomerId);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = action.CustomerId,
            Items = action.Items,
            Notes = action.Notes,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _repository.SaveAsync(order, cancellationToken);

        _logger.LogInformation("Order {OrderId} created", order.Id);
    }
}
```

### Action Handler With Return

```csharp
public class GetOrderHandler : IActionHandler<GetOrderAction, Order>
{
    private readonly IOrderRepository _repository;

    public GetOrderHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<Order> HandleAsync(
        GetOrderAction action,
        CancellationToken cancellationToken)
    {
        var order = await _repository.GetByIdAsync(action.OrderId, cancellationToken);

        if (order is null)
        {
            throw new OrderNotFoundException(action.OrderId);
        }

        return order;
    }
}
```

### Handler Registration

Handlers are discovered and registered automatically:

```csharp
// Register all handlers from an assembly (recommended)
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Or register handlers from multiple assemblies
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(CreateOrderHandler).Assembly);
    dispatch.AddHandlersFromAssembly(typeof(GetOrderHandler).Assembly);
});
```

### Handler Lifetime

By default, handlers are registered as **scoped** services. This means:

- A new instance is created per HTTP request (in ASP.NET Core)
- Handlers can safely depend on scoped services like `DbContext`
- State is not shared between requests

To change the lifetime:

```csharp
// Singleton handler (must be thread-safe)
services.AddSingleton<IActionHandler<GetConfigAction, Config>, GetConfigHandler>();

// Transient handler (new instance per resolution)
services.AddTransient<IActionHandler<ProcessAction>, ProcessHandler>();
```

## Dispatching Actions

Inject `IDispatcher` to dispatch actions:

```csharp
public class OrderService
{
    private readonly IDispatcher _dispatcher;

    public OrderService(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task CreateOrderAsync(
        string customerId,
        List<string> items,
        CancellationToken cancellationToken)
    {
        var action = new CreateOrderAction(customerId, items);
        var result = await _dispatcher.DispatchAsync(action, cancellationToken);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(result.ErrorMessage);
        }
    }

    public async Task<Order> GetOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var action = new GetOrderAction(orderId);
        var result = await _dispatcher.DispatchAsync<GetOrderAction, Order>(
            action, cancellationToken);

        return result.ReturnValue;
    }
}
```

## Nested Dispatch (Child Context)

When dispatching from within a handler, use `DispatchChildAsync` to propagate context:

```csharp
public class CreateOrderHandler : IActionHandler<CreateOrderAction>
{
    private readonly IDispatcher _dispatcher;

    public async Task HandleAsync(CreateOrderAction action, CancellationToken ct)
    {
        // Create the order...

        // Dispatch a child action - context is propagated
        var notifyAction = new NotifyOrderCreatedAction(orderId);
        await _dispatcher.DispatchChildAsync(notifyAction, ct);
    }
}
```

This ensures:
- The same `CorrelationId` is used
- Distributed tracing spans are linked correctly
- Context items are available to child handlers

## Event Handlers

For handling domain events or integration events:

```csharp
public class OrderCreatedEventHandler : IEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(
        OrderCreatedEvent @event,
        CancellationToken cancellationToken)
    {
        // React to the event
    }
}
```

Multiple handlers can subscribe to the same event:

```csharp
// Both handlers will be invoked
public class SendEmailOnOrderCreated : IEventHandler<OrderCreatedEvent> { }
public class UpdateInventoryOnOrderCreated : IEventHandler<OrderCreatedEvent> { }
```

## Error Handling

Handlers can throw exceptions or return error results:

### Using Exceptions (Recommended)

```csharp
public class GetOrderHandler : IActionHandler<GetOrderAction, Order>
{
    public async Task<Order> HandleAsync(GetOrderAction action, CancellationToken ct)
    {
        var order = await _repository.GetByIdAsync(action.OrderId, ct);

        if (order is null)
        {
            throw new NotFoundException($"Order {action.OrderId} not found");
        }

        return order;
    }
}
```

The exception mapping middleware converts exceptions to `IMessageResult.Failed` with proper problem details. Configure mappings in your `AddDispatch` setup.

## Handler Interface Hierarchy

Dispatch provides two tiers of handler interfaces:

### Recommended: Specialized Handlers

For 90% of use cases, use the specialized handlers that return your business types directly:

| Interface | Returns | Framework Wraps To |
|-----------|---------|-------------------|
| `IActionHandler<TAction>` | `Task` | `IMessageResult` |
| `IActionHandler<TAction, TResult>` | `Task<TResult>` | `IMessageResult<TResult>` |
| `IEventHandler<TEvent>` | `Task` | `IMessageResult` |
| `IDocumentHandler<TDocument>` | `Task` | `IMessageResult` |

**Benefits:**
- Clean handler code with no framework abstractions
- Framework wraps results automatically
- Exceptions converted to failures by middleware

### Advanced: IDispatchHandler

For scenarios requiring direct control over `IMessageResult`, use `IDispatchHandler<TMessage>`:

```csharp
public record GetOrderAction(Guid OrderId) : IDispatchMessage;

public class GetOrderHandler : IDispatchHandler<GetOrderAction>
{
    private readonly IOrderRepository _repository;

    public GetOrderHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<IMessageResult> HandleAsync(
        GetOrderAction action,
        IMessageContext context,
        CancellationToken ct)
    {
        var order = await _repository.GetByIdAsync(action.OrderId, ct);

        if (order is null)
        {
            return MessageResult.Failed(
                MessageProblemDetails.NotFound(
                    $"Order with ID {action.OrderId} was not found"));
        }

        return MessageResult.Success(order);
    }
}
```

### When to Use IDispatchHandler

Use `IDispatchHandler` only when you need capabilities not available with specialized handlers:

| Capability | `IActionHandler` | `IDispatchHandler` |
|------------|------------------|-------------------|
| Return `MessageResult.SuccessFromCache()` | ❌ | ✅ |
| Set `CacheHit = true` on result | ❌ | ✅ |
| Set `ValidationResult` on success | ❌ | ✅ |
| Set `AuthorizationResult` on success | ❌ | ✅ |
| Return failure without throwing exception | ❌ | ✅ |
| Access `IMessageContext` in handler | ❌ | ✅ |
| Batch processing (`IBatchableHandler`) | ❌ | ✅ |

:::tip When in doubt, use specialized handlers
Start with `IActionHandler` or `IEventHandler`. Only switch to `IDispatchHandler` if you need the advanced capabilities above.
:::

## What's Next

- [Message Context](message-context.md) - Work with context metadata
- [Results and Errors](results-and-errors.md) - Handle success and failure
- [Handlers](../handlers.md) - Advanced handler patterns and registration
- [Pipeline](../pipeline/index.md) - Add behaviors around handlers

## See Also

- [Middleware](../middleware/index.md) — Add cross-cutting concerns like logging and exception mapping around handlers
- [Validation Middleware](../middleware/validation.md) — Automatically validate actions before they reach handlers
- [Dependency Injection](./dependency-injection.md) — Handler registration, lifetimes, and service injection patterns
- [Error Handling Patterns](../patterns/error-handling.md) — Dead-letter queues, retries, and resilient error handling strategies
