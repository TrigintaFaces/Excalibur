---
sidebar_position: 2
title: Pipeline Profiles
description: Configure reusable pipeline profiles for different message processing scenarios
---

# Pipeline Profiles

Pipeline profiles are reusable middleware configurations that define which middleware to include and in what order for specific processing scenarios. Instead of manually configuring middleware for each use case, select a pre-built profile or create your own.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [pipeline concepts](./index.md) and [middleware](../middleware/index.md)

## Built-in Profiles

Dispatch includes five built-in profiles optimized for common scenarios:

| Profile | Use Case | Middleware Count |
|---------|----------|------------------|
| `default` | Standard message processing | 8 middleware |
| `strict` | External/partner inputs with full security | 13 middleware |
| `internal-event` | Trusted internal event processing | 5 middleware |
| `batch` | High-throughput batch operations | 3 middleware |
| `hot-path` | Ultra-low-latency message processing | 0 middleware |

## Using Pipeline Profiles

### Select a Profile

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Use the strict profile for external API endpoints
    dispatch.ConfigurePipeline("Default", pipeline =>
    {
        pipeline.UseProfile("strict");
    });
});
```

### Profile per Message Type

Configure different profiles for different message types:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    dispatch.ConfigurePipeline("Default", pipeline =>
    {
        // External commands get full security pipeline
        pipeline.UseProfile("strict")
            .ForMessageKinds(MessageKinds.Action);

        // Internal events use lightweight pipeline
        pipeline.UseProfile("internal-event")
            .ForMessageKinds(MessageKinds.Event);
    });
});
```

## Profile Details

### Default Profile

The standard pipeline profile with canonical middleware ordering. Suitable for most use cases.

**Middleware Order:**
1. `TenantIdentityMiddleware` - Multi-tenancy context
2. `ContractVersionCheckMiddleware` - Event/document versioning
3. `ValidationMiddleware` - Input validation
4. `AuthorizationMiddleware` - Permission checks
5. `TimeoutMiddleware` - Processing timeouts
6. `TransactionMiddleware` - Transaction management
7. `OutboxStagingMiddleware` - Outbox pattern support
8. `MetricsLoggingMiddleware` - Observability

```csharp
// Equivalent to:
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    dispatch.ConfigurePipeline("Default", pipeline =>
    {
        pipeline.Use<TenantIdentityMiddleware>();
        pipeline.Use<ContractVersionCheckMiddleware>();
        pipeline.Use<ValidationMiddleware>();
        pipeline.Use<AuthorizationMiddleware>();
        pipeline.Use<TimeoutMiddleware>();
        pipeline.Use<TransactionMiddleware>();
        pipeline.Use<OutboxStagingMiddleware>();
        pipeline.Use<MetricsLoggingMiddleware>();
    });
});
```

### Strict Profile

Full security pipeline for external/partner inputs. Includes rate limiting, authentication, input sanitization, and comprehensive audit logging.

**Middleware Order:**
1. `RateLimitingMiddleware` - Throttle external requests
2. `AuthenticationMiddleware` - Verify identity
3. `TenantIdentityMiddleware` - Multi-tenancy context
4. `InputSanitizationMiddleware` - Sanitize inputs
5. `ValidationMiddleware` - Input validation
6. `AuthorizationMiddleware` - Permission checks
7. `ContractVersionCheckMiddleware` - Versioning
8. `TimeoutMiddleware` - Processing timeouts
9. `CircuitBreakerMiddleware` - Resilience
10. `TransactionMiddleware` - Transaction management
11. `OutboxStagingMiddleware` - Outbox pattern support
12. `AuditLoggingMiddleware` - Audit trail
13. `MetricsLoggingMiddleware` - Observability

**When to Use:**
- Public API endpoints
- Partner integrations
- Untrusted external inputs
- Compliance-sensitive operations

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    dispatch.ConfigurePipeline("Default", pipeline =>
    {
        pipeline.UseProfile("strict");
    });
});
```

### Internal Event Profile

Lightweight pipeline for internal event processing between trusted services. Skips authentication and authorization since events originate from trusted sources.

**Middleware Order:**
1. `TenantIdentityMiddleware` - Multi-tenancy context
2. `ContractVersionCheckMiddleware` - Event versioning
3. `TimeoutMiddleware` - Processing timeouts
4. `OutboxStagingMiddleware` - Outbox pattern support
5. `MetricsLoggingMiddleware` - Observability

**When to Use:**
- Domain event handlers
- Event-driven sagas
- Internal service communication

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    dispatch.ConfigurePipeline("Default", pipeline =>
    {
        pipeline.UseProfile("internal-event")
            .ForMessageKinds(MessageKinds.Event);
    });
});
```

### Batch Profile

Optimized for high-throughput batch processing and data backfill operations. Includes batching and bulk optimization middleware.

**Middleware Order:**
1. `BatchingMiddleware` - Group messages for bulk processing
2. `BulkOptimizationMiddleware` - Optimize bulk operations
3. `MetricsLoggingMiddleware` - Observability

