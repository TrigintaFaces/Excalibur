---
sidebar_position: 6
title: Results and Errors
description: Handle success and failure patterns in Dispatch using the MessageResult API
---

# Results and Errors

Dispatch provides a comprehensive result type system for handling operation outcomes without relying on exceptions for control flow. The `IMessageResult` and `IMessageResult<T>` interfaces enable clean error handling with full support for railway-oriented programming patterns.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
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
    validationResult: validationContext,
    authorizationResult: authResult,
    cacheHit: false);
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
```

## IMessageResult Interface

The base interface for all result types:

```csharp
public interface IMessageResult
{
    // Primary success indicator
    bool Succeeded { get; }

    // Alias for Succeeded
    bool IsSuccess => Succeeded;

    // Error message when failed
    string? ErrorMessage { get; }

    // Whether result was served from cache
    bool CacheHit { get; }

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
if (result.IsSuccess)
{
    // Handle success
}

// Check for cache hit
if (result.CacheHit)
{
    _logger.LogDebug("Result served from cache");
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

## Result Extensions (Railway-Oriented Programming)

Dispatch provides functional extensions for composing result operations cleanly. All extensions automatically short-circuit on failure, preserving the error information.

### Map - Transform Success Values

Transform the success value without affecting failures:

```csharp
// Sync transformation
var dto = result.Map(order => new OrderDto(order));

// Async transformation
var dto = await result.MapAsync(async order =>
{
    var details = await GetDetailsAsync(order);
    return new OrderDto(order, details);
});

// Map on async result
var dto = await resultTask.Map(order => new OrderDto(order));
```

### Bind - Chain Result Operations

Chain operations that return results:

```csharp
// Chain sync operations
var finalResult = getOrderResult.Bind(order =>
{
    if (order.Status == OrderStatus.Cancelled)
        return MessageResult.Failed<ShippingInfo>("Cannot ship cancelled order");

    return MessageResult.Success(GetShippingInfo(order));
});

// Chain async operations
var finalResult = await getOrderResult.BindAsync(async order =>
{
    var inventory = await CheckInventoryAsync(order);
    if (!inventory.Available)
        return MessageResult.Failed<ShipmentResult>("Insufficient inventory");

    return await ShipOrderAsync(order);
});
```

### Match - Pattern Matching

Execute different code paths based on success/failure:

```csharp
// Sync match
var response = result.Match(
    onSuccess: order => Ok(new OrderDto(order)),
    onFailure: problem => Problem(problem?.Detail ?? "Unknown error")
);

// Async match
var response = await resultTask.Match(
    onSuccess: order => Ok(new OrderDto(order)),
    onFailure: problem => Problem(problem?.Detail ?? "Unknown error")
);

// Async handlers
var response = await result.MatchAsync(
    onSuccess: async order => Ok(await EnrichOrderAsync(order)),
    onFailure: async problem => await LogAndReturnErrorAsync(problem)
);
```

### Tap - Side Effects

Execute side effects without modifying the result:

```csharp
// Sync tap (logging, metrics)
var result = await dispatcher.DispatchAsync(action, ct)
    .Tap(order => _logger.LogInformation("Order {Id} retrieved", order.Id));

// Async tap
var result = await dispatcher.DispatchAsync(action, ct)
    .TapAsync(async order => await SendNotificationAsync(order));
```

### GetValueOrDefault - Safe Value Access

Get the value or a default:

```csharp
// With default value
var order = result.GetValueOrDefault(Order.Empty);

// With null default
var order = result.GetValueOrDefault();
```

### GetValueOrThrow - Fail Fast

Get the value or throw an exception:

```csharp
// Throws InvalidOperationException if failed
var order = result.GetValueOrThrow();

// Async version
var order = await resultTask.GetValueOrThrow();
```

### Chaining Multiple Operations

Combine multiple extensions for complex workflows:

```csharp
// Multi-step order processing with railway pattern
public async Task<IMessageResult<OrderConfirmation>> ProcessOrderAsync(
    Guid orderId,
    CancellationToken ct)
{
    return await GetOrderAsync(orderId, ct)          // Get the order
        .Bind(ValidateOrderAsync)                     // Validate (may fail)
        .Bind(order => ReserveInventoryAsync(order, ct))  // Reserve (may fail)
        .BindAsync(async order =>                     // Process payment
        {
            var payment = await ProcessPaymentAsync(order, ct);
            return payment.Succeeded
                ? MessageResult.Success(order)
                : MessageResult.Failed<Order>("Payment failed", payment.ProblemDetails);
        })
        .Map(order => new OrderConfirmation(order))   // Transform to confirmation
        .Tap(confirmation => _logger.LogInformation(  // Log success
            "Order {OrderId} confirmed", confirmation.OrderId));
}
```

**Key principle:** Each `Bind` in the chain only executes if all previous operations succeeded. If any step fails, the failure propagates immediately to the end of the chain (short-circuit behavior).

### Extension Method Summary

| Method | Input | Output | Behavior |
|--------|-------|--------|----------|
| `Map<TIn,TOut>` | `IMessageResult<TIn>` | `IMessageResult<TOut>` | Transform success value |
| `MapAsync<TIn,TOut>` | `IMessageResult<TIn>` | `Task<IMessageResult<TOut>>` | Async transform |
| `Bind<TIn,TOut>` | `IMessageResult<TIn>` | `IMessageResult<TOut>` | Chain result operations |
| `BindAsync<TIn,TOut>` | `IMessageResult<TIn>` | `Task<IMessageResult<TOut>>` | Async chain |
| `Match<TIn,TOut>` | `IMessageResult<TIn>` | `TOut` | Branch on success/failure |
| `MatchAsync<TIn,TOut>` | `IMessageResult<TIn>` | `Task<TOut>` | Async branch |
| `Tap<T>` | `IMessageResult<T>` | `IMessageResult<T>` | Side effect (unchanged) |
| `TapAsync<T>` | `IMessageResult<T>` | `Task<IMessageResult<T>>` | Async side effect |
| `GetValueOrDefault<T>` | `IMessageResult<T>` | `T?` | Value or default |
| `GetValueOrThrow<T>` | `IMessageResult<T>` | `T` | Value or throw |

All methods also have overloads that work on `Task<IMessageResult<T>>` for seamless async chaining.

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
    int ErrorCode { get; set; }    // Application-specific error code
    string Detail { get; set; }    // Human-readable explanation
    string Instance { get; set; }  // URI identifying the specific occurrence
    IDictionary<string, object?> Extensions { get; }  // Additional extension fields
}
```

### Creating Problem Details

```csharp
using Excalibur.Dispatch.Abstractions;

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

### Standard Problem Details Type URIs

Dispatch provides standardized Type URIs via the `ProblemDetailsTypes` class. These URIs follow RFC 9457 guidelines using URN format instead of URLs.

**Why URNs instead of URLs?**

- URNs are explicitly non-resolvable identifiers (no 404 errors when clients try to access them)
- Self-documenting format with clear namespace hierarchy
- Consistent with RFC 9457 recommendation that Type URIs don't need to resolve

All Type URIs use the format: `urn:dispatch:error:{type}` with lowercase kebab-case suffixes.

```csharp
using Excalibur.Dispatch.Abstractions;

// Use constants instead of inline strings
var problemDetails = new MessageProblemDetails
{
    Type = ProblemDetailsTypes.NotFound,  // "urn:dispatch:error:not-found"
    Title = "Resource Not Found",
    Status = 404,
    Detail = $"Order {orderId} was not found"
};
```

#### Available Type Constants

| Constant | URN Value | Description |
|----------|-----------|-------------|
| `ProblemDetailsTypes.Validation` | `urn:dispatch:error:validation` | Request data failed validation rules |
| `ProblemDetailsTypes.NotFound` | `urn:dispatch:error:not-found` | Requested resource does not exist |
| `ProblemDetailsTypes.Conflict` | `urn:dispatch:error:conflict` | Request conflicts with current state |
| `ProblemDetailsTypes.Forbidden` | `urn:dispatch:error:forbidden` | Authenticated but not authorized |
| `ProblemDetailsTypes.Unauthorized` | `urn:dispatch:error:unauthorized` | Authentication required but missing/invalid |
| `ProblemDetailsTypes.Timeout` | `urn:dispatch:error:timeout` | Operation exceeded time limit |
| `ProblemDetailsTypes.RateLimited` | `urn:dispatch:error:rate-limited` | Caller exceeded rate limits |
| `ProblemDetailsTypes.Internal` | `urn:dispatch:error:internal` | Unexpected server-side error |
| `ProblemDetailsTypes.Routing` | `urn:dispatch:error:routing` | Message could not be routed to handler |
| `ProblemDetailsTypes.Transport` | `urn:dispatch:error:transport` | Message transport/delivery failed |
| `ProblemDetailsTypes.Serialization` | `urn:dispatch:error:serialization` | Serialization/deserialization failed |
| `ProblemDetailsTypes.Concurrency` | `urn:dispatch:error:concurrency` | Optimistic concurrency check failed |
| `ProblemDetailsTypes.HandlerNotFound` | `urn:dispatch:error:handler-not-found` | No handler registered for message type |
| `ProblemDetailsTypes.HandlerError` | `urn:dispatch:error:handler-error` | Message handler threw an exception |
| `ProblemDetailsTypes.MappingFailed` | `urn:dispatch:error:mapping-failed` | Exception mapping to problem details failed |
| `ProblemDetailsTypes.BackgroundExecution` | `urn:dispatch:error:background-execution` | Background task execution failed |

#### Extending with Custom Types

For application-specific error types, follow the same URN pattern:

```csharp
public static class AppProblemDetailsTypes
{
    private const string Prefix = "urn:myapp:error:";

    public const string InsufficientFunds = Prefix + "insufficient-funds";
    public const string OrderExpired = Prefix + "order-expired";
    public const string InventoryUnavailable = Prefix + "inventory-unavailable";
}
```

> **RFC 9457 Reference:** For more details on Problem Details for HTTP APIs, see [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457.html).

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
        throw new NotFoundException($"Order {id} not found"); // Don't do this

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

## Exception Hierarchy

Dispatch provides a unified exception hierarchy for handling exceptional conditions. All framework exceptions support RFC 7807 problem details conversion via `ToProblemDetails()`.

### Exception Class Diagram

```
Exception
└── ApiException (Excalibur.Dispatch.Abstractions) — simple base with ToProblemDetails()
    └── DispatchException (Dispatch) — rich features: ErrorCode, Category, Severity
        ├── ResourceException — base for resource errors
        │   ├── ResourceNotFoundException — 404 Not Found
        │   ├── ConflictException — 409 Conflict
        │   │   └── ConcurrencyException — optimistic locking failures
        │   └── ForbiddenException — 403 Forbidden
        ├── ValidationException — 400 Bad Request with field errors
        └── OperationTimeoutException — 408 Request Timeout
```

### ApiException - Base Class

The simplest exception with RFC 7807 support:

```csharp
using Excalibur.Dispatch.Abstractions;

// Throw a simple API exception
throw new ApiException(404, "Resource not found", null);

// Convert to problem details
catch (ApiException ex)
{
    var problemDetails = ex.ToProblemDetails();
    // Returns: Type, Title, Status, Detail, Instance, ErrorCode, Extensions
}
```

### DispatchException - Rich Features

Extends `ApiException` with error categorization, severity, tracing, and fluent builders:

```csharp
using Excalibur.Dispatch.Exceptions;

// Create with fluent configuration
throw new DispatchException("ORDER_FAILED", "Failed to process order")
    .WithContext("orderId", orderId)
    .WithContext("customerId", customerId)
    .WithCorrelationId(correlationId)
    .WithUserMessage("Your order could not be processed. Please try again.")
    .WithSuggestedAction("Contact support if the problem persists.");
```

### Specialized Exceptions

#### ResourceNotFoundException (404)

```csharp
using Excalibur.Dispatch.Exceptions;

// Simple usage
throw new ResourceNotFoundException("Order", orderId.ToString());

// Using factory method
throw ResourceNotFoundException.ForEntity<Order>(orderId);

// Output: "The requested Order with ID '123' was not found."
```

#### ValidationException (400)

```csharp
using Excalibur.Dispatch.Exceptions;

// From validation errors dictionary
var errors = new Dictionary<string, string[]>
{
    ["Email"] = new[] { "Email is required" },
    ["Age"] = new[] { "Age must be at least 18" }
};
throw new ValidationException(errors);

// Using factory methods
throw ValidationException.RequiredField("Email");
throw ValidationException.InvalidFormat("Phone", "XXX-XXX-XXXX");
throw ValidationException.OutOfRange("Age", 18, 120);

// Fluent error building
throw new ValidationException("Validation failed")
    .AddError("Email", "Invalid email format")
    .AddError("Name", "Name is required");
```

#### ConflictException (409)

```csharp
using Excalibur.Dispatch.Exceptions;

// Resource conflict
throw new ConflictException("Order", "duplicate-order",
    "An order with this ID already exists");
```

#### ConcurrencyException (409)

```csharp
using Excalibur.Dispatch.Exceptions;

// Optimistic locking failure
throw new ConcurrencyException("Order",
    expectedVersion: 5,
    actualVersion: 7,
    "The order was modified by another user");
```

#### ForbiddenException (403)

```csharp
using Excalibur.Dispatch.Exceptions;

// Access denied
throw new ForbiddenException("Order", "delete",
    "You are not authorized to delete this order");
```

#### OperationTimeoutException (408)

```csharp
using Excalibur.Dispatch.Exceptions;

// Timeout occurred
throw new OperationTimeoutException("PaymentProcessing",
    TimeSpan.FromSeconds(30),
    "Payment processing timed out");
```

### ToProblemDetails() Conversion

All exceptions in the hierarchy support RFC 7807 problem details:

```csharp
try
{
    await ProcessOrderAsync(orderId, ct);
}
catch (ApiException ex)
{
    // Works for ApiException and all derived types
    var problemDetails = ex.ToProblemDetails();

    return new ObjectResult(problemDetails)
    {
        StatusCode = problemDetails.Status
    };
}
```

**DispatchException produces richer output:**

```csharp
catch (DispatchException ex)
{
    var problemDetails = ex.ToDispatchProblemDetails();
    // Includes: ErrorCode, Category, Severity, CorrelationId, TraceId,
    //           SpanId, Timestamp, SuggestedAction, Context extensions
}
```

### Converting Handler Results from Exceptions

Use exceptions when the condition is truly exceptional, and convert to results at the boundary:

```csharp
public async Task<IMessageResult<Order>> GetOrderAsync(Guid id, CancellationToken ct)
{
    try
    {
        var order = await _repository.FindByIdAsync(id, ct)
            ?? throw new ResourceNotFoundException("Order", id.ToString());

        return MessageResult.Success(order);
    }
    catch (ApiException ex)
    {
        return MessageResult.Failed<Order>(ex.Message, ex.ToProblemDetails());
    }
}
```

### When to Use Exceptions vs Results

| Scenario | Approach |
|----------|----------|
| Expected business failure (e.g., insufficient funds) | Return `MessageResult.Failed` |
| Unexpected infrastructure failure | Throw exception |
| Resource not found in handler | Either works - choose consistently |
| Validation errors | `ValidationException` or `MessageResult.Failed` with validation details |
| External service timeout | `OperationTimeoutException` |
| Concurrency conflict | `ConcurrencyException` |

## Exception Mapping

Dispatch provides centralized exception-to-HTTP mapping that automatically converts exceptions to RFC 7807 Problem Details responses. This eliminates boilerplate try-catch blocks in handlers and ensures consistent error formatting.

### Configuring Exception Mapping

Configure exception mapping using the `ConfigureExceptionMapping` extension on the dispatch builder:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.ConfigureExceptionMapping(mapping =>
    {
        // ApiException hierarchy auto-mapped via ToProblemDetails() (default)
        mapping.UseApiExceptionMapping();

        // Custom mappings for third-party exceptions
        mapping.Map<DbException>(ex => new MessageProblemDetails
        {
            Type = "urn:dispatch:error:database",
            Title = "Database Error",
            Status = 500,
            Detail = ex.Message
        });

        // Conditional mapping based on exception properties
        mapping.MapWhen<HttpRequestException>(
            ex => ex.StatusCode == HttpStatusCode.NotFound,
            ex => MessageProblemDetails.NotFound("External resource not found"));

        // Default fallback for unmapped exceptions
        mapping.MapDefault(ex => MessageProblemDetails.InternalError(
            "An unexpected error occurred."));
    });
});
```

### Quick Setup with Defaults

For most applications, the default configuration is sufficient:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseExceptionMapping();  // Enables defaults
});
```

