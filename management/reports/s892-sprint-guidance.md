# S892 Sprint Guidance — COMPASS / GUIDE phase

> **Author:** SoftwareArchitect · **Phase:** GUIDE (task 2651) · **Baseline HEAD:** `1a39c0e8a` (S891 CLOSE).
> **Input spec:** `management/specs/sprint-892-spec.md` (ProductManager). **This document pins the
> architectural seams** the spec delegates to SA (§4 K1.3, §5, §10 "SEAMS FOR SOFTWAREARCHITECT TO PIN").
> Every ruling below is grounded run→read→cite against current HEAD source (verbatim reads, not the bug text).
> **PM owns lane assignment / wave-packing / sequencing; SA owns these seam rulings. PM is the only committer.**

---

## 0. TL;DR — the four rulings

| # | Seam | Ruling | Blast radius |
|---|------|--------|--------------|
| **S1** | Keystone canonical outbox mapping (`owxhc8`/K1) | **Option A (static seam) CONFIRMED on HEAD.** But the seam is **NOT a byte-identical superset today** — reconcile it FIRST (preserve `CreatedAt`, carry `Status`/`RetryCount`), THEN route SqlServer through it. K1.1 parity gate is RED today — that's the point. | **Wider than "SqlServer-only"** — reconciliation changes PG/Oracle `CreatedAt` too. |
| **S2** | Outbox fencing validator (`vmy75v`) | Move the fencing fail-fast **out of `OutboxProcessor`'s ctor** into a **startup validator registered by core `AddOutbox`** so it covers the **default (publisher) drain path**. Prefer `IValidateOptions<OutboxDeliveryOptions>`+`ValidateOnStart()`; fall back to an `IHostedService` startup-validator if store resolution is awkward. | Core outbox registration + both drain paths. |
| **S3** | Tenant-scoping capability marker (`xdcr3t`/`zh70zl`/`59sitk`, S886 class) | **Reuse the existing coupled primitive `AddTenantScopedStore`** — marker emitted **inseparably** from context-injected wiring. **Delete** the standalone `RegisterProjectionTenantCapability`. Marker becomes structurally impossible without wiring. Ruled ONCE across projection+inbox+event-store. | EventSourcing.SqlServer + MultiTenancy DI. |
| **S4** | Event/snapshot `TimeProvider` (`3e82d2`) | **Spec premise is partly phantom** — `IEventMetadataWriter` does NOT exist on HEAD. `Snapshot.Create(TimeProvider)` overload = **valid, do it**. `DomainEvent.OccurredAt` already uses `TimeProvider.System` — re-scope away from the phantom interface. | Domain + Snapshot; narrower than filed. |

---

## 1. KEYSTONE — Seam-2 (`owxhc8` + `su6232`, land together, single Backend owner)

### 1.1 Option A (static canonical seam) — S888 ruling RECONFIRMED on HEAD ✅
`OutboxMessage.FromOutboundMessage` (`src/Excalibur/Excalibur.Outbox/Outbox/OutboxMessage.cs:105`) **is** a
`public static` factory — one input (`OutboundMessage`), no injected `IMapper`. PG
(`PostgresOutboxStore.cs:453,487`) and Oracle (`OracleOutboxStore.cs:439,473`) already delegate to it.
**Ruling: option A holds.** A static mapping seam is the WWMD-correct shape (value mapping is a pure
function, not a service); **do NOT introduce a bespoke injected `IMapper`** — that would reinvent nothing the
BCL owns and add a needless seam. `owxhc8` is confirmed **RE-SCOPED to SqlServer-only routing** (MongoDb's
document-shape mapping stays out — different wire shape, not drift).

### 1.2 ⚠ LOAD-BEARING FINDING — the seam is NOT byte-identical today; reconcile to a SUPERSET before routing
The spec frames K1 as "just route SqlServer's inline map through the seam." **That is not safe as-is.**
Verbatim HEAD comparison of the two mappings:

| Field | Canonical `FromOutboundMessage:105` | SqlServer inline `InsertOutboxMessageRequest.cs:43-76` | Divergence |
|---|---|---|---|
| `CreatedAt` | `DateTimeOffset.UtcNow` (**re-stamps** at map time) | `message.CreatedAt` (**preserves** caller value) | **YES — cross-provider bug** |
| `Status` | *not mapped* (relies on ctor default) | `@Status = (int)message.Status` (explicit) | needs default-equivalence check |
| `RetryCount` | *not mapped* (relies on ctor default) | `@RetryCount = message.RetryCount` (explicit) | needs default-equivalence check |
| Headers serializer | `EventSerializationDefaults.Canonical` | local `JsonOptions` | verify identical |

**Consequence:** PG/Oracle (already on the seam) **already overwrite `message.CreatedAt` with `UtcNow`**,
while SqlServer preserves it. This is a live **Liskov behavioral divergence** hiding *inside* the "canonical"
seam. The K1.1 byte-identical gate will (correctly) go **RED** today.

**RULING:**
1. **Reconcile the canonical seam to the CORRECT superset FIRST** (one commit, before routing SqlServer):
   - **Preserve `message.CreatedAt`** — the seam must NOT fabricate a timestamp. Stamp-once-at-construction
     is the TimeProvider-correct contract (ties to S4/`3e82d2`); re-stamping at map time is the bug. This
     **fixes PG/Oracle too**, so the reconciliation's blast radius includes them — **flag to PM: this is
     wider than "SqlServer-only,"** and is the right call (align-UP, don't preserve the divergence).
   - **Confirm `Status`/`RetryCount` ctor defaults == `Pending`/`0`** for a fresh stage; if not, add them to
     the seam so the SqlServer INSERT stays complete. Do not drop columns the INSERT needs.
   - **Confirm the header serializer** is the same options instance/behavior.
2. **Then route SqlServer** (`SqlServerOutboxStore.InsertMessageAsync:1787` → `InsertOutboxMessageRequest`)
   through the reconciled seam, with **AC-K1.1 byte-identical parity RED-first → GREEN** as the gate.
3. `InsertOutboxMessageRequest` is a **public** type — verify the routing doesn't change its public surface;
   if the field-set narrows, that's a `PublicAPI.Unshipped.txt` delta to review (internal-first: consider
   whether it should be `internal` — but that's a separate call, do not widen).

### 1.3 K2 ordering (`su6232`, Liskov) — align UP, conformance kit ASSERTS
Confirmed STILL-REPRODUCES: PG/Oracle `InsertOutboxMessage.cs:62-64` omit `sequence_number`; SqlServer
persists + `ORDER BY (PartitionKey, SequenceNumber)`. **Ruling:** PG/Oracle **persist** `sequence_number` and
drain `ORDER BY (PartitionKey, SequenceNumber)`; the conformance kit (currently excluding ordering at
`:278-280`) is fixed to **ASSERT per-partition ordering across ALL providers**. Scope = **per-partition
strictly-increasing + at-least-once; NOT global total order; down-align prohibited** (spec §4 K2.3).
Because `owxhc8` adds `SequenceNumber` to the canonical shape and `su6232` consumes it, **they share the
canonical mapping + the sequence column → land together, one Backend owner, one keystone commit.**

### 1.4 Great-minds pre-mortem — deliberately SKIPPED (noted per doctrine)
Concerns map to **Liskov** (cross-provider parity), **Metz** (mapping single-home), **Lamport** (ordering/
at-least-once). **Skipped** the persona sub-agents this round: the seam is already grounded in verbatim
source and the load-bearing risks (CreatedAt divergence, byte-identical superset, align-UP) are pinned above.
Recommend the **Liskov lens be applied at REVIEW** (`/liskov-review` or `barbara-liskov`) on the delivered
conformance kit — that's where cross-provider substitutability is provable against real behavior.

---

## 2. Seam S2 — `vmy75v` outbox fencing validator (mechanism ruling)

**Grounded finding (verbatim):** the fencing fail-fast throw lives in `OutboxProcessor`'s **ctor**
(`OutboxProcessor.cs:283-292`: `if (_fencingActive && outboxStore.GetService(typeof(IFencedOutboxStore)) is
null) throw`). The **default** `OutboxBackgroundService` single-processor path drains via `IOutboxPublisher`
(`OutboxBackgroundService.cs:188-207 → ProcessOutboxAsync:304`) and **never constructs `OutboxProcessor`**
(`grep 'new OutboxProcessor' → 0`). So **the fencing check is unreachable on the default drain path** — the
CHANGELOG's "refuses to start rather than draining unfenced" is false for the default configuration. **No
`IValidateOptions`/`ValidateOnStart` exists** for the delivery/fencing options (only for
`MultiTransportOutboxOptions`/`OutboxPartitionOptions`).

**RULING (mechanism):** enforce the fencing invariant **structurally at startup, from the core `AddOutbox`
registration**, so it runs regardless of which drain path is wired:
- **Preferred:** `IValidateOptions<OutboxDeliveryOptions>` + `ValidateOnStart()` — the Microsoft-canonical
  fail-fast-config seam. The validator injects the resolved outbox store (singleton) + leader-gate presence
  and asserts `!(fencingActive && store not IFencedOutboxStore)`. `ValidateOnStart` makes it fail the host at
  boot.
- **Fallback:** if resolving the store inside options validation is awkward (scoped/keyed store), use a
  dedicated **`IHostedService` startup-validator** whose `StartAsync` throws — host fail-fast is equivalent.
- **Single home:** move the invariant to the startup validator and **remove the duplicated ctor throw** (or
  keep it only as defense-in-depth, documented) — one structural enforcement point, not two.
- **enforce-invariants-structurally:** the goal is that "a fencing-required outbox drains unfenced" is
  **inexpressible** because the host refuses to start on *every* path.
- **Safety∧liveness (MANDATORY):** RED arm — unfenced+leader-elected+no `IFencedOutboxStore` → host refuses
  to start on the **default** path. GREEN arm — a fencing-capable store **OR** explicit `AsSingleWriter()`
  opt-out → host starts and drains. The liveness arm is the one that catches a validator that rejects
  everything.

---

## 3. Seam S3 — tenant-scoping capability marker (`xdcr3t`/`zh70zl`/`59sitk`, S886 class) — ruled ONCE

**Grounded finding (verbatim):** the **correct coupled primitive already exists** —
`TenantScopedStoreServiceCollectionExtensions.AddTenantScopedStore<TContract,TStore>(storeFactory)`
(`:66-93`) resolves `ITenantContext` **there** (fail-closed) and threads it into construction, emitting the
marker **only alongside** that registration. Saga/inbox/outbox/eventstore use it. **The SqlServer projection
store does NOT** — it registers the marker standalone (`RegisterProjectionTenantCapability →
TryAddSingleton<ITenantScopingCapability<IProjectionStore<object>>>` at `SqlServerProjectionStoreExtensions.cs:229`),
and all 3 DI factories (`:55/99/145`) **omit** `ITenantContext`. `RequireTenantScopingCapability`
(`MultiTenancyServiceCollectionExtensions.cs:234`) only checks marker **presence** → the gate can pass on a
marker no wiring produced. **This is exactly the S886 `rw2ull` marker-decoupled-from-wiring shape.**

**RULING (the invariant, applied once across projection + inbox + event-store families):**
> **A capability marker MUST be emitted by the same registration act that wires tenant enforcement — never a
> standalone `TryAddSingleton<ITenantScopingCapability<T>>`.** (enforce-invariants-structurally + the S886
> clause.)

Concretely for `xdcr3t`:
1. **Reuse `AddTenantScopedStore`** (or an equivalent coupled factory) for the SqlServer/Postgres projection
   stores: the factory resolves `ITenantContext` and hands it to the store; the marker is emitted **in that
   same call**. This simultaneously fixes the **DOA-via-public-DI** bug (the 3 sites currently omit the arg →
   `ArgumentNullException`) — the DI seam now always supplies the context.
2. **Delete `RegisterProjectionTenantCapability`** — the standalone marker registration is the defect.
3. The store's `ITenantContext` at the **DI seam is REQUIRED**, not `= null`. Greenfield → make the correct
   contract now. (The `= null` overload may remain for direct/test construction, but the DI path never uses
   the null form.)
4. `zh70zl` / `59sitk`: `ITenantScopingCapability<T>` must **mean** tenant-filtering, and the scoped set is
   derived from **contract type**, not from DI registrations — both fall out of #1 (the marker only exists
   where the coupled wiring ran).
