# Excalibur.Compliance.Postgres

Postgres implementation of GDPR compliance stores for the Excalibur framework.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Postgres` | Complete | Everything for PostgreSQL: ES + Outbox + Inbox + Saga + LE + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.Postgres` for a production-ready PostgreSQL stack with a single package reference.

## Features

- **Erasure Store** - Track and manage GDPR erasure requests with certificate persistence
- **Legal Hold Store** - Manage legal holds that block erasure under Article 17(3)
- **Data Inventory Store** - Maintain data location registrations and discovered personal data

## Quick Start

```csharp
services.AddPostgresErasureStore(options =>
{
    options.ConnectionString = "Host=localhost;Database=compliance;...";
});

services.AddPostgresLegalHoldStore(options =>
{
    options.ConnectionString = "Host=localhost;Database=compliance;...";
});

services.AddPostgresDataInventoryStore(options =>
{
    options.ConnectionString = "Host=localhost;Database=compliance;...";
});
```

## Schema

These stores verify their schema on startup and throw if it is absent. Provision it before first
run by executing the script shipped inside this package under `scripts/`:

```text
scripts/001_CreateComplianceSchema.sql
```

```sh
psql -h host -U user -d database -f 001_CreateComplianceSchema.sql
```

It creates the `compliance` schema and five tables: `erasure_requests`, `erasure_certificates`,
`data_inventory_registrations`, `discovered_data_locations` and `legal_holds`. Every statement is
guarded with `IF NOT EXISTS`, so the script is safe to re-run.

Setting `AutoCreateSchema = true` makes a store create its own tables on first use instead. That
is a convenience for development: it requires the application's own role to hold DDL rights, which
production deployments usually withhold deliberately, and it puts schema changes outside whatever
change control governs the database. On the erasure and legal-hold surfaces that is rarely the
right trade — prefer the script.

If you override `SchemaName` or any table name, rename the corresponding objects in the script to
match.

## Requirements

- Postgres 12+
- Npgsql driver
