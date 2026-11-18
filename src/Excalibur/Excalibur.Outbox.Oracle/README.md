# Excalibur.Outbox.Oracle

Oracle Database implementation of the transactional outbox pattern for reliable, at-least-once
message delivery.

## Part Of

The Excalibur application framework â€” messaging durability providers. Sibling of
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

## Worker connection ownership

String, named-string and bound-section registration creates a provider-local staging lifecycle and
fresh private worker connections. A pre-existing application `IDb` does not override that selection.
`BindConfiguration("OracleOutbox")` reads the section's `ConnectionString` into a private snapshot and
binds its Oracle options; diagnostics name missing keys without printing their values. Workers and
staging keep the same construction-time snapshot.

`ConnectionFactory(sp => applicationDb)` remains borrowed staging/direct access. To run polling workers
with this mode, also configure an explicit fresh worker factory:

```csharp
using System.Threading.Tasks;
using Excalibur.Data;
using Excalibur.Outbox;
using Excalibur.Outbox.Oracle;
using Microsoft.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;

public static class BorrowedOracleOutboxRegistration
{
    public static void Configure(IOutboxBuilder outbox, IDb applicationDb, string connectionString)
    {
        outbox.UseOracle(oracle => oracle
            .ConnectionFactory(_ => applicationDb)
            .WorkerConnectionFactory((_, _) =>
                ValueTask.FromResult(new OracleConnection(connectionString))));
    }
}
```

The callback transfers a new, closed, un-enlisted connection targeting the same logical outbox database.
Never return a cached, DI-owned or borrowed connection. The framework opens and disposes each returned
connection. Custom callback scopes remain alive through connection cleanup. Explicit worker factories
survive later primary connection selection and override the string-derived worker default.

A borrowed staging-only host is supported. Registering an election alone does not activate polling.
Enabling a worker without configured ownership fails at startup; manual publisher/processor construction
also validates its required capabilities. No first-message or borrowed-transaction fallback is used.
Caller-transaction Stage remains caller-owned. Provider-specific direct request/token-only APIs can have
provisional borrowed semantics and are not the strict worker completion capability.

Workers claim and buffer inside an exclusive local transaction, commit, then return messages. Strict
completion compares the captured acquisition with its live stored lease under the row lock; a stale
acquisition or expired lease returns `ClaimLost`, and a stale tenure returns `FenceRefused` first. Oracle
sent completion deletes the row and dead-letter completion archives then deletes, so later absent-row
reports return `MessageNotFound`. Sent deletes exactly one row; archive/delete must both affect exactly
one, otherwise the owned transaction is rolled back. Accepted failure reports preserve maximum observed
attempts and the requested backoff/floor on one post-lock server clock.

Owned operations resolve local physical base tables with Oracle NAME_RESOLVE and accessible object metadata
on that operation's private connection. Bare names, explicit current-schema names and local synonyms of
one table use the same versioned owner/table fence identity. The resolved quoted SQL target is used for
the operation; no cached CURRENT_SCHEMA or synonym spelling becomes authority. Views, remote synonyms,
unresolved names and identities exceeding the existing 600-byte key capacity fail before mutation.
Fencing keys are compared as exact UTF8 RAW bytes and lengths. The fence SCOPE_KEY column must have
binary persistent uniqueness semantics and an enabled validated single-column unique constraint; a
linguistic column collation is rejected. All workers must share the same selected fence table. Do not
mix previous configured-string/uppercase key versions with this version. Concurrent administrative DDL
(drop, rename or recreate) is outside the worker transaction protocol.

Ambient worker transactions are rejected rather than silently capturing caller work. A commit failure
has unknown durability and returns no successful claim/result. Transport delivery remains at-least-once.
Raw claim/completion NativeAOT verification is separate from serialization support. Oracle staging still
uses legacy header JSON encoding and has no generated-metadata configuration contract; this ownership
protocol is not a blanket native serialization or exactly-once guarantee.

