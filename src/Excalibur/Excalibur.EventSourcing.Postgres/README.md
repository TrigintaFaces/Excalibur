# Excalibur.EventSourcing.Postgres

Postgres implementations for Excalibur event sourcing, including event store, snapshot store, and outbox store. Uses Npgsql and Dapper for high-performance data access.

## Installation

```bash
dotnet add package Excalibur.EventSourcing.Postgres
```

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddPostgresEventStore(connectionString);
services.AddPostgresSnapshotStore(connectionString);
```

## Features

- **Event Store** -- Postgres-backed `IEventStore` with optimistic concurrency
- **Snapshot Store** -- Postgres-backed `ISnapshotStore` for aggregate state snapshots
- **Outbox Store** -- Postgres-backed outbox for reliable message publishing
- **AOT Compatible** -- Full Native AOT and IL trimming support
- **Health Checks** -- Integrated Postgres health check registration
- **Auto-Migration** -- Optional `PostgresMigrationHostedService` for schema setup

## Configuration

```csharp
services.AddPostgresEventStore(connectionString, options =>
{
    options.SchemaName = "events";
    options.TableName = "event_store";
});
```

## Documentation

See the [Postgres data provider guide](https://github.com/TrigintaFaces/Excalibur) for detailed configuration and usage.

## License

This package is part of the Excalibur framework. See [LICENSE](..\..\..\LICENSE) for license details.
