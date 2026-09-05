---
sidebar_position: 2
title: Pipeline Profiles
description: Configure reusable pipeline profiles for different message processing scenarios
---

# Pipeline Profiles

Pipeline profiles are reusable middleware configurations that define which middleware to include and in what order for specific processing scenarios. Instead of manually configuring middleware for each use case, select a pre-built profile or create your own.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [pipeline concepts](./index.md) and [middleware](../middleware/index.md)

## Built-in Profiles

Dispatch includes five built-in profiles optimized for common scenarios:

| Profile | Use Case | Middleware Count |
|---------|----------|------------------|
| `default` | Standard message processing | 7 middleware |
| `strict` | External/partner inputs — declares the full middleware set (see the security note below) | 13 middleware |
| `internal-event` | Trusted internal event processing | 5 middleware |
| `batch` | High-throughput batch operations | 3 middleware |
| `hot-path` | Ultra-low-latency message processing | 0 middleware |

## Using Pipeline Profiles

### Select a Profile

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Use the strict profile for external API endpoints.
    // Selecting it does NOT guarantee its middleware run — verify the ones you depend on.
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
        // External commands select the full middleware set — verify the ones you rely on resolve
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

:::info The default profile runs by default
`AddDispatch` selects the `default` profile for the standard dispatch path **without any explicit `ConfigurePipeline`/`UseProfile` call**. You configure a profile only to choose a *different* one (e.g. `strict`).

**Selecting a profile is not the same as activating its middleware.** A profile entry runs only if its type is resolvable from the service provider, and the framework does not register the profile's middleware types for you — each has its own registration call (see the table below). On a zero-config `AddDispatch`, **none** of the `default` profile's seven entries materialize.

Every profile entry declares a **criticality**, and that is what decides the outcome when the entry cannot be constructed. An `Optional` entry is skipped and emits a debug log (`InvokerMiddlewareSkipped`, event ID 10024) rather than failing the dispatch. A `Required` entry fails the build instead, naming what is missing. All seven `default` entries are `Optional`, so a zero-config `AddDispatch` starts cleanly with an empty pipeline.

An entry can fail to materialize in two ways, and criticality governs both alike: the middleware was never registered, **or** it was registered but its own dependency was not — `OutboxStagingMiddleware`, for example, resolves once you add the outbox.
:::

:::info Which entries are enforced, and which are best-effort

`Optional` entries are skipped — silently, at Debug — when they cannot be constructed, **including when you
have registered the middleware but not the service it depends on.** The pipeline builds successfully and
reports nothing at Warning or above. Treat an `Optional` entry as best-effort: it is in the pipeline only
if everything it needs is present.

`Required` entries are not best-effort. If one cannot be materialized, `Build()` throws and names it, so a
host missing a dependency fails to start rather than serving traffic without the protection.

**The `strict` profile marks its five security entries `Required`** — `ThrottlingMiddleware`,
`AuthenticationMiddleware`, `TenantIdentityMiddleware`, `InputSanitizationMiddleware`, and
`AuthorizationMiddleware`. A host that selects `strict` without registering an `IAuthorizationService`
now **fails at startup** naming that service; it can no longer build a pipeline in which authorization is
silently absent. The remaining eight `strict` entries, and every entry in `default`, `internal-event`, and
`batch`, are `Optional`.

You therefore no longer need a composition-root resolve check for the security middleware `strict`
declares — the build is the check. For an `Optional` entry you depend on, verify it in two steps:

```csharp
// 1. Register the middleware type itself — the container does not do this for you.
builder.Services.AddSingleton<AuditLoggingMiddleware>();

// 2. Resolve it at startup. This bypasses the pipeline builder, so nothing is swallowed:
//    it throws naming the missing dependency, and succeeds once it is wired.
_ = provider.GetRequiredService<AuditLoggingMiddleware>();
```

**Both steps are required, and the order matters.** Resolving without step 1 throws *"No service for type
… has been registered"* whether or not the dependency is wired — a check that always fails tells you
nothing, and you will end up ignoring it. With step 1 in place the resolve discriminates.

:::

:::warning `Required` proves the middleware is built — not that it runs for every message

The build-time check confirms a `Required` middleware can be **materialized**. It does not widen the
message kinds that middleware applies to.

A `Required` middleware whose `[AppliesTo(…)]` is narrower than the profile's supported kinds is skipped
for the kinds it excludes, and the build still succeeds — the check and the filter ask different
questions.

On the `strict` profile registered by `AddDispatch`, every `Required` middleware currently applies to
events:

| Middleware | Applies to |
| --- | --- |
| `AuthenticationMiddleware` | `Action \| Event` |
| `AuthorizationMiddleware` | `Action \| Event` |
| `ThrottlingMiddleware` | `Action \| Event` |
| `InputSanitizationMiddleware` | `Action \| Event` |
| `TenantIdentityMiddleware` | `All` |

So events dispatched through the registered `strict` profile **are** authenticated and authorized.

The distinction still matters for any middleware you add yourself: a successful build proves a
`Required` middleware could be **materialized**, not that it runs for the message kinds you dispatch.
Check `[AppliesTo(…)]` on middleware you register, and enforce an authorization boundary in the handler
if the middleware guarding it excludes that kind.
:::

:::info Middleware you add explicitly is required by default

Middleware added by an explicit `Use…()` call is treated as **required**: it is an instruction, not a
suggestion. If a required middleware cannot be materialized, `Build()` **fails** and the error names every
unresolved entry and why:

```
Pipeline 'default' cannot be built because 2 required middleware could not be resolved:
  - Excalibur.Dispatch.Middleware.AuthorizationMiddleware: the middleware type itself is not registered. Register it together with the services it depends on - typically by calling the Add... extension method that enables this feature.
  - Excalibur.Dispatch.Middleware.AuditLoggingMiddleware: Unable to resolve service for type 'Excalibur.Dispatch.Observability.ITelemetrySanitizer' while attempting to activate 'Excalibur.Dispatch.Middleware.AuditLoggingMiddleware'.
How to fix: each entry above names what it needs. Register the missing service(s) before building the host, or remove that middleware from the pipeline configuration. Every unresolved entry is listed together so they can be fixed in one pass rather than one build at a time.
```

The failure is at **startup**, not on the first dispatch, so a host that is missing a dependency does not
start and then silently process traffic without it.

The failure path is the same one a `Required` profile entry takes. An explicit `Use…()` call and a
`Required` profile entry are enforced identically; the difference is only where the entry was declared.
:::

**Declared middleware order**, and what registers each entry. The framework does not register these for
you — on a zero-config `AddDispatch`, none of them materialize:

| # | Middleware | Purpose | Registered by |
|---|---|---|---|
| 1 | `TenantIdentityMiddleware` | Multi-tenancy context | `UseTenantIdentity()` |
| 2 | `ContractVersionCheckMiddleware` | Event/document versioning | `AddUpcastingMessageBusDecorator()` |
| 3 | `ValidationMiddleware` | Input validation | `UseValidationStack()`, `UseDevelopmentMiddleware()`, or `UseFullMiddleware()` |
| 4 | `TimeoutMiddleware` | Processing timeouts | register the type yourself |
| 5 | `TransactionMiddleware` | Transaction management | register the type yourself |
| 6 | `OutboxStagingMiddleware` | Outbox pattern support | `AddUpcastingMessageBusDecorator()` |
| 7 | `MetricsLoggingMiddleware` | Observability | register the type yourself |

