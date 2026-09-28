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
services.AddExcalibur(x => x.AddSagas(saga =>
{
    saga.UsePostgres("Host=localhost;Database=myapp;");
}));

// With options
services.AddPostgresSagaStore(options =>
{
    options.ConnectionString = "Host=localhost;Database=myapp;";
    options.Schema = "dispatch";
    options.TableName = "sagas";
    options.CommandTimeoutSeconds = 30;
});
```

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.

## Database schema

**This package does not create its tables.** The DDL ships inside the `.nupkg` under `scripts/`, and
you apply it — so a schema change to your database is always something you did deliberately, never
something a version upgrade did to you while you were not looking.

| Script |
|---|
| `scripts/01-SagaSchema.sql` |

Apply them in filename order before starting the application, and again — for any new ones — before
starting a new version after an upgrade.

The `scripts/` folder lives inside the package, not in your build output. To read it:

```bash
# the package folder, then the scripts inside it
dotnet nuget locals global-packages --list
ls ~/.nuget/packages/excalibur.saga.postgres/<version>/scripts/
```

**The folder is authoritative.** If any list of scripts — including this one — disagrees with what is
actually in the package you installed, the package is right.
