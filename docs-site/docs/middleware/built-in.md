---
sidebar_position: 2
title: Built-in Middleware
description: Pre-built middleware components for common cross-cutting concerns
---

# Built-in Middleware

Dispatch includes middleware for common cross-cutting concerns. Enable them individually or use presets.

## Before You Start

- **.NET 10.0**
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

if (!result.Succeeded && result.ValidationResult is ValidationResult validationResult)
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

For ASP.NET Core applications, the authorization bridge reads standard `[Authorize]` attributes from message **and handler** types, composes them into a policy with ASP.NET Core's own `IAuthorizationPolicyProvider`, and evaluates that policy through its `IAuthorizationService`. The `ClaimsPrincipal` is sourced from `HttpContext.User`.

Because the host's provider does the composing, everything you configure through `AddAuthorization` applies here unchanged — named policies, requirements, handlers, and **the default policy a bare `[Authorize]` resolves to**. There is no separate default-policy setting on this middleware, deliberately: a second place to configure one thing is a second place for the two to disagree, and the host's is the one that governs the rest of your application.

```csharp
// Configure authorization ONCE, as you would for controllers or endpoints.
services.AddAuthorization(options =>
{
    // A bare [Authorize] on a message or handler resolves THIS.
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim("scope", "orders.write")
        .Build();
});

services.AddDispatch(dispatch =>
{
    dispatch.UseAspNetCoreAuthorization(options =>
    {
        options.RequireAuthenticatedUser = true;
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

There is no default-policy option here. A bare `[Authorize]` resolves `AuthorizationOptions.DefaultPolicy` — the one you configure with `AddAuthorization`.

#### What happens when the handler cannot be determined

The bridge resolves which handlers will process a message from the dispatch handler registry, so a requirement declared on a **handler** is enforced even when the message itself declares nothing.

When it cannot determine them, it does **not** assume none applies:

| situation | outcome |
|---|---|
| handlers known, none declares a requirement | the message proceeds |
| an action whose handlers cannot be determined | **refused**, with a log entry naming the message type |
| an event with no registered subscriber | proceeds — no handler will run, so there is nothing to enforce |

An action reaches a handler by definition, so if the bridge cannot see what that handler requires it declines rather than guesses. Register the handler through the dispatch handler registry — `AddDispatchHandlers()` discovers everything registered in DI — or declare the requirement on the message type, and the decision becomes determinable.

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
                Detail = string.Join(", ", ex.ValidationErrors.SelectMany(e => e.Value))
            },
            ResourceNotFoundException ex => new MessageProblemDetails
            {
                Type = "not-found",
                Title = "Resource Not Found",
                Status = 404,
                Detail = ex.Message
            },
            UnauthorizedAccessException => new MessageProblemDetails
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
    dispatch.UseThrottling(); // Registers ThrottlingMiddleware (system throughput protection)
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

Throttling always runs outside retry, so a rejected message is rejected once rather than being retried
into the limiter. You do not arrange this by call order: position comes from each middleware's stage, and
the pipeline composes the lower-numbered stage as the outer wrapper. Throttling is pre-processing (100),
exception mapping is post-processing (700), retry and the circuit breaker are error-handling (800).

```csharp
dispatch.UseExceptionMapping()
        .UseAuthentication()
        .UseAuthorization()
        .UseValidation()
        .UseThrottling()
        .UseRetry()
        .UseCircuitBreaker();
```

Call order decides position only among middleware that share a stage, where registration order applies.
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
    options.MaxRetryAttempts = 3;
    options.BaseDelay = TimeSpan.FromMilliseconds(100);
    options.MaxDelay = TimeSpan.FromSeconds(30);
    options.BackoffMultiplier = 2.0;
    // Fixed, Linear, Exponential, ExponentialWithJitter, Fibonacci, or FullJitter
    // (AWS-style full jitter — maximally decorrelates concurrent retries).
    options.BackoffStrategy = BackoffStrategy.Exponential;

    // Configure retryable exceptions
    options.RetryableExceptions.Add(typeof(TransientException));

    // Configure non-retryable exceptions (these are never retried)
    options.NonRetryableExceptions.Add(typeof(ValidationException));
});
```

### What Gets Retried

The retry middleware distinguishes **transient** from **permanent** failures, matching Polly / `HttpClientFactory` `HandleTransientHttpError` semantics:

| Outcome | Retried? |
|---------|----------|
| Failed `IMessageResult` with RFC 7807 status `408`, `429`, or `5xx` | **Yes** (transient) |
| Failed `IMessageResult` with a `4xx` status other than 408/429 | **No** (permanent client error — retrying cannot fix it and risks re-running a non-idempotent handler) |
| Failed `IMessageResult` with no `ProblemDetails` / no `Status` | **No** (a returned failure with no transient signal is a handler statement that retry will not help) |
| Successful `IMessageResult` | **No** |
| **Thrown exception** | Governed separately by `RetryableExceptions` / `NonRetryableExceptions` (above) — unchanged |

