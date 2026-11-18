# Architecture — Excalibur.LeaderElection

> **Guarantee contract for leader election.** This document is the source of truth for *what mutual-exclusion
> guarantee each provider offers, how it is achieved, and which of those guarantees is actually enforced by a
> test.* It is a contributor + integrator reference. Keep it current: any change to an acquire, renew, expiry,
> or fencing path updates this file, verified at architectural review.

## The guarantee

**At most one leader per resource, bounded by the provider's expiry mechanism and its grace period.**

Stated falsifiably: two candidates contending for the same resource never both observe themselves as leader
**except** during a window bounded by the incumbent's lease expiry plus its configured grace period. Outside
that window the election is mutually exclusive. It is **not** a distributed lock strong enough to protect a
non-idempotent side effect on its own — see *Consumer obligations*.

Sub-guarantees:

| # | Property | Statement |
|---|---|---|
| **M1** | Mutual exclusion | Of N concurrent candidates for one resource, at most one holds leadership at any instant. |
| **M2** | Liveness | Some candidate does acquire leadership, and leadership transfers after the incumbent stops or its lease lapses. Mutual exclusion alone is satisfied by electing nobody, so this arm is part of the contract. |
| **F1** | Fencing | A leadership tenure carries a monotonic fencing token, so a superseded leader presenting a stale token is rejected fail-closed by a downstream fenced resource. |
| **G1** | Grace bound | A candidate that cannot confirm its own leadership relinquishes within its grace period. The grace period is a hard upper bound on time-to-relinquish; a provider may relinquish sooner but never later. |

## How it is achieved (per provider)

| Provider | Acquire primitive | Expiry | Fencing token | Grace / skew |
|---|---|---|---|---|
| **Kubernetes** | `Lease` object replaced under an API-server `resourceVersion` compare-and-swap; a lost race surfaces as a `409 Conflict` | `renewTime` + lease duration, renewed on a timer | **Native and co-atomic** — the token is the Lease's own transition counter, advanced by the same write that changes the holder, and the advance is computed from the counter read out of the same lease object whose `resourceVersion` the write is conditioned on. *(Corrected: this row previously recorded that the transition predicate read the process's cached belief rather than the store's reported holder. That defect is fixed — both safety decisions now derive from the store — and the row is updated rather than left standing, because a stale defect record argues against its own fix.)* Local authority now expires independently of pending SDK work: `IsLeader` and `CurrentLeadership` derive from a monotonic proof deadline at `RenewInterval + GracePeriod` from the confirmed request start. Retirement seals further ownership operations and queues the loss notification; consumer callbacks may delay delivery. Actual SDK operations and resource shutdown still join completion, so this is not a finite RPC-completion or shutdown bound for an uncooperative client | **Two-clock**: the incumbent writes `renewTime` from its own clock and the challenger judges it against the challenger's, so the explicit grace addend must exceed the worst-case inter-candidate skew |
| **MongoDB** | `FindOneAndUpdate` whose *update* is an aggregation pipeline: takeover eligibility is decided **server-side inside the same document operation**, so there is no read-decide-write window | `expiresAt` field plus a TTL index for cleanup | Present. An in-document token is computed co-atomically, but the **default** provider mints from a separate counter document, because the in-document value is destroyed by release and by TTL expiry | Compared **server-side against the database's own clock**, not a local one, plus a takeover grace covering the incumbent's renewal jitter |
| **SQL Server** | `sp_getapplock` (exclusive, session-owned) on a hardened, non-pooled connection — the lock *is* the claim | Connection-session scoped; verified by reading the app-lock mode back, yielding a three-state answer | Present, minted from a dedicated `SEQUENCE` inside the acquire path before leadership is declared; fail-closed if the mint cannot complete | Strict comparison against elapsed monotonic time. Supports an **accelerate-only** early relinquish on a definitive loss — it can only shorten time-to-relinquish, never extend past the grace period |
| **PostgreSQL** | `pg_try_advisory_lock` on a dedicated, non-pooled session — the advisory lock *is* the claim | Connection-session scoped; verified by re-reading the lock's presence for this backend | Present, minted from a dedicated `SEQUENCE` inside the acquire path before leadership is declared; fail-closed | Strict comparison against elapsed monotonic time. Deliberately has **no** accelerate path, so grace is the sole bound |
| **Redis** | `SET key value NX PX ttl` — the single-shot set-if-absent is the claim | **Server-side TTL** is the real expiry; renewal is an owner-token compare-then-extend script, so a non-owner cannot extend | Present, minted by incrementing a separate counter key **after** the claim succeeds; fail-closed — the just-acquired lock is released if the mint fails | Expiry safety rests entirely on the Redis server's TTL and never on a client clock; the client-side grace only governs how quickly a candidate self-demotes after a renewal fault |
| **Consul** | Session-scoped KV acquire — the Consul server enforces one holder per session | Session TTL, renewed on a timer, plus a lock delay that defers reacquisition after invalidation | Present, minted by a bounded compare-and-swap on a separate counter key after the acquire succeeds; fail-closed | Grace period, enforced client-side on the renewal and monitor paths, on top of the Consul server's session TTL and lock delay |
| **InMemory** | First-come-first-served insert into a process-local dictionary, taken **only while the candidate is running** — the acquisition re-reads its own lifecycle state after the insert and hands the resource straight back if it stopped in between, because the timer callback and the start path can both be racing a shutdown and an entry check cannot establish the invariant. Release is a **compare-and-remove against the releasing candidate's own id**, so a candidate can only ever relinquish its own tenure — its shutdown, dispose, and unhealthy step-down paths are separate and none excludes the others, so a release that merely removed *the resource* could delete a successor's record | **None** — leadership ends only on stop, dispose, or the unhealthy step-down path | **Present, minted in-process.** A per-resource `long` counter in the shared process state (`InMemoryLeaderElectionSharedState.FencingTokens`), incremented atomically per mint (`ConcurrentDictionary.AddOrUpdate`); fail-closed at `long.MaxValue`. The provider and the process are the same thing here, so an in-process counter is a genuinely correct arbiter — not a weaker stand-in for the distributed providers' external counters | Not applicable — no lease timestamp exists |

