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

### The central invariant

> **D1 — Lease before dispatch.** A message is handed to a transport only from a lease this dispatcher won
> in one indivisible action at the store, **and every path that hands a message to a transport wins that
> lease first.**

The second clause is what makes the first worth having, and **the second clause does not hold today.**
Three loops in the default drain hand messages to transports and only one of them claims — see *Paths that
dispatch without a claim*. S1 below is therefore scoped to the claim, not to delivery, and that scoping is
a description of a gap rather than a design choice.

Sub-guarantees (invariants):

| # | Property | Statement | Status |
|---|---|---|---|
| **S1** | At-most-once **per claim** | Two concurrent claimers never claim the same message — each claim returns a set **disjoint** from every concurrent claim. | Holds **on the claim path only**. Two of the three default drain loops do not claim. |
| **R1** | Backoff floor | A message failed through the claim path is not re-claimable within the floor **F**, and is re-claimable after **F**. No zero-backoff retry hot-loop. | Holds for the **claim predicate**, on both the plain and the computed-backoff failure paths, on every store implementing the computed-backoff seam — SQL Server, Postgres, MongoDB, Oracle and Redis. Each composes the caller's schedule with F as a maximum, so a shorter delay is raised and a longer one is preserved. On all five that maximum is taken over **durations** and anchored to the server clock, so no dispatcher clock reaches the persisted gate. |
| **R2** | Reservation ownership on **release** | Only the dispatcher holding a message's current reservation may **release** it (mark-failed / unreserve). Enforced as `dispatcher_id IS NULL OR dispatcher_id = @DispatcherId`. | Holds. Ownership is a stored **dispatcher identity**, so the guard is process-granular, not per-attempt. |
| **R2′** | Mark-sent is **not** ownership-guarded | `MarkSentAsync` takes only a message id, so mark-sent matches on the id — plus, where fencing is active, the leadership high-water. It carries **no** reservation-ownership term. | This is the contract, not an omission to read around. |
| **R3** | Monotonic attempts, and **termination** | The recorded attempt count never decreases (`GREATEST(attempts, n)`), **and it advances whenever a failure is recorded**, so a repeatedly failing message reaches the dead-letter ceiling in bounded attempts rather than retrying forever. | Holds **on the claim path**. Monotonicity alone is not the point of R3: a store that never decreases the count but also never advances it satisfies the letter and loses the reason, so termination is stated here explicitly rather than left implied by the ceiling. **Known gap — the scheduled-message sweep.** On PostgreSQL and Oracle the scheduled read does not project the stored attempt count, so a scheduled message reloads as though it had never been tried. Termination does not hold for a message delivered through that sweep: it is retried without approaching the ceiling. The drain reload restores the count on every provider; SQL Server is unaffected on both paths. |
| **F1** | Leadership fencing *(optional)* | On stores implementing `IFencedOutboxStore`, a superseded leader presenting a stale token cannot claim, mark-sent, or delete. | Holds on the five fenced stores. Refused at startup where required and absent — both when the store cannot fence and when nothing gates the drain. |

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

## How it is achieved (the seam)

1. **Stage** — `StageMessageAsync` persists the message durably in the same transaction as the business
   write (the outbox pattern), so a message is never lost between commit and dispatch.
2. **Claim (S1)** — `GetUnsentMessagesAsync` is an **atomic claim, not a plain read**. Each provider uses
   its native atomic primitive so concurrent claimers get disjoint sets:
   - **SQL Server** — `UPDATE … OUTPUT` with `READPAST, UPDLOCK, ROWLOCK`
   - **Postgres, Oracle** — `FOR UPDATE SKIP LOCKED` inside a claim CTE
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

   The *mechanism* is provider-dependent, and an earlier revision described only one shape and attributed
   it to a provider that has no fencing at all:
   - **Postgres, Oracle** — genuinely co-atomic. The fence advance and the claim are one statement: the
     fence row is locked and advanced in a CTE the claim CTE is gated on.
   - **SQL Server** — advance-then-re-guard, **two** statements. `EnforceOutboxFenceRequest` advances the
     high-water with a single serializable `MERGE … WITH (UPDLOCK, HOLDLOCK)`, so two concurrent leaders cannot both
     advance it; the claim then **re-guards against the same durable high-water**, closing the window
     between the two statements. The safety property is identical; the mechanism is not a single write.
   - **MongoDB, InMemory** — a stored high-water compared and advanced on the claim.

   **Where the high-water lives matters.** On the fenced relational stores it is a dedicated fence record,
   independent of the message rows, so it survives cleanup: a superseded leader's stale token is still
   rejected after cleanup has purged the sent, token-bearing rows. The per-message reservation (R2)
   independently prevents two dispatchers releasing each other's messages.

   **Fencing is opt-in, but its absence is refused rather than tolerated.** When a leader election is
   registered and the consumer has not set `OutboxDeliveryOptions.SingleActiveWriter`, **two** conditions are
   rejected at host startup by `OutboxFencingStartupInvariant.EnsureFencingCapableStore`
   (`src/Excalibur/Excalibur.Outbox/Outbox/OutboxFencingStartupInvariant.cs:48`), invoked from the outbox
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

All three funnel into one claimed drain — `MessageBusOutboxPublisher.cs:200`, `:205` and `:219` each call
`DrainClaimedBatchAsync` at `:246`, which issues the same claim the processor path uses.

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

