# Excalibur.Outbox.Postgres

Postgres implementation of the transactional outbox pattern for reliable message delivery.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Dispatch.Postgres` | Starter | + Dispatch core + Outbox + Hosting |
| `Excalibur.Postgres` | Complete | + Inbox + Saga + Leader Election + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.Postgres` for a production-ready PostgreSQL stack with a single package reference.

## Schema

The store does **not** create its tables at runtime. Provision them before the first message is
staged, by running the script shipped inside this package under `scripts/`:

```text
scripts/001_CreateOutboxSchema.sql
```

Run it against your target database:

```sh
psql -h host -U user -d database -f 001_CreateOutboxSchema.sql
```

It creates three tables, using the default names:

| Table | Purpose |
|---|---|
| `outbox` | Staged messages awaiting delivery |
| `outbox_dead_letters` | Messages that exhausted their retries |
| `outbox_fence` | Durable leadership high-water mark, kept out of the message table so draining cannot lower it |

If you override `SchemaName`, `OutboxTableName`, `DeadLetterTableName`, or `FenceTableName`,
rename the corresponding objects in the script to match.

Every statement is guarded with `IF NOT EXISTS`, so the script is safe to re-run.

## Postgres specifics

- All timestamp columns are `TIMESTAMPTZ`. A column created `WITHOUT TIME ZONE` is shifted by the
  session timezone on reload, so a scheduled message would become due at the wrong instant.
- `tenant_id` is `NOT NULL DEFAULT '__untenanted__'`. An untenanted message stores the reserved
  sentinel, not `NULL`, so there is exactly one way to say a message has no tenant and a scoped
  predicate always compares a value against a value. The staging path binds the term explicitly,
  so the default is a backstop for hand-written `INSERT`s.
- A database provisioned by an earlier prerelease has no in-place upgrade path; re-provision it
  from the shipped schema script.
- `outbox_fence.scope_key` must keep its primary key — the fenced claim and fenced delete upsert
  with `ON CONFLICT (scope_key)`, which requires a matching unique constraint.

## Enqueue payloads and message identity

`EnqueueAsync` serializes the message's runtime type using the framework's canonical JSON
contract. It keeps PostgreSQL's existing envelope framing and propagated headers, and uses
`context.GetDestination()` when supplied; otherwise the destination is the message's
namespace-qualified type name.

The owning store exposes `IOutboxPayloadTransformStore` for decorators such as
`AddOutboxEncryption`. The provider prepares the envelope before the transform runs; the
transform receives its exact serialized payload, prepared tenant and cancellation token.
A failed, canceled or null-result transform accepts no row. The provider copies a successful
result before persistence. Encryption decorators mediate this capability rather than
exposing an unencrypted write path. Register all stores before adding encryption.

Message identity follows these rules:

- On `EnqueueAsync`, a null or empty context message ID requests a new identity. A nonempty
  ID consisting only of whitespace is rejected before payload serialization or persistence.
- A nonblank supplied ID is opaque and preserved exactly, including surrounding spaces.
  The provider captures it before serialization so a message getter cannot change the
  identity used in the persisted row or its framing header.
- Both `StageMessageAsync` overloads and `SaveMessagesAsync` require an explicit nonblank
  ID. Invalid IDs are rejected before accepting a write. `SaveMessagesAsync` validates the
  entire input collection first and uses each captured, validated identity for its insert.

Collection validation does not make a save batch transactional. Once database writes begin,
backend failures retain the existing per-message behavior and can leave earlier valid rows
persisted. A stage using an external `NpgsqlTransaction` borrows the caller's connection and
transaction; the caller remains responsible for commit or rollback. A successful write in
that transaction is provisional until the caller commits.

The convenience enqueue path still uses reflective payload serialization and retains its
trimming and dynamic-code annotations. It is not certified for NativeAOT.

Pre-serialized `StageMessageAsync` avoids payload serialization, but this provider still
encodes envelope headers through its legacy JSON metadata path. PostgreSQL staging does not
currently expose a generated-metadata configuration contract; it is not a NativeAOT
workaround or certification. Verify trimming and NativeAOT requirements against the full
application path.

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
