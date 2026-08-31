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
- A database created while `tenant_id` was nullable is converged by
  `002_MakeOutboxTenantTotal.sql`, which backfills `NULL` (and blank) to the sentinel before
  applying the constraint. **Run it with the processor stopped, and deploy this package version
  first** — the older package binds a raw null tenant and would fail the new constraint.
- `outbox_fence.scope_key` must keep its primary key — the fenced claim and fenced delete upsert
  with `ON CONFLICT (scope_key)`, which requires a matching unique constraint.
