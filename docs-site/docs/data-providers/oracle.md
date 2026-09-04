---
sidebar_position: 13
title: Oracle
description: Oracle Database provider for event sourcing, outbox, inbox, and saga persistence using Dapper and Oracle.ManagedDataAccess.
---

# Oracle Provider

The Oracle provider brings Excalibur's reliable-persistence subsystems to [Oracle Database](https://www.oracle.com/database/). It offers Dapper-based event sourcing, outbox, inbox, and saga stores over the managed `Oracle.ManagedDataAccess.Core` driver — behind the same store abstractions used by every other provider, so your application code is unchanged.

The Oracle packages are **opt-in**: install only the subsystems you use, and register each with its `AddOracle*` / `UseOracle` extension.

## Before You Start

- **.NET 10.0**
- An Oracle Database instance (local, cloud, or Autonomous Database)
- Familiarity with [event sourcing](../event-sourcing/index.md), the [outbox pattern](../patterns/outbox.md), the [inbox pattern](../patterns/inbox.md), and [sagas](../sagas/index.md)

## Packages

| Package | Registers | Subsystem |
|---------|-----------|-----------|
| `Excalibur.EventSourcing.Oracle` | `IEventStore`, `ISnapshotStore` | [Event sourcing](../event-sourcing/index.md) |
| `Excalibur.Outbox.Oracle` | Oracle outbox store | [Outbox pattern](../patterns/outbox.md) |
| `Excalibur.Inbox.Oracle` | `IInboxStore` | [Inbox pattern](../patterns/inbox.md) |
| `Excalibur.Saga.Oracle` | `ISagaStore`, saga timeout store | [Sagas](../sagas/index.md) |

**Common dependencies:** `Oracle.ManagedDataAccess.Core`, `Dapper`

:::caution The Oracle driver is not open source

`Oracle.ManagedDataAccess.Core` is Oracle's own ODP.NET Core driver and is **not** distributed under
an OSI-approved open-source license. Its `LICENSE.txt` opens:

> Your use of this Program is governed by the Oracle Free Distribution, Hosting, and Use Terms and
> Conditions set forth below, unless you have received this Program (alone or as part of another
> Oracle product) under an Oracle license agreement (including but not limited to the Oracle Master
> Agreement), in which case your use of this Program is governed solely by such license agreement
> with Oracle.

Excalibur redistributes no Oracle software and asserts nothing about your eligibility on your
behalf -- the same position it takes on the [QuestPDF license](../compliance/checklists/soc2.md).
Installing any of the four packages above makes NuGet install the driver into your application, so
the obligations are yours. Read the terms shipped inside the driver package and confirm your
deployment is covered before you ship.

Every other Excalibur database provider carries an OSI-approved driver license: SQL Server, MySQL
and SQLite are MIT, and Npgsql is the PostgreSQL license. Choosing a different provider is the
remedy if these terms do not suit you.

:::

## Event Sourcing

```bash
dotnet add package Excalibur.EventSourcing.Oracle
```

Register the event store and (optionally) the snapshot store with a connection factory:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;

services.AddOracleEventStore(
    () => new OracleConnection(connectionString),
    schema: "EXCALIBUR",
    table: "EVENTSTOREEVENTS");

services.AddOracleSnapshotStore(
    () => new OracleConnection(connectionString),
    schema: "EXCALIBUR",
    table: "EVENTSTORESNAPSHOTS");
```

Or configure via options, validated at startup:

```csharp
services.AddOracleEventStore(options =>
{
    options.ConnectionString = connectionString;
    options.Schema = "EXCALIBUR";
    options.Table = "EVENTSTOREEVENTS";
});
```

Batch appends are atomic — a multi-row append is all-or-nothing, so a mid-batch failure never leaves a torn event-stream prefix.

The event store writes each event's declared `[MessageName]` -- not its CLR type name -- to the stored event-type field, and resolves that name back to a CLR type through the registered event-type registry on read. See [Stable Message Names](../event-sourcing/domain-events.md#stable-message-names).

## Outbox

```bash
dotnet add package Excalibur.Outbox.Oracle
```

The Oracle outbox is registered through the outbox builder with `UseOracle`:

```csharp
services.AddExcalibur(x => x.AddOutbox(outbox =>
{
    outbox.UseOracle(oracle =>
    {
        oracle.ConnectionString(connectionString)
              .SchemaName("messaging")
              .TableName("outbox_messages")
              .ReservationTimeout(TimeSpan.FromMinutes(10))
              .MaxAttempts(5);
    })
    .EnableBackgroundProcessing();
}));
```

A connection can also be resolved from the container via `ConnectionFactory(sp => ...)`.

## Inbox

```bash
dotnet add package Excalibur.Inbox.Oracle
```

```csharp
services.AddOracleInboxStore(options =>
{
    options.ConnectionString = connectionString;
    options.SchemaName = "messaging";
    options.TableName = "inbox_messages";
});
```

An overload accepts a connection-factory provider (`Func<IServiceProvider, Func<OracleConnection>>`) when the connection is built from other registered services.

## Sagas

```bash
dotnet add package Excalibur.Saga.Oracle
```

```csharp
services.AddOracleSagaStore(options =>
{
    options.ConnectionString = connectionString;
    options.SchemaName = "sagas";
});

// Optional: durable saga timeouts
services.AddOracleSagaTimeoutStore(options =>
{
    options.ConnectionString = connectionString;
});
```

Completed sagas can be purged on a retention window via the saga automatic-cleanup background service (`SagaOptions.EnableAutomaticCleanup`), consistent with the other saga providers. See [Sagas → Retention & Cleanup](../sagas/index.md#retention--cleanup).

## SQL Injection Protection

Schema and table names supplied via configuration are validated and quoted, so they cannot be used to inject SQL — the same protection applied by the SQL Server and PostgreSQL providers.

## What's Next

- [Event Sourcing](../event-sourcing/index.md) — Aggregates, event stores, and snapshots
- [Outbox Pattern](../patterns/outbox.md) — Reliable message publishing
- [Sagas](../sagas/index.md) — Long-running process orchestration
