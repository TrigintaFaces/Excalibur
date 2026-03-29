---
sidebar_position: 9
title: Configuration - Advanced
description: Configuration validation, builder API reference, common patterns, and ValidateOnStart
---

# Configuration: Advanced

Advanced configuration topics including startup validation, builder API reference, and complete configuration patterns.

## Before You Start

- Completed [Configuration](./configuration.md) basics
- Familiarity with [Microsoft.Extensions.Options](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)

## Configuration Validation

Validate configuration at startup:

```csharp
builder.Services.AddOptions<DispatchOptions>()
    .Bind(builder.Configuration.GetSection("Dispatch"))
    .Validate(options =>
    {
        if (options.DefaultTimeout <= TimeSpan.Zero)
            return false;
        return true;
    }, "DefaultTimeout must be positive")
    .ValidateOnStart();
```

## Builder Pattern Reference

### IDispatchBuilder -- Core Methods

| Method | Purpose | Example |
|--------|---------|---------|
| `ConfigurePipeline()` | Named pipeline setup | `dispatch.ConfigurePipeline("default", p => ...)` |
| `RegisterProfile()` | Pipeline profile | `dispatch.RegisterProfile(new MyProfile())` |
| `AddBinding()` | Transport binding | `dispatch.AddBinding(b => ...)` |
| `UseMiddleware<T>()` | Global middleware | `dispatch.UseMiddleware<LoggingMiddleware>()` |
| `ConfigureOptions<T>()` | Options configuration | `dispatch.ConfigureOptions<DispatchOptions>(o => ...)` |

### IDispatchBuilder -- Transport Extensions (`Use` prefix)

| Method | Package | Example |
|--------|---------|---------|
| `UseRabbitMQ()` | `Excalibur.Dispatch.Transport.RabbitMQ` | `dispatch.UseRabbitMQ(rmq => ...)` |
| `UseKafka()` | `Excalibur.Dispatch.Transport.Kafka` | `dispatch.UseKafka(kafka => ...)` |
| `UseAwsSqs()` | `Excalibur.Dispatch.Transport.AwsSqs` | `dispatch.UseAwsSqs(sqs => ...)` |
| `UseAzureServiceBus()` | `Excalibur.Dispatch.Transport.AzureServiceBus` | `dispatch.UseAzureServiceBus(asb => ...)` |
| `UseGooglePubSub()` | `Excalibur.Dispatch.Transport.GooglePubSub` | `dispatch.UseGooglePubSub(pubsub => ...)` |

All transport methods support named overloads: `dispatch.UseKafka("analytics", kafka => ...)`.

### IDispatchBuilder -- Cross-Cutting Extensions (`Use` prefix)

| Method | Package | Example |
|--------|---------|---------|
| `UseObservability()` | `Excalibur.Dispatch.Observability` | `dispatch.UseObservability(obs => ...)` |
| `UseResilience()` | `Excalibur.Dispatch.Resilience.Polly` | `dispatch.UseResilience(res => ...)` |
| `UseCaching()` | `Excalibur.Dispatch.Caching` | `dispatch.UseCaching()` |
| `UseSecurity()` | `Excalibur.Dispatch.Security` | `dispatch.UseSecurity(configuration)` |

### Standalone IServiceCollection Methods

These standalone methods remain available for consumers who prefer direct registration:

| Method | Purpose | Package |
|--------|---------|---------|
| `AddDispatch()` | Core Dispatch services | `Excalibur.Dispatch` |
| `AddRabbitMQTransport()` | RabbitMQ transport | `Excalibur.Dispatch.Transport.RabbitMQ` |
| `AddKafkaTransport()` | Kafka transport | `Excalibur.Dispatch.Transport.Kafka` |
| `AddAwsSqsTransport()` | AWS SQS transport | `Excalibur.Dispatch.Transport.AwsSqs` |
| `AddAzureServiceBusTransport()` | Azure Service Bus | `Excalibur.Dispatch.Transport.AzureServiceBus` |
| `AddGooglePubSubTransport()` | Google Pub/Sub | `Excalibur.Dispatch.Transport.GooglePubSub` |
| `AddDispatchObservability()` | Observability | `Excalibur.Dispatch.Observability` |
| `UseDispatchResilience()` | Resilience (Polly) | `Excalibur.Dispatch.Resilience.Polly` |
| `UseCaching()` | Caching (on `IDispatchBuilder`) | `Excalibur.Dispatch.Caching` |
| `UseSecurity()` | Security (on `IDispatchBuilder`) | `Excalibur.Dispatch.Security` |
| `AddMessagePackSerialization()` | MessagePack serialization | `Excalibur.Dispatch.Serialization.MessagePack` |
| `AddPluggableSerialization()` | Pluggable serialization (MemoryPack default) | `Excalibur.Dispatch` |

