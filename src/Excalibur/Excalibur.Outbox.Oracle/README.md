# Excalibur.Outbox.Oracle

Oracle Database implementation of the transactional outbox pattern for reliable, at-least-once
message delivery.

## Part Of

The Excalibur application framework — messaging durability providers. Sibling of
`Excalibur.Outbox.SqlServer` and `Excalibur.Outbox.Postgres`.

## Usage

```csharp
services.AddExcalibur(x => x.AddOutbox(outbox =>
{
    outbox.UseOracle(oracle =>
    {
        oracle.ConnectionString("User Id=app;Password=***;Data Source=localhost:1521/FREEPDB1")
              .ReservationTimeout(TimeSpan.FromMinutes(5))
              .MaxAttempts(5);
    })
    .EnableBackgroundProcessing();
}));
```

A connection can also be supplied with `ConnectionStringName(...)` to resolve it from
configuration, or with `ConnectionFactory(...)` to reuse an existing `IDb`.

## Schema

The store does **not** create its tables at runtime. Provision them before the first message is
staged, by running the script shipped inside this package under `scripts/`:

```text
scripts/001_CreateOutboxSchema.sql
```

Run it against your target schema:

```sh
sqlplus user/password@//host:1521/service @001_CreateOutboxSchema.sql
```

It creates three tables, using the default names:

| Table | Purpose |
|---|---|
| `OUTBOX` | Staged messages awaiting delivery |
| `OUTBOX_DEAD_LETTERS` | Messages that exhausted their retries |
| `OUTBOX_FENCE` | Durable leadership high-water mark, one row per fencing scope |

All three are required. The fence table is deliberately separate from `OUTBOX`: a successful
drain deletes the message rows it sent, so a high-water mark stored on those rows would be
lowered by draining, and a superseded leader's stale token would be accepted afterwards.

If you override `SchemaName`, `OutboxTableName`, `DeadLetterTableName`, or `FenceTableName`,
rename the corresponding objects in the script to match.

## Oracle specifics

- The claim path opens a `FOR UPDATE SKIP LOCKED` cursor rather than using `FETCH FIRST`:
  Oracle rejects the two in one statement (ORA-02014), so the batch cap is applied as the
  cursor is fetched.
- Re-running the schema script against an existing table raises ORA-00955 (name already used),
  which is safe to ignore — Oracle has no `CREATE TABLE IF NOT EXISTS`.
- `tenant_id` is `DEFAULT '__untenanted__' NOT NULL` (Oracle requires `DEFAULT` before
  `NOT NULL`). An untenanted message stores the reserved sentinel, not `NULL`, so there is
  exactly one way to say a message has no tenant.
- The sentinel is deliberately non-empty. Oracle folds the empty string to `NULL`, so an
  empty-string sentinel would collapse straight back into the `NULL` the column exists to
  eliminate.
- A database created while `tenant_id` was nullable is converged by
  `002_MakeOutboxTenantTotal.sql`, which backfills `NULL` to the sentinel before applying the
  constraint. **Run it with the processor stopped, and deploy this package version first** — the
  older package binds a raw null tenant and would fail the new constraint with ORA-01400.

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
