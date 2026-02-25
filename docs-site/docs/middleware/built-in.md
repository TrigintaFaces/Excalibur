---
sidebar_position: 2
title: Built-in Middleware
description: Pre-built middleware components for common cross-cutting concerns
---

# Built-in Middleware

Dispatch includes middleware for common cross-cutting concerns. Enable them individually or use presets.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [pipeline concepts](../pipeline/index.md) and [Dispatch configuration](../core-concepts/configuration.md)

## Logging Middleware

Structured logging for all message processing:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseLogging(); // Registers LoggingMiddleware with default options
});
```

### Configuration

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseLogging(options =>
    {
        // Log level for successful requests
        options.SuccessLevel = LogLevel.Information;

        // Log level for failed requests
        options.FailureLevel = LogLevel.Error;

        // Include message payload in logs (default: false for security)
        options.IncludePayload = false;

        // Include timing information
        options.IncludeTiming = true;

        // Exclude specific message types from logging
        options.ExcludeTypes.Add(typeof(HealthCheckQuery));
    });
});
```

### Log Output

```json
{
  "Timestamp": "2025-01-15T10:30:00Z",
  "Level": "Information",
  "Message": "Message processed successfully",
  "Properties": {
    "MessageType": "CreateOrderAction",
    "MessageId": "abc-123",
    "CorrelationId": "xyz-789",
    "DurationMs": 45,
    "Success": true
  }
}
```

## Validation Middleware

Validates messages using FluentValidation or DataAnnotations:

```csharp
services.AddDispatch(options =>
{
    options.ConfigurePipeline("Default", pipeline =>
    {
        pipeline.Use<ValidationMiddleware>();
    });
});

// Register validators
services.AddValidatorsFromAssembly(typeof(Program).Assembly);
```

### FluentValidation Integration

```csharp
public class CreateOrderValidator : AbstractValidator<CreateOrderAction>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty()
            .WithMessage("Customer ID is required");

        RuleFor(x => x.Items)
            .NotEmpty()
            .WithMessage("Order must have at least one item");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.Quantity)
                    .GreaterThan(0);
            });
    }
}
```

### DataAnnotations Support

```csharp
public record CreateOrderAction(
    [Required] string CustomerId,
    [MinLength(1)] List<OrderItem> Items,
    [Range(0, 1000000)] decimal MaxAmount
) : IDispatchAction;
```

### Validation Results

```csharp
var result = await dispatcher.DispatchAsync(action, ct);

if (!result.IsSuccess && result.ValidationResult is ValidationResult validationResult)
{
    foreach (var error in validationResult.Errors)
    {
        Console.WriteLine($"{error.PropertyName}: {error.Message}");
    }
}
```

## Authorization Middleware

Dispatch provides multiple authorization approaches. Choose the one that fits your scenario.

### ASP.NET Core Authorization Bridge

**Package:** `Excalibur.Dispatch.Hosting.AspNetCore`

For ASP.NET Core applications, the authorization bridge reads standard `[Authorize]` attributes from message and handler types and evaluates them via ASP.NET Core's `IAuthorizationService`. The `ClaimsPrincipal` is sourced from `HttpContext.User`.

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseAspNetCoreAuthorization(options =>
    {
        options.RequireAuthenticatedUser = true;
        options.DefaultPolicy = "MyPolicy"; // optional
    });
});

// Register ASP.NET Core authorization policies as usual
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

    options.AddPolicy("CanCreateOrders", policy =>
        policy.RequireClaim("permission", "orders:create"));
});
```

#### Attribute-Based Authorization

```csharp
using Microsoft.AspNetCore.Authorization;

[Authorize("AdminOnly")]
public record DeleteUserAction(Guid UserId) : IDispatchAction;

[Authorize("CanCreateOrders")]
public record CreateOrderAction(...) : IDispatchAction;

// Multiple policies (AND logic -- all must pass)
[Authorize("CanCreateOrders")]
[Authorize("IsActive")]
public record CreatePriorityOrderAction(...) : IDispatchAction;

// Role-based (OR logic within a single attribute)
[Authorize(Roles = "Admin,Manager")]
public record ManageUsersAction(...) : IDispatchAction;

