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
| **Kubernetes** | `Lease` object replaced under an API-server `resourceVersion` compare-and-swap; a lost race surfaces as a `409 Conflict` | `renewTime` + lease duration, renewed on a timer | **Native and co-atomic** — the token is the Lease's own transition counter, advanced by the *same* write that changes the holder. The provider only reads it | Explicit grace addend on the expiry comparison, and the timestamp is **API-server-stamped**, so the comparison does not trust a local clock |
| **MongoDB** | `FindOneAndUpdate` whose *update* is an aggregation pipeline: takeover eligibility is decided **server-side inside the same document operation**, so there is no read-decide-write window | `expiresAt` field plus a TTL index for cleanup | Present. An in-document token is computed co-atomically, but the **default** provider mints from a separate counter document, because the in-document value is destroyed by release and by TTL expiry | Compared **server-side against the database's own clock**, not a local one, plus a takeover grace covering the incumbent's renewal jitter |
| **SQL Server** | `sp_getapplock` (exclusive, session-owned) on a hardened, non-pooled connection — the lock *is* the claim | Connection-session scoped; verified by reading the app-lock mode back, yielding a three-state answer | Present, minted from a dedicated `SEQUENCE` inside the acquire path before leadership is declared; fail-closed if the mint cannot complete | Strict comparison against elapsed monotonic time. Supports an **accelerate-only** early relinquish on a definitive loss — it can only shorten time-to-relinquish, never extend past the grace period |
| **PostgreSQL** | `pg_try_advisory_lock` on a dedicated, non-pooled session — the advisory lock *is* the claim | Connection-session scoped; verified by re-reading the lock's presence for this backend | Present, minted from a dedicated `SEQUENCE` inside the acquire path before leadership is declared; fail-closed | Strict comparison against elapsed monotonic time. Deliberately has **no** accelerate path, so grace is the sole bound |
| **Redis** | `SET key value NX PX ttl` — the single-shot set-if-absent is the claim | **Server-side TTL** is the real expiry; renewal is an owner-token compare-then-extend script, so a non-owner cannot extend | Present, minted by incrementing a separate counter key **after** the claim succeeds; fail-closed — the just-acquired lock is released if the mint fails | Expiry safety rests entirely on the Redis server's TTL and never on a client clock; the client-side grace only governs how quickly a candidate self-demotes after a renewal fault |
| **Consul** | Session-scoped KV acquire — the Consul server enforces one holder per session | Session TTL, renewed on a timer, plus a lock delay that defers reacquisition after invalidation | Present, minted by a bounded compare-and-swap on a separate counter key after the acquire succeeds; fail-closed | **Delegated entirely to the Consul server** (session TTL and lock delay). See *Known gaps* — the framework-level grace option is not consulted by this provider |
| **InMemory** | First-come-first-served insert into a process-local dictionary | **None** — leadership ends only on stop, dispose, or the unhealthy step-down path | **None.** The tenure's fencing token is always null, by design for a single-process implementation | Not applicable — no lease timestamp exists |

**Fencing co-atomicity is a spectrum, and it matters.** Kubernetes advances its token in the same write that
transfers leadership. SQL Server, PostgreSQL, Consul, Redis, and MongoDB's default path mint the token in a
**separate operation sequenced immediately after** the claim. All of those fail closed — if the mint cannot
complete, the lock is released and leadership is never declared — so a tenure without a valid token is not
observable. But the token is not derived from the same atomic step as the claim, which is a weaker structural
property than the Kubernetes case.

## Consumer obligations

- **Leadership is advisory unless you fence the resource you are protecting.** Pin `CurrentLeadership` once per
  tenure, carry its fencing token into every write, and require the downstream store to reject a stale token.
  Checking a boolean "am I leader" before acting is not sufficient: the answer can be stale by the time the
  write lands.
- **Do not use the in-memory provider to protect anything shared.** It has no expiry and no fencing token, and
  its mutual exclusion is process-local — two processes each elect their own leader.
- **Set the grace period below the tolerance of whatever you are protecting.** It is the upper bound on how
  long a candidate that has lost contact may still believe it leads.
- **Prefer idempotent leader-only work.** Every provider's guarantee has an expiry-plus-grace window; idempotent
  work makes that window harmless rather than merely unlikely.

## Evidence (conformance)

A shared conformance kit exists and covers the contract properly, including **both** arms — mutual exclusion
under concurrent contention (safety) *and* acquisition, renewal-over-time, and takeover-after-stop (liveness).

| Guarantee | Conformance arm |
|---|---|
| M1 mutual exclusion | `ConcurrentContention_OnlyOneLeader` — of four concurrent starters, exactly one leads |
| M1 agreement | `ConcurrentContention_AllCandidatesAgreeOnLeader` |
| M2 acquisition | `StartAsync_AcquiresLeadership_WhenNoCompetition` |
| M2 renewal | `Leader_MaintainsLeadership_OverTime`, plus `Leader_DoesNotRaiseLostLeadership_WhileRenewing` as its safety twin |
| M2 transfer | `LeaderChange_NewCandidateBecomesLeader_WhenCurrentLeaderStops`, `LeaderChange_CompetitorReceivesLeaderChangedEvent` |
| Release | `StopAsync_RelinquishesLeadership`, `StopAsync_RaisesLostLeadershipEvent` |
| Idempotence | `StartAsync_IsIdempotent`, `StopAsync_IsIdempotent`, `StopAsync_BeforeStart_DoesNotThrow` |

> ### ⚠ Two of seven providers run this kit. Five are UNVERIFIED.
>
> **PostgreSQL and the in-memory provider derive the shared conformance base. Kubernetes, MongoDB, SQL Server,
> Redis, and Consul do not** — no arm of this kit enforces mutual exclusion or takeover on those five.
>
> **PostgreSQL is the first *cross-process* provider under the kit, and `M1` holds there against a real
> container**: `ConcurrentContention_OnlyOneLeader` passes, which was the load-bearing unknown — a
> process-local implementation cannot exhibit the failure mode that matters, so the in-memory pass never
> evidenced it.
>
> For the remaining five, the mechanisms are sound by inspection and each rests on a well-understood primitive
> supplied by the backing system. But *by the evidence standard this repository applies to guarantees*, `M1`
> and `M2` are **UNVERIFIED** there: no failing test would report it if a future change broke them.
>
> Do not read the mechanism table above as verification for those five. It describes the mechanism; it does
> not prove the mechanism is still wired.

## Known gaps

- **Five of seven providers are UNVERIFIED against the conformance kit**, as above. This is the most significant
  gap in this subsystem and it is an evidence gap, not a known misbehaviour.
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
- **The Consul provider does not consult the framework-level grace period.** The option is available on the
  shared options type and this provider never reads it; expiry and reacquisition delay are delegated to the
  Consul server's session TTL and lock delay instead. That delegation may well be the correct design — Consul
  owns the session — but the framework-level knob therefore has no effect on this provider, and a consumer
  tuning it would see nothing change. Treat the setting as inert here until the intended behaviour is ruled.
- **The in-memory provider has no lease expiry at all.** A leader that stops responding without disposing
  retains leadership indefinitely within its process. It is a development and testing implementation.
- **Fencing is only as strong as the resource that honours it.** A fencing token that no downstream store
  validates provides nothing; the token is a mechanism for the protected resource to reject a superseded
  writer, not a property of the election on its own.
