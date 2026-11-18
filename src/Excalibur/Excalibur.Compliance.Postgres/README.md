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
run by executing **every** script shipped inside this package under `scripts/`, in numeric order:

```sh
for f in 001_*.sql 002_*.sql 003_*.sql 004_*.sql 005_*.sql 006_*.sql 007_*.sql; do
  psql -h host -U user -d database -f "$f"
done
```

Run them all. Each one is guarded — `IF NOT EXISTS` on every create, and the alters check before
they change anything — so the set is safe to re-run and safe against a database that already holds
some of the tables. Running a subset is the one thing that does not work, because the startup
verifier requires tables that only the later scripts create:

| Script | Creates |
|---|---|
| `001_CreateComplianceSchema.sql` | the `compliance` schema and `erasure_requests`, `erasure_certificates`, `erasure_destroyed_keys`, `erasure_destruction_intents`, `data_inventory_registrations`, `discovered_data_locations`, `legal_holds` |
| `002_ReplaceLegalBasisColumns.sql` | column changes only |
| `003_AddLegalHoldReleaseAndVersion.sql` | column changes only |
| `004_AddKeyDestructionLedger.sql` | `erasure_destroyed_keys`, `erasure_destruction_intents` — already made by 001, so this is a no-op on a fresh database and the upgrade path for one provisioned earlier |
| `005_AddKeyMintLedger.sql` | `erasure_minted_keys` |
| `006_AddDataSubjectTokenFingerprint.sql` | `erasure_token_fingerprint` |
| `007_AddRetentionDeclarationLedger.sql` | `erasure_retention_declarations` |

**The last two are required, not optional.** The erasure store will not start without
`erasure_minted_keys`: the erasure enumerates the key handles it destroys from that ledger rather
than re-deriving them, so a missing ledger would mean a handle holding the subject's data that
nothing destroys. And `erasure_token_fingerprint` is what refuses startup when the data-subject
hashing key has changed, which would otherwise orphan every pseudonymised record. Startup fails
fast and names the missing table, so a partial provision is loud rather than silent — but it does
mean the store never starts.

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

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
