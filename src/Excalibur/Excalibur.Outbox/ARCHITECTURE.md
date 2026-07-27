# Architecture — Excalibur.Outbox

> **Guarantee contract for the transactional outbox.** This document is the source of truth for *what
> delivery guarantee the outbox provides and how it is achieved*. It is a contributor + integrator
> reference; the consumer-facing summary also appears in the published docs. Keep it current: any change to
> a claim/mark/fence path updates this file, verified at architectural review.

## Delivery guarantee

**At-least-once.** Every staged message is delivered to its transport **at least once**. Under a dispatcher
crash or a retry, a message **may be delivered more than once**; the duplicate window is bounded by the
retry floor **F**. The outbox is **not exactly-once**.

> **Consumer obligation:** message handlers **MUST be idempotent.** Design every handler so that processing
> the same message twice has the same effect as processing it once.

Sub-guarantees (invariants):

| # | Property | Statement |
|---|---|---|
| **S1** | At-most-once **per claim** | Two concurrent pollers never claim the same message — each claim returns a set **disjoint** from every concurrent claim. This is the property that stops two workers draining and sending the same row. |
| **R1** | Backoff floor | A failed message is **not** re-claimable within **F**, and **is** re-claimable after **F**. No zero-backoff retry hot-loop. |
| **R2** | Dispatcher ownership | Only the dispatcher that holds a message's current reservation may mark or unreserve it. |
| **R3** | Monotonic attempts | The recorded attempt count never decreases. |
| **F1** | Leadership fencing *(optional)* | On stores implementing `IFencedOutboxStore`, the high-water lives in a dedicated fence record, so a superseded leader presenting a stale fencing token is rejected fail-closed; it cannot mark-sent, claim, or delete the message. |

Ordering is **best-effort oldest-first within a claim** (by creation time); there is **no** cross-drain
total order. Use `PartitionKey` / `GroupKey` to keep related messages together.

## How it is achieved (the seam)

1. **Stage** — `StageMessageAsync` persists the message durably in the same transaction as the business
   write (the outbox pattern), so a message is never lost between commit and dispatch.
2. **Claim (S1)** — `GetUnsentMessagesAsync` is an **atomic claim, not a plain read**. Each provider uses
   its native atomic primitive so concurrent claimers get disjoint sets:
   - SqlServer — `UPDATE … OUTPUT` with `READPAST, UPDLOCK, ROWLOCK`
   - Postgres / Oracle / Marten — `FOR UPDATE SKIP LOCKED`
   - MongoDB — per-document `FindOneAndUpdate`
   - Redis — a Lua script (atomic read-decide-write)
   - Cosmos DB — single-document `IfMatch` ETag compare-and-swap
   - DynamoDb — conditional `UpdateItem`; Firestore — `runTransaction`
   - InMemory — in-process lock + leases (development / testing)
3. **Fail / retry (R1·R2·R3)** — `MarkFailedAsync` performs **one atomic write** that frees the lease, sets
   the next-attempt floor to `now + F`, and sets `attempts = GREATEST(attempts, n)`. Splitting these steps
   would let a crash leave a message lease-free with no floor — a retry hot-loop — so the single-write
   atomicity is part of the contract.
4. **Send + mark-sent** — after the transport acknowledges, `MarkSentAsync` removes the message
   (delete-on-sent stores) or flags it (tracking stores). `IOutboxStoreCapabilities.SupportsSentTracking`
   distinguishes the two; delete-on-sent stores report `false`.
5. **Leadership fencing (F1, optional)** — stores implementing `IFencedOutboxStore` carry a monotonic
   fencing token on claim and mark-sent; a token below the stored high-water is rejected
   (`StaleOutboxFencingTokenException`). The fence advance is **co-atomic with the claim** (e.g. Cosmos
   advances the high-water inside the same `IfMatch` replace that claims the row) so a superseded leader
   cannot slip a delivery between a separate fence-check and a separate claim.
   **Where the high-water is stored matters.** PostgreSQL, Oracle, MongoDB and SQL Server keep it in a
   dedicated fence record, independent of the message rows, so it survives cleanup and is advanced
   monotonically — a compare-and-advance that never lowers the mark. SQL Server's `OutboxFence` control table
   is updated by a single serializable `MERGE … WITH (HOLDLOCK)`: two concurrent leaders cannot both advance
   it, and a superseded leader's stale token is rejected fail-closed even after cleanup has purged the sent,
   token-bearing rows. The per-message reservation (R2) independently prevents two dispatchers from holding
   the same message.

## Consumer obligations

- **Handlers MUST be idempotent** (at-least-once ⇒ duplicates on retry/crash).
- **Set the retry floor `F` greater than the maximum expected delivery duration.** If a dispatcher pauses
  mid-delivery long enough for its lease to expire, a retry can overlap the in-flight send; `F > max
  delivery duration` keeps that window closed to normal operation.
