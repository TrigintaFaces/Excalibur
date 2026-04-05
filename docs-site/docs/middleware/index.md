---
sidebar_position: 1
title: Middleware
description: Cross-cutting concerns with the Dispatch middleware pipeline
---

# Middleware

Middleware components process messages before and after handlers, providing cross-cutting concerns like logging, validation, and authorization.

:::info Why Should I Care?
Without middleware, every handler ends up with copy-pasted try/catch logging, manual validation checks, and authorization guards. Middleware lets you **write cross-cutting logic once** and apply it to every message automatically -- just like ASP.NET Core middleware but for your dispatch pipeline. Add logging, validation, retry, and auth without touching any handler code.
:::

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [handlers](../handlers.md) and [pipeline concepts](../pipeline/index.md)

## Overview

```mermaid
flowchart LR
    subgraph Pipeline["Middleware Pipeline"]
        direction LR
        M1[Logging] --> M2[Validation]
        M2 --> M3[Authorization]
        M3 --> M4[Custom...]
        M4 --> H[Handler]
        H --> M4
        M4 --> M3
        M3 --> M2
        M2 --> M1
    end

    A[Request] --> M1
    M1 --> R[Response]
```

## Quick Start

### Register Middleware

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Fluent pipeline extensions (recommended)
    dispatch.UseExceptionMapping()
            .UseAuthentication()
            .UseAuthorization()
            .UseValidation()
            .UseThrottling()
            .UseRetry()
            .UseCircuitBreaker();
});
```

:::tip Pipeline.Use vs UseX()
Both approaches register the same middleware. The `Use{Feature}()` extensions are shorthand for the explicit pipeline API:

```csharp
// Shorthand (recommended)
dispatch.UseValidation();

// Equivalent explicit registration
dispatch.ConfigurePipeline("Default", pipeline =>
{
    pipeline.Use<ValidationMiddleware>();
});
```
:::

### Create Custom Middleware

```csharp
public class TimingMiddleware : IDispatchMiddleware
{
    private readonly ILogger<TimingMiddleware> _logger;

    public TimingMiddleware(ILogger<TimingMiddleware> logger)
    {
        _logger = logger;
    }

    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Logging;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate next,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        // Call next middleware
        var result = await next(message, context, ct);

        sw.Stop();
        _logger.LogInformation(
            "{MessageType} completed in {ElapsedMs}ms",
            message.GetType().Name,
            sw.ElapsedMilliseconds);

        return result;
    }
}
```

## Middleware Stages

Middleware executes in defined stages for consistent ordering:

| Stage | Order | Purpose |
|-------|-------|---------|
| `Start` | 0 | Pipeline starting point |
| `RateLimiting` | 50 | Throttle requests |
| `PreProcessing` | 100 | Early processing, enrichment |
| `Instrumentation` | 150 | Performance metrics, telemetry |
| `Authentication` | 175 | Verify identity |
| `Logging` | 190 | Structured logging |
| `Validation` | 200 | Input validation |
| `Serialization` | 250 | Message serialization |
| `Authorization` | 300 | Permission checks |
| `Cache` | 400 | Response caching |
| `Optimization` | 450 | Batching, bulk operations |
| `Routing` | 500 | Message routing |
| `Deduplication` | 599 | Deduplication and idempotency checks |
| `Processing` | 600 | Handler execution |
| `PostProcessing` | 700 | Result processing |
| `ErrorHandling` | 800 | Error handling and compensation |
| `End` | 1000 | Pipeline final stage |

### Stage Assignment

```csharp
public class MyMiddleware : IDispatchMiddleware
{
    // Explicit stage
    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

    // Or null for default ordering
    // public DispatchMiddlewareStage? Stage => null;
}
```

### Custom Stage Ordering

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    dispatch.ConfigurePipeline("Default", pipeline =>
    {
        // Add at specific stage
        pipeline.UseAt<CustomMiddleware>(DispatchMiddlewareStage.PreProcessing);

        // Middleware is ordered by stage - set the Stage property
        // on your middleware class to control execution order
        pipeline.Use<CustomMiddleware>();
    });
});
```

## Built-in Middleware

| Middleware | Stage | Package | Description |
|------------|-------|---------|-------------|
| `LoggingMiddleware` | Logging | Excalibur.Dispatch | Structured request/response logging |
| `ValidationMiddleware` | Validation | Excalibur.Dispatch | FluentValidation / DataAnnotations |
| `AspNetCoreAuthorizationMiddleware` | Authorization | Excalibur.Dispatch.Hosting.AspNetCore | ASP.NET Core `[Authorize]` policy bridge |
| `AuthorizationMiddleware` (A3) | Authorization | Excalibur.A3 | Activity-based `[RequirePermission]` authorization |
| `ExceptionMiddleware` | ErrorHandling | Excalibur.Dispatch | Exception to result conversion |
| `MetricsMiddleware` | Logging | Excalibur.Dispatch | OpenTelemetry metrics |
| `TracingMiddleware` | PreProcessing | Excalibur.Dispatch | Distributed tracing |

## Message Context

Access and modify the message context:

```csharp
public async ValueTask<IMessageResult> InvokeAsync(
    IDispatchMessage message,
    IMessageContext context,
    DispatchRequestDelegate next,
    CancellationToken ct)
{
    // Read direct properties (hot-path, preferred)
    var userId = context.UserId;
    var correlationId = context.CorrelationId;

    // Read/write custom items dictionary
    var customValue = context.GetItem<string>("CustomKey");
    context.SetItem("ProcessedAt", DateTime.UtcNow);

    // Access scoped services
    var db = context.RequestServices.GetRequiredService<IDbConnection>();

    return await next(message, context, ct);
}
```

## Short-Circuiting

Return early without calling the next middleware:

```csharp
public async ValueTask<IMessageResult> InvokeAsync(
    IDispatchMessage message,
    IMessageContext context,
    DispatchRequestDelegate next,
    CancellationToken ct)
{
    // Check cache
    if (TryGetCached(message, out var cached))
    {
        return cached; // Short-circuit - don't call next
    }

    // Continue pipeline
    var result = await next(message, context, ct);

    // Cache result
    Cache(message, result);

    return result;
}
```

## Conditional Middleware

Apply middleware based on conditions:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    dispatch.ConfigurePipeline("Default", pipeline =>
    {
        // Only in development
        pipeline.UseWhen<DebugMiddleware>(
            sp => sp.GetRequiredService<IHostEnvironment>().IsDevelopment());

        // Only for specific message types
        pipeline.UseWhen<AuditMiddleware>(
            message => message is IDispatchAction);

        // Based on configuration
        pipeline.UseWhen<FeatureMiddleware>(
            sp => sp.GetRequiredService<IConfiguration>()
                .GetValue<bool>("Features:NewFeature"));
    });
});
```

## Recommended Reading Order

1. **[Built-in Middleware](built-in.md)** -- Start here. See what's available out of the box.
2. **[Custom Middleware](custom.md)** -- Create your own middleware for application-specific concerns.
3. **[Validation](validation.md)** -- Automatic input validation with FluentValidation.
4. **[Authorization](authorization.md)** -- Permission and policy checks.
5. **[Serialization Providers](serialization-providers.md)** -- Message serialization options.

## Next Steps

- [Pipeline](../pipeline/index.md) -- Understand pipeline stages and execution order
- [Configuration](../core-concepts/configuration.md) -- Register middleware via the dispatch builder

## See Also

- [Handlers](../handlers.md) -- Action and event handlers that middleware wraps
- [Transports](../transports/index.md) -- Transport-level middleware integration
- [Observability](../observability/index.md) -- Tracing and metrics for middleware execution
