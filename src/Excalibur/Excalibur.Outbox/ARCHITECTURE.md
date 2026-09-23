# Architecture — Excalibur.Outbox

> **Guarantee contract for the transactional outbox.** This document is the source of truth for *what
> delivery guarantee the outbox provides and how it is achieved*. It is a contributor + integrator
> reference; the consumer-facing summary also appears in the published docs. Keep it current: any change to
> a claim/mark/fence path updates this file, verified at architectural review.
>
> **Scope.** This contract governs the polling outbox family — stores implementing `IOutboxStore`
> (`src/Dispatch/Excalibur.Dispatch.Abstractions/Outbox/IOutboxStore.cs`). It does **not** govern the
> change-feed family (`ICloudNativeOutboxStore`), which makes materially weaker promises and is stated
> separately under *The change-feed family* below. The two families are mutually exclusive by construction,
> and that exclusivity is gated rather than merely asserted — see `OutboxCapabilityMatrixShould`.

## Delivery guarantee

**At-least-once.** Every staged message is delivered to its transport **at least once**. Under a dispatcher
crash or a retry, a message **may be delivered more than once**. The outbox is **not exactly-once**.

> **Consumer obligation:** message handlers **MUST be idempotent.** Design every handler so that processing
> the same message twice has the same effect as processing it once.

**A bounded store refuses rather than discards.** The in-memory store holds at most `MaxMessages` messages.
Reaching that bound never costs a delivery: capacity is reclaimed only from messages whose delivery is over
(sent, or terminally dead-lettered). When every message present is still awaiting delivery, `StageMessageAsync`
**throws** rather than evicting one, so the caller -- whose own transaction has not committed -- learns the
message was not accepted instead of believing it was queued.

> **Consumer obligation:** size `MaxMessages` for the drain you actually run. A store that is persistently
> full of undelivered messages is a drain that is not keeping up, and staging will fail until it does.

**Enforced** by `OutboxCapacityEvictionShould`, which carries both arms this claim needs:
`RefuseToStage_RatherThanDiscardAnUnsentLeasedMessage` is the safety half — a full store of undelivered
messages refuses rather than evicting one — and `EvictTheDeliveredMessage_WhenOneIsAvailable` plus
`EvictTheDeadLetteredMessage_RatherThanRefusing` are the liveness half, without which the first would be
satisfied by a store that refuses every stage forever. It needs no backing service and runs
unconditionally.

### The central invariant

> **D1 — Lease before dispatch.** A message is handed to a transport only from a lease this dispatcher won
> in one indivisible action at the store, **and every path that hands a message to a transport wins that
> lease first.**

The second clause is what makes the first worth having, and **it holds for the shipped drain paths.** All
three loops the default drain runs — pending, scheduled and retry — funnel into one claimed batch:
`MessageBusOutboxPublisher.PublishPendingMessagesAsync`, `PublishScheduledMessagesAsync` and
`RetryFailedMessagesAsync` each delegate to `DrainClaimedBatchAsync`, whose first act is the atomic claim
(each loop → `DrainClaimedBatchAsync` → `ClaimBatchAsync`). `OutboxProcessor` is
claim-only by construction. See *The publisher drain: every loop claims* for the per-loop table.

**Where D1 is bounded.** It is a statement about the drain paths this package ships. A consumer that
selects messages by its own query and hands them to a transport without going through the claim is outside
it, and S1 below is stated against the claim for that reason — a claim-scoped property is what a store can
enforce, since the store cannot see a dispatch it was never asked to authorise.

**D1 is UNVERIFIED.** The statement above was checked by reading the three call sites, and it is true of
the code as written. No conformance arm enforces it: nothing fails when a drain path is changed to hand a
message to a transport without claiming it first. S1's arm is not a substitute — it proves two concurrent
claims are disjoint, which says nothing about whether a dispatch was preceded by a claim at all. **The
falsification condition, so it is not left implied:** add a fourth loop, or redirect an existing one past
`DrainClaimedBatchAsync`, and every arm in this document still passes. Until an arm RED-detects that, D1
rests on review of the drain rather than on a test, and a reader should treat a change to the publisher's
loop structure as unguarded on this property.

Sub-guarantees (invariants):

| # | Property | Statement | Status |
|---|---|---|---|
| **S1** | At-most-once **per claim** | Two concurrent claimers never claim the same message — each claim returns a set **disjoint** from every concurrent claim. | Holds **on the claim path**. All three loops the default drain runs claim before dispatch. A caller that selects messages by its own query and hands them to a transport without claiming is outside this property, as D1 states; the store cannot enforce a claim it was never asked for. |
| **R1** | Backoff floor | A message failed through the claim path is not re-claimable within the floor **F**, and is re-claimable after **F**. No zero-backoff retry hot-loop. | Holds for the **claim predicate**, on both the plain and the computed-backoff failure paths, on every store implementing the computed-backoff seam — SQL Server, Postgres, MongoDB, Oracle and Redis. Each composes the caller's schedule with F as a maximum, so a shorter delay is raised and a longer one is preserved. On all five that maximum is taken over **durations** and anchored to the server clock, so no dispatcher clock reaches the persisted gate. |
| **R2** | Reservation ownership on **release** | Only the dispatcher holding a message's current reservation may **release** it (mark-failed / unreserve). | **Enforced — but by different mechanisms, to different depths.** Each store implements this in its own terms: a stored-identity comparison in the failure statement (SQL Server matches a `LeasedBy` column against the store's configured processor identity; Oracle matches the process-stable prefix of the stored claim token), an in-process comparison against the store's own identity (the in-memory store), or an exact claim-token match supplied by the caller (PostgreSQL, on the claim-scoped route only). **PostgreSQL's unscoped failure statement carries no ownership term at all**, by a deliberate choice recorded at the statement: a predicate a caller satisfies by omitting the value it guards is not a guard, so that store offers two statements rather than one with a hole. **The depth is decided TWICE — once by what the claim records, once by how much of it the release compares — and a store can do the first well and discard it at the second.** **Oracle** stamps a fresh per-claim token on both its ordinary and its fenced claim, and then its failure statement matches only the **process-stable prefix** of that token. The per-claim half is recorded and projected away, so the release is process-granular. **PostgreSQL** mints a per-claim token on every claim, and its claim-scoped route compares the whole token, so that route distinguishes two claim cycles of one process. Its unscoped statement carries no ownership term, so nothing is refused there. **SQL Server** records a per-claim token and then compares a different amount of it on each of its two routes: the claim stamps `{processorId}:{claimId}` per call (`Requests/GetUnsentMessagesRequest.cs:123`), the unscoped failure statement matches only the process-stable prefix of it (`Requests/OutboxFailureMark.cs:110-112`) and is therefore process-granular, while the fenced claim-scoped statement matches the whole stamped value (`Requests/FencedMarkMessageFailedRequest.cs:106`) and does distinguish two claim cycles of one process. The **in-memory** store is the same point from the other side: its claim-scoped route compares the recorded owner exactly (`InMemoryOutboxStore.FencedCompletion.cs:87`), but what the claim recorded is a per-store-instance identity (`InMemoryOutboxStore.cs:215`), so an exact comparison is still only store-instance-granular there. A release guard discriminates no more finely than the claim recorded, whichever route it takes. **Net effect, stated by capability and by named interface, because two nearly-identical capability names now sit in this paragraph and the two cases otherwise read as a contradiction.** A store that implements the claim-scoped completion (`IClaimScopedOutboxStore`) is handed the claim its caller holds, and on the one store that implements it, it compares the whole token, so two claim cycles of one process are distinguished — but that depth is the store's, not the interface's, and the enumeration below says which stores actually reach it; where that store also implements the fenced form of it (`IFencedClaimScopedOutboxStore`), the same statement additionally refuses a caller whose leadership tenure has been superseded. **Measured at the shipped source, by interface, because the two counts differ and a single number was wrong here before:** `IClaimScopedOutboxStore` — the unfenced claim-scoped completion — is implemented by **PostgreSQL alone**. `IFencedClaimScopedOutboxStore` — the same statement additionally refusing a superseded tenure — is implemented by **four** stores: **InMemory, Oracle, PostgreSQL and SQL Server**. **Implementing that interface is not the same as gaining a per-claim comparison**, which is the distinction the paragraph above draws and the reason the two counts here differ. On **Oracle, PostgreSQL and SQL Server** the statement compares the whole per-claim token, so a caller holding a claim has a per-claim comparison available on **three** stores, not one. On **InMemory** the same route compares its recorded owner exactly, but that owner is a per-store-instance value, so it separates store instances rather than claim cycles. **On the remaining stores — MongoDB, Marten, Redis and ElasticSearch, none of which declares either claim-scoped interface — the unscoped member is the only route available, the strongest comparison made there is a process identity, and two claim cycles of one process are NOT distinguished.** That second half is a warning and is stated widely on purpose. **What is NOT claimed here, and over what population.** Eleven outbox store packages ship, and they are not eleven of the same thing: **eight expose a failure-report member and are what this section is about; three (the change-feed family) expose no failure-report member at all**, so a statement about ownership on release is not weaker for them — it does not apply. **Six of the eight have been read directly**, and the statements above describe those six. **The two that have not been read are the Elasticsearch and Marten stores**; whether either refuses a report from a different dispatcher process is not characterised, because it has not been measured — do not read their absence from this section as an assurance about them. **Consequence, as the observable outcome.** A failure reported by an expired claim of a still-running dispatcher is accepted wherever the compared value is process-granular. It releases a reservation a live successor of that same process still holds, and clears the reservation deadline with it — so the duplicate-delivery window stops being bounded by the reservation timeout and falls back to the failure floor, which is typically an order of magnitude shorter. Sizing that floor is a consumer obligation. **Known gap — the fallback, not the route.** Two components complete outbox failures, and both now ask a store for the claim-scoped capability and use it where it is present. (A third component claims messages without ever completing a failure, so it is outside this row; "drain" is avoided here because it names a different set depending on whether one means claiming or completing.) **What remains is the fallback beneath them:** on a store that does not implement the claim-scoped completion — four of the eight stores that expose a failure-report member: MongoDB, Marten, Redis and ElasticSearch — the unscoped member is the only route available, and the strongest comparison made there is a process identity. A release guard can also discriminate no more finely than the claim recorded, so a store that records only a process identity, or compares only a prefix of what it recorded, gains nothing from the route alone. Closing this requires the remaining stores to implement the capability, or the unscoped member to carry an ownership term a caller cannot satisfy by omitting the value it guards. **Evidence.** `MarkFailed_ByANonOwner_ShouldNotReleaseTheClaim` binds the process-granular arm. **Read it as bound, not as run** — it returns without asserting where a store exposes no way to give a message a foreign owner, and where its backing service is absent. **No arm binds the per-claim depth, and none binds the same-process two-cycle case. Both are documented UNVERIFIED.** |