- For related-message ordering, set `PartitionKey` / `GroupKey`; do not rely on global ordering.
- **The outbox table's primary key MUST be the message-id column alone.** If you provision the schema
  yourself, do not make it composite — not `(Id, TenantId)`, not `(Id, PartitionKey)`, not any wider key.

  This is load-bearing, not stylistic. The drain-then-mark protocol addresses a row by its id: a
  mark-sent, mark-failed, or delete targets `WHERE <id column> = @MessageId` plus a reservation-ownership
  guard. That is sound only while the id alone identifies at most one row. Under a composite key an id
  can address **more than one row**, and the two ways it fails are both silent:

  | deployment | what a single mark does | observable symptom |
  |---|---|---|
  | composite PK, no tenant predicate | marks **another partition's message** sent | that message is never delivered — **silent loss** |
  | composite PK, tenant predicate in play | matches **zero** rows | the claimed message is never marked — **infinite redelivery** |

  Neither raises an error. The first is indistinguishable from successful delivery; the second looks like
  a slow consumer. The shipped creation scripts satisfy this obligation, so a deployment that ran them is
  already correct — the obligation is stated for schemas provisioned by hand or by external migration
  tooling. Note that the shipped **dead-letter** table deliberately uses a composite key; that table is
  addressed differently and is not drained by this protocol, so do not take it as a pattern to copy onto
  the outbox table.

## Evidence (conformance)

Every provider derives `OutboxStoreConformanceTestBase`
(`tests/Shared/Tests.Shared/Conformance/Outbox/OutboxStoreConformanceTestBase.cs`) against a **real**
backing store (a live container; the arms are not skipped when infrastructure is present). Guarantee → test:

| Guarantee | Conformance test |
|---|---|
| S1 disjoint claim | `GetUnsentMessages_ConcurrentClaimers_ReceiveDisjointSets` (safety: sets never overlap; liveness: work *is* handed out) |
| R1 floor / re-claimability | owned-path re-claim arm: claim → fail-as-owner → not claimable within F, claimable after F, attempts recorded |
| R3 monotonic attempts | `MarkFailed_IncrementingRetryCount_TracksRetries` + the drain-reload attempt-restore arm |
| Full-field durability | `StageMessage_RoundTripsEveryConsumerSuppliedField_OnReload` |
| F1 fencing | `Fencing_MarkSentWithStaleToken_IsRejectedFailClosed`, `Fencing_ValidMonotonicToken_ClaimsAndDrains`, `Fencing_SupersededLeaderCannotMutateOrLoseMessage_AfterHandover`, `Fencing_HighWaterSurvivesCleanup` (the fence high-water outlives cleanup of sent rows; applies to every store advertising a durable fence, non-fencing stores self-skip) |

## Provider maturity

| Provider | Claim primitive | Fencing | At-least-once conformance |
|---|---|---|---|
| InMemory | in-proc lock + leases | via base | ✅ full |
| SqlServer | `UPDATE…OUTPUT` READPAST/UPDLOCK/ROWLOCK | ✅ durable — the high-water mark lives in a dedicated `OutboxFence` control table that cleanup never touches, so a superseded leader's stale token is still rejected after cleanup has purged the sent, token-bearing rows. The fence-first advance is monotonic (takes the maximum; never moves backwards) and fail-closed. The per-message lease independently prevents two processors claiming the same row. Verified against a live SqlServer container by `Fencing_HighWaterSurvivesCleanup` | ✅ full |
| Postgres | `FOR UPDATE SKIP LOCKED` | ✅ | ✅ full |
| Oracle | `FOR UPDATE SKIP LOCKED` | ✅ | ✅ full |
| MongoDB | `FindOneAndUpdate` | ✅ | 🚧 completing (retry-floor / ownership) — treat as **UNVERIFIED** until green |
| Redis | Lua | single-writer | 🚧 completing (retry-floor / ownership) — **UNVERIFIED** until green |
| Cosmos DB | single-doc `IfMatch` CAS | 🚧 co-atomic fence in progress | 🚧 completing — **UNVERIFIED** until green |
| DynamoDb | conditional `UpdateItem` | single-writer | 🚧 completing — **UNVERIFIED** until green |
| Firestore | `runTransaction` | single-writer | 🚧 completing — **UNVERIFIED** until green |
| Marten | `FOR UPDATE SKIP LOCKED` (Postgres) | single-writer | 🚧 completing — **UNVERIFIED** until green |
| ElasticSearch | `if_seq_no`/`if_primary_term` CAS | single-writer | 🚧 completing — **UNVERIFIED** until green. Near-real-time index: a claim must force a refresh after stage or it can see stale rows |

> A cell marked **UNVERIFIED** means the guarantee is *intended* but the non-skipped real-infra conformance
> arm proving it is not yet green for that provider — do not depend on at-least-once there in production
> until it is.

## Limitations

- **At-least-once, not exactly-once.** The dispatcher-ownership guard on the failure path is
  *process-granular*: a paused dispatcher whose lease expired can, on resumption, free a **newer**
  reservation held by the same process, so the same message may be re-claimed while an earlier delivery is
  still in flight. The resulting duplicate window is **bounded by F**, not eliminated. Exactly-once would
  require threading a per-attempt token through the failure path; it is not currently provided. Idempotent
  handlers make at-least-once correct.
- **Leadership fencing is opt-in.** Stores that do not implement `IFencedOutboxStore` operate
  single-writer / unfenced and rely on the deployment running a single active dispatcher (or a leader
  election) to avoid split delivery.
- **No global ordering.** Messages are handed out oldest-first within a claim only.
