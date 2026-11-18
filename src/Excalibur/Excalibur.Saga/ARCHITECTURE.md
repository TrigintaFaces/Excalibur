# Architecture — Excalibur.Saga

> **Guarantee contract for saga state persistence.** This document is the source of truth for *what tenant
> isolation and concurrency guarantees the saga stores provide, per provider, and how each is achieved*. It is
> a contributor + integrator reference. Keep it current: any change to a keyed read, a version-gated write, or
> a tenant predicate updates this file, verified at architectural review.

## Tenant isolation guarantee

**The guarantee is stated per provider, in one of exactly two forms. A single sentence covering both is
forbidden, because the difference is material rather than cosmetic.**

| Form | Statement | What is true |
|---|---|---|
| **Server-side** | *A tenant's read cannot **retrieve** another tenant's row.* | The tenant term is in the query the database evaluates. The row never leaves the database. |
| **Client-side** | *A tenant's read cannot **return** another tenant's row; the row is retrieved and discarded before it reaches the caller.* | The row **leaves the database and enters this process** before being rejected. That changes what is true for compliance, for memory, and for a crash dump. |

Where a server-side predicate is achievable it is **required**. Client-side discard is acceptable only where
the provider genuinely cannot express the predicate — a point read that addresses a document by identifier
cannot carry one — and there it is **a stated gap with a test**, not a design preference.

### Identity and match are two different questions

**A reader who conflates them concludes that two tenants can coexist at one saga identifier when they cannot.**
They are separate properties and this document states them separately:

| Question | What it decides | What it cannot do |
|---|---|---|
| **Does the IDENTITY carry the tenant?** | Which rows can *exist*. Two tenants at one saga identifier are two rows, or they are one. | Nothing, if the answer is no — no later check can manufacture a second row. |
| **Does the MATCH carry the tenant?** | Whether a statement is *willing* to touch a row it addressed. | Create a row for the second tenant. It can only ever *subtract*. |

The saga identifier is a business correlation key — an order number, a customer reference — so two tenants
legitimately run a saga at the same one. When the identity does **not** carry the tenant, both tenants address
a single row, and the match term is the only defence available: it correctly refuses the cross-tenant
overwrite, and in doing so it also refuses the second tenant a saga of its own. The isolation control
degenerates into an estate-wide uniqueness constraint on the saga identifier, and the refusal surfaces to the
second tenant as a concurrency or isolation failure on a saga it is creating for the first time.

**Both properties now hold on every shipped provider.** The identity carries the tenant, so a cross-tenant
write is unaddressable rather than merely refused; the match term is retained on top of it.

| Provider | Identity carries the tenant | Match carries the tenant | Identity as stored |
|---|---|---|---|
| SQL Server | **Yes** | Yes | `PRIMARY KEY CLUSTERED (TenantId, SagaId)` |
| PostgreSQL | **Yes** | Yes | `PRIMARY KEY (tenant_id, saga_id)` |
| Oracle | **Yes** | Yes | `PRIMARY KEY (TenantId, SagaId)` |
| MongoDB | **Yes** | Yes | `_id` = `t:{tenantId}:{sagaId}` |
| Firestore | **Yes** | Yes (ownership comparison) | document id = `t:{tenantId}:{sagaId}_{sagaType}` |
| Cosmos DB | **Yes** | Yes (ownership comparison) | `id` = `t:{tenantId}:{sagaId}`; partition key remains the saga type |
| DynamoDB | **Yes** | Yes (write condition) | `PK` = `SAGA#t:{tenantId}:{sagaId}`; `SK` remains the saga type |

The tenant segment is total: a host with no tenancy resolves the framework single-tenant default and a
genuinely untenanted saga resolves the reserved untenanted sentinel, so no identifier can be produced without
one.

**Why the match term is retained where the identity already carries the tenant.** For any row a store wrote
itself the two cannot disagree — identity and stored owner are assigned once, from the same scope, and the
owner is never re-stamped — so the term subtracts nothing. It is kept because it is the only term that still
applies to a row the store did **not** write: one at a colliding identifier whose stored owner differs is
excluded rather than matched. It is not, on its own, what makes coexistence work.

### Read direction, per provider

| Provider | Form | Mechanism | Seam |
|---|---|---|---|
| SQL Server | **Server-side** | Tenant term in the keyed read and in the `MERGE` match | `Excalibur.Saga.SqlServer/Requests/LoadSagaRequest.cs:64` |
| PostgreSQL | **Server-side** | Tenant term in the predicate | `Excalibur.Saga.Postgres/Requests/LoadSagaRequest.cs:75` |
| Oracle | **Server-side** | `MERGE … ON` includes the tenant equality, so another tenant's row cannot match | `Excalibur.Saga.Oracle/Requests/SaveSagaRequest.cs:83` |
| MongoDB | **Server-side** | The `_id` addressed by a keyed read carries the tenant, and a tenant equality filter is composed on top | `MongoDbSagaStore.cs` — `BuildDocumentId` |
| DynamoDB | **Server-side** | `GetItem` is keyed by partition/sort key, and the partition key carries the tenant, so another tenant's item cannot be addressed. The ownership comparison after the fetch is retained | `DynamoDbSagaDocument.cs` — `CreatePK` |
| Cosmos DB | **Server-side** | `ReadItem` addresses id + partition key; the id carries the tenant (the partition key remains the saga type). The ownership comparison after the fetch is retained | `CosmosDbSagaDocument.cs` — `CreateId` |
| Firestore | **Server-side** | The document id fetched carries the tenant. The ownership comparison after the snapshot returns is retained | `FirestoreSagaStore.cs` — `GetDocumentId` |

