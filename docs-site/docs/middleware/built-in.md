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
services.AddDispatch(dispatch =>
{
    // Shorthand registration
    dispatch.UseValidation();
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

## Rate Limiting Middleware

Protects the system from excessive message processing:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseRateLimiting(); // Registers RateLimitingMiddleware
});
```

### Configuration

```csharp
services.Configure<RateLimitingOptions>(options =>
{
    options.PermitLimit = 100;
    options.Window = TimeSpan.FromSeconds(10);
    options.QueueLimit = 50;
});
```

### Supported Algorithms

| Algorithm | Description |
|-----------|-------------|
| Token Bucket | Smooth rate limiting with burst allowance |
| Sliding Window | Rate limiting based on a sliding time window |
| Fixed Window | Rate limiting based on fixed time windows |
| Concurrency | Limits concurrent message processing |

:::tip Pipeline Order
Place `UseRateLimiting()` **before** `UseRetry()` to prevent retry amplification:

```csharp
dispatch.UseExceptionMapping()
        .UseAuthentication()
        .UseAuthorization()
        .UseValidation()
        .UseRateLimiting()   // Before retry
        .UseRetry()
        .UseCircuitBreaker();
```
:::

## Retry Middleware

Automatic retry with configurable policies:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseRetry(); // Registers RetryMiddleware
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

## Transaction Middleware

Wraps downstream handlers in a transaction scope for atomic commit/rollback:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseTransaction(); // Registers TransactionMiddleware
});
```

Ensures that all state changes within the handler execute atomically -- if any step fails, the entire transaction is rolled back.

:::tip Pipeline Order
Place `UseTransaction()` late in the pipeline, after validation but before outbox:

```csharp
dispatch.UseValidation()
        .UseTransaction()
        .UseOutbox();
```
:::

## Outbox Middleware

Stores outgoing messages in an outbox for reliable at-least-once delivery:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseOutbox(); // Registers OutboxMiddleware
});
```

Messages are persisted to the outbox store within the current transaction and delivered asynchronously by a background processor.

:::tip Pipeline Order
Place `UseOutbox()` at the end of the pipeline, after `UseTransaction()`:

```csharp
dispatch.UseTransaction()
        .UseOutbox();
```
:::

## Inbox / Idempotency Middleware

Tracks processed messages for idempotent handling and deduplicates before handler execution:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseInbox();       // Registers InboxMiddleware
    // OR
    dispatch.UseIdempotency(); // Alias -- registers the same InboxMiddleware
});
```

Both `UseInbox()` and `UseIdempotency()` register the same `InboxMiddleware`. Use whichever name best communicates your intent.

:::tip Pipeline Order
Place inbox/idempotency early, before validation and transaction, to reject duplicates before doing any work:

```csharp
dispatch.UseInbox()
        .UseValidation()
        .UseTransaction()
        .UseOutbox();
```
:::

## CloudEvents Middleware

Enriches messages with CloudEvents metadata (source, type, subject) per the [CloudEvents specification](https://cloudevents.io/):

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseCloudEvents(); // Registers CloudEventMiddleware
});
```

:::tip Pipeline Order
Place `UseCloudEvents()` early in the pipeline so downstream middleware sees CE metadata:

```csharp
dispatch.UseCloudEvents()
        .UseAuthentication()
        .UseAuthorization()
        .UseValidation();
```
:::

## Tenant Identity Middleware

Resolves the current tenant from message context and makes it available to downstream handlers:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseTenantIdentity(); // Registers TenantIdentityMiddleware
});
```

:::tip Pipeline Order
Place after authentication but before authorization, so tenant context is available for tenant-scoped authorization policies:

```csharp
dispatch.UseAuthentication()
        .UseTenantIdentity()
        .UseAuthorization();
```
:::

## Input Sanitization Middleware

Sanitizes message properties to prevent injection attacks (XSS, SQL injection, etc.) before handler execution:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseInputSanitization(); // Registers InputSanitizationMiddleware
});
```

:::tip Pipeline Order
Place after authorization but before validation, so sanitized values are what gets validated:

```csharp
dispatch.UseAuthorization()
        .UseInputSanitization()
        .UseValidation();
```
:::

## Performance Middleware

Tracks message processing performance with detailed timing metrics:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UsePerformance(); // Registers PerformanceMiddleware
});
```

## Background Execution Middleware

Offloads message processing to a background thread, freeing the caller:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseBackgroundExecution(); // Registers BackgroundExecutionMiddleware
});
```

## Batching Middleware

Batches multiple messages for unified processing, improving throughput:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseBatching(); // Registers UnifiedBatchingMiddleware
});
```

## Contract Versioning Middleware

Validates message contract versions before handler execution:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseContractVersioning(); // Registers ContractVersionCheckMiddleware
});
```

## Audit Logging Middleware

Logs message processing for audit trail and compliance:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseAuditLogging(); // Registers AuditLoggingMiddleware
});
```

## Low-Allocation Validation Middleware