:::note Behavior change
Earlier versions retried *every* non-success result. If you previously relied on a non-transient result being retried, return a transient status (408/429/5xx) or throw a retryable exception instead. Genuine transient faults typically surface as exceptions and are handled by the exception path. See [What's New](../whats-new.md).
:::

The computed backoff delay is always clamped to `MaxDelay` before the wait `TimeSpan` is constructed, so a high attempt count can never overflow — it collapses to `MaxDelay`.

### Backoff Strategies

| Strategy | Description |
|----------|-------------|
| `Fixed` | Same delay between each attempt |
| `Linear` | Delay increases linearly (BaseDelay × attempt) |
| `Exponential` | Delay doubles each attempt |
| `ExponentialWithJitter` | Exponential with random jitter to prevent thundering herd |

### Per-Message Retry Policy

```csharp
[Retry(MaxRetryAttempts = 5, BaseDelayMs = 500)]
public record ImportDataAction(...) : IDispatchAction;
```

## Caching Middleware

Response caching for dispatch actions using .NET HybridCache:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseCaching(); // Registers CachingMiddleware with HybridCache
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
    dispatch.UseOutbox(); // Registers OutboxStagingMiddleware and CascadeMiddleware
});
```

Calling `UseOutbox()` states that this host stages, so it **requires an `IOutboxStore`**: register one for your provider alongside this call, or the host refuses to start and names the registration it is missing. You do not need `UseOutbox()` to get staging on the default pipeline — registering a store is enough there, and a host with no store simply has no outbox. Reach for `UseOutbox()` when you want the missing store reported at startup, or when you need the cascade step it also adds.

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

:::warning Delivery guarantee
The inbox/idempotency middleware uses an atomic claim-before-execute protocol. Its guarantee is **exactly-once for *concurrent* redelivery** (the atomic claim blocks the second caller) but **at-least-once across a *process crash*** — the claim and the post-handler mark are two steps, not one transaction, so a crash mid-handler leads to a reclaim-and-retry. **Handlers must be idempotent** to be safe across the crash boundary. See the [Idempotent Consumer Guide](../patterns/idempotent-consumer.md).
:::

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

### The `type` Attribute

The CloudEvents `type` attribute is the identifier external subscribers filter on -- an AWS EventBridge
rule or an Azure Event Grid subscription matches against it. For a message your own dispatcher sends,
`type` is the message's **declared name** (`[MessageName("...")]`), never the CLR type's `FullName`: a
declared name is stable across namespace, assembly, and assembly-version changes, while a name derived
from the CLR type breaks the moment any of those change. Renaming a message type later is done by
keeping the old name reachable with `[MessageNameAlias("...")]`, not by re-deriving `type` from the new
CLR name.

When a message is a **re-emitted foreign CloudEvent** (received from another organisation and forwarded
rather than originated here), `type` instead preserves that event's original identity verbatim --
overwriting it with your own declared name would corrupt provenance for anyone consuming the
re-emitted event.

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

A message opts in by implementing the `IExecuteInBackground` marker interface. The dispatch returns at once with a successful result whose `Disposition` is `MessageDisposition.AcceptedForBackgroundExecution` — the work is accepted and **pending**, and the handler has not run yet. Do not record it as completed on the strength of that result.

**Cancellation.** If the dispatch's cancellation token is already cancelled, the message is **not accepted**: the handler never runs, and the result is the cancelled result (`Succeeded` is `false`). Once a message is accepted, the handler no longer receives your token — a request-scoped token fires when the response that carries "accepted" completes, which would cancel the work you were just told was accepted. The handler receives a token that is cancelled only when the host's shutdown timeout elapses with the work still running.

**Failures.** A background handler that throws is always logged as an error. What happens next is a host-level setting:

```csharp
services.Configure<BackgroundExecutionOptions>(options =>
{
    // LogOnly (default): log the failure and keep running.
    // StopHost: log the failure, then stop the host through IHostApplicationLifetime.
    options.ExceptionBehavior = BackgroundExecutionExceptionBehavior.StopHost;
});
```

`StopHost` needs an `IHostApplicationLifetime`, which every generic host provides. If it is configured where none is registered, the application fails at startup with an options validation error, rather than quietly falling back to logging at the first failure.

:::warning Background execution is decoupled, not durable
- **Graceful shutdown waits for it.** When the host stops, in-flight background work is awaited, bounded by the host's own `HostOptions.ShutdownTimeout` (30 seconds by default). Work started by that work while the host is stopping is awaited too.
- **Work that outlives the budget is lost, and logged.** When the shutdown timeout elapses, the token passed to any handler still running is cancelled so it can stop cleanly, and an error is logged with the number of tasks lost. Raise `ShutdownTimeout` if your background work is expected to finish in time.
- **An abrupt stop loses it without a drain.** A process kill, an out-of-memory termination or a node eviction does not run the shutdown drain, so in-flight background work is lost. That is inherent to running in-process.

If the work must survive a crash or a restart, do not background it — route it through the [outbox](#outbox-middleware), which persists it before the caller is answered.
:::

## Batching Middleware

Batches multiple messages for unified processing, improving throughput:

```csharp
services.AddDispatch(dispatch =>
{
    dispatch.UseBatching(); // Registers UnifiedBatchingMiddleware
});
```

Tune it through `UnifiedBatchingOptions`:

```csharp
services.Configure<UnifiedBatchingOptions>(options =>
{
    options.MaxBatchSize = 64;                            // default: 32
    options.MaxBatchDelay = TimeSpan.FromMilliseconds(50); // default: 250ms
    options.MaxParallelism = 8;                            // default: Environment.ProcessorCount
});
```

A batch is dispatched when it reaches `MaxBatchSize`, or when `MaxBatchDelay` elapses with a
partial batch waiting — whichever comes first. `MaxParallelism` caps how many batches are
processed concurrently.

:::note Invalid values fail at startup, not at the first message
`UseBatching()` validates these three settings when the host starts. A non-positive value throws
during startup with a message naming the setting and the value it was given.

This is deliberate, because two of the three would otherwise fail silently. A `MaxBatchSize` of
zero means a batch is never full, so batches only ever flush on the delay timer — throughput
collapses with nothing logged. A non-positive `MaxBatchDelay` turns the flush timer into a busy
loop. Neither reports an error on its own; you would see the symptom in production and have no
signal pointing at the cause.
:::

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

## Ordering Validation Middleware

Enforces strictly-increasing per-key ordering for messages that arrived through a receive path where
ordering is enforced -- fail-closed on an out-of-order or unstamped message on that path:

```csharp
services.AddDispatch(typeof(Program).Assembly); // or services.AddDispatch(dispatch => { ... })
services.AddOrderingValidation(); // Registers OrderingValidationMiddleware
```

Unlike the other middleware on this page, this is a **service collection** extension, not a dispatch
builder one -- call it alongside `AddDispatch(...)`, not inside its lambda. It also needs one more
thing before it does anything: your receive-to-dispatch bridge must call
`TransportOrderingMetadata.TryStampOrdering(received, context)`, because the framework has no seam that
holds both a received transport message and a dispatch context.

It works with either `AddDispatch` overload. See [Ordering Validation](./ordering-validation.md) for
the full stamping walkthrough and the per-transport native-sequence table.

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

    // Production preset: retry + exception mapping (pair with UseObservability() for metrics/tracing)
    dispatch.UseProductionMiddleware();

    // Full preset: all middleware with sensible defaults
    dispatch.UseFullMiddleware();
});
```