## Common Configuration Patterns

### Minimal API

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
});
var app = builder.Build();

app.MapPost("/orders", async (CreateOrderAction action, IDispatcher dispatcher, CancellationToken ct) =>
    await dispatcher.DispatchAsync(action, ct));

app.Run();
```

### Full-Featured Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

// Unified Dispatch registration -- transports + cross-cutting through the builder
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);

    // Transports (Use prefix)
    dispatch.UseKafka(kafka => kafka.BootstrapServers(builder.Configuration["Kafka:Servers"]));
    dispatch.UseRabbitMQ(rmq => rmq.HostName("localhost"));

    // Cross-cutting concerns (Use prefix)
    dispatch.UseObservability();
    dispatch.UseResilience(res => res.DefaultRetryCount = 3);
    dispatch.UseCaching();
    dispatch.UseSecurity(builder.Configuration);

    // Global middleware
    dispatch.UseMiddleware<LoggingMiddleware>();
    dispatch.UseMiddleware<ValidationMiddleware>();
    dispatch.UseMiddleware<AuthorizationMiddleware>();

    // Options
    dispatch.ConfigureOptions<DispatchOptions>(options =>
    {
        options.DefaultTimeout = TimeSpan.FromSeconds(30);
    });

    // Multi-transport routing
    dispatch.UseRouting(routing =>
    {
        routing.Transport
            .Route<OrderCreatedEvent>().To("kafka")
            .Default("rabbitmq");
    });
});

// Serialization
// MemoryPack is auto-registered by AddDispatch(). For alternatives:

// Health checks
builder.Services.AddHealthChecks()
    .AddTransportHealthChecks();

// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("Excalibur.Dispatch.Observability"))
    .WithMetrics(m => m.AddDispatchMetrics());

var app = builder.Build();
app.MapHealthChecks("/health");
app.Run();
```

### Combined with Excalibur

When using Excalibur subsystems alongside Dispatch, call `AddDispatch` for transport and pipeline configuration, then `AddExcalibur` for domain infrastructure. `AddExcalibur` registers Dispatch primitives with defaults, so both orderings are safe:

```csharp
// Dispatch -- transports, middleware, pipelines
builder.Services.AddDispatch(dispatch =>
{
    dispatch.AddHandlersFromAssembly(typeof(Program).Assembly);
    dispatch.UseKafka(kafka => kafka.BootstrapServers("localhost:9092"));
    dispatch.UseObservability();
    dispatch.ConfigurePipeline("default", p => p.UseValidation());
});

// Excalibur -- event sourcing, outbox, sagas
builder.Services.AddExcalibur(excalibur =>
{
    excalibur
        .AddEventSourcing(es => es.UseEventStore<SqlServerEventStore>())
        .AddOutbox(outbox => outbox.UseSqlServer(opts => opts.ConnectionString = connectionString))
        .AddSagas();
});
```

## Options Validation with ValidateOnStart