## Enqueue payloads and binary persistence

`EnqueueAsync` uses the message's runtime type and the framework's canonical JSON contract.
It retains Oracle's envelope framing and propagated headers. A configured destination is
preserved; otherwise the namespace-qualified message type is used for routing.

Oracle enqueue requires a nonblank context message ID, including when using encryption.
Null, empty and all-whitespace IDs are rejected before serialization. Nonblank IDs remain
opaque and exact, including surrounding spaces. The provider captures the identity before
serialization and uses the same value for the row and its `MessageId` header.

The owning store supports `IOutboxPayloadTransformStore` for `AddOutboxEncryption` and other
mediating decorators. It prepares the envelope before invoking the transform once, with
its canonical payload, tenant and cancellation token. Input memory is borrowed read-only
during that invocation. The provider copies returned bytes before persistence. Callback
faults, null results and cancellation detected before persistence produce no write. Once a
database command starts, existing cancellation and commit-uncertainty semantics remain in
effect. Register stores before adding encryption; the encryption decorator does not expose
a raw transform writer.

Payload parameters are explicitly bound as Oracle BLOBs on both normal inserts and scheduled
writes. An empty byte array persists as a non-null, zero-length BLOB, distinct from SQL NULL;
nonempty binary data remains byte-exact. Dead-letter moves copy that BLOB without converting
it to text. Known request parameter replacements remain observed at execution and must keep
the constructor-defined CLR representation (`byte[]` for `MessageBody`).

Caller-supplied `OracleTransaction` staging still borrows the transaction and connection.
The caller commits or rolls back; a staged write is provisional until commit. Payload
transformation does not change worker ownership or make delivery exactly-once.

Convenience enqueue retains reflective serialization and its trimming/dynamic-code annotations.
Pre-serialized staging avoids payload serialization but still uses legacy header encoding.
Neither route is advertised as a generated-metadata NativeAOT workaround; verify the full
application's supported path.

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

- Dead-letter moves archive the message and delete the source row atomically, retaining its tenant
  attribution. `MarkDeadLetteredAsync` reports `Applied` for a completed move; a missing or already
  moved identifier reports `MessageNotFound`. `MoveToDeadLetter` returns 1 for a completed move and
  0 otherwise; its batch counterpart sums those move counts.
- The claim path opens a `FOR UPDATE SKIP LOCKED` cursor rather than using `FETCH FIRST`:
  Oracle rejects the two in one statement (ORA-02014), so the batch cap is applied as the
  cursor is fetched.
- Re-running the schema script against an existing table raises ORA-00955 (name already used),
  which is safe to ignore â€” Oracle has no `CREATE TABLE IF NOT EXISTS`.
- `tenant_id` is `DEFAULT '__untenanted__' NOT NULL` (Oracle requires `DEFAULT` before
  `NOT NULL`). An untenanted message stores the reserved sentinel, not `NULL`, so there is
  exactly one way to say a message has no tenant.
- The sentinel is deliberately non-empty. Oracle folds the empty string to `NULL`, so an
  empty-string sentinel would collapse straight back into the `NULL` the column exists to
  eliminate.
- A database provisioned by an earlier prerelease has no in-place upgrade path; re-provision it
  from the shipped schema script.

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

## Dead-letter retirement

Retirement records the absolute observed logical send count and preserves a greater
persisted count. Administrative moves preserve the existing count because they perform
no delivery. Marking a message sent deletes its outbox row, so later retirement cannot
archive it. Reservation reloads retain both the retry count and last error.

## License

This project is multi-licensed under:
- [Excalibur License 1.1](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](https://github.com/TrigintaFaces/Excalibur/blob/main/licenses/LICENSE-SSPL-1.0.txt)

See [LICENSE](https://github.com/TrigintaFaces/Excalibur/blob/main/LICENSE) for details.
