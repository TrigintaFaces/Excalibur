# S888 Sprint Guidance (COMPASS / GUIDE) — SoftwareArchitect

**Phase:** GUIDE (task #2604) · **Baseline:** `8997102bf` (mainline `21156a57f`) · **Input:** `s888-spec-decomposition.md`
**Sprint:** Provider Parity, Exactly-Once Durability & Design-Audit Hardening.

This pins the 9 §8 SA-seam rulings + the execution order. Each seam is pinned to the Microsoft/BCL bar
("what would Microsoft do?") and to **structural inexpressibility** (`enforce-invariants-structurally`) over
minimal blast-radius — greenfield, so the *correct* contract is free. Great-minds lenses that informed the
reasoning (advisory, not dispatched as separate agents — cost discipline overnight; I own the seam):
**Lamport** → D2/L5 split-brain interleaving; **Liskov** → 8cnpj4 behavioral-subtype postconditions.

---

## Execution order (dependency-gated)

1. **FIRST — 8cnpj4 Liskov postconditions (§ruling 9).** Gates Lane E: TestsDeveloper cannot derive
   conformance locks until the 14 postconditions are signed off. Delivered below — unblocks immediately.
2. **Keystone lane A** — `uw1nv4` fence-storage (ruling 2) + `y1moc0` capability (ruling 1). Both pinned below.
3. **Keystone lane B** — `rjolfk` (premise confirmed) + `ljbwh8` Oracle inbox (mirror l9c3cv).
4. **D2≡L5 leader split-brain (ruling 6)** — single owner, couples `5fswhd` (ruling 3). Pin the seam once.
5. **D1≡L11/L12 tenant marker (ruling 7)** — single owner; S886 inseparability.
6. GDPR cryptor placement (ruling 5), provider infra `63xsiv` front-load, audit slices, governance Lane F.

**Single-owner couplings (per `coordinate-before-parallel-work`) — PM assigns ONE implementer each:**
D2 (`0qyitl`) **≡** L5 · Dijkstra-casing (row08) **≡** L9 (`uo90tv`) serializer · D1 (`15ph5g`) **≡** tenant half of L11/L12.

---

## The 9 SA-seam rulings

### 1. `y1moc0` — `SupportsSentTracking` capability shape → **capability-as-data property, NOT a base virtual/skip**
**RULING:** Express the opt-out as a **`bool SupportsSentTracking` on a capabilities interface**
(`IOutboxStoreCapabilities`, mirroring the shipped `IInboxStoreCapabilities` properties-bag), which the
conformance base queries — **not** a base-class `virtual`/skip (which buries the opt-out in inheritance and
is un-greppable) and **not** a fresh marker interface (ISP proliferation).
- **WWMD:** capabilities-as-data is the BCL idiom (`HttpClientHandler.SupportsAutomaticDecompression`,
  `Stream.CanSeek`). `IInboxStoreCapabilities` is our own precedent.
- **STRUCTURAL (non-vacuous, testing-patterns §3):** the conformance base MUST assert **both arms** —
  *safety* (the 6 sent-tracking/cleanup facts are skipped/inverted when `SupportsSentTracking==false`) **and**
  *liveness* (they are **still asserted** for tracking stores). A capability gate with only the skip arm is
  satisfied by a store that does nothing.
- **Delete-on-sent = Postgres + Oracle only.** Marten tracks sent → `SupportsSentTracking==true`. Owner: BackendDeveloper (Lane A).

### 2. `uw1nv4` / `sd36sc` — fence high-water storage → **dedicated fence control table/row, mirror Mongo's control-doc CAS**
**RULING:** For delete-on-sent Postgres/Oracle, store the fencing high-water in a **dedicated control
table** keyed by scope — `(scope_key PK, high_water_token BIGINT NOT NULL)` — enforced by an **atomic CAS**
on both claim and mark, exactly mirroring the shipped Mongo pattern (`MongoDbOutboxStore.cs:576-619`:
`{_id:"<collection>::fence", highWater}`, fence-FIRST, fail-closed). Marten uses a document row.
- **Why not `MAX(FencingToken)` over live rows:** delete-on-sent deletes the winning rows, so the live-row
  max (SqlServer's approach) is structurally inapplicable — it would reset. The control row is the only
  durable high-water. (Same class as `5fswhd`'s reset-to-1 defect — the durable-fence-must-survive-deletion invariant.)
- **STRUCTURAL:** fence BEFORE claim; a stale token (`< high_water`) → **0 rows (set-based, MUST NOT throw)**
  on claim, and a rejected mark; high-water advances **monotonically** (`GREATEST`/CAS, never decreases).
  This makes "a demoted leader advances the feed" inexpressible.
- **+ triage the 5 unfenced cloud stores** → see ruling 2b. Owner: BackendDeveloper (Lane A), real-infra per provider.

### 2b. `t3hwan` (sd36sc 5-store triage) — Cosmos / DynamoDb / Elasticsearch / Firestore / Redis
**RULING:** **Build fencing (control-doc/item CAS) for the stores with a native atomic conditional write —
Cosmos (etag/conditional), DynamoDb (conditional expression), Firestore (transaction), Redis (Lua/WATCH).**
For any store lacking a first-class atomic conditional (Elasticsearch — optimistic `if_seq_no`/`if_primary_term`
is available but weaker) → **document the single-writer requirement** explicitly (leader-gated deployment)
rather than ship a fence that can't be atomic. Decision recorded per-store in `t3hwan`; do not ship a
non-atomic fence masquerading as one (that's the S887 CDC null-token class).

### 3. `5fswhd` — Mongo durable fencing provider as DEFAULT
**SEAM RULING (mine):** deliver the `sd36sc` **durable** Mongo fencing provider as the **default
registration**, so the reset-to-1 path (defect-2: `ReleaseLockAsync` DeleteOne + `ttl_expiresAt` TTL destroy
the doc → token resets to 1 on restart) is **structurally unreachable** — the high-water lives in a durable
control doc that survives release/TTL, not in the lock doc. Do **not** close on defect-1 alone.
**SCOPE (PM's call, not mine):** reopening the CLOSED `sd36sc` to carry this is a **ProjectManager scope
decision** — I rule the seam; PM rules the reopen. Couples D2≡L5 (same `MongoDbLeaderElection`).

### 4. `xlqpju` — global-ordering seam → **CONFIRM close-as-satisfied**
**RULING:** `IGlobalStreamQuery` (`Queries/IGlobalStreamQuery.cs:25`) **IS** the accepted global-ordering
capability seam. `IEventStore` is already the correct 3 methods; `IGloballyOrderedEventStore` does not and
should not exist (that would be ISP-violating fat-interface). Rename/consolidate is cosmetic, not a defect.
**Close-as-satisfied** (PHANTOM). PM closes at integration.

### 5. `wc85fx` / `aknqta` / `y0robr` — per-subject cryptor placement
**RULING:** Inject **`IFieldEncryptor` / `SubjectFieldCryptor`** (shipped in `Excalibur.Compliance.CryptoShredding`,
reuse `ktepi9`) at each decorator's **per-field encrypt/decrypt seam, keyed by the resolved
`[DataSubjectId]`** — mirroring the reference `EncryptingEventStoreDecorator`. Replace the inert single
`_defaultContext` (which keys everything to one context → key destruction can't be per-subject). The
placement is the decorator's write (encrypt) and read (decrypt) call sites, resolving the DataSubjectId from
the payload, NOT a constructor-fixed default.
- **STRUCTURAL:** two-subject real-infra shred lock — destroy-key(A) → A PII unrecoverable, **B intact**
  (liveness), A non-PII still loads. RED against `_defaultContext`. Surfaces: snapshot=`wc85fx` (field-level
  from scratch — current `EncryptingSnapshotStore` is blob-level single-key), outbox=`aknqta`,
  inbox+projection=`y0robr` (narrowed). Owner: BackendDeveloper (Lane D).

### 6. D2 ≡ L5 — leader split-brain → **CAS + monotonic `TimeProvider` grace, ruled ONCE, single owner**
**RULING (Lamport-informed):** at-most-one leadership MUST be enforced by an **atomic CAS on the durable
fence** (not a wall-clock hope) plus a **grace interval measured on `TimeProvider.GetTimestamp()`**
(monotonic, injectable — never ambient `DateTime.UtcNow`). Takeover requires: (a) CAS the fence token
strictly upward, AND (b) the incumbent's lease provably expired on the monotonic clock + grace.
- **STRUCTURAL:** the RED lock is a **skewed-clock interleaving** that today yields two concurrent
  `IsLeader==true`; after the fix it is inexpressible (the second CAS fails → single leader). `FencingToken`
  is **non-null** in fenced mode (the S887 CDC lesson: null must mean only "unfenced/single-instance").
- **Couples `5fswhd`** (same `MongoDbLeaderElection`) → **one owner** for D2(`0qyitl`)+L5+5fswhd. Uses
  `TimeProvider` (WWMD: BCL time abstraction, never `DateTime.UtcNow` on a decision path).

### 7. D1 ≡ L11/L12 — tenant capability marker → **inseparable from the `ITenantContext` injection (S886 `rw2ull`)**
**RULING:** the tenant capability marker (`ITenantScopingCapability<TContract>` +
`TenantScopingCapabilityMarker<T>`, the shipped pattern at
`EventSourcingBuilderExtensions.cs:237` / `PostgresEventSourcingServiceCollectionExtensions.cs:173`) MUST be
registered **only by the same factory/Add\* call that injects `ITenantContext` into the store** — structurally
inseparable, so a store built WITHOUT `ITenantContext` **cannot** carry a truthful marker. This is exactly
the S886 `rw2ull` fix and the S887 guejd9 marker pattern I cleared at REVIEW_ARCH.
- **STRUCTURAL:** the RED lock registers the marker **independently** (store without `ITenantContext`) and
  proves the requirement check **fails** (a lying marker is refused). L11's AWS crypto-shred
  false-certificate is the **highest-severity** instance — a marker attesting erasure that didn't happen.
  Single owner for D1(`15ph5g`) + tenant half of L11(`vlky2n`)/L12(`cvvfo6`).

### 8. `jrbf4r` — phantom narrowing + event-id-range reassignment
**RULING (narrowing):** delete **only** the genuine phantoms — `EventStoreDispatcherService` and the generic
`IEventStore.LoadAsync<TKey>`. **PRESERVE** the live types: `EventStoreMessage<TKey>`
(`Delivery/EventStore/EventStoreMessage.cs:16`) and the `Delivery.EventStore` namespace — grep each token vs
`src/` before deleting (verify-before-claiming: a negative needs a positive control).
**EVENT-ID-RANGE (my call):** the range freed by removing the dead `EventStoreDispatcherService` /
`InMemoryEventStoreDispatcher` diagnostics **is retired, NOT reassigned** this sprint — reassigning a freed
range risks colliding with a consumer that pinned to the old ids; leave the range reserved-and-documented.
A future feature draws from the next free range (per the event-id strategy), not the reclaimed hole. Owner:
DocumentationWriter (phantom text) + me (range decision, recorded here).

### 9. `8cnpj4` — 14 Liskov load-bearing postconditions → **SIGNED OFF** (Lane E unblocked)
Each is the behavioral-subtype postcondition the conformance test must bind (assert the **property**, not a
mechanism; ≥1 fixture implements the interface **directly**, per testing-patterns §3 fixture-shape corollary):

- **L1** `IEventStore.AppendAsync` — a concurrency/append conflict **returns a failure result, does NOT throw**
  (RED: Cosmos/Dynamo/Redis/Sqlite/InMemory that throw). *(L1 home `nq761s` is CLOSED — PM reopen or fresh child.)*
- **L2** `IOutboxStore` atomic-claim — two concurrent claimers get **disjoint** message sets (RED Mongo/InMemory).
- **L3** `IInboxStore` — the durability fault-model is **named in the contract**; InMemory must not advertise a durability it lacks (RED InMemory).
- **L4** `ISagaStore.Purge` — purges **terminal-only**; a non-terminal purge is refused (RED Cosmos/Dynamo/Firestore that `NotSupported` vs doc).
- **L5** `ILeaderElection` — **at-most-one** + `FencingToken` non-null (**≡ D2**, single owner; ruling 6).
- **L6** `IMaterializedViewBuilder`+`ICdcStateStore` — exactly-once fold + `DeletePosition` returns **true iff it existed** (RED cloud unconditional-true).
- **L7** `IMessageBus` — handler fault-**independence**: one handler throwing doesn't abort siblings (RED LocalMessageBus fail-fast).
- **L8** `ITransportSubscriber` — every `MessageAction` **settles** the message (ack/nack/defer) (RED Grpc log-only).
- **L9** serializers — `ResolveType(GetTypeName(t)) == t` **round-trip identity** + wire parity; AOT casing (RED 3 incompatible names) *(≡ Dijkstra row08, single owner)*.
- **L10** `IDb` — a concurrency violation surfaces as the **typed `ConcurrencyException`** (RED: no relational provider throws it).
- **L11** authz/erasure — an erasure **certificate is issued iff the erasure structurally happened** (AWS crypto-shred false-certificate = **highest severity**); `AuthorizationEffect.Permit=0`→Deny default. *(≡ D1 tenant marker.)*
- **L12** `ITenantContext` — history-constraint preserved across the family; RabbitMQ options `ValidateOnStart`.
- **L13** `IWorkflowSignalInbox` — deterministic-replay analyzer is **error-not-warn**; signal durability (the S887 guejd9 contract).
- **L14** job-scheduler family — all share one `IJobSchedulerProvider` contract (no per-impl divergence).

**Sign-off:** all 14 postconditions are load-bearing and correctly stated. TestsDeveloper may derive the
conformance locks now — each **non-vacuous** (RED on the named pre-fix provider, ≥1 direct-interface fixture).

---

## Non-negotiables carried into IMPLEMENT (from SPEC §10, endorsed)
- Non-skipped real-infra locks binding the **default** serializer/client; assert **emitted behaviour**, not registration presence (`verify-against-real-infra-not-mock`).
- Author≠impl regression locks, RED-on-pre-fix; **every safety arm paired with a liveness arm** (testing-patterns §3).
- F-5 cross-project sweep on every type-contract change (capability markers, new columns, new interface members).
- Independent REVIEW_CODE reads impl on **committed mainline** (the S887 net that caught 4 CI-invisible own-keystone defects — do not skip even under attrition; PM clause-4-carries the reviewer if terminated).
- PM is the only committer; coupled impl+lock per lane; CLOSE gates run **SERIALLY** (S885 CS2012 .dll-lock lesson).