// Allow anonymous bypass
[AllowAnonymous]
public record GetPublicDataQuery(...) : IDispatchQuery<PublicData>;
```

#### Custom Authorization Requirements

The bridge passes the `IDispatchMessage` as a resource to `AuthorizeAsync`, enabling custom `AuthorizationHandler<TRequirement, IDispatchMessage>` implementations:

```csharp
public class OrderOwnerRequirement : IAuthorizationRequirement
{
    public string ResourceClaim { get; } = "OrderId";
}

public class OrderOwnerHandler : AuthorizationHandler<OrderOwnerRequirement, IDispatchMessage>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrderOwnerRequirement requirement,
        IDispatchMessage resource)
    {
        if (resource is IOrderMessage orderMessage)
        {
            var userId = context.User.FindFirst("sub")?.Value;
            if (orderMessage.OwnerId == userId)
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
```

#### Options

| Option | Default | Description |
|--------|---------|-------------|
| `Enabled` | `true` | Enable/disable the middleware |
| `RequireAuthenticatedUser` | `true` | Reject when `HttpContext` is unavailable or user is unauthenticated. Set to `false` for background job scenarios. |
| `DefaultPolicy` | `null` | Fallback policy when `[Authorize]` specifies no explicit policy |

### A3 Activity-Based Authorization

For grant-based and activity-driven authorization using `[RequirePermission]` attributes, see [Authorization (A3)](authorization.md).

### Dispatch Core Authorization

The core `Excalibur.Dispatch.Middleware.AuthorizationMiddleware` provides config-based authorization using `IMessageContext`. It does not read `[Authorize]` attributes.

### Co-Existence

All three authorization middlewares can be registered in the same pipeline -- they check different attributes and use different identity sources. See the ASP.NET Core authorization bridge documentation for the co-existence model.

## Exception Mapping Middleware

Converts exceptions to structured RFC 7807 Problem Details:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseExceptionMapping(); // Registers ExceptionMappingMiddleware
});
```

### Custom Exception Mappers

Register custom `IExceptionMapper` implementations to control how exceptions are converted:

```csharp
public class CustomExceptionMapper : IExceptionMapper
{
    public IMessageProblemDetails Map(Exception exception)
    {
        return exception switch
        {
            ValidationException ex => new MessageProblemDetails
            {
                Type = "validation-error",
                Title = "Validation Failed",
                Status = 400,
                Detail = string.Join(", ", ex.Errors)
            },
            NotFoundException ex => new MessageProblemDetails
            {
                Type = "not-found",
                Title = "Resource Not Found",
                Status = 404,
                Detail = ex.Message
            },
            UnauthorizedException => new MessageProblemDetails
            {
                Type = "unauthorized",
                Title = "Unauthorized",
                Status = 401
            },
            _ => new MessageProblemDetails
            {
                Type = "internal-error",
                Title = "Internal Server Error",
                Status = 500,
                Detail = exception.Message
            }
        };
    }

    public Task<IMessageProblemDetails> MapAsync(
        Exception exception,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(Map(exception));
    }

    public bool CanMap(Exception exception) => true; // Handles all exception types
}

// Register in DI
services.AddSingleton<IExceptionMapper, CustomExceptionMapper>();
```

> **Note:** `OperationCanceledException` is never mapped and is always re-thrown to allow proper cancellation propagation.

## Metrics Middleware

OpenTelemetry metrics for observability:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseMetrics(); // Registers MetricsMiddleware
});

services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddDispatchMetrics(); // Adds Excalibur.Dispatch.Core meter
        metrics.AddOtlpExporter();
    });
```

### Emitted Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `dispatch.messages.processed` | Counter | Total messages processed |
| `dispatch.messages.duration` | Histogram | Processing duration in ms |
| `dispatch.messages.published` | Counter | Messages published |
| `dispatch.messages.failed` | Counter | Failed messages |
| `dispatch.sessions.active` | Gauge | Active sessions |

### Metric Tags

- `message_type`: Message class name
- `handler_type`: Handler class name
- `success`: Whether processing succeeded
- `error_type`: Error category (when failed)
- `destination`: Publish destination (when publishing)

## Tracing Middleware

Distributed tracing with OpenTelemetry:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseTracing(); // Registers TracingMiddleware
});

services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("Excalibur.Dispatch"); // Dispatch activity source
        tracing.AddOtlpExporter();
    });
```

