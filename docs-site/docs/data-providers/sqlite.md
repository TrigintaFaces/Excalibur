---
sidebar_position: 2
title: SQLite
description: Embedded SQLite event store and snapshot store for local development, testing, and single-node deployments.
---

# SQLite Provider

SQLite gives you an event store and a snapshot store that run inside your process against a single file, with no server to install and no container to start.

Unlike the other providers listed here, this one is **event sourcing only**. It does not supply data executors, saga, inbox, outbox, leader election, CDC, compliance, or audit storage. Reach for it when you want durable events on a laptop, in a test run, or in a single-node or edge deployment — and for anything that needs a shared server or a second process, use [PostgreSQL](./postgres.md) or [SQL Server](./sqlserver.md).

## Before You Start

- **.NET 10.0**
- No server, no container. The database is a file the provider creates on first use.
- Familiarity with [event sourcing](../event-sourcing/index.md)

## Installation

```bash
dotnet add package Excalibur.EventSourcing.Sqlite
```

**Dependencies:** `Excalibur.EventSourcing`, `Microsoft.Data.Sqlite`

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddEventSourcing(es =>
{
    es.UseSqlite(options =>
    {
        options.ConnectionString = "Data Source=events.db";
    });
});
```

That registers `IEventStore` and `ISnapshotStore`. The `Events` and `Snapshots` tables are created on first use, so there is no migration step for a fresh database.

## Registration Methods

| Method | What It Registers | Key Options |
|--------|-------------------|-------------|
| `es.UseSqlite(Action<SqliteEventSourcingOptions>)` | `IEventStore` + `ISnapshotStore` | `ConnectionString`, `EventStoreTable`, `SnapshotStoreTable`, `EventTypeInfoResolver` |
| `es.UseSqlite(IConfiguration)` | `IEventStore` + `ISnapshotStore` | Binds the same options from a configuration section |

### Options

| Option | Default | Purpose |
|--------|---------|---------|
| `ConnectionString` | *(required)* | e.g. `Data Source=events.db`, or `Data Source=:memory:` for a database that lives only as long as the connection |
| `EventStoreTable` | `Events` | Event table name |
| `SnapshotStoreTable` | `Snapshots` | Snapshot table name |
| `EventTypeInfoResolver` | `null` | A source-generated JSON resolver, required under native AOT (see below) |

The event store writes each event's declared `[MessageName]` -- not its CLR type name -- to the `EventType` column, and resolves that name back to a CLR type through the registered event-type registry on read. See [Stable Message Names](../event-sourcing/domain-events.md#stable-message-names).

## Native AOT

Domain events are your types, so the framework cannot source-generate their serialization. With no resolver configured the store serializes them by reflection, which is fine under the JIT. A native-AOT application published with reflection-based serialization disabled has no such fallback, and the first append fails.

Supply a resolver covering your event types and the runtime types of any values you put in `IDomainEvent.Metadata`:

```csharp
[JsonSerializable(typeof(OrderPlaced))]
[JsonSerializable(typeof(OrderShipped))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(int))]
internal sealed partial class EventContext : JsonSerializerContext;

es.UseSqlite(options =>
{
    options.ConnectionString = "Data Source=events.db";
    options.EventTypeInfoResolver = EventContext.Default;
});
```

Declare each closed metadata value type you actually store. Declaring `Dictionary<string, object>` as a shortcut compiles and then throws on the values it was meant to cover.

The stored bytes do not change with this setting — the resolver supplies type metadata only — so events written with and without one are interchangeable.

## Schema

Tables are created on first use. The DDL is also shipped as scripts in the package for deployments that apply schema out of band, where the database user at runtime may not hold `CREATE TABLE`.

```sql
CREATE TABLE IF NOT EXISTS [Events] (
    GlobalPosition INTEGER PRIMARY KEY AUTOINCREMENT,
    EventId TEXT NOT NULL,
    AggregateId TEXT NOT NULL,
    AggregateType TEXT NOT NULL,
    EventType TEXT NOT NULL,
    EventData BLOB,
    Metadata BLOB,
    Version INTEGER NOT NULL,
    Timestamp TEXT NOT NULL,
    TenantId TEXT NOT NULL,
    UNIQUE(AggregateId, AggregateType, Version, TenantId)
);
```

`CREATE TABLE IF NOT EXISTS` does not alter an existing table, so a database created by an earlier version does not pick up later schema changes automatically — apply the numbered scripts in order for those.

## Concurrency

Appends are guarded by the `UNIQUE(AggregateId, AggregateType, Version, TenantId)` constraint, so two writers racing to append the same aggregate version cannot both succeed.

SQLite permits one writer at a time across the whole database. That is a good fit for a single process and a poor one for several competing for the same file, which is the point at which a server-backed provider is the right move.

## See Also

- [Data Providers Overview](./index.md) — Architecture and core abstractions
- [In-Memory Provider](./inmemory.md) — Non-durable alternative for unit tests
- [PostgreSQL Provider](./postgres.md) — Server-backed alternative with the full provider surface
- [Event Sourcing](../event-sourcing/index.md) — Event store concepts