| **R2′** | Mark-sent is **not** ownership-guarded | `MarkSentAsync` takes only a message id, so mark-sent matches on the id — plus, where fencing is active, the leadership high-water. It carries **no** reservation-ownership term. | This is the contract, not an omission to read around. |
| **R3** | Monotonic attempts, and **termination** | The recorded attempt count never decreases (`GREATEST(attempts, n)`), **and it advances whenever a failure is recorded**, so a repeatedly failing message reaches the dead-letter ceiling in bounded attempts rather than retrying forever. | Holds **on the claim path**. Monotonicity alone is not the point of R3: a store that never decreases the count but also never advances it satisfies the letter and loses the reason, so termination is stated here explicitly rather than left implied by the ceiling. Termination holds on every provider and on both paths: the scheduled read projects the stored attempt count (`attempts AS Attempts` → `RetryCount`), so a scheduled message resumes its attempt history rather than restarting at zero. |
| **F1** | Leadership fencing *(optional)* | On stores implementing `IFencedOutboxStore`, a superseded leader presenting a stale token is refused on **claim and mark-sent** on every store that participates in fencing. **Which further members are fenced is per store, and the honest form is a matrix rather than a sentence — see the fenced-member matrix below.** Four stores additionally fence the **completion path** — **InMemory, Oracle, PostgreSQL and SQL Server**: they implement `IFencedClaimScopedOutboxStore` (mark-failed, carrying both the leadership token and the claim identity) and `IFencedDeadLetterableOutboxStore` (dead-lettering, carrying the leadership token), both returning an outcome rather than throwing. InMemory is in that set because it is the development and testing store and is held to the same contract; the three a production deployment can choose from are Oracle, PostgreSQL and SQL Server. **MongoDB and Redis implement neither, and that is now a startup refusal rather than a degraded configuration** — see the note on composition below. On Postgres and Oracle mark-sent is realised as a row DELETE, so on those stores the fenced mark-sent IS a fenced delete statement — that is the storage form of mark-sent, not a third fenced operation a consumer can invoke. Refusal on the failure members is **per store and per member**, and it is no longer uniform. Where a store implements the fenced form of the claim-scoped completion, its mark-failed statement carries both the leadership token and the claim identity and is refused on either — **one shipped store does.** Its ordinary mark-failed and its backoff statement carry a claim identity but **no leadership token**, so they refuse a foreign claim and not a superseded tenure. **Dead-lettering now depends on which of two statements the caller reaches, on the one store whose statements have been read whole.** PostgreSQL ships both: a fenced form that advances the durable high-water and conditions **both** the copy into the dead-letter table **and** the delete from the outbox on the presented token still matching it — so a superseded tenure moves nothing and deletes nothing — and the original form, which matches the message id alone and refuses no one. **The fenced form carries the leadership token only**: it separates tenures and does not separate two claim cycles of one tenure. **The other stores' dead-letter paths have not been characterised and no claim is made about them here** — do not read that silence as an assurance. **Note what the unfenced statement does and does not do:** the row's payload is copied to the dead-letter table before the outbox row is deleted, so a message terminated by a superseded tenure is recoverable from that table — the defect is a wrong terminal decision, not a lost message. Both shipped completion paths treat a mark-sent refusal as an abort of the cycle rather than routing it into one of those members. | **Holds** for every message that was claimed through the fenced claim on InMemory, Oracle and Postgres — this is every message either shipped drain path ever presents to mark-sent. MongoDB and Redis are outside this property entirely: they do not fence (see the composition note). Per this document's own standard, a guarantee whose cited arm does not establish it is documented UNVERIFIED rather than asserted. **Holds on SQL Server.** The guard advances the durable high-water under a range lock and conditions the write on that advance accepting the token, in one transaction. Enforced by `SqlServerOutboxFenceClaimsUnderSnapshotShould`, which runs against a dedicated database with snapshot isolation enabled and fails against the pre-fix guard. A concurrent conformance arm covers the PER-DOCUMENT guard only: it places the row in the state a fresher tenure's own claim would have left it in and deliberately does NOT advance the scope-wide high-water, so it proves the per-document term is honoured and is **not** evidence about the scope-wide dimension. **The scope-wide case is now covered by its own arm**, `Fencing_SupersededAfterItsOwnClaim_ShouldRefuseTheMarkSent`: a tenure claims a message while it is genuinely still the leader, a fresher tenure then advances the high-water by completing an unrelated message through the ordinary fenced path, and the first tenure's mark-sent must be refused. It takes no per-store hook, so unlike the per-document arm it applies to every store that participates in fencing rather than only those that can place per-document state. The distinction it draws is the one that matters here: a store treating a completed claim as standing authority to finish the message passes the per-document arm and fails this one, because the fence must be re-judged against the durable high-water at mutation time rather than at claim time. **Coverage splits by mechanism, and the bound-vs-run caveat applies to one half of it.** The in-memory store is covered **structurally**: `FenceCheckAndGuardedMutationShareOneLockTests` asserts, over the store's syntax, that every touch of the fencing high-water sits inside one lock region and that the mutations the fence authorises sit inside that same region — so a check separated from the write it guards is a compile-time-visible defect rather than a race a test must win. That guard needs no backing service and runs unconditionally. It is used because the window it protects is in-process: a behavioural arm for it passes on both sides of the defect and therefore locks nothing. Oracle, Postgres and SQL Server are covered **behaviourally**, by the fencing arms their conformance classes bind — and those classes are integration-gated, executing only where the real backing service is available. **For those five, read "an arm covers this" as bound, not as run:** a claim whose only enforcement is an arm that does not execute in a given suite is, for that suite, unenforced, so the arms named here establish enforcement where the provider's service is present and nowhere else. A claim whose only enforcement is an arm that does not run in a given suite is, for that suite, unenforced — so the arms named here establish enforcement **where the provider's service is present**, and nowhere else. A suite that declares it participates in fencing and whose store then stops presenting `IFencedOutboxStore` now FAILS rather than skipping, so a silently dropped capability is a red. A suite that declares no participation still skips, which is the honest outcome for a store that never claimed the capability -- the distinction is the declaration, not the skip. **COMPOSITION NOTE.** When a leader election is registered, the host refuses to start unless the outbox store implements `IFencedOutboxStore` **and both completion-path capabilities** (`IFencedClaimScopedOutboxStore`, `IFencedDeadLetterableOutboxStore`). **MongoDB and Redis implement none of the three, so neither can be composed under a leader election: the host throws at startup unless it opts into `AsSingleWriter()` and takes responsibility for there being exactly one active writer.** Both can express the atomic comparison a fence needs, but neither can keep the high-water durable: a value an asynchronous replica can roll backwards across failover or promotion is not a fence, so they do not offer the fenced contract at all rather than offering a weaker one. The refusal turns on the conjunction: all three are required, and implementing the first alone does not satisfy it. Everything said above about their fencing mechanisms therefore describes how they behave **where fencing is inactive**, which is the only configuration in which they can run; it is not a description of a degraded-but-live fenced deployment, and must not be read as one. A consumer wanting a fenced outbox today uses Oracle, PostgreSQL or SQL Server. **Whether that refusal is the right trade — fail-closed at startup versus a documented partial guarantee — is a product decision recorded elsewhere, not a property of this store.** |

**L1 — a message is never simultaneously DELIVERED and ACTIONABLE in the dead-letter queue. UNVERIFIED.**

The external dead-letter entry is written **before** the fenced terminal mark, deliberately. The ordering
was weighed and this is the safer half of it:

```
mark-then-enqueue, crash between   row TERMINAL, no entry       -> the message is present NOWHERE. Silent loss.
enqueue-then-mark, crash between   entry present, row claimable -> a duplicate on redrive. Visible over-count.
```

**An over-count a human can see beats a loss nobody can**, so the failure mode this ordering selects is
the one that has an observer. When the mark is instead **refused synchronously**, the entry is withdrawn,
because a refusal is precisely the case where withdrawal is sound: we learned in-band that our mark did
not take, so nothing else is relying on the entry.

**SCOPE — L1 is claimed ONLY for the synchronous-refusal path.** A crash between the enqueue and the mark
leaves it violated **by design**, per the trade above.

**And on that path it holds only under four conditions, all of which a consumer can observe or cause:**

1. **The composed dead-letter queue implements the admin surface** that exposes entry withdrawal. A
   consumer-supplied queue without it makes withdrawal unavailable; the framework logs an error and
   returns, and L1 does not hold for that composition. **This is the condition a consumer is most likely
   to cause, because the queue is theirs to supply.**
2. **The withdrawal succeeds.** A negative result or an exception leaves the entry in place; it is logged,
   not retried, and not thrown.
3. **The withdrawal completes within its own deadline.** The compensation is **not** cancellable by the
   drain: it does not take the drain's cancellation token but opens its own
   `CancellationTokenSource(DeadLetterCompensationTimeout)`, so a host shutting down does not prevent it
   attempting. The bound is deliberate — this runs on the way out, and an unbounded call to a remote
   queue would hold shutdown open indefinitely. What remains is therefore an ordinary timeout rather
   than a certainty: if the withdrawal does not finish inside that deadline the entry stays, and the
   cancellation observed on that path is the compensation's own deadline expiring, not the host stopping.
4. **The enqueue does not throw after writing the entry.** That call is not inside a try, so a throw after
   the external write unwinds the whole operation with no mark, no compensation, and the entry's handle
   never captured — **withdrawal is then impossible for want of a reference.** This is distinguishable
   from the crash case above: in a crash nobody is left who knows the entry should not exist; here
   somebody is, and cannot act.

**Why UNVERIFIED rather than asserted:** no arm exercises this. Nothing refuses a fenced mark and then
asserts the entry is gone, so the compensation has never been observed to fire. The conditions above come
from an independent reading of the committed code, not from a run. **Per this document's own standard, a
guarantee whose only support is a reading is documented and not claimed.**

**Consumer obligation:** handlers must be idempotent — the same at-least-once contract as the rest of this
document. Redrive is not an exception to it, and under condition 1 in particular — the one a consumer is
most likely to cause, since the queue is theirs to supply — an operator may find an entry for a message a
live successor is also about to deliver.

**On R2.** An earlier revision stated R2 as *"only the dispatcher that holds a message's current
reservation may mark **or** unreserve it."* The **mark** half was never implementable as written:
`IOutboxStore.MarkSentAsync(string messageId, CancellationToken)` accepts no owner token, so there is no
value a store could compare, and every provider marks sent by id alone. The property that actually exists
is the release-side guard, so it is stated on its own line and the mark-sent path is stated as what it is.

### Ordering

Ordering within a claim is **`PartitionKey`, then `SequenceNumber`, then creation time** — not oldest-first
by creation time, which is what this document previously said. Creation time is the *last* tiebreak, and all
three relational claims now apply it: SQL Server, PostgreSQL and Oracle each order by partition key, then
sequence number, then creation time. There is **no** cross-drain total order. `PartitionKey` and
`SequenceNumber` are the real sort keys — set them deliberately rather than treating them as a hint for
keeping related messages together.

**Enforced on all three relational providers, each by its own never-skipped lock against a real
container.** Each stages a partition's messages *out* of sequence order and requires the claim to hand
them back ascending:

| provider | lock | the ordering arm |
|---|---|---|
| SQL Server | `OutboxOrderingPersistenceIntegrationShould` | `ClaimSamePartitionMessagesInAscendingSequenceOrder`, plus `RestorePartitionOrderingOnReclaimAfterFailure` for the order surviving a reclaim after failure |
| PostgreSQL | `PostgresOutboxKeystoneRoundTripShould` | `ClaimSamePartitionMessages_InAscendingSequenceNumberOrder_AcrossSuccessiveSingleClaims` |
| Oracle | `OracleOutboxKeystoneRoundTripShould` | the same arm name |

Each is paired with a round-trip arm — `PersistAndRoundTripTheOrderingColumns` on SQL Server,
`PreserveTheCallersCreatedAt_AndCanonicalFields_OnStageThenReload` on the other two — without which the
ordering assertions would be comparing values the store never read back, and would pass against a store
that returned the columns as nulls.

## How it is achieved (the seam)

1. **Stage** — `StageMessageAsync` persists the message durably in the same transaction as the business
   write (the outbox pattern), so a message is never lost between commit and dispatch.
2. **Claim (S1)** — `GetUnsentMessagesAsync` is an **atomic claim, not a plain read**. Each provider uses
   its native atomic primitive so concurrent claimers get disjoint sets:
   - **SQL Server** — `UPDATE … OUTPUT` with `READPAST, UPDLOCK, ROWLOCK`
   - **Postgres** — `FOR UPDATE SKIP LOCKED` inside a claim CTE, one statement
   - **Oracle** — `FOR UPDATE SKIP LOCKED` on a cursor inside a PL/SQL block. Oracle forbids a row cap in
     the same statement as `SKIP LOCKED`, so the cap sits on the cursor fetch and the claimed rows are read
     back by a second statement keyed on the claim token. Disjointness is decided by the block; the
     read-back returns only rows this dispatcher already owns. Same outcome as Postgres, different mechanism
   - **Marten** — a **separate claims table this store owns**, claimed by one
     `INSERT … ON CONFLICT (message_id) DO UPDATE … WHERE … RETURNING`; PostgreSQL takes a row lock per
     key, so of two dispatchers presenting the same message exactly one gets it back. The claim is
     deliberately *not* in the Marten document: a document's fields live in a `jsonb` column whose property
     names come from the serializer the **consumer** configured, which this store does not own and cannot
     impose. `FOR UPDATE SKIP LOCKED` does not appear in this provider — an earlier revision said it did.
   - **MongoDB** — per-document `FindOneAndUpdate`
   - **Redis** — a Lua script (atomic read-decide-write)
   - **ElasticSearch** — `if_seq_no` / `if_primary_term` CAS stamping a per-message lease
   - **InMemory** — in-process lock + leases (development / testing)
3. **Fail / retry (R1·R2·R3)** — `MarkFailedAsync` performs **one atomic write** that frees the lease, sets
   the next-attempt floor to `now + F`, and sets `attempts = GREATEST(attempts, n)`. Splitting these steps
   would let a crash leave a message lease-free with no floor — a retry hot-loop — so the single-write
   atomicity is part of the contract. The release carries the R2 ownership guard.

   The split breaks R3 as readily as R1, in the mirror direction, and both halves matter. If the floor lands
   and the **attempt count** is lost, the message returns to the pool on schedule, fails again, and records
   the same count again: it never approaches the ceiling and is retried forever. Marten reached this contract
   through two writes on two connections — the claim table, then the document holding the count — and now
   performs them in one transaction, enlisting the document session on the claim connection so both commit
   together or neither does. A crash now leaves the claim held, and the message is retried when the claim
   ages out with its count intact.
