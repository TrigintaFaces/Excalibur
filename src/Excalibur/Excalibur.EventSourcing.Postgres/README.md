# Excalibur.EventSourcing.Postgres

Postgres implementations for Excalibur event sourcing, including event store, snapshot store, and outbox store. Uses Npgsql and Dapper for high-performance data access.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Dispatch.Postgres` | Starter | + Dispatch core + Outbox + Hosting |
| `Excalibur.Postgres` | Complete | + Inbox + Saga + Leader Election + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.Postgres` for a production-ready PostgreSQL stack with a single package reference.

## Installation

```bash
dotnet add package Excalibur.EventSourcing.Postgres
```

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
    es.UsePostgres(pg => pg.DataSource(dataSource))));
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
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(es =>
    es.UsePostgres(pg => pg
        .DataSource(dataSource)
        .EventStoreSchema("events")
        .EventStoreTable("event_store"))));
```

## Documentation

See the [Postgres data provider guide](https://github.com/TrigintaFaces/Excalibur) for detailed configuration and usage.

## License

This package is part of the Excalibur framework. See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for license details.

## Schema

**This package never creates its tables at runtime.** Provision them before the first append —
`PostgresMigrator` is not a provisioning path, it runs migrations from an assembly you supply.

The canonical DDL ships in the package:

| script | creates |
| --- | --- |
| `scripts/001_CreateSnapshotSchema.sql` | `public.event_store_snapshots` |
| `scripts/004_CreateEventStoreSchema.sql` | `public.events` |

Defaults: schema `public`, tables `events` and `event_store_snapshots`, all configurable via
`PostgresEventSourcingOptions`. Without the event-store table the first append fails with
`42P01: relation "events" does not exist`.

Both scripts are re-runnable and only ever create missing objects; neither alters an existing
table. `scripts/002_…` and `scripts/003_…` are migrations for databases provisioned by earlier
versions — read their headers before running them, as one is order-dependent with the package
release that introduced it.
