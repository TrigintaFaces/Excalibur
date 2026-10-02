---
sidebar_position: 23
title: Migration Guides
description: Upgrading an existing Excalibur install to 10.0.0, and step-by-step guides for migrating to Dispatch from MediatR, MassTransit, NServiceBus, and the ASP.NET eventing proposal.
---

# Migration Guides

Two different journeys land on this page. If you are **upgrading an existing Excalibur install**, start
with [Upgrading to 10.0.0](#upgrading-to-1000). If you are **arriving from another .NET messaging
library**, the guides under [Migrating from another library](#migrating-from-another-library) cover the
key differences, mapping tables, and step-by-step instructions.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch
  ```
- Familiarity with [Getting Started](../getting-started/index.md) and [Core Concepts](../core-concepts/index.md)

## Upgrading to 10.0.0

**Already using Excalibur and moving to 10.0.0? Start here.** The guides below this section are for
arriving from a *different* library; these two are for upgrading ours.

Read **[Before you upgrade](../whats-new.md#before-you-upgrade)** first — it collects every change that
requires you to act, including the schema columns to add and the APIs that were removed. Three of those
changes rewrite data you have already stored, and all three must be done **before** you deploy the new
package:

- **[Cosmos DB, DynamoDB, Firestore and MongoDB keys carry the tenant](nosql-tenant-key-rekey.md)** --
  The **event stores and saga stores** on those four providers compose the owning tenant into the stored
  key, so documents written by an earlier version are not addressable by this one. **The store refuses to
  serve rather than reading them back as empty**, so an unmigrated deployment fails at its first read
  instead of splitting an aggregate's history in two or re-running a saga from the beginning. This one
  applies **even if you never enabled multi-tenancy** — the key carries a reserved single-tenant or
  untenanted segment either way. Re-key, or start on a fresh collection.
- **[Authorization grants require a tenant](authorization-tenant-required.md)** -- Grants written with no
  tenant on Cosmos DB, DynamoDB, Firestore, or MongoDB were filed under a reserved literal that is part of
  the partition key or document id. Correcting one is a delete-and-reinsert. Each provider stores grants in
  two containers, and the guide names both.
- **[The default pipeline no longer seats middleware](default-pipeline-is-empty.md)** -- `AddDispatch()`
  used to add four middleware to every host; it now adds none, and you name the ones you want. **Almost
  every host is unaffected.** The one population that must act is narrow and specific: a host that
  **registers an `IOutboxStore` but never calls `UseOutbox()`**, because the previous version's own
  error text said a store registration alone was enough. Hosts that call `UseOutbox()` are unaffected,
  and hosts with no store were already failing the same way before this change.
- **[Global-stream reads stop at the first gap](global-stream-reads-stop-at-gaps.md)** -- a read of the global event stream now returns only the contiguous run from your position. This closes a silent event-skip, and introduces a stall you need to be able to recognise: a subscriber sitting on a permanently absent position stops advancing and the host still looks healthy. Read this if you run global-stream projections or subscriptions, or if you archived events on an older version.
- **[Firestore and Elasticsearch inbox keys change shape](inbox-document-id-rekey.md)** -- The document id
  identifying an inbox entry is composed differently, so entries written by an earlier version are not found
  by this one. Drain the inbox before upgrading, or re-key the existing entries.

One further change rewrites stored data, and it is the one to do **after** the rollout rather than
before:

- **[MongoDB outbox timestamps change shape](mongodb-outbox-instant-format.md)** -- The MongoDB outbox
  now stores instants as BSON dates. Delivery is unaffected, because the store reads both shapes. What
  does not carry over is TTL expiry of messages already marked sent before the upgrade: the expiry
  monitor skips the older shape silently, so a deployment relying on the TTL index alone retains them
  indefinitely. The retention sweep removes them either way. Rewrite them in place once every instance
  is running the new version.

One change needs a schema column before the store will run at all:

- **[A legal hold carries a concurrency token](legal-hold-concurrency-token.md)** -- `LegalHold.Version` makes
  updating a hold a compare-and-set, closing a lost update that could release a hold someone had just extended
  and let the next erasure destroy the records it protected. **Both shipped create scripts carry the column, so
  a freshly provisioned database is already correct.** A compliance database provisioned *before* it fails at
  startup naming the missing column, and **no `ALTER` script ships** -- `AutoCreateSchema` will not repair an
  existing table, because that path only creates tables that are absent. Re-provision, or add the column
  yourself. **Only hosts using legal holds are affected**, and a custom `ILegalHoldStore` needs code changes as
  well as schema: the method signature did not change, so it still compiles while keeping the lost update.

One change fails start-up until you answer it, and it touches no stored data:

- **[Activity-group grant sync is atomic, and four providers must opt in](activity-group-grant-sync-atomicity.md)**
  -- Synchronizing activity-group grants from a remote authority now replaces a set of grants in one step.
  SQL Server, PostgreSQL and the in-memory store do that natively; on Cosmos DB, DynamoDB, Firestore and
  MongoDB they cannot, so start-up fails until you accept the non-atomic sync explicitly. **Only hosts that
  call the `IActivityGroupService` sync methods are affected.** The same change makes an empty per-user
  payload revoke rather than be refused, and carries two provider fixes that made grant operations fail
  outright on SQL Server and grant inserts fail on PostgreSQL.

One change is a compile-level break in code that reads an append's returned version, and it touches no
stored data:

- **[An append result states its outcome](append-result-outcome.md)** -- `AppendResult` and
  `CloudAppendResult` gained an `Outcome` discriminator, so a recognised retry is no longer
  indistinguishable from a fresh write. **`Success` is unchanged**, so a host that only asks *did it work*
  needs no change. What stops compiling is a `long` receiving `NextExpectedVersion`, which is now `long?`.
  A conflict still reports the **measured** actual version and reports `null` only where no version read
  succeeded -- read the note before adding a null branch, because the two failures differ.

One change lands in your own test project rather than in your application, and it touches no stored data:

One change makes a monitored value go from healthy to never-synchronised, and the old value was fabricated:

- **[Multi-region replication honesty](multi-region-reports-no-replication.md)** -- `MultiRegionKeyProvider`
  recorded a successful key-replication instant, zeroed its pending-key backlog and logged completion on
  every call, **although no branch of its sync copies key material**. The recovery-point check then computed
  its lag from that instant and reported the target met, so a host configuring disaster recovery saw a met
  objective over a passive region holding no keys. It now reports no sync and keeps its backlog, and the
  recovery-point check returns early rather than computing from a fabricated instant. **An alert that fires
  after this upgrade is reporting something that was already true.** The same change makes the wrapped
  provider's capabilities reachable through the decorator -- notably the durable-key capability, whose
  absence made a cloud-backed deployment read as a volatile key store.

One change requires a schema migration before your application will start, and only on a SQL erasure store
-- and it **discards every historical destruction record**:

- **[Erasure destroyed-key record](erasure-destroyed-key-record.md)** -- The erasure now records which key
  **generations** it destroyed, so a retry can attest the coverage its first pass achieved and a read can
  report the subject's erasure at all. Before this, an erasure that destroyed a subject's key and then failed
  part-way could **never be reported complete** -- the key store reports an already-destroyed key as absent,
  which is the same answer it gives for a key that never existed, so the retry attested nothing for it.
  **The SQL Server and PostgreSQL erasure stores need two tables and refuse to start without them**, naming
  what is missing. A fresh database gets the final shape from the shipped `001` schema script; an existing
  one runs the shipped `004`/`005` ledger migration -- which **drops and recreates** the destroyed-keys
  table, because its old rows carry no generation and one cannot be back-filled. **Every past request then
  reports an empty destroyed-handle breakdown**; signed completion certificates are untouched and still
  verify, and the request's own keys-destroyed count survives. **Let in-flight erasures finish first.**
  `IErasureStore` gained two members, `RecordKeyDestroyedAsync` changed signature, and a store must now also
  implement `IKeyDestructionLedger`. Hosts on the in-memory store have nothing to do.

- **[Conformance kits gained arms](conformance-kit-arms-added.md)** -- The shipped saga-store,
  positioned-projection-store, key-management-provider and erasure-store conformance kits each gained arms. **Only hosts that derive a test suite from
  a `*ConformanceTestKit` are affected.** If your suite wires the kit's completeness guard it now fails,
  naming the members to add — that is the case that tells you. If it does not wire the guard, the new arms
  **silently never run** and your suite stays green over checks that did not execute, which is why the note
  recommends wiring the guard on every derived suite.

One change is a compile error only for hosts that write their own key-management provider:

- **[Provisioning a key is no longer a rotation](create-key-if-absent.md)** -- `IKeyManagementProvider`
  gained `CreateKeyIfAbsentAsync`, because minting a data subject's key on the write path was built on
  `RotateKeyAsync`, which is create-**or**-rotate: two ordinary concurrent first writes for one new subject
  retired each other's key version, with no rotation requested by anyone. **Only hosts that implement
  `IKeyManagementProvider` themselves are affected** — every provider we ship implements the member, and no
  method you call changed signature. The note carries the contract an implementation owes, including what
  atomicity a backend can honestly promise.

One change makes field-encrypted data at rest unreadable, and it is the one to read first:

- **[Field envelope names its key generation](field-envelope-names-its-key-generation.md)** -- The crypto-shredding field envelope gained a
  key generation and a format version, and **ciphertext written by an earlier prerelease is refused rather
  than read**. A data subject's key handle is derived from the subject, so it is re-minted by the next
  ordinary write after an erasure and the version ordinal restarts -- which made a destroyed subject
  indistinguishable from one whose key was provisioned again, and read an erased subject's fields as live.
  **Decrypt any field-encrypted data you need to keep BEFORE upgrading**, then re-encrypt; there is no read
  path for the earlier layout, because the generation it needs was never written down. Encrypted audit
  logs and the outbox, inbox and store decorators are unaffected. `ISubjectKeyManager.GetOrCreateKeyAsync`
  now returns `SubjectKey`, and `KeyMetadata` gained `Generation` -- a new `KeyGeneration` type, minted from
  a CSPRNG and **stable across a rotation**, so a per-version backend identifier is not one. Each is a
  compile error, and only for hosts that implement those contracts themselves.

Two changes are behavioural only -- nothing stops compiling, so they are the ones to read rather than
discover:

- **[Key creation instants are reported, not invented](key-creation-instants-are-reported-not-invented.md)** -- `KeyMetadata.CreatedAt` now
  carries the instant of the key version it describes, and a provider that cannot learn one **fails
  instead of substituting the local clock**. On HashiCorp Vault the instant was read from the handle's
  first version for every version, so versions of one handle compared equal and could not be ordered.
  The fabrication was worse than a wrong date: an invented instant sorts ahead of every measured one,
  and on the AWS historical-key provider -- whose purpose is choosing the version live at a given
  instant -- it truncated the scan and resolved to an **earlier version than the one that was live**,
  reporting success. **`KeyMetadata.CreatedAt` is now `DateTimeOffset?`**, so code that reads it stops
  compiling until it decides what an unknown instant means -- and the note gives the safe answer:
  unknown counts as STALE, never as recent, because the lifted comparison picks the opposite by
  default.

One change is a compile error in your own code, and only if you write against the crypto-shredding types:

- **[Crypto-shredding takes a retention scope](crypto-shredding-retention-scope.md)** -- A deployment can
  now declare that an aggregate type must survive an erasure because the law requires the data kept, so
  the key lookup takes the scope the value sits in. `ISubjectKeyManager.GetOrCreateKeyAsync`,
  `IFieldEncryptor.EncryptAsync` and `SubjectFieldCryptor.EncryptFieldsAsync` each gained a parameter, and
  `EventStoreErasureContributor`'s two constructors became one. **Only hosts that implement those
  interfaces, call the cryptor directly, or construct the contributor by hand are affected** — a host that
  registers through `AddCryptoShredding()` has nothing to change *for this change*, and the read path is
  unchanged by it. It does have something to change for the next one: that call now requires a ledger.

One change makes the crypto-shredding read path stop claiming an erasure it cannot confirm, and **refuses to
start** a composition that cannot state one:

- **[A destroyed key is stated, never inferred](key-destruction-is-stated-not-inferred.md)** -- A `null`
  from `IFieldEncryptor.DecryptAsync` asserts that a field was lawfully crypto-shredded, and it used to be
  produced from the *absence* of a key. A backend with a recovery window reports a deleted-but-restorable
  key as not found — Azure Key Vault does so for a soft-deleted key's whole retention period — so for that
  window every read of the subject's fields claimed an erasure over data one call could restore. **The
  tombstone now comes from a durable destruction record** — `IKeyDestructionLedger`, keyed on the key
  generation the envelope names — and the read path asks the key backend nothing: a backend retains nothing
  about material it never held, so *destroyed* and *never here* are the same observation there at every
  granularity. **`AddCryptoShredding()` and `AddEventSourcingCryptoShredding()` now refuse to start unless a
  ledger is registered**, which an erasure store supplies; a deployment that encrypts personal data at rest
  and never destroys a subject key calls `AddCryptoShreddingWithoutErasure()` instead, and registering both
  is refused. `IKeyDestructionStatusProvider` is back to **one** member and is now asked only by erasure
  verification, about a handle, at completion — if an earlier note had you add a version-scoped or
  generation-scoped overload, delete it.

One change needs a **database migration** and will stop your application starting until you run it, and it
also touches any code that reads an erasure certificate's retention entries:

- **[An erasure certificate states only what the erasure established](erasure-certificate-states-what-it-established.md)** --
  The certificate no longer presents a claim nobody established as one. Both legal-basis enumerations gained
  a `NotEstablished = 0` member and every other member **moved up by one** -- so a value no binder,
  deserializer or cast ever assigned now says so, where before it read as Article 17(3)(a) or 17(1)(a).
  **These ordinals are persisted as `INT`, so six columns across three tables need the shipped migration
  script, and the columns are REPLACED rather than renamed: pre-upgrade legal-basis values are not carried
  forward and those rows report their basis as not established.** The new column name makes an unmigrated
  database fail loudly at startup instead of silently misreading every stored basis, and re-running the
  script is safe. The unanchored
  `RetentionPeriod` is no longer emitted, because a duration with no instant beside it invites the reader to
  anchor it on the certificate's own date and read a longer retention than the law gives.
  `ErasureRequestStatus` gains `CompletedExceptConcurrentWrites`, which an erasure overtaken by a write for
  the same data subject now reports instead of being forced to claim completion or a failure. **Every
  certificate already issued still verifies** -- an enum serialises to its name, so the signed bytes are
  unchanged. If you store **consent**, its legal-basis column is a different enumeration and is *not*
  migrated; read the note before touching it.

Also see **[Migrating to .NET 10](net10-only.md)** if your projects are not yet on `net10.0`, and
**[Version Upgrades](version-upgrades.md)** for the versioning policy and what each release stage promises.

## Migrating from another library

- **[From MediatR](from-mediatr.md)** -- Drop-in compatibility shim (`Excalibur.Dispatch.Compat.MediatR`) for a mechanical namespace-swap migration with Roslyn code-fixes (`EXMIG####`), plus the canonical-API rewrite path for request/response and notification patterns.
- **[From MassTransit](from-masstransit.md)** -- Migrate consumers, sagas, and transport configuration to Dispatch equivalents.
- **[From NServiceBus](from-nservicebus.md)** -- Migrate handlers, sagas, and pipeline behaviors to the Dispatch model.
- **[From ASP.NET Eventing Proposal](from-aspnet-eventing-proposal.md)** -- Migrate from the ASP.NET eventing proposal pattern.

## Framework Migrations

- **[Migrating to .NET 10](net10-only.md)** -- Every shipping package collapsed to `net10.0`. Consumer project TFM, SDK, Docker images, and serverless runtime identifiers must be updated.


## Reference

- **[Version Upgrades](version-upgrades.md)** -- Versioning policy and current registration API reference.
- **[MessageContext Guide](messagecontext-v1.md)** -- Using IMessageContext direct properties for type-safe, high-performance message context access.

## See Also

- [Getting Started](../getting-started/index.md) — New project setup from scratch
- [Core Concepts](../core-concepts/index.md) — Excalibur framework fundamentals