4. **Send + mark-sent** — after the transport acknowledges, `MarkSentAsync` removes the message
   (delete-on-sent stores) or flags it (tracking stores). `IOutboxStoreCapabilities.SupportsSentTracking`
   distinguishes the two; delete-on-sent stores report `false`. This statement matches on the message id
   and, where fencing is active, the leadership high-water — see R2′.
5. **Leadership fencing (F1, optional)** — the property is: **no claim or mark-sent is admitted under a
   token below the stored high-water, and the high-water only ever advances.** A stale token yields zero
   claimable rows — it must not throw, or a superseded leader crash-loops its drain — while a stale
   mark-sent fails closed with `StaleOutboxFencingTokenException`.

   **What the fence does NOT cover, stated so it is not inferred closed.** The fence has two members.

   **The multi-transport completion path is unfenced in full, and no member of it accepts a token.**
   A multi-transport message is never presented to mark-sent at all: the publisher completes it through
   `MarkTransportSentAsync`, `MarkTransportFailedAsync`, `MarkTransportSkippedAsync` and
   `UpdateAggregateStatusAsync`, and no fenced overload of any of them exists. A superseded tenure that
   resumes after its lease has aged out can therefore still write the delivery outcome of a message the
   live leader has already completed, and **no refusal is possible because no fence is ever presented.**
   `IMultiTransportOutboxStore` is implemented by SQL Server only, so this is reachable rather than
   hypothetical. What stands between it and a re-delivered message today is the terminal-status term on
   the failure statements, which is a second guard and is marked UNVERIFIED on the status-recompute path.
   **Read any statement that F1 covers "every message either shipped drain path presents to mark-sent"
   as excluding these messages: a guarantee scoped by the calls it happens to receive says nothing about
   the calls it does not.**

   **On SQL Server a refusal is distinguished from a not-found by a value read in an earlier round
   trip, so the two are not always distinguishable.** The store enforces the fence and then marks in a
   second statement, classifying a zero-row result against the high-water it captured before the
   mutation ran. When the presented token equals that captured value — the steady state, since a leader
   advances the high-water to its own token on its own claim — a genuine refusal is reported as a
   generic not-found rather than as a fencing refusal, and a caller keying on the fencing exception will
   not see it. **The mutation still refuses correctly; only its classification is lossy.** Postgres and
   Oracle return the high-water from the same folded statement and disambiguate three ways, which is the
   shape this path should adopt. UNVERIFIED: no arm RED-detects the misclassification.

   **This paragraph is now PER STORE. On InMemory, Oracle, PostgreSQL and SQL Server the two completion
   members below DO take a token** — `IFencedClaimScopedOutboxStore.MarkFailedAsync` carries the leadership token
   and the claim identity, `IFencedDeadLetterableOutboxStore.MarkDeadLetteredAsync` carries the leadership
   token, and both return an outcome instead of throwing. **Backoff scheduling and the release path still
   take no token on every store.** On a store implementing neither completion interface the whole
   paragraph below applies unchanged — but note that such a store can no longer be composed under a leader
   election at all (see the composition note at the end of this row), so the configuration it describes is
   reachable only where fencing is inactive.

   Where the completion members take no token, a
   superseded leader is refused on the success path and admitted on every failure path if a caller routes
   a refusal into one of them. **A caller MUST treat `OutboxFenceRefusedException` — the abstract base of both `StaleOutboxFencingTokenException` and `OutboxFencingTokenUnavailableException`, so catching the base covers a refusal this document does not enumerate — as an abort of the
   whole drain cycle with no further store write** — it means "this message is no longer mine", never
   "this message is bad" — and both shipped drain paths (`OutboxProcessor` and the
   `OutboxBackgroundService` → `MessageBusOutboxPublisher` path) recognise such a refusal specifically and
   abort that message with no further write, rather than letting it fall into the generic failure handling
   that would retry, back off, or dead-letter it on the superseded tenure's behalf.
   **PARTIAL, and stated so it is not inferred complete.** That recognition is placed at the points a
   refusal is observed, not at every point one can arise: the failure members (`MarkFailedAsync`, backoff
   scheduling, dead-lettering, release) accept no token, so a refusal reaching one of them by a path that
   does not classify it is admitted. **The falsifiable form, now per store:** this is complete for a given store exactly when all four
   members take a fencing token and refuse a stale one, and incomplete while any of them does not.
   **Measured: two of the four — mark-failed and dead-lettering — now do so on InMemory, Oracle,
   PostgreSQL and SQL Server. Backoff scheduling and the release path do so nowhere.** So the row is
   complete on no store, and it is closer on those four than on the rest. The obligation above is
   therefore the **promise**, and the construction that discharges it in full is fenced overloads for those
   members. Until those exist, a caller — ours or a consumer's — that does not itself treat the refusal as
   an abort of the whole cycle can still produce an unfenced write.
   **UNVERIFIED:** no arm RED-detects a refusal routed into a failure member; an arm that does so is what
   would move this row off UNVERIFIED. A consumer implementing their
   own drain against this contract carries the same obligation, and carries it unaided.

   **The fence does not narrow the delivery guarantee.** A superseded leader still *delivers*; the fence
   refuses it only afterwards, and cannot reach back through the transport. The duplicate is unavoidable
   and handlers must be idempotent — this is the same at-least-once contract as the rest of this
   document, and it is stated here because this is where a reader looks for an exception to it.

   **Consumer obligation on the token source.** The token MUST come from a generator that is monotonic
   for the **lifetime of the outbox fence record**, not merely for the lifetime of a leadership session.
   The high-water is durable in the outbox store while the token is issued by a different component,
   usually backed by a different store. Re-creating that component's backing store — deleting and
   recreating a coordination lease, a namespace teardown, a move between namespaces — can restart the
   sequence below a high-water the outbox already recorded, after which every claim returns empty and the
   outbox never drains again. That failure is **silent** on the claim path itself: the claim path is specified not to throw, so no
   error surfaces. It is, however, **diagnosable and recoverable**. A store that implements
   `IFencedOutboxStoreDiagnostics` — discovered the way every optional outbox capability is,
   `store.GetService(typeof(IFencedOutboxStoreDiagnostics))` — exposes `GetFencingHighWaterAsync()` to read
   the recorded high-water directly, which is what distinguishes "no work is due" from "my token is below
   the mark", and `ResetFencingHighWaterAsync(newHighWater, force, ct)` to correct it. **Lowering the mark
   requires `force: true`, and it re-admits any leader whose token falls below the new value** — the split
   brain the fence exists to prevent. Reach for it only against a fence you have independently confirmed is
   poisoned, never as a routine unstick.
   **Enforced** on any store presenting the capability, by
   `FencingDiagnostics_GetHighWater_ShouldReportTheRecordedValue` (the read returns the recorded value) and
   `FencingDiagnostics_Reset_ShouldRefuseLoweringWithoutForceAndSucceedWithForce` (a lowering reset without
   `force` is refused and changes nothing). Both arms self-skip where the capability is absent, so read
   them as evidence about the stores that present it, not about the whole family.

   The *mechanism* is provider-dependent, and an earlier revision described only one shape and attributed
   it to a provider that has no fencing at all:
   - **Postgres, Oracle** — genuinely co-atomic. The fence advance and the claim are one statement: the
     fence row is locked and advanced in a CTE the claim CTE is gated on.
   - **SQL Server** — **the two members differ, and the difference is the guarantee.** The *advance* is
     sound: `EnforceOutboxFenceRequest` raises the high-water with a single `MERGE … WITH (UPDLOCK,
     HOLDLOCK)`, monotonically, so two concurrent leaders cannot both advance it — the second observes the
     first's write and is ordered after it. **Mark-sent now uses that same construction.** An earlier revision
     guarded it with a read-only scalar subquery in the `UPDATE`'s `WHERE`, which took no lock and advanced
     nothing — it *checked* the fence rather than *claiming* it, and under read-committed snapshot isolation
     (on by default on Azure SQL Database) that read is a versioned one taken at statement start, so an
     advance landing mid-statement was invisible to it by construction. The guard now performs the same
     monotonic `MERGE … WITH (UPDLOCK, HOLDLOCK)`, captures the resulting high-water through `OUTPUT
     INSERTED.HighWaterToken`, and conditions the mutation on that captured value — claiming the fence in
     the same transaction as the write it authorises.
     **UNVERIFIED, and the reason is structural rather than an absence of effort:** the store advances the
     fence in a separate call before the guarded write, and that advance takes the same row lock, so a test
     driven through the public mark-sent blocks there and passes identically whether the guard claims or
     reads. The pre-advance hides the property from a test at the same moment it makes the property hold.
     Read this as a correctness argument about locks, not as a measurement.
   - **InMemory** — a single in-process lock spans the fence check and the mutation it guards, on both
     claim and mark-sent, so there is no round trip for a fresher tenure to land inside.
     **Enforced structurally**, by `FenceCheckAndGuardedMutationShareOneLockTests`: it scans this store's
     syntax and fails when any touch of the fencing high-water, or any mutation the fence authorises, sits
     outside the one lock region. It runs unconditionally — no backing service — and carries four arms that
     each feed it a deliberately broken store and require an offence, so a split that silently stopped being
     detected is itself a red. A future change that splits this back into two regions therefore fails a test
     rather than passing unnoticed.
     **What it does NOT establish:** it is a statement about the SHAPE of the code, not a behavioural proof.
     No cross-provider arm yet RED-detects a superseded tenure's write actually landing. The structural guard
     is used here deliberately, because the window is in-process and a behavioural arm passes on both sides
     of the defect — see the coverage-by-mechanism note under F1.
   - **MongoDB and Redis do not fence.** Both could express the atomic comparison, but neither can keep the
     high-water durable across failover or replica promotion, and a high-water that can move backwards is not
     a fence. They therefore implement none of the fenced contracts, and under a leader election the host
     refuses to start with either unless it opts into `AsSingleWriter()` (`OutboxFencingStartupInvariant.cs`).

   **Where the high-water lives matters.** On the fenced relational stores it is a dedicated fence record,
   independent of the message rows, so it survives cleanup: a superseded leader's stale token is still
   rejected after cleanup has purged the sent, token-bearing rows. The per-message reservation (R2)
   independently prevents two dispatchers releasing each other's messages.

   **Fencing is opt-in, but its absence is refused rather than tolerated.** When a leader election is
   registered and the consumer has not set `OutboxDeliveryOptions.SingleActiveWriter`, **two** conditions are
   rejected at host startup by `OutboxFencingStartupInvariant.EnsureFencingCapableStore`
   (`src/Excalibur/Excalibur.Outbox/Outbox/OutboxFencingStartupInvariant.cs:51`), invoked from the outbox
   prerequisite validator so it covers **every** drain path: a store that does not implement
   `IFencedOutboxStore`, and a drain with no `ILeaderProcessingGate` to fence it. You cannot silently run an
   unfenced store, or an ungated drain, behind a leader election.

   **The enabling predicate is the election, not the gate — and that distinction is the guarantee.** An
   earlier revision keyed fencing on the presence of the gate. A guard whose enabling condition is supplied
   by the component it exists to require cannot detect the case it exists to detect: a host that registered
   an election through a path that never wired the gate resolved no gate, so the predicate read
   "single instance" and startup passed while every instance drained unfenced. The predicate is now
   `(election registered || gate present) && !SingleActiveWriter`. Keeping the gate as an independent signal
   makes the change monotonic — it can only turn a silent pass into a refusal, never a refusal into a pass.
   Registration is probed with `IServiceProviderIsService`, so a host is not made to construct its election
   merely to be validated.

   Conformance: `OutboxRefusesUnfencedLeaderElectionShould` binds both arms — the refusal, and the liveness
   arm proving a host with no election registered still starts **and drains**. That liveness arm is
   load-bearing: a guard asserted only on its safety half is satisfied by one that refuses every
   single-node host.

## The publisher drain: every loop claims

`OutboxProcessor` is claim-only: its single read path is the atomic claim. The layer above it —
`OutboxBackgroundService` driving an `IOutboxPublisher` — runs **three** loops per cycle, and **all three
route through that same atomic claim**:

