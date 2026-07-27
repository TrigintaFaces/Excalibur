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

### Read direction, per provider

| Provider | Form | Mechanism | Seam |
|---|---|---|---|
| SQL Server | **Server-side** | Tenant term in the keyed read and in the `MERGE` match | `Requests/LoadSagaRequest.cs:32` |
| PostgreSQL | **Server-side** | Tenant term in the predicate | `PostgresSagaStore.cs:237` |
| Oracle | **Server-side** | `MERGE … ON` includes the tenant equality, so another tenant's row cannot match | `OracleSagaStore.cs:252` |
| MongoDB | **Server-side** | Keyed reads compose a tenant equality filter evaluated by the engine | `MongoDbSagaStore.cs:408` |
| DynamoDB | **Client-side** | `GetItem` is keyed by partition/sort key only; ownership is compared after the item returns | `DynamoDbSagaStore.cs:239` |
| Cosmos DB | **Client-side** | `ReadItem` addresses id + partition key (the partition key is the saga type, not the tenant); ownership is compared after the document returns | `CosmosDbSagaStore.cs:297` |
| Firestore | **Client-side** | A document snapshot is fetched by id; ownership is compared after it returns | `FirestoreSagaStore.cs:210` |

Line references are anchors for a reader auditing the claim, not a stable contract — re-locate by the
described mechanism if a line has moved.

### Write direction, stated separately

**The write direction is not implied by the read direction.** A cross-tenant *overwrite* is the case a reader
most readily assumes is covered by a read-side sentence, and it is not — so each provider's write guarantee is
stated on its own:

| Provider | Cross-tenant overwrite | How it is refused |
|---|---|---|
| MongoDB | Refused **server-side** | The update match binds tenant + saga id + version together; a cross-tenant write matches zero documents and falls to the no-resurrect failure |
| DynamoDB | Refused **server-side** on the update path | A conditional expression carrying the tenant term is evaluated by the database, so the engine rejects the write. The fresh-insert path is guarded by an attribute-not-exists condition instead, which rejects any pre-existing item regardless of owner — so no silent overwrite occurs there either |
| Firestore | Refused **client-side, but race-free** | The ownership comparison runs *inside* the Firestore transaction that performs the write, so check and write are atomic |
| Cosmos DB | Refused **client-side** | The existing document is read, ownership is compared, and the write is abandoned before it is issued. Correct as written, but the guarantee rests on that comparison rather than on the engine |
| SQL Server / PostgreSQL / Oracle | Refused **server-side** | The tenant term is part of the matching condition **unconditionally** — never contingent on whether the caller resolved a tenant. This distinction is the whole guarantee: a term added only for a scoped caller leaves an unscoped save matching on the saga identifier alone, and a match is a *write*, so it would update another tenant's row. The match term and the persisted term are both resolved through the keyed partition, so the untenanted case binds a reserved sentinel rather than a null — a null bind matches nothing and would write the row where no scoped read looks. |

### Estate-wide operations are deliberate, not gaps

Purging across every tenant is a separate, explicitly named operation. On Cosmos DB, DynamoDB and Firestore a
*scoped* purge **refuses** rather than silently purging estate-wide, because a range delete has no per-document
reachability gate and refusing is the only honest option. MongoDB implements a genuine per-tenant scoped purge.
The all-tenants sweep carries no tenant term **by design** and is an operator-level operation — keep it out of
tenant-reachable code paths.

## Consumer obligations

- **On the client-side stores (Cosmos DB, Firestore, and the DynamoDB read path), scope at your own boundary
  as well.** The store will not let another tenant's state reach you, but the row is materialised in this
  process first. Do not treat a populated `SagaState.TenantId` as evidence that a read was refused at the
  engine.
- **Do not register the all-tenants purge for tenant-facing injection.**
- **Saga identifiers must be unique across tenants — this is your obligation, not the store's.** The
  idempotency record is keyed by saga identifier and idempotency key alone, with no tenant term, and the
  saga identifier is taken from the incoming event rather than minted by this library. Two tenants
  presenting the same saga identifier therefore share one idempotency record, and the consequence is
  **suppressed delivery, not disclosure**: one tenant marking a key as processed causes the other to skip
  a message it never handled. Fresh random identifiers satisfy this; identifiers derived from
  caller-supplied or externally-visible values may not.
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

Version-gated concurrency — no-overwrite and no-resurrect — is enforced separately by the per-provider
concurrency conformance suites against real containers.

## Limitations

- **Client-side discard is a real gap, not a wording choice.** On Cosmos DB and Firestore a point read cannot
  carry a tenant predicate, so another tenant's document is transported into this process before it is
  rejected. This is documented rather than eliminated because the provider cannot express the predicate on a
  point read; a query-based read path can and does.
- **The cross-tenant overwrite guarantee is engine-enforced on some stores and code-enforced on others.** The
  table above says which. On the code-enforced stores the guarantee depends on a comparison in this
  repository, so treat any refactor of a saga write path as touching a security boundary.
- **A populated tenant identifier is not proof of enforcement.** It records the owner; it does not describe
  which of the two forms the store implements.