**What this drain still does not do is present a fencing token.** It calls the unfenced claim overload, so
on a store implementing `IFencedOutboxStore` behind a registered leader election, this path drains without
fencing even though the fencing seam is wired. Claim-disjointness (S1) is unaffected — it rests on the
per-message atomic claim, not on leadership, so no message is delivered to two dispatchers from a live
lease — but a consumer who configured fencing does not get it on this path. Tracked separately.

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
dispatchers share — the store's own. No dispatcher's local clock reaches the predicate, so two dispatchers
that disagree about the time cannot disagree about whether a lease is live:

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
a single immediate read, is what the guarantee is designed around. Messages a read does return are always
in creation order on every provider.

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

> **The two delivery models are mutually exclusive. This is the load-bearing sentence in this section.**
> Use the change-feed trigger **or** a claim-based self-managed poller — never both against the same
> container. The trigger path does not observe the claim's lease: a message a poller holds is still handed to
> the trigger's handler, and both publish it. Combining them reproduces precisely the duplicate delivery the
> claim exists to prevent, and is therefore worse than either alone. They are alternatives, not layers.

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

## Tenant scoping — which statements carry a tenant term, and why none does

Every relational outbox statement now declares its tenancy decision, and the decision is part of the type
rather than a property of the SQL you have to read to discover. A request either confines itself to the
caller's partition or states, at its declaration, why it spans every tenant. As of this writing every
statement is in the second group.

A third state does exist, and this document previously denied it. The declaration is advisory: the
member carrying it is `protected` on the request base and absent from the request interface, so no
executor can read it, and a request that records no decision compiles and runs like any other. Read a
declaration as a note from its author about intent, never as evidence that the statement below it
carries a tenant term.

**Confined to the caller's partition.** None is.

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
  store) **or disable the two non-claiming drain loops** — see *Paths that dispatch without a claim*.
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

| Guarantee | Conformance arm |
|---|---|
| S1 disjoint claim | `GetUnsentMessagesAsync_ConcurrentClaimers_ShouldReceiveDisjointSets` — safety: sets never overlap; liveness: work *is* handed out |
| R1 floor / re-claimability | `MarkFailed_WithinTheFloor_ShouldNotBeReclaimable_ReservedPath` and `_UnclaimedPath` — safety; `MarkFailed_AfterTheFloorElapses_ShouldBecomeReclaimable` — liveness; `MarkFailed_ByTheClaimOwner_ShouldRecordAndRelease` — the owner's own report still lands |
| R2 release ownership | `MarkFailed_ByANonOwner_ShouldNotReleaseTheClaim` |
| R3 monotonic attempts | `MarkFailedAsync_ShouldSetRetryCount` and `MarkFailed_StaleLateReport_ShouldNotLowerTheAttemptCount` |
| Full-field durability | `StageMessageAsync_ShouldRoundTripEveryCallerSuppliedField` |
| F1 fencing | `Fencing_StaleToken_ShouldBeRefusedWithoutApplyingTheMutation`, `Fencing_Refusal_ShouldReportTheHighWaterMark`, `Fencing_CurrentLeaderToken_ShouldClaimAndComplete`, `Fencing_HighWaterMark_ShouldSurviveCleanup`, `Fencing_SupersededLeader_ShouldNeitherMutateNorLoseTheMessage` |

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

## Provider maturity

All rows below are the polling (`IOutboxStore`) family. Cosmos DB, DynamoDB and Firestore are **not** in
this table — see *The change-feed family*.

| Provider | Claim primitive | Fencing | At-least-once conformance |
|---|---|---|---|
| InMemory | in-proc lock + leases | ✅ in-process high-water | ✅ full |
| SqlServer | `UPDATE…OUTPUT` READPAST/UPDLOCK/ROWLOCK | ✅ durable — dedicated `OutboxFence` control table cleanup never touches, advanced by a serializable `MERGE … WITH (UPDLOCK, HOLDLOCK)`; monotonic and fail-closed. Advance-then-re-guard, not one statement. Verified against a live container by `Fencing_HighWaterMark_ShouldSurviveCleanup` | ✅ full |
| Postgres | `FOR UPDATE SKIP LOCKED` | ✅ co-atomic with the claim | ✅ full |
| Oracle | `FOR UPDATE SKIP LOCKED` | ✅ co-atomic with the claim | ✅ full except full-field durability, which is **UNVERIFIED** (skipped arm; see Evidence) |
| MongoDB | `FindOneAndUpdate` | ✅ stored high-water | 🚧 completing (retry-floor / ownership) — treat as **UNVERIFIED** until green. Claim eligibility is decided on the MongoDB server's clock, via an `$expr` over `$$NOW` (see *Fault model*) |
| Redis | Lua script | ❌ unfenced (single-writer) | ✅ full. Claim eligibility is decided on the Redis server's clock, via `redis.call('TIME')` inside the script |
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
- **The duplicate window is bounded by F only on the claim path.** The retry loop does not read the floor,
  and the computed-backoff path is not clamped to it. Neither is bounded by F. Do not describe F as a
  global bound on duplicate delivery.
- **D1's second clause is unmet.** Two of the three default drain loops dispatch without winning a lease,
  and can select rows the claim path currently holds.
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