| loop | option default | how it selects messages | claims? | honors the floor? |
|---|---|---|---|---|
| `PublishPendingMessagesAsync` | always on | the atomic claim (S1) | **yes** | yes |
| `PublishScheduledMessagesAsync` | **on** (`ProcessScheduledMessages = true`) | the atomic claim (S1) | **yes** | yes |
| `RetryFailedMessagesAsync` | **on** (`RetryFailedMessages = true`) | the atomic claim (S1) | **yes** | yes |

All three funnel into one claimed drain — `PublishPendingMessagesAsync`, `PublishScheduledMessagesAsync`
and `RetryFailedMessagesAsync` each delegate to `DrainClaimedBatchAsync`, whose first act is the same
claim the processor path uses.

**An earlier revision of this document said otherwise, and understated the guarantee.** It described the
scheduled and retry loops as ordinary selects that raced the lease, and the retry loop as ignoring the
`NextAttemptAt` floor. That was accurate for an earlier implementation and is no longer accurate: there is
no non-claiming dispatch path in this drain, the lease is not raced, and the floor is honored because it is
part of the claim predicate. A consumer who read the earlier text and worked around a gap that has since
closed can stop doing so — running a single active dispatcher, or disabling `ProcessScheduledMessages` and
`RetryFailedMessages`, is no longer required for this reason.

**One qualification, so this is not read as broader than it is.** `IOutboxPublisher` has **no registration
anywhere in this framework** — the background service is registered by default but cannot resolve until a
consumer supplies a publisher. So this drain runs only in a host that completes it. That the framework calls
this the default drain and never wires it is a separate defect, tracked separately.

**This drain now presents a fencing token, and an earlier revision of this document recorded that it did
not.** Where a leadership tenure and a fencing-capable store are both present, the claim and the mark-sent
carry the tenure's token, so this path gets the same F1 treatment as the processor path: a superseded leader
is refused by the store's durable high-water rather than admitted. The publisher discovers the capability
through the store's own `GetService` seam, so a decorated store is not mistaken for an unfenced one.

**The gate the background service already applied is not a substitute, and that is why the token was
needed.** The non-partitioned loop checks `ShouldProcess` before draining, which is check-then-act: a
dispatcher paused past the end of its tenure -- a long GC, a stalled host -- resumes, reads its own stale
leadership snapshot, and writes. Only the token is comparable against the stored high-water, so a drain that
cannot present one cannot be refused. Where a tenure is active but no token is available the drain now
**fails closed** rather than falling through to the unfenced members; where no election is configured, or
the consumer has declared `SingleActiveWriter`, the unfenced members remain the legitimate path.

Claim-disjointness (S1) was never affected by the earlier gap -- it rests on the per-message atomic claim,
not on leadership, so no message was delivered to two dispatchers from a live lease.

## Per-message backoff: `IBackoffSchedulableOutboxStore`

An optional capability implemented by **MongoDB, Oracle, Postgres, Redis, SQL Server**. Where a store
implements it, `OutboxProcessor` **prefers it** over the plain failure path, so on those five providers it —
not `MarkFailedAsync` — is the failure path that normally runs. It was absent from earlier revisions of this
document, which meant R1 was documented against a predicate the processor usually bypasses. (The
`OutboxBackgroundService` publisher path does not use the capability at all; it always calls
`MarkFailedAsync`.)

`MarkFailedWithBackoffAsync(messageId, errorMessage, retryCount, nextAttemptAt, ct)` takes an **absolute
instant computed by the caller** — `now + IBackoffCalculator.CalculateDelay(attempt)`.

**The caller's instant is composed with the floor, not substituted for it** — the store writes the **later**
of the two — and on every one of the five that maximum is taken over the two **durations** (the caller's
delay and F) and then anchored to the **server's** clock: `DATEADD(MILLISECOND, …, SYSUTCDATETIME())` on SQL
Server, `now() + GREATEST(make_interval(…), make_interval(…))` on Postgres, `SYSTIMESTAMP +
NUMTODSINTERVAL(GREATEST(…), 'SECOND')` on Oracle, `$$NOW` on MongoDB, `redis.call('TIME')` on Redis. Taking
the maximum over durations selects exactly what taking it over instants would, since
`gate - now == max(nextAttemptAt - now, F)`, but it leaves **no caller clock in the persisted value**.

That last property is what makes the gate sound, and both directions of skew matter. The floor term was
always server-anchored, so a skewed dispatcher could never *shorten* F. But while the caller's instant was
persisted verbatim, the claim predicate compared a **dispatcher-stamped** value against the **server's**
clock — one comparison across two machines that need not agree. A dispatcher running ahead of the database
therefore kept a message invisible for the whole skew *after its backoff had genuinely elapsed*. Deferring a
due message is not the harmless direction: it is a delivery stall bounded by nothing but the size of the
skew, and a store that never hands a due message back satisfies every safety property while delivering
nothing. Converting the caller's instant to the duration it represents before it leaves the dispatcher
preserves the caller's intent exactly and puts one clock on both sides of the comparison.

Oracle was the last of the five to still compose two **instants**, and it no longer does. An earlier
revision of this document recorded that exception and told consumers running Oracle to keep their dispatcher
skew small; that advice is now obsolete and the exception is gone. The conversion happens on the dispatcher,
where the caller's instant and the caller's own clock reading are subtracted — both readings of one clock, so
whatever that clock's offset from the database is, it cancels — and the surviving duration is re-anchored on
`SYSTIMESTAMP`. Evidence: `OracleOutboxBackoffFloorClampShould`, whose two skew arms drive a dispatcher an
hour ahead of the database and assert both that a message whose backoff has elapsed comes back on the
server's floor and that a longer computed schedule survives the conversion as a delay rather than being
inflated by the skew.

A caller-computed delay longer than F is honoured on every provider, so the exponential curve is preserved
above the floor; only delays shorter than F are raised. Because the composition is a maximum, relaxing the
floor is not something ordinary use can express: it takes inverting that one operator, and each store
carries that note in source.

The default calculator is exponential with a 1-second base and jitter, so attempt 1 computes roughly
0.75–1.25 s against a 30-second floor — about thirty times sooner than F. `IBackoffCalculator` is an
injectable single-method contract returning an arbitrary `TimeSpan`, so a consumer-supplied calculator can
return anything at all. That is precisely what the composition exists to absorb: whatever the caller
computes, the persisted value is the later of it and F.

Read R1 accordingly: **"not re-claimable within F" describes both failure paths on every store that
implements the computed-backoff seam.** The caller's curve is preserved above the floor and raised below it.

## Fault model

The guarantees above hold under process crash, process pause, transport failure, and duplicate delivery.
They are stated against the assumptions below; where an assumption is not currently met, that is said here
rather than left for a reader to discover.

> **D2 — Single-clock decisions.** No claim, lease, or floor decision compares timestamps taken on two
> different machines.

**D2 holds on the polling outbox family. It does not hold on the change-feed family's claim capability.**

**Where it now holds.** Every `IOutboxStore` provider decides claim eligibility on a clock all its
dispatchers share — the store's own, so two dispatchers that disagree about the time cannot disagree about
whether a lease is live. **One exception remains, on the scheduled-due gate only:** Marten selects due
candidates using the dispatcher's clock and its claim does not re-check the schedule, so a dispatcher
running ahead of the database can deliver a scheduled message early. Duplicate delivery is unaffected —
disjointness is decided by the claim, not by a clock:

| provider | the clock the claim reads | how |
|---|---|---|
| SQL Server, Postgres, Oracle | the database server's | the claim predicate and the lease stamp are terms of one statement |
| Marten | the database server's | `clock_timestamp()` supplies the cutoff, the stamp, and the failure floor — `MartenOutboxClaims.cs`. `clock_timestamp()` rather than `now()` because the floor is written inside a caller-managed transaction, where `now()` would be that transaction's start time |
| MongoDB | the MongoDB server's | the predicate is an `$expr` over `$$NOW`; the stamp is an aggregation-pipeline `$set` of `$$NOW` — `MongoDbOutboxStore.cs`. This requires the stored instant to BE a BSON date, which is why every instant on `MongoDbOutboxDocument` carries `[BsonRepresentation(BsonType.DateTime)]`; the driver's default sub-document form cannot be compared against `$$NOW` at all |
| Redis | the Redis server's | `redis.call('TIME')` inside the claim script supplies the reclaim cutoff, the new lease expiry, and the scheduled-due gate. The caller passes no instant, so a caller's clock has no path to the decision |
| ElasticSearch | the Elasticsearch node's | two painless scripts, each reading `System.currentTimeMillis()` on the node — `ElasticsearchOutboxStore.cs`. The claim compares the stored lease against that reading and stamps the new lease from it, declining a live lease with `ctx.op = 'noop'`; the non-success transition stamps `nextAttemptAt` as that reading plus the floor **duration**, so the retry floor is written on the same clock the claim predicate reads it back on. All four claim clauses — lease, floor, schedule and status — are resolved by the node; `GetUnsentMessagesAsync` reads no dispatcher clock at all. The `if_seq_no`/`if_primary_term` compare-and-swap is retained alongside both: the CAS refuses a document that changed since the caller read it, while the script is what makes the predicate true rather than merely atomic |
| InMemory | the hosting process's | leases live in an in-process dictionary, so the process that writes a lease is the process that judges it. One clock by construction rather than by design — and a second process shares no state with the first, which is why this provider is for development and testing only |

Conformance: `MongoDbOutboxClaimClockSkewShould`, `MartenOutboxClaimClockSkewShould`,
`ElasticsearchOutboxClaimClockSkewShould`, `ElasticsearchOutboxRetryFloorClockSkewShould`,
`RedisOutboxClaimClockSkewShould`. Each drives a second
dispatcher whose `TimeProvider` runs a full lease plus five minutes ahead and asserts it is handed nothing
the first holds, and each pairs that with a liveness arm asserting an elapsed lease IS reclaimed — without
which the safety assertion would be satisfied by a store that claims nothing at all, forever.

**Where it does not hold.** One population remains — the change-feed family. The retry floor on the
computed-backoff path used to be a second one, on Oracle, Postgres and SQL Server; all three now compose
durations and anchor them on the database clock, so no `NextAttemptAt` computed by a dispatcher reaches a
persisted gate on any polling provider.

| where | what is compared across machines | effect of skew |
|---|---|---|
| **Cosmos DB, DynamoDB, Firestore** — `ICloudNativeOutboxStoreClaim.ClaimPendingAsync` | the *claiming process's* clock against a lease instant stamped by *whichever process held the message before* | see the bound stated below. The disjointness of the claim is unaffected — that is decided by a conditional write, not by a clock — but the **lease** stops excluding anybody once skew exceeds it |

**The skew bound on the three change-feed providers, stated so it can be falsified.** Let `L` be the
configured lease timeout and `d` the largest clock difference between any two claimant processes.

> The lease guarantee — *a claimed message is not claimable again until its stamp plus `L` has elapsed* —
> holds **if and only if `d < L`**. When `d ≥ L`, a claimant whose clock runs ahead computes a cutoff past
> the live lease and re-claims a message the holder is still publishing, **with no elapsed time and no
> fault**: no crash, no pause, no contention. Both processes then publish it. The duplicate window in that
> state is not bounded by `L`, or by the retry floor, or by anything else in the store — every poll can
> re-claim every in-flight message.

The atomic claim does not close this, and it is worth being precise about why: atomicity arbitrates two
**simultaneous** claimants. Under skew the two are not simultaneous — the second claimant is the only
writer at that instant, so its conditional write succeeds legitimately, on a predicate that was already
false. Nothing in a compare-and-swap examines the predicate's truth.

> **Consumer obligation, for these three providers only:** set `L` greater than **the maximum delivery
> duration plus the maximum clock skew between any two claimant processes**, and run NTP (or equivalent) on
> every host that calls `ClaimPendingAsync`. If you cannot bound skew, you cannot bound duplicates on this
> path — use the change-feed trigger instead, which decides concurrency by partition lease in the trigger
> infrastructure and consults no claimant clock at all.

This gap is **UNVERIFIED by test**: these three expose no server-clock primitive to a conditional write, so
there is nothing to assert against. It is recorded here as a known limitation rather than asserted as safe.

The relational providers are deliberately clean on the **plain** failure path, and the code says so: the
floor is written as `NOW() + F` on the **server** clock and the claim compares against that same server
clock — one clock on both sides. What reintroduces a second clock is narrower than it once was: the
computed-backoff path on the three relational stores that accept an **absolute instant** from the caller,
and the claim capability on the three change-feed providers.