Excalibur uses `ValidateOnStart()` to catch configuration errors at application startup rather than at first use. This follows the [Microsoft.Extensions.Options validation pattern](https://learn.microsoft.com/en-us/dotnet/core/extensions/options#options-validation).

### How It Works

When you call `ValidateOnStart()`, the framework validates all `IOptions<T>` registrations during `IHost.StartAsync()`. If any validation fails, the application throws `OptionsValidationException` immediately -- before handling any requests.

```csharp
// This is what happens inside Excalibur's DI extensions:
services.AddOptions<LeaderElectionOptions>()
    .ValidateDataAnnotations()   // Validates [Required], [Range], etc.
    .ValidateOnStart();          // Runs validation at startup, not first use
```

### Built-In Validators

Excalibur provides `IValidateOptions<T>` validators for cross-property constraint checking. These go beyond `[DataAnnotations]` to validate relationships between properties.

**Example: LeaderElectionOptionsValidator**

```csharp
public sealed class LeaderElectionOptionsValidator : IValidateOptions<LeaderElectionOptions>
{
    public ValidateOptionsResult Validate(string? name, LeaderElectionOptions options)
    {
        if (options.RenewInterval >= options.LeaseDuration)
        {
            return ValidateOptionsResult.Fail(
                $"RenewInterval ({options.RenewInterval}) must be less than " +
                $"LeaseDuration ({options.LeaseDuration}).");
        }

        if (options.GracePeriod >= options.LeaseDuration)
        {
            return ValidateOptionsResult.Fail(
                $"GracePeriod ({options.GracePeriod}) must be less than " +
                $"LeaseDuration ({options.LeaseDuration}).");
        }

        return ValidateOptionsResult.Success;
    }
}
```

### Packages with ValidateOnStart

The following packages register `ValidateOnStart()` + `ValidateDataAnnotations()` for their Options classes. Many also include cross-property `IValidateOptions<T>` validators:

| Package | Options Class | Cross-Property Validator |
|---|---|---|
| `Excalibur.Dispatch` | `DispatchTelemetryOptions`, `CircuitBreakerOptions`, `TimePolicyOptions`, `OutboxOptions` | Yes |
| `Excalibur.Dispatch.Observability` | `ContextObservabilityOptions`, `TelemetrySanitizerOptions` | Yes |
| `Excalibur.Dispatch.Security` | `JwtAuthenticationOptions`, `SigningOptions` | Yes |
| `Excalibur.Dispatch.Resilience.Polly` | `PollyResilienceOptions` | Yes |
| `Excalibur.Dispatch.Caching` | `CacheOptions` | Yes |
| `Excalibur.Dispatch.Compliance` | `ErasureOptions` | Yes |
| `Excalibur.Dispatch.Patterns` | `ClaimCheckOptions` | Yes |
| `Excalibur.Dispatch.Transport.RabbitMQ` | `RabbitMqTransportOptions` | Yes |
| `Excalibur.Dispatch.Transport.GooglePubSub` | `StreamingPullOptions`, `OrderingKeyOptions` | Yes |
| `Excalibur.Dispatch.LeaderElection` | `LeaderElectionOptions` | Yes |
| `Excalibur.EventSourcing` | `MaterializedViewOptions`, `SnapshotUpgradingOptions` | Yes |
| `Excalibur.Saga` | `SagaOptions` | Yes |
| `Excalibur.Saga.SqlServer` | `SqlServerSagaStoreOptions`, `SqlServerSagaTimeoutStoreOptions` | Yes |
| `Excalibur.Cdc` | `CdcProcessingOptions` | Yes |
| Various data providers | `SqlServerPersistenceOptions`, `PostgresPersistenceOptions`, CDC state store options | Yes |

### Writing Custom Validators

Create an `IValidateOptions<T>` implementation for cross-property validation in your own Options classes:

```csharp
public sealed class MyFeatureOptionsValidator : IValidateOptions<MyFeatureOptions>
{
    public ValidateOptionsResult Validate(string? name, MyFeatureOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.RetryCount > 0 && options.RetryDelay <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail(
                "RetryDelay must be positive when RetryCount > 0.");
        }

        return ValidateOptionsResult.Success;
    }
}
```

Register your validator alongside your Options:

```csharp
services.AddOptions<MyFeatureOptions>()
    .ValidateDataAnnotations()
    .ValidateOnStart();

services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IValidateOptions<MyFeatureOptions>, MyFeatureOptionsValidator>());
```

:::tip Why ValidateOnStart Matters
Without `ValidateOnStart()`, misconfigured options are only detected when `IOptions<T>.Value` is first accessed -- which could be hours into production under a specific code path. `ValidateOnStart()` fails fast at startup, before any traffic is served.
:::

## See Also

- [Configuration](./configuration.md) -- Basic setup and builder API
- [Configuration - Environments](./configuration-environments.md) -- Environment-specific settings and transports
- [Dependency Injection](dependency-injection.md) -- DI patterns and lifetimes
- [Pipeline](../pipeline/index.md) -- Middleware configuration details