**Fencing co-atomicity is a spectrum, and it matters.** Kubernetes advances its token in the same write that
transfers leadership. SQL Server, PostgreSQL, Consul, Redis, and MongoDB's default path mint the token in a
**separate operation sequenced immediately after** the claim. All of those fail closed — if the mint cannot
complete, the lock is released and leadership is never declared — so a tenure without a valid token is not
observable. But the token is not derived from the same atomic step as the claim, which is a weaker structural
property than the Kubernetes case.

### Fencing capabilities and counter-history boundary

`IFencingTokenProvider` is the read/validation capability. `IFencingTokenIssuer` extends it with an actual mint; six self-minting elections depend on that capability. Kubernetes exposes only the reader and advances its Lease transition counter in the native ownership CAS. The default issuer registration projects the selected reader without taking ownership of it twice. Explicit independent issuer registrations are application-owned authority composition.

Validation requires a recorded positive mark and a positive presented value at or above that mark. It is a read-time check, not proof of issuance or a promise about future state. Consumers pin the tenure token and enforce it at the protected resource's write linearization point. **F1 assumes the authority's counter history survives.** Deleting, resetting or rolling back that history can admit a stale token; a larger value is not a durable tenure identity. This profile does not solve counter-reset recovery.

### Counter-history obligations and verification limits

Every contender for one resource must share its election's mark authority. Reads must not fall back to a lagging replica. A read fault/cancellation prevents middleware dispatch and propagates its original failure; it is not converted to a stale-token answer. Consumers discard a pinned tenure token on loss and stop admitting new work; in-flight writes still require resource-side fencing.