### Trace Attributes

| Attribute | Description |
|-----------|-------------|
| `message.type` | Message class name |
| `message.id` | Unique message ID |
| `handler.type` | Handler class name |
| `dispatch.operation` | Operation type (handle, publish, middleware) |
| `middleware.type` | Middleware class name (for middleware spans) |

## Retry Middleware

Automatic retry with configurable policies:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseMiddleware<RetryMiddleware>();
});

services.Configure<RetryOptions>(options =>
{
    options.MaxAttempts = 3;
    options.BaseDelay = TimeSpan.FromMilliseconds(100);
    options.MaxDelay = TimeSpan.FromSeconds(30);
    options.BackoffMultiplier = 2.0;
    options.BackoffStrategy = BackoffStrategy.Exponential;

    // Configure retryable exceptions
    options.RetryableExceptions.Add(typeof(TransientException));

    // Configure non-retryable exceptions (these are never retried)
    options.NonRetryableExceptions.Add(typeof(ValidationException));
});
```

### Backoff Strategies

| Strategy | Description |
|----------|-------------|
| `Fixed` | Same delay between each attempt |
| `Linear` | Delay increases linearly (BaseDelay × attempt) |
| `Exponential` | Delay doubles each attempt |
| `ExponentialWithJitter` | Exponential with random jitter to prevent thundering herd |

### Per-Message Retry Policy

```csharp
[Retry(MaxAttempts = 5, BaseDelayMs = 500)]
public record ImportDataAction(...) : IDispatchAction;
```

## Caching Middleware

Response caching for dispatch actions using .NET HybridCache:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddCaching(); // Registers CachingMiddleware with HybridCache
});
```

### Cache Configuration

```csharp
[CacheResult(ExpirationSeconds = 300)] // 5 minutes
public record GetProductQuery(string ProductId) : IDispatchAction<Product>;

[CacheResult(ExpirationSeconds = 60, OnlyIfSuccess = true, IgnoreNullResult = true)]
public record GetUserPreferencesQuery(string UserId) : IDispatchAction<UserPreferences>;
```

### Interface-Based Caching

For more control, implement `ICacheable<TResult>`:

```csharp
public record GetProductQuery(string ProductId)
    : IDispatchAction<Product>, ICacheable<Product>
{
    public int ExpirationSeconds => 300;

    public bool ShouldCache(Product? result) => result is not null;

    public string[] GetCacheTags() => [$"product:{ProductId}"];
}
```

### Cache Invalidation

Implement `ICacheInvalidator` on messages that should trigger cache invalidation:

```csharp
public record UpdateProductAction(string ProductId, string Name)
    : IDispatchAction, ICacheInvalidator
{
    public IEnumerable<string> GetCacheTagsToInvalidate()
        => [$"product:{ProductId}"];

    public IEnumerable<string> GetCacheKeysToInvalidate()
        => []; // Or specific cache keys
}
```

The `CacheInvalidationMiddleware` automatically invalidates caches when these messages are processed.

## Middleware Presets

Use presets for common configurations:

```csharp
services.AddDispatch(dispatch =>
{
    // Development preset: logging (verbose), validation, exception mapping
    dispatch.UseDevelopmentMiddleware();

    // Production preset: metrics, tracing, retry, exception mapping
    dispatch.UseProductionMiddleware();

    // Full preset: all middleware with sensible defaults
    dispatch.UseFullMiddleware();
});
```

### Preset Contents

| Preset | Middleware Included |
|--------|---------------------|
| Development | Logging (Debug level), Validation, ExceptionMapping |
| Production | Metrics, Tracing, Retry, ExceptionMapping |
| Full | Logging, Validation, Metrics, Tracing, Retry, ExceptionMapping |

## Next Steps

- [Custom Middleware](custom.md) -- Create your own middleware
- [Validation](validation.md) -- Deep dive into validation
- [Authorization](authorization.md) -- Authorization patterns

## See Also

- [Custom Middleware](custom.md) - Create your own middleware for application-specific cross-cutting concerns
- [Pipeline Overview](../pipeline/index.md) - Understand pipeline stages and how middleware is ordered
- [Middleware Overview](index.md) - Introduction to middleware concepts and registration