Line references are anchors for a reader auditing the claim, not a stable contract — re-locate by the
described mechanism if a line has moved.

### Write direction, stated separately

**The write direction is not implied by the read direction.** A cross-tenant *overwrite* is the case a reader
most readily assumes is covered by a read-side sentence, and it is not — so each provider's write guarantee is
stated on its own:

Read this table together with the identity/match table above. **The first column is why a cross-tenant write
cannot be addressed at all; the second is what would refuse it if one ever were.** Neither column alone is the
guarantee, and the second column on its own was, before the identity carried the tenant, also the mechanism
that denied the second tenant its own saga.

| Provider | Unaddressable, because the identity carries the tenant | Refused, if a write did reach a foreign row |
|---|---|---|
| MongoDB | The `_id` is `t:{tenantId}:{sagaId}`, so a save under one tenant cannot name another's document | The update match binds tenant + document id + version together; it matches zero documents and falls to the no-resurrect failure |
| DynamoDB | The partition key is `SAGA#t:{tenantId}:{sagaId}` | A conditional expression carrying the tenant term is evaluated **server-side** on the update path. The fresh-insert path is guarded by an attribute-not-exists condition, which rejects any pre-existing item regardless of owner |
| Firestore | The document id carries the tenant | The ownership comparison runs *inside* the Firestore transaction that performs the write, so check and write are atomic — client-side, but race-free |
| Cosmos DB | The `id` carries the tenant | The existing document is read and ownership compared before the write is issued — client-side, and resting on that comparison rather than on the engine |
| SQL Server / PostgreSQL / Oracle | The tenant is a leading column of the primary key, so a save under one tenant cannot address another's row | The tenant term is part of the matching condition **unconditionally** — never contingent on whether the caller resolved a tenant. This distinction is the whole guarantee: a term added only for a scoped caller leaves an unscoped save matching on the saga identifier alone, and a match is a *write*, so it would update another tenant's row. The match term and the persisted term are both resolved through the keyed partition, so the untenanted case binds a reserved sentinel rather than a null — a null bind matches nothing and would write the row where no scoped read looks. |

### Estate-wide operations are deliberate, not gaps

Purging across every tenant is a separate, explicitly named operation. **All four document stores implement a
genuine per-tenant scoped purge**: the tenant is a first-class field on the document/item, so the scoped purge
applies it as a real server-side equality predicate alongside the completion/age filter, exactly as MongoDB
already did. It never refuses — the tenant term (`TenantScope.TenantId`) is total, so untenanted, the
single-tenant default, and a real tenant all bind a concrete predicate value. `PurgeAllTenantsCompletedBeforeAsync`
is the separate, explicitly named all-tenants sweep: it carries no tenant term **by design** and is an
operator-level operation, reachable only by calling it directly — keep it out of tenant-reachable code paths.

## Event dedup guarantee

**Replay protection applies ONLY to a delivery that carries a message identity, and the protection it gives
is a bounded window rather than exactly-once.** Stated falsifiably, as three separate conditions:

1. **A delivery carrying a message identity** (`IMessageContext.MessageId`) is deduplicated on that
   identity. A second delivery presenting the same identity to the same saga is ignored: the handler is not
   invoked and no state is written.
2. **The remembered set is bounded at 1000 identities per saga instance and evicted FIFO.** Past that bound
   a redelivery of an evicted identity **re-executes the step**. This is a bounded window, not an
   approximation of exactly-once.
3. **A delivery carrying NO message identity is processed and NOT deduplicated.** For such a delivery the
   saga is **at-least-once** and its handlers **MUST be idempotent**. The framework does not invent an
   identity for it, does not guess one from the payload, and does not reject it.

The identity is a property of the **delivery**, not of the payload. It is neither derived from nor influenced
by the event type, the saga id, or `ISagaEvent.StepId`. Every send this framework makes stamps a message id,
so condition 1 is the normal case; condition 3 is reachable only for an inbound message from a producer that
sends no id of its own.

**What the log tells an operator, and what it does not promise the business.** The first time an event type
arrives with no identity, the coordinator emits a **Warning** naming that type, and it emits it once per type
per process. Every such delivery also increments the counter `excalibur.saga.undeduplicable_deliveries`,
tagged with the event type — so a producer that never sends an id is visible in metrics rather than only in a
log line from startup. **That visibility is for an operator; it is not a guarantee to the business.** A
duplicated saga step is silent downstream, so the contract above states which deliveries are protected and
which are not, rather than resting on someone reading the warning.

### Why the identity is not derived from the payload

It was, and the derivation lost events. The key was `{Type.FullName}:{SagaId}:{StepId}`, or
`{Type.FullName}:{SagaId}` when no step id was set. `StepId` is a nullable property on a public interface
that nothing enforces — no analyzer, no validation, no registration-time check — so **every event of one
type reaching one saga composed the same key**: the first was processed and every later one was discarded as
a duplicate and **never executed**, silently, while the log line said "skipped duplicate event".

That failure direction matters, and it is the reverse of the bounded window's. The window's failure is
**re-execution**, which an idempotent handler absorbs. A collapsed key's failure is **zero execution**, which
no consumer obligation recovers: idempotency protects a step from running twice and does nothing for a step
that was never handed its event.

### Upgrading a saga that is already running

