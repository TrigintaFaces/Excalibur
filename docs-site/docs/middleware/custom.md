---
sidebar_position: 3
title: Custom Middleware
description: Create custom middleware for your specific cross-cutting concerns
---

# Custom Middleware

Create custom middleware to implement cross-cutting concerns specific to your application.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [pipeline concepts](../pipeline/index.md) and [built-in middleware](./built-in.md)

## Basic Structure

```csharp
public class MyMiddleware : IDispatchMiddleware
{
    private readonly IMyService _service;

    public MyMiddleware(IMyService service)
    {
        _service = service;
    }

    // Optional: specify pipeline stage
    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate next,
        CancellationToken cancellationToken)
    {
        // Pre-processing logic

        // Call next middleware
        var result = await next(message, context, cancellationToken);

        // Post-processing logic

        return result;
    }
}
```

## Registration

```csharp
services.AddDispatch(options =>
{
    options.ConfigurePipeline("Default", pipeline =>
    {
        pipeline.Use<MyMiddleware>();
    });
});

// The middleware is automatically registered in DI
// Or register explicitly:
services.AddScoped<MyMiddleware>();
```

## Common Patterns

### Timing and Metrics

```csharp
public class TimingMiddleware : IDispatchMiddleware
{
    private readonly ILogger<TimingMiddleware> _logger;
    private readonly IMeterFactory _meterFactory;

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Logging;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate next,
        CancellationToken ct)
    {
        var messageType = message.GetType().Name;
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await next(message, context, ct);
            sw.Stop();

            _logger.LogInformation(
                "{MessageType} completed in {Duration}ms",
                messageType, sw.ElapsedMilliseconds);

            RecordMetric(messageType, sw.ElapsedMilliseconds, success: true);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            RecordMetric(messageType, sw.ElapsedMilliseconds, success: false);
            throw;
        }
    }
}
```

### Context Enrichment

```csharp
public class CorrelationMiddleware : IDispatchMiddleware
{
    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate next,
        CancellationToken ct)
    {
        // Ensure correlation ID exists
        if (string.IsNullOrEmpty(context.CorrelationId))
        {
            context.CorrelationId = Guid.NewGuid().ToString();
        }

        // Add to activity for distributed tracing
        Activity.Current?.SetTag("correlation.id", context.CorrelationId);

        // Add to log scope
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = context.CorrelationId
        }))
        {
            return await next(message, context, ct);
        }
    }
}
```

### Tenant Resolution

```csharp
public class TenantMiddleware : IDispatchMiddleware
{
    private readonly ITenantResolver _resolver;

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate next,
        CancellationToken ct)
    {
        // Resolve tenant from message or context
        var tenantId = await _resolver.ResolveAsync(message, context, ct);

        if (string.IsNullOrEmpty(tenantId))
        {
            return MessageResult.Failed(new TenantRequiredError());
        }

        // Store tenant in context (direct property for hot-path access)
        context.TenantId = tenantId;

        // Configure tenant-specific services
        using (var scope = context.RequestServices.CreateScope())
        {
            var tenantDb = scope.ServiceProvider.GetRequiredService<ITenantDatabase>();
            tenantDb.SetTenant(tenantId);

            return await next(message, context, ct);
        }
    }
}
```

### Audit Logging

```csharp
public class AuditMiddleware : IDispatchMiddleware
{
    private readonly IAuditLogger _auditLogger;

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PostProcessing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate next,
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;

        var result = await next(message, context, ct);

        // Only audit mutations (actions)
        if (message is IDispatchAction action)
        {
            await _auditLogger.LogAsync(new AuditEntry
            {
                MessageType = message.GetType().Name,
                MessageId = message.MessageId,
                UserId = context.UserId,
                TenantId = context.TenantId,
                Timestamp = startTime,
                Duration = DateTime.UtcNow - startTime,
                Success = result.IsSuccess,
                ErrorMessage = result.ErrorMessage
            }, ct);
        }

        return result;
    }
}
```

### Rate Limiting

```csharp
public class RateLimitMiddleware : IDispatchMiddleware
{
    private readonly IRateLimiter _limiter;

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.RateLimiting;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate next,
        CancellationToken ct)
    {
        var clientId = context.GetItem<string>("ClientId") ?? "anonymous";
        var messageType = message.GetType().Name;

        var lease = await _limiter.AcquireAsync(
            $"{clientId}:{messageType}",
            ct);

        if (!lease.IsAcquired)
        {
            return MessageResult.Failed(new RateLimitExceededError
            {
                RetryAfter = lease.RetryAfter
            });
        }

        try
        {
            return await next(message, context, ct);
        }
        finally
        {
            lease.Dispose();
        }
    }
}
```

