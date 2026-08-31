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
| SQL Server | **Server-side** | Tenant term in the keyed read and in the `MERGE` match | `Requests/LoadSagaRequest.cs:32` |
| PostgreSQL | **Server-side** | Tenant term in the predicate | `PostgresSagaStore.cs:237` |
| Oracle | **Server-side** | `MERGE … ON` includes the tenant equality, so another tenant's row cannot match | `OracleSagaStore.cs:252` |
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

**Event dedup is bounded, and the bound is part of the contract.** A saga ignores an event id it has already
processed. The set of remembered ids is bounded at **1000 per saga instance** and evicted **FIFO**. Beyond that
bound, a redelivery of an evicted event **re-executes the step**. This is a bounded window, not an
approximation of exactly-once.

**Consumer obligation:** saga steps **MUST be idempotent**. If a saga can process more than 1000 events, or you
need dedup with no bound, place the transactional inbox in front of the saga — the saga's own set is not a
substitute for it.

The framework does not ship a durable dedup backstop for saga steps, and that is deliberate rather than
unfinished: message delivery is at-least-once one level down, the transactional inbox already owns unbounded
deduplication, and a second, saga-local copy of it would promise exactly-once where nothing else does.

### Evidence (conformance)

Two arms exercise the real coordinator rather than the id set in isolation: a safety arm redelivering an event
still inside the window and asserting the step does not run again, and a liveness arm driving one saga past the
bound and asserting that a redelivery of the evicted first event **does** run again. The liveness arm is the
load-bearing one — it fails the moment the bound or the eviction policy changes without this paragraph changing
with it.

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
