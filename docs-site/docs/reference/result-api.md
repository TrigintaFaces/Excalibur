---
sidebar_position: 2
title: Result API Reference
description: Signatures for the railway-oriented result extensions, the exception hierarchy, and Problem Details type URIs
---

# Result API Reference

Signatures and lookup tables for the result extension methods, the framework exception hierarchy, and the standard Problem Details type URIs. For what these are for and when to reach for each, see [Results and Errors](../core-concepts/results-and-errors.md).

## Before You Start

- **.NET 10.0**
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- The resource exception types (`ResourceException`, `ResourceNotFoundException`, `ConflictException`, `ConcurrencyException`) come from `Excalibur.Data` and its abstractions package

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

## Exception Hierarchy

Dispatch provides a unified exception hierarchy for handling exceptional conditions. All framework exceptions support RFC 7807 problem details conversion via `ToProblemDetails()`.

### Exception Class Diagram

```
Exception
└── ApiException (Excalibur.Dispatch.Abstractions) — simple base with ToProblemDetails()
    ├── ResourceException (Excalibur.Data.Abstractions) — base for resource/persistence errors
    │   ├── ResourceNotFoundException — 404 Not Found
    │   └── ConflictException — 409 Conflict
    │       └── ConcurrencyException — optimistic locking failures
    └── DispatchException (Dispatch) — rich features: ErrorCode, Category, Severity
        ├── ForbiddenException — 403 Forbidden (security/authorization)
        ├── ValidationException — 400 Bad Request with field errors
        └── OperationTimeoutException — 408 Request Timeout
```

### ApiException - Base Class

The simplest exception with RFC 7807 support:

```csharp
using Excalibur.Dispatch;

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
using Excalibur.Data;

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
using Excalibur.Data;

// Resource conflict
throw new ConflictException("Order", "duplicate-order",
    "An order with this ID already exists");
```

#### ConcurrencyException (409)

```csharp
using Excalibur.Data;

// Optimistic locking failure with numeric versions
throw new ConcurrencyException("Order", orderId.ToString(),
    expectedVersion: 5, actualVersion: 7);

// Using factory method for aggregates
throw ConcurrencyException.ForAggregate<Order>(orderId,
    expectedVersion: 5, actualVersion: 7);
```

#### ForbiddenException (403)

```csharp
using Excalibur.Dispatch.Exceptions;

// Access denied
throw new ForbiddenException("Order", "Delete");

// With required permission
throw new ForbiddenException("Order", "Delete", "Orders.Delete");

// Using factory method
throw ForbiddenException.MissingRole("Order", "Delete", "Admin");
```

#### OperationTimeoutException (408)

```csharp
using Excalibur.Dispatch.Exceptions;

// Timeout occurred
throw new OperationTimeoutException("PaymentProcessing",
    TimeSpan.FromSeconds(30),
    "Payment processing timed out");
```

## Standard Problem Details Type URIs

Dispatch provides standardized Type URIs via the `ProblemDetailsTypes` class. These URIs follow RFC 9457 guidelines using URN format instead of URLs.

**Why URNs instead of URLs?**

- URNs are explicitly non-resolvable identifiers (no 404 errors when clients try to access them)
- Self-documenting format with clear namespace hierarchy
- Consistent with RFC 9457 recommendation that Type URIs don't need to resolve

All Type URIs use the format: `urn:dispatch:error:{type}` with lowercase kebab-case suffixes.

```csharp
using Excalibur.Dispatch;

// Use constants instead of inline strings
var problemDetails = new MessageProblemDetails
{
    Type = ProblemDetailsTypes.NotFound,  // "urn:dispatch:error:not-found"
    Title = "Resource Not Found",
    Status = 404,
    Detail = $"Order {orderId} was not found"
};
```

### Available Type Constants

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
| `ProblemDetailsTypes.HandlerError` | `urn:dispatch:error:handler-error` | A message bus failed while delivering to a handler. An in-process handler's own exception propagates instead. |
| `ProblemDetailsTypes.MappingFailed` | `urn:dispatch:error:mapping-failed` | Exception mapping to problem details failed |
| `ProblemDetailsTypes.BackgroundExecution` | `urn:dispatch:error:background-execution` | Background task execution failed |

### Extending with Custom Types

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

## See Also

- [Results and Errors](../core-concepts/results-and-errors.md) — The concepts behind these APIs, and when to use each pattern
- [Results to HTTP Responses](../middleware/results-to-http.md) — Terminal operators that convert a result to an `IResult`
- [Exception Mapping](../middleware/exception-mapping.md) — Convert these exceptions to Problem Details centrally