**The remembered identities are persisted inside saga state, and the format changed.** An identity written by
the superseded scheme cannot be produced again, so an entry already in a saved saga's set can never match a
new delivery. Those entries are **left in place, untranslated and inert** — every key this scheme writes
begins with a marker (`#`) that no superseded key can begin with, because those began with a
namespace-qualified CLR type name.

The consequence, stated plainly: **a saga that was mid-flight across the upgrade may run one step a second
time** if a delivery it had already recorded is redelivered. That is the at-least-once direction, and the
standing obligation that saga steps be idempotent covers it. The alternative — honouring the old keys — would
have re-created the collapse for every upgraded saga and kept dropping events indefinitely, which nothing
covers.

**There is no migration to run, and none is supplied.** The superseded entries are not merely stale, they are
**ambiguous**: a key that two distinct events collapsed onto records that *a* delivery of that type was
processed and cannot say which, or how many were discarded behind it. Nothing can decompose it, so nothing
rewrites it. A consumer who wants to avoid the one re-execution described above drains the saga before
upgrading; a consumer who does not is at at-least-once for one delivery per in-flight saga, which handler
idempotency already covers.

**Events already dropped by the superseded scheme are a different matter, and idempotency is no help with
them.** They were never handed to a handler, so there is no execution to make idempotent — the step ran zero
times. Recovering them is a replay, and it is evidence-based because the framework holds no record of what it
discarded:

- **Do not look in the remembered set for evidence.** A collided key appears exactly once however many
  events collapsed onto it, so the set's size is not the count of deliveries a saga processed. That is the
  ambiguity above, and it is why the set cannot be used to identify what is missing.
- **The determination is against your own stream and your own saga state.** The shape to look for is a saga
  that received more than one event of a single type while that type carried no step id. Compare the events
  your stream holds for that saga against the progress its state actually records; the difference is what was
  discarded. Nothing in the framework can make that comparison for you.
- **Re-deliver the missing events on this version.** Each delivery now carries its own identity, so a replay
  of two events of one type is two events and no longer collapses — which was not true before, where a
  re-delivery would have been discarded exactly as the original was. A saga too far diverged to replay into
  is advanced by hand.

**Consumer obligation:** saga steps **MUST be idempotent**. If a saga can process more than 1000 events, or
you need dedup with no bound, or your producers do not send message ids, place the transactional inbox in
front of the saga — the saga's own set is not a substitute for it.

The framework does not ship a durable dedup backstop for saga steps, and that is deliberate rather than
unfinished: message delivery is at-least-once one level down, the transactional inbox already owns unbounded
deduplication, and a second, saga-local copy of it would promise exactly-once where nothing else does.

Coordinator-owned identity evictions emit the `excalibur.saga.dedup_evictions` counter
and the `SagaDeduplicationEntryEvicted` warning after positive state-write acknowledgement.
The only metric label is `mode` (`ordinary` or `atomic`); identities and payloads are not logged.
An ordinary write is reported before subsequent timer recovery. An atomic retry reports only
after the same immutable batch is acknowledged as committed. Refused speculative attempts and
duplicate deliveries do not count. This is best-effort process telemetry: a crash or lost write
acknowledgement can omit a real commit; it is not a durable exactly-once eviction ledger.
Direct collection mutation and deserialization are outside these coordinator diagnostics.

### Evidence (conformance)

Two arms exercise the real coordinator rather than the id set in isolation: a safety arm redelivering an event
still inside the window and asserting the step does not run again, and a liveness arm driving one saga past the
bound and asserting that a redelivery of the evicted first event **does** run again. The liveness arm is the
load-bearing one — it fails the moment the bound or the eviction policy changes without this paragraph changing
with it.

**Both of those arms hold the state in memory, so neither can observe a store that loses the set.** They
hand the coordinator the same object on every load, which is the right shape for testing the dedup
decision and the wrong shape for testing whether the decision survives persistence. A third arm covers
that half: it round-trips a derived saga state through the serializer every durable store persists
through and asserts the remembered ids come back, with a plain settable property asserted first as a
control so a broken round trip cannot be mistaken for a lost set.

**Why that arm is load-bearing rather than redundant.** The remembered ids live in a get-only collection,
which the serializer writes and then discards on read unless the property can be populated. When that
happens the set returns empty, every event looks new, and the bound stated above is not 1000 but zero —
the guarantee reads as satisfied while nothing is deduplicated anywhere durable.

A further group of arms binds the identity condition itself, each RED-proven against a mutant of the
coordinator:

- **Three deliveries agreeing on type, saga and step, differing only in message identity, all execute.** Red
  against the superseded derivation.
- **A redelivery presenting the same identity executes once.** This is the non-vacuity control: without it
  the arm above is satisfied by a coordinator that deduplicates nothing at all.
- **Identity-less deliveries all execute, record nothing, warn once per type, and increment the counter once
  per delivery.** Four deliveries across two event types are asserted to produce four executions, an empty
  remembered set, exactly two warnings, and per-type counts of three and one. Red against a coordinator that
  falls back to the derived key, against one that warns once per process, and against one that counts once
  per type.
- **A superseded-format key seeded into a saga's set does not block a new delivery, is not removed, and the
  key written alongside it carries the scheme marker.**
- **The key written contains no part of the event type name, namespace, or saga id.** This is the structural
  arm: it goes red if any payload-derived component is reintroduced, including a qualification prefix added
  for readability, which is how the collapsed key arrived the first time.

**What is NOT verified.** These arms run against an in-memory store, so they bind the coordinator's decision
and not the persistence of the new key format across each of the eight saga stores. The remembered set is a
collection of opaque strings and its round trip is already covered by the arm above, so the format change
does not alter what the stores serialize — but no arm exercises a real store across the format change, and a
real-infrastructure conformance arm per store remains owed.