| Provider | Counter-history obligation / reset hazard | Store restart/restore evidence |
|---|---|---|
| In-memory | Election and counter must share the same `InMemoryLeaderElectionSharedState` instance. The default is one static process-local instance; separately supplied instances are separate scopes. Process restart loses history by design. | Process-local only; no durable restart guarantee |
| SQL Server | Retain the resource sequence and its authoritative history. DROP, RESTART or restoring older state can regress/reset it. Read the selected writable authority, not a read-only replica. | UNVERIFIED for restart/restore durability configurations |
| PostgreSQL | Retain the resource sequence/history; DROP, RESTART or old backup restoration can reset it. Standby/read-only reads are not a valid authority fallback. | UNVERIFIED for restart/restore durability configurations |
| Redis | Keep the fencing key without expiry/eviction and preserve its history through the chosen persistence/failover policy. Primary-only routing does not detect lost/restored history. | UNVERIFIED for persistence, eviction and failover/reset configurations |
| Consul | Retain the fencing KV key/history and use consistent authority reads. Deletion or old snapshot restoration can reset it. | UNVERIFIED for restart/snapshot-restore history |
| MongoDB | Retain the separate fencing counter collection without TTL/reset and preserve majority-acknowledged history. Old backup restoration can regress it. | UNVERIFIED for restart/restore durability configurations |
| Kubernetes | Retain the Lease transition history. Deleting/recreating the Lease resets its native counter; an API-server restart is distinct from deleting that object. | UNVERIFIED for restart/restore/object-recreation history |

These are falsifiable operator obligations and explicit evidence gaps, not proof that configuration detects or mitigates a reset. No provider supplies a durable tenure identity across history replacement. Existing conformance and local-provider passes are not restart/restore certification.

### The tenure/token invariant, and the shape that enforces it structurally

**A reported tenure carries a usable fencing token whenever fencing is configured.**
`Leadership.FencingToken` is null if and only if the deployment configured no fencing; an implementation
that cannot obtain a usable token does not report a tenure, it relinquishes. That is what lets a
downstream guard admit a null token as a deliberate opt-out rather than treat it as an unknown.

**The invariant must hold on every path that declares a tenure, and acquisition is not the only one.** A
tenure can also be concluded from a store read — a recovery after a write whose response was lost, or a
poll confirming this candidate still holds the claim. Those paths cannot see the acquisition path's
checks and must make the same decision themselves.

**Measured across all seven providers, the discriminator is not the provider — it is whether leadership
is PARAMETERISED or DERIVED:**

| shape | providers | why |
|---|---|---|
| **parameterised** — `BecomeLeader(long fencingToken)` | PostgreSQL, Redis, SQL Server | the token is an argument, so a tenure cannot be declared without one; the only sentinel call site is the explicitly-unconfigured path. Structurally safe, by signature and call-site enumeration |
| declared once, immediately after a successful mint | MongoDB, in-memory | a single declaration site on the acquire path |
| **derived** from stored state | Kubernetes, Consul | every assignment to the state leadership is derived FROM is another declaration of leadership — including ones a background poll makes |

**Both providers that carried the defect were the derived ones, and neither hid on the acquisition path.**
Kubernetes concluded a tenure from a lost-response recovery and adopted whatever counter the store
reported, leaving the no-token sentinel standing when the count was absent. Consul's monitor assigned the
cached holder identity from a key read — and because `IsLeader` is derived from it, that assignment *was*
a declaration, which also raised the became-leader event.

**The Consul route is worth stating in full because no step in it is wrong.** The claim succeeds, the mint
fails, the acquisition path correctly relinquishes, the release is refused — the store answers that the
key was not released, which is a real and handled answer — and moments later the poll truthfully reports
this candidate as the holder. Every step correct, the composition unfenced. A reviewer looking for a
mistaken step will not find one.

**So when adding a provider: prefer passing the token to whatever declares leadership.** Where that is not
possible, enumerate every assignment to the state leadership is derived from and treat each as a tenure
declaration.

## Consumer obligations

- **Leadership is advisory unless you fence the resource you are protecting.** Pin `CurrentLeadership` once per
  tenure, carry its fencing token into every write, and require the downstream store to reject a stale token.
  Checking a boolean "am I leader" before acting is not sufficient: the answer can be stale by the time the
  write lands.
- **Do not use the in-memory provider to protect anything shared.** It has no expiry, and its mutual exclusion
  is process-local — two processes each elect their own leader. It does carry a fencing token (an in-process
  counter — see F1 above), which fences a stale in-process leader against a live one in the same process, but
  that is not a substitute for cross-process protection.
- **Set the grace period below the tolerance of whatever you are protecting.** It is the upper bound on how
  long a candidate that has lost contact may still believe it leads.
