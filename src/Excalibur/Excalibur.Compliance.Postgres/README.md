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

## Requirements

- Postgres 12+
- Npgsql driver
