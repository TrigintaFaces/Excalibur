---
sidebar_position: 7
title: Dependency Injection
description: Configure services, handlers, and lifetimes with Dispatch
---

# Dependency Injection

Dispatch integrates with Microsoft.Extensions.DependencyInjection, providing automatic handler discovery, middleware registration, and flexible configuration options.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [Microsoft.Extensions.DependencyInjection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)

## Basic Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

// Discover handlers from current assembly (recommended pattern)
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});
```

## Registration Methods

### AddDispatch (Recommended)

The primary registration method with fluent configuration:

```csharp
// Simple: Basic registration with no configuration
builder.Services.AddDispatch();

// With configuration (recommended)
builder.Services.AddDispatch(dispatch =>
{
    // Handlers are auto-registered with DI container (Transient by default)
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Configure middleware and pipelines
    dispatch.UseMiddleware<LoggingMiddleware>();
    dispatch.UseMiddleware<ValidationMiddleware>();

    // Configure options
    dispatch.ConfigureOptions<DispatchOptions>(options =>
    {
        options.DefaultTimeout = TimeSpan.FromSeconds(30);
    });
});
```

### Automatic Handler DI Registration

When using `AddHandlersFromAssembly`, handlers are **automatically registered with the DI container**. You no longer need separate registrations:

```csharp
// All handler types are scanned and registered automatically:
// - IDispatchHandler<>, IActionHandler<>, IActionHandler<,>
// - IEventHandler<>, IDocumentHandler<>
// - IStreamingDocumentHandler<,>, IStreamConsumerHandler<>
// - IStreamTransformHandler<,>, IProgressDocumentHandler<>