## Consumer obligations

- **The document stores compose the tenant into the stored key, so upgrading to this behaviour is a
  storage-format change.** A saga document written by an earlier release is keyed on the saga identifier
  alone and is **not addressable** under the current key shape. The stores detect this and refuse rather
  than proceed — see the next obligation — but nothing rewrites those documents for you. Before upgrading a deployment
  with sagas in flight on MongoDB, Firestore, Cosmos DB or DynamoDB, drain them or re-key them — export each
  document, prefix its stored key with the tenant segment shown in the identity table above, and re-import.
  The relational stores are unaffected: their tenant column already exists and the tenant is part of the
  primary key.
- **All four document stores now REFUSE rather than restart when they find an unmigrated document, so the
  re-key procedure above is enforced and not merely documented.** Left undetected, an unaddressable document
  fails silently and in the worst available way: the store reports no saga in flight, the caller treats a
  saga that is already part-executed as new and starts it again — re-firing every compensating action and
  every external call that has already happened — while a continuation event for that saga is instead
  **dropped**, leaving its progress stranded rather than repeated. On the create path the conditional-create guard
  *succeeds*, because from the store's view nothing is there, so a second saga is written beside the
  original. Each store now probes for a legacy-shaped key at the first point it would act on the **absence**
  of a document, and throws naming the collection, container or table and the offending key. Read the
  refusal as an instruction to run the re-key procedure, not as a transient fault: it is permanent until the
  documents are migrated, and a retry cannot clear it.
- **Do not register the all-tenants purge for tenant-facing injection.**
- A saga's tenant is bound at creation. Saving an existing saga under a different tenant is refused, not
  merged — treat that failure as an escalation, not a retry.

## Evidence (conformance)

**TWO conformance artifacts bind this contract, and a coverage claim anywhere in this file must say which
one it measures or be measured against the UNION of both.** They are
`Excalibur.Testing.Conformance/Conformance/SagaStoreConformanceTestKit.cs`, which ships to consumers, and
`tests/Shared/Tests.Shared/Conformance/Saga/SagaStoreConformanceTestBase.cs`, which is internal. Neither is
a superset of the other.

This is stated because the identical structure in another subsystem of this framework cost four
work-streams: one gap bullet dropped a three-word qualifier from a sentence that was true a few lines
above it, asserting over the contract what was only true of one kit, and work was allocated against a gap
that did not exist. **An unscoped restatement of a scoped claim is more dangerous than a stale claim,
because the correctly scoped original sits nearby and nothing about the file looks wrong.**

The gap bullets below that say *no conformance test detects it* were checked against BOTH artifacts and are
true of both — neither covers emitted-message loss, dispatch failure during a flush, or timeout retirement.
That is a measurement rather than an assumption, and it is recorded so the claims do not have to be
re-derived. They remain true only while that holds: anyone adding coverage to either artifact owes a pass
over these bullets.

Each provider carries a dedicated tenant-isolation suite with **both** arms — a safety arm proving one tenant
cannot load another's saga, and a liveness arm proving a caller still loads its **own** saga. The liveness arm
is load-bearing: a store that returned nothing to anybody would satisfy the safety arm alone.

| Provider | Suite | Gating |
|---|---|---|
| MongoDB | `MongoDbSagaStoreTenantIsolationShould` | **Non-skipped** — asserts container availability rather than skipping, so the tenant filter is evaluated by the real engine |
| Cosmos DB | `CosmosDbSagaStoreTenantIsolationShould` | Real emulator via container fixture |
| DynamoDB | `DynamoDbSagaStoreTenantIsolationShould` | Real container fixture |
| Firestore | `FirestoreSagaStoreTenantIsolationShould` | Real container fixture |
| Oracle | `OracleSagaStoreTenantIsolationShould` | Real container; also asserts another tenant cannot match or overwrite a same-id row |
| PostgreSQL | `PostgresSagaStoreTenantIsolationShould` | Real container |

**The legacy-key refusal carries its own per-provider suite**, each with the same two arms. The safety arm
seeds a document under the pre-tenant key through the raw provider client — the store can no longer write
that shape — and requires both a load and a create to refuse, naming the collection, container or table, and
requires the seeded document to be still exactly where it was afterwards. The liveness arm is what makes the
first mean anything: a probe that refused unconditionally would satisfy the safety arm on its own, so the
liveness arm seeds a correctly-keyed document and requires an absent saga to load as "not found" and a new
saga to then be creatable.

| Provider | Suite |
|---|---|
| MongoDB | `MongoDbSagaStoreLegacyKeyRefusalShould` |
| Cosmos DB | `CosmosDbSagaStoreLegacyKeyRefusalShould` |
| DynamoDB | `DynamoDbSagaStoreLegacyKeyRefusalShould` |
| Firestore | `FirestoreSagaStoreLegacyKeyRefusalShould` |

**Coexistence at a shared saga identifier is proven by its own arm**, in the shared conformance kit rather
than per provider: `TenantPartitions_MustNotOverwriteEachOthersSagaWithTheSameId`. Its order is the point.
Two tenants save a saga at the **same** identifier; the arm first asserts the second tenant reads back **its
own** state, and only then that the first tenant's state is untouched. Asserting the second half alone would
be satisfied by a store that refused every write — which is precisely the failure mode that a tenant term
without a tenant-carrying identity produces, so the liveness half is what distinguishes a working isolation
control from a denial of creation.