Validates messages using a low-allocation path for high-throughput scenarios:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseZeroAllocMiddleware(); // Registers ZeroAllocationValidationMiddleware
});
```

## CloudEvents Sub-Extensions

In addition to `UseCloudEvents()` (which registers the core CloudEvent middleware), three service registration extensions provide CloudEvents-specific functionality:

```csharp
services.AddDispatch(dispatch =>
{
    // Core CloudEvents middleware (enriches messages with CE metadata)
    dispatch.UseCloudEvents();

    // Validate CloudEvents before processing
    dispatch.UseCloudEventValidation(async (cloudEvent, ct) =>
    {
        // Return true if valid, false to reject
        return cloudEvent.Type is not null;
    });

    // Batch CloudEvents for efficient processing
    dispatch.UseCloudEventBatching(options =>
    {
        // Configure batch options
    });

    // Transform CloudEvents during processing
    dispatch.UseCloudEventTransformation(async (cloudEvent, dispatchEvent, context, ct) =>
    {
        // Transform the event
    });
});
```

:::note
These extensions register **services** (not pipeline middleware). The `Use*()` naming provides consistency with the pipeline API surface.
:::

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

### Fine-Grained Middleware Stacks

For more control, use composable stacks instead of all-or-nothing presets:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Compose stacks as needed
    dispatch.UseSecurityStack()      // Authentication → Authorization → TenantIdentity
            .UseResilienceStack()    // Timeout → Retry → CircuitBreaker
            .UseValidationStack();   // Validation → ExceptionMapping
});
```

| Stack | Middleware (in order) |
|-------|----------------------|
| `UseSecurityStack()` | AuthenticationMiddleware, AuthorizationMiddleware, TenantIdentityMiddleware |
| `UseResilienceStack()` | TimeoutMiddleware, RetryMiddleware, CircuitBreakerMiddleware |
| `UseValidationStack()` | ValidationMiddleware, ExceptionMappingMiddleware |

Stacks can be combined freely with individual middleware. For example, use a security stack with custom logging:

```csharp
dispatch.UseSecurityStack()
        .UseLogging()
        .UseValidationStack();
```

## Recommended Pipeline Order

When combining multiple middleware, use this recommended order:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch
        .UseCloudEvents()          // Enrich early with CE metadata
        .UsePerformance()          // Track processing timing
        .UseAuthentication()       // Establish identity
        .UseTenantIdentity()       // Resolve tenant after auth
        .UseAuthorization()        // Check permissions
        .UseAuditLogging()         // Audit trail after auth
        .UseInbox()                // Deduplicate before processing
        .UseInputSanitization()    // Sanitize before validation
        .UseContractVersioning()   // Validate message version
        .UseValidation()           // Validate structure
        .UseRateLimiting()         // Throttle before retry
        .UseRetry()                // Retry transient failures
        .UseTransaction()          // Wrap in transaction
        .UseOutbox();              // Store for reliable delivery
});
```

Not all middleware is required -- pick the ones you need for your scenario. The order matters: security middleware should run before business logic middleware, and reliability middleware (retry, circuit breaker) should wrap the innermost operations.

### Available Extensions Reference

| Extension | Middleware | Category |
|-----------|-----------|----------|
| `UseLogging()` | `LoggingMiddleware` | Observability |
| `UseMetrics()` | `MetricsMiddleware` | Observability |
| `UseTracing()` | `TracingMiddleware` | Observability |
| `UsePerformance()` | `PerformanceMiddleware` | Observability |
| `UseAuditLogging()` | `AuditLoggingMiddleware` | Observability |
| `UseValidation()` | `ValidationMiddleware` | Validation |
| `UseInputSanitization()` | `InputSanitizationMiddleware` | Validation |
| `UseZeroAllocMiddleware()` | `ZeroAllocationValidationMiddleware` | Validation |
| `UseContractVersioning()` | `ContractVersionCheckMiddleware` | Validation |
| `UseRetry()` | `RetryMiddleware` | Resilience |
| `UseCircuitBreaker()` | `CircuitBreakerMiddleware` | Resilience |
| `UseTimeout()` | `TimeoutMiddleware` | Resilience |
| `UseBulkhead()` | `BulkheadMiddleware` | Resilience |
| `UseRateLimiting()` | `RateLimitingMiddleware` | Resilience |
| `UseExceptionMapping()` | `ExceptionMappingMiddleware` | Error Handling |
| `UseTransaction()` | `TransactionMiddleware` | Reliability |
| `UseOutbox()` | `OutboxMiddleware` | Reliability |
| `UseInbox()` | `InboxMiddleware` | Reliability |
| `UseIdempotency()` | `InboxMiddleware` (alias) | Reliability |
| `UseDeduplication()` | `DeduplicationMiddleware` | Reliability |
| `UseCloudEvents()` | `CloudEventMiddleware` | Messaging |
| `UseTenantIdentity()` | `TenantIdentityMiddleware` | Security |
| `UseBackgroundExecution()` | `BackgroundExecutionMiddleware` | Threading |
| `UseBatching()` | `UnifiedBatchingMiddleware` | Throughput |
| `UseCloudEventValidation()` | Service registration | CloudEvents |
| `UseCloudEventBatching()` | Service registration | CloudEvents |
| `UseCloudEventTransformation()` | Service registration | CloudEvents |

## Next Steps

- [Custom Middleware](custom.md) -- Create your own middleware
- [Validation](validation.md) -- Deep dive into validation
- [Authorization](authorization.md) -- Authorization patterns

## See Also

- [Custom Middleware](custom.md) - Create your own middleware for application-specific cross-cutting concerns
- [Pipeline Overview](../pipeline/index.md) - Understand pipeline stages and how middleware is ordered
- [Middleware Overview](index.md) - Introduction to middleware concepts and registration
