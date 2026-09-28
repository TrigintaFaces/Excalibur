# Architecture — Event Sourcing Tenant Isolation

> **Guarantee contract for tenant isolation across the event store and snapshot store.** This document is
> the source of truth for *which event-sourcing stores keep one tenant's data from being read, written, or
> erased under another tenant, which do not, and how* — and how "no tenant" (an untenanted deployment) is represented
> so it can never collide with a real tenant. It is a contributor + integrator reference. Keep it current:
> any change to a tenant write path, read predicate, or erase predicate updates this file, verified at
> architectural review.

## Guarantee

**Tenant confinement is a property of each store, not of the subsystem.** Every shipped event store now has
it. Read the table before you choose a backend — two rows are still UNVERIFIED, and one gap remains on the
cold tier.

**The guarantee, stated so it can be falsified.** A store is *confining* when an operation performed under
tenant partition P observes and mutates only rows written under P. The test is mechanical: append three
events for aggregate `a` under tenant A, then load `a` under tenant B. A confining store returns **zero**
events. A non-confining store returns **three**.

Every row states how it was established. A row backed only by reading the source, with no arm we have
observed executing, is marked **UNVERIFIED** — including rows we believe are correct. The event-store
container fixtures do not opt into graceful degradation (`ContainerFixtureBase.cs:94`), so a missing
container fails that provider's run rather than passing it by skipping.

| Store | Confining? | What holds the boundary | Established by |
|---|---|---|---|
| Event store — SQL Server, PostgreSQL, Oracle, SQLite | **Yes** | every statement binds a tenant term, and the term sits inside the stream uniqueness constraint | the shared conformance kit's three tenant arms (`EventStoreConformanceTestKit.cs:829, 895, 952`), inherited unmodified by each provider suite |
| Event store — Redis | **Yes** | the tenant is a segment of the stream key (`RedisEventStore.cs:293`) | the same three arms, plus a dedicated `RedisEventStoreTenancyConformanceShould` |
| Event store — in-memory | **Yes** | the tenant is a component of the stream dictionary key (`InMemoryEventStore.cs:39`) | the same three arms, run as a unit suite with no container gate |
| Event store — Cosmos DB, DynamoDB, Firestore, MongoDB | **Yes** | the tenant is the leading segment of the document key: the DynamoDB partition key (`DynamoDbEventStore.cs:589`), the Cosmos partition key (`CosmosDbEventStore.cs:549`), the Firestore document id's prefix (`FirestoreEventStore.cs:610`), and the MongoDB `streamId` — which sits inside the unique `(streamId, aggregateType, version)` index, so the version sequence is per-tenant too (`MongoDbEventStore.cs:506`) | the same three arms, run against a real DynamoDB, Cosmos emulator, Firestore emulator and MongoDB |
| Event store — tenant routing (sharding) | **UNVERIFIED** | the routing store selects a distinct physical store per tenant; confinement is the shard map's, not the inner store's | source only — the sharding integration suite is not among those we hold a measurement of executing |
| Snapshot store — SQL Server, PostgreSQL, Oracle, SQLite | **Yes** | the tenant participates in the upsert key | three tenant arms in `SnapshotConformanceTestBase.cs:186, 229, 271`, plus the untenanted-double-write arm (§ Evidence) |
| Snapshot store — Cosmos DB, DynamoDB, Firestore, MongoDB, Redis | **Yes** | the tenant is composed into the document id / cache key | the same three arms — every provider snapshot suite derives that base |
| Cold (archive) store — S3, Azure Blob, GCS | **UNVERIFIED** | the tenant is an encoded segment of the object key (`AwsS3ColdEventStore.cs:205`) | source only — we hold no measurement of the tiered-storage integration suites executing |

### The four document stores: the tenant is in the key, not in a filter

**Cosmos DB, DynamoDB, Firestore and MongoDB event stores compose the owning tenant into the document key
as its leading segment.** Two tenants writing the same aggregate id address two different keys, so they hold
two document sets and two independent version sequences.

- `DynamoDbEventStore.cs:589` — the partition key
- `CosmosDbEventStore.cs:549` — the partition key
- `FirestoreEventStore.cs:610` — the document id's prefix (each document id is this value plus the version)
- `MongoDbEventStore.cs:506` — the stored `streamId`, which sits inside the unique
  `(streamId, aggregateType, version)` index

**Why the key and not a predicate.** A filter would confine reads while leaving both tenants on one document
set and one version counter: the second tenant to use an aggregate identifier would be told it has a
concurrency conflict on a stream it never wrote, and could never create it. Composing the key makes a
cross-tenant read *unaddressable* rather than filtered out, and makes the version sequence per-tenant as a
consequence rather than as a second mechanism. The conformance kit's third arm
(`EventStoreConformanceTestKit.cs:952`) is the one that separates the two: a filter-only store passes both
isolation arms and fails it.

The tenant term is total — never null, never empty. A host that never enabled multi-tenancy resolves the
framework single-tenant default; a genuinely untenanted row resolves the reserved untenanted sentinel. So
every key carries a tenant segment and none can be produced without one. The constant leading segment also
keeps the composed Firestore document id clear of that store's reserved `__.*__` id shape, which the
untenanted sentinel would otherwise sit inside.

### What this changes for a multi-tenant host

**A multi-tenant host may now register any of the four.** Each registration supplies the ambient tenant to
the store and emits the matching capability in the same act, for both contracts the store is registered
under — `IEventStore` and, for the three document-database providers, `ICloudNativeEventStore`. Attesting
only the first would leave the host refused on the second, so both are emitted from the same seam and
neither can be present without the store having been built with the ambient tenant.

- **A single-tenant host** — one that never enables ambient multi-tenancy — resolves the framework default
  context. There is one partition, so there is nothing to cross.
- **Under `Sharding`**, routing to a per-tenant physical store is the *physical* half of confinement and the
  key is the *logical* half. A shard map that points two tenants at one database is now still confining,
  because the store contributes its own tenant term.

### Upgrading: existing documents were written under the old key shape

**This changes the stored key shape.** Documents written by an earlier version carry
`{aggregateType}:{aggregateId}` (MongoDB: a `streamId` of `{aggregateId}`) with no tenant segment. Nothing
reads that shape any more, so those documents are unaddressable: present in the store, and matched by no
key the store can now compose.

**The store refuses to serve rather than reading them back as empty streams.** Each of the four guards
every point at which it would otherwise act on the *absence* of documents — a whole-stream load that came
back empty, a current-version read that found none, and (on Firestore, whose append proves absence by keyed
reads of its own) an append at the head of a stream. The first time one of those is reached, the store
probes its configured collection/table for a document whose key carries no tenant segment. Finding one, it
refuses, naming the collection, naming the offending key, and pointing at the procedure below; it modifies
nothing. So an unmigrated deployment fails at the first read whose emptiness would have been a lie, with
every event intact, rather than serving that empty stream to a caller who would take it for a new aggregate
and append a second, disjoint history under it. No data is destroyed, and the old documents stay readable by
the earlier package version.

**A read that returns documents proves the collection is addressable, so it is never probed.** Only silence
is ambiguous, and only silence is checked — so the guard costs nothing at startup, nothing on any read that
finds data, and at most one probe per store instance. It is deliberately *not* on the initialisation path:
probing there would spend a request on every process start, and on every serverless cold start, forever, to
detect a condition that can only hold across a one-time upgrade.

**Only the event documents change.** On these four backends the snapshot store has composed the tenant into
its document id since an earlier release, so a consumer already holds tenant-keyed snapshots beside
tenant-less events — that asymmetry is what this closes. Snapshots needed no migration then and need none
now: a snapshot whose key misses is not found and the aggregate rebuilds from its event stream. An event
stream that misses has nothing behind it to rebuild from, which is why this one needs a procedure.

There is no in-place migration tool, and one cannot be written honestly for the general case: deciding which
tenant an existing untenanted document belongs to is a question about the deployment, not about the data.
What is required, per collection/table:

1. **Stop writers.** The re-key is not concurrency-safe against a live writer.
2. **Export every event document**, preserving `version` order within each stream.
3. **Re-key each document** by prefixing the tenant segment `t:{tenantId}:` to the existing key, where
   `{tenantId}` is the tenant that owns the aggregate. A deployment that was single-tenant uses the
   framework default identifier; a deployment with no ambient tenancy at all uses the reserved untenanted
   sentinel. Both are exported as constants (`TenantDefaults.DefaultTenantId`,
   `TenantScope.UntenantedSentinel`) so the value is copied from the code rather than retyped — a mistyped
   variant strands every row in a partition nothing queries, with no error to signal it.
4. **Re-import**, then verify by loading one aggregate per tenant and checking the event count matches the
   export.

A deployment that can afford to rebuild its read models may instead start a fresh collection and leave the
old one in place.

### Partitions

Where a store *is* confining, there are exactly **three kinds of partition**, and they are mutually
exclusive:

| Partition | Meaning |
|---|---|
| A **real tenant** (`Scoped("<id>")`) | a genuine tenant identifier; guaranteed non-null and non-whitespace |
| The **default tenant** (`Scoped(TenantDefaults.DefaultTenantId)`) | the single tenant of a single-tenant deployment that has opted into ambient tenancy; a reserved identifier that a real tenant can never equal |
| **Untenanted** (`TenantScope.Untenanted`) | tenancy is not applicable to the row (no ambient tenant); a distinct partition, not a tenant, bound to the reserved `__untenanted__` term |

The defect this guarantee closes: **multiple different encodings of "no tenant" landing in one column**, so
that a write under one encoding becomes invisible (or destructively visible) to a read under another. There
is now **one** canonical default-tenant identifier at the context layer, and each store encodes "untenanted"
with a **single, collision-proof value** appropriate to its constraint model (below).

> **Consumer obligation.** On a **confining** store, the per-statement tenant term is applied store-side
> with no opt-in — every read, write, and erase binds a real partition (a scoped tenant or the untenanted
> sentinel) and none can omit it. On the four document stores the same is true of the document key, which
> carries the term instead of a predicate. What is **not** store-side, and does require an opt-in, is the fail-closed rejection
> of an operation that never resolved a tenant: that guard is the row-discriminator composition
> (`TenantScopedEventStore`, applied by enabling ambient multi-tenancy), and without it an operation that
> never establishes a tenant runs — and its writes land — in the untenanted partition rather than being
> refused. To operate multi-tenant, both resolve a tenant per operation **and** enable the composition; to
> operate single-tenant, do neither and every operation stays in the untenanted partition.

