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
| `default` | Standard dispatch; middleware opt-in | 0 middleware |
| `strict` | External/partner inputs — declares the full middleware set (see the security note below) | 13 middleware |
| `internal-event` | Trusted internal event processing | 5 middleware |
| `batch` | Batch processing | 2 middleware |
| `direct` | No middleware stages configured by the factory | 0 middleware |

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
using Excalibur.Dispatch;
using Microsoft.Extensions.DependencyInjection;

public static class ProfileRegistration54
{
    public static void Configure(IServiceCollection services, System.Reflection.Assembly handlerAssembly)
    {
        services.AddDispatch(dispatch =>
        {
            dispatch.AddHandlersFromAssembly(handlerAssembly);

            // Each named builder selects one profile.
            dispatch.ConfigurePipeline("ExternalCommands", pipeline => pipeline
                .UseProfile("strict")
                .ForMessageKinds(MessageKinds.Action));

            dispatch.ConfigurePipeline("InternalEvents", pipeline => pipeline
                .UseProfile("internal-event")
                .ForMessageKinds(MessageKinds.Event));
        });
    }
}
```

These are separately named pipeline configurations, not automatic action/event routing. Select the intended pipeline through your application's configured dispatch/routing path; calling `UseProfile` twice on one builder replaces its previous profile entries.

## Profile Details

### Default Profile

**The `default` profile contains no middleware.** A host that configures nothing dispatches and does
nothing else.

:::info Nothing runs unless you ask for it

`AddDispatch` selects the `default` profile for the standard dispatch path without any explicit
`ConfigurePipeline`/`UseProfile` call — but that profile is empty, so selecting it adds no middleware stages. Every
behaviour is yours to add, either by naming middleware on a pipeline or by selecting a profile that
declares some.

This is deliberate, and it follows the rule ASP.NET Core uses: a registration call seats
*infrastructure*, never *behaviour*. Two things drove it. The first is the reported historical configuration differences: the migration fixture reported about
**1,760 ns and 1,720 B per dispatch** between the four-middleware and empty configurations, and
**+346 ns and +448 B** for the configuration with a single semantically-inert stage.
These observations do not isolate middleware cost, verify historical stage-execution parity, or establish current-source costs. The second reason is behavior: two of those four
changed *behaviour*. A timeout can cancel your work on a deadline you never chose, and tenant-identity
resolution runs for a host that never asked to be multi-tenant. Behaviour that arrives because you
called a registration method is behaviour you cannot find in your own code.

To run the middleware this profile used to seat, name them:

```csharp
services.AddDispatch(builder => builder.ConfigurePipeline("Default", pipeline => pipeline
    .ForMessageKinds(MessageKinds.All)
    .Use<TenantIdentityMiddleware>()
    .Use<TimeoutMiddleware>()
    .Use<MetricsLoggingMiddleware>()
    .Use<OutboxStagingMiddleware>()));