This enables:
- Automatic mapping of `ApiException` hierarchy using `ToProblemDetails()`
- Default mapper returns 500 Internal Server Error for unknown exceptions

### Builder API Reference

| Method | Description |
|--------|-------------|
| `UseApiExceptionMapping()` | Enables auto-mapping of `ApiException` and derived types (enabled by default) |
| `Map<TException>(mapper)` | Registers a synchronous mapping for a specific exception type |
| `MapAsync<TException>(mapper)` | Registers an async mapping when conversion requires I/O |
| `MapWhen<TException>(predicate, mapper)` | Registers a conditional mapping based on exception properties |
| `MapDefault(mapper)` | Sets the fallback mapper for unhandled exceptions |

### Evaluation Order

Exception mappings are evaluated in this order:

1. **Type-specific mappings** - First match wins, evaluated in registration order
2. **ApiException auto-mapping** - If enabled and exception inherits from `ApiException`
3. **Default mapper** - Catches all remaining exceptions

```csharp
dispatch.ConfigureExceptionMapping(mapping =>
{
    // Order matters! More specific mappings should come first
    mapping.MapWhen<DbException>(
        ex => ex.Number == 2627,  // Unique constraint violation
        ex => new MessageProblemDetails { Status = 409, ... });

    mapping.Map<DbException>(ex => ...);  // General database errors

    mapping.UseApiExceptionMapping();      // ApiException hierarchy

    mapping.MapDefault(ex => ...);         // Everything else
});
```

