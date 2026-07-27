# Excalibur.Inbox.Postgres

Postgres implementation of the inbox pattern for idempotent message processing.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.Postgres` | Complete | Everything for PostgreSQL: ES + Outbox + Inbox + Saga + LE + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.Postgres` for a production-ready PostgreSQL stack with a single package reference.

## Schema

The store never creates its table at runtime. Provision it before the first message is
processed using the canonical DDL shipped in the package under `scripts/001_CreateInboxSchema.sql`.
Defaults: schema `public`, table `inbox_messages` (both configurable via `PostgresInboxOptions`).

### Tenant-isolation key

Deduplication keys on `(message_id, handler_type)` and, when a tenant is ambient, additionally
on `tenant_id`. The unique key in the database **must** match your deployment mode:

| Deployment | Store `ON CONFLICT` target | Required unique key |
|---|---|---|
| Single-tenant (no ambient tenant) | `(message_id, handler_type)` | `(message_id, handler_type)` |
| Multi-tenant (ambient `ITenantContext`) | `(message_id, handler_type, tenant_id)` | `(message_id, handler_type, tenant_id)`, `tenant_id NOT NULL` |

In multi-tenant mode `tenant_id` **must be `NOT NULL`**: a nullable column in the triple key
lets pre-15 Postgres treat `NULL`s as distinct, so `ON CONFLICT` never fires and duplicates
slip through. The shipped DDL ships the single-tenant pair key by default with the multi-tenant
triple key as a documented alternative.