- **Prefer idempotent leader-only work.** Every provider's guarantee has an expiry-plus-grace window; idempotent
  work makes that window harmless rather than merely unlikely.

## Evidence (conformance)

**TWO conformance kits bind this contract, and every sentence below names which one it measures.** A
coverage claim that names no kit is the defect this section already cost the project once: the shared base
in `tests/Shared/**/LeaderElectionConformanceTestBase.cs`, derived by all seven providers, and the shipped
`LeaderElectionConformanceTestKit` in `Excalibur.Testing.Conformance`, derived by ten classes. They overlap
on mutual exclusion and takeover and diverge elsewhere — renewal and idempotence are the base's alone — so
**a gap stated over the CONTRACT must be measured against the UNION, never against one kit.** The
consolidation to one kit per contract is a separate, tracked piece of work.

Between them they cover the contract including **both** arms — mutual exclusion under concurrent contention
(safety) *and* acquisition, renewal-over-time, and takeover-after-stop (liveness). The table immediately
below is the **shared base**.

| Guarantee | Conformance arm |
|---|---|
| M1 mutual exclusion | `ConcurrentContention_OnlyOneLeader` — of four concurrent starters, exactly one leads |
| M1 agreement | `ConcurrentContention_AllCandidatesAgreeOnLeader`. **Provider-conditional:** where the claim IS a lock (`sp_getapplock`, `pg_try_advisory_lock`) the lock carries no holder identity, so a follower cannot name the leader and is not asked to. Those providers override this arm to assert what the primitive does guarantee — exactly one leader, and the leader can name itself |
| M2 acquisition | `StartAsync_AcquiresLeadership_WhenNoCompetition` |
| M2 renewal | `Leader_MaintainsLeadership_OverTime`, plus `Leader_DoesNotRaiseLostLeadership_WhileRenewing` as its safety twin |
| M2 transfer | `LeaderChange_NewCandidateBecomesLeader_WhenCurrentLeaderStops`, `LeaderChange_CompetitorReceivesLeaderChangedEvent` |
| Release | `StopAsync_RelinquishesLeadership`, `StopAsync_RaisesLostLeadershipEvent` |
| F1 fencing — **the no-token-issued half, all seven providers** | `FencingTokenProviderConformanceTestKit` — fourteen behavioral cases from nine behavior arms, plus one arm-wiring guard, derived once per real provider: in-memory (no infrastructure, so it runs on every unit pass) and Postgres, SQL Server, Redis, MongoDB, Consul and Kubernetes against real servers. The load-bearing arms are the two the whole provider family lacked: a resource for which no token was ever issued reads back as no token, and refuses **every** token presented against it -- the minimum of the signed 64-bit domain, `-1`, `0`, `1`, `2` and its maximum. Zero and one are the values the shipped inversions admitted; the maximum is there because the failure is an inversion and not an off-by-one, so a mark reported as a low value admits the top of the domain as readily as the bottom. The rest bind strict positivity, monotonicity across handovers, refusal of a superseded token, admission of the pinned incumbent — the liveness arm, without which a fence that refuses everything passes every safety arm while protecting nothing — and distinct tokens under concurrent minting. **There are two never-issued states and only the second detects the catalog-column inversion:** a store holding nothing at all, which the sequence-backed providers answer correctly whichever column they read, and a mark-bearing artifact that **exists and was never advanced**, which `UnadvancedMark_IsReportedAsNoTokenAndRefusesEveryToken` constructs. That arm is gated on a declared property rather than on a silent absence, so which substrates have the state can be counted: Postgres, SQL Server and Kubernetes declare it — a sequence exists before it is drawn from, and a Lease exists carrying a transition count of zero — and for the other four it is unreachable through the provider. **The arm count does not change when that gate flips**, because a gated arm asserts its own declaration and reports a pass; the declared property in source is the only signal that it tests anything, which is the cost of gating an arm on a substrate capability. Each arm is RED-proven against a mutation of the provider it guards, and each mutation reddens only that provider's suite |
| F1 fencing — **SQL Server**, the catalog-column inversion in detail | `SqlServerLeaderElectionFencingShould` — against a real SQL Server: the mint is strictly monotonic across handovers and issues 40 distinct tokens under concurrent minting, a superseded token is refused, a candidate that cannot mint never leads and frees the lock, the high-water mark reads back as the token last issued, and **a sequence that exists but was never drawn reports no token at all, so the fence refuses every token including the value the sequence's own `START WITH` would suggest**. That last arm is the RED detector for a fail-OPEN inversion: the per-resource sequence is created and first drawn in one batch with no enclosing transaction, so an interruption between the two leaves a created, never-drawn sequence — and a high-water read taken from the catalog's `current_value` (declared `NOT NULL`, documented as reporting `START WITH` when the sequence has never been used) answers 1 for that state and admits 1 and every value above it. Reading `last_used_value` (nullable, the last value actually generated) is what makes never-issued expressible |
| F1 relinquish-on-mint-failure — **Consul, MongoDB and in-memory** | `ConsulRelinquishesOnMintFailureShould`, `MongoDbRelinquishesOnMintFailureShould`, `InMemoryLeaderElectionFencingShould` — a mint that fails for a cause **other than** domain exhaustion releases the lock the candidate just took and never declares leadership, which is the half all three previously kept only for exhaustion. The in-memory provider holds a process-local dictionary entry rather than a lease, so it has nothing to time the stall out: a candidate that kept the entry blocked every other in-process candidate for the life of the process. Each binds three arms: the transient failure (the reachable one), exhaustion as a regression, and a liveness arm proving a successful mint leads and does **not** release — without which a provider that released unconditionally would pass the other two while leading nobody |
| F1 exhaustion boundary — **Kubernetes**, acquisition and reader | `KubernetesElectionExhaustionRuleShould` + `KubernetesFencingTokenExhaustionShould` — `leaseTransitions` is a 32-bit counter. The election refuses an acquisition at the maximum because the next transition would overflow, and takes over when one value remains using the ownership CAS. The read-only provider exposes no mint capability and still reports and validates the final authoritative positive mark. The controls retain actor maximum refusal, maximum-minus-one takeover and reader agreement with both positive boundary values; a final mark is readable even though no further acquisition can advance it |
| F1 no-usable-token relinquish — **Kubernetes** | `KubernetesElectionRelinquishesOnUnusableTokenShould` — a Lease this candidate already holds that records no leadership transition yields no strictly-positive token, so the election gives the lease back and declares nothing, rather than reporting a token of zero. Five arms: it does not lead and raises no `BecameLeader` for a count of zero AND an absent count, it clears the holder in both, and a positive count still leads — the last being what stops the fix from being relinquish-always. The cost this closes was liveness, not safety: the provider refuses every token against a zero mark, so a consumer that pinned a zero had every protected operation refused for its whole tenure with nothing at acquisition saying why |
| F1 recovered-tenure token — **Kubernetes** | `KubernetesRecoveredTenureHasAUsableTokenShould` — the lost-race recovery route (a write the API server applied whose response was lost, replayed into a 409, then a re-read naming this candidate as holder) reaches the same question as acquisition by a different path and cannot see it. A recovered count that is absent, or zero, is not a usable token, so the lease is given back and no tenure is reported; a positive count still leads, which is what stops the fix being stand-down-always. Before this, an absent count left the no-token sentinel standing WHILE leadership was reported — and the sentinel renders as a NULL token, which is the documented signal for 'fencing is not configured', so the change-feed checkpoint guard admitted the write unconditionally. That is the fence being BYPASSED rather than un-advanced, and it is the one shape here whose cost is safety rather than liveness |
| Idempotence | `StartAsync_IsIdempotent`, `StopAsync_IsIdempotent`, `StopAsync_BeforeStart_DoesNotThrow` |
| M1 + M2 **through the public registration** | `SqlServerLeaderElectionMutualExclusionEndToEndShould` — two independently composed hosts contend for one resource against real SQL Server, exactly one leads, and the other takes over when it stops. Every kit binding above constructs its candidate directly, which proves the provider behaves when handed its dependencies and cannot observe whether a consumer's own registration yields a working election at all |