Consequently the consumer obligation "set F greater than maximum delivery duration" is **sufficient for the
retry floor on every `IOutboxStore` provider**, because the floor and the predicate that reads it are now
anchored to the same clock everywhere in that family. Where the caller supplies a computed backoff — the
five stores implementing `IBackoffSchedulableOutboxStore`, which does not include Marten — it travels as a
**duration** on MongoDB and Redis, a duration carrying no clock, so re-anchoring it at the store preserves
the caller's curve exactly while leaving one clock in the comparison; and as an absolute instant on Oracle,
Postgres and SQL Server, where the server-anchored maximum bounds the error to lengthening only. Marten
exposes no computed-backoff seam at all: its only floor is the plain failure path's, written from
`clock_timestamp()`.

Skew remains part of the duplicate window on the **three change-feed providers' claim capability**, under
the bound stated above. It is no longer part of it on the polling family.

### A row that cannot be decoded is a fault of the row, not of the transport

The staged body is the serialized **message**, written by the outbox writer as the message's own runtime
type. The identifier, the type name and the metadata travel beside it on the stored row rather than inside
the body, and the drain reads them from there. A drain that re-read the body expecting to find those fields
nested inside it would fail on every message the writer produces, so the two halves are stated together
here: **what is written is the message, and what is read is the message.**

When a row's body cannot be decoded — an empty body, a type name no registry resolves, a payload that is
not the shape its type declares, or metadata that will not bind — the outcome is fixed and is not a retry:

> **The row is dead-lettered on first encounter, with a deserialization reason, and the attempt is not
> charged to the transport's circuit breaker.**

Both halves are deliberate and each is falsifiable on its own.

**It does not retry**, because decoding is a pure function of bytes already at rest. A body this process
cannot decode will not decode on the next poll either, so consuming the attempt budget only delays the
same terminal outcome while occupying a claim slot each time round.

**It does not reach the breaker**, because the circuit breaker is a statement about *transport health*, and
a row that failed to decode never reached a transport. Charging it there would let one corrupt row count
against the circuit and, at the configured threshold, open it — stalling delivery of every healthy message
sharing that transport. The decode therefore happens **before** the breaker is entered rather than inside
it, so a poison row has no path to the failure count at all; this is a property of where the call sits, not
of a filter that could be widened later.

Conformance: `OutboxStagedPayloadDrainShould`. Its three arms stage through the writer's own API and drain
through the hosted processor — a message staged as an event is dispatched as that event; a corrupt row is
dead-lettered without the breaker being entered or charged; and a healthy message staged behind a corrupt
one is still delivered, which is the liveness arm without which the second would be satisfied by a drain
that delivered nothing at all.

**Consumer obligation.** Register an `IDeadLetterQueue`. Without one a poison row is discarded when it is
dead-lettered, and its body — the only remaining copy of that message — is lost with it.

## The change-feed family (`ICloudNativeOutboxStore`)

**Cosmos DB, DynamoDB, and Firestore do not implement `IOutboxStore`.** They implement
`ICloudNativeOutboxStore`, which does not derive from it, and their DI extensions register them under that
contract only. **None of S1, R1, R2, or R3 applies to them.** An earlier revision listed all three in the
claim-primitive table, extended at-least-once to them, and offered Cosmos as the worked example of
co-atomic fencing. Each named mechanism was real somewhere and absent from the path described:

| provider | previously claimed | what is actually there |
|---|---|---|
| Cosmos DB | "single-doc `IfMatch` ETag compare-and-swap" as the **claim** primitive | `IfMatchEtag` is real, but only in the mark-published and increment-retry paths. It makes **marking** at-most-once and says nothing about claiming. `GetPendingAsync` is a plain query: `SELECT * FROM c WHERE … isPublished = false ORDER BY c.createdAt`. |
| Cosmos DB | "advances the high-water inside the same `IfMatch` replace that claims the row" | the package contains **no fencing member of any kind** — no fence, no high-water. A row-claiming `IfMatch` replace does now exist, on the opt-in claim described below, but it advances no high-water and there is none for it to be co-atomic with. |
| DynamoDB | "conditional `UpdateItem`" | `ConditionExpression` appeared nowhere in the package at the time of that correction; the only matches were `KeyConditionExpression`, a *query* key condition. `GetPendingAsync` is still a plain query and the publish path's `UpdateItem` calls are still unconditional. A conditional `UpdateItem` now exists, but only on the opt-in claim described below. |
| Firestore | "`runTransaction`" | the outbox did not call it at the time of that correction, though five other Firestore stores here did. `GetPendingAsync` is still a plain snapshot query. The outbox now calls `RunTransaction`, but only on the opt-in claim described below. |

Read that column as a record of the **publish path**, which is what the corrected claims were about, and of
`GetPendingAsync`, which is unchanged. Each provider now also carries an atomic claim, on a separate opt-in
member that a consumer must call deliberately — see *The claim capability* below.

What these three actually provide is the change-feed pattern: write the message in a transactional batch,
let the provider's change feed trigger a serverless function, publish, then mark published. Concurrency is
managed by the trigger infrastructure — one change-feed lease per partition — not by a claim in the store.
**Two processes polling `GetPendingAsync` on the same partition receive the same messages.** Do not put
these behind a self-managed multi-process poller built on that read; use the change-feed trigger they are
built for — or, where a poller is genuinely required, the opt-in claim described below, which is the only
supported way to poll these stores from more than one process.

**Ordering: FIFO, per provider, and one named latency caveat.** `GetPendingAsync` and `ClaimPendingAsync`
both return pending messages in the order they were staged. Cosmos DB and Firestore order via a
strongly-consistent native query (`ORDER BY c.createdAt` / `OrderBy("createdAt")`) with no further caveat.
DynamoDB's base-table query is physically ordered by a per-message key, not by creation time, so both reads
instead query a Global Secondary Index keyed on the partition and creation time. A GSI is
eventually-consistent with the base table: for a brief interval after `AddAsync` returns, a just-staged
message can be absent from a `GetPendingAsync`/`ClaimPendingAsync` read on that provider — a **latency**
property (the row is durably present on DynamoDB's strongly-consistent base table throughout; the next read
sees it) and not a loss property. AWS documents GSI propagation as typically completing within a fraction
of a second under normal conditions, with no formal upper bound — a recovery sweep run on an interval, not
a single immediate read, is what the guarantee is designed around. Messages a read does return come back
in creation order on these three providers: Cosmos DB and Firestore order the query by creation time, and
DynamoDB reads through its partition-and-creation-time index. **UNVERIFIED:** no conformance arm locks
this ordering for the cloud-native providers yet. The relational providers' ordering is locked, as
described under Ordering.

### The claim capability — `ICloudNativeOutboxStoreClaim`

The trigger remains the recommended shape, and `GetPendingAsync` is untouched: it is still a plain read that
claims nothing. A consumer who nonetheless needs a self-managed poller — because the trigger is unavailable
in their host, or because they are sweeping for messages the feed has already passed — now has a supported
way to do it, through a capability they must deliberately opt into.

**What it guarantees.** `ClaimPendingAsync(partitionKey, batchSize, claimantId, ct)` returns a set
**disjoint** from every set returned by a concurrent call against the same partition. Each returned message
carries a lease naming its claimant and the instant it was stamped, and is not claimable again until that
instant plus the store's configured lease timeout has elapsed — after which any claimant may take it, which
is what stops a claimant that dies mid-delivery from stranding its messages. This is **S1 and a lease, and
nothing more**: it is not at-least-once delivery, which remains the consumer's to establish, and it is not
fencing.

**How, per provider.** Each uses its own atomic primitive. In every case the candidate query decides nothing
— two claimants querying at the same instant see the same rows — and the conditional write decides
everything:

| provider | the atomic step | what the loser observes |
|---|---|---|
| Cosmos DB | `ReplaceItem` under `IfMatchEtag` | `412 PreconditionFailed` |
| DynamoDB | `UpdateItem` under a `ConditionExpression` naming the lease | `ConditionalCheckFailedException` |
| Firestore | the read and the write inside one `RunTransaction` | the transaction aborts and re-runs |

Losing is the normal outcome under concurrency, not an error: the contested message is simply absent from
the result. An unconditional write in any of those three positions — an upsert, a bare update, a read
followed by a separate write — has no precondition to fail, so every claimant wins and every claimant
publishes.

**The claim also depends on the stored document reaching the wire under the names the predicate reads, and
that is part of the guarantee rather than an implementation detail.** The Cosmos predicate is
`NOT IS_DEFINED(c.leasedAt) OR IS_NULL(c.leasedAt) OR c.leasedAt < @leaseCutoff`. A serializer that emitted
the lease field under any other name would leave `c.leasedAt` undefined on every document, making the first
clause true for every row: every message would read as unclaimed regardless of who held the lease, so the
claim would be **inert while appearing to work**, and two instances would publish the same message. This
predicate fails **open**, so the correspondence cannot be left to configuration.

It is not. Every persisted property on the Cosmos document is mapped explicitly, for **both** System.Text.Json
and Newtonsoft, to the exact name the queries read. That matters because the builder lets a consumer supply
their own client, which bypasses the serializer options this package configures and falls back to the SDK
default — so the framework does not own the serializer on that path. Explicit dual mapping makes the naming
policy irrelevant instead of load-bearing. **Consumer obligation: a persisted property added to that document
must carry the same explicit mapping**; anything relying on a naming policy is correct only on the paths where
this package happens to build the client.

> **The two delivery models are mutually exclusive. This is the load-bearing sentence in this section.**
> Use the change-feed trigger **or** a claim-based self-managed poller — never both against the same
> container. The trigger path does not observe the claim's lease: a message a poller holds is still handed to
> the trigger's handler, and both publish it. Combining them reproduces precisely the duplicate delivery the
> claim exists to prevent, and is therefore worse than either alone. They are alternatives, not layers.
>
> **Enforced, not merely stated.** A host that registers both an `IChangeFeedSubscription<CloudOutboxMessage>`
> and the claim-based drain is refused at host start, with a message naming both registrations
> (`OutboxDeliveryExclusivity`, checked from `OutboxPrerequisiteValidator.Validate`). Locked by
> `OutboxRefusesTwoDeliveryMechanismsShould`, whose liveness arms prove each mechanism *alone* still starts —
> so the refusal means this pair specifically, and cannot be satisfied by refusing every host.
>
> **What the refusal cannot see**, stated because the gap is yours to close and not ours to detect: it reads
> the container. A subscription you construct and start yourself, without registering it, is outside what the
> host was told about. The rule above still governs that deployment; only the automatic refusal is out of
> reach of it.
>
> **Projecting the lease into the feed document does not substitute for choosing one.** It lets the handler
> read the lease, but a read is not a conditional write: if the handler reads an unleased message before the
> poller claims it, both still publish. The observation is stale by the time it is acted on, which is why the
> mechanisms are exclusive rather than cooperative.

**No fencing.** The claim provides S1 and a lease. It does **not** provide F1. There is no leadership
high-water on any of these three providers and no token to present, so a superseded leader is not refused —
the statement above that this family carries no fencing member of any kind still holds exactly. A deployment
that needs fencing must use one of the fenced providers.

**Consumer obligations.**

- **Set the lease timeout above the maximum time a publish can take.** A claimant still delivering when its
  lease expires can have the message taken from under it and delivered again. Handlers must be idempotent in
  any case; the timeout is what keeps that window closed to normal operation rather than opening it on every
  slow send.
- **Treat clock skew as part of the duplicate window.** Lease eligibility is decided against the
  *claimant's* clock, because none of the three primitives exposes a server-side clock to a conditional
  write. A claimant whose clock runs fast treats a live lease as expired and claims a message another
  claimant is still delivering — a duplicate outside the lease window, not inside it. This is the same
  client-clock exposure the fault model records for MongoDB, Redis, ElasticSearch and Marten. Run NTP on
  claimant hosts.
- **Handlers must still be idempotent.** Nothing here makes delivery exactly-once.

**Evidence.** One never-skipped real-infrastructure lock per provider —
`CosmosDbOutboxStoreClaimAtomicityShould`, `DynamoDbOutboxStoreClaimAtomicityShould`,
`FirestoreOutboxStoreClaimAtomicityShould` — each opening with a hard `DockerAvailable.ShouldBeTrue(...)` so
a missing container fails rather than passing vacuously. Each asserts both arms: **safety**, that two
concurrent claimants never receive the same message, and **liveness**, that every staged message is
nonetheless claimed by exactly one of them, that a live lease is refused, and that an expired lease is
reclaimed. The liveness arm is deliberate — disjointness alone is satisfied perfectly by a store that hands
out nothing, to anybody, forever.

