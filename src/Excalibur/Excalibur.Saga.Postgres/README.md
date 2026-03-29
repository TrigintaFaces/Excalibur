# Excalibur.Saga.Postgres

PostgreSQL implementation of saga state persistence for the Excalibur framework.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Postgres` | Complete | Everything for PostgreSQL: ES + Outbox + Inbox + Saga + LE + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.Postgres` for a production-ready PostgreSQL stack with a single package reference.

## Features

- JSONB storage for saga state with atomic upserts
- Configurable schema and table names
- Connection factory support for advanced scenarios
- ISagaBuilder.UsePostgres() fluent API
- ValidateOnStart with DataAnnotations

## Usage

```csharp
// Simple registration
services.AddPostgresSagaStore("Host=localhost;Database=myapp;");

// Via ISagaBuilder
services.AddExcaliburSaga(saga =>
{
    saga.UsePostgres("Host=localhost;Database=myapp;");
});

// With options
services.AddPostgresSagaStore(options =>
{
    options.ConnectionString = "Host=localhost;Database=myapp;";
    options.Schema = "dispatch";
    options.TableName = "sagas";
    options.CommandTimeoutSeconds = 30;
});
```
