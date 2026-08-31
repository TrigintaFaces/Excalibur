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

### 4. Projections: rebuild is the erasure path

A projection is a read model rolled up from the event stream, not an independently erasable store. It does
not carry per-subject encryption keys and it has no erase endpoint of its own. **Its erasure guarantee is
structural: a projection rebuilt after a subject's aggregate has been tombstoned contains no trace of that
subject, because the rebuild replays the tombstoned stream and never applies the erased events.**

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

**Known gap.** A projection updated incrementally (not via full rebuild) between an aggregate's erasure and
its next rebuild may still reflect the pre-erasure state for that subject until a rebuild runs. Consumers
with a strict erasure SLA on a given projection should trigger a rebuild as part of handling an erasure
request rather than relying on the next incremental update to clear it.

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
| A projection rebuilt from a stream containing a tombstoned event omits the erased subject's data, retains a different subject's data on the same projection, and the rebuild completes rather than halting at the tombstone | `ProjectionRebuildErasureShould`, paired with `ProjectionPoisonHaltParityShould` (a genuinely corrupt, non-erasure event still halts the rebuild) |
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
  re-running the create script is a no-op that leaves the old shape in place. SQLite therefore also ships
  `Scripts/002_MakeEventAndSnapshotIdentityTenantScoped.sql`, which performs the same rebuild for both the
  event table and the snapshot table: each is renamed aside, recreated on the current tenant-scoped shape,
  and every carried-over row stamped with the reserved sentinel. It **stops at the shape** — see the next
  gap for why a static script deliberately does not attempt the single-tenant convergence.

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
