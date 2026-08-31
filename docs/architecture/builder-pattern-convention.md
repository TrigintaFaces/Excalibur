# Builder Interface Pattern Convention

> **Sprint 804 update (ADR-321/325):** The per-subsystem `AddExcalibur{X}(...)` service-collection aggregators referenced below have been **internalized or deleted** as part of the composition-root-only unification. New code should compose via `services.AddExcalibur(excalibur => excalibur.Add{X}(...))` on `IExcaliburBuilder`. The Pattern 1/Pattern 2 classifications below remain correct as *internal* design guidance for the sub-builders (`IOutboxBuilder`, `IEventSourcingBuilder`, etc.) — they no longer describe the *public* consumer contract.

## Overview

The Excalibur.Dispatch framework has 30+ builder interfaces across packages. This document establishes the standard pattern for new builders and identifies top inconsistencies for future cleanup.

## Convention

### Pattern 1: Simple Registration (no composition)

**Use when:** Single Options class, no sub-features to compose.

```csharp
public static IServiceCollection AddExcaliburOutbox(
    this IServiceCollection services,
    Action<OutboxOptions> configure)
{
    services.Configure(configure);
    // register services
    return services;
}
```

**Returns:** `IServiceCollection` for standard DI chaining.

### Pattern 2: Composable Registration (builder callback)

**Use when:** Multiple sub-features, provider selection, decorator chains.

```csharp
public static IServiceCollection AddExcaliburEventSourcing(
    this IServiceCollection services,
    Action<IEventSourcingBuilder> configure)
{
    var builder = new EventSourcingBuilder(services);
    configure(builder);
    return services;
}

public interface IEventSourcingBuilder
{
    IEventSourcingBuilder UseSnapshotStore<T>() where T : class, ISnapshotStore;
    IEventSourcingBuilder UseEventStore<T>() where T : class, IEventStore;
    IEventSourcingBuilder AddProjection<T>() where T : class, IProjection;
}
```

**Returns:** `IServiceCollection`. Builder is passed via callback.

### Pattern 3: Complex Infrastructure (returns builder)

**Use when:** Consumer needs to chain `.AddScheme<T>()` style registration. Follows `AddAuthentication()` pattern.

```csharp
public static IKafkaTransportBuilder AddKafkaTransport(
    this IServiceCollection services,
    Action<KafkaTransportOptions>? configure = null)
{
    // register base services
    return new KafkaTransportBuilder(services);
}

public interface IKafkaTransportBuilder
{
    IKafkaTransportBuilder UseProducer(Action<KafkaProducerOptions> configure);
    IKafkaTransportBuilder UseConsumer(Action<KafkaConsumerOptions> configure);
    IKafkaTransportBuilder UseDeadLetter(Action<IRabbitMQDeadLetterBuilder> configure);
}
```

**Returns:** Builder instance for continued chaining.

### Decision Matrix

| Criteria | Pattern 1 | Pattern 2 | Pattern 3 |
|----------|-----------|-----------|-----------|
| Sub-features | 0 | 1-5 | 5+ |
| Provider selection | No | Yes | Yes |
| Decorator chains | No | Possible | Common |
| Nested builders | No | No | Yes |
| Microsoft reference | `AddHealthChecks()` | `AddAuthentication(Action<>)` | `AddAuthentication()` returning builder |

## Current Landscape

| Pattern | Count | Examples |
|---------|-------|---------|
| Fluent builder returning self | ~12 | `IRabbitMQTransportBuilder`, `IKafkaTransportBuilder`, `ICdcBuilder`, `ISagaBuilder` |
| Sub-builders (nested fluent) | ~8 | `IRabbitMQQueueBuilder`, `IRabbitMQExchangeBuilder`, `IKafkaProducerBuilder` |
| `Action<Options>` only | ~15+ | Various `AddExcalibur*(Action<Options>)` |
| Returns `IServiceCollection` | ~5 | `AddExcaliburOutbox` |
| Mapping builders | 3 | `IMessageMappingBuilder`, `IMessageTypeMappingBuilder<T>` |

## Top 5 Inconsistencies

1. **`AddExcaliburOutbox`** returns `IServiceCollection` but has sub-features (DLQ, multi-transport) — should use Pattern 2 with `IOutboxBuilder`.

2. **CDC providers** each have their own builder (`IMongoDbCdcBuilder`, `IFirestoreCdcBuilder`, etc.) — should share a base `ICdcProviderBuilder` interface for common operations.

3. **RabbitMQ** has 5 sub-builders (queue, exchange, binding, dead letter, transport) — correct Pattern 3 but deep nesting may hurt discoverability. Consider consolidating queue+exchange into one builder.

4. **`IA3Builder`** naming is opaque — should follow `I{Feature}Builder` convention with a descriptive name.

5. **Namespace inconsistency** — some builders live in `Microsoft.Extensions.DependencyInjection` (correct for DI extensions), some in feature namespaces. DI entry points should be in `Microsoft.Extensions.DependencyInjection`; builder interfaces should be in feature namespaces.

## Builder Method Naming: Connection Configuration

> **Sprint 822 (bd-hvhwn3):** Audit confirmed all builders already follow the fluent method pattern. No API changes required — only stale doc comment fixes.

### Canonical Pattern: Fluent Methods (Not Property Setters)

All builder interfaces use **fluent methods** for connection configuration, not property setters:

```csharp
// CORRECT — fluent builder method (canonical pattern)
inbox.UseSqlServer(sql => sql.ConnectionString("Server=...;Database=...;"));

// INCORRECT — property setter (not used by any builder interface)
inbox.UseSqlServer(sql => sql.ConnectionString("Server=...;Database=..."));
```

### Standard Connection Overloads (Canonical 4)

Every `Use{Provider}` builder should expose these four mutually exclusive connection methods:

| Method | Purpose |
|--------|---------|
| `ConnectionString(string)` | Direct connection string |
| `ConnectionStringName(string)` | Resolve from `IConfiguration.GetConnectionString(name)` at runtime |
| `ConnectionFactory(Func<IServiceProvider, Func<TConnection>>)` | Factory for Managed Identity, Key Vault, custom pooling |
| `BindConfiguration(string)` | Bind entire options from a configuration section |

These are **last-wins** when multiple are called. This pattern is consistent across all 15+ provider builders (CDC, Saga, EventSourcing, Inbox, Outbox, IdentityMap, AuditLogging, Compliance, Data providers).

### Distinction: Builder Methods vs Options Properties

The `ConnectionString` property setter (`opts.ConnectionString = "..."`) exists only on **Options types** (e.g., `SqlServerEventSourcingOptions`, `CdcSqlServerOptions`), not on builder interfaces. Builders internally map fluent calls to Options properties via `PostConfigure`. Consumers should never need to set Options properties directly — the builder API is the supported contract.

## Guidelines for New Builders

1. Start with Pattern 1 (`Action<Options>` returning `IServiceCollection`).
2. Graduate to Pattern 2 only when there are multiple sub-features or providers to compose.
3. Use Pattern 3 only for complex infrastructure with nested configuration (transports, data providers).
4. Builder method names should use `Use*()` for providers and `Add*()` for features.
5. All builders should be in the feature namespace, DI extensions in `Microsoft.Extensions.DependencyInjection`.
6. Builder interfaces should be kept minimal (max 5-7 methods).
7. Connection configuration must use the canonical 4-method fluent pattern (see above).
