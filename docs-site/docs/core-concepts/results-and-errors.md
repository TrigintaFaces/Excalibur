---
sidebar_position: 6
title: Results and Errors
description: Handle success and failure patterns in Dispatch using the MessageResult API
---

# Results and Errors

Dispatch provides a comprehensive result type system for handling operation outcomes without relying on exceptions for control flow. The `IMessageResult` and `IMessageResult<T>` interfaces enable clean error handling with full support for railway-oriented programming patterns.

:::info Why Should I Care?

Throwing exceptions for business validation ("order not found", "insufficient stock") is expensive and makes error paths invisible in your code. `IMessageResult` makes success and failure **first-class values** -- you can pattern-match, chain with `.Map()` and `.Match()`, and convert directly to HTTP responses with `.ToApiResult()`. Your handlers return results, not throw exceptions.
:::

## Before You Start

- **.NET 10.0**
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [actions and handlers](./actions-and-handlers.md)

## MessageResult API

### Creating Success Results

```csharp
// Simple success (no return value)
return MessageResult.Success();

// Success with a typed value
return MessageResult.Success(order);

// Success from cache hit
return MessageResult.SuccessFromCache();

// Success with additional context
return MessageResult.Success(
    value: order,
    routingDecision: null,
    validationResult: validationContext,
    authorizationResult: authResult,
    disposition: MessageDisposition.Handled);
```

### Creating Failed Results

```csharp
// Simple failure with error message
return MessageResult.Failed("Order not found");

// Failure with problem details
return MessageResult.Failed(new MessageProblemDetails
{
    Type = ProblemDetailsTypes.NotFound,  // "urn:dispatch:error:not-found"
    Title = "Resource Not Found",
    Status = 404,
    Detail = $"Order with ID {orderId} was not found"
});

// Typed failure
return MessageResult.Failed<Order>("Validation failed", problemDetails);

// Failure from an exception
return MessageResult.Failed(exception);

// Typed failure from an exception
return MessageResult.Failed<Order>(exception);

// Failure with validation and authorization context
return MessageResult.Failed(problemDetails, validationResult, authorizationResult);
```

### Creating Cancelled Results

Use `Cancelled()` when an operation is cancelled via a `CancellationToken` or business logic. The non-generic overload returns a cached singleton for hot-path performance.

```csharp
// Cancellation (returns cached singleton)
return MessageResult.Cancelled();

// Typed cancellation
return MessageResult.Cancelled<Order>();
```

## IMessageResult Interface

The base interface for all result types:

```csharp
public interface IMessageResult
{
    // Primary success indicator
    bool Succeeded { get; }


    // Error message when failed
    string? ErrorMessage { get; }

    // How the result was produced: Handled, ServedFromCache, or SuppressedAsDuplicate
    MessageDisposition Disposition { get; }

    // Validation context
    object? ValidationResult { get; }

    // Authorization context
    object? AuthorizationResult { get; }

    // Structured error information
    IMessageProblemDetails? ProblemDetails { get; }
}
```

## IMessageResult\<T\> Interface

Extends the base interface with a typed return value:

```csharp
public interface IMessageResult<out T> : IMessageResult
{
    // The return value (null if failed)
    T? ReturnValue { get; }
}
```

## Checking Results

```csharp
var result = await dispatcher.DispatchAsync(action, cancellationToken);

// Check success
if (result.Succeeded)
{
    // Handle success
}

// Alternative syntax
if (result.Succeeded)
{
    // Handle success
}

// Check how the result was produced. `Succeeded` says whether the operation failed; it does not say
// whether a handler ran. A cached result and a suppressed duplicate both succeed with no handler
// invocation, so code that records a message as processed must read the disposition, not just success.
switch (result.Disposition)
{
    case MessageDisposition.ServedFromCache:
        _logger.LogDebug("Result served from cache");
        break;
    case MessageDisposition.SuppressedAsDuplicate:
        _logger.LogDebug("Message already accepted for processing; no handler ran");
        break;
    case MessageDisposition.Handled:
    default:
        break;
}

// Access error information
if (!result.Succeeded)
{
    Console.WriteLine(result.ErrorMessage);
    Console.WriteLine(result.ProblemDetails?.Detail);
}
```