```

`AddDispatch` still **registers** all four, so naming any of them resolves without further work. Note
`OutboxStagingMiddleware` in particular: if a handler writes to the outbox and the pipeline has no
staging stage, the write has nowhere to go and the dispatch fails with a message naming both things you
need — the `IOutboxStore` registration and the stage.

**Selecting a profile is not the same as activating its middleware.** A profile entry runs only if its
type is resolvable from the service provider. Entries that need infrastructure only you can supply are
reached through the `Use…()` call that registers their service, or through a profile you select
deliberately.

Every profile entry declares a **criticality**, and that is what decides the outcome when the entry cannot be constructed. An `Optional` entry that cannot be materialized during `Build()` is skipped and logged at `Warning` (`InvokerMiddlewareSkipped`, event ID 10024), naming the actual selected profile, middleware type, and reason. Each unresolved entry produces one warning per `Build()` call, not one per dispatch; a resolvable entry produces no skip warning. A `Required` entry fails the build instead, naming what is missing. The default profile has no entries; internal-event and batch entries are `Optional`.

An entry can fail to materialize in two ways, and criticality governs both alike: the middleware was never registered, **or** it was registered but its own dependency was not — `ValidationMiddleware`, for example, requires an `IMessageValidationService`.

This is a build-time materialization check, not a guarantee about later factory results. A scoped factory can resolve during the build probe and return null during a later dispatch; the existing per-dispatch `ScopeResolvedMiddleware` path then skips an `Optional` entry or refuses a `Required` one. The build warning does not certify that later dynamic resolution.
:::

:::info Which entries are enforced, and which are best-effort

`Optional` entries are skipped when they cannot be constructed, **including when you
have registered the middleware but not the service it depends on.** The pipeline builds successfully, and
each skip is logged at `Warning` — so a skipped entry is visible in a default logging configuration rather
than silent. Treat an `Optional` entry as best-effort: it is in the pipeline only
if everything it needs is present.

`Required` entries are not best-effort. If one cannot be materialized, `Build()` throws and names it, so a
host missing a dependency fails to start rather than serving traffic without the protection.

**The `strict` profile marks its five security entries `Required`** — `ThrottlingMiddleware`,
`AuthenticationMiddleware`, `TenantIdentityMiddleware`, `InputSanitizationMiddleware`, and
`AuthorizationMiddleware`. A host that selects `strict` without registering an `IAuthorizationService`
now **fails at startup** naming that service; it can no longer build a pipeline in which authorization is
silently absent. The remaining eight `strict` entries, and every entry in `internal-event` and `batch`, are
`Optional`. The default profile has no entries.

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

**Registered services are not active middleware.** `AddDispatch()` registers
`TenantIdentityMiddleware`, `TimeoutMiddleware`, `MetricsLoggingMiddleware`, and
`OutboxStagingMiddleware` for explicit use. The built-in `default` profile declares none of them.
Add the stages your application requires, or select a profile that declares them.

Outbox writes require both a registered `IOutboxStore` and an active outbox staging stage.
Registering a store alone does not add staging to the empty default profile. A write without staging
fails with guidance identifying both requirements. Calling `UseOutbox()` explicitly also enables
startup validation of the required store.

Explicit `pipeline.Use<T>()` entries are required by default; the call does not itself register the
middleware type. Missing required entries fail pipeline construction. Optional profile entries may
instead be skipped with a warning. See [Creating Custom Profiles](#creating-custom-profiles).

The default profile supplies no authorization boundary. Select `strict` or explicitly configure the
required security middleware.

### Strict Profile

The security-oriented profile for external/partner inputs. It declares thirteen middleware, of which
**five are `Required`**: the pipeline refuses to build without rate limiting, authentication, tenant
identity, input sanitization, and authorization. The other eight are `Optional` — infrastructure whose
absence degrades behavior without removing a boundary you asked for.

**Declared middleware order.** `Required` entries fail the build when they cannot be materialized;
`Optional` entries that cannot be materialized at build time are skipped with the profile/type/reason warning described above; registered, resolvable entries remain quiet.

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

The five `Required` entries are the ones that **enforce** a boundary rather than record one. See the applicability table above for message-kind behavior.

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

Configures batching and metrics middleware for batch-processing scenarios. Measure the resulting workload.

**Declared middleware order** (each entry runs only if it and its dependencies are registered):
1. `UnifiedBatchingMiddleware` - Group messages for bulk processing
2. `MetricsLoggingMiddleware` - Observability

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

### Direct Profile

Zero-middleware profile with correlation and context management handled by the dispatcher. The dated [benchmark observations](../performance/competitor-comparison.md) include a 96 B command fixture result, with 72 B historically attributed to ambient-context publication; that causal attribution is not established by the allocation total alone. Allocation depends on the workload, runtime and ambient async-local state; this is not a universal floor or a maximum-throughput guarantee.

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
        pipeline.UseProfile("direct");
    });
});
```

:::warning Performance Trade-off

The `direct` profile adds no middleware-based validation, authorization or error-handling stages. Dispatcher error semantics still apply. Configure any stages required by the application before selecting this profile.
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
- `MiddlewareCriticality.Optional` — the entry is skipped and logged at Warning, and the pipeline builds
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

Implement the public `IPipelineProfile` contract (shown below), then register that instance and select its name:

```csharp
using Excalibur.Dispatch;
using Microsoft.Extensions.DependencyInjection;

public static class ProfileRegistration388
{
    public static void Configure(IServiceCollection services)
    {
        services.AddDispatch(dispatch =>
        {
            // MyCustomProfile below implements the public IPipelineProfile contract.
            dispatch.RegisterProfile(new MyCustomProfile());
            dispatch.ConfigurePipeline("Custom", pipeline =>
                pipeline.UseProfile("my-custom-profile"));
        });
    }
}
```

To implement `IPipelineProfile` directly, expose the entries and their criticality:

```csharp
using Excalibur.Dispatch;
using Excalibur.Dispatch.Middleware.Auth;
using Excalibur.Dispatch.Middleware.Logging;

public class MyCustomProfile : IPipelineProfile, IPipelineProfileMatcher
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
            new(typeof(LoggingMiddleware), MiddlewareCriticality.Optional),
            new(typeof(MetricsLoggingMiddleware), MiddlewareCriticality.Optional),
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

Register the profile on the dispatch builder, then select it by name inside the pipeline:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Registration takes an instance, and it belongs on the dispatch builder — the pipeline
    // builder only selects profiles, it does not hold the registry.
    dispatch.RegisterProfile(new MyCustomProfile());

    dispatch.ConfigurePipeline("Default", pipeline =>
    {
        pipeline.UseProfile("my-custom-profile");
    });
});
```

`UseProfile(string)` throws when the name is not registered, so register before you select. If you do not
need the profile addressable by name, pass the instance straight to the overload —
`pipeline.UseProfile(new MyCustomProfile())` — and skip the registry entirely.

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
| High-frequency sensors | `direct` |

### Consider Security Requirements

```csharp
using Excalibur.Dispatch;
using Microsoft.AspNetCore.Builder;

public static class ProfileEndpointMappings
{
    public static void Configure(Microsoft.AspNetCore.Routing.IEndpointRouteBuilder app)
    {
        // External endpoints: always use strict — and verify its security middleware resolve,
        // because selecting the profile alone does not guarantee they run
        app.MapPost("/api/external/orders", async (CreateOrderCommand cmd, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            // This call uses the dispatcher's existing configuration; it does not select a profile.
            return await dispatcher.DispatchAsync(cmd, cancellationToken);
        });

        // Internal endpoints: can use default
        app.MapPost("/internal/process-event", async (OrderCreatedEvent evt, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            // This call also does not switch profiles; configure selection outside this endpoint.
            return await dispatcher.DispatchAsync(evt, cancellationToken);
        });
    }
}

public sealed record CreateOrderCommand(Guid OrderId) : IDispatchAction;
public sealed record OrderCreatedEvent(Guid OrderId) : IDispatchEvent;
```

### Test Profile Performance

```csharp
// Benchmark different profiles
[Benchmark]
public async Task DefaultProfile() =>
    await _dispatcherWithDefault.DispatchAsync(new TestAction());

[Benchmark]
public async Task DirectProfile() =>
    await _dispatcherWithDirect.DispatchAsync(new TestAction());
```

## IPipelineProfile Interface

The `IPipelineProfile` interface defines the contract for pipeline profiles:

```csharp
using Excalibur.Dispatch;

// Consume the real public contract; do not declare a second framework interface.
public static class ProfileContractInspection
{
    public static (string Name, string Description, IReadOnlyList<MiddlewareEntry> Entries, bool IsStrict, MessageKinds Kinds)
        Inspect(IPipelineProfile profile)
        => (profile.Name, profile.Description, profile.MiddlewareEntries, profile.IsStrict, profile.SupportedMessageKinds);
}
```

Message matching is a separate contract. Implement `IPipelineProfileMatcher` alongside `IPipelineProfile`
when a profile needs to accept or reject individual messages:

```csharp
using Excalibur.Dispatch;

public static class ProfileMatcherInspection
{
    public static bool Matches(IPipelineProfileMatcher matcher, IDispatchMessage message)
        => matcher.IsCompatible(message);
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