builder.Services.AddDispatch(dispatch =>
{
    // This single call registers handlers with both Dispatch AND the DI container
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// No longer needed - handlers are auto-registered:
// builder.Services.AddScoped<CreateOrderHandler>(); // REMOVED
```

:::caution The two scanning entry points cover different interfaces

The nine interfaces above are what `dispatch.AddHandlersFromAssembly(...)` — the builder overload — scans.
The `services.AddDispatch(params Assembly[])` overload shown under **Multiple Assemblies** is a different
code path and scans only four: `IActionHandler<>`, `IActionHandler<,>`, `IEventHandler<>` and
`IDocumentHandler<>`. Streaming, stream-transform, stream-consumer and progress handlers are **not**
registered by that overload. Use the builder form if you rely on those.

:::

### Customizing Handler Lifetime

Control handler service lifetime with optional parameters:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    // Default: Transient lifetime
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Custom lifetime — only needed if you want the handler INSTANCE tied to the scope.
    // Depending on scoped services does not require this; see Handler Lifetimes below.
    dispatch.AddHandlersFromAssembly(
        typeof(Infrastructure).Assembly,
        lifetime: ServiceLifetime.Scoped);

    // Register nothing from this assembly — neither the concrete handler types nor their
    // handler interfaces. Use this when you register every handler yourself; the call still
    // tells Dispatch that you own handler registration, so it does not fall back to scanning
    // the entry assembly for you.
    dispatch.AddHandlersFromAssembly(
        typeof(Legacy).Assembly,
        registerWithContainer: false);
});
```

## Handler Lifetimes

By default, handlers are registered as **transient**.

You do not need to change this to depend on scoped services. Dispatch decides whether a
dependency-injection scope is required by inspecting the handler's constructor dependency graph, not by
looking at the lifetime you registered it with — so a transient handler that depends on `IUnitOfWork`
still gets a scope, in every host. Register a lifetime explicitly only when you want one for a reason of
your own; the default exists so the framework does not impose the most restrictive option on a consumer
who expressed no preference.

### Lifetime Guidelines

| Lifetime | Use When |
|----------|----------|
| **Transient** *(default)* | You have no specific requirement — this is the right choice for most handlers, including ones with scoped dependencies |
| **Scoped** | You want the handler instance itself tied to the scope, e.g. it holds per-request state of its own |
| **Singleton** | Handler is thread-safe, holds no instance state, and has **no scoped dependencies**. A singleton handler that depends on a scoped service is a captive dependency — the scoped service becomes effectively singleton — and Dispatch does not detect it. Use `ValidateScopes` when building your provider if you want the container to catch this. |

:::note Handlers with scoped dependencies are fully supported
A handler whose dependencies reach a **scoped** service is resolved from a dependency-injection
**scope**, never the root container — whatever lifetime the handler itself is registered with, and
including the context-less `dispatcher.DispatchAsync(message, ct)` overload.

- **ASP.NET Core:** the handler shares the **active request scope** (the same `IUnitOfWork` / `IDb` /
  `DbContext` as the rest of the request). This is wired automatically by
  `WebApplicationBuilder.AddDispatch(...)`. If you compose Dispatch through a different entry point
  (for example `services.AddExcalibur(...)`), call `services.AddDispatchAmbientScope()` once to share
  the request scope.
- **Workers / console / serverless:** each dispatch gets a **fresh scope** that is disposed when the
  handler completes.

A handler that reaches no scoped dependency does **not** pay for a scope. That determination is made
once per handler type, not per dispatch.
:::

:::note Your registered lifetime is honoured
Dispatch resolves handlers with the lifetime you registered. `Scoped` gives you one instance per scope
and `Singleton` gives you one for the process, exactly as `Microsoft.Extensions.DependencyInjection`
defines them.

There is one optimisation, and it applies only to `Transient`. A handler registered `Transient` that has
no constructor dependencies and no instance state is indistinguishable from a shared instance — nothing
can observe the difference except reference identity — so Dispatch may reuse one instance rather than
activating a new one per dispatch. `Transient` is also the default, so a handler that got it by default
has had no preference overridden. Disable this with:

```csharp
dispatch.WithOptions(o =>
    o.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton = false);
```

Dispatch never applies this to `Scoped` or `Singleton`. Those are deliberate departures from the default,
and `Scoped` in particular is what you choose when per-request isolation matters.

If you register a handler `Scoped` or `Singleton` that has no dependencies and no state, Dispatch logs
one `Information` message naming it (event ID `40908`), once per handler type, mentioning that
`Transient` would be cheaper. It is advisory — your registration is still honoured — and exists so a
lifetime chosen by habit is easy to spot.
:::

:::note All handlers for one published event share a scope
When an event is published to several handlers, those handlers observe the **same** scoped instances —
one scope per published event, not one per handler. Two handlers for the same event see the same
`IUnitOfWork`, so a single event is handled as a single unit of work. Separate publishes get separate
scopes.

This is deliberate, and it is not the same property as fault isolation. `PublishAsync` **does** isolate
faults: every handler is started and awaited, and one handler throwing does not abandon the others. It
does **not** isolate state. A handler that fails after leaving a shared `DbContext` in a broken state
hands that state to its siblings, so treat sibling handlers as sharing a transaction, not as independent
units.
:::

## Service Injection

Inject services into handlers through constructor injection:

```csharp
public class CreateOrderHandler : IActionHandler<CreateOrderAction, Guid>
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<CreateOrderHandler> _logger;
    private readonly IMessageContextAccessor _contextAccessor;

    public CreateOrderHandler(
        IOrderRepository repository,
        ILogger<CreateOrderHandler> logger,
        IMessageContextAccessor contextAccessor)
    {
        _repository = repository;
        _logger = logger;
        _contextAccessor = contextAccessor;
    }

    public async Task<Guid> HandleAsync(
        CreateOrderAction action,
        CancellationToken cancellationToken)
    {
        var correlationId = _contextAccessor.MessageContext?.CorrelationId;
        _logger.LogInformation(
            "Creating order for {CustomerId}, CorrelationId: {CorrelationId}",
            action.CustomerId,
            correlationId);

        return await _repository.CreateAsync(action, cancellationToken);
    }
}
```

## Multiple Assemblies

Register handlers from multiple assemblies:

```csharp
builder.Services.AddDispatch(
    typeof(DomainHandlers).Assembly,
    typeof(InfrastructureHandlers).Assembly,
    typeof(IntegrationHandlers).Assembly);
```

Or with the builder pattern using the `params Assembly[]` overload:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    // Single call with multiple assemblies (params overload)
    dispatch.AddHandlersFromAssembly(
        typeof(DomainHandlers).Assembly,
        typeof(InfrastructureHandlers).Assembly,
        typeof(IntegrationHandlers).Assembly);
});
```

## Manual Registration

For fine-grained control, register handlers manually:

```csharp
// Auto-discover most handlers
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});