**F1's no-token-issued half was bound for one provider and is now bound for all seven.** Every provider already had a fencing test asserting that its mint is monotonic and that a candidate which cannot mint does not lead. None asserted the *other* half of fail-closed — that a resource for which no token was ever issued refuses **every** token rather than some plausible-looking default. That is the half three implementations got wrong independently, and the reason they could is that the test which would have failed existed in no suite their authors could copy from: validation is a high-water comparison of the form `token >= mark`, so a mark rendered as a low real-looking value admits everything, and the state that must admit nothing instead admits all. It is an inversion, not an off-by-one.

`FencingTokenProviderConformanceTestKit` now binds that half once, and **all seven providers derive it**. Kubernetes was the last and needed one thing settled first: its provider reads the Lease's own `leaseTransitions` rather than minting, so a mark is established by advancing the Lease rather than by calling the mint. The mechanism table's fencing column describes each mint; it does not establish that the absent-token state fails closed, which is what these arms do.

**Zero is unset, and that is the contract rather than a local choice.** Every fencing token is strictly positive, so for a provider reading a backing store's native counter an absent counter and a counter at zero are the same state: no transition has been recorded, so no token was ever issued. `KubernetesFencingTokenProvider` implements that — it answers no-token for both and refuses every token against them.

**The election once violated it and no longer does. Stated in the past tense deliberately: a fixed defect left in the present tense is a standing argument against its own fix.** It used to write a real token of zero on the already-holder path, reasoning in a comment that zero was the lesser evil against a sentinel the checkpoint guard would admit unconditionally. That weighed two bad options while a third was already specified: when no strictly-positive token can be issued the contract requires failing closed and relinquishing. Both acquisition and the lost-race recovery path now gate on `KubernetesFencingTokenProvider.IsUsableFencingToken` and give the lease back rather than declaring a tenure, and `KubernetesElectionRelinquishesOnUnusableTokenShould` and `KubernetesRecoveredTenureHasAUsableTokenShould` are the arms that keep it that way — including the liveness arm proving a positive count still leads, without which the fix would be relinquish-always. The cost it carried was liveness on the acquisition path — a consumer handed zero pinned it and had every protected operation refused for its whole tenure, with no error at acquisition — and safety on the recovery path, where the sentinel rendered as a null token, which is the documented signal for *fencing is not configured*.