## How it is achieved (the seam)

### 1. One canonical default-tenant identifier (context layer)

The single-tenant default context and the configured fallback resolve to **one** reserved identifier —
`TenantDefaults.DefaultTenantId` — used everywhere a default tenant is needed. It is a **reserved token**
(wrapped in double underscores), deliberately shaped so a real deployment naming a tenant `"Default"` can
never collide with it. `Scoped()` rejects null/whitespace, so a real tenant identifier can never equal the
reserved token either.

### 2. Untenanted encoding per store — dictated by the constraint model

"Untenanted" is a **distinct partition**; how it is physically represented is a store-private detail governed
by that store's uniqueness/upsert mechanism and by whether a tenant column exists at all. Two shapes are in
use, and **both satisfy the same invariant** (write and read agree within the store; a real tenant can never
equal the untenanted partition):

- **Relational append stores (the SQL Server, PostgreSQL, Oracle and SQLite event stores): the tenant term
  is ALWAYS emitted, on both the write and the read/erase path.** Every scope is routed through
  `KeyedTenantPartition`, which has no empty inhabitant, so an untenanted operation binds the reserved
  `__untenanted__` term rather than omitting the column or the predicate. There is no code path that emits a
  tenant-less statement against these stores. *This paragraph describes the relational family, Redis and the
  in-memory store only — the Cosmos DB, DynamoDB, Firestore and MongoDB event stores carry the tenant term
  in the document key rather than as a predicate (see* Guarantee *).* The shipped
  reference schema declares `TenantId NOT NULL DEFAULT '__untenanted__'` and carries it **inside** the stream
  uniqueness constraint, so the untenanted partition is a first-class partition rather than an absence, and
  two tenants holding the same aggregate identifier occupy separate rows. A `Scoped(id)` write stamps that
  tenant. Isolation is therefore per statement, and the surrounding composition adds a second, independent
  guard homogeneously per deployment (the same injected `ITenantContext` governs write and read/erase):
    - **Non-multi-tenant deployment:** every row carries the reserved untenanted term, so a read/erase spans
      only the single untenanted partition — there are no other tenants' rows to reach.
    - **Multi-tenant deployment:** every operation resolves a `Scoped` tenant, and the row-discriminator
      composition interposes a **fail-closed guard** (`TenantScopedEventStore`, §3) that **throws** on any
      unscoped erase/read *before* a predicate-less statement can run. So an unscoped operation can never reach
      another tenant's rows.
  Consequently, cross-tenant over-erasure is prevented by the **composition-layer guard (multi-tenant) plus the
  all-`NULL` untenanted partition (non-multi-tenant)** — the enforcing conformance test is the erasure
  contributor's multi-tenant fail-closed lock (§ Evidence). The bare store's unscoped no-predicate statement is
  safe *in composition*, not structurally isolated at the bare layer. The event row's uniqueness key **does**
  include the tenant — `UQ_EventStoreEvents_Stream UNIQUE (AggregateId, AggregateType, Version, TenantId)`
  (`Scripts/001_CreateEventStoreSchema.sql:72`) — which is what lets two tenants hold the same aggregate id
  at the same version without colliding.

- **Relational upsert stores (the SQL snapshot stores): untenanted = the reserved sentinel
  `'__untenanted__'`, by design.** This applies to the SQL Server, PostgreSQL, Oracle and SQLite snapshot
  stores; document and key-value stores encode the tenant in the document key instead (see *Known gaps*).
  The snapshot is an **upsert** keyed on `(aggregate, aggregate-type, tenant)`, so the tenant participates in a
  `UNIQUE` constraint. `NULL` cannot serve there: SQLite (and pre-15 PostgreSQL) treat `NULL` as **distinct**
  in a `UNIQUE` constraint, so a nullable tenant would make every untenanted save a new row instead of an
  upsert — a duplicate-snapshot leak — and SQLite offers no `NULLS NOT DISTINCT`. The snapshot stores
  therefore encode untenanted as the reserved non-empty sentinel `'__untenanted__'`, bound on both the write
  and the read path so the two always agree. This is **collision-proof**: a scoped tenant term can never be
  the sentinel, so no real tenant can claim the untenanted partition. The sentinel is a **store-internal key
  encoding** — it never crosses the tenant boundary and never appears as a tenant identity to a consumer.

  > **The empty string cannot serve as this sentinel and is no longer used.** Oracle folds `''` to `NULL`, so
  > the identical intent became a *different value* on that provider and required a separate function-based
  > unique index to stay correct. A concrete non-empty sentinel expresses identically on every provider, which
  > is what allows all of them to share one representation and one set of statements.

  > **Upgrading from a deployment written under the earlier `''` encoding:** rows stored with `''` are not
  > matched by a read that binds the sentinel — the read uses a direct equality on the tenant column, with no
  > `COALESCE` to bridge the two encodings, so an unmigrated untenanted snapshot is silently invisible and the
  > aggregate rebuilds from its events instead. Run the snapshot sentinel migration script shipped with your
  > provider package before upgrading; do not rely on the old value being read back.

### 3. Erase (right-to-erasure) stays within the partition

The erase path uses the **same binding as the reads**, and it is unconditional: `TenantScope.Scoped` emits
`tenant = @t`, and an untenanted scope emits the same predicate bound to the reserved `__untenanted__` term,
targeting the untenanted partition only.
Under multi-tenancy the erase is **fail-closed structurally**, symmetric with reads and appends: the
row-discriminator composition wraps the erasure surface with a tenant-scoping guard that requires a resolved
ambient tenant and throws `TenantRequiredException` **before any erase** — so a multi-tenant deployment can
never emit a predicate-less erase across every tenant's rows, even on the default per-subject erase path. A
genuine non-multi-tenant deployment (no guard, no tenant column) erases its single partition unchanged. The
erase therefore can neither miss a subject's rows (a silent no-op erase) nor sweep another tenant's rows
(over-erasure). The snapshot upsert stores, whose tenant column is always present, scope by the reserved
untenanted key (the reserved `'__untenanted__'` sentinel) instead of omitting the predicate.

The erasure capability is **forwarded through the whole event-store decoration chain** so it is reachable via
the supported dependency-injection composition. The shared decorator base forwards the erase to its inner store
by default (recursing to the terminal provider store, which performs the tombstoning), so a telemetry, metrics,
or tenant-scoping decorator cannot silently strip erasure merely by not re-implementing it. The tenant-scoping
decorator overrides the forward to apply the fail-closed guard above; a decorator wrapping a store that does not
support erasure surfaces a clear error at erase-time rather than removing the capability.