// Override specific handlers
builder.Services.AddScoped<IActionHandler<CreateOrderAction, Guid>, CustomCreateOrderHandler>();

// Register with specific lifetime
builder.Services.AddSingleton<IActionHandler<GetConfigAction, Config>, CachedConfigHandler>();
```

## Middleware Registration

Register custom middleware using the configuration builder:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Configure middleware via builder
    dispatch.ConfigurePipeline("Default", pipeline =>
    {
        pipeline.Use<LoggingMiddleware>();
        pipeline.Use<ValidationMiddleware>();
        pipeline.Use<AuthorizationMiddleware>();
    });
});
```

## Decorator Pattern

Wrap handlers with cross-cutting concerns using decorators. `Decorate<TService, TDecorator>()` is built
into Excalibur.Dispatch — no additional package is required.

It **throws** if the service type has more than one registration, rather than silently decorating only
the last one. An ambiguous decoration is therefore a startup error you can see, not a runtime behaviour
you have to discover.

```csharp
// Register the handler. CreateOrderHandler returns a Guid, so it implements the
// two-argument IActionHandler<TAction, TResult> — the decorators must match that shape.
builder.Services.AddTransient<IActionHandler<CreateOrderAction, Guid>, CreateOrderHandler>();

// Decorate with logging
builder.Services.Decorate<
    IActionHandler<CreateOrderAction, Guid>,
    LoggingHandlerDecorator<CreateOrderAction, Guid>>();

// Decorate with retry
builder.Services.Decorate<
    IActionHandler<CreateOrderAction, Guid>,
    RetryHandlerDecorator<CreateOrderAction, Guid>>();
```

## Keyed Services

Use keyed services for named implementations:

```csharp
// Register keyed handlers
builder.Services.AddKeyedScoped<IOrderProcessor, StandardOrderProcessor>("standard");
builder.Services.AddKeyedScoped<IOrderProcessor, ExpressOrderProcessor>("express");

// Inject by key
public class OrderHandler
{
    public OrderHandler(
        [FromKeyedServices("express")] IOrderProcessor expressProcessor)
    {
        // ...
    }
}
```

:::note Keyed message handlers are now wired correctly
`AddDispatch()` runs a handler-lifetime analysis over the service collection. It now reads the keyed
implementation type (`KeyedImplementationType`) for keyed descriptors under `IsKeyedService`, so
**keyed message handlers** — e.g. `AddKeyedScoped<IActionHandler<MyAction>, MyHandler>("key")` — are
correctly discovered, lifetime-promoted, and dispatched, with their **service key preserved**.

Previously these keyed handlers were **silently never wired** on the .NET 9 / .NET 10 runtime
(Microsoft.Extensions.DependencyInjection 9.x/10.x): `ServiceDescriptor.ImplementationType` returns
`null` for a keyed descriptor, so the handler was skipped during analysis — never indexed, promoted,
or dispatched, and **no error was raised** (the handler just never executed). On the older
Microsoft.Extensions.DependencyInjection 8.x runtime the same path threw an `InvalidOperationException`
instead. The fix corrects both: keyed handlers work on every runtime. Ordering between `AddDispatch()`
and your keyed registrations does not matter.
:::

:::tip Non-keyed aliases for core stores

Excalibur subsystem packages (EventSourcing, Outbox, Inbox, Saga, LeaderElection, Persistence) register their primary stores as keyed singletons under `"default"`. **Non-keyed convenience aliases are registered automatically**, so you can inject `IEventStore`, `IOutboxStore`, `ISagaStore`, etc. directly — no `[FromKeyedServices]` attribute required:

```csharp
// Just inject the store directly — the non-keyed alias forwards to keyed "default"
public class OrderService(IEventStore eventStore, IOutboxStore outboxStore)
{
    // ...
}
```

Use `[FromKeyedServices("key")]` only when you register multiple named implementations of the same interface.
:::

## Startup Prerequisite Validation