**The arm that detects this provider's inversion is the unadvanced-mark one, not the two never-issued arms**, and the difference was measured rather than assumed. A fresh resource leaves no Lease at all, and an absent Lease is answered as no-token by the not-found path however the counter is interpreted — so against the two never-issued arms alone, reporting a transition count of zero as a real mark stays **green**. The reachable state is a Lease that *exists* carrying zero, which is its initial state and the state of a Lease at that name created by any other controller. That is what the unadvanced-mark arm constructs, and that is the arm the inversion reddens.

**The in-memory provider carries one additional arm outside the shared kit**, because it is the only
provider whose release paths are not serialised by a backing system:
`InMemoryLeaderElectionStaleReleaseShould` drives two of a leader's own release paths into each other
while a successor acquires between them, and fails if the successor is left believing it leads after its
record has been removed by someone else. **M1 admits no window here** — the exception clause in the
guarantee is bounded by lease expiry, and this provider has none — so any such interleaving is a plain
violation rather than a permitted transient. Its liveness twin asserts that stopping, disposing, and
stepping down still hand leadership on; a release predicate tightened until it never releases would
otherwise satisfy the safety arm while wedging the resource shut forever.

**A second additional arm, `InMemoryLeaderElectionStoppedAcquireShould`, binds the lifecycle half of the
same guarantee**: a candidate holds the resource only while it is running. Without expiry, a resource
taken by a candidate that has already stopped is held forever — its own dispose will not reclaim it,
because dispose stops first and a second stop returns without releasing — so every other candidate fails
permanently. The safety arms race a start against a stop, and a lease-renewal tick against a stop; the
liveness arms assert that a running candidate still acquires, still keeps the resource across renewal
ticks, and can still stand again after stopping, so a guard that released every acquisition would not
pass.