5. `RequireTenantScopingCapability` should, where feasible, inspect **emitted behavior** (a tenant-scoped
   read excludes another tenant's row) rather than marker presence — the verify-against-real-infra bar.

**Model note:** projections currently enforce via a **decorator** (`TenantScopedProjectionStore<T>` in
`MultiTenancy.DecorateProjectionStores`) + inner-store fail-closed-on-null. Either model is acceptable
**provided the marker is inseparable from the enforcement wiring**. Pick the coupled-factory model (#1) for
consistency with the other four families — **one seam, ruled once.** Backend (xdcr3t) + Tests (zh70zl/59sitk)
coordinate on the single shared helper; do not fork two marker mechanisms.

---

## 4. Seam S4 — `3e82d2` event/snapshot TimeProvider — SPEC PREMISE PARTLY PHANTOM

**Grounded finding (verbatim):** `IEventMetadataWriter` **does not exist as a production interface** on HEAD
— repo-wide grep finds it only in **stale comments** (`AssemblyInfo.cs:10-13`, `DomainEvent.cs:363`) and a
test fixture; `DomainEvent` declares `: IDomainEvent` only (`DomainEvent.cs:32`). This is consistent with the
S884 removal of `Version`/`AggregateId` from `IDomainEvent`. `DomainEvent.OccurredAt` already defaults to
`TimeProvider.System.GetUtcNow()` (`DomainEvent.cs:38`); `Snapshot.Create` has **no** TimeProvider overload
(`Snapshot.cs:48-63`, hardcoded `DateTimeOffset.UtcNow` + `Guid.NewGuid()`).

**RULING — re-scope `3e82d2`:**
- ❌ **The AC "extend `IEventMetadataWriter` with a `SetOccurredAt` hook" cannot be built as written** — the
  interface is a phantom. **ProductManager: correct this AC** (do not send Backend after a non-existent seam;
  this is the `scope-the-keystone`/premise-gate discipline catching an inherited pre-fix description).
- ✅ **`Snapshot.Create(…, TimeProvider, idFactory)` internal overload — VALID, do it.** Snapshots are
  framework-constructed (repository/store), which is DI-reachable, so an injected `TimeProvider` +
  id-factory (CSPRNG-safe; `Guid.NewGuid()` is fine for a snapshot id, but route it through the factory for
  testability) is clean and WWMD-correct. Keep the existing `DateTimeOffset.UtcNow` overload for callers
  that don't inject.
- ◑ **`DomainEvent.OccurredAt`:** already uses the `TimeProvider.System` BCL abstraction and is `init`-set at
  construction — domain events are **domain-constructed, not DI-constructed**, so plumbing an injected
  `TimeProvider` into every event ctor is invasive and low-value. **Keep the `init`-settable OccurredAt**
  (tests/callers supply a controlled value). Only if a specific **decision path** is found reading a raw
  wall-clock should THAT path be fixed — grep for decision-path clock reads before expanding scope.

---

## 5. Other lane seams (shorter rulings)

- **`dvp6ve` (Decorate keyed-store bypass, P1):** confirmed `DecorateSnapshotStore/EventStore` bind
  `LastOrDefault` on ServiceType only → a keyed 'default' store is left undecorated (PII-plaintext re-entry).
  **Ruling: structural** — assert exactly one matching descriptor **or** decorate **every** matching
  registration (keyed + non-keyed); never a `LastOrDefault` bind. Pairs with `egm9wd`. Safety (no
  undecorated store escapes) ∧ liveness (a legitimately single non-keyed registration still decorates).
- **`vlky2n` (Liskov L11 false-erasure, P1):** premise-gate reads **close-as-satisfied** —
  `AuthorizationEffect.Permit = 0` holds but the described default-Permit decision site is absent, and
  `ErasureService.GenerateCertificateAsync:294-361` already gates on `Completed` and reports `Verified`
  honestly. **SA concurs it is a strong close-as-satisfied candidate; TestsDeveloper (filer) does the final
  re-confirm at IMPLEMENT.** Not a blocker; do not write ACs against a phantom.
- **`rzr5zs` (Dijkstra D7 M-of-N escrow):** recovery must **fail closed below threshold** and reproduce at
  ≥M — real regression, both directions (security-rules: discarded-crypto-result / quorum-not-enforced class).
- **`h9nlsf` (D6 checkpoint-past-failed-apply):** on apply failure the host **halts, checkpoint does not
  advance** (no read-model drift). Structural + safety∧liveness. Reassign to Backend with the O+P cluster
  (per spec §5 Lane A note) — PM's call.

---

## 6. Cross-cutting quality bar (bake into every AC — non-negotiable)
- **WWMD / Microsoft-first:** cross-cutting infra fails **open**; "can't produce a value" → `Try`/nullable,
  not an exception on the hot path; **no "document the limitation"** — build the fix or file a tracked bead.
  Reuse BCL/first-party primitives (`AddTenantScopedStore`, `IValidateOptions`, `TimeProvider`); never
  hand-roll an equivalent.
- **enforce-invariants-structurally:** markers, fencing, tenant filtering, checkpoint-advance, erasure
  decisions enforced by type/seam so the violation is **inexpressible**; the independent lock **RED-proves**
  the seam, does not substitute for it.
- **verify-against-real-infra-not-mock:** every external-system fix (fencing CAS, ordering, tenant filter,
  erasure) ships a **non-skipped real-infra lock** asserting **emitted behavior**, not marker/attribute
  presence, binding the provider's **default serializer**.
- **F-5 sweep incl shipped DDL:** any schema/column change sweeps `tests/**` fixtures **and**
  `docs-site/**`+`samples/**` DDL (`su6232` adds `sequence_number` → sweep every CREATE/INSERT/SELECT).
- **Tests:** every safety AC paired with a **liveness** arm; **author≠impl** locks; non-vacuous (RED on
  pre-fix code); interface contracts get ≥1 fixture implementing the interface directly (no inherited base).

---

## 7. Sequencing input to PM (PM owns the final call)
- **Wave 1 (keystone, one Backend owner, one commit):** reconcile canonical seam (S1.2) → route SqlServer
  (`owxhc8`) → `su6232` sequence persistence + conformance-kit ASSERT. K1.1 byte-identical gate + K2 ordering
  gate both green. **SqlServer outbox surface is single-owner-sequenced** (`owxhc8`/`j1wfzu`/`vmy75v`/`nu13kj`
  all touch it — reserve-before-edit).
- **Wave 2 (parallel):** fencing-CAS family (`fk7buk`/`rtw4u6`/`kt1i58`/`9uawse`, distinct provider files,
  each real-infra lock); S2 fencing validator (`vmy75v`); S3 marker seam (`xdcr3t`+`zh70zl`+`59sitk`, single
  shared helper — Backend+Tests coordinate, do not fork); `dvp6ve`+`egm9wd`; Lane A audit seams.
- **Wave 3:** premise-recount beads (`hvgmlk`/`1t0imi`/`81pwbt`); DDL-completeness cluster
  (`1vxywb`/`ufv8ij`/`m3j4si`/`7o2vuu`/`ayundp`, one owner).
- **Decisions to route (spec §6):** D-1 `s25n17` null-secondary-provider → ProductManager (lean fail-closed);
  D-2 `hqwy8t` all-copies deferral → ProductManager (tracked, never silent); D-3 Seam-2 exec order → PM+SA
  (this doc §7 is my input).
- **Premise re-scopes for ProductManager to fix in beads BEFORE IMPLEMENT:** `3e82d2` (phantom
  `IEventMetadataWriter`); `owxhc8` (wider than SqlServer-only — includes seam CreatedAt reconciliation);
  `vlky2n` (close-as-satisfied candidate).

---
*GUIDE seam rulings grounded run→read→cite vs HEAD. PM owns lane assignment + sequencing + integration.*
