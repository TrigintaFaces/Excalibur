---
sidebar_position: 1
title: Configuration Overview
description: Configure Excalibur services using dependency injection and fluent builders
---

# Configuration Overview

Excalibur uses Microsoft-style dependency injection with fluent builder patterns for configuration. This guide covers the unified configuration approach and common patterns.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [dependency injection](../core-concepts/dependency-injection.md) and [core concepts](../core-concepts/index.md)

## Unified Entry Point

The `AddExcalibur()` extension method is the primary entry point for configuring all Excalibur subsystems:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddExcalibur(excalibur =>
{
    excalibur
        .AddEventSourcing(es => es.UseEventStore<SqlServerEventStore>())
        .AddOutbox(outbox => outbox.UseSqlServer(connectionString))
        .AddCdc(cdc => cdc.TrackTable<Order>())
        .AddSagas(opts => opts.EnableTimeouts = true)
        .AddLeaderElection(opts => opts.LeaseDuration = TimeSpan.FromSeconds(30));
});
```

This automatically:
- Registers Dispatch primitives (`IDispatcher`, `IMessageBus`, etc.)
- Sets up the core pipeline with sensible defaults
- Configures each subsystem you enable

## Required NuGet Packages

| Feature | Package |
|---------|---------|
| Core domain modeling | `Excalibur.Domain` |
| Event sourcing | `Excalibur.EventSourcing` |
| SQL Server provider | `Excalibur.EventSourcing.SqlServer` |
| PostgreSQL provider | `Excalibur.EventSourcing.Postgres` |
| Outbox pattern | `Excalibur.Outbox` |
| CDC (Change Data Capture) | `Excalibur.Cdc` |
| Sagas | `Excalibur.Saga` |
| Leader election | `Excalibur.LeaderElection` |

## Configuration Patterns

### Minimal Configuration

For simple applications:

```csharp
services.AddExcalibur(excalibur =>
{
    excalibur.AddEventSourcing(es =>
    {
        es.UseEventStore<SqlServerEventStore>();
    });
});
```

### Production Configuration

For production applications with all features:

```csharp
var connectionString = builder.Configuration.GetConnectionString("Database");

services.AddExcalibur(excalibur =>
{
    // Event sourcing with snapshots
    excalibur.AddEventSourcing(es =>
    {
        es.UseEventStore<SqlServerEventStore>()
          .UseIntervalSnapshots(100)
          .AddRepository<OrderAggregate, OrderId>();
    });

    // Reliable messaging via outbox
    excalibur.AddOutbox(outbox =>
    {
        outbox.UseSqlServer(connectionString)
              .EnableBackgroundProcessing()
              .WithProcessing(p => p.BatchSize(100));
    });

    // Change data capture for projections
    excalibur.AddCdc(cdc =>
    {
        cdc.UseSqlServer(connectionString)
           .TrackTable<Order>()
           .TrackTable<Customer>();
    });
});
```

### Advanced Dispatch Configuration

When you need custom Dispatch configuration (transports, middleware), configure Dispatch separately:

```csharp
// 1. Configure Dispatch with transports and middleware
services.AddDispatch(dispatch =>
{
    dispatch.UseRabbitMQ(rmq => rmq.HostName("localhost"));
    dispatch.AddObservability();
    dispatch.ConfigurePipeline("default", p => p.UseValidation());
});

// 2. Configure Excalibur subsystems
services.AddExcalibur(excalibur =>
{
    excalibur
        .AddEventSourcing(es => es.UseEventStore<SqlServerEventStore>())
        .AddOutbox(outbox => outbox.UseSqlServer(connectionString));
});
```

Both orderings are safe because all Dispatch registrations use `TryAdd` internally.

## Configuration Sources

### From appsettings.json

```json
{
  "ConnectionStrings": {
    "EventStore": "Server=localhost;Database=Events;..."
  },
  "Excalibur": {
    "Outbox": {
      "BatchSize": 100,
      "PollingInterval": "00:00:05"
    },
    "Snapshots": {
      "Interval": 100
    }
  }
}
```

```csharp
services.AddExcalibur(excalibur =>
{
    var config = builder.Configuration.GetSection("Excalibur");

    excalibur.AddEventSourcing(es =>
    {
        es.UseEventStore<SqlServerEventStore>()
          .UseIntervalSnapshots(config.GetValue<int>("Snapshots:Interval"));
    });
});
```

### From Environment Variables

```csharp
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("Database");

services.AddExcalibur(excalibur =>
{
    excalibur.AddEventSourcing(es =>
    {
        es.UseSqlServer(connectionString);
    });
});
```

## Configuration Sections

| Section | Description | Guide |
|---------|-------------|-------|
| Event Sourcing | Event stores, repositories, snapshots | [Event Store Setup](./event-store-setup.md) |
| Outbox | Reliable messaging, processing options | [Outbox Setup](./outbox-setup.md) |
| Snapshots | Snapshot strategies and triggers | [Snapshot Setup](./snapshot-setup.md) |

## Health Checks

Add health checks for all Excalibur components:

```csharp
services.AddExcaliburHealthChecks(health =>
{
    health.AddSqlServer(connectionString, name: "database")
          .AddCheck<OutboxHealthCheck>("outbox");
});

app.MapHealthChecks("/.well-known/ready");
```

## Validation

Configuration is validated at startup. Common validation errors:

| Error | Cause | Solution |
|-------|-------|----------|
| `EventStore not configured` | Missing `UseEventStore<T>()` | Add event store configuration |
| `Connection string is null` | Missing connection string | Check appsettings or env vars |
| `Invalid batch size` | BatchSize `<= 0` | Use positive batch size |

## Next Steps

- [Event Store Setup](./event-store-setup.md) — Configure event stores and repositories
- [Outbox Setup](./outbox-setup.md) — Configure reliable messaging
- [Snapshot Setup](./snapshot-setup.md) — Configure snapshot strategies

## See Also

- [Core Concepts](../core-concepts/index.md) - Foundational Dispatch concepts
- [Dependency Injection](../core-concepts/dependency-injection.md) - DI patterns and registration
- [Data Providers](../data-providers/index.md) - Database provider configuration