Every Excalibur subsystem registers an internal `IHostedService` prerequisite validator that runs during `IHost.StartAsync`. If you call an `Add*` method (e.g., `AddEventSourcing(...)`) without selecting a concrete provider (e.g., `.UseSqlServer(...)`), the host fails immediately with an actionable error message instead of failing later at first use:

```
Excalibur event sourcing is missing the required IEventStore implementation.
Call a provider extension inside AddEventSourcing(...) — for example
es => es.UseSqlServer(sql => sql.ConnectionString(...)),
es => es.UsePostgres(...), or es => es.UseCosmosDb(...)
— before host startup.
```

Prerequisite validators are registered for:

| Subsystem | Required Interface | Add Method |
|---|---|---|
| Event Sourcing | `IEventStore` | `AddEventSourcing(...)` |
| Outbox | `IOutboxStore` | `AddOutbox(...)` |
| Inbox | `IInboxStore` | `AddInbox(...)` |
| Saga | `ISagaStore` | `AddSagas(...)` |
| Leader Election | `ILeaderElection` | `AddLeaderElection(...)` |
| Persistence | `IPersistenceProvider` | `AddPersistence(...)` |

These validators are AOT-safe (no reflection) and invisible to consumers — they are registered transparently by each subsystem's DI extension.

### Host-less containers must trigger the gates explicitly

Prerequisite validators — and the fail-fast durability gates (audit-store, key, grant, schedule, and separation-of-duties checks) registered with `ValidateOnStart()` — run from the host's startup validation, which only fires when the application calls `IHost.StartAsync`. A consumer who builds an `IServiceProvider` manually and resolves services directly, without ever starting a host (a custom serverless runtime, a manual `BuildServiceProvider()`, a unit of work that never builds a host), never triggers them, so those fail-fast guarantees are silently inert.

Such a consumer **must** call `ValidateStartupGates()` once, immediately after building the provider:

```csharp
services.AddLogging();   // required — Dispatch resolves ILogger<T>; a host supplies this for you
var provider = services.BuildServiceProvider();

// Throws OptionsValidationException when a ValidateOnStart() gate fails,
// InvalidOperationException when a prerequisite is missing.
provider.ValidateStartupGates();
```

`AddLogging()` is not optional on this path. `WebApplication.CreateBuilder` and the generic host register
logging for you, which is why the ASP.NET snippets above omit it; a bare `ServiceCollection` does not, and
resolving `IDispatcher` without it throws `InvalidOperationException: Unable to resolve service for type
'ILogger<...>'`.

`ValidateStartupGates()` runs both families of gate. For the `ValidateOnStart()` gates it invokes the framework's own startup validator, so *every* registered gate is covered — one added later is picked up automatically. It then runs every prerequisite validator in the container, which is what surfaces the missing-provider errors shown above. It no-ops when neither family is registered, returns the same provider for chaining, and is safe to call once after build.

It starts no hosted services. Outbox processors, leader election, and other background work stay unstarted — a container you never intended to run as a host does not acquire one by being asked whether it is wired correctly.

**One class of check it cannot run.** A gate that must perform I/O to reach a verdict — probing a remote secret mount, reading a physical table schema — cannot run from a synchronous method without blocking, so those stay host-only. Each carries its own fail-closed check on the path it protects, so a host-less consumer fails on first use of that path rather than proceeding on an unverified assumption:

| Check | Host-less behavior |
|---|---|
| Inbox store physical schema | Verified per store on first use |
| Vault key-suspension mount reachability | Suspension enforcement fails closed when the mount is unreachable |

Hosts that build their provider through the generic host and call `StartAsync` — including Azure Functions and AWS Lambda on the isolated-worker model — already run these gates at start and do not need this call. It is for the genuinely host-less path only.

If you write your own prerequisite check, implement `IStartupPrerequisiteValidator` alongside `IHostedService` and register both, so it fires on the hosted path and the host-less one:

```csharp
services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, MyPrerequisiteValidator>());
services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, MyPrerequisiteValidator>());
```

## Transport and Cross-Cutting Registration

The `AddDispatch()` builder also supports transport and cross-cutting concern registration through extension methods.