**A known limit of that evidence, recorded rather than left to be discovered.** Only the concurrency arm is
non-vacuous against the atomic step. The live-lease arm passes even against a store whose conditional write
has been removed, because in the sequential case the candidate query already excludes a leased message. It is
the conditional write that provides the property *under concurrency*, and only the concurrent arm exercises
that. Read the concurrency arm as the one that binds the mechanism.

### Checkpoint durability (Cosmos DB change-feed subscription)

**Guarantee.** Change-feed checkpoint persistence is best-effort and fails open: a checkpoint-save
failure does not stop event delivery and does not change the feed's existing at-least-once guarantee —
handlers consuming this feed MUST be idempotent. The redelivery window on restart is bounded by the last
successfully persisted checkpoint, and can grow while checkpoint saves are failing. After a configurable
number of consecutive checkpoint-save failures (default 10), the subscription reports itself unhealthy
through its `IsCheckpointDegraded`/`CheckpointLag` surface while continuing to deliver events; consumers
running durable checkpointing SHOULD alert on that signal and treat it as "checkpoint store degraded,
redelivery window growing" rather than assume silence means health.

**How it is achieved.** `CosmosDbOutboxChangeFeedSubscription.ReadChangesAsync` yields every page's
events to the consumer *before* attempting to persist that page's checkpoint, and wraps the checkpoint
save in a try/catch: a failure is logged and tracked, never rethrown, so it cannot tear down the
subscription. A shared, pure decision component tracks the consecutive-failure count and reports the
degraded-escalation exactly once per run of failures, clearing on the next success.

**Consumer obligations.** Handlers consuming this feed must already be idempotent (a pre-existing
at-least-once obligation, unchanged by this). A consumer running durable checkpointing should monitor
`IsCheckpointDegraded` and treat a sustained `true` as an operational alert.

**Known gap.** Durable checkpointing is opt-in (the default checkpoint store is in-memory and does not
survive a restart at all); this guarantee applies only when a durable store is configured. There is
currently no automatic escalation beyond the health-signal surface — a persistently degraded subscription
keeps running and keeps delivering events indefinitely; an operator decides whether and when to intervene.

**Evidence.** `CosmosDbChangeFeedCheckpointFailOpenShould`, with both arms:
`KeepDeliveringEvents_LogTheFailure_AndDegradeAtTheBound_WhenCheckpointSavesAreForcedToFail` forces every
checkpoint save to fail and requires delivery to continue and the degraded signal to raise at the bound —
so a subscription that tore down on a checkpoint failure, or one that stayed silent while degraded, is
red. `PersistTheCheckpointAndResumeFromIt_OnTheHealthyPath` is the liveness arm: without it the first
would be satisfied by a subscription that never persisted a checkpoint at all, which is fail-open taken
to the point of uselessness.

## Tenant scoping — which statements carry a tenant term, and why most do not

Every relational outbox statement now declares its tenancy decision, and the decision is part of the type
rather than a property of the SQL you have to read to discover. A request either confines itself to the
caller's partition or states, at its declaration, why it spans every tenant. **Almost every statement is
in the second group, and the exception is named below rather than rounded away** — the consumer-facing
transport-delivery read is confined, and an earlier revision of this section said none was while the rest
of the section described that read as confined twice over.

A third state does exist, and this document previously denied it. The declaration is advisory: the
member carrying it is `protected` on the request base and absent from the request interface, so no
executor can read it, and a request that records no decision compiles and runs like any other. Read a
declaration as a note from its author about intent, never as evidence that the statement below it
carries a tenant term.

**Confined to the caller's partition.** One statement is: the consumer-facing read of a message's
transport deliveries. It binds a tenant the caller supplies explicitly as a server-evaluated predicate
(`src/Excalibur/Excalibur.Outbox.SqlServer/Requests/GetTenantScopedTransportDeliveriesRequest.cs:59`),
and it is the only statement in the family that does — SQL Server is the only provider implementing the
multi-transport surface this read belongs to, so the question does not arise on the others. A caller
scoped to one tenant that supplies another tenant's message identifier matches zero rows rather than
receiving that message's delivery records. Read the detail — including what that confinement is and is
not — under *Reading a message's transport deliveries* below. Every other statement is estate-wide.

This document previously listed the statistics read here, with the harm of an unscoped form given as
learning another tenant's message volumes. That entry was wrong, and it is corrected in place rather than
quietly dropped, because reading as a disclosure risk is exactly what kept the term alive through several
sweeps of the surrounding statements.

Statistics is an operator report, and it cannot be anything else. The store method that reaches it takes no
tenant argument, and the statistics type it returns carries no tenant field — so a confined result has no
way to say which partition it describes. Confinement here is not underspecified, it is unrepresentable. An
outbox store also reads no ambient tenant context, so the only way the statement could have obtained a
partition was to infer one from ambient state, which is the mechanism this store's own contract rules out.
The other relational providers have declared this read estate-wide all along; the SQL Server statement now
matches them, and all three carry the same declared reason.

**Deliberately estate-wide.** Outbox statistics is, for the reason above. The drain is cross-tenant by design — one dispatcher serves every tenant, and
each row carries its tenant so the handler re-establishes the owning partition before handling the message.
Scoping the drain to an ambient tenant would stall delivery for every other tenant. Retention purges and the
fence are estate-wide for the same class of reason: they act for the operator, or key on something that has
no tenant dimension at all. Each of these says so at its declaration, so the set can be enumerated by
searching for the declaration instead of by re-reading every statement.

**The post-claim statements are estate-wide too, and a tenant term could not confine them.** The drain hands
back a row it claimed across tenants; every statement that then acts on that row is addressed by an
identifier which already determines the tenant, so a tenant term could only subtract the row the caller must
reach. It could never redirect the statement to a different tenant's row. Two different reasons, worth
keeping distinct:

| statement | why a tenant term cannot confine it |
|---|---|
| mark a message sent / failed / dead-lettered; the aggregate-status update of the message row | addressed by the outbox `Id`, which is that table's primary key, so the statement already matches at most one row — the row the claim returned |
| mark a transport delivery sent / failed / skipped; the aggregate-status roll-up; the drain's estate-wide read of a message's transport deliveries | reached through `MessageId`, a foreign key to the globally-unique outbox `Id`, so every row that can match belongs to the one message the claim returned, and therefore to one tenant |

Adding a tenant term to these does not isolate anything; it only fails to match when the ambient tenant
differs from the row's, which leaves a delivered message marked unsent, its lease expiring, and the message
delivered again. Isolation on these tables is established where the row is **written**, by stamping the
tenant, and by the drain re-establishing the owning partition per message.

**Reading a message's transport deliveries is split into two operations, one confined and one not.** The
consumer-facing read takes the caller's tenant as an explicit constructor argument -- never inferred from
ambient state, since this store reads no ambient tenant -- and binds it as a SQL predicate evaluated by the
server: a caller supplying another tenant's `MessageId` matches zero rows. That confinement is one the
caller opts into by supplying its own tenant, not an authorization boundary this store enforces -- the
store has no ambient tenant to check the supplied value against, so it trusts it. A caller entitled to name
any tenant it likes can read any tenant's deliveries this way; establishing which tenant a caller is
actually entitled to name is the caller's own responsibility. The drain's estate-wide read is a
separate, explicitly named operation reserved for the delivery drain and other operator paths; it is
deliberately unconfined for the same key-adjacency reason as the row above it, and is not reachable from the
tenant-facing interface. Treat a message identifier handed to the unconfined operation as a capability and do
not expose it across a tenant boundary.

**Consumer obligation.** A host that resolves a tenant per operation gets confinement on every statement
above that is confined, including the transport-delivery read, by supplying that tenant explicitly. A host
that does not runs untenanted, and untenanted is a real partition with a reserved key — not a missing value,
and not the empty string, which is not portable across providers.

**How that is enforced, and how you can check it.** The reserved key is a property of the SCHEMA, not a
convention the store is trusted to follow: on the relational providers the tenant column is declared
`NOT NULL DEFAULT '__untenanted__'`, so a row that carries no tenant is inexpressible rather than merely
discouraged, and a writer that omits the column entirely still lands in the untenanted partition. The
staging path binds the term through `KeyedTenantPartition`, which has no empty inhabitant, so the value
written is total by construction rather than by the caller having supplied one.

**What a DRAINED message carries, on every provider.** The paragraph above is about the stored column; this
is about the object the drain hands back, and it is the part a consumer's handler actually branches on. A
message staged with no tenant is drained carrying the reserved key — **never a null, an empty string, or
whitespace** — on every provider, document and key-value stores included, not only the relational ones. So a
handler written as `msg.TenantId is null` is wrong by construction: it would re-establish a different
partition depending on which store happened to be underneath it. Compare against the reserved key, or route
the value through `KeyedTenantPartition.FromStoredValue`, which is total over every spelling.

This one is a contract on the store rather than a property of a schema — `OutboundMessage.TenantId` stays
settable-null so that *staging* can express "no tenant", which means the type cannot make the absence
unrepresentable on the way out. It is enforced instead by the published conformance kit's
`UntenantedPartition_MustRoundTripItsOwnMessage` arm, which fails a store that round-trips the absence as an
absence as well as one that invents a tenant. A consumer implementing their own store has that arm as the
single answer to implement against.

The empty string is excluded for a portability reason worth stating explicitly: Oracle folds `''` to
`NULL`, so an empty sentinel would collapse back into the missing value the constraint exists to remove —
the column would satisfy `NOT NULL` while the representation stayed split. That is why the reserved key is
non-empty.

Evidence, each a never-skipped lock against a real database: `PostgresOutboxTenantTotalityShould` and
`OracleOutboxTenantTotalityShould`. Both assert both arms — an omitted tenant defaults to the reserved
key, and a real tenant is stored verbatim rather than absorbed by that default — and both drive the
shipped provisioning and upgrade scripts rather than a copy of them.

**The SQL Server outbox has no such lock.** An earlier revision of this paragraph listed
`SqlServerEventTenantTotalityShould` as the third per-provider row. That type exists and passes, but it
locks `EventStoreEvents` — the event store, a different subsystem with its own migration — so it is not
evidence about this table. The SQL Server outbox's tenant totality is therefore **UNVERIFIED**: the
schema declares the constraint, and no never-skipped lock proves the shipped script still carries it.

**Known gap — a database created before the tenant column was made total is not converged automatically.**
Until the provider's upgrade script is run, such a database still holds `NULL` for untenanted rows, which
is the second spelling this guarantee exists to remove. Run it with the processor stopped, and deploy the
package first: the current staging path binds the reserved key, so the older package is the one that
cannot satisfy the new constraint.

**That gap has two cases and they do not take the same remedy.** A database whose tenant column is
present but NULLABLE is converged by the provider's tenant-totality script alone. A database
provisioned before the column EXISTED is not: that script tests for the column, finds none, and
reports that there is nothing to do — a true statement about itself which reads as a clean upgrade.
Such a database needs an additive step that adds the missing column first, and only then is the
totality script operating on anything.

Where that additive step lives is per provider, and the mechanism differs because the dialects differ:

| provider | absent-column upgrade |
|---|---|
| PostgreSQL | In the create-schema script, as a guarded `ADD COLUMN IF NOT EXISTS` per column. Running the packaged scripts in order converges a database provisioned under any earlier revision. |
| SQL Server | In the create-schema script, as a guarded `COL_LENGTH` test per column. Running the packaged scripts in order converges a database provisioned under any earlier revision. |
| Oracle | In the create-schema script, as a guarded per-column `ALTER TABLE … ADD` driven from the data dictionary. Oracle has no create-if-not-exists, so each `CREATE` is issued from a block that swallows only "name is already used" — which is what makes the script re-runnable, and therefore able to act as the upgrade path at all. Running the packaged scripts in order converges a database provisioned under any earlier revision. |

The general rule this leaves: **run the provider's scripts in order and in full.** The totality script is
not sufficient on its own, and skipping the create-schema script ahead of it does not announce itself
uniformly: on PostgreSQL the totality script emits a notice and exits successfully, while on Oracle it
raises and names the script to run first. A successful exit is therefore not evidence of a converged
database on every provider.

## Consumer obligations

- **Handlers MUST be idempotent** (at-least-once ⇒ duplicates on retry/crash).
- **Set the retry floor `F` greater than the maximum expected delivery duration.** If a dispatcher pauses
  mid-delivery long enough for its lease to expire, a retry can overlap the in-flight send; `F > max
  delivery duration` keeps that window closed to normal operation. `F` is per provider
  (`FailureBackoffFloorSeconds`, default 30 s) and is validated to exceed the poll interval.
