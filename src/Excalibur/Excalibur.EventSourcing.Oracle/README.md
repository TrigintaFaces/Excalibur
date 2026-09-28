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
| `scripts/001_CreateSnapshotSchema.sql` | `EVENTSTORESNAPSHOTS` | Only if you enable snapshots |
| `scripts/002_CreateEventStoreSchema.sql` | `EVENTSTOREEVENTS` | Yes — this is the event store itself |
| `scripts/003_CreateSubscriptionCheckpointSchema.sql` | `SUBSCRIPTIONCHECKPOINTS` | Only if you run catch-up subscriptions |

In Oracle a schema is a user. Run the scripts while connected **as** the user named by
`OracleEventStoreOptions.Schema` (default `EXCALIBUR`), or switch first with
`ALTER SESSION SET CURRENT_SCHEMA = EXCALIBUR`. The objects are created unqualified, so connecting
as a different user without switching creates them where the store will not look for them.

```sh
sqlplus excalibur/password@//host:1521/service @002_CreateEventStoreSchema.sql
```

Oracle has no `CREATE TABLE IF NOT EXISTS`, so re-running a create script raises ORA-00955 (name
already used), which is safe to ignore.

`TENANTID` is created `NOT NULL`, and an untenanted event stores the reserved `__untenanted__`
sentinel rather than `NULL`. That is what makes the stream-identity constraint bind untenanted rows:
Oracle treats `NULL`s as distinct in a unique index, so a nullable tenant term would leave two
appends at the same version of the same untenanted stream both able to succeed.

A database provisioned by an earlier prerelease has no in-place upgrade path; re-provision it from
these scripts.

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

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
