# Excalibur.Postgres

Complete PostgreSQL metapackage for Excalibur. One package reference, one registration call -- everything you need to run a production Excalibur application on PostgreSQL.

## Quick Start

```csharp
services.AddExcaliburPostgres(pg =>
{
    pg.ConnectionString = "Host=...";
});
```

This registers: Dispatch core, PostgreSQL event sourcing, outbox, hosting, inbox, sagas, leader election, audit logging, compliance, and data access.

## Included Packages

| Package | Purpose |
|---|---|
| `Excalibur.Dispatch.Postgres` | Dispatch core + event sourcing + outbox + hosting |
| `Excalibur.Inbox.Postgres` | Idempotent message processing |
| `Excalibur.Saga.Postgres` | Long-running process managers |
| `Excalibur.LeaderElection.Postgres` | Multi-instance coordination |
| `Excalibur.Dispatch.AuditLogging.Postgres` | Audit trail persistence |
| `Excalibur.Compliance.Postgres` | GDPR/compliance features |
| `Excalibur.Data.Postgres` | Data access layer |

## Component Toggles

All components are enabled by default. Disable optional components via the options:

```csharp
services.AddExcaliburPostgres(pg =>
{
    pg.ConnectionString = connectionString;
    pg.UseLeaderElection = false; // skip leader election
    pg.UseCompliance = false;     // skip GDPR/compliance
});
```

## Tier Model

This is a **Complete** tier metapackage. See also:

- `Excalibur.Dispatch.Postgres` -- **Starter** tier (Dispatch + event sourcing + outbox + hosting only)
