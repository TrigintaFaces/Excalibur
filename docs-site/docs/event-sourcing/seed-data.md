---
sidebar_position: 14
title: Seed Data Pattern
description: Initialize event-sourced aggregates with seed data using IHostedService
---

# Seed Data Pattern

Event-sourced systems need a way to initialize reference data — configuration aggregates, system accounts, default categories — without relying on SQL scripts or migration tooling. This recipe shows how to seed aggregates using `IHostedService` so seed data is expressed as domain events, fully auditable and consistent with your event sourcing model.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.EventSourcing
  dotnet add package Excalibur.EventSourcing.SqlServer  # or your provider
  ```
- Familiarity with [aggregates](./aggregates.md) and [repositories](./repositories.md)

## Why Seed via Events?

| Approach | Auditable | Projections Update | Idempotent | Version-Safe |
|----------|-----------|-------------------|------------|--------------|
| SQL INSERT scripts | No | No (bypasses event store) | Manual | Fragile |
| EF migrations | No | No | Requires tracking | Fragile |
| **Event-sourced seed (this pattern)** | Yes | Yes (inline + async) | Built-in | Yes |

Seeding through the event store means:
- Projections automatically reflect seed data (inline and async)
- Full audit trail of when and how data was initialized
- Concurrency checks prevent duplicate seeding naturally
- Upcasters and versioning apply to seed events like any other

## The Pattern

### 1. Define Your Aggregate

```csharp
public sealed class SystemConfiguration : AggregateRoot<Guid>
{
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    // For rehydration
    public SystemConfiguration() { }

    // Factory for initial creation
    public static SystemConfiguration Create(Guid id, string key, string value)
    {
        var config = new SystemConfiguration();
        config.RaiseEvent(new SystemConfigurationCreated(id, key, value));
        return config;
    }

    protected override void Apply(IDomainEvent @event)
    {
        switch (@event)
        {
            case SystemConfigurationCreated e:
                Id = e.ConfigId;
                Key = e.Key;
                Value = e.Value;
                IsActive = true;
                break;
        }
    }
}
```

### 2. Create the Seed Service

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public sealed class SeedDataHostedService : IHostedService
{
    private readonly IEventSourcedRepository<SystemConfiguration, Guid> _repository;
    private readonly ILogger<SeedDataHostedService> _logger;

    public SeedDataHostedService(
        IEventSourcedRepository<SystemConfiguration, Guid> repository,
        ILogger<SeedDataHostedService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await SeedIfNotExistsAsync(
            WellKnownIds.DefaultTenant,
            "default-tenant",
            "Default",
            cancellationToken);

        await SeedIfNotExistsAsync(
            WellKnownIds.SystemAccount,
            "system-account",
            "System",
            cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedIfNotExistsAsync(
        Guid id, string key, string value, CancellationToken cancellationToken)
    {
        // LoadAsync returns an aggregate with Version=0 if no events exist.
        // Check Version to determine if the aggregate has been seeded.
        var existing = await _repository.LoadAsync(id, cancellationToken);
        if (existing.Version > 0)
        {
            _logger.LogDebug("Seed data '{Key}' already exists, skipping", key);
            return;
        }

        var config = SystemConfiguration.Create(id, key, value);
        await _repository.SaveAsync(config, cancellationToken);

        _logger.LogInformation("Seeded '{Key}' with id {Id}", key, id);
    }
}
```

### 3. Register the Service

```csharp
services.AddHostedService<SeedDataHostedService>();
```

:::tip Ordering

`IHostedService` instances start in registration order. Register your seed service **after** event store and projection infrastructure so stores are available when seeding runs.

:::

## Idempotency

The pattern is naturally idempotent:

1. **Version check**: `LoadAsync` returns the aggregate's current state. If `Version > 0`, it's already seeded.
2. **Optimistic concurrency**: Even if two instances race, `AppendAsync` with `expectedVersion: -1` (new aggregate) will fail for the second writer with a `ConcurrencyException`.
3. **Safe retries**: On startup failure, the service retries on next application start — already-seeded aggregates are skipped.

```csharp
// For extra safety in multi-instance deployments:
private async Task SeedIfNotExistsAsync(
    Guid id, string key, string value, CancellationToken cancellationToken)
{
    var existing = await _repository.LoadAsync(id, cancellationToken);
    if (existing.Version > 0)
    {
        return; // Already seeded
    }

    try
    {
        var config = SystemConfiguration.Create(id, key, value);
        await _repository.SaveAsync(config, cancellationToken);
    }
    catch (ConcurrencyException)
    {
        // Another instance seeded first — this is fine
        _logger.LogDebug("Seed data '{Key}' was created by another instance", key);
    }
}
```

## Well-Known IDs

Use deterministic GUIDs for seed data so every environment gets the same identifiers:

```csharp
public static class WellKnownIds
{
    // Deterministic GUIDs — same across all environments
    public static readonly Guid DefaultTenant =
        new("00000000-0000-0000-0000-000000000001");

    public static readonly Guid SystemAccount =
        new("00000000-0000-0000-0000-000000000002");

    public static readonly Guid AdminRole =
        new("00000000-0000-0000-0000-000000000003");
}
```

## Conditional Seeding

For environment-specific seed data (e.g., test data in development only):

```csharp
public sealed class DevelopmentSeedService : IHostedService
{
    private readonly IHostEnvironment _environment;
    private readonly IEventSourcedRepository<TestCustomer, Guid> _repository;

    public DevelopmentSeedService(
        IHostEnvironment environment,
        IEventSourcedRepository<TestCustomer, Guid> repository)
    {
        _environment = environment;
        _repository = repository;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return;
        }

        // Seed test data only in development
        await SeedTestCustomersAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

## Best Practices

### Do

- Use deterministic IDs (well-known GUIDs) for seed aggregates
- Handle `ConcurrencyException` gracefully in multi-instance scenarios
- Log seed operations for debugging startup issues
- Register seed services after infrastructure services
- Keep seed data minimal — only what the system needs to boot

### Don't

- Seed large datasets at startup (use background jobs instead)
- Depend on seed ordering across aggregate types (seed independently)
- Use `Task.Run` or fire-and-forget in `StartAsync` (let the host manage lifecycle)
- Store secrets as seed data (use configuration/key vault instead)

## See Also

- [Aggregates](./aggregates.md) — Define the domain objects being seeded
- [Repositories](./repositories.md) — Load and save event-sourced aggregates
- [Projections](./projections.md) — Seed data automatically updates inline projections
