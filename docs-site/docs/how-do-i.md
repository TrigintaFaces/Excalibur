---
sidebar_position: 3
title: How do I…?
description: Find a shipped capability by the problem you are trying to solve, not by the subsystem that owns it.
---

# How do I…?

Every page in these docs is filed under the subsystem whose code it belongs to. That is the right way to
*maintain* documentation and a poor way to *find* it — because you usually arrive with a **problem**, not a
subsystem name.

This page is the other index. Find the row that sounds like your question; the link goes to whichever
subsystem happens to own the answer.

:::tip If you are about to build something, look here first
The capability you need is often already shipped and documented under a subsystem you were not reading. That
is the specific failure this page exists to prevent.
:::

## Databases and schema

| I need to… | Use | Owned by |
|---|---|---|
| Create or upgrade a database schema | [Schema migrations](./event-sourcing/migrations.md) — the `excalibur-migrate` CLI (`up` / `down` / `status` / `script`). It is a **generic runner**: you give it a provider, a connection string, and the **assembly + namespace** holding the migration scripts — so it is not limited to event sourcing. The audit-logging and compliance SQL Server packages embed their scripts as resources, so they can be applied this way | Event sourcing (the tool), any package (the scripts) |
| Verify a deployed table matches the shape the code expects, before starting the app | [Schema migrations → status](./event-sourcing/migrations.md) | Event sourcing |
| Generate SQL for a DBA to review instead of applying it from the app | [Schema migrations → script](./event-sourcing/migrations.md) | Event sourcing |
| Get the exact DDL for a specific store | [Event store setup](./configuration/event-store-setup.md) · [Outbox](./patterns/outbox.md) · [Inbox](./patterns/inbox.md) · [Audit logging](./compliance/audit-logging.md) | Per subsystem |
| Add a tenant column to an existing single-tenant table without losing rows | [Multi-tenancy → migrating an existing table](./multi-tenancy.md) | Multi-tenancy |

## Running more than one instance

| I need to… | Use | Owned by |
|---|---|---|
| Stop two instances doing the same scheduled work | [Leader election](./leader-election/index.md) — and read its caveat: leadership is *at-most-one bounded by lease expiry plus grace*, **not** a distributed lock | Leader election |
| Make a leader-only write safe against a superseded leader | [Leader election → fencing](./leader-election/index.md) — carry the fencing token into the write and have the store reject a stale one | Leader election |
| Drain an outbox from multiple instances without double-sending | [Outbox](./patterns/outbox.md) — draining is fenced automatically | Outbox |

## Not processing the same message twice

| I need to… | Use | Owned by |
|---|---|---|
| Suppress duplicate deliveries from a transport | [Inbox](./patterns/inbox.md) — **effectively-once**: exactly-once for concurrent redelivery, at-least-once across a crash, so handlers must be idempotent | Inbox |
| Publish reliably when my database commit succeeds but the broker might not | [Outbox](./patterns/outbox.md) — at-least-once delivery | Outbox |
| Handle a message that keeps failing | [Dead letter handling](./patterns/dead-letter.md) | Error handling |

## Tenant isolation

| I need to… | Use | Owned by |
|---|---|---|
| Understand what "tenant-scoped" actually guarantees on a given store | [Multi-tenancy](./multi-tenancy.md), then the per-subsystem architecture contract in that package's source tree | Multi-tenancy |
| Know whether a read is refused by the database or filtered in-process | The subsystem's `ARCHITECTURE.md` — the distinction is stated per provider, because under client-side filtering the row enters your process before being discarded | Per subsystem |
| Scope audit reads to one tenant | [Audit logging](./compliance/audit-logging.md) — search and count are ambient-scoped; **retrieval by id is not** | Audit logging |

## Compliance and data subject requests

| I need to… | Use | Owned by |
|---|---|---|
| Delete a person's data on request | [GDPR erasure](./compliance/gdpr-erasure.md) — cryptographic erasure | Compliance |
| Produce an audit trail an auditor will accept | [Audit logging](./compliance/audit-logging.md) | Compliance |
| Provision the compliance tables | [Schema migrations](./event-sourcing/migrations.md) — **not** a compliance page. The tooling is shared and filed under event sourcing; point it at the compliance package's embedded scripts | Event sourcing (the tool) |
| Control who can read audit events | [Authorization](./security/authorization.md) · [Audit logging → RBAC](./compliance/audit-logging.md) | Security |

## Testing and verification

| I need to… | Use | Owned by |
|---|---|---|
| Verify my own provider implementation satisfies a contract | [Testing](./advanced/testing.md) — the shipped conformance kits. **The arms carry no test attributes**: each one runs only when you declare an attributed wrapper for it, so count your wrappers before citing coverage | Testing |
| Test against real infrastructure rather than mocks | [Testing](./advanced/testing.md) — container fixtures | Testing |

## Operating it in production

| I need to… | Use | Owned by |
|---|---|---|
| See what the framework is doing at runtime | [Observability](./observability/index.md) · [Diagnostics](./diagnostics/index.md) | Observability |
| Run day-to-day operational tasks | [Operations](./operations/index.md) | Operations |
| Deploy to a specific host or platform | [Deployment](./deployment/index.md) | Deployment |

---

## If your question is not here

That is a documentation gap worth reporting, and it is a **more useful** report than it sounds: the capability
probably exists. Two things that help you find it yourself:

- **Search for the behaviour, not the noun.** Searching for the subsystem name finds only pages that already
  know they belong to it. Searching for what you want to *happen* — "verify schema", "reject stale token",
  "suppress duplicate" — crosses subsystem boundaries.
- **Check the package's `ARCHITECTURE.md`** for guarantee-critical subsystems (outbox, inbox, event store,
  saga, leader election). It states the guarantee in falsifiable terms, how it is achieved, and which
  conformance test would go red if it broke — including gaps documented as **UNVERIFIED** rather than asserted.