**When to Use:**
- Data imports/exports
- Backfill operations
- ETL pipelines
- High-volume background processing

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    dispatch.ConfigurePipeline("Default", pipeline =>
    {
        pipeline.UseProfile("batch");
    });
});
```

### Hot-Path Profile

Zero-middleware profile for ultra-low-latency message processing. Correlation and context management is handled directly in the Dispatcher, allowing maximum throughput with zero allocation overhead.

**Middleware:** None (handled at Dispatcher level)

**When to Use:**
- High-frequency trading systems
- Real-time event streaming
- Performance-critical paths
- Scenarios where microseconds matter

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    dispatch.ConfigurePipeline("Default", pipeline =>
    {
        pipeline.UseProfile("hot-path");
    });
});
```

:::warning Performance Trade-off
The hot-path profile bypasses all middleware including validation, authorization, and error handling. Only use for trusted, pre-validated messages where latency is critical.
:::

## Creating Custom Profiles

### Define a Custom Profile

Create profiles tailored to your application's needs:

```csharp
public class MyCustomProfile : IPipelineProfile
{
    public string Name => "my-custom-profile";
    public string Description => "Custom profile for my application";
    public bool IsStrict => false;
    public MessageKinds SupportedMessageKinds => MessageKinds.All;

    public IReadOnlyList<Type> MiddlewareTypes { get; }

    public MyCustomProfile()
    {
        var types = new List<Type>
        {
            typeof(CustomLoggingMiddleware),
            typeof(ValidationMiddleware),
            typeof(CustomAuthorizationMiddleware),
            typeof(MetricsMiddleware)
        };
        MiddlewareTypes = types.AsReadOnly();
    }

    public bool IsCompatible(IDispatchMessage message) => true;

    public IReadOnlyList<Type> GetApplicableMiddleware(MessageKinds messageKind)
        => MiddlewareTypes;

    public IReadOnlyList<Type> GetApplicableMiddleware(
        MessageKinds messageKind,
        IReadOnlySet<DispatchFeatures> enabledFeatures)
        => MiddlewareTypes;
}
```

### Register Custom Profiles

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    dispatch.ConfigurePipeline("Default", pipeline =>
    {
        // Register the custom profile
        pipeline.RegisterProfile<MyCustomProfile>();

        // Use it
        pipeline.UseProfile("my-custom-profile");
    });
});
```

### Extend Built-in Profiles

Add middleware to existing profiles:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    dispatch.ConfigurePipeline("Default", pipeline =>
    {
        pipeline.UseProfile("default");

        // Add additional middleware
        pipeline.Use<CustomAuditMiddleware>();
        pipeline.Use<CustomMetricsMiddleware>();
    });
});
```

## Profile Selection Best Practices

### Match Profile to Context

| Scenario | Recommended Profile |
|----------|---------------------|
| Public REST API | `strict` |
| Internal microservice calls | `default` |
| Domain event handlers | `internal-event` |
| Data migration jobs | `batch` |
| High-frequency sensors | `hot-path` |

### Consider Security Requirements

```csharp
// External endpoints: always use strict
app.MapPost("/api/external/orders", async (CreateOrderCommand cmd, IDispatcher dispatcher) =>
{
    // Strict profile configured for this endpoint
    return await dispatcher.DispatchAsync(cmd);
});

// Internal endpoints: can use default
app.MapPost("/internal/process-event", async (OrderCreatedEvent evt, IDispatcher dispatcher) =>
{
    // Internal event profile
    return await dispatcher.DispatchAsync(evt);
});
```

### Test Profile Performance

```csharp
// Benchmark different profiles
[Benchmark]
public async Task DefaultProfile() =>
    await _dispatcherWithDefault.DispatchAsync(new TestAction());

[Benchmark]
public async Task HotPathProfile() =>
    await _dispatcherWithHotPath.DispatchAsync(new TestAction());
```

## IPipelineProfile Interface

The `IPipelineProfile` interface defines the contract for pipeline profiles:

```csharp
public interface IPipelineProfile
{
    /// <summary>
    /// Gets the unique name of this pipeline profile.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of what this profile is designed for.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the ordered list of middleware types to include.
    /// </summary>
    IReadOnlyList<Type> MiddlewareTypes { get; }

    /// <summary>
    /// Gets whether this profile enforces strict ordering and validation.
    /// </summary>
    bool IsStrict { get; }

    /// <summary>
    /// Gets the message kinds this profile is optimized for.
    /// </summary>
    MessageKinds SupportedMessageKinds { get; }

    /// <summary>
    /// Validates whether a message is compatible with this profile.
    /// </summary>
    bool IsCompatible(IDispatchMessage message);

    /// <summary>
    /// Gets middleware applicable to the specified message kind.
    /// </summary>
    IReadOnlyList<Type> GetApplicableMiddleware(MessageKinds messageKind);

    /// <summary>
    /// Gets middleware applicable to the message kind and enabled features.
    /// </summary>
    IReadOnlyList<Type> GetApplicableMiddleware(
        MessageKinds messageKind,
        IReadOnlySet<DispatchFeatures> enabledFeatures);
}
```

## See Also

- [Pipeline Overview](./index.md) - Middleware pipeline basics and execution model
- [Middleware Overview](../middleware/index.md) - Full middleware reference and built-in middleware catalog
- [Configuration](../core-concepts/configuration.md) - Dispatch configuration options and patterns

## Related Documentation

- [Pipeline Overview](./) - Middleware pipeline basics
- [Middleware](../middleware/index.md) - Middleware reference
- [Configuration](../core-concepts/configuration.md) - Dispatch configuration