Version-gated concurrency — no-overwrite and no-resurrect — is enforced separately by the per-provider
concurrency conformance suites against real containers.

## Limitations

- **The legacy-key refusal refuses; it does not migrate.** Which tenant owns an existing untenanted document
  is a question about the deployment rather than about the data, so no store can decide it. Nothing is
  modified: the refusal converts a silent restart into a loud failure while the saga state and its
  correlation are still intact, and the re-key procedure under *Consumer obligations* remains manual.
- **The refusal fires at the first decision that turns on absence, not at startup.** A store that has not
  yet been asked for a saga it cannot find has not probed, so a process can start cleanly against an
  unmigrated collection. This is deliberate — probing at initialisation would cost a query on every process
  start, on every serverless cold start, forever, to detect a condition that can only hold across a one-time
  upgrade — and it costs nothing in coverage: a legacy document is by definition absent from the store's
  point of view, so the load that would restart the saga is exactly the call that triggers the probe.
- **On DynamoDB the check reads a single scan page.** The tenant appears only in the partition key and
  DynamoDB offers no ordered access across partitions, so the check is a filtered scan rather than an index
  range read, bounded to one page so a large correctly-keyed table does not pay for a full scan. A table
  upgraded in place carries the old shape on every saga item, so the first page cannot miss it; a table that
  holds both shapes and whose legacy items all fall beyond the first page — which takes a partial rollback
  to produce — is not detected.
- **On the document stores, confinement is now a property of the key rather than of a predicate.** A point
  read cannot carry a tenant predicate on Cosmos DB, Firestore or DynamoDB, which is why the tenant was moved
  into the identifier the read addresses: another tenant's document is not filtered out after arriving, it is
  never named. The ownership comparison after the fetch is retained as a check on documents these stores did
  not write, not as the confinement mechanism.
- **Coexistence is proven, not merely permitted.** The conformance arm that covers this asserts the liveness
  half first — the second tenant must save and read back **its own** saga at the shared identifier — before
  it asserts that the first tenant's state is untouched. A store that refused every write would satisfy the
  overwrite assertion alone, which is exactly the failure this ordering exists to catch.
- **The cross-tenant overwrite guarantee is engine-enforced on some stores and code-enforced on others.** The
  table above says which. On the code-enforced stores the guarantee depends on a comparison in this
  repository, so treat any refactor of a saga write path as touching a security boundary.
- **A populated tenant identifier is not proof of enforcement.** It records the owner; it does not describe
  which of the two forms the store implements.

## Message dispatch guarantee

### Selected primary SQL atomic profile

The explicitly selected primary SQL durable-transition profile provides **at-least-once command
emission; duplicates possible on recovery; command handlers MUST be idempotent**. Commands and events
are prepared before the transaction, then their original encoded payloads and identities are written
to the selected SQL outbox in the **same transaction and commit** as saga state and processed-delivery
evidence. A failure before commit rolls back both state and effects. After commit, the existing outbox
publisher can independently discover and deliver the original commands even if the coordinator fails
before dispatch; repeating the original delivery does not invoke the handler or mint another batch.
Outbox delivery and downstream business processing are separate outcomes; this is not exactly-once
delivery or a promise that broker acceptance means the consumer has completed its work.

Select durable transitions explicitly with source-generated state/payload metadata and the matching
SQL saga, outbox-preparation and, when configured, timeout participants. Apply the complete supported
schema before starting writers. The primary SQL profile requires an owned connection-string saga
registration and its supported table binding; a borrowed connection factory, an unsupported participant
or a different selected logical backend must not silently fall back to a state-only commit. See the
SQL provider README and `samples/04-reliability/PrimarySqlAtomicSaga` for the concrete composition.

Configured schedule/cancel mutations and completion's cancel-all participate in that transaction.
A profile without a configured timeout store or timer effects needs no fabricated timer participant.
Retained original timeout authority, rather than payload presence, gates receiver admission: queue
retirement can remove a payload before the receiver runs, and public cancellation still revokes that
queued authority. The profile preserves consumer routing and partition/group/sequence metadata;
private intent ordinals do not promise FIFO or a global delivery head. Retained commit evidence also
survives ordinary outbox payload cleanup; its identity/history must not be independently discarded
while the original delivery can recur.

**Enforcing real-SQL controls.**
`SqlServerSagaCommandOutboxRecoveryShould.PreserveCommittedCommandsAcrossPostCommitFaultAndSameDelivery`
injects a fault after independently confirmed commit, verifies both original commands remain
claimable, and subsequently delivers them with the actual outbox publisher, with no repeated handler,
replacement rows or work on the next drain.
`RollBackRealStateReceiptTimerAndBothCommandsThenAllowHealthyTransition` injects a controlled failure
on the second real outbox insertion, requires state and every provisional effect to roll back, then
proves healthy retry and actual delivery. `KeepHealthyCommandEmissionAndCompletionOnRealSqlState`
retains the ordinary no-crash liveness control; additional profile controls exercise mixed
commands/events/timers, backend mismatch, no-timeout completion and queued public cancellation.
These are primary SQL guarantees, not all-provider, NativeAOT, database-restore or every-crash
certification. Other profiles retain the limitations below.

### Ordinary state-only profile

Commands and events emitted by a saga step are dispatched **after** the saga state is committed, and
are **not** part of that commit. The saga buffers them in memory; the coordinator saves the state and
then flushes the buffer.