### Preset Contents

| Preset | Middleware Included |
|--------|---------------------|
| Development | Logging (Debug level), Validation, ExceptionMapping |
| Production | Retry, ExceptionMapping (pair with `UseObservability()` for Metrics + Tracing) |
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
        .UseThrottling()            // Throttle before retry
        .UseRetry()                // Retry transient failures
        .UseTransaction()          // Wrap in transaction
        .UseOutbox();              // Store for reliable delivery
});
```

Not all middleware is required -- pick the ones you need for your scenario. The order matters: security middleware should run before business logic middleware, and reliability middleware (retry, circuit breaker) should wrap the innermost operations.

### Available Extensions Reference

All middleware classes listed below are **internal** -- register them using the builder extension methods shown in the first column. Do not reference the concrete class names directly.

| Extension | Middleware (internal) | Category |
|-----------|-----------|----------|
| `UseLogging()` | `LoggingMiddleware` | Observability |
| `UseMetrics()` | `MetricsMiddleware` | Observability |
| `UseTracing()` | `TracingMiddleware` | Observability |
| `UsePerformance()` | `PerformanceMiddleware` | Observability |
| `UseAuditLogging()` | `AuditLoggingMiddleware` | Observability |
| `UseValidation()` | `ValidationMiddleware` | Validation |
| `UseInputSanitization()` | `InputSanitizationMiddleware` | Validation |
| `UseContractVersioning()` | `ContractVersionCheckMiddleware` | Validation |
| `UseRetry()` | `RetryMiddleware` | Resilience |
| `UseCircuitBreaker()` | `CircuitBreakerMiddleware` | Resilience |
| `UseTimeout()` | `TimeoutMiddleware` | Resilience |
| `UseThrottling()` | `ThrottlingMiddleware` | Resilience |
| `UseExceptionMapping()` | `ExceptionMappingMiddleware` | Error Handling |
| `UseTransaction()` | `TransactionMiddleware` | Reliability |
| `UseOutbox()` | `OutboxStagingMiddleware` + `CascadeMiddleware` | Reliability |
| `UseInbox()` | `InboxMiddleware` | Reliability |
| `UseIdempotency()` | `InboxMiddleware` (alias) | Reliability |
| `UseCloudEvents()` | `CloudEventMiddleware` | Messaging |
| `AddOrderingValidation()` | `OrderingValidationMiddleware` | Reliability |
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
