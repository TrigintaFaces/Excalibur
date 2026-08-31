# Excalibur.LeaderElection.Postgres

PostgreSQL implementation of leader election for the Excalibur framework. Uses advisory locks (`pg_try_advisory_lock`) for distributed coordination with automatic failover and session-based locking.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Postgres` | Complete | Everything for PostgreSQL: ES + Outbox + Inbox + Saga + LE + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.Postgres` for a production-ready PostgreSQL stack with a single package reference.

## Features

- **Advisory lock-based leader election** -- lightweight, session-scoped locks that auto-release on connection loss
- **Health-based leader election** -- extends standard LE with health-aware candidate tracking and voluntary step-down
- **Factory pattern** -- create multiple independent leader elections with different lock keys
- **Telemetry integration** -- OpenTelemetry metrics and traces via `TelemetryLeaderElection` decorator
- **Health checks** -- ASP.NET Core health check integration

## Quick Start

```csharp
services.AddPostgresLeaderElection(options =>
{
    options.ConnectionString = "Host=localhost;Database=myapp;";
    options.LockKey = 12345;
});
```

Or using the builder pattern:

```csharp
services.AddExcalibur(x => x.AddLeaderElection(builder =>
{
    builder.UsePostgres(options =>
    {
        options.ConnectionString = connectionString;
    });
}));
```

## Schema

Advisory-lock leader election needs **no table** — the lock lives in the PostgreSQL lock manager.

The health-based variant records candidate health in a table, which it creates automatically on
first use. For a deployment that provisions schema separately, or runs without table-creation
rights, the canonical DDL ships in the package as
`scripts/001_CreateLeaderElectionHealthSchema.sql`. It is derived from the statement the store
issues at runtime, so a database provisioned either way has the same shape. Defaults: schema
`public`, table `leader_election_health` (both configurable via
`PostgresHealthBasedLeaderElectionOptions`).

The script only ever creates the table if it is missing; it does not alter an existing one.
