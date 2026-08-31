# Excalibur.EventSourcing.Oracle

Oracle Database implementations of the Excalibur event-sourcing stores.

Provides:

- `OracleEventStore` — `IEventStore` with optimistic concurrency (read-current-version-then-compare inside a serializable transaction), atomic append, and GDPR erasure.
- `OracleSnapshotStore` — `ISnapshotStore` with `MERGE`-based upsert semantics.

Data access uses Dapper over `Oracle.ManagedDataAccess.Core` (ODP.NET). No EntityFramework.

## Schema

The stores do **not** create their tables at runtime. Provision them before the first append, by
running the scripts shipped inside this package under `scripts/`:

| Script | Creates | Required |
|---|---|---|
| `scripts/003_CreateEventStoreSchema.sql` | `EVENTSTOREEVENTS` | Yes — this is the event store itself |
| `scripts/001_CreateSnapshotSchema.sql` | `EVENTSTORESNAPSHOTS` | Only if you enable snapshots |
| `scripts/002_MigrateSnapshotsToKeyedSentinel.sql` | — | Upgrade only, see below |
| `scripts/004_MakeEventTenantTotal.sql` | — | Upgrade only, see below |

In Oracle a schema is a user. Run the scripts while connected **as** the user named by
`OracleEventStoreOptions.Schema` (default `EXCALIBUR`), or switch first with
`ALTER SESSION SET CURRENT_SCHEMA = EXCALIBUR`. The objects are created unqualified, so connecting
as a different user without switching creates them where the store will not look for them.

```sh
sqlplus excalibur/password@//host:1521/service @003_CreateEventStoreSchema.sql
```

Oracle has no `CREATE TABLE IF NOT EXISTS`, so re-running a create script raises ORA-00955 (name
already used), which is safe to ignore.

`002_MigrateSnapshotsToKeyedSentinel.sql` is an upgrade for databases whose snapshot table predates
the keyed tenant sentinel. It is not needed for a fresh install.

`004_MakeEventTenantTotal.sql` is the same upgrade for the EVENT table: it backfills
`EVENTSTOREEVENTS.TENANTID` from `NULL` to the reserved `__untenanted__` sentinel and then makes the
column `NOT NULL`, so an untenanted event is a value rather than a missing one. It is not needed for
a fresh install — `003` already creates the column that way.

Run it if your event store predates the sentinel. It closes a real concurrency hole as well as
tidying the representation: Oracle treats `NULL`s as distinct in a unique index, so while `TENANTID`
was nullable the stream-identity constraint did not constrain untenanted rows, and two appends at
the same version of the same untenanted stream could both succeed. Tenanted streams were never
affected. The script's STEP 0 reports any such duplicate that already exists; resolve those rows
before continuing, because only you can decide which append survives.

## Registration

```csharp
services.AddOracleEventStore(o =>
{
    o.ConnectionString = "User Id=excalibur;Password=...;Data Source=localhost:1521/FREEPDB1";
    o.Schema = "EXCALIBUR";
});
services.AddOracleSnapshotStore(o => o.ConnectionString = "...");
```

Options are validated at startup (`ValidateOnStart`).

## Driver license

This package depends on `Oracle.ManagedDataAccess.Core`, Oracle's own ODP.NET Core driver. It is
**not** distributed under an OSI-approved open-source license. Its `LICENSE.txt` opens:

> Your use of this Program is governed by the Oracle Free Distribution, Hosting, and Use Terms and
> Conditions set forth below, unless you have received this Program (alone or as part of another
> Oracle product) under an Oracle license agreement (including but not limited to the Oracle Master
> Agreement), in which case your use of this Program is governed solely by such license agreement
> with Oracle.

Excalibur redistributes no Oracle software and asserts nothing about your eligibility on your
behalf. Referencing this package makes NuGet install the driver into your application, so the
obligations are yours. Read the terms shipped in the driver package before you deploy, and confirm
your deployment is covered -- the free terms carry conditions that the MIT and PostgreSQL licenses
of Excalibur's other database drivers do not.

If those terms do not suit you, no other Excalibur provider carries them: the SQL Server, MySQL and
SQLite drivers are MIT and Npgsql is the PostgreSQL license. Every dependency's license is listed in
`THIRD-PARTY-NOTICES.md` in the repository.