## Functional Composition

Dispatch supports railway-oriented programming patterns that enable cleaner, more expressive code when working with results. Instead of verbose null checking and conditional logic, you can chain operations functionally.

### Why Functional Composition?

Traditional result handling requires verbose conditional logic:

```csharp
// Verbose pattern - lots of null checks and early returns
public async Task<IActionResult> GetOrder(Guid orderId, CancellationToken ct)
{
    var action = new GetOrderAction(orderId);
    var result = await _dispatcher.DispatchAsync<GetOrderAction, Order>(action, ct);

    if (!result.Succeeded || result.ReturnValue is null)
    {
        return result.ProblemDetails?.Status switch
        {
            404 => NotFound(result.ProblemDetails),
            400 => BadRequest(result.ProblemDetails),
            _ => StatusCode(500, result.ProblemDetails)
        };
    }

    var order = result.ReturnValue;
    var dto = new OrderDto(order);
    _logger.LogInformation("Retrieved order {OrderId}", dto.Id);
    return Ok(dto);
}
```

Functional composition enables cleaner, more declarative code:

```csharp
// Functional pattern - chain operations cleanly
public async Task<IActionResult> GetOrder(Guid orderId, CancellationToken ct)
{
    var action = new GetOrderAction(orderId);

    return await _dispatcher
        .DispatchAsync<GetOrderAction, Order>(action, ct)
        .Map(order => new OrderDto(order))
        .Tap(dto => _logger.LogInformation("Retrieved order {OrderId}", dto.Id))
        .Match(
            onSuccess: dto => Ok(dto),
            onFailure: problem => problem?.Status switch
            {
                404 => NotFound(problem),
                400 => BadRequest(problem),
                _ => StatusCode(500, problem)
            });
}
```

The functional pattern:
- Eliminates null checks (handled automatically)
- Makes the happy path obvious (read top to bottom)
- Ensures errors propagate correctly (railway pattern)
- Reduces nesting and improves readability

### When to Use Each Pattern

| Method | Use When |
|--------|----------|
| **Map** | Transforming the success value (e.g., entity to DTO) |
| **Bind** | Chaining operations that might fail (e.g., validation, external calls) |
| **Match** | Converting the result to a different type (e.g., IActionResult) |
| **Tap** | Executing side effects (logging, metrics, notifications) |
| **GetValueOrDefault** | You need a fallback value when the result fails |
| **GetValueOrThrow** | You want to fail fast with an exception |