A process failure between those two steps loses those messages permanently. The commit records the
triggering event as processed, so redelivery is skipped by the deduplication check and the messages
are never re-emitted. This is **at-most-once relative to the state commit**, a known residual of
the ordinary state-only profile. The selected atomic profile above does not upgrade this path.

**A failure DURING the flush needs no process failure at all, and it is still a loss — but it is no
longer SILENT.** The flush calls the dispatcher per buffered message. A dispatch can fail by RETURNING an
unsuccessful result rather than by throwing — the timeout middleware with throw-on-timeout disabled, and
a tripped rate limiter, both do exactly that. That result used to be **discarded**, so such a failure was
neither retried, recorded, logged nor counted, and a lost saga message was indistinguishable from a
delivered one at every log level.

It is now reported. Each unsuccessful dispatch is logged at **Warning** under event id
`SagaEmittedMessageNotDispatched` with the saga identifier, the message type, and the dispatcher's own
error text (`SagaBase.cs`, inside the flush). The loop still does **not** stop, and the message is still
**not** retried: both are deliberate. The state is already committed and the triggering event is already
marked processed, so the redelivery that raising would provoke is the one the deduplication check
refuses — raising would surface an error for durably committed work without recovering anything, while
abandoning the remaining messages.

**So the loss is unchanged and only its DETECTABILITY improved. Do not read the log entry as a
guarantee.** An arbitrary subset of a step's messages can still be lost while the saga durably records
the step as complete; what changed is that each loss now names itself. Recovery is still **UNVERIFIED**:
no conformance test detects the loss, and nothing re-emits the message.

**Evidence for the reporting half only.** `SagaBaseShould.ReportAnEmittedMessageTheDispatcherCouldNotDeliver`
RED-detects the discard, and its liveness partner
`SagaBaseShould.StaySilentWhenAnEmittedMessageWasDelivered` pins that a delivered message is not reported
— without which a flush that cried wolf on every dispatch would satisfy the first arm.

**A dispatch that THROWS is a separate case and is deliberately left alone.** It aborts the loop, so the
remaining messages are never attempted, and the exception reaches the host. That is loud and recoverable
by the host's own error handling, which is a materially different situation from a result quietly
discarded; the discriminator applied here is silence, not severity.

*Superseded, quoted so a reader who absorbed it recognises it: "A failure part-way through the flush has
the same shape on a smaller scale. Messages already dispatched stay dispatched, the remainder do not, and
nothing retries them." That described a flush that STOPS at the first failure, leaving a prefix and a
remainder. The flush does not stop, so there is no remainder, and a reader reasoning about a
dispatched-prefix/undispatched-suffix split was reasoning about a shape this code does not have.*

**The timer intents sit on the SAME side of the commit as the messages, and the apply runs first.**
Scheduling a timeout, cancelling one, and the cancel-all that completion performs are buffered during the
handler (`SagaBase.cs:92`) and applied by the coordinator only after the state commit returns
(`SagaCoordinator.cs:419` then `:426`), ahead of the message dispatch at `:431`. So a refused commit
applies no timer effect at all: the row a schedule would have created does not exist, and the row a
cancellation would have retired is still there and still deliverable. A store raising a concurrency
conflict is the normal operating mode of an optimistically concurrent store under multi-instance
deployment, and that case no longer strands or orphans a timer.

Timers are applied before messages because a dispatched message can return as a new delivery. A
reply-driven cancellation running against a row that does not exist yet would retire nothing, and the
schedule would then land behind it as a row no state references.

**Evidence.** `SagaCoordinatorTimeoutStoreShould.WriteNoTimeoutRowWhenTheStateCommitIsRefused` and
`SagaCoordinatorTimeoutStoreShould.RetireNoTimeoutRowWhenTheStateCommitIsRefused` RED-detect the
pre-commit timing, driving a real `ConcurrencyException` out of the state store. Their liveness partner
is `SagaCoordinatorTimeoutStoreShould.ApplyTheTimerBeforeDispatchingWhenTheStateCommitSucceeds`, which
pins the order and without which a coordinator that applied nothing at all would satisfy both safety
arms. All three go RED when the apply is moved back ahead of the commit.

**Ordinary timer recovery is conditional on the selected state and timeout stores.** The coordinator
persists the original ordered timer journal with state and processed-delivery evidence, then applies it
through the selected timeout store's `ISagaTimerRecoveryStore` facet. Recovery runs before completed
and duplicate-delivery short-circuits, preserving the original IDs without invoking the handler again.
An uncertain application acknowledgement retains the journal for exact receipt reconciliation rather
than minting replacement timers. State saves compare lifetime incarnation and version; pending journals
are protected from purge. For application-managed updates, call `LoadAsync`, change business fields on
the returned snapshot, then call `SaveAsync`. This preserves opaque lifetime and version metadata
automatically. A new object with only a copied saga ID and version is not an equivalent expected-state
update and can conflict; do not replace its concurrency metadata with values guessed at save time. Receiver cancellation cutoffs and retained retirement authority prevent an
older journal from resurrecting cancelled or already retired work.

This requires durable state, a mediated receiver capability on the actual selected timeout store, and
matching schema. Apply SQL Server state `05-SagaTimerRecoveryState.sql` and receiver
`06-SagaTimerRecovery.sql`, PostgreSQL state `02-SagaTimerRecoveryState.sql`, or Oracle state
`02-SagaTimerRecoveryState.sql` and receiver `03-SagaTimerRecovery.sql` after their base schemas.
Quiesce old writers during migration; the framework does not auto-migrate. In-memory recovery exercises
the same protocol only within its process lifetime and is not durable across process loss.