> `AuthorizationMiddleware` is **not** part of the `default` profile, and every `default` entry is
> `Optional`. Selecting `default` declares no security boundary; if you need authorization, select `strict`
> or add it with an explicit `UseAuthorization()` call.

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
        pipeline.Use<TimeoutMiddleware>();
        pipeline.Use<TransactionMiddleware>();
        pipeline.Use<OutboxStagingMiddleware>();
        pipeline.Use<MetricsLoggingMiddleware>();
    });
});
```

### Strict Profile

The security-oriented profile for external/partner inputs. It declares thirteen middleware, of which
**five are `Required`**: the pipeline refuses to build without rate limiting, authentication, tenant
identity, input sanitization, and authorization. The other eight are `Optional` — infrastructure whose
absence degrades behavior without removing a boundary you asked for.

**Declared middleware order.** `Required` entries fail the build when they cannot be materialized;
`Optional` entries are skipped and logged at Debug.

| # | Middleware | Purpose | Criticality |
|---|---|---|---|
| 1 | `ThrottlingMiddleware` | Throttle external requests | **Required** |
| 2 | `AuthenticationMiddleware` | Verify identity | **Required** |
| 3 | `TenantIdentityMiddleware` | Multi-tenancy context | **Required** |
| 4 | `InputSanitizationMiddleware` | Sanitize inputs | **Required** |
| 5 | `ValidationMiddleware` | Input validation | Optional |
| 6 | `AuthorizationMiddleware` | Permission checks | **Required** |
| 7 | `ContractVersionCheckMiddleware` | Versioning | Optional |
| 8 | `TimeoutMiddleware` | Processing timeouts | Optional |
| 9 | `CircuitBreakerMiddleware` | Resilience | Optional |
| 10 | `TransactionMiddleware` | Transaction management | Optional |
| 11 | `OutboxStagingMiddleware` | Outbox pattern support | Optional |
| 12 | `AuditLoggingMiddleware` | Audit trail | Optional |
| 13 | `MetricsLoggingMiddleware` | Observability | Optional |

The five `Required` entries are the ones that **enforce** a boundary rather than record one. Note that
authentication and authorization apply to actions only — see the warning above about events.

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

**Declared middleware order** (each entry runs only if it and its dependencies are registered):
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

**Declared middleware order** (each entry runs only if it and its dependencies are registered):
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

Zero-middleware profile for ultra-low-latency message processing. Correlation and context management is handled directly in the Dispatcher, allowing maximum throughput at the framework's lowest allocation floor (96 B per dispatch, of which 72 B is the ambient-context `ExecutionContext` copy and scales with your application's async-local density -- see the [benchmarks](/docs/performance/competitor-comparison)).

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

:::info A custom profile can mark an entry as required

A profile declares its middleware as `IReadOnlyList<MiddlewareEntry>`, and each entry carries a
`MiddlewareCriticality`. A profile you write yourself can therefore state that an entry **must** be
present, exactly as the built-in `strict` profile does — you no longer have to move security middleware
out of a profile to get it enforced.

- `MiddlewareCriticality.Required` — the pipeline fails to build if the entry cannot be materialized,
  naming the middleware and the service that is missing.
- `MiddlewareCriticality.Optional` — the entry is skipped and logged at Debug, and the pipeline builds
  without it.

**A `MiddlewareEntry` constructed without a criticality is `Required`.** Naming a middleware and omitting
how much protection you want is not the same as asking for none, so the default is the safe one. State
`Optional` explicitly when you genuinely want a best-effort entry.

**If you implement `IPipelineProfile` yourself, note that the constructor default only applies to entries
you actually construct.** `MiddlewareEntry` is a struct, so a default value — or an unfilled slot in an
array you sized up front — has no middleware type and a criticality of `MiddlewareCriticality.Unspecified`.
Building a pipeline from such an entry fails, naming the profile and the position, rather than resolving it
to a criticality you never stated. Never state `Unspecified` yourself; it exists so that forgetting to state
one is loud instead of silent.

Explicit `Use…()` calls remain available and are required by default; use them when the middleware is not
part of a reusable profile.

:::

The simplest way to build a profile is `PipelineProfile`, which takes the entries in order:

```csharp
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Middleware.Auth;

var profile = new PipelineProfile("my-custom-profile", MessageKinds.All)
{
    Description = "Custom profile for my application",
};

// Required: if IAuthorizationService is not registered, the build fails and names
// that service, instead of producing a pipeline without authorization.
profile.AddMiddleware<AuthorizationMiddleware>(1, MiddlewareCriticality.Required);

// Optional: skipped and logged at Debug when it cannot be materialized.
profile.AddMiddleware<CustomLoggingMiddleware>(2, MiddlewareCriticality.Optional);
profile.AddMiddleware<ValidationMiddleware>(3, MiddlewareCriticality.Optional);
```

To implement `IPipelineProfile` directly, expose the entries and their criticality:

```csharp
public class MyCustomProfile : IPipelineProfile
{
    public string Name => "my-custom-profile";
    public string Description => "Custom profile for my application";
    public bool IsStrict => false;
    public MessageKinds SupportedMessageKinds => MessageKinds.All;

    public IReadOnlyList<MiddlewareEntry> MiddlewareEntries { get; }

    public MyCustomProfile()
    {
        var entries = new List<MiddlewareEntry>
        {
            // Enforced: a missing IAuthorizationService fails the build.
            new(typeof(AuthorizationMiddleware), MiddlewareCriticality.Required),

            // Best-effort: skipped and logged if they cannot be materialized.
            new(typeof(CustomLoggingMiddleware), MiddlewareCriticality.Optional),
            new(typeof(MetricsMiddleware), MiddlewareCriticality.Optional),
        };
        MiddlewareEntries = entries.AsReadOnly();
    }

    public bool IsCompatible(IDispatchMessage message) => true;
}
```

Check the middleware's `[AppliesTo]` message kinds against your profile's `SupportedMessageKinds`. A
`Required` entry that does not apply to a message kind your profile accepts still passes the build-time
check, and is still filtered out for that kind.

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
// External endpoints: always use strict — and verify its security middleware resolve,
// because selecting the profile alone does not guarantee they run
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
    /// Gets the ordered middleware entries, each carrying whether the built
    /// pipeline may omit it. This is the profile's only middleware declaration.
    /// </summary>
    IReadOnlyList<MiddlewareEntry> MiddlewareEntries { get; }

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

    /// <summary>
    /// Gets middleware applicable to the message kind and enabled features.
    /// </summary>
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