### Adding to the Pipeline

The exception mapping middleware catches exceptions and converts them to `IMessageResult.Failure`:

```csharp
dispatch.AddPipeline("default", pipeline =>
{
    pipeline.UseTracing();           // First: set up tracing context
    pipeline.UseExceptionMapping();  // Second: catch exceptions early
    pipeline.UseValidation();        // Validation before processing
    pipeline.UseAuthorization();     // Authorization checks
});
```

The middleware:
- Runs at `DispatchMiddlewareStage.ErrorHandling`
- **Never maps** `OperationCanceledException` (always rethrown for proper cancellation)
- Logs mapped exceptions at Warning level with status code and type
- Falls back gracefully if mapping itself fails

### Async Mappings

Use async mappings when exception conversion requires I/O operations:

```csharp
mapping.MapAsync<CustomException>(async (ex, ct) =>
{
    // Look up error details from database or external service
    var errorInfo = await _errorService.GetErrorInfoAsync(ex.ErrorCode, ct);

    return new MessageProblemDetails
    {
        Type = errorInfo.Type,
        Title = errorInfo.Title,
        Status = errorInfo.HttpStatus,
        Detail = errorInfo.UserMessage
    };
});
```

### Common Mapping Patterns

#### Database Exceptions

```csharp
mapping.Map<SqlException>(ex => ex.Number switch
{
    2627 => new MessageProblemDetails  // Unique constraint
    {
        Type = "urn:dispatch:error:duplicate",
        Title = "Duplicate Entry",
        Status = 409,
        Detail = "A record with this key already exists."
    },
    547 => new MessageProblemDetails   // Foreign key constraint
    {
        Type = "urn:dispatch:error:constraint",
        Title = "Reference Constraint Violation",
        Status = 400,
        Detail = "The referenced record does not exist."
    },
    _ => new MessageProblemDetails
    {
        Type = "urn:dispatch:error:database",
        Title = "Database Error",
        Status = 500,
        Detail = "A database error occurred."
    }
});
```