**Enforcing controls.**
`SagaCoordinatorTimeoutApplyRecoveryShould.RecoverTheCommittedTimerOnSameDeliveryWithoutRehandlingOrMintingAnotherId`
retains its one-shot fault before timer insertion after committed state/dedup, and requires a new
coordinator to recover the original timer without rehandling. Its healthy controls preserve timer-before-
message dispatch with and without a command. `OrdinaryTimerRecoveryAuthorityShould` checks own-batch
ordering, external cancellation, newer-timer preservation, retirement, lifetime recreation and fence
pairing. Provider controls must verify their own persistence boundary; these contracts and unit controls
do not certify every cloud provider, database deployment or NativeAOT driver.

This repairs the historical ordinary timer-apply gap, not the separate ordinary command/event dispatch
boundary. Ordinary messages remain outside the state transaction and retain their at-most-once dispatch
residual after a lost acknowledgement or postcommit failure. The explicitly selected primary SQL profile
instead commits its prepared command/event rows and configured timer effects with state. Timeout delivery
remains at least once for eligible work and requires idempotent handlers; the selected-store failure
holds and explicit recovery below are separate from journal recovery.

*Superseded, quoted so a reader who absorbed it recognises it: "The timer intents are on the OTHER side
of the commit... Scheduling and cancelling a saga timeout are written to the timeout store immediately,
inside the handler - they are not buffered like messages." Both are now buffered and the apply is gated
on the commit, so the pre-commit effects that wording described are prevented. The ordinary timer journal now addresses
that post-commit recovery boundary under the capability and persistence conditions above.*

**Consumer obligation.** Do not rely on a saga-emitted message as the sole trigger for work that must
not be lost. Trigger such work from a durable record — the event store or an outbox written in the
same transaction as the state — rather than from the dispatch.

**Known ordinary-profile gap.** Its messages are not written in the same atomic unit as saga state.
Use a supported durable-transition composition for work that depends on that guarantee; selecting the
primary SQL profile does not confer atomic participation on another provider or an ordinary state-only
registration.

## Timeout delivery guarantee

**Provider scope.** Durable timeout stores ship for SQL Server and Oracle. PostgreSQL, Cosmos DB,
DynamoDB, MongoDB and Firestore saga state stores do not by themselves provide matching durable
timeout storage. The default in-memory timeout store is process-local, even when saga state is
durable. Choose state and timeout storage separately within a supported composition; state-provider
parity is not a durable-timer guarantee.

A scheduled saga timeout is delivered **at-least-once**. The duplicate window is bounded by the claim
lease: a processor leases a due timeout before delivering it, and if that processor stalls for longer
than the lease without completing, another processor may re-claim and deliver the same timeout.
**Timeout handlers must therefore be idempotent.** A timeout delivered twice is inside this contract; a
timeout delivered zero times and then retired is not.

**How the floor is held.** A timeout row is retired only after a dispatch that both returned *and*
reported success. This distinction is load-bearing, because a dispatch can fail **without throwing**:
the timeout middleware returns an unsuccessful result rather than raising when its throw-on-timeout
option is disabled, and the rate-limiting middleware returns one when the limit is exceeded. Retiring
on the strength of "the call returned" would delete a timeout that was never delivered, leaving the
saga waiting forever for something that no longer exists. The delivery service inspects the result and
does **not** retire an unsuccessful one. A failed untyped result has no canonical retry evidence and is
retained as Unknown through the selected store's hold protocol. Thrown faults use the configured failure
classifier: Transient and Uncertain retain normal lease-based retry, while Permanent, Poison and Unknown
are held for explicit recovery. A retirement or cleanup fault after a successful dispatch is not
reclassified as a new handler failure; caller cancellation does not grant a successful disposition.

This closes the case where the dispatch *reported* a failure. It does not establish that a handler ran —
see the first known gap below, which is the wider hole and is not yet closed.

**Evidence.** `SagaTimeoutDeliveryServiceShould.NotMarkDelivered_WhenDispatchReturnsFailedResultWithoutThrowing`
covers the returned-but-unsuccessful path, and
`SagaTimeoutDeliveryServiceShould.NotMarkDelivered_WhenDispatchFails` covers the throwing path. Their
liveness partner is `SagaTimeoutDeliveryServiceShould.ProcessDueTimeouts_WhenTimeoutsExist`, which
asserts that a successful dispatch still retires the row — without it, a service that never retired
anything would satisfy both safety arms.

Ownership is covered by
`InMemorySagaTimeoutStoreShould.RefuseAStaleClaimantAfterTheLeaseWasTakenOver_AndStillLetTheLiveOneRetire`,
which constructs the stale-claimant interleaving directly — claim, let the lease lapse, re-claim, then
attempt the first claim's retirement — and asserts both halves: the stale claimant is refused and the
row survives, and the live claimant still retires it. Cross-tenant confinement of the same call is
covered by `InMemorySagaTimeoutStoreTenantIsolationShould.ConfineAMarkDeliveredToTheCallingTenant`,
which shows that holding a valid claim is not sufficient when the ambient tenant does not own the row.

### Selected-store failure holds and explicit recovery

Before pumping, hosted timeout delivery obtains `ISagaTimeoutFailureStore` from the actual selected
`ISagaTimeoutStore` and awaits `ValidateFailureStorageAsync`. An unrelated separately registered facet
cannot satisfy this gate. Unsupported custom stores or unusable selected-target schema refuse startup;
standalone manual store APIs retain their documented legacy-schema behavior.

