---
sidebar_position: 1
title: Configuration Overview
description: Configure Excalibur services using dependency injection and fluent builders
---

# Configuration Overview

Excalibur uses Microsoft-style dependency injection with fluent builder patterns for configuration. This guide covers the unified configuration approach and common patterns.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  dotnet add package Excalibur.Hosting
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
        .AddOutbox(outbox => outbox.UseSqlServer(sql => sql.ConnectionString(connectionString)))
        .AddCdc(cdc => cdc.TrackTable<Order>())
        .AddSagas(saga => saga.WithCoordination().WithTimeouts())
        .AddLeaderElection(le => le
            .UseSqlServer(sql => sql.ConnectionString(connectionString))
            .WithOptions(o => o.LeaseDuration = TimeSpan.FromSeconds(30)));
});
```

`AddExcalibur()` is provided by `Excalibur.Hosting`, while each `.AddXxx(...)` subsystem method is provided by its feature package.

This automatically:
- Registers Dispatch primitives (`IDispatcher`, `IMessageBus`, etc.)
- Sets up the core pipeline with sensible defaults
- Configures each subsystem you enable

## Required NuGet Packages

| Feature | Package |
|---------|---------|
| Unified builder (`AddExcalibur`) | `Excalibur.Hosting` |
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
        outbox.UseSqlServer(sql => sql.ConnectionString(connectionString))
              .EnableBackgroundProcessing()
              .WithProcessing(p => p.BatchSize(100));
    });

    // Change data capture for projections
    excalibur.AddCdc(cdc =>
    {
        cdc.UseSqlServer(sql => sql.ConnectionString(connectionString))
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
    dispatch.UseObservability();
    dispatch.ConfigurePipeline("default", p => p.UseValidation());
});

// 2. Configure Excalibur subsystems
services.AddExcalibur(excalibur =>
{
    excalibur
        .AddEventSourcing(es => es.UseEventStore<SqlServerEventStore>())
        .AddOutbox(outbox => outbox.UseSqlServer(sql => sql.ConnectionString(connectionString)));
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
        es.UseSqlServer(opts => opts.ConnectionString(connectionString));
    });
});
```

## Configuration Sections

| Section | Description | Guide |
|---------|-------------|-------|
| `ApplicationContext` | Application identity | [Application identity](#application-identity) |
| Event Sourcing | Event stores, repositories, snapshots | [Event Store Setup](./event-store-setup.md) |
| Outbox | Reliable messaging, processing options | [Outbox Setup](./outbox-setup.md) |
| Snapshots | Snapshot strategies and triggers | [Snapshot Setup](./snapshot-setup.md) |

## Application identity

`ConfigureApplicationContext()` establishes who your application is. Two values are required:

| Key | Meaning | Default when unset |
|-----|---------|--------------------|
| `ApplicationName` | The application's display identity | The host environment's application name — your entry assembly, unless the host was told otherwise |
| `ApplicationSystemName` | The machine-readable identity used in URLs, cache keys and stored records | `ApplicationName` in kebab-case |

**Both have defaults, so a host that configures nothing still starts.** Set them explicitly when the
defaults are not what you want to live with — and for `ApplicationName` in particular, because it is
written into authorization grant records, where a value derived from an assembly name is rarely the
one you would choose.

```json
{
  "ApplicationContext": {
    "ApplicationName": "Order Management",
    "ApplicationSystemName": "order-management"
  }
}
```

A value you supply always wins; only blank values are filled in. The section is bound to
`IOptions<ApplicationContextOptions>` and validated at startup, so a value that is present but empty
fails immediately rather than surfacing later as a blank field in a log, a telemetry dimension, or a
stored grant.

:::note Upgrading from 3.x
Startup validation of these two values is new. An application that previously ran with a blank
`ApplicationName` was not working correctly — it was writing that blank into every record that
carries it. If your host fails to start naming these fields, set them, or accept the defaults above.
:::

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