#### External HTTP Calls

```csharp
mapping.Map<HttpRequestException>(ex =>
{
    var status = ex.StatusCode switch
    {
        HttpStatusCode.NotFound => 404,
        HttpStatusCode.Unauthorized => 502,  // External auth failure
        HttpStatusCode.ServiceUnavailable => 503,
        _ => 502  // Bad Gateway for other external errors
    };

    return new MessageProblemDetails
    {
        Type = "urn:dispatch:error:external-service",
        Title = "External Service Error",
        Status = status,
        Detail = "An external service request failed."
    };
});
```

#### Timeout Handling

```csharp
mapping.Map<TimeoutException>(ex => new MessageProblemDetails
{
    Type = "urn:dispatch:error:timeout",
    Title = "Request Timeout",
    Status = 408,
    Detail = "The operation timed out. Please try again."
});

mapping.Map<TaskCanceledException>(ex => new MessageProblemDetails
{
    Type = "urn:dispatch:error:timeout",
    Title = "Request Timeout",
    Status = 408,
    Detail = "The request was cancelled due to timeout."
});
```

### Migration from Manual Exception Handling

**Before (manual handling in each handler):**

```csharp
// Using IDispatchHandler which returns IMessageResult directly
public class GetOrderHandler : IDispatchHandler<GetOrderAction>
{
    public async Task<IMessageResult> HandleAsync(
        GetOrderAction action, IMessageContext context, CancellationToken ct)
    {
        try
        {
            var order = await _repository.GetByIdAsync(action.OrderId, ct);
            return MessageResult.Success(order);
        }
        catch (DbException ex)
        {
            return MessageResult.Failed(
                ex.Message,
                new MessageProblemDetails { Status = 500, Title = "Database Error" });
        }
    }
}
```

**After (centralized exception mapping):**

```csharp
// Using IActionHandler - simpler, returns TResult directly
public class GetOrderHandler : IActionHandler<GetOrderAction, Order>
{
    public async Task<Order> HandleAsync(
        GetOrderAction action, CancellationToken ct)
    {
        // Just return the result - framework wraps it in IMessageResult
        // DbException automatically mapped to problem details by middleware
        return await _repository.GetByIdAsync(action.OrderId, ct);
    }
}
```

### IExceptionMapper Service

The `IExceptionMapper` service can be injected directly for advanced scenarios:

```csharp
public class ErrorController : ControllerBase
{
    private readonly IExceptionMapper _mapper;

    [HttpGet("convert")]
    public IActionResult ConvertException(Exception ex)
    {
        if (_mapper.CanMap(ex))
        {
            var problemDetails = _mapper.Map(ex);
            return StatusCode(problemDetails.Status ?? 500, problemDetails);
        }
        return StatusCode(500);
    }
}
```

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