Full signatures and async overloads for each method are in the [Result API reference](../reference/result-api.md#result-extensions-railway-oriented-programming).

## Complete Example: Order Processing

```csharp
public class OrderController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<OrderController> _logger;

    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        CreateOrderRequest request,
        CancellationToken ct)
    {
        var action = new CreateOrderAction(request.CustomerId, request.Items);

        return await _dispatcher
            .DispatchAsync<CreateOrderAction, Order>(action, ct)
            .Tap(order => _logger.LogInformation("Order {Id} created", order.Id))
            .Map(order => new OrderResponse(order))
            .Match(
                onSuccess: response => CreatedAtAction(
                    nameof(GetOrder),
                    new { id = response.Id },
                    response),
                onFailure: problem => problem switch
                {
                    { Status: 400 } => BadRequest(problem),
                    { Status: 404 } => NotFound(problem),
                    { Status: 409 } => Conflict(problem),
                    _ => StatusCode(500, problem)
                });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken ct)
    {
        var action = new GetOrderAction(id);

        return await _dispatcher
            .DispatchAsync<GetOrderAction, Order>(action, ct)
            .Map(order => new OrderResponse(order))
            .Match(
                onSuccess: Ok,
                onFailure: problem => problem?.Status == 404
                    ? NotFound()
                    : Problem(problem?.Detail));
    }
}
```

For Minimal API endpoints, the terminal operators in `Excalibur.Dispatch.Hosting.AspNetCore` remove the explicit `Match` branch — see [Results to HTTP responses](../middleware/results-to-http.md).

## Handler Implementation

Return results from your handlers:

```csharp
public class CreateOrderHandler : IActionHandler<CreateOrderAction, Order>
{
    private readonly IOrderRepository _repository;
    private readonly IValidator<CreateOrderAction> _validator;

    // Note: Handlers return TResult directly (not IMessageResult<TResult>).
    // The framework wraps the return value in IMessageResult automatically.
    // Use validation middleware for pre-validation, or throw exceptions
    // that exception mapping middleware converts to proper results.
    public async Task<Order> HandleAsync(
        CreateOrderAction action,
        CancellationToken ct)
    {
        // Validate (or use validation middleware for automatic validation)
        var validation = await _validator.ValidateAsync(action, ct);
        if (!validation.IsValid)
        {
            throw new ValidationException(
                validation.Errors.ToDictionary(
                    e => e.PropertyName,
                    e => new[] { e.ErrorMessage }));
        }

        // Create order
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = action.CustomerId,
            Items = action.Items,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.SaveAsync(order, ct);
        return order;  // Framework wraps in IMessageResult<Order>
    }
}
```

## Problem Details

Use `IMessageProblemDetails` for RFC 7807 compliant error responses:

```csharp
public interface IMessageProblemDetails
{
    string Type { get; set; }      // URI identifying the problem type
    string Title { get; set; }     // Short human-readable summary
    int? Status { get; set; }      // HTTP status code (RFC 7807)
    int ErrorCode { get; set; }    // Application-specific error code
    string Detail { get; set; }    // Human-readable explanation
    string Instance { get; set; }  // URI identifying the specific occurrence
    IDictionary<string, object?> Extensions { get; }  // Additional extension fields
}
```

### Creating Problem Details

```csharp ignore
using Excalibur.Dispatch;

var problemDetails = new MessageProblemDetails
{
    Type = ProblemDetailsTypes.Validation,  // "urn:dispatch:error:validation"
    Title = "Insufficient Funds",
    Status = 402,
    Detail = $"Account {accountId} has insufficient funds. Required: {required}, Available: {available}",
    Instance = $"/orders/{orderId}"
};

return MessageResult.Failed<PaymentResult>("Payment failed", problemDetails);
```

Use the `ProblemDetailsTypes` constants rather than inline strings. The full set, and the convention for adding your own, is in the [Result API reference](../reference/result-api.md#standard-problem-details-type-uris).

## Best Practices

### Prefer Results Over Exceptions

```csharp
// Good: Return result for expected failures
public async Task<IMessageResult<Order>> GetOrderAsync(Guid id, CancellationToken ct)
{
    var order = await _repository.FindByIdAsync(id, ct);
    if (order is null)
        return MessageResult.Failed<Order>("Order not found");

    return MessageResult.Success(order);
}

// Avoid: Throwing exceptions for expected cases
public async Task<Order> GetOrderAsync(Guid id, CancellationToken ct)
{
    var order = await _repository.FindByIdAsync(id, ct);
    if (order is null)
        throw new ResourceNotFoundException($"Order {id} not found"); // Don't do this

    return order;
}
```

### Use Functional Composition

```csharp
// Good: Chain operations functionally
return await GetOrderAsync(id, ct)
    .Bind(ValidateOrderAsync)
    .Bind(ProcessPaymentAsync)
    .Map(CreateConfirmation);

// Avoid: Verbose null checking
var orderResult = await GetOrderAsync(id, ct);
if (!orderResult.Succeeded) return orderResult.Failed();

var validationResult = await ValidateOrderAsync(orderResult.ReturnValue);
if (!validationResult.Succeeded) return validationResult.Failed();

var paymentResult = await ProcessPaymentAsync(validationResult.ReturnValue);
// ...
```

### Include Context in Problem Details

```csharp
// Good: Detailed, actionable error information
return MessageResult.Failed<T>(
    "Validation failed",
    new MessageProblemDetails
    {
        Type = ProblemDetailsTypes.Validation,  // "urn:dispatch:error:validation"
        Title = "Validation Error",
        Status = 400,
        Detail = "The 'email' field must be a valid email address",
        Instance = $"/users/{userId}"
    });

// Avoid: Vague error messages
return MessageResult.Failed<T>("Invalid input");
```

## When to Use Exceptions vs Results

| Scenario | Approach |
|----------|----------|
| Expected business failure (e.g., insufficient funds) | Return `MessageResult.Failed` |
| Unexpected infrastructure failure | Throw exception |
| Resource not found in handler | Either works - choose consistently |
| Validation errors | `ValidationException` or `MessageResult.Failed` with validation details |
| External service timeout | `OperationTimeoutException` |
| Concurrency conflict | `ConcurrencyException` |

The exception types named here, and the full hierarchy they belong to, are in the [Result API reference](../reference/result-api.md#exception-hierarchy). To convert them to Problem Details centrally instead of per handler, see [Exception Mapping](../middleware/exception-mapping.md).

## Error Handling Decision Tree

Use this to pick the right error strategy for your scenario:

```
Is the error a business rule violation (validation, not found, insufficient funds)?
├── YES → Return MessageResult.Failed(problemDetails)
│         Don't throw exceptions for expected business outcomes.
│
└── NO → Is it a transient infrastructure failure (timeout, connection reset)?
    ├── YES → Let resilience middleware handle it
    │         Add: dispatch.UseResilience()
    │         The middleware retries automatically with backoff.
    │
    └── NO → Is it a permanent infrastructure failure (bad config, missing table)?
        ├── YES → Throw an exception
        │         Let it propagate to the caller. Log it. Fix the root cause.
        │
        └── NO → Is it a message that cannot be processed after retries?
            └── YES → Use the dead-letter pattern
                      Messages move to a DLQ after max retries.
                      See: Dead Letter Pattern docs
```

| Scenario | Strategy | Code Pattern |
|----------|----------|-------------|
| Validation failure | `MessageResult.Failed(problem)` | Return result, don't throw |
| Entity not found | `MessageResult.Failed("Not found")` | Return result |
| Transient failure | Resilience middleware | `dispatch.UseResilience()` |
| Permanent failure | Exception | `throw new InvalidOperationException(...)` |
| Poison message | Dead letter queue | Transport DLQ config |
| Concurrency conflict | Reload + retry | Catch `ConcurrencyException`, reload aggregate |

## Next Steps

- [Results to HTTP Responses](../middleware/results-to-http.md) - Convert a result to an `IResult` in a Minimal API endpoint
- [Exception Mapping](../middleware/exception-mapping.md) - Map thrown exceptions to Problem Details centrally
- [Result API Reference](../reference/result-api.md) - Signatures for every ROP extension, the exception hierarchy, and the type URIs

## What's Next

- [Configuration](configuration.md) - Configure Dispatch options and services
- [Dependency Injection](dependency-injection.md) - Service registration and lifetimes
- [Pipeline](../pipeline/index.md) - Middleware for cross-cutting concerns
- [Patterns](../patterns/index.md) - Outbox, inbox, and dead-letter patterns

## See Also

- [Actions and Handlers](./actions-and-handlers.md) — Define actions and handlers that produce and consume results
- [Validation Middleware](../middleware/validation.md) — Automatic action validation that produces structured error results
- [Error Handling Patterns](../patterns/error-handling.md) — Dead-letter queues, retries, and resilient error handling strategies
- [Dead Letter Pattern](../patterns/dead-letter.md) — Handle permanently failed messages with dead-letter queues