**Each of these lives in its own package** and is not available on a bare `Excalibur.Dispatch` install:
`UseObservability` needs `Excalibur.Dispatch.Observability`, `UseResilience` needs
`Excalibur.Dispatch.Resilience.Polly`, `UseCaching` needs `Excalibur.Dispatch.Caching`, `UseSecurity` needs
`Excalibur.Security`, and each transport needs its own transport package. Install the package for the
concern you want; the snippets below will not compile without them.

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Transports (Use prefix — pluggable infrastructure)
    dispatch.UseRabbitMQ(rmq => rmq.HostName("localhost"));
    dispatch.UseKafka(kafka => kafka.BootstrapServers("localhost:9092"));

    // Cross-cutting (Add prefix — additive features)
    dispatch.UseObservability();
    dispatch.UseResilience(res => res.DefaultRetryCount = 3);
    dispatch.UseCaching();
    dispatch.UseSecurity(builder.Configuration);
});
```

See [Configuration](configuration.md) for full builder pattern reference.

## Excalibur Subsystem Registration

The unified `AddExcalibur()` entry point registers Dispatch primitives with sensible defaults:

Feature methods are package-owned: `.AddEventSourcing(...)` comes from `Excalibur.EventSourcing`, `.AddOutbox(...)` from `Excalibur.Outbox`, and `.AddSagas(...)` from `Excalibur.Saga`.

```csharp
// Simple — Dispatch defaults are sufficient
builder.Services.AddExcalibur(excalibur =>
{
    excalibur
        .AddEventSourcing(es => es.UseEventStore<SqlServerEventStore>())
        .AddOutbox(outbox => outbox.UseSqlServer(opts => opts.ConnectionString(connectionString)))
        .AddSagas();
});
```

### Excalibur with Custom Dispatch Configuration

When you need transports, pipeline profiles, or middleware, call `AddDispatch` with a builder action. Either order works, and a handler you register yourself is honored whether you register it before or after `AddDispatch`: assembly scanning yields to a registration you already made, and a registration you make afterwards takes precedence over the scanned one. Handlers you register for an *event* are not subject to that — every handler registered for an event runs, whether it came from scanning, from you, or from both:

```csharp
// 1. Configure Dispatch with transports and middleware
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseRabbitMQ(rmq => rmq.HostName("localhost"));
    dispatch.UseObservability();
    dispatch.ConfigurePipeline("default", p => p.UseValidation());
});

// 2. Configure Excalibur subsystems
builder.Services.AddExcalibur(excalibur =>
{
    excalibur
        .AddEventSourcing(es => es.UseEventStore<SqlServerEventStore>())
        .AddOutbox(outbox => outbox.UseSqlServer(opts => opts.ConnectionString(connectionString)));
});
```

## Common Services

Dispatch registers these services automatically:

| Service | Lifetime | Purpose |
|---------|----------|---------|
| `IDispatcher` | Singleton | Message dispatching (resolves scoped handlers from the active or a fresh scope) |
| `IMessageContextAccessor` | Singleton | Access current message context (ambient via AsyncLocal) |
| `IMessageContextFactory` | Singleton | Create new contexts |
| `IPipelineProfileRegistry` | Singleton | Pipeline profile lookup |

## Testing Configuration

Override services for testing:

```csharp
public class TestFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace real services with test doubles
            services.RemoveAll<IOrderRepository>();
            services.AddScoped<IOrderRepository, InMemoryOrderRepository>();

            // Replace external services
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
        });
    }
}
```

## What's Next

You've covered all the core concepts. Start building with Dispatch:

- [Handlers](../handlers.md) - Advanced handler patterns
- [Pipeline](../pipeline/index.md) - Middleware and behaviors
- [Transports](../transports/index.md) - Configure message transport for production
- [Event Sourcing](../event-sourcing/index.md) - Build event-sourced applications

## See Also

- [Configuration](./configuration.md) — Builder pattern reference, options binding, and environment-specific setup
- [Test Harness](../testing/test-harness.md) — DispatchTestHarness for integration testing with service overrides
- [Middleware](../middleware/index.md) — Register and configure middleware in the DI pipeline
- [Custom Middleware](../middleware/custom.md) — Build your own middleware with constructor-injected services