- **On the change-feed providers only, also set the lease timeout above the maximum clock skew** between
  any two claimant processes. Cosmos DB, DynamoDB and Firestore decide lease eligibility on the *claimant's*
  clock, so skew is part of their duplicate window — see *Fault model* for the bound, stated so it can be
  falsified. This obligation no longer applies to `F` on the polling family: every one of those providers
  now decides the floor, the lease and the schedule on the store's own clock, so no dispatcher's clock is on
  either side of any of those comparisons. An earlier revision of this document asked you to size `F`
  against dispatcher-to-store skew, and against dispatcher-to-dispatcher skew on MongoDB, Redis,
  ElasticSearch and Marten. Both are obsolete, and the second contradicted this document's own fault-model
  table by the time it was written.
- **A custom `IBackoffCalculator` cannot go below the floor.** `IBackoffCalculator` returns an arbitrary
  `TimeSpan` and the computed-backoff path composes it with `F` as a **maximum**, so a delay shorter than `F`
  is raised to `F` and a longer one is preserved verbatim. An earlier revision of this document said the
  opposite — that your value is bound verbatim and is not clamped — which described the behaviour before the
  composition landed and would have had you build your own floor into a calculator that does not need one.
- **If you run more than one dispatcher process, either run a leader election** (which forces a fenced
  store) **or keep every dispatch on the claimed drain paths** — see *The publisher drain: every loop claims*.
- For related-message ordering, set `PartitionKey` / `SequenceNumber`; do not rely on global ordering.
- **The outbox table's primary key MUST be the message-id column alone.** If you provision the schema
  yourself, do not make it composite — not `(Id, TenantId)`, not `(Id, PartitionKey)`, not any wider key.

  This is load-bearing, not stylistic. The drain-then-mark protocol addresses a row by its id: a
  mark-sent, mark-failed, or delete targets `WHERE <id column> = @MessageId` plus, on the release path, a
  reservation-ownership guard. That is sound only while the id alone identifies at most one row. Under a
  composite key an id can address **more than one row**, and the two ways it fails are both silent:

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

The shared conformance suite is **`OutboxStoreConformanceTestKit`**
(`src/Excalibur/Excalibur.Testing.Conformance/Conformance/OutboxStoreConformanceTestKit.cs`). Note that it
lives in `src/` and is a **shipped public type** in the consumer testing package, not a private test-tree
base — an earlier revision cited it under a `tests/Shared/…` path and an older type name, neither of which
exists. If you implement your own store, deriving from this kit is how you check it against the contract
above.

Arms below are cited **by name only, deliberately**. An earlier revision carried a line number beside each
one; every one of them was stale within a single edit of the kit, and a reader who checked found the
citation wrong rather than the evidence missing. A name is greppable and does not rot.

Eight providers derive the kit: SQL Server, Postgres, Oracle, MongoDB, Marten, Redis, ElasticSearch,
InMemory. Every deriver that needs a container opens with a hard `DockerAvailable.ShouldBeTrue(...)`, so a
missing container **fails** rather than passing vacuously. InMemory needs none and runs as a unit suite.

**Every labelled guarantee in this document has a row here, including the ones no arm enforces.** A
guarantee absent from this table used to read as an oversight; the rows marked UNVERIFIED are the honest
state, and they are listed rather than omitted so a reader can tell "nothing enforces this" apart from
"nobody wrote it down."

| Guarantee | Conformance arm |
|---|---|
| **D1** lease before dispatch | **UNVERIFIED — no arm.** Nothing fails when a drain path dispatches without claiming first; S1's arm proves concurrent claims are disjoint, not that a dispatch was preceded by a claim. See *The central invariant* for the falsification condition |
| **D2** single-clock decisions | `MongoDbOutboxClaimClockSkewShould`, `MartenOutboxClaimClockSkewShould`, `ElasticsearchOutboxClaimClockSkewShould`, `ElasticsearchOutboxRetryFloorClockSkewShould`, `RedisOutboxClaimClockSkewShould` — each pairs a skewed-dispatcher safety arm with a liveness arm asserting an elapsed lease *is* reclaimed. **UNVERIFIED on the change-feed family**, which exposes no server-clock primitive to assert against — see *Fault model* |
| S1 disjoint claim | `GetUnsentMessagesAsync_ConcurrentClaimers_ShouldReceiveDisjointSets` — safety: sets never overlap; liveness: work *is* handed out |
| R1 floor / re-claimability | `MarkFailed_WithinTheFloor_ShouldNotBeReclaimable_ReservedPath` and `_UnclaimedPath` — safety; `MarkFailed_AfterTheFloorElapses_ShouldBecomeReclaimable` — liveness; `MarkFailed_ByTheClaimOwner_ShouldRecordAndRelease` — the owner's own report still lands |
| R2 release ownership | `MarkFailed_ByANonOwner_ShouldNotReleaseTheClaim` |
| **R2′** mark-sent takes no ownership term | **Structural, not behavioural.** `IOutboxStore.MarkSentAsync(string messageId, CancellationToken)` accepts no owner token, so there is no value a store could compare — the property is read off the signature rather than asserted by an arm. A future overload taking a token would change the contract, not break a test |
| R3 monotonic attempts | `MarkFailedAsync_ShouldSetRetryCount` and `MarkFailed_StaleLateReport_ShouldNotLowerTheAttemptCount` |
| **L1** never DELIVERED and ACTIONABLE in the dead-letter queue | **Unit arms only; UNVERIFIED against a real store.** `OutboxProcessorShould.WithdrawTheDeadLetterEntry_WhenTheFencedMarkIsRefused` and `WithdrawTheDeadLetterEntry_WhenTheTenureHasNoTokenAtAll` (safety) and `NotWithdrawTheDeadLetterEntry_WhenTheMarkIsApplied` (liveness) drive the processor against substituted stores and observe the withdrawal; no arm yet observes it on a real dead-letter queue. Claimed only for the synchronous-refusal path, and only under four conditions a consumer can cause — see *The central invariant* |
| Bounded store refuses rather than discards | `OutboxCapacityEvictionShould` — safety: `RefuseToStage_RatherThanDiscardAnUnsentLeasedMessage`; liveness: `EvictTheDeliveredMessage_WhenOneIsAvailable`, `EvictTheDeadLetteredMessage_RatherThanRefusing`. Runs unconditionally, no backing service |
| Ordering within a claim | `OutboxOrderingPersistenceIntegrationShould` (SQL Server), `PostgresOutboxKeystoneRoundTripShould`, `OracleOutboxKeystoneRoundTripShould` — each stages out of order and requires an ascending claim, each paired with a round-trip arm so the assertion cannot pass against unread columns |
| Full-field durability | `StageMessageAsync_ShouldRoundTripEveryCallerSuppliedField` |
| F1 fencing | `Fencing_StaleToken_ShouldBeRefusedWithoutApplyingTheMutation`, `Fencing_Refusal_ShouldReportTheHighWaterMark`, `Fencing_CurrentLeaderToken_ShouldClaimAndComplete`, `Fencing_HighWaterMark_ShouldSurviveCleanup`, `Fencing_SupersededLeader_ShouldNeitherMutateNorLoseTheMessage`, `Fencing_ReclaimedMessage_ShouldRefuseTheSupersededMarkSent` |

**The table above is the registry of the LABELLED guarantees only.** This document also states guarantees
at section level, without a letter-and-number label, and those are as binding on us as the labelled ones.
They are listed here too, so that "every guarantee sentence in this document names its evidence" is a
property you can check against one place rather than by reading the whole file:

| Section-level guarantee | Stated under | Conformance arm |
|---|---|---|
| The two delivery families are mutually exclusive by construction | *Scope* | `OutboxCapabilityMatrixShould.Store_BelongsToExactlyOneFamily` — a frozen per-store matrix asserting `IOutboxStore` XOR `ICloudNativeOutboxStore` |
| A bounded store refuses rather than discards | *Delivery guarantee* | `OutboxCapacityEvictionShould` — three arms, no backing service, runs unconditionally |
| Ordering within a claim is partition key, then sequence number, then creation time | *Ordering* | `OutboxOrderingPersistenceIntegrationShould` (SQL Server), `PostgresOutboxKeystoneRoundTripShould`, `OracleOutboxKeystoneRoundTripShould`, each paired with a round-trip arm |
| A superseded leader's claim and mark-sent are refused by the durable high-water | *How it is achieved*, step 5 | `SqlServerOutboxFenceClaimsUnderSnapshotShould`, `Fencing_SupersededAfterItsOwnClaim_ShouldRefuseTheMarkSent`, and `FenceCheckAndGuardedMutationShareOneLockTests` for the in-process store — see the F1 row for what each does and does not establish |
| The fencing diagnostics surface reads the recorded high-water, and a lowering reset requires `force` | *How it is achieved*, step 5 | `FencingDiagnostics_GetHighWater_ShouldReportTheRecordedValue`, `FencingDiagnostics_Reset_ShouldRefuseLoweringWithoutForceAndSucceedWithForce`. Both self-skip where the capability is absent |
| A host is refused at startup when a leader election is registered without a fencing-capable store and a gate | *How it is achieved*, step 5 | `OutboxRefusesUnfencedLeaderElectionShould` — the refusal plus a liveness arm proving an election-free host still starts and drains |
| The SQL Server mark-sent guard's refusal-versus-not-found classification is lossy | *How it is achieved*, step 5 | **UNVERIFIED — no arm.** Nothing RED-detects the misclassification; the mutation still refuses correctly |
| A refusal routed into an unfenced failure member is admitted | *How it is achieved*, step 5 | **UNVERIFIED — no arm.** An arm that RED-detects a refusal reaching a failure member is what would move this off UNVERIFIED |
| The caller's computed backoff is composed with the floor as a maximum, over durations, on the server clock | *Per-message backoff* | `OracleOutboxBackoffFloorClampShould` — two skew arms driving a dispatcher an hour ahead of the database |
| A row that cannot be decoded is dead-lettered on first encounter and never charged to the breaker | *Fault model* | `OutboxStagedPayloadDrainShould` — three arms, including the liveness arm that a healthy message behind a corrupt one is still delivered |
| The change-feed claim returns a set disjoint from every concurrent call, under a lease | *The claim capability* | `CosmosDbOutboxStoreClaimAtomicityShould`, `DynamoDbOutboxStoreClaimAtomicityShould`, `FirestoreOutboxStoreClaimAtomicityShould` — each opening with a hard `DockerAvailable` assertion. Only the concurrency arm is non-vacuous against the atomic step |
| Messages a change-feed read returns come back in creation order | *The change-feed family* | **UNVERIFIED — no arm.** The mechanism is named per provider in that section; nothing locks the ordering |
| Registering both a change-feed subscription and the claim drain is refused at host start | *The claim capability* | `OutboxRefusesTwoDeliveryMechanismsShould` — the refusal plus a liveness arm per mechanism alone |
| Change-feed checkpoint persistence fails open and raises a degraded signal at the bound | *Checkpoint durability* | `CosmosDbChangeFeedCheckpointFailOpenShould` — both arms, safety and liveness |
| A message staged with no tenant is drained carrying the reserved key on every provider | *Tenant scoping* | `UntenantedPartition_MustRoundTripItsOwnMessage` in the published conformance kit; `PostgresOutboxTenantTotalityShould` and `OracleOutboxTenantTotalityShould` drive the shipped provisioning scripts |
| The SQL Server outbox table's tenant column is total | *Tenant scoping* | **UNVERIFIED — no lock.** The schema declares the constraint; no never-skipped arm proves the shipped script still carries it |
| Sent and dead-lettered are terminal and no completion may reverse either | *Evidence*, below | `SqlServerOutboxMarkBatchFailedGuardsShould`, `SqlServerOutboxSingleMarkFailedTerminalGuardShould`, `SqlServerOutboxMarkFailedLeaseClearShould`, `ElasticsearchOutboxStoreClaimAtomicityShould`. The status-recompute path and the remaining status-column providers are **UNVERIFIED** — guarded, unproven |
| A sent message is deleted once it has aged past the configured retention bound | *Retention* | `OutboxRetentionContributorShould` — safety and liveness, against the real in-memory store |

Two regions carry their own non-vacuity guard, because every arm in them returns without asserting when
its staging seam yields nothing — correct for a store that lacks the capability, and useless as evidence
for one that has it:

- `ReclaimFloorSuite_ShouldExerciseThisStoreOrNotDeclareIt` — a suite that declares the re-claim floor seam
  and then returns nothing fails rather than passing empty.
- `OwnershipSuite_ShouldExerciseThisStoreOrNotDeclareIt` — the same for R2. A suite that overrides
  `TryReserveMessageUnderForeignDispatcherAsync`, and so declares it can stage a foreign claim owner, must
  actually produce one. This matters most where the ownership guard has a bespoke shape: a provider whose
  guard matches on a prefix of a composite reservation token is exactly the provider whose ownership arm
  needs to run, and a guard of that shape has silently matched zero rows before.