> ### All seven providers run this kit.
>
> **PostgreSQL, Redis, MongoDB, the in-memory provider, SQL Server, Consul and Kubernetes all derive the
> shared conformance base.** SQL Server is the second provider to need the follower-agreement override and
> for the same reason as PostgreSQL — `sp_getapplock` answers the session that attempted it and records no
> owner a third party can read back — so leader *discovery* is UNVERIFIED there by the nature of the
> primitive, while mutual *exclusion* is verified. Consul and Kubernetes bind the kit unchanged.
>
> **⚠ READ THE SCOPE OF THE SENTENCE ABOVE BEFORE QUOTING IT, because its predecessor was quoted without
> scope and cost three allocation decisions.** It is about *this* base, the one derived from
> `tests/Shared/**/LeaderElectionConformanceTestBase.cs`. **It was never the only kit binding mutual
> exclusion.** `Excalibur.Testing.Conformance`'s `LeaderElectionConformanceTestKit` carries
> `MultipleCandidate_OnlyOneBecomesLeader`, `MultipleCandidate_IncumbentShouldExcludeLaterCandidate` and
> `ConcurrentContention_ExactlyOneLeader`, and **ten classes derive it — including Kubernetes, SQL Server
> and Consul, each of which wires all three of those arms.** Measured rather than read: those three suites
> discover **64 arms** and run **64 passed, 0 skipped** against real Consul, SQL Server and k3s. So at the
> moment the
> predecessor sentence read "no arm of this kit enforces mutual exclusion or takeover on those three", that
> was true of *this* base and false of the subsystem. What this base added for those three was renewal and
> idempotence coverage, which the other kit does not carry — real, and not the split-brain gap it was read as.
>
> **MongoDB is the third, and it also binds the kit unchanged** — the lock document carries the holder's
> candidate id, so a follower can name the leader and the follower-agreement arm needs no override.
> Its candidates deliberately SHARE one client, unlike the PostgreSQL deriver's two: takeover is decided
> server-side inside a single `FindOneAndUpdate` whose update is an aggregation pipeline, so arbitration
> is a property of the document and the server's clock rather than of the connection — whereas
> `pg_try_advisory_lock` is session-scoped, and two Postgres candidates on one connection would hold one
> lock and could not contend at all.
>
> **Redis is the second cross-process provider under the kit, and it binds the kit UNCHANGED** — unlike
> PostgreSQL it needs no override of the follower-agreement arm, because Redis stores the candidate id as
> the lock's own value, so a follower genuinely can name the holder. Expiry is the server's `PX` TTL and
> each candidate opens its own multiplexer, so contention is between independent connections against one
> server. Non-vacuity is RED-proven against the trap the deriver documents: giving the candidates separate
> lock keys reddens `ConcurrentContention_OnlyOneLeader` and
> `ConcurrentContention_AllCandidatesAgreeOnLeader` and leaves the other fourteen arms green.
>
> ### ⚠ WHAT THESE DERIVERS ADD IS RENEWAL AND IDEMPOTENCE, NOT MUTUAL EXCLUSION — corrected after the fact
>
> **The sentences above are scoped to THIS kit and that scope is load-bearing. Read as a statement about the
> subsystem they would be false.** A second kit binds mutual exclusion for every provider and already did
> before any of these derivers existed: `LeaderElectionConformanceTestKit`, with eight container-backed
> derivers, 16 arms apiece, wiring `MultipleCandidate_OnlyOneBecomesLeader`,
> `MultipleCandidate_IncumbentShouldExcludeLaterCandidate` AND `ConcurrentContention_ExactlyOneLeader` —
> including for Redis and MongoDB.
>
> **So what the shared base adds, and the only thing it adds, is renewal and idempotence:**
> `Leader_MaintainsLeadership_OverTime`, `Leader_DoesNotRaiseLostLeadership_WhileRenewing`,
> `StartAsync_IsIdempotent`, `StopAsync_IsIdempotent` and `StopAsync_BeforeStart_DoesNotThrow`, none of
> which the other kit carries. That is worth having and it is not split-brain coverage.
>
> **Two measurement errors produced the overclaim and both are worth recognising.** The first was inherited:
> a known-gaps bullet had dropped the "of this kit" qualifier and generalised to the subsystem, so anyone
> allocating against it allocated against a gap that did not exist. The second was an instrument of this
> author's own — a regex enumerating wired arms as `=> Name()` reported the concurrent-contention arm as
> ABSENT for Redis, because that one call is written across two lines. **A pattern that assumes a layout
> returns a confident absence when the layout differs**, which is the same class as the search that found
> the duplication in the first place: it was found by an unrelated grep returning an unexpected directory,
> not by anyone asking whether a second kit existed.
>
> **PostgreSQL is the first *cross-process* provider under the kit, and `M1` holds there against a real
> container**: `ConcurrentContention_OnlyOneLeader` passes, which was the load-bearing unknown — a
> process-local implementation cannot exhibit the failure mode that matters, so the in-memory pass never
> evidenced it.
>
> ⚠ **THIS PARAGRAPH USED TO SAY THERE WERE "REMAINING" PROVIDERS WHOSE `M1` AND `M2` WERE UNVERIFIED, AND
> THAT "no failing test would report it if a future change broke them". BOTH HALVES ARE RETRACTED.** There
> are no remaining providers — all seven derive this base. And the second half was never scoped to this
> base: *no failing test* is a claim over the whole test population, which the ten derivers of
> `LeaderElectionConformanceTestKit` already falsified for every provider it named.
>
> **It is quoted rather than deleted because of HOW it survived.** The correction pass that fixed the
> Known-gaps bullet also walked this passage and updated its *count* from five to three while leaving the
> false assertion beside it intact. It is inside a block quote, which a prose pass reads as structure
> rather than as a claim — the same reason a false statement survives in a table cell or a bullet. After
> correcting a claim, re-enumerate mechanically and read every hit that is not flowing prose.
>
> The mechanism table above still describes mechanisms rather than proving them wired, which is why the
> conformance arms exist. That part always stood.