Confirmed holds retain original payload, timeout/saga identity and due time and exclude that work from
normal claim acquisition. Diagnostic reads still expose retained payloads. InMemory holds are
process-local (`HoldsSurviveProcessRestart` is false); supported SQL Server/Oracle stores persist their
hold state. State-store durability on another provider does not confer timeout-hold durability.
Provision SQL Server `07-SagaTimeoutFailureHolds.sql` or Oracle `04-SagaTimeoutFailureHolds.sql` for an
existing selected base timeout table; fresh provider timeout schemas include the fields. Standalone
nonowned timeouts do not require SQL receiver migration06. Apply only the authority/recovery schemas
required by the selected owned-timeout or coordinator profile, as its provider instructions specify.
Follow the actual-table/offline rollout instructions; there is no automatic migration.

A failed or uncertain hold write is not a delivery acknowledgment or proof of rollback. The worker
reconciles the original operation inputs, retaining at most `BatchSize` pending observations. At capacity,
it keeps reconciling/rebinding existing work but defers new dispatch without retiring claimed payloads;
normal lease expiry preserves later eligibility. These are bounded local observations, not a process-
durable blacklist. The selected store remains the authority for held/current/ended/refused outcomes.

After repairing the cause, an authorized operator uses bounded `GetHeldFailuresAsync` query pages in one
exact tenant partition (optionally one saga), then `TryResumeHeldFailureAsync` with the inspected token
and a retained command ID. Tokens/cursors are target/partition-bound expected-state handles, not access
credentials. Null resume means refusal. A successful resume preserves original ID/payload/due, rotates
the delivery cycle once and leaves normal claiming to obtain a fresh lease; same-command replay does
not rotate again. Canceled/replaced work and stale cycles cannot be resurrected. Retained receipt scope
is bounded source/latest-cycle metadata, not an indefinite command ledger. Held work is not automatically
retried until explicit resume; this mechanism is not a positive certificate that a saga handler acted.

`SagaTimeoutFailureOwnershipShould.RetainNonRetryableOriginalWithoutDispatchingItAgain` covers
Permanent/Poison/Unknown through normal real-store claim cycles;
`RetryKnownTransientThroughARealFreshClaimThenRetireOnSuccess`, healthy delivery and caller-cancellation
partners retain liveness and no false acknowledgment.
`InMemorySagaTimeoutFailureStoreShould` covers retained payload/claim exclusion, partition/target/epoch
refusal, conditional replay/resume and cancellation. SQL/Oracle owning controls establish their own
physical persistence boundaries. Existing hosted pending-capacity/reconciliation controls do not certify
arbitrary backend failover, every deployment or driver NativeAOT.

See [consumer hold and recovery composition](../../../docs-site/docs/sagas/index.md#recovering-held-timeout-deliveries).
The existing primary SQL sample's optional recovery demo uses the actual public storage protocol; it is
not a hosted-classifier end-to-end or Native-driver proof. Its default workload remains separate.

### Known gaps

- **A successful dispatch is not proof that a handler ran, and the row is retired anyway.** Inspecting
  the result closes the case where the dispatch reported failure; it does not establish that a timeout
  handler was reached. Four reachable paths end in a successful result with no handler invocation and the
  row deleted: a registered timeout type that is dispatchable but is not a saga event is ignored by the
  saga middleware; a saga that does not handle the event returns without acting; a host that composes the
  timeout delivery service without the saga handling middleware silently delivers nothing at all; and a
  delivery suppressed as a duplicate by an inbox reports success while the competing attempt may still
  fail. The first three are deterministic and need no concurrency. The common cause is that each of those
  layers can decline to act and none of them can report that it declined, so the caller cannot
  distinguish "handled" from "ignored". **UNVERIFIED**, and the widest of the gaps listed here.
- ~~Retirement is not claim-conditional.~~ **Closed.** Retirement now requires the claim it is
  completing. `ClaimDueTimeoutsAsync` returns each timeout paired with a per-claim token, and retirement
  takes that pair and reports whether it retired the row or found the claim superseded. Every store makes
  the ownership test and the removal one atomic step — a single conditional `DELETE … AND ClaimedBy = …`
  on the relational stores, one lock on the in-memory store — so there is no window between testing
  ownership and acting on it. A processor that stalls past its lease is refused and cannot destroy the
  live claimant's retry. The retirement call is only reachable from a claim, so retiring a timeout you do
  not hold is not expressible: the diagnostic read returns bare timeouts and claims nothing.
- **A timeout whose type cannot resolve is retained as Unknown, not retired.** Resolution remains
  limited to registered types; no arbitrary assembly/type scan is introduced. An older deployment that
  cannot construct a registered dispatch message requires operator repair and conditional resume as
  described above. This preserves source data, not automatic handler reachability or cross-version
  compatibility. `SagaTimeoutDeliveryServiceShould.RetainUnknown_WhenTimeoutTypeCannotBeResolved`
  and the non-dispatchable/no-constructor partners exercise the retained paths.
- **A dispatch satisfied without invoking a handler counts as delivered.** A result that succeeds
  because it was served from cache, or suppressed as a duplicate, currently retires the row even though
  no timeout handler ran. Treating those as undelivered is not simply a tightening: if the suppression
  is durable, the row would never retire and the timeout would be re-polled forever, so the correct
  rule has to preserve liveness as well as safety. **UNVERIFIED.**

**Consumer obligation.** Make timeout handlers idempotent, and do not rely on a timeout firing exactly
once. If a timeout drives work that must not be lost, have the handler record its own completion
durably rather than inferring it from the timeout having been delivered.