A suite that has not overridden the seam at all has genuinely opted out and neither guard fires.

**One arm does not run everywhere:**

- **Full-field durability is skipped on Oracle**, and the reason recorded on the skip is out of date: it
  says Oracle does not persist priority or the multi-transport routing fields, but the Oracle insert
  statement and both reserve statements carry `priority`, `partition_key`, `group_key`, `sequence_number`,
  `target_transports` and `is_multi_transport` as dedicated columns. The round-trip is therefore
  **UNVERIFIED on Oracle** — not known broken, and not proven. Treat it as unproven until the arm is
  re-enabled.

An earlier revision also recorded S1 as skipped on ElasticSearch, with its coverage relocated to a
dedicated lock. **That was wrong in the direction of understating the evidence.** The ElasticSearch
conformance suite declares no skip at all, so S1 runs there through the kit like every other provider —
*and* the dedicated lock exists alongside it: `ElasticsearchOutboxStoreClaimAtomicityShould`, six arms
proving disjoint batches, expired-lease reclaim, single-winner mark-sent, that a concurrent success and
failure settle on delivered, and that a late failure report cannot reopen a delivered message. That
provider is covered twice, not once.

**Sent and dead-lettered are both terminal, and no completion may reverse either.** This is not one of
the kit arms above, because it is a property of each provider's completion *statements* rather than of
the shared contract, and it is the one invariant the ownership guard cannot carry: marking sent releases
the lease, so after delivery the row reads as unleased to every dispatcher alive — the superseded one
included. What refuses a late report is a separate term naming both terminal values in the same
statement. Reversing either returns a message whose delivery outcome was already decided to the claim
pool, producing a duplicate from our own bookkeeping rather than from any transport. `Sending` is **not**
terminal and is deliberately outside this term: a completion must still be able to move a message out of
it.

**The population is defined by EFFECT, not by name.** A statement carries the term because it *writes the
parent status column* — a wider set than the statements with "fail" in their names. One that recomputes a
parent status from its per-transport rows writes that column too, and without the term an all-failed
transport set returns a dead-lettered message to the claimable failed set. A statement joins this
population by touching that column, and carries the term for that reason alone. **SQL Server carries it
on every statement in that set**, and three never-skipped real-infrastructure classes bind the failure
paths against a live container: `SqlServerOutboxMarkBatchFailedGuardsShould` for the batch path,
`SqlServerOutboxSingleMarkFailedTerminalGuardShould` for the single-message path and the
backoff-scheduling path the processor prefers, and `SqlServerOutboxMarkFailedLeaseClearShould` for
ownership and lease release. Each pairs the refusal with a liveness arm, so a statement that matched
nothing at all would fail rather than pass. The status-recompute path carries the term but has **no
dedicated arm** — guarded, unproven. ElasticSearch carries the property too, bound by
`ElasticsearchOutboxStoreClaimAtomicityShould`.

**Postgres and Oracle reach it by construction instead, and need no guard.** Both DELETE the row on
mark-sent rather than moving it to a terminal status, so a delivered message has no row for a late
failure report to match; their failure statement carries an ownership term and no status term, because
those stores have no status column. The property holds there for a structural reason rather than a
guarded one — which is stronger, but it also means it cannot be checked by reading the failure statement.
The remaining status-column providers carry the same both-terminal-values term on their own completion
statements, but have **no dedicated executed arm**; treat the property there as enforced and unproven.
The change-feed family reaches it a third way: those stores track publication with a flag and have no
status concept for a late report to move, so there is nothing to reverse.

## Provider maturity

All rows below are the polling (`IOutboxStore`) family. Cosmos DB, DynamoDB and Firestore are **not** in
this table — see *The change-feed family*.

| Provider | Claim primitive | Fencing | At-least-once conformance |
|---|---|---|---|
| InMemory | in-proc lock + leases | ✅ in-process high-water | ✅ full |
| SqlServer | `UPDATE…OUTPUT` READPAST/UPDLOCK/ROWLOCK | ✅ durable — dedicated `OutboxFence` control table cleanup never touches, advanced by a serializable `MERGE … WITH (UPDLOCK, HOLDLOCK)`; monotonic and fail-closed. Advance-then-re-guard, not one statement. Verified against a live container by `Fencing_HighWaterMark_ShouldSurviveCleanup` | ✅ full |
| Postgres | `FOR UPDATE SKIP LOCKED` | ✅ co-atomic with the claim | ✅ full |
| Oracle | `FOR UPDATE SKIP LOCKED` | ✅ co-atomic with the claim | ✅ full except full-field durability, which is **UNVERIFIED** (skipped arm; see Evidence) |
| MongoDB | `FindOneAndUpdate` | ❌ does not fence — cannot keep a high-water durable; refused at startup under a leader election unless `AsSingleWriter()` | 🚧 completing (retry-floor / ownership) — treat as **UNVERIFIED** until green. Claim eligibility is decided on the MongoDB server's clock, via an `$expr` over `$$NOW` (see *Fault model*) |
| Redis | Lua script | ❌ does not fence — cannot keep a high-water durable; refused at startup under a leader election unless `AsSingleWriter()` | ✅ full. Claim eligibility is decided on the Redis server's clock, via `redis.call('TIME')` inside the script |
| Marten | claims table via `INSERT … ON CONFLICT … RETURNING` | ❌ unfenced (single-writer) | 🚧 completing — **UNVERIFIED** until green. Claim eligibility is decided on the PostgreSQL server's clock, via `clock_timestamp()` |
| ElasticSearch | `if_seq_no`/`if_primary_term` CAS stamping a per-message lease | ❌ unfenced (single-writer) | 🚧 the **claim is verified** by a dedicated never-skipped lock against a live container: concurrent pollers receive disjoint batches, an expired lease is reclaimable, a late failure report cannot reopen a delivered message, and concurrent mark-sent admits exactly one winner. The **full at-least-once suite is UNVERIFIED** until green. Near-real-time index: a claim must force a refresh after stage or it can see stale rows. Claim eligibility — lease, retry floor and schedule alike — is decided on the Elasticsearch node's clock; an earlier revision of this row said the dispatcher's, which contradicted this document's own fault model |

> A cell marked **UNVERIFIED** means the guarantee is *intended* but the non-skipped real-infra conformance
> arm proving it is not yet green for that provider — do not depend on it there in production until it is.
> **UNVERIFIED is not the same as broken**, and it is not the same as verified-absent; where a property is
> known not to hold, this document says so directly.

## Limitations

- **At-least-once, not exactly-once.** The dispatcher-ownership guard on the release path is
  *process-granular*: a paused dispatcher whose lease expired can, on resumption, free a **newer**
  reservation held by the same process, so the same message may be re-claimed while an earlier delivery is
  still in flight. Exactly-once would require threading a per-attempt token through the failure path; it is
  not currently provided. Idempotent handlers make at-least-once correct.
- **`F` bounds re-claimability on every path the shipped drains run.** All three publisher loops and the
  processor reach the store through the same claim, which carries the floor, and the computed-backoff
  path composes the caller's delay with `F` as a maximum (`GREATEST(delay, floor)`), so it can only push
  the next attempt further out. What `F` does **not** bound is a duplicate arising from a delivery already
  in flight when the reservation expires — that window is the reservation timeout, not `F`.
- **D1's second clause is met by the drains this package ships, and is not a property of the store.** All three
  loops the default drain runs claim before dispatch. It is a statement about our drains only: a consumer that
  selects rows by its own query and dispatches them without claiming defeats it, and no store can detect that.
- **D2 is unmet on the change-feed claim capability only.** Cosmos DB, DynamoDB and Firestore decide
  `ClaimPendingAsync` eligibility by comparing two claimant processes' clocks; where that difference reaches
  the lease timeout the lease excludes nobody and the duplicate window is unbounded. Every `IOutboxStore`
  provider now decides claim eligibility on the store's own clock. The computed-backoff path still compares
  a dispatcher's absolute instant against the database server's clock on Oracle, Postgres and SQL Server,
  bounded to lengthening the floor.
- **Mark-sent carries no ownership term** (R2′). Ownership guards release, not completion.
- **Leadership fencing is opt-in**, but where a leader election is registered and single-active-writer is
  not asserted, an unfenced store is **refused at startup** rather than silently relied upon. Unfenced
  stores require the deployment to run a single active dispatcher.
- **MongoDB carries two durable shapes for its instants, and its TTL index only sees one.** Every instant
  on a MongoDB outbox message is now stored as a BSON date, because the claim predicate is evaluated on
  the server's own clock and a comparison against `$$NOW` is only expressible against a date. A message
  staged by an earlier version is stored in the driver's default shape for a `DateTimeOffset` — a
  `{ DateTime, Ticks, Offset }` sub-document — so an upgraded collection holds both. The store reads both
  shapes wherever it compares an instant, on the claim path and on the admin queries, so a message staged
  before the upgrade is claimed, scheduled, gated and cleaned up on the instant it actually carries. Two
  consequences are not closed by that and are stated here rather than implied:
  - **The TTL index does not expire a message staged before the upgrade.** It is declared over the
    sent-time field, and MongoDB's expiry monitor acts only on a date; a sub-document is left alone. Such
    messages are removed by the retention sweep instead, which does read both shapes. A deployment that
    relies on the TTL index alone and never runs the sweep will retain its pre-upgrade sent messages
    indefinitely.
  - **During a rolling upgrade the reverse direction is not symmetric.** An instant written as a date by an
    upgraded dispatcher is invisible to the comparisons an earlier dispatcher makes, because query
    operators are type-bracketed. While both versions run, a lease stamped by an upgraded dispatcher is not
    reclaimed by an earlier one until the earlier one is retired. This delays recovery of a crashed
    upgraded dispatcher's messages for the length of the rollout; it does not duplicate or lose a delivery,
    and it resolves when the rollout completes.

  Consumers who want the second shape retired can rewrite it in place; the sub-document's `DateTime` member
  is the instant, and setting each instant field to that member converts a message to the current shape.
  Doing so while an earlier version is still running will hide those messages from it, so it belongs after
  the rollout rather than during it.
- **No global ordering.** Messages are handed out `PartitionKey`, `SequenceNumber`, then creation time
  within a claim only.

## Retention (GDPR right-to-erasure via bounded age)

Outbox payloads can carry data-subject content, and this surface carries no per-subject encryption key --
the encryption seam here is single-context, documented as such on `AddOutboxEncryption`. So the
erasure guarantee on this surface is **bounded retention provably elapsing**, not key destruction: a sent
message is deleted once it has aged past a configured retention bound, independent of which subject it
belongs to.

**Stated so it can be falsified.** Given `AddOutboxRetention` is registered (in addition to
`AddRetentionEnforcement`) with `RetentionDays = N`: after an enforcement pass evaluated at time `T`,
every message whose `SentAt` is at or before `T - N days` is absent from the store, while a message whose
`SentAt` is after that bound is unaffected and still present.

**How it is achieved.** `OutboxRetentionContributor` (`Excalibur.Compliance`) is registered against the
`IRetentionContributor` seam that `IRetentionEnforcementService` already orchestrates and schedules --
the same seam the erasure side's `IErasureContributor` plugs into. On each pass it computes the cutoff
from `OutboxRetentionOptions.RetentionDays` and repeatedly calls the store-agnostic
`IOutboxStoreAdmin.CleanupAllTenantsSentMessagesAsync(cutoff, batchSize, cancellationToken)` -- the same
estate-wide, age-based deletion primitive every first-party outbox provider already implements -- until a
pass removes fewer rows than the batch size. A dry run, or `RetentionDays <= 0`, never deletes and reports
zero records cleaned, honoring `IRetentionContributor`'s "never report success while deleting nothing"
contract.

**Consumer obligation.** Retention on this surface is opt-in: call `AddOutboxRetention()` in addition to
`AddRetentionEnforcement()`. Without it, enforcement runs honestly inert for the outbox -- it logs a
warning and reports zero records cleaned rather than silently claiming success.

**Known gap.** Deletion is by age only, not by data subject: a specific subject's message cannot be
erased on demand ahead of the retention bound elapsing. A consumer with a strict per-request erasure SLA
on outbox payloads should set `RetentionDays` short enough to satisfy it, or avoid placing
subject-identifying data in outbox payloads.

**Evidence.** `OutboxRetentionContributorShould` -- safety: a message past its retention bound is gone;
liveness: a message still within the bound survives -- run against the real in-memory outbox store.
