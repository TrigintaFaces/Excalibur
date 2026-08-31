---
sidebar_position: 1
title: Pipeline
description: Middleware pipeline for cross-cutting concerns like validation, logging, and authorization
---

# Pipeline

The Dispatch pipeline processes messages through a chain of middleware components. Each middleware can inspect, modify, or short-circuit message processing.

## Before You Start

- **.NET 10.0**
- Install the required package:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [handlers](../handlers.md) and [dependency injection](../core-concepts/dependency-injection.md)

## Pipeline Architecture

Messages flow through middleware in a nested pattern:

```mermaid
flowchart LR
    subgraph Pipeline["Request → Response"]
        direction LR
        R[Request] --> M1[Rate Limiting]
        M1 --> M2[Validation]
        M2 --> M3[Authorization]
        M3 --> H[Handler]
        H --> M3r[Authorization]
        M3r --> M2r[Validation]
        M2r --> M1r[Rate Limiting]
        M1r --> Res[Response]
    end
```

Each middleware can:
- Execute logic before calling `next()`
- Execute logic after `next()` returns
- Modify the message or context
- Short-circuit by returning without calling `next()`
- Handle exceptions from downstream components

## Pipeline Stages

Middleware executes in defined stages. Lower values execute first on request, last on response:

| Stage | Value | Purpose |
|-------|-------|---------|
| `Start` | 0 | Pipeline entry point |
| `RateLimiting` | 50 | Throughput control |
| `PreProcessing` | 100 | Tracing and context setup |
| `Instrumentation` | 150 | Performance metrics |
| `Authentication` | 175 | Identity verification |
| `Logging` | 190 | Audit trails and diagnostics |
| `Validation` | 200 | Input validation |
| `Serialization` | 250 | Message serialization |
| `Authorization` | 300 | Permission checks |
| `Cache` | 400 | Cache lookup and storage |
| `Optimization` | 450 | Batching and bulk operations |
| `Routing` | 500 | Handler resolution |
| `Processing` | 600 | Core handler execution |
| `PostProcessing` | 700 | Cleanup and result transformation |
| `Error` | 800 | Exception handling |
| `ErrorHandling` | 801 | Exception handling (alias) |
| `End` | 1000 | Pipeline exit |

## Quick Start

### Register Middleware

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Configure middleware pipeline
    dispatch.ConfigurePipeline("Default", pipeline =>
    {
        pipeline.Use<LoggingMiddleware>();
        pipeline.Use<ValidationMiddleware>();
        pipeline.Use<AuthorizationMiddleware>();
    });
});
```

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

## Message Kind Filtering

Limit which messages your middleware processes:

```csharp
public class ValidationMiddleware : IDispatchMiddleware
{
    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

    // Only validate commands and queries, not events
    public MessageKinds ApplicableMessageKinds => MessageKinds.Action;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate next,
        CancellationToken ct)
    {
        // Validation logic...
        return await next(message, context, ct);
    }
}
```

### How a message's kind is determined

A message's kind comes from the **interface it implements**, not from a property or an attribute:

| Interface | Kind |
|-----------|------|
| `IDispatchAction` / `IDispatchAction<TResponse>` | `MessageKinds.Action` |
| `IDispatchEvent` | `MessageKinds.Event` |
| `IDispatchDocument` | `MessageKinds.Document` |

`MessageKinds` is a flags enum, so a type implementing more than one of these carries every
corresponding kind.

### Messages with no kind

A type that implements only the bare `IDispatchMessage` marker declares no kind. Dispatch does not
reject it and does not guess: it treats the message as `MessageKinds.All`, so **every** middleware
applies to it rather than the fewest.

That is deliberate and it is not an error. The alternative — applying no middleware to a message whose
kind is unknown — would route an unclassified message straight past validation, authorization and
auditing. Applying all of them is the fail-closed choice.

To find such a type in a running system, look for the activity event
`dispatch.message.unclassified` on the current trace. It carries:

| Tag | Meaning |
|-----|---------|
| `dispatch.message.type` | The full name of the type that declared no kind |
| `dispatch.message.missing_interface` | The interfaces it could implement to declare one |
| `dispatch.message.applied_kinds` | The kinds applied instead — `All` |

The event is emitted on the trace rather than the log because classification happens on the dispatch
path with no logger in scope. If you did not intend the message to receive every middleware, have it
implement `IDispatchAction`, `IDispatchEvent`, or `IDispatchDocument`.

## Short-Circuiting

Return early without calling `next()` to skip downstream processing:

```csharp
public class CachingMiddleware : IDispatchMiddleware
{
    public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Cache;

    public async ValueTask<IMessageResult> InvokeAsync(
        IDispatchMessage message,
        IMessageContext context,
        DispatchRequestDelegate next,
        CancellationToken ct)
    {
        var cacheKey = BuildCacheKey(message);
        var cached = await _cache.GetAsync<IMessageResult>(cacheKey);

        if (cached is not null)
        {
            return cached; // Short-circuit: don't call next
        }

        var result = await next(message, context, ct);

        if (result.Succeeded)
        {
            await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));
        }

        return result;
    }
}
```

## Conditional Middleware

Apply middleware based on runtime conditions:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.ConfigurePipeline("Default", pipeline =>
    {
        // Only in development
        pipeline.UseWhen<DebugMiddleware>(
            sp => sp.GetRequiredService<IHostEnvironment>().IsDevelopment());

        // Only when audit feature is enabled
        pipeline.UseWhen<AuditMiddleware>(
            sp => sp.GetRequiredService<IConfiguration>()
                .GetValue<bool>("Features:AuditEnabled"));

        // Based on configuration
        pipeline.UseWhen<FeatureMiddleware>(
            sp => sp.GetRequiredService<IConfiguration>()
                .GetValue<bool>("Features:NewFeature"));
    });
});
```

## Automatic Default Pipeline

When you register middleware using `UseMiddleware<T>()` without explicitly calling `ConfigurePipeline()`, Dispatch automatically creates a "Default" pipeline:

```csharp
// This automatically creates a default pipeline
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseMiddleware<LoggingMiddleware>();
    dispatch.UseMiddleware<ValidationMiddleware>();
});
```

Middleware is then ordered by their `Stage` value.

## In This Section

| Topic | Description |
|-------|-------------|
| [Pipeline Profiles](profiles.md) | Reusable middleware configurations for different scenarios |

## See Also

- [Middleware](../middleware/index.md) - Built-in and custom middleware components
- [Handlers](../handlers.md) - Action and event handlers that the pipeline wraps
- [Performance](../performance/index.md) - Optimize pipeline throughput
