# Excalibur.Inbox.SqlServer

SQL Server implementation of the inbox pattern for Excalibur.

## Part Of

This package is included in the following metapackages:

| Metapackage | Tier | What It Adds |
|---|---|---|
| `Excalibur.SqlServer` | Complete | Everything for SQL Server: ES + Outbox + Inbox + Saga + LE + Audit + Compliance + Data |

> **Tip:** Install `Excalibur.SqlServer` for a production-ready SQL Server stack with a single package reference.

## Schema

The store never creates its table at runtime. Provision it before the first message is
processed using the canonical DDL shipped in the package under `scripts/001_CreateInboxSchema.sql`.
Defaults: schema `dbo`, table `inbox_messages` (both configurable via `SqlServerInboxOptions`).

### Tenant-isolation key

Deduplication keys on `(MessageId, HandlerType)` and, when a tenant is ambient, additionally
on `TenantId`. The primary/unique key in the database **must** match your deployment mode:

| Deployment | Store MERGE match key | Required unique key |
|---|---|---|
| Single-tenant (no ambient tenant) | `(MessageId, HandlerType)` | `(MessageId, HandlerType)` |
| Multi-tenant (ambient `ITenantContext`) | `(MessageId, HandlerType, TenantId)` | `(MessageId, HandlerType, TenantId)`, `TenantId NOT NULL` |

In multi-tenant mode `TenantId` **must be `NOT NULL`** so two tenants sharing
`(MessageId, HandlerType)` do not collide on the pair key. The shipped DDL ships the
single-tenant pair key by default with the multi-tenant triple key as a documented alternative.
