---
sidebar_position: 7
title: Dependency Injection
description: Configure services, handlers, and lifetimes with Dispatch
---

# Dependency Injection

Dispatch integrates with Microsoft.Extensions.DependencyInjection, providing automatic handler discovery, middleware registration, and flexible configuration options.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Dispatch.Abstractions
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
    // Handlers are auto-registered with DI container (Scoped by default)
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

### Customizing Handler Lifetime

Control handler service lifetime with optional parameters:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    // Default: Scoped lifetime
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Custom lifetime
    dispatch.AddHandlersFromAssembly(
        typeof(Infrastructure).Assembly,
        lifetime: ServiceLifetime.Transient);

    // Skip DI registration (advanced: when you manage registration separately)
    dispatch.AddHandlersFromAssembly(
        typeof(Legacy).Assembly,
        registerWithContainer: false);
});
```

## Handler Lifetimes

By default, handlers are registered as **scoped** (one instance per request).

### Lifetime Guidelines

| Lifetime | Use When |
|----------|----------|
| **Scoped** | Handler depends on scoped services (DbContext, UnitOfWork) |
| **Transient** | Handler is stateless and lightweight |
| **Singleton** | Handler is thread-safe with no scoped dependencies |

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

Or with the builder pattern:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(DomainHandlers).Assembly);
    dispatch.AddHandlersFromAssembly(typeof(InfrastructureHandlers).Assembly);
    dispatch.AddHandlersFromAssembly(typeof(IntegrationHandlers).Assembly);
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

Wrap handlers with cross-cutting concerns using decorators. The `Decorate<>()` method requires the [Scrutor](https://github.com/khellang/Scrutor) package:

```bash
dotnet add package Scrutor
```

```csharp
// Register the handler
builder.Services.AddScoped<IActionHandler<CreateOrderAction>, CreateOrderHandler>();

// Decorate with logging (requires Scrutor)
builder.Services.Decorate<IActionHandler<CreateOrderAction>, LoggingHandlerDecorator<CreateOrderAction>>();

// Decorate with retry (requires Scrutor)
builder.Services.Decorate<IActionHandler<CreateOrderAction>, RetryHandlerDecorator<CreateOrderAction>>();
```

## Keyed Services (.NET 8+)

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

## Transport and Cross-Cutting Registration

The `AddDispatch()` builder also supports transport and cross-cutting concern registration through extension methods:

```csharp
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Transports (Use prefix — pluggable infrastructure)
    dispatch.UseRabbitMQ(rmq => rmq.HostName("localhost"));
    dispatch.UseKafka(kafka => kafka.BootstrapServers("localhost:9092"));

    // Cross-cutting (Add prefix — additive features)
    dispatch.AddObservability();
    dispatch.AddResilience(res => res.DefaultRetryCount = 3);
    dispatch.AddCaching();
    dispatch.AddSecurity(builder.Configuration);
});
```

See [Configuration](configuration.md) for full builder pattern reference.

## Excalibur Subsystem Registration

The unified `AddExcalibur()` entry point registers Dispatch primitives with sensible defaults:

```csharp
// Simple — Dispatch defaults are sufficient
builder.Services.AddExcalibur(excalibur =>
{
    excalibur
        .AddEventSourcing(es => es.UseEventStore<SqlServerEventStore>())
        .AddOutbox(outbox => outbox.UseSqlServer(connectionString))
        .AddSagas();
});
```

### Excalibur with Custom Dispatch Configuration

When you need transports, pipeline profiles, or middleware, call `AddDispatch` with a builder action. Both orderings are safe because all Dispatch registrations use `TryAdd` internally:

```csharp
// 1. Configure Dispatch with transports and middleware
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseRabbitMQ(rmq => rmq.HostName("localhost"));
    dispatch.AddObservability();
    dispatch.ConfigurePipeline("default", p => p.UseValidation());
});

// 2. Configure Excalibur subsystems
builder.Services.AddExcalibur(excalibur =>
{
    excalibur
        .AddEventSourcing(es => es.UseEventStore<SqlServerEventStore>())
        .AddOutbox(outbox => outbox.UseSqlServer(connectionString));
});
```

## Common Services

Dispatch registers these services automatically:

| Service | Lifetime | Purpose |
|---------|----------|---------|
| `IDispatcher` | Scoped | Message dispatching |
| `IMessageContextAccessor` | Scoped | Access current message context |
| `IMessageContextFactory` | Scoped | Create new contexts |
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