**Consumer obligation — ask for the capability, never type-test for it.** Probe the resolved event store with
`GetService(typeof(IEventStoreErasure))` and treat a `null` answer as *this chain cannot erase*. A `is
IEventStoreErasure` type test is not equivalent and will mislead you in both directions: the decorator base
declares the interface unconditionally (C# has no conditional interface declaration), so the test answers
`true` for every decorator regardless of what it wraps, while the capability probe answers for the chain
beneath. What the guarantee therefore covers: an erase reached through the probe is performed by the terminal
store and passes through every decorator's invariant on the way. What it does not cover: the framework cannot
give a store erasure it does not implement — a chain whose terminal store has none answers `null`, and a host
that requires erasure is expected to fail its own startup check rather than discover it at the first erase.

Verified by `IsolatingEventStoreDecoratorErasureProbeShould`, which asserts both directions: `null` over a
non-erasure inner (so the probe cannot over-claim) and the decorator itself over an erasure-capable inner (so
the erase is reached *through* the decorator rather than around it).

### 4. Projections: erasure propagates by REPLAY, and which replay call works depends on the shape

A projection is a read model rolled up from the event stream, not an independently erasable store. It does
not carry per-subject encryption keys and it has no erase endpoint of its own. **Its erasure guarantee is
structural: a projection rebuilt after a subject's aggregate has been tombstoned contains no trace of that
subject, because the rebuild replays the tombstoned stream and never applies the erased events.**

> **A rebuild is ADDITIVE, and for erasure that distinction is the whole story.** A rebuild re-folds
> every projection id the replayed stream produces and writes each one. It does not remove a row for an
> id the stream no longer produces, and it cannot: the store contract offers no key enumeration, so the
> set of rows that exist is not knowable from here.
>
> **The consequence, stated rather than left to be discovered.** Erasure tombstones an aggregate's
> events in place, and the replay skips a tombstone *before* deriving a key from it. For a FULLY erased
> aggregate every event is a tombstone, so the replay produces **no key for that subject at all** — and
> an additive rebuild therefore leaves whatever row the live path last wrote for it. A rebuild removes
> a subject's data from every row it *re-folds*; it does not delete the subject's own row. Use the
> subject-scoped call in the table below for that.
>
> *(Superseded, quoted so a reader who inherited it can recognise it: "UNVERIFIED for a per-aggregate
> projection … the rebuild service folds the whole stream into ONE state and writes it under the
> projection's TYPE NAME, while every apply path keys by the projection id. Those key spaces are
> disjoint." That was true and is now fixed: the rebuild derives the projection id exactly as the apply
> path does — one shared derivation, `MultiStreamProjection.DeriveProjectionId` — so it writes the keys
> a reader loads. `ProjectionRebuildKeyingShould` binds it, and the mutation that reddens it is the old
> behaviour itself.)*

> **Consumer obligation: STOP THE PROJECTION'S PROCESSOR BEFORE REBUILDING IT.** A rebuild reads the
> whole stream and then writes every key it touched, conditionally on the position each key held when
> it was first touched. A live writer advancing those rows meanwhile makes the conditional write
> **refuse**, and the rebuild reports failure rather than completing — deliberately, because a
> superseded key is a key the rebuild's own fold never landed on. The read model is unavailable for the
> duration of the rebuild.
>
> **This obligation is not enforceable by us and is therefore stated, not checked.** Nothing in the
> framework can observe whether a consumer's processor is running. What the framework does instead is
> refuse loudly: a rebuild interrupted this way names the id it stopped on and says the rebuild is
> incomplete, so ids written before it hold rebuilt state and the rest do not. Re-running from scratch
> is the recovery, and it is safe — the replay is deterministic.

> **Memory: a rebuild holds every folded projection in memory until it writes.** The bound is the
> number of DISTINCT projection ids the whole stream produces, multiplied by the size of one projection
> instance. The framework controls neither factor. Flushing mid-replay is sound but trades this bound
> for unbounded write amplification and reader-visible intermediate states, so it is not what ships. A
> data set large enough to exhaust memory here needs the incremental path, not a rebuild.

**Stated so it can be falsified.** Given a projection that has applied at least one event from a data
subject's aggregate: after that aggregate is erased and the projection is rebuilt from the stream, the
rebuilt projection state contains none of the erased subject's data, while data contributed by a *different*
subject's aggregate on the same stream is unaffected and still present.

**How it is achieved.** The rebuild replays every stored event through the projection's handlers in stream
order. A tombstoned event carries the reserved erasure-marker event type in place of its original type and
payload; the rebuild recognizes that marker structurally, before attempting to resolve or deserialize the
event, and skips it — it is never handed to a projection handler, so it can never populate projection state.
This mirrors how aggregate rehydration already treats the same marker on the write side. A genuinely corrupt
or unresolvable event (any event type *other than* the reserved marker) is not treated as erasure — the
rebuild still halts rather than silently skip it, so real data loss is never mistaken for a GDPR erasure.

**Every other stream reader advances past a tombstone too, and this is what makes the rebuild guarantee
reachable.** A tombstone is a permanent part of the stream, so every reader meets it forever after the erase,
not once. Live subscriptions, the async and global-stream projection hosts, the materialized-view replay, the
on-demand (ephemeral) projection builder and projection recovery all recognize the marker structurally,
deliver nothing for the event, and advance their position or checkpoint past it. A reader that instead treated
the tombstone as a deserialization failure would halt and never advance, so it would re-read the same event on
every subsequent poll and stop permanently at the first erased event: erasing one subject would silently and
irrecoverably stop a consumer's live projections. Advancing is therefore part of the erasure guarantee, not a
convenience.

**Falsifiable, and enforced.** Given a stream containing an erased event followed by a later, un-erased event:
a live subscription started from the beginning of that stream delivers the later event. The enforcing test is
`ErasedEventSubscriptionShould.DeliverEventsAppendedAfterAnErasedEvent`, which builds the tombstone through
the store's own erase path rather than hand-stubbing it. Its sibling,
`NotAdvancePastAGenuinelyUnresolvableEvent`, holds the other half: an event that is merely unresolvable is
*not* skipped, so the two properties cannot be satisfied by a reader that simply ignores everything it cannot
deserialize. `ErasedEventMarkerShould` pins the recognition predicate itself, which every reader shares.

**Aggregate and workflow-journal replay refuse instead of skipping, deliberately.** A stream-wide reader
projects many aggregates, so skipping one subject's tombstones still produces a correct read model for
everyone else. A reader reconstructing a *single* subject's own state has no such fallback: the thing it is
rebuilding is the thing that was erased. Aggregate rehydration therefore returns a defined erased sentinel
rather than a silently partial aggregate, and workflow journal replay refuses outright, because a journal
entry is the record that stops an activity being executed twice and a hole in it would re-run work that
already ran.

**Known gap.** A projection updated incrementally between an aggregate's erasure and its next replay
may still reflect the pre-erasure state for that subject. Nothing clears it automatically: the framework
does not connect erasure to any projection call. **And a rebuild does not close it for the subject's own
row** — see the additive note above: a fully erased aggregate produces no key, so the rebuild writes
nothing for it. The subject-scoped call in the table below is the remedy for that row.

**Which call to make depends on what you need cleared, and a rebuild alone is not enough for the
subject's OWN row.** A rebuild re-folds every id the stream still produces, which removes the subject's
contributions from every row it shares with others. It does not produce — and so does not overwrite —
a row for an aggregate that is now entirely tombstones.

> **The root cause worth carrying, because it explains the whole design below.** The positioned-write
> contract assumes projection state is a function of position, and erasure mutates events IN PLACE.
> `fold()` changes while every position stays fixed, so **across an erasure no position comparison
> carries any information about whether a row's state is correct.**
>
> An advancing write therefore cannot express what an erasure needs. It requires the new position to
> exceed the stored one — which is exactly what refuses a redelivered batch — so a caught-up row is
> refused, and the refusal is indistinguishable from "another writer is already ahead of you". That
> shape once made this remedy report a successful recovery having written nothing. It is why the
> operation below exists and why its result is a distinct type.

| what needs clearing | the call |
|---|---|
| the subject's OWN row, on a projection keyed per aggregate (the default) | `IProjectionRecovery.ReapplyAsync<TProjection>(aggregateId, aggregateType, ct)` — replays that one aggregate under the key the apply path writes, which for a fully erased aggregate is the empty state. **Requires `UseProjectionRecovery()`**; without it nothing registers `IProjectionRecovery` and the call cannot be resolved |
| the subject's contributions to rows SHARED with other subjects (a `KeyedBy` projection, a singleton) | a full rebuild — it re-folds every such key from the tombstoned stream |
| the subject's own row on a `KeyedBy` projection | **no supported call, and recovery now REFUSES rather than guessing.** `ReapplyAsync` replays one aggregate onto a fresh state; writing that under a derived key would overwrite the key with a state missing every *other* aggregate that feeds it. It throws and says to rebuild the whole projection instead. Rebuilding clears the subject's contributions; it cannot delete a key that exists only because of them |

### How the per-aggregate remedy writes, stated so it can be falsified

**Guarantee.** A recovery of an erased aggregate either **writes the re-folded state**, or **fails
loudly**. It never reports a successful recovery having written nothing.

**How it is achieved.** Recovery reads the row's position before the replay, then chooses by a
comparison it already holds:

```
readAt  = the position the row holds, read before the replay
highest = the highest position the replay APPLIED, or none if it applied nothing

highest > readAt  ->  ADVANCE   UpsertAtPositionAsync(expected: readAt, new: highest)
                                The row was BEHIND: the replay folded events it had not seen, so the
                                position genuinely moves.
otherwise         ->  RE-FOLD   RefoldAtPositionAsync(atPosition: readAt)
                                The row is caught up, or the erasure SHORTENED the stream (every event
                                a tombstone yields no position at all). The position is already correct
                                and it is the STATE that changed.
readAt is none    ->  CREATE    No position to preserve.
```

`RefoldAtPositionAsync` rewrites the state at the position the row already holds, without advancing it
(`IPositionedProjectionStore`). Three properties are load-bearing:

- **It re-folds AT `readAt`, never at the highest surviving position.** The row has folded everything up
  to `readAt`; after the erasure "everything up to `readAt`" is a smaller set with the same boundary, so
  the position is still an honest statement about which prefix the state covers. Writing the lower
  surviving position instead would retreat the position, make the row look behind, and have the next
  batch re-fold events it already has.
- **It MUST NOT create a row.** An absent row was deleted, deletion is how erasure removes personal
  data, and recreating it would reinstate what the erasure removed — while reporting success.
- **Its result is a SEPARATE type from an advancing write's.** Same outcome names, opposite settle
  rules: for an advance, a refusal reporting a position at or beyond the one attempted means the work is
  already done; for a re-fold that is exactly backwards, because the writer who is ahead folded from the
  PRE-erasure stream. Mixing the two is a compile error rather than a silent compliance failure.

Preserving the position on the fully-erased branch is sound because an erased aggregate's empty state
IS the true fold for any prefix: the replay skips tombstones structurally, so nothing is missing from it.

**Evidence.** Two service-level arms, `ErasedProjectionRecoveryShould` — one for a partial erasure with a
caught-up row, one for a full erasure with a positioned row. Each is RED-detecting by construction: the
mutation that reddens the first is treating any replayed position as an advance, and the mutation that
reddens the second is writing unconditionally on the re-fold branch. At the store, six arms in the
positioned-projection conformance kit, inherited by all eight providers and run against real engines:
the re-fold applies at the held position and does not move it; it is refused when the row advanced after
the read; it is refused at a position the row does not hold; a row with no established position is
terminal rather than retryable; a repeated re-fold leaves the row identical; and **a re-fold against an
absent row reports vanished and creates nothing.** The last is the arm that stops a store resurrecting a
row an erasure deleted.

**Consumers with a strict erasure SLA must therefore drive this themselves**, and must know which row
holds the subject. For the third shape that is not currently possible through this framework; treat a
`KeyedBy` projection carrying personal data as requiring an erasure mechanism of your own until this
closes.

> **CORRECTED. The previous sentence here named the wrong missing capability.** It read: *"The missing
> capability in all three rows is the same one — enumerating which rows of a projection exist, keyed the
> way the apply path keys them — and it is not built."* **Enumeration IS built**: `QueryAsync` returns
> every row in the ambient tenant, `CountAsync` counts them, and the paged, cursor and distinct-value
> capabilities all range over the same set.
>
> Two things are actually missing, and they are different from each other and from what was written:
>
> 1. **No member of the family hands a store KEY back.** Every read returns `TProjection` values, and
>    every mutating member requires the caller to already know the `string id`. The key exists in every
>    provider's table — the relational stores select on an `Id` column — but the contract never returns
>    it, and nothing obliges a projection type to carry its own id (the constraint is only
>    `where TProjection : class`).
> 2. **Even a key-returning read would not close the third shape.** For a per-aggregate projection the
>    id IS the aggregate id the caller already holds, so the key adds nothing. For a `KeyedBy`
>    projection, the full set of ids does not say which rows a given subject contributed to — that is a
>    fact about the events that produced each row, not about the rows. Closing it needs a record of
>    which subjects contributed to which key, written when the fold happens.
>
> So the gap is narrower than "enumeration is not built" in one direction and wider in another, and
> a key-returning read is an API-completeness item rather than the remedy.

## Consumer obligations

- To operate multi-tenant, establish the ambient tenant per operation **and** enable ambient multi-tenancy
  (row-discriminator composition) so a request that fails to establish one is rejected rather than
  silently landing in the untenanted partition. Establishing the tenant without enabling the composition
  gets you the write-side confinement above but not the fail-closed guard.
- Otherwise (single-tenant deployment) do neither, and every operation stays in the untenanted partition.
- Do not attempt to use the reserved empty-string or reserved default-tenant identifier as a real tenant id
  — `Scoped()` rejects empty/whitespace, and the reserved default token is not a real tenant.

## Evidence (conformance)

The tenant-isolation properties are proven by real-infrastructure conformance/regression tests (non-skipped
where the backing store is available). The event-store container fixtures do not opt into graceful
degradation, so a missing container fails that provider's run rather than passing it by skipping.

The refusal above carries its own arms, one pair per document store
(`{Provider}EventStoreLegacyKeyRefusalShould`): one seeds a document under the legacy untenanted key shape
and asserts a load refuses and names the collection; the other seeds a correctly-keyed document and asserts
the store loads an absent aggregate as an empty stream and then writes to it normally. The second is what
keeps the first honest — a probe that refused unconditionally would pass the safety arm alone. Note that the
liveness arm reaches the probe rather than bypassing it: its load *is* an empty read, so the probe runs,
comes back clean, and the empty result stands.

The three tenancy arms are non-vacuous against the document stores by construction, and that was measured
rather than assumed: with the tenant segment removed from the Firestore key, the safety arm and the
per-partition version arm both go RED against the real emulator, and both return GREEN with it restored.

| Property | Proven by |
|---|---|
| A multi-tenant host registering a document event store that attests no tenant capability **refuses to start** — under both isolation strategies, and in either registration order; and one registering a shipped document provider **starts**, because that provider attests both contracts it registers under | `CloudNativeEventStoreTenantCapabilityGateShould` (refusal arms paired with permitted arms, so a gate that refused everything would also fail) |
| Two tenants holding the same aggregate identifier in a document store each see only their own events, each see **all** of their own events, and each version that aggregate independently | the shared kit's three tenancy arms, inherited by the Cosmos DB, DynamoDB, Firestore and MongoDB conformance suites and run against real infrastructure |
| Untenanted write is readable by an untenanted read | snapshot + event-store unscoped round-trip arms |
| A tenant-scoped read never sees another tenant's or the untenanted rows (and vice versa) | scoped-isolation arms |
| An untenanted double-write upserts to **exactly one row** (the reserved sentinel upsert key) | snapshot untenanted-double-write arm |
| A multi-tenant unscoped erase fails closed (throws, mutates zero rows); a non-MT erase still tombstones; a scoped erase touches only its tenant | event-store erase fail-closed + isolation arms |
| Erasure resolved through the supported DI composition reaches the store and tombstones (the decoration chain does not strip the capability), for both multi-tenant and non-multi-tenant hosts | event-store erasure real-DI-resolve arms (MT + non-MT) |
| A rebuild driven through the surface a consumer's host actually rebuilds through never hands an erased subject's event to a projection, still hands another subject's event to it, and runs to completion | `SqlServerProjectionRebuildErasureEndToEndShould` — real SQL Server, the real erasure path, and `IMaterializedViewProcessor` resolved from a container composed the documented way. The property is asserted at the **view builder**, which is where this document states it; a persisted-view assertion cannot tell the event being skipped from a builder that ran and wrote nothing |
| The same property on the in-process rebuild service | `ProjectionRebuildErasureShould`, paired with `ProjectionPoisonHaltParityShould` (a genuinely corrupt, non-erasure event still halts the rebuild). **Scope, CORRECTED: a consumer CAN reach this service.** `AddProjectionRebuild()` is a public registration extension and `IProjectionRebuildService` is a public interface, both at committed HEAD, so a host that calls it resolves and drives this path directly. The superseded wording said the service was unreachable — *“no registration extension, so no consumer can reach it”* — and it is quoted here because it understated the reach of every defect in this service to anyone assessing one. These arms cover a path consumers use, not a sibling of it |
| The default-tenant identifier is a single canonical reserved value | tenant-defaults unit arms |
| A SQLite database created before the snapshot table had a tenant column is still readable and writable after upgrading, and one holding the empty-string encoding becomes reachable again | SQLite released-schema upgrade arms + empty-tenant convergence arms |
| A SQLite table holding both untenanted encodings for one aggregate refuses at startup, naming the table and aggregate, without mutating a row | SQLite convergence collision arm |
| The SQLite upgrade script a separately-provisioned deployment runs reaches the same tenant-scoped shape: rows and global positions survive, carried-over rows hold the reserved sentinel, one tenant still cannot append the same version twice while two tenants can, and a second run refuses and changes nothing | SQLite shipped upgrade-script arms (`SqliteShippedTenantUpgradeScriptShould`) |

## Known gaps

- **The four document event stores changed their stored key shape, and there is no in-place migration
  tool.** Cosmos DB, DynamoDB, Firestore and MongoDB now compose the tenant into the document key. Documents
  written by an earlier package version have no tenant segment and are therefore unaddressable after
  upgrading. The re-key procedure is in *Upgrading: existing documents were written under the old key
  shape*; it is an export/re-key/re-import, and it cannot be automated for the general case because deciding
  which tenant an existing untenanted document belongs to is a question about the deployment rather than
  about the data. Their **snapshot** stores are unaffected — those already composed the tenant into the
  document id.

  **The gap is a refusal, not a silent misread.** Each of the four probes its configured collection/table
  at most once per store instance, on the first occasion it would otherwise act on the absence of documents,
  and refuses when it finds a document whose key carries no tenant segment — naming the collection, naming
  the offending key, pointing at the procedure, and modifying nothing. What that replaces is the worse
  failure: the load returned an empty stream, the caller took it for a new aggregate, appended at version 0,
  and ended holding two disjoint histories under one identity while the store still held the first.

  **Neither startup nor a read that finds data pays for it.** On Cosmos DB, Firestore and MongoDB the probe
  is an ordered range read over the key, bounded to one document. DynamoDB has no ordered access across
  partitions, so it is a single filtered `Scan` page: a table upgraded in place carries the old shape on
  *every* item, so the first page cannot miss it, and bounding the request keeps a large correctly-keyed
  table from paying for a full scan. A table that holds both shapes only beyond the first page — which takes
  a partial rollback to produce — is **not** detected.

  **On MongoDB one residual path reports the refusal as a failed result rather than as a throw**: an append
  at the head of a stream issued with no preceding load. That store's append already flattens any exception
  into a failed `AppendResult`, so the refusal arrives with `Success` false and the full message rather than
  as an exception. Nothing is written and no history is split either way; the load-then-append flow every
  repository uses refuses from the load, which throws.

- **A cold (archive) store is refused under multi-tenancy even though its keys carry the tenant.** The S3,
  Azure Blob and GCS cold stores encode the tenant as a segment of the object key, but no shipped cold-store
  registration attests a tenant capability, so a multi-tenant host that registers one fails at startup
  naming `IColdEventStore`. The refusal is conservative rather than a report of a leak in those keys; a
  multi-tenant deployment cannot use cold-tier archival until a registration attests the mechanism.

- **Upgrading across the untenanted-sentinel change requires running the provider's migration script —
  except on SQLite, which now reconciles itself.** The **relational** snapshot stores (SQL Server,
  PostgreSQL, Oracle, SQLite) encode untenanted as one reserved non-empty sentinel. **Document and
  key-value snapshot stores do not use this encoding at all** — they compose the tenant into the
  snapshot's document identifier, so an untenanted snapshot simply has no tenant term in its key and
  there is no `UNIQUE`-constraint problem for a sentinel to solve. The paragraphs above describe the
  **relational** family only. An earlier encoding used the empty string, which Oracle folds to `NULL` and
  which therefore could not be a shared representation. Reads bind the sentinel with a direct equality and
  **no `COALESCE` bridging the two encodings**, so on **SQL Server, PostgreSQL and Oracle** a snapshot row
  still carrying the old value is **not found** — the read returns nothing and the aggregate rebuilds from
  its event stream rather than raising an error. The data is not lost, but the snapshot's performance
  benefit is silently gone until the rows are migrated. Run the snapshot sentinel migration script shipped
  in your provider package as part of the upgrade.

  **SQLite is the exception, in both directions, and the difference is worth stating precisely.** Its
  failure was never the silent degradation described above. A SQLite database created before the tables had
  a tenant column at all keeps that shape — `CREATE TABLE IF NOT EXISTS` does not alter an existing table —
  so every read and every write raised `no such column: TenantId` rather than quietly returning nothing. The
  store reconciles the table on first use: a table with no tenant column is rebuilt with one and every
  existing row is stamped as untenanted, and a table whose rows still hold the empty-string encoding has
  those rows converged onto the sentinel. Both steps are idempotent and require no action from you. The one
  case that cannot be reconciled automatically is a table holding **both** encodings for the same aggregate,
  since the two rows would collapse onto one key; that refuses at startup, names the table and the
  aggregate, and changes nothing, so you can delete or re-key the stale snapshot and restart.

  **That runtime reconciliation is only reachable by a host that runs the package against its own database
  with table-creation rights.** A deployment whose schema is owned centrally by a migration tool, or
  provisioned and reviewed before the application touches it, never reaches it — and for that deployment
  re-running the create script is a no-op that leaves whatever shape is already there. The shipped create
  script provisions the current tenant-scoped shape; there is no in-place upgrade from an earlier one, so a
  database that predates it is re-provisioned rather than migrated.

- **A single-tenant deployment's own rows can be split across TWO different, both-correct identities —
  `__untenanted__` and `__default__` — and closing that gap for existing rows is a separate step from the
  sentinel-encoding upgrade above.** A single-tenant host's ambient tenant context resolves to the
  framework's single-tenant identity (`__default__`); rows written before that context existed, or by any
  code path that supplied no tenant at all, are stored under the reserved `__untenanted__` sentinel
  instead — a different, equally valid partition, not a defect on its own (a multi-tenant deployment
  legitimately keeps the two separate: it can hold rows that belong to no named tenant alongside rows that
  belong to one). For a single-tenant deployment specifically, the split means a read scoped to the
  identity the host now uses does not find rows filed under the other one — they are not lost, but they
  are unreachable until converged.

  **SQLite closes this automatically** (`SqliteTableInitializer`'s per-store convergence, gated on
  single-tenant mode, collision-guarded, run on every store construction). **SQL Server, PostgreSQL, and
  Oracle do not close it yet, and a hand-run SQL script is deliberately not how this ships.** SQLite can
  gate its convergence in C# at store construction by reading `TenantContextOptions.RequireTenant`
  directly; a static SQL migration script has no equivalent read — it can only *document* "run this only
  for a single-tenant deployment" as an operator precondition it has no way to enforce. Run against a
  multi-tenant host, such a script would fold rows that genuinely belong to no tenant onto a specific,
  nameable, wrong tenant — the exact harm this subsystem's tenant-isolation guarantee exists to prevent.
  A migration whose only safeguard is a comment in its header is not the same control as SQLite's runtime
  gate, so this remains open pending a design that gives the relational providers an equivalent
  enforceable gate, rather than shipping a script that trades one gap for a worse one. **SQLite's shipped
  shape-upgrade script observes the same line**: it brings the tables onto the tenant-scoped shape and
  stamps the untenanted sentinel, and it does not converge those rows onto the single-tenant identity,
  because SQL cannot read the host's deployment mode. That convergence stays with the runtime gate. **Document and
  key-value stores are unaffected by this gap** — they either compose the tenant into the row's identity
  directly (no sentinel to converge) or have no tenant concept wired into this subsystem yet.

- **Tiered (hot/cold) erasure covers the hot tier only.** When events are archived to a cold tier, the erase
  tombstones the hot tier; the cold archive has no erase surface yet, so a right-to-erasure request against an
  aggregate with archived events is not silently partial — it fails at host startup rather than leaving cold
  copies behind. Startup validation asks the composed event store for its erasure capability, and a tiered
  composition cannot answer, so the host that enables both is rejected while the composition can still be
  changed — not at the first erasure request, when a statutory clock is already running. A host composed
  without an `IHost` (serverless wiring) runs no startup validation, so there the same probe still rejects
  the composition when the erasure contributor is first resolved.
  Full cold-tier erasure is not yet implemented. Consumers requiring GDPR erasure of archived events should not
  enable cold-tier archival until that capability lands.

- **Redis event store now binds the tenant term in every stream key, matching its snapshot-store sibling.**
  Until this was closed, `RedisEventStore` carried no `ITenantContext` dependency and no tenant term anywhere
  in its key construction — the only `IEventStore` implementation in this subsystem without one — so two
  tenants appending events for the same `(aggregateType, aggregateId)` shared one Redis stream **and one
  version counter**: a cross-tenant write collision/corruption, not merely a read leak. The fix adds a
  required `ITenantContext` constructor parameter (a breaking change, matching `RedisSnapshotStore`'s own
  shape) and folds the resolved tenant into the stream key (`{prefix}:t:{tenantId}:{aggregateType}:
  {aggregateId}`), so the version counter — derived from the stream key — is tenant-scoped too. There is no
  legacy-row convergence question here (nothing was ever written under a different key shape to converge
  from); existing streams simply move under the new key on next use.
# Architecture — Atomic Append

## Guarantee

**An append is all-or-nothing, on every provider.** `IEventStore.AppendAsync` either commits every event
in the batch or writes none of them; it never commits a prefix. A batch larger than the provider can write
in one atomic operation is **refused before any write**, with `EventBatchTooLargeException` carrying the
offending count and the limit, so the caller can split the append and retry.

The falsifiable form: for every provider, appending `limit + 1` events to an empty stream leaves that
stream **empty** and raises `EventBatchTooLargeException`; appending exactly `limit` events succeeds. For a
provider that declares no limit, an append of any size either commits whole or does not commit at all.

**Why refusal rather than splitting.** Committing a large append as a sequence of smaller atomic writes
looks like success and produces a torn prefix whenever one of them fails. A consumer **cannot detect that
state**: the stream holds a prefix with no suffix, and every subsequent read is consistent with a shorter
history. There is no read that distinguishes a torn stream from a stream that was simply never written
further, so the damage is silent and permanent. A torn append is event-stream corruption, which event
sourcing must never produce.

## How it is achieved (the seam)

Each provider rejects at its own append boundary, before any request reaches the service:

| Provider | Atomic limit | Seam |
| --- | --- | --- |
| DynamoDB | 100 (`TransactWriteItems`) | `DynamoDbEventStore.AppendAsync` |
| Cosmos DB | 100 (`TransactionalBatch`) | `CosmosDbEventStore.AppendAsync` |
| Firestore | 500, lowerable via `MaxBatchSize` | `FirestoreEventStore.AppendAsync` |
| SQL Server, PostgreSQL, Oracle, SQLite, MongoDB, Redis, in-memory | none — one transaction (or one Lua script) covers any size | provider `AppendAsync` |

Because the limit is enforced at the boundary, each provider's transactional write path is reached only
with a batch it can commit in a single operation, so that path is genuinely all-or-nothing rather than a
loop that could stop halfway.

## Evidence (conformance)

`EventStoreConformanceTestKit.AppendAsync_AboveTheAtomicLimit_ShouldRefuseWholeOrAppendAtomically`, run by
every provider suite. It is a **parity** arm: a provider declares its ceiling through `AtomicAppendLimit`,
and the arm holds it to whichever answer it gave — refuse above the ceiling, or genuinely append any size
when none is declared. It asserts the refusal **and** that the stream is untouched, so a store that throws
after writing part of the batch fails exactly as one that never threw. A discriminator appends exactly the
limit first, so a store that cannot write a large batch at all cannot pass the refusal for the wrong
reason.

A per-provider suite asserting only its own behaviour cannot detect a disagreement *between* providers,
which is how three providers came to answer this case three different ways; only an arm every provider
runs can.

## Consumer obligations

- **Split large appends yourself.** Catch `EventBatchTooLargeException`, or keep an append at or below the
  configured provider's limit. The exception carries `ActualCount` and `MaxBatchSize`.
- **Do not treat the refusal as retryable.** It is an `ArgumentOutOfRangeException`: the identical call can
  never succeed. Retrying without splitting loops forever.

## Known gaps

- **DynamoDB and Cosmos DB expose a documented non-atomic opt-out** (`UseTransactionalWrite=false`,
  `UseTransactionalBatch=false`). On those paths the append is committed per item and a failure partway
  through *can* leave a partial stream. This is the consumer's explicit trade, made by configuration, and
  the limit is not enforced there. The guarantee above describes the default, atomic configuration, which
  is what the conformance suites register. Firestore offers no such opt-out.

# Architecture — Stored Message Identity

## Guarantee

**One identity, stated once, used by every path that names the message.** A message type declares its
name with `[MessageName("...")]`, and that declared name is the only identity written anywhere: the
event store's event-type column, the outbox `MessageType` a consumer routes on, and the CloudEvents
`type` an external subscriber filters on. In falsifiable terms:

- **A stored event resolves if and only if its type declares the same name, or declares an alias for
  the stored name, AND that type was registered.** Resolution is over the registered set; declaring a
  name is necessary but not sufficient. Namespace, assembly and assembly-version changes are *not observable* in stored
  identity, so none of them can make previously-written data unreadable.
- **A name identifies exactly one type WITHIN THE REGISTERED SET.** Two *registered* types claiming one
  name is refused at registration. This is weaker than it sounds and the difference matters: the write
  path does not consult the registry at all -- every store asks the type for its declared name directly
  -- so two types declaring the same name, where only one is registered, write identical bytes and both
  read back as the registered one. Nothing currently detects that. Uniqueness is a property of the
  registered set, not yet of the type universe, and making it the latter needs enforcement over all
  declaring types rather than over the ones a consumer remembered to register.
- **A message type with no declared name cannot be registered.** There is no derived fallback, so a
  message can never acquire an identity its author did not choose.

The name is permanent. Renaming is a **two-phase deployment**, and doing it in one phase corrupts
readability permanently -- see *Renaming a message* under consumer obligations below.

## Fault model

Crash-stop processes; N concurrent instances at **mixed build versions**; one shared durable store; no
coordination between instances. Every clause above is quantified over *every reader at every build
version currently or recently deployed* -- not over one process. The mixed-version reader is always
reachable, which is what makes the rename protocol below a two-phase one.

## How it is achieved (the seam)

- `MessageNameHelper.GetName(Type)` — the single source of the declared name; throws when a type
  declares none. Every writer goes through it.
  (`src/Dispatch/Excalibur.Dispatch.Abstractions/MessageNameHelper.cs`)
- `EventTypeRegistry.Register(Type)` — requires the declared name, and indexes it through a guard that
  refuses a second type claiming a name already taken. Aliases are indexed for reading only; the
  type→name map keeps the canonical name, so writes always use it.
  (`src/Dispatch/Excalibur.Dispatch.Abstractions/EventSourcing/EventTypeRegistry.cs`)
- `EventTypeRegistry.ResolveType(string)` — an exact lookup. There is no normalizing or fuzzy match: a
  declared name has no version, culture or assembly in it to differ on.
- CloudEvents `type` is the declared name at emit
  (`src/Dispatch/Excalibur.Dispatch/CloudEvents/CloudEventExtensions.cs`), **except** on a
  receive-then-re-emit round trip, where the inbound envelope's type is preserved — it belongs to the
  originating publisher and is not ours to rewrite
  (`src/Dispatch/Excalibur.Dispatch/CloudEvents/CloudEventEnvelopeConverter.cs`).

## Consumer obligations

- **Declare a name on every event type you register**, and choose it once — it is permanent, and it is
  the identifier other organisations write subscription filters against. Recommended shape:
  `<Publisher>.<BoundedContext>.<EventName>`, e.g. `Contoso.Sales.CustomerCreated`.
- **Never put a version in the name.** Schema evolution is an upcaster's job; a version in the identity
  orphans stored data on every change.
- **Renaming a message takes TWO deployments, in this order.** The governing invariant is: *never write
  a name that some live reader cannot resolve.* A one-step rename violates it.

  ```
  Phase 1   [MessageName("old")]  [MessageNameAlias("new")]     deploy to every instance, wait for full rollout
            every instance can now READ "new"; none writes it yet

  Phase 2   [MessageName("new")]  [MessageNameAlias("old")]     now safe: every reader already resolves "new"
  ```

  Doing it in one step -- declaring the new name while the old build is still running -- means the new
  instances write a name the old ones cannot resolve. Those events are durable, so the old build fails
  on that aggregate permanently, and every projection rebuild it attempts fails at the same offset.
  **Rolling back is worse than a failed rollout:** roll the new build back and everything it wrote is
  unreadable by the build you rolled back to. Wait for phase 1 to reach every instance before starting
  phase 2.

- **Keep every retired name as an alias, forever.** Deleting one makes every event still stored under
  it unreadable.
- **Do not reuse a name across two types**, including across packages — names share one namespace.

## Evidence (conformance)

`tests/unit/Excalibur.Dispatch.Abstractions.Tests/EventSourcing/StableEventTypeIdentityShould.cs` —
RED-detects a violation of each clause above: a type registered without a declared name, two types
claiming one name, an alias leaking into the write path, and a name shape that would need escaping
where it is stored. The suite also holds the security property that widening identity did not widen
the registry's allow-list: an unregistered type is still refused with the assembly scan off.

## Known gaps

- **A3's audit label is not held to this guarantee.** `ActivityAudit` uses the declared name when the
  request type has one and falls back to the type's simple name otherwise, because an audited request
  passes through no registration seam at which a missing name could be refused. The consequence is a
  recoverable one — an audit trail that renames a request type mid-life records two labels for it — not
  an unreadable record. Declare a name on request types whose audit history must stay queryable across
  a rename.
- **An event whose type cannot be resolved halts the projection that reads it**, rather than being
  skipped or quarantined. Skipping would silently corrupt an accumulating view, so halting is the safe
  direction, but the diagnostics for it are poor and it does not stop retrying.

# Architecture — Global Stream Ordering

## Guarantee

**Stated so it can be falsified.** At every instant, the set of committed global positions is a
**contiguous prefix** `1..k`.

> **The sentence that used to follow this one was a NON-SEQUITUR, and it is withdrawn.** It read:
> *"Consequently no event ever becomes visible at a position below one a subscriber has already read,
> so a subscriber may advance a high-water mark to the highest position it has observed and never look
> back."* The first clause above is true. **The second does not follow from it**, and a subscriber that
> relies on it can silently and permanently skip a committed event.
>
> **The prefix property is a predicate on a STATE. A subscriber scan SPANS states.** A
> `SELECT ... WHERE Position > @cp ORDER BY Position` examines each slot at a different instant and
> never returns to one it has passed. Nothing in the prefix property relates what a scan saw at one
> instant to what is committed at another. One writer is enough to lose an event: the scan passes
> slot 1 while it is uncommitted, slot 1 commits, slot 2 commits, the scan reaches slot 2 and
> delivers it. The high-water mark is now above a committed event that was never delivered.
>
> **The counter row does not save this.** It totally orders commits, so position 2 cannot commit
> before position 1 — true, and irrelevant, because the scan's READS are interleaved with those
> commits rather than ordered against them.
>
> **CORRECTION, and it reverses the deployment guidance that stood here for part of an hour.**
> An earlier revision of this note asserted that `READ_COMMITTED_SNAPSHOT` ON (the Azure SQL default)
> was the EXPOSED configuration and that RCSI OFF was safe. **That is backwards, and it was wrong in
> the direction that tells a self-hosted operator they are fine.**
>
> RCSI gives STATEMENT-level snapshot semantics: one `SELECT` reads the data as of the start of that
> statement, so it cannot see a position committed mid-scan while missing an earlier one. Plain
> locking READ COMMITTED has no statement snapshot — it takes and releases shared locks row by row —
> so a row inserted and committed AHEAD of the scan's current position after the scan began is
> visible to it. That is the interleaving that skips an event.
>
> **The error was naming a CONFIGURATION instead of the PROPERTY**, which is why the correction
> below states the property first.
>
> **The deciding property: the scan must be atomic with respect to concurrent commits.**
> A `SELECT ... WHERE Position > @cp ORDER BY Position` that is NOT atomic can pass a slot
> while it is uncommitted and later return a higher position that committed in the meantime.
> Under statement-level snapshot semantics (SQL Server with RCSI on; PostgreSQL and Oracle
> READ COMMITTED, which are MVCC) the scan is atomic and this cannot happen. Under SQL
> Server's locking READ COMMITTED — the default for a self-hosted instance — it is not
> atomic, and it can.
>
> **The fix defends regardless of isolation configuration, and it lives in ONE place:**
> `ContiguousGlobalStreamQuery`, a decorator over `IGlobalStreamQuery`. It delivers only the
> CONTIGUOUS run starting at the caller's position + 1 and stops at the first gap. Because
> committed positions are a contiguous prefix, a position missing from a read belongs to a
> transaction still in flight rather than to a hole — so stopping DEFERS events rather than
> dropping them, and the next read takes them in order. It is a decorator rather than a line in
> each provider because one correctness rule copied five times is a rule the sixth provider will
> not have.
>
> **Applied to SQL Server, deliberately not to the others, and the asymmetry is the point.** The
> skip is only reachable where the scan is non-atomic. PostgreSQL and Oracle are MVCC, SQLite is
> serialised, and the in-memory store is in-process — on those the guard would buy nothing and
> cost something, because it WAITS on a missing position.
>
> A stream provisioned by the current schema cannot carry a permanent hole: archival tombstones a
> row rather than deleting it, so the position survives with a null payload, and every consumer
> already skips a null-payload row.
>
> The trade, stated: if a position were permanently absent this stalls rather than skips. That is
> the correct direction — a stall is loud, a skipped event is silent.

**The test is mechanical.** Append an event and note its position. Force a second append to abort *after*
it has allocated. Append a third. The third event's position is exactly the first **plus one**. A store
allocating from an identity column or a sequence gives the first **plus two** — the aborted append burned
a value that will never be reissued.

**Why a hole is not merely untidy.** A tailing subscriber cannot tell a permanent hole from a slow one. If
it waits, one rolled-back append stalls every projection forever. If it skips, a slow append is silently
and permanently lost — committed, durable, and below a mark that has already passed it, with nothing
downstream able to detect the omission. Making holes impossible removes the choice rather than answering
it.

## How it is achieved (the seam)

Positions are **not** assigned by an identity column or a sequence. Every provider allocates a contiguous
block from a single counter row, **inside the appending transaction**:

| Provider | Allocation seam | Mechanism | Issued with the insert? |
|---|---|---|---|
| SQL Server | `AllocateAndInsertEventsRequest.cs` | `UPDATE ... WITH (ROWLOCK) SET @First = Value + 1, Value = Value + @AllocCount` | **yes** — one command |
| PostgreSQL | `AllocateAndInsertEventsRequest.cs` | `UPDATE ... RETURNING` in a data-modifying CTE, insert `CROSS JOIN`s its output | **yes** — one statement |
| Oracle | `OracleEventStore.cs` | PL/SQL block, `UPDATE ... RETURNING ... INTO` an OUT bind | no — see below |
| SQLite | `SqliteEventStore.cs` | `UPDATE ... SET Value = Value + @Count ... RETURNING` | no — see below |
| In-memory | `InMemoryEventStore.cs` | `Interlocked.Add` under the store's append lock | n/a |

**Two providers deliberately do NOT merge the allocation into the insert, and that asymmetry is a
decision rather than an omission.** Do not "fix" it for consistency without measuring first.

- **SQLite** runs in-process. A round trip there is a function call into the SQLite library, not a
  network hop, and SQLite already serializes writers at the database level — so there is no contention
  window to narrow and nothing measurable to recover.
- **Oracle** binds its insert with ODP.NET array binding (`ArrayBindCount`), which executes the statement
  once per bound row set. Folding the allocation into that statement would execute the allocation once
  per row, which is wrong. Doing it correctly needs a PL/SQL block with `FORALL` over associative arrays
  — a large rewrite to recover one round trip, and unmeasured. Oracle still benefits from the other
  window-narrowing change: outbox staging runs before the allocation.

Two properties of that row do the work, and both belong to the *transaction* rather than to the counter:
its exclusive lock is released only at COMMIT, so no second append can allocate while one is in flight;
and its increment **rolls back with the transaction**, so an aborted append consumes nothing.

Neither the relative ordering of two independent counters nor the lifetime of any transaction enters the
correctness argument, and that is deliberate. An identity column and a sequence each hand their number out
at INSERT and let it escape the transaction; on Oracle a sequence additionally defaults to `CACHE 20`, so a
pooled session can issue a *low* position long after another session committed a *higher* one.

Allocation is the **last** step before the rows are written. Correctness does not depend on that; sustained
append throughput does, because every other appender blocks on the counter row until this transaction
commits.

### Archival preserves positions

Archival **tombstones**; it does not delete. The payload moves to cold storage and the row stays, carrying
its version, its position and an `ArchivedAt` stamp (`TombstoneArchivedEventsRequest.cs:71`). The tiered
decorator restores archived payloads on read (`TieredEventStoreDecorator.cs:95`).

This is what keeps the guarantee true for the whole lifecycle rather than only until the first archive run.
Deleting the rows would leave archived events perfectly readable per-aggregate and **invisible to every
global-stream consumer**, so a projection rebuild would silently produce an incomplete read model.

`ArchivedAt` is also what separates an archived entry from an **erased** one. Both have no payload; an
archived payload is retrievable and an erased payload is gone. Code that infers "archived" from "payload is
missing" will resurrect data a data-subject request removed.

## Evidence (conformance)

| Property | Arm |
|---|---|
| An aborted append burns no position (SQL Server, real engine) | `SqlServerEventStoreGaplessPositionShould` |
| Same on PostgreSQL, where sequence advancement is *documented* as non-transactional | `PostgresGlobalStreamPositionShould` |
| Same on Oracle, plus positional parameter binding and the OUT-bind allocation | `OracleGlobalStreamPositionShould` |
| Same on SQLite (embedded, so inherently non-skipped), plus counter seeding on a populated database | `SqliteGlobalStreamPositionShould` |
| Every appended event carries a distinct ascending position; paging never repeats or skips | `InMemoryGlobalStreamQueryShould` |
| Archived payloads are restored and erased ones are not | `TieredEventStoreDecoratorShould` |

## Consumer obligations

- **Run the shipped schema script.** It creates and seeds the position counter table. The store cannot
  allocate a position without it, and fails loudly rather than inventing one.
- **Do not reintroduce an identity column or a sequence** for the position, and do not add a second counter
  row — the schema's `CHECK` constraint makes the latter unrepresentable on purpose.
- **Do not DELETE rows from the events table.** Positions form a contiguous prefix because nothing removes
  them; a retention job that deletes rows reintroduces exactly the hole this design removes.
- **Expect appends to serialize, and size for it.** Concurrent appends contend on the counter row, which
  is the intrinsic price of a single global total order over concurrent writers rather than an artifact
  of this implementation — an identity column only appears to avoid the cost because it does not, in
  fact, produce a total order.

  Measured, rather than estimated, by `AppendAllocationStrategyBenchmarks`, which runs the same append
  against tables differing ONLY in how the position is produced:

  | concurrent writers | vs an identity column |
  |---|---|
  | 8 | **3.9x** slower (±0.4) |
  | 32 | **4.9x** slower (±0.5) |

  **The cost RISES with concurrency**, which is the shape a serialization bottleneck has: the counter
  row's lock is held to COMMIT, so appends proceed one commit at a time while an identity column lets the
  database group-commit them. In the same run the identity baseline absorbed 32 concurrent appends in
  28 ms; this store took 138 ms.

  Two things reduce it, and both are the same idea — **narrow the window the lock is held across**, since
  work inside it is paid by every blocked appender rather than only by the one holding the lock:

  - The allocation is issued **in the same command as the first insert** rather than as a round trip of
    its own. Measured separately: 5.2x → 3.9x at 8 writers, 6.8x → 4.9x at 32.
  - Outbox staging, which is one round trip per integration event, runs **before** the allocation rather
    than after. Measured separately: an append emitting three integration events is **2x faster** under
    concurrency (0.51x at 8 writers, 0.49x at 32).

  A multi-event append also allocates its whole block in ONE counter update, so the allocation cost
  amortizes across the batch rather than being paid per event.

  **The single-writer figure is deliberately omitted.** It is dominated by per-append latency rather than
  by contention, and it did not measure reproducibly here — across runs the uncontended ratio moved
  between 0.9x and 1.3x with the baseline's own variance as large as the effect. Treat the uncontended
  cost as "not distinguishable from an identity column on this hardware" and measure it on yours.

  **Absolute figures are NOT quoted.** These came from a containerized SQL Server on a developer
  workstation, where even the identity baseline took ~9 ms for a single append — that describes the
  storage, not this design. Re-run the benchmark on representative hardware before planning capacity.

  *(Superseded figures, recorded so a reader who met them elsewhere recognises them: this table once read
  1.14x / 7.6x / 4.7x at 1 / 8 / 32 writers. Those were measured without truncating between arms, so the
  comparison table had accumulated twice the rows of the baseline table — the benchmark was partly
  measuring table growth. The old numbers also showed the cost FALLING from 8 to 32 writers, which is
  backwards for a serialization bottleneck and was the tell.)*

  A host needing more write throughput than one ordered stream can carry should shard, and accept that
  there is then no cross-shard global order to read.

## Known gaps

- **Rungs R3 and R4 are UNVERIFIED for this seam.** The arms above are sampling-class: they execute the
  code for chosen inputs. No property-based suite generates the input space and no model checks the
  reachable state space. The blast radius is catastrophic, so the honest label is unverified rather than
  covered — a green suite is not a discharged ladder.
- **Oracle and SQLite do not implement archival.** They allocate positions the same way, but a host on
  those providers has no tiered storage, so the archival half of this document does not apply.
- **A subscriber checkpoint is durable only if one is registered.** The advance is a compare-and-set —
  the caller states the position it believes is current and the store answers `Advanced` or `Superseded`,
  so the loser of a race is told rather than silently overwriting the winner. SQL Server, PostgreSQL,
  Oracle and SQLite each implement it, and each is bound by the shipped checkpoint conformance kit
  against a real engine. **Registering an event store does not register a checkpoint store**: absent an
  explicit registration the checkpoint is held in memory, so it is lost on restart and every projection
  replays the stream from the beginning.
- **Replaying an async projection from zero is NOT idempotent, so an in-memory checkpoint is a
  correctness risk and not merely a cost — CLOSED on a store that records positions.** The async apply
  path loads the persisted projection, applies the event to it, and writes it back. It used to write
  back unconditionally, with the row carrying no last-applied position, so nothing could detect a second
  application: an assignment-shaped handler survived it and an accumulating one (`Total++`, appending to
  a list) silently double-counted on every restart, without bound. A store providing
  `IPositionedProjectionStore<T>` now refuses that write. **A store that does not provide it still has
  this gap**, and the framework gives a host **no way to require the capability at startup** — see the
  positioned-write section below, which states what a host can actually do today.
- **Two live readers of one subscription can still both apply the same events, though the common case is
  closed.** Registering leader election makes at most one reader ACTIVE: the others stand by and take
  over when leadership moves. What that does NOT do is fence the WRITE. A leader paused past its lease
  and then resumed is a reachable state, and it will apply a batch before it discovers it no longer owns
  the subscription — because the compare-and-set protects the checkpoint ROW, not the WORK, and a reader
  applies a whole batch before contesting the mark. **Without leader election registered, a host assumes
  it is the only instance; running two is the consumer's to prevent.** The sentence that used to end this
  bullet — *"closing this completely requires the projection write itself to be conditional on a
  last-applied position"* — is now DONE rather than pending: on a store providing
  `IPositionedProjectionStore<T>` the second writer's write is refused by the store, so the double
  application is inexpressible rather than merely unlikely. On a store without that capability the
  original bullet still stands and handlers must be idempotent.
- **A reader that loses the compare-and-set now stands by rather than exiting.** It adopts the winning
  reader's mark, reports itself unhealthy, and keeps polling, so a later drain of the winner leaves a
  reader able to take over. It used to return from its background service, which meant that after a
  rolling deploy the surviving process could be the one that had stood down — no reader processing, and
  the application still reporting healthy.

---

# Architecture — Position-Conditional Projection Writes

## Guarantee

For a projection identified by `x` with stored position `P(x)`:

```
state(x) = fold(apply, init, { e : pos(e) <= P(x) })
```

**The position is not a number the writer picks.** It asserts WHICH PREFIX of the global stream is
folded into the stored state, and every committed write makes that statement true at the instant it
commits.

**Stated so it can be falsified.** Given a projection whose handler accumulates (`Total++`) and a batch
of events already folded into it: re-delivering that batch leaves the stored state and the stored
position unchanged. Given a batch that overlaps the folded prefix and extends past it, only the events
above the stored position are folded, and the position advances to the highest one applied. Given two
writers that both read at `P` and both compute a higher position, exactly one write commits.

**What is NOT guaranteed.** A store that does not provide `IPositionedProjectionStore<T>` writes
unconditionally and has none of the above; the apply path degrades to the previous behaviour and a
re-delivered batch is folded twice. That degradation is **silent by design of the capability pattern** —
the write succeeds and reports success, because a capability that is absent is simply not asked for.

**CORRECTED: this paragraph used to say "a host that depends on the guarantee should require it at
startup", and the framework gives you no way to do that.** There is no registration-time check, no
options validator, and no exception; asking for the capability is the apply path's own internal decision
and a host cannot observe it. Telling you to do something the surface does not support is worse than
saying nothing, so here is what you can actually do:

- **All eight stores the framework ships provide the capability**, so a host using a shipped store has
  the guarantee. If that is you, no action.
- **If you supply your own `IProjectionStore<T>`, implement `IPositionedProjectionStore<T>` as well**, or
  accept that your projections are folded at-least-once and make your handlers assignment-shaped rather
  than accumulating. An accumulating handler on a store without the capability double-counts on every
  restart, without bound.
- **If you DECORATE a shipped store, forward `GetService`.** The capability is discovered by asking the
  store for it, so a decorator that does not forward the call hides a capability its inner store has —
  and the degradation above is what you get, silently, from a decorator that looks harmless.

A startup gate that lets a host say "refuse to start without this" is owed and is not yet built.

## The two conjuncts, and why neither substitutes for the other

A committed write requires BOTH:

1. the stored position equals the position the caller read at — this orders concurrent writers;
2. the new position is strictly greater than the stored one — this refuses a re-delivery.

They are independent, and a store enforcing only one passes for the wrong reason:

- **Monotonicity alone** (which is what an engine's "external version" mode provides) admits a writer
  holding a stale read that happens to name a high position. Its state was folded without the events in
  between, and they are lost while the position claims they are present.
- **Compare-and-set alone** admits a re-delivery, because the caller obtained its expected value BY
  READING IT, so a replayed batch satisfies the comparison by construction.

This is measured rather than argued. Removing conjunct 2 from the SQL Server statement reddens exactly
one conformance arm; removing conjunct 1 reddens three, including the exactly-once arm. The two
mutations redden disjoint sets.

## How it is achieved (the seam)

- The contract: `Excalibur.EventSourcing.Abstractions/IPositionedProjectionStore.cs` — one read returning
  state and position together, one write taking the position read at and the position being claimed, and
  an outcome of `Applied`, `Superseded` or `Vanished`.
- The protocol, in ONE place so four apply factories cannot each get it subtly wrong:
  `Excalibur.EventSourcing/Projections/PositionedProjectionWriter.cs`.
- The condition is expressed by the STORE, in a single atomic operation wherever the engine allows one:
  a guarded `MERGE` on SQL Server, a CTE on PostgreSQL, a condition expression on DynamoDB, a filtered
  replace on MongoDB, a transaction on Firestore. Cosmos, Elasticsearch and OpenSearch have no
  single-statement form, so they read a version token and condition the write on it — the position
  comparison decides admissibility, the token makes the write atomic against a concurrent writer.

**A missing position withdraws the CONDITION, never the WRITE.** On the save path the events are being
committed now and no global position exists yet, so the write is unconditional. Treating an absent
position as a reason not to write discards the projection silently, which is why that branch lives in
the shared writer rather than at each call site.

**A row carrying no position at all is ADOPTED, not refused.** A projection written by a rebuild, a
recovery, or a version of the store that did not record positions has none. Refusing it would refuse
every later attempt identically — a silent permanent stall rather than a conflict — so a caller claiming
no position may create a row or adopt an unpositioned one, and may not overwrite a positioned one.

## Evidence (conformance)

`PositionedProjectionStoreConformanceTestKit` (in the `Excalibur.Testing.Conformance` package) states the
guarantee as fifteen arms, each of which a consumer can run against their own store. Arms of note:

- `Refuse_a_position_that_does_not_advance` — the re-delivery arm; a monotonicity-only store is the only
  kind that can fail it.
- `Refuse_a_stale_expected_position` — the stale-read arm; this is the one an "external version" scheme
  fails while looking correct.
- `Admit_exactly_one_of_two_writers_racing_from_one_read` — the exactly-once property, stated directly.
- `Adopt_a_row_that_carries_no_position` — the liveness arm that distinguishes a correct refusal from a
  permanent stall.
- `Read_the_state_and_its_position_as_one_observation` — **weaker than its name**, and the kit says so
  in the arm itself. It establishes that a read returns one write's state paired with that same write's
  position, not that the two are fetched atomically; detecting a non-atomic pair needs a writer
  interleaved between the two fetches, which this contract cannot express against an arbitrary store.
  The stores that fetch in two round trips (Cosmos, Elasticsearch, OpenSearch) are therefore
  UNVERIFIED on atomicity specifically, and rely on their version token to make the subsequent WRITE
  safe rather than the read.
- `Still_persist_a_fold_that_carries_no_position` (in the apply-path suite) — the arm that catches a
  store-aware apply path dropping the save path's work.

Six more cover the RE-FOLD, the operation an erasure needs because it changes the fold beneath a fixed
position. They are a distinct set because the advancing arms above are satisfied by a store that refuses
every re-fold:

- `Refold_the_state_at_the_position_the_row_already_holds` — the liveness arm for the set. A store that
  forwards a re-fold to its advancing statement fails here and passes everything else.
- `Refuse_a_refold_when_the_row_advanced_after_the_read` — proves that dropping monotonicity did not
  drop the ordering conjunct. A store implementing the re-fold as an unconditional replace fails only
  this one.
- `Refuse_a_refold_at_a_position_the_row_does_not_hold` — stale on arrival, as against overtaken in
  flight; a store comparing the wrong column passes one and fails the other.
- `Report_requires_rebuild_for_a_row_with_no_established_position` — terminal, not retryable. Answering
  "superseded" here would loop forever, because nothing about an unpositioned row changes on its own.
- `Apply_the_same_refold_twice_without_changing_the_result` — what discharges the safety argument for an
  operation that does not advance. Without it, "repeating a re-fold is harmless" is prose.
- `Report_a_refold_against_an_absent_row_as_vanished_without_creating_it` — **the highest-value arm in
  the set.** A store that creates here reinstates a subject's data after an erasure removed it, while
  reporting success. That is a compliance failure and it is invisible from outside: the row is
  well-formed and every value in it was written correctly.

The kit's own completeness arm names any arm a derivation forgot, so a new provider cannot present the
contract with one of these unwired.

The kit is exercised against REAL infrastructure and is never skipped: SQL Server, PostgreSQL, MongoDB,
DynamoDB, Cosmos DB, Firestore, Elasticsearch and OpenSearch each have a derivation that fails rather
than skips when its container or emulator is unavailable. Running it is what found that four of those six providers could not execute
their own conditional write at all — failures no mocked client can reproduce, because a mock returns
what it was told and never refuses.

## Rigor: which class of guarantee this seam actually carries

A test and a proof are different classes of guarantee, and the evidence above is the sampling class.
Stated per rung so nothing here reads as more than it is:

- **R1 — met.** Compiler, nullable reference types, analyzers.
- **R2 — met.** The guarantee above is stated in falsifiable terms, and the conformance arms RED-detect
  its violation on six real engines.
- **R3 — PARTIAL, and the seam's rung is still UNMET.** The fold invariant specifically now has a
  generated-input suite (`PositionedProjectionInvariantShould`): it builds delivery schedules rather
  than enumerating them — overlapping batches, exact re-deliveries, gaps, repeated batches — checks
  `state = fold(apply, init, { e : pos(e) <= P })` against each, and on failure shrinks to the shortest
  schedule that still breaks and prints its seed. It is non-vacuous: changing the already-folded
  filter from `<=` to `<` reddens both arms and shrinks to a two-batch counterexample. **This covers
  ONE guarantee of the seam, not the seam**, so the seam's R3 remains unmet and is not claimed.
- **R4 — UNVERIFIED, and not dischargeable in this repository today.** There is no model checker and
  no model. The properties that would justify one are temporal and concurrent — exactly TLA+'s
  subject — and the interleavings that matter here are the ones a generated schedule samples rather
  than searches. Nothing in this document should be read as a proof.

**The boundary that applies to all of it:** the generated suite draws from the state space; it does not
search it. A schedule shape the generator cannot produce is a defect it cannot find, and that limit is
a property of the generator, not of the code under test.

## Consumer obligations

- **Handlers must remain idempotent on a store WITHOUT the capability.** The guarantee above is a
  property of the store, not of the framework.
- **The projection table must carry the position column.** On SQL Server and PostgreSQL that is
  `LastAppliedPosition` / `last_applied_position`, `NOT NULL DEFAULT -1`. Both halves matter: `NOT NULL`
  keeps a row from existing in a state the condition cannot compare against, and `-1` rather than `0`
  because zero is a legitimate stream position, so a zero default would make a new row claim it had
  already folded the first event. The document stores need no schema change.
- **A projection written before the column existed is adopted on its next write**, and that first
  adoption re-folds the batch it is given, which over-counts for an accumulating projection. **This is
  not a one-time cost** — see the known gap below on the adoption cycle — and rebuilding rather than
  adopting is not currently an available instruction, because no path in this framework rebuilds a
  projection row the apply path reads.

## Known gaps

- **An unconditional write onto a positioned row starts a cycle that has no bound, and its most
  reachable trigger is an erasure.** `IProjectionStore.UpsertAsync` replaces the state with something
  that was not folded from any known prefix, so the store correctly stops claiming a position for it.
  The next batch then finds an unpositioned row, ADOPTS it, folds itself onto whatever state is there,
  and stamps a high position — so the row asserts a fold over everything up to that position while
  holding only the tail.
  **This is not a one-time cost.** The bound previously stated here — that an accumulating projection
  double-counts once at adoption — assumed a row becomes positioned exactly once; any later
  unconditional write returns it to unpositioned and the re-fold recurs. `UpsertAsync` is public
  surface, so a seed script or an administrative fix-up produces the same state from outside the
  framework.
  **The framework no longer produces this state itself.** Recovering a fully erased aggregate used to
  reach it — the replay yielded no position, so the write took the unconditional path on the key the
  apply path uses. Recovery now re-folds at the position the row holds instead, so what remains is
  reachable only from outside: an unconditional write by consumer or operator code.
  **Refusing adoption is still not implemented**, which is why this is documented rather than closed.
  Two of the three reasons previously given here no longer hold and are corrected rather than deleted:
  the rebuild and the apply path now derive the projection id from one shared derivation, so they no
  longer write disjoint key spaces, and recovery does preserve a position it can honestly keep. What
  remains true is that recovery is single-aggregate and is driven by nothing in the framework, so there
  is still no automatic path that rebuilds a projection row the apply path reads. Refusing adoption
  needs the two unpositioned states told apart first — a row that was never positioned and holds a
  complete fold, where adoption is correct, versus one whose position was destroyed, where it is not.
  Refusing both would convert a silent miscount into a permanent stall.
  **Consumer obligation until this closes:** treat `UpsertAsync` on a projection whose store records
  positions as an administrative operation, not a routine one, and prefer replaying through the
  recovery path for a specific aggregate over writing its projection directly.

- **All eight providers now execute the conformance kit against real infrastructure.** SQL Server,
  PostgreSQL, MongoDB, DynamoDB, Elasticsearch, OpenSearch, Cosmos DB and Firestore each run every arm
  against a real server or emulator, never a mock, with availability asserted rather than skipped —
  an arm that passes by being skipped is indistinguishable from one that passed by working, and the
  property under test is an engine behaviour that cannot be simulated.

  **Five of the eight needed a code fix to reach that state, and none of those defects was visible in a
  reading of the C#.** Four could not execute their own conditional write at all. The fifth is the one
  worth knowing about as a consumer, because it failed in the direction that looks like success: the
  Cosmos read returned its state and its position from one expression whose first half MUTATED the
  document node, stripping the framework metadata before the position was read from it. Every read
  therefore reported no stored position. No stored position means adoptable, so every conditional write
  silently degraded into an unconditional one — the store reported success while providing none of the
  exclusion it advertised, and two writers racing from one read were both admitted. Nothing about that
  is visible without executing it against the engine.
- **The read-then-write providers can refuse spuriously.** On Cosmos, Elasticsearch and OpenSearch any
  unrelated write moves the version token, so a caller can be told `Superseded` while the position is
  unchanged. It re-reads and retries, which is correct; the cost is an extra round trip, not a wrong
  answer.
- **A deleted projection is RECREATED by the next write, and the paragraph that stood here said the
  opposite.** The superseded text read: *"a write against a missing row reports `Vanished` and the row
  is not recreated. A projection deleted for any other reason therefore stops being updated until it is
  rebuilt."* **That is false, and false in the direction that reassures.**

  `Vanished` is reachable only on the branch where the caller supplies a position it read. Once a row is
  gone, every caller reads nothing, so every subsequent write carries no expected position and takes the
  create-or-adopt branch instead. Enumerated across all eight stores, **seven cannot return `Vanished`
  to a caller that read nothing** — the create-when-absent arm fires first and the write succeeds. Cosmos
  DB is the lone exception, and only through one sub-path.

  So the real outcome is neither of the two the old text offered, and it is worse than both: the row is
  **recreated**, folded only from whatever tail the reader delivers next, and stamped with a position
  asserting a full fold over everything up to it. That is precisely the state this seam exists to make
  impossible — a row claiming a prefix it does not hold.

  **What this means for you.** Deleting a projection row out of band is not a supported way to reset it.
  Do not delete rows to force a rebuild, and do not treat a deleted row as a quiescent state; the next
  batch will resurrect it with a position it has not earned. To reset a projection, replay it through
  the recovery path for the aggregates concerned. For erasure specifically the exposure is narrower,
  because erasure also tombstones the source events, so a resurrected row folds tombstones rather than
  personal data — but the position it claims is still wrong.
- **A permanently-losing writer redelivers forever rather than losing events, and that trade is
  deliberate.** When a conditional write is refused AND the store is BEHIND the position the fold tried
  to reach, the apply path raises rather than returning quietly: those events are in neither the
  projection nor the stored position, so reporting success would let the reader advance its checkpoint
  past them and lose them silently. The async host catches that, declines to advance, and the batch is
  redelivered — which resolves on the next pass in every case we can construct, because the re-read
  either finds the events already folded (and filters them out) or folds them successfully. If some
  other writer were to hold the projection behind indefinitely, this becomes a redelivery loop rather
  than progress. That is a livelock, and it is the failure we chose: it is loud — the fault is logged
  and recorded against the projection's health on every pass — whereas the alternative is events
  vanishing from a read model with nothing to indicate it.