## Known gaps

- ~~**Three of seven providers are UNVERIFIED against the conformance kit.** This is the most significant gap
  in this subsystem and it is an evidence gap, not a known misbehaviour.~~ **WITHDRAWN, and it was wrong
  before it was closed.** All seven providers now derive the shared base, so the count is stale — but the
  claim was not merely stale, it was **unscoped**. The warning above it said "no arm of *this kit*", and this
  bullet dropped "this kit" and generalised to the subsystem. Mutual exclusion and takeover were bound on
  Kubernetes, SQL Server and Consul the whole time, by the eight classes deriving
  `LeaderElectionConformanceTestKit`. **An unscoped restatement of a scoped claim is how a pessimistic
  record becomes a work item**: three people read this bullet rather than the warning, and allocated
  derivers against a split-brain gap that did not exist. The coverage those derivers added is renewal and
  idempotence, which is worth having and is not what this bullet promised. When restating a scoped claim in
  a summary, carry the scope or do not restate it.
- **A follower cannot always learn who the leader is, and that is a contract-level gap rather than a provider
  bug.** `CurrentLeaderId` is documented as *the current leader's identifier*, but a provider whose acquire
  primitive answers only *"did I get it?"* has no way to satisfy it. PostgreSQL's `pg_try_advisory_lock`
  returns a boolean with **no owner identity**, so the field is populated on the instance that *won* and stays
  null on every follower. **SQL Server has the same limitation** — its `APPLOCK_MODE` probe reports the lock
  *mode*, not the holder.
  **Which providers can answer it, and how — the split is not what the acquire primitive suggests:**
  Kubernetes, MongoDB and Consul store the holder in the lease record, so a follower reads it directly.
  **Redis can also answer it**, despite acquiring through a plain set-if-absent: the candidate id *is* the
  value of the lock key, so on a lost race the follower reads the key and learns the leader. It is
  PostgreSQL and SQL Server — the two whose primitive answers only *"did I get it?"* — that cannot.
  **Consumer consequence: do not use `CurrentLeaderId` to route work to the leader, display which instance
  leads, or drive failover logic.** Treat a null as "unknown", never as "no leader". Use `CurrentLeadership`
  on the instance itself to answer *am I the leader*, which every provider answers correctly, and fence the
  protected resource rather than addressing the leader by identity.
- **The in-memory provider has no lease expiry at all.** A leader that stops responding without disposing
  retains leadership indefinitely within its process. It is a development and testing implementation.
- **Fencing is only as strong as the resource that honours it.** A fencing token that no downstream store
  validates provides nothing; the token is a mechanism for the protected resource to reject a superseded
  writer, not a property of the election on its own.
