---
sidebar_position: 7
title: Exception Mapping
description: Map thrown exceptions to RFC 7807 Problem Details centrally, instead of try-catch in every handler
---

# Exception Mapping

This guide shows how to configure centralized exception-to-Problem-Details mapping so handlers can throw and the pipeline produces a consistent failed `IMessageResult`. For the result type and the choice between throwing and returning a failure, see [Results and Errors](../core-concepts/results-and-errors.md).

## Before You Start

- **.NET 10.0**
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [results and errors](../core-concepts/results-and-errors.md) and the [pipeline](../pipeline/index.md)

## How It Works

Dispatch provides centralized exception-to-HTTP mapping that automatically converts exceptions to RFC 7807 Problem Details responses. This eliminates boilerplate try-catch blocks in handlers and ensures consistent error formatting.

## Configuring Exception Mapping

Configure exception mapping using the `WithExceptionMapping` extension on the dispatch builder:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.WithExceptionMapping(mapping =>
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
dispatch.WithExceptionMapping(mapping =>
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

## Common Mapping Patterns

### Database Exceptions

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

### External HTTP Calls

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

### Timeout Handling

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

## Migration from Manual Exception Handling

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

## Converting Handler Results from Exceptions

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

## ToProblemDetails() Conversion

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

## IExceptionMapper Service

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

## See Also

- [Results and Errors](../core-concepts/results-and-errors.md) — When to throw and when to return a failed result
- [Result API Reference](../reference/result-api.md) — The exception hierarchy and every specialized exception type
- [Results to HTTP Responses](results-to-http.md) — Turn the mapped failure into an HTTP response
- [Error Handling Patterns](../patterns/error-handling.md) — Dead-letter queues, retries, and resilient error handling strategies
