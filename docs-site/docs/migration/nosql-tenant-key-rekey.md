---
sidebar_position: 7
title: Cosmos DB, DynamoDB, Firestore and MongoDB Keys Carry the Tenant
description: The event stores and saga stores on the four document providers compose the owning tenant into the stored key. Documents written by an earlier version are unaddressable, the store refuses rather than reading them back as empty, and this is the one-time re-key procedure.
---

# Cosmos DB, DynamoDB, Firestore and MongoDB Keys Carry the Tenant

**On the four document providers, the owning tenant is now part of the stored key.** This applies to
the **event stores** and to the **saga stores**. A document written by an earlier version has no tenant
segment, so no key this version composes can address it.

**Read this before you upgrade, not after.** Nothing is deleted and nothing is rewritten — but a
deployment that upgrades with data in place stops being able to reach that data, and on the saga side
the failure mode without the guard is a saga re-run from the beginning.

**This page matters to you only if you store events or sagas on Cosmos DB, DynamoDB, Firestore or
MongoDB.** The relational stores (SQL Server, PostgreSQL, Oracle) are unaffected: their tenant column
already exists and is part of the primary key. Redis, SQLite and the in-memory stores are unaffected.
If you are adopting one of the four for the first time, there is nothing to do.

## Why the tenant had to move into the key

A tenant term in a *filter* confines reads while leaving both tenants sharing one document and one
version counter. Aggregate identifiers and saga correlation keys come from your domain — an order
number, a customer reference — so two tenants arrive at the same identifier as a matter of course.
With one document between them:

- the second tenant to use the identifier is told it has a **concurrency conflict on a stream it never
  wrote**, and can never create its own;
- the check that correctly refuses a cross-tenant overwrite is the same check that denies the second
  tenant a record of its own. The isolation control degenerates into an estate-wide uniqueness
  constraint on the identifier.

With the tenant in the key, each tenant has its own document, a cross-tenant read is *unaddressable*
rather than filtered out, and each tenant gets its own version sequence as a consequence rather than as
a second mechanism.

There is deliberately **no read-side fallback to the old key**. A store that tried the new key and then
the old one would keep both shapes live indefinitely, and the guarantee this exists to provide would be
unverifiable for as long as the fallback remained.

## The key shapes

Event streams:

| Provider | Where the key lives | Shape |
| --- | --- | --- |
| Cosmos DB | `streamId`, which is also the partition key | `t:{tenantId}:{aggregateType}:{aggregateId}` |
| DynamoDB | partition key | `t:{tenantId}:{aggregateType}:{aggregateId}` |
| Firestore | document id | `t:{tenantId}:{aggregateType}:{aggregateId}:{version}` |
| MongoDB | `streamId` | `t:{tenantId}:{aggregateId}` |

Sagas:

| Provider | Where the key lives | Shape |
| --- | --- | --- |
| Cosmos DB | `id`; the partition key remains the saga type | `t:{tenantId}:{sagaId}` |
| DynamoDB | `PK`; the sort key remains the saga type | `SAGA#t:{tenantId}:{sagaId}` |
| Firestore | document id | `t:{tenantId}:{sagaId}_{sagaType}` |
| MongoDB | `_id` | `t:{tenantId}:{sagaId}` |

**The tenant segment is always present.** A host that never enables multi-tenancy resolves the
framework single-tenant default `__default__`; a genuinely untenanted deployment resolves the reserved
untenanted value `__untenanted__`. There is no key without a tenant segment, so "I don't use
multi-tenancy" does not exempt you from this page.

**Only the event and saga documents change.** The **snapshot** stores on these four backends have
composed the tenant into the document id since an earlier release, so you already hold tenant-keyed
snapshots. They need no migration: a snapshot whose key misses is simply not found, and the aggregate
rebuilds from its event stream. An event stream that misses has nothing behind it to rebuild from —
which is why this one needs a procedure and that one did not.

## What happens if you upgrade without migrating

**The store refuses rather than reporting an absence it cannot vouch for.**

Each of the eight stores guards every point at which it would otherwise act on the *absence* of
documents — a load that came back empty, a version read that found nothing, a create that assumes
nothing is there. The first time one of those is reached, the store checks its configured
collection or table for a document whose key carries no tenant segment. Finding one, it throws
`InvalidOperationException` naming the collection and the offending key, and **modifies nothing**.

```
Events container 'events' holds at least one event document whose stream identifier
('Order:A-1001') carries no tenant segment, so it was written by a release that stored
streams without one. Those documents are unaddressable under the current key shape: a load
of the aggregate they belong to would return an empty stream, and the caller would then
append a second, disjoint history under the same identity. Nothing has been modified. ...
```

What that replaces is the worse outcome, and it is worth being explicit about because it is the reason
the guard exists:

| Without the guard | With it |
| --- | --- |
| The load returns an **empty stream**. Your code reads that as a new aggregate, appends at version 0, and you end holding **two disjoint histories under one identity** while the store still holds the first. | The first read that would have lied fails, with every event intact and nothing written. |
| A saga load returns **nothing**. Your coordinator reads that as *no saga in flight*, starts it again, and **re-fires every compensating action and external call it already performed**. | The saga host fails to start the saga rather than duplicating its effects. |

**The check costs you nothing in normal operation.** It is not on the startup path — probing there
would spend a request on every process start, and on every serverless cold start, forever, to detect a
condition that can only hold across a one-time upgrade. A read that returns documents proves the
collection is addressable and is never probed. Only silence is ambiguous, and only silence is checked,
at most once per store instance.

### Two limits of the guard, stated plainly

- **DynamoDB detects this with a single filtered `Scan` page**, because it has no ordered access across
  partitions to express the check as a range read. A table upgraded in place carries the old shape on
  *every* item, so the first page cannot miss it, and stopping at one page keeps a large correctly-keyed
  table from paying for a full scan. A table holding **both** shapes with the old one only beyond the
  first page — which takes a partial rollback to produce — is **not** detected.
- **On the MongoDB event store, one path reports the refusal as a failed result rather than as a
  throw**: an append at the head of a stream issued with no preceding load. That store's append
  flattens any exception into a failed `AppendResult`, so the refusal arrives with `Success` false
  carrying the same message. Nothing is written and no history is split either way; the
  load-then-append flow every repository uses refuses from the load, which throws.

## Before you upgrade

### Sagas: drain if you can

A saga is transient in a way an event stream is not — it exists until it completes. If you can stop
accepting new work and let in-flight sagas finish, that is by far the cheapest migration:

1. Stop accepting the messages that start sagas.
2. Let in-flight sagas run to completion or timeout.
3. Upgrade and restart.

Completed sagas are retained in the store after they finish, so draining leaves their documents behind on
the old key. **The refusal still fires on them** — the check looks at the shape of a key, and cannot tell
a completed saga's document from an in-flight one. So draining is not by itself enough: once you no
longer need those records, delete them or move them to another collection, or re-key them with the
procedure below.

### Events, and sagas you cannot drain: re-key

**There is no migration tool, and one cannot be written honestly for the general case.** Deciding which
tenant an existing untenanted document belongs to is a question about your deployment, not about the
data. Per collection or table:

1. **Stop writers.** The re-key is not safe against a live writer.
2. **Export every document.** For events, preserve `version` order within each stream.
3. **Re-key each document** by prefixing the tenant segment to the existing key, following the shape
   tables above. On DynamoDB sagas the tenant segment goes *after* the `SAGA#` prefix, not before it.
4. **Re-import**, then verify: load one aggregate per tenant and check the event count matches the
   export; load one saga per tenant and check its state and version.

**Copy the tenant value from the framework rather than retyping it.** If you ran single-tenant, use
`TenantDefaults.DefaultTenantId`; if you never enabled ambient tenancy at all, use
`TenantScope.UntenantedSentinel`. Both are public constants:

```csharp
using Excalibur.Dispatch;   // TenantDefaults, TenantScope

// Single-tenant deployment that opted into ambient tenancy:
var tenantSegment = $"t:{TenantDefaults.DefaultTenantId}:";   // t:__default__:

// Deployment with no ambient tenancy at all:
var tenantSegment = $"t:{TenantScope.UntenantedSentinel}:";   // t:__untenanted__:
```

A mistyped variant strands every row in a partition nothing queries, **and nothing reports it** — the
key is well-formed, it simply names a tenant that never asks.

### Or: start clean

If you can afford to rebuild your read models, point the provider at a **fresh collection or table** and
leave the old one in place. The old documents stay readable by the earlier package version, so this is
reversible in a way an in-place re-key is not.

## Registering these stores in a multi-tenant host

**It now starts.** Each of these registrations supplies the ambient tenant to the store and declares the
capability in the same act, so the multi-tenancy startup check passes under both isolation strategies
and in either registration order. Under sharding, routing each tenant to a distinct physical store is
the *physical* half of separation and the key is the *logical* half — so a shard map that points two
tenants at the same database is still safe, because the store contributes its own tenant term.

A single-tenant host is unaffected by the isolation change itself: there is one partition, so there is
nothing to cross. It is **not** unaffected by the key change — see the sentinel note above.

## See also

- [Event sourcing providers](../event-sourcing/providers.md#upgrading-the-four-document-providers)
- [Sagas → Persistence providers](../sagas/index.md#persistence-providers)
- [Multi-tenancy](../multi-tenancy.md)
- [Before you upgrade](../whats-new.md#before-you-upgrade)