### Circuit Breaker

```csharp
public class CircuitBreakerMiddleware : IDispatchMiddleware
{
    private readonly ICircuitBreakerPolicy _circuitBreaker;

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate next,
        CancellationToken ct)
    {
        var circuitKey = message.GetType().Name;

        if (!_circuitBreaker.IsAllowed(circuitKey))
        {
            return MessageResult.Failed(new CircuitOpenError
            {
                RecoveryTime = _circuitBreaker.GetRecoveryTime(circuitKey)
            });
        }

        try
        {
            var result = await next(message, context, ct);

            if (result.IsSuccess)
                _circuitBreaker.RecordSuccess(circuitKey);
            else
                _circuitBreaker.RecordFailure(circuitKey);

            return result;
        }
        catch (Exception)
        {
            _circuitBreaker.RecordFailure(circuitKey);
            throw;
        }
    }
}
```

## Accessing Request Data

### From Message

```csharp
public async ValueTask<IMessageResult> InvokeAsync(
    IDispatchMessage message,
    IMessageContext context,
    DispatchRequestDelegate next,
    CancellationToken ct)
{
    // Type check
    if (message is CreateOrderAction order)
    {
        // Access message properties
        var customerId = order.CustomerId;
    }

    // Check message kind
    if (message.Kind == MessageKinds.Event)
    {
        // Handle event-specific logic
    }
}
```

### From Context

```csharp
// Read direct properties (hot-path, preferred for common values)
var userId = context.UserId;
var tenantId = context.TenantId;
var correlationId = context.CorrelationId;

// Read custom items from dictionary
var tenant = context.GetItem<Tenant>("CurrentTenant");

// Write custom values to Items dictionary
context.SetItem("ProcessingStarted", DateTime.UtcNow);
context.SetItem("RequestSource", "API");

// Access scoped services
var db = context.RequestServices.GetRequiredService<IDbConnection>();
```

## Error Handling

### Returning Errors

```csharp
public async ValueTask<IMessageResult> InvokeAsync(...)
{
    if (!IsValid(message))
    {
        return MessageResult.Failed(
            MessageProblemDetails.ValidationError("Field: Error message"));
    }

    if (!IsAuthorized(context))
    {
        return MessageResult.Failed("Access denied");
    }

    return await next(message, context, ct);
}
```

### Wrapping Exceptions

```csharp
public async ValueTask<IMessageResult> InvokeAsync(...)
{
    try
    {
        return await next(message, context, ct);
    }
    catch (ExternalServiceException ex)
    {
        _logger.LogError(ex, "External service failed");
        return MessageResult.Failed(new ExternalServiceError(ex.Message));
    }
}
```

## Testing Middleware

```csharp
public class TimingMiddlewareTests
{
    [Fact]
    public async Task Logs_Duration_For_Successful_Request()
    {
        // Arrange
        var logger = new FakeLogger<TimingMiddleware>();
        var middleware = new TimingMiddleware(logger);

        var message = new TestAction();
        var context = new MessageContext();
        var next = A.Fake<DispatchRequestDelegate>();
        A.CallTo(() => next(message, context, A<CancellationToken>._))
            .Returns(MessageResult.Success());

        // Act
        await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        // Assert
        logger.Logs.Should().ContainSingle(log =>
            log.Message.Contains("completed in") &&
            log.Level == LogLevel.Information);
    }
}
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Dependencies | Use constructor injection |
| Stage | Always specify a stage for ordering |
| Async | Use async/await properly |
| Exceptions | Catch and convert to results |
| Context | Don't modify message, use context |
| Disposal | Implement IAsyncDisposable if needed |

## Next Steps

- [Built-in Middleware](built-in.md) — Reference implementations
- [Validation](validation.md) — Input validation patterns

## See Also

- [Built-in Middleware](built-in.md) - Reference implementations for logging, validation, authorization, caching, and more
- [Middleware Overview](index.md) - Introduction to middleware concepts, stages, and registration
- [Pipeline Profiles](../pipeline/profiles.md) - Configure named pipeline profiles with different middleware stacks
