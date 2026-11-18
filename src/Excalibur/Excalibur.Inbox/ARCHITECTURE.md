# Architecture — Excalibur.Inbox

> **Guarantee contract for the transactional inbox (deduplication / idempotent receiver).** This document
> is the source of truth for *what processing guarantee the inbox provides and how it is achieved*. It is a
> contributor + integrator reference. Keep it current: any change to a claim / dedup / mark / schema-emission
> path updates this file, verified at architectural review.

## Guarantee

The inbox turns **at-least-once delivery** from a transport into **effectively-once processing** of each
`(MessageId, HandlerType)` — per tenant when multi-tenancy is enabled.

- **At-least-once delivery.** The transport may deliver the same message **more than once** (a consumer that
  crashes after committing but before acknowledging is redelivered). The inbox does **not** make delivery
  exactly-once.
- **Exactly-once *state transition*.** On the transactional path, the handler's side effects and the
  processed-mark commit as **one atomic unit** on a single local transaction — there is no window in which a
  handler's writes are committed but the message is not marked processed, or vice versa. A redelivered
  duplicate is detected by the processed-check and the handler is **not** re-invoked.
- **Duplicate window bounded by the lease, on the lease protocol.** A processor that acquires a lease and
  then crashes before marking it holds the term only until its lease/visibility window expires; after that
  the message is re-claimable. The window is **bounded by the configured lease**, not eliminated. On the
  claim protocol there is no lease and no such window: the claim never auto-expires, the caller governs its
  lifetime, and an abandoned claim is stranded rather than re-admitted (see Known gaps).
- **Tenant isolation (opt-in).** With multi-tenancy enabled, the claim/dedup key carries the resolved
  `TenantId`, so two tenants presenting the same `(MessageId, HandlerType)` never deduplicate against each
  other. With multi-tenancy disabled the relational providers emit the bare pair and no tenant
  discriminator. Document providers, whose entry is addressed by a single id rather than by a composite
  column key, always carry a tenant term in that id — the reserved untenanted sentinel when multi-tenancy
  is off — so the id shape does not vary by deployment mode.

> **Consumer obligation:** message handlers **MUST be idempotent.** At-least-once delivery means a handler
> can observe the same message more than once across a crash; the inbox's dedup makes the *committed effect*
> once, but a handler must not assume it is invoked exactly once.

Sub-guarantees (invariants):

> **WHICH CONTROL CLOSES WHICH FAILURE**, because J1a has two independent failure modes and they are not
> fixed in the same place:
>
> | failure | mechanism | closed by |
> |---|---|---|
> | J1a, source-switching | the resolver reaching a different source on different paths — the reflective arm versus the assigned context id | removing the reflective arm |
> | J1a, per-delivery assignment | a caller assigning a freshly fabricated identity to the context before the resolver runs | the authoring sites that fabricate one; this is the **silent** mode |
> | J1b | nothing resolves an identity | refusing the message makes it **visible**; validating at startup makes it **early** |
>
> Only the first is a property of the resolver. The others are upstream of it, which is why a guard inside
> the resolver cannot close them and why the resolution ORDER rather than a guard carries J1a today.

> **D1 IS CONDITIONAL ON J, AND THAT CONDITION WAS NOT WRITTEN DOWN UNTIL NOW.** Without J1a, D1 can be
> satisfied by two concurrent invocations under two different keys. Without J1b it is
> vacuously true and operationally empty — at most one entry commits per key, and every delivery gets its
> own key, so no duplicate is ever detected and the handler runs every time. Without J2, D1 suppresses
> messages that were never duplicates. Both failures are **silent**: the dispatch reports success and
> nothing downstream learns, which is this subsystem's catastrophic class by its own discriminator.
>
> This document previously argued the correctness of a key whose definition appeared nowhere in it. The
> gap was found by an independent concurrency review, which stated it as prior to every other finding in
> the cluster, and it is the reason a resolver defect survived a specification this detailed: no row
> required the property, so no arm checked it. The resolver had **zero** arms.


| # | Property | Statement |
|---|---|---|
| **J1a** | Key AGREEMENT across deliveries *(safety)* | For any two deliveries `d1`, `d2` of the same logical message to the same handler, **no two resolve DIFFERENT keys** — where `key(d) = (MessageId(d), HandlerType(d)[, TenantId(d)])`. Violating it grants two leases for one message and runs two handler invocations concurrently, which voids D1 and S1 together. **The identity must be a property of the MESSAGE, never of the DELIVERY.** Enforced at `InboxMiddleware.GetMessageId`, whose resolution order is ranked by stability across a redelivery rather than by cost: a payload-borne identity (`IDomainEvent.EventId`, typed) first, then the wire envelope's id, then an id a middleware placed in `Items`, then an id ASSIGNED to the context, and only then a `MessageId` property by convention. **A context identifier the context minted for itself is refused**, because `MessageContext.MessageId` mints a GUID in its own getter when nobody assigned one — and a context is constructed per dispatch, so a minted value names this delivery. **The minted-value check is NECESSARY AND NOT SUFFICIENT, and the ordering is what carries the property:** "somebody assigned this" is a fact about the WRITE, while J1 needs a fact about the VALUE, and three reachable public paths assign a per-delivery value — `InMemoryTransportAdapter`, `CronTimerTransportAdapter`, and `MessageMetadata.CreateDefault()` composed with `ApplyToContext()`. A payload identity therefore outranks the context entirely. **Known gap:** a message carrying no identity of its own, received through one of those three paths, still resolves a per-delivery key; it is not refused, so J1 does not hold for it. Closing that requires refusing a message the inbox cannot key, which is tracked. `IMessageIdentityFeature.ExternalId` is **not** a substitute, and the reason is that it carries **two meanings**: its contract documents it as a correlation key for external systems, while the retry drain writes a per-message identity into it. The value carries no provenance, so a reader cannot tell which writer wrote it — and keying on it would therefore violate J2 the way a correlation id does, on the writes that mean a business key. *(Superseded wording, corrected because it was false on a world-visible surface: "nothing writes a per-message value into it". One writer does.)* |
| **J1b** | Key AVAILABILITY *(liveness)* | Every delivery the inbox is asked to deduplicate resolves a key at all. **Not currently held for every message**, and the failure is the quiet one: a message that resolves no identity is processed with no deduplication, so a deployment configured for effectively-once silently gets at-least-once for that message type and nothing in its configuration says so. J1a and J1b are stated separately because they FAIL DIFFERENTLY AND ARE CLOSED BY DIFFERENT CONTROLS, and a single "key stability" row conflated them — which is this document's own safety-paired-with-liveness discipline, missing from the row that exists to enforce it. A resolver that resolves NOTHING satisfies J1a perfectly. |
| **J1c** | The key's SECOND term is load-bearing | `HandlerType` is part of the key, so it owes the same agreement property as the message id: two deliveries of one message to one handler must derive the same handler-type string. Held today **by convention, stated at the seam**: the middleware derives it as the message type's fully-qualified name and stores that same form, so an entry it created, the value the drain carries into its claim, and the comparison that admits the drain's own claim all agree by construction, and no writer was found that stores a resolved handler's FQN instead. Written down because the convention is not enforced and the divergence IS expressible — `IHandlerRegistryEntry` exposes `MessageType` and `HandlerType` as distinct `Type`s — so a future entry writer has nothing telling them the term decides a key. If the two strings ever disagree, the drain's own claim is unrecognised, the middleware treats the drain as a competitor, and the dispatch reports a suppressed duplicate while the handler never runs. |
| **J2** | Key separation across messages | For distinct logical messages `m != m'` delivered to the same handler, `key(m, h) != key(m', h)`. A correlation id is therefore never an identity: it names a CONVERSATION, so keying on it collapses every message in one conversation to a single entry and suppresses all but the first as duplicates. The `CorrelationId` arm that the resolver used to fall back to is removed. K1 (below) is the encoder-side half of this property; J2 is the resolver-side half, and K1 presupposed it. |
| **D1** | Single committed processing | For a given `(MessageId, HandlerType[, TenantId])`, at most one transaction ever commits the "processed" state transition. Concurrent duplicates serialize; the loser observes "already processed" and skips the handler. |
| **D1a** | Processed is absorbing | Once a `(MessageId, HandlerType[, TenantId])` has reached `Processed`, no store write moves it out of that state. Every write that would change the status of a processed entry — the failure mark and the in-flight processing mark alike — refuses the transition and leaves the entry as it found it, rather than reporting an error. This is what D1 rests on: a demoted entry is re-admittable, so a single overwriting write turns effectively-once processing back into repeated processing with no duplicate visible to any caller. |
| **D2** | Atomic handler+mark | On the transactional path the handler's writes and the processed-mark commit or roll back together — never one without the other — provided the handler enlists its writes through the supplied transaction/scope. |
| **L1** | Lease-bounded reclaim | A claimed-but-unprocessed message is not re-claimable within its lease, and **is** re-claimable after it. A crashed processor cannot strand a message forever (subject to the non-transactional tombstone gap below). |
| **R1** | Claim-path retry by removal | On the claim protocol, `TryClaim` is refused whenever an entry exists for the key — `Processing`, `Processed` or `Failed` alike — and retry is reached by `Release`, which removes the non-terminal row so the next redelivery claims it again. A `Failed` entry is owned by the estate-wide retry drain and is not re-claimable: the protocol carries no term, so a redelivery admitted alongside the drain could not be fenced against it and the handler would run twice. This differs from the lease protocol, where acquisition does re-admit a `Failed` entry because recording the failure clears the term. |
| **T1** | Tenant-keyed dedup | When multi-tenant, the lock-check, the conflict/merge key, and the synthesized insert all carry the resolved `TenantId`; two tenants sharing a message id can never collide. |
| **T2** | Estate-wide retry drain | `IInboxStoreAdmin.GetAllTenantsFailedEntriesAsync` applies **no** tenant term on any provider, by contract rather than by omission. The retry sweeper has no tenant of its own, so a tenant-confined variant would drain only whichever partition happened to be ambient and let every other tenant's failed entries accumulate unretried. The caller re-establishes each entry's own tenant from `InboxEntry.TenantId` before acting on it. |
| **S1** | Single in-flight invocation (retry drain) | For a given `(MessageId, HandlerType[, TenantId])`, at most one handler invocation is in flight at any instant — across concurrent drains, across a redelivery racing a drain, and across hosts. The drain is a **second, independent processor of the same rows** as the receive path, so this is not implied by the receive path's D1: two drains reading the same `Failed` entry would each dispatch it, and both marks would be idempotent, so nothing would report the duplicate. **Held only where the store declares the lease protocol** — the drain's read takes no term, so the store's atomic lease acquisition is the entire fence. On a store without that protocol S1 does **not** hold and the single-instance consumer obligation applies. |
| **S2** | Exactly one committed finalize (retry drain) | Every successful handler invocation performed by the drain is followed by **exactly one** committed `Processing → Processed` transition, recorded under that entry's own `(MessageId, HandlerType)` **and under that entry's own tenant**. Both halves are load-bearing and fail differently: a finalize under the wrong key addresses no row, and a finalize under the wrong tenant — including a *cleared* ambient, which means "no tenant was established" rather than "this row has no tenant" — is refused by a multi-tenant store after the handler has already run. **And the converse, which this row asserted only in one direction: a committed finalize implies an invocation.** The drain finalizes only where the deduplication stage reports that it ADMITTED the dispatch. A stage that refused - because a live lease on the entry is held elsewhere - leaves both the finalize and the deduplication record to the holder of that lease. Stating the guarantee one-way is what allowed a refusal to be recorded as a completion: a refusal and a completion are indistinguishable in the dispatch result, and because the entry's external identifier was ALSO recorded as deduplicated, a later redelivery of the same message was finalized without reaching a handler either - making the message unrecoverable by retry rather than merely lost once. |
| **L2** | Bounded attempts to terminal (retry drain) | Every `Failed` entry reaches a terminal state — `Processed`, or dead-lettered — within a bounded number of attempts. Each drain of an entry must consume exactly one attempt (a transient short-circuit that never reached the handler consumes none), and an entry that is re-admitted without its attempt count advancing is unbounded, not merely slow. |
| **K1** | Injective dedup key | On a provider that addresses an entry by one composed id rather than a composite column key, distinct `(TenantId, MessageId, HandlerType)` triples never render the same id. Two halves, established differently. Against the **separator** it is structural: each term is percent-encoded, and the encoder's output alphabet excludes the separator, so it cannot occur inside a term and the id is decodable. Against the **encoder** it is guarded: the percent-encoder is not injective over all strings — it folds every unpaired surrogate onto one replacement character — so terms that are not well-formed text are refused rather than admitted. A term set whose composed id exceeds the provider's id limit is likewise refused, never truncated, since a truncated key is one two messages could share. A join that is merely *unlikely* to collide is not sufficient: a collision here is a silently dropped message on the success path, not an error. |
| **H1** | Fail-closed mode↔schema handshake | A store whose deployment mode disagrees with its physical schema (multi-tenant store on the pair schema, or single-tenant store on the triple schema) fails fast at startup rather than running a predicate-less or malformed query. |
| **H2** | Fail-closed context↔mode coupling | Registering a resolving tenant context while the deployment stays in single-tenant mode is rejected at startup, closing the silent cross-tenant-loss configuration. |
| **S3** | A terminal mark implies an invocation *(safety, both paths)* | An entry reaches terminal `Processed`, and its identifier is recorded as deduplicated, only where a handler invocation for that entry committed. S2 states this for the retry drain; S3 states it for the composition, because the drain does not invoke a handler directly — it re-dispatches through the pipeline and the deduplication stage decides whether the handler runs. **The two cannot be stated as one row, because the refusal and the completion are indistinguishable in the dispatch result**: both surface as a succeeded dispatch, by design, so the only thing carrying the distinction is what the stage reports on the context. The mechanism is therefore a *reported* disposition rather than an inferred one, and the direction it fails in is the property: the stage marks REFUSED on entry to its deduplication decision and upgrades to ADMITTED only beside an actual handler invocation, so an exit that reports nothing reads as a refusal and costs a redelivery against an idempotent handler — loud and recoverable — rather than a silently swallowed message. **Known gap, and it is an obligation on a public extension point rather than a defect in this subsystem:** a middleware ordered BEFORE the deduplication stage that returns a succeeded result WITHOUT invoking the next delegate leaves no disposition at all, and the drain cannot distinguish that from a pipeline in which this stage is simply absent — where the handler did run. No first-party middleware does this. **A middleware ordered before the inbox stage MUST NOT return a succeeded result without invoking the next delegate.** |
| **L3** | Eventually HANDLED, not merely eventually terminal *(liveness)* | A message admitted to the inbox is eventually handled, not merely eventually brought to a terminal state. L2 bounds attempts to terminality and is stated in exactly those terms, so **L2 is satisfied in full by a subsystem that dead-letters everything on its first attempt** — safe, bounded, and useless. L3 is the arm of the pair that such a subsystem fails. It is the liveness partner of S3 in the same way J1b is of J1a, and for the same reason: a one-directional statement of a delivery guarantee is satisfied by doing nothing, and this document's own discipline is that every safety row owes a liveness row that inaction fails. **Not held unconditionally, and the conditions are the consumer's:** a handler that always throws, a dead-letter ceiling reached, or a message whose identity the inbox cannot key (J1b) each end in terminality without a successful invocation. What L3 asserts is that none of those outcomes is produced by this subsystem's own mechanism — a refusal, a lapsed lease, a reclaim, or a disposition the drain could not read must never be the reason a message stops being retried. |

## What the guaranteed operation TRAVERSES

A guarantee contract that names only where an operation starts and ends leaves the composition in the
middle unspecified, and on this subsystem the composition is where the defects have been. Stated
explicitly, once:

**The retry drain does not invoke a handler. It re-dispatches through the full pipeline**, which means
it re-enters the deduplication stage that it is itself coordinating with. So one drain of one entry
traverses: the drain's own lease acquisition on the entry; the dispatcher; every middleware ordered
before the deduplication stage; the stage, which takes its own deduplication decision and may refuse
the very caller that holds the entry's lease; the handler, if the stage admits it; and then the drain's
own finalize. **Each of those is a place the entry's identity must agree with itself**, which is why
J1a, J1c and S3 exist as separate rows rather than as consequences of D1.

**Where a component re-enters a pipeline it is part of, that re-entry is a named property or it is
unspecified.** Here it is named: the drain presents the term it already holds, the stage recognises it
and admits the holder rather than treating it as a competitor, and the stage reports back what it did.

### Axes this document does NOT cover

Stated so that silence is not read as a guarantee:

- **Ordering.** Nothing here promises messages are handled in the order they were received, within a
  key or across keys. The drain selects by retry eligibility, not by arrival.
- **Latency.** No bound is given on the interval between admission and handling, nor on drain frequency.
- **Handler semantics.** Whether a handler's own effects are idempotent, transactional, or reversible is
  the consumer's property, not this subsystem's — and D2's atomicity holds only for writes the handler
  enlists through the supplied scope.
- **Cross-subsystem exactly-once.** The inbox deduplicates DELIVERIES to a handler. It says nothing about
  effects that handler produces in other subsystems, including the outbox.
- **Poison-message policy.** What a dead-lettered entry means, who drains it, and whether it is ever
  replayed are the dead-letter subsystem's concerns.
- **Clock agreement between hosts.** One path still compares a local clock against a timestamp written by
  another host; see the non-leased fallback below.

## How it is achieved (the seam)

1. **Deduplicated processing (D1·D2)** — `IScopedTransactionalInboxStore.TryProcessTransactionallyAsync`
   (per provider, e.g. `SqlServerInboxStore` / `PostgresInboxStore`) runs the claim-check, the handler, and
   the processed-mark on **one connection and one local transaction** (no distributed coordinator). The
   claim-check takes a key/range lock (`UPDLOCK, HOLDLOCK` on SQL Server; `FOR UPDATE` on Postgres/Oracle) so
   a concurrent processor of the same key blocks until the first commits or rolls back, then observes the
   committed "processed" status and skips. Non-transactional receivers use `TryClaim` + `TryMarkAsProcessed`,
   where the processed-mark is the idempotency point.
2. **Tenant-keyed emission (T1)** — the tenant discriminator is emitted from the **actual physical column**
   the store detected at startup (`_hasTenantColumn`), never from the mode flag. The store can neither name a
   tenant column that is absent nor omit one that is present. `TenantScope.FromContext` yields the resolved
   tenant in multi-tenant mode, `None` (no predicate, no fragments) in single-tenant mode, and **fails
   closed** when multi-tenant is active but no tenant is resolved — it never reaches a predicate-less query.
3. **Estate-wide retry drain (T2)** — the scope is stated in the operation's name, matching the three
   sibling admin operations that are also estate-wide (`GetAllTenantsEntriesAsync`,
   `GetAllTenantsStatisticsAsync`, `CleanupAllTenantsProcessedEntriesAsync`). The conformance kit's
   `GetAllTenantsFailedEntriesAsync_MustReturnEveryTenantsFailedEntries` arm stages a failed entry under two
   tenants and reads with one of them ambient: an implementation that scoped this read by ambient tenant
   returns only one and fails. `InboxProcessor` re-enters each drained entry's own tenant scope before
   dispatching it, so a cross-tenant read never becomes a cross-tenant dispatch. Where it finalizes at all
   it does so under **that entry's own `(MessageId, HandlerType)`**, carried through from the drained
   `InboxEntry` rather than re-derived from the message handed to the pipeline: that message carries the
   *message* type (the short type name, not the handler type's fully qualified name) and, on the envelope
   payload path, an id read back out of the payload. Either substitution addresses a row that does not
   exist, so the drain would neither finalize a succeeded entry nor record an attempt against a failed
   one, and the entry would be re-admitted and its handler re-run on every subsequent drain.

   The drain has **one** consumer path at every parallel-processing degree; a degree of `1` is the
   degree-1 case of the batch path, not a separate algorithm. A second, separately written sequential
   branch — reachable by default, and recording no outcome of any kind — is what previously left S2 and
   L2 unheld, and two implementations of one invariant is why they drifted. **S2 and L2 hold for the
   drain. S1 holds only on a store that declares the lease protocol** — see item 8 below. The
   receive-path guarantees (D1, D1a, D2, T1) are unaffected — they are enforced inside the store and the
   conformance kit fails a provider that breaks them.
4. **Mode↔schema handshake (H1)** — `InboxSchemaContract.Verify`
   (`Excalibur.Inbox/DependencyInjection/InboxSchemaContract.cs`) centralizes the four-combination check as
   pure logic; `InboxSchemaValidationHostedService` runs it at startup so a mismatch fails before the first
   message. The per-store `EnsureSchema` check is the host-less floor.
5. **Context↔mode coupling (H2)** — `TenantContextConsistencyValidator`
   (`Excalibur.Dispatch.Abstractions/ContextValues`, wired by `AddDefaultTenantContext` via
   `IValidateOptions<TenantContextOptions>` + `ValidateOnStart`) rejects a resolving `ITenantContext`
   registered while `RequireTenant` is false — the configuration that would apply the single-tenant schema yet
   route multiple tenants through the same keyed rows. Unlike the H1 mode↔schema handshake — which also has a
   per-store first-use floor (`EnsureSchema`) — this guard is **startup-only** via `ValidateOnStart` and has
   **no** per-store floor, so it does not fire in host-less wiring (see Known gaps).
6. **Lease reclaim (L1)** — `TryAcquireLease` stamps a lease; acquisition readmits a failed entry for
   retry, and a crashed processor's claim is reclaimable once the lease elapses. Both properties belong to
   the lease protocol (`ILeasedInboxStore`): `TryClaim` stamps no lease and readmits nothing.
7. **Claim-path retry (R1)** — the claim protocol reaches retry by **removing the row**, not by readmitting
   a status. `TryClaim` inserts only when no entry exists for the key, so it is refused for a `Processing`,
   `Processed` **or `Failed`** entry alike; `Release` deletes the non-terminal row, and the next redelivery
   finds none and claims it again. A `Failed` entry on this path is owned by the estate-wide retry drain
   (T2) and is deliberately not re-claimable: this protocol carries no term, so nothing could fence a
   redelivery against the drain dispatching the same entry, and admitting it would run the handler twice.
8. **Drain single-in-flight (S1)** — the drain's read (`InboxProcessor.ReadRetryableEntriesAsync`) is a
   plain query: it writes nothing and takes no term, so two processors issuing it hold the same rows. The
   fence is applied per entry, immediately before dispatch, by `ILeasedInboxStore.TryAcquireLeaseAsync` —
   one atomic compare-and-set inside the store, decided against the store's own clock. Exactly one caller
   moves the entry to `Processing` and receives a term; every other caller receives `null` and skips the
   entry without dispatching it. The success path then finalizes through the fenced `CompleteAsync`, which
   the store applies only while that term is still current; a caller whose term lapsed does **not** fall
   back to an unfenced mark, because a lapsed term means another processor legitimately reclaimed the entry.

   Two consequences are worth stating because they are easy to assume away. **A registration lifetime is
   not a fence** — making the processor a singleton bounds one container, while this race crosses hosts,
   processes and serverless invocations. And **the fence alone would break liveness**: it parks the entry
   in `Processing`, so a drain that dies mid-handler would strand it wherever the drain's read admits only
   `Failed`. That is why a store declaring the lease protocol is *required* to return expired-lease
   `Processing` entries from `GetAllTenantsFailedEntriesAsync` as well as `Failed` ones. The two halves are
   a pair: the acquisition supplies S1, the read admission preserves L2, and shipping either alone trades
   one property for the other. A store that does not declare the lease protocol has neither half, keeps the
   `Failed`-only read, and is covered by the single-instance consumer obligation below.

## Consumer obligations

- **A domain event's `EventId` MUST be the same value on every delivery of that event, and two ways of
  writing one break that.** It is the identity the inbox deduplicates on (Invariant J1a), and
  `DomainEvent.EventId` is declared `public virtual string { get; init; }` with a fresh-UUID initializer.
  So: **(a)** the initializer fires whenever the deserialized payload omits the field, which means a
  foreign producer that does not emit it — or a serializer configured not to — mints a new identity on
  every delivery and no duplicate is ever detected; and **(b)** overriding `EventId` as a *computed*
  property returns a value that is never deserialized, which is the idiom the base class teaches three
  lines further down, where `CorrelationId` and `CausationId` are both `virtual` and derived from
  `Metadata`. An `EventId` written in that style has no setter, is recomputed per instance, and defaults
  to null when `Metadata` is absent. Ensure the field is emitted and round-trips, and keep `EventId` a
  stored value rather than a derived one.
- **Handlers MUST be idempotent** (at-least-once delivery ⇒ a handler can see a message more than once across
  a crash/redelivery).
- **Set the lease / visibility timeout greater than the maximum expected processing duration.** If a
  processor pauses mid-processing long enough for its lease to expire, a redelivery can be re-claimed while the
  first attempt is still in flight; a lease longer than the worst-case processing time keeps that window closed
  to normal operation.
- **For multi-tenant deployments, enable multi-tenancy through the supported composition** (`AddMultiTenancy`,
  which sets required-tenant mode and the triple key). Registering a custom resolving tenant context without it
  is rejected at startup (H2), not silently accepted.
- **On the non-transactional `TryClaim` path, finalize or release every claimed message.** Mark it processed
  on success and **release** it on failure — release removes the row so a redelivery is re-claimed. Recording
  a failure instead hands the entry to the retry drain and makes it un-claimable by a redelivery, which is
  correct only when the drain is the intended retry path. A processor that claims and then crashes without
  doing either leaves a Processing row that nothing reclaims, because this protocol has no lease (see Known
  gaps). On the lease protocol the equivalent stall is bounded by the lease.
- **In host-less / serverless wiring, a consumer MUST ensure `RequireTenant` matches their `ITenantContext`
  registration** (a resolving context requires multi-tenant mode) — the startup consistency guard (H2) does
  not fire without an `IHost`.
- **On a store that does NOT declare the lease protocol, run the retry drain single-instance.** The drain's
  read takes no ownership term, and on such a store nothing else does either, so two drains running
  concurrently — a second host, a second process, a second serverless invocation — will both dispatch the
  same failed entry and **both will run its handler**. Neither reports a conflict, because both of their
  marks are idempotent, so the duplicate is invisible to every caller and to every metric. **The duplicate
  window is unbounded**: it is not narrowed by a lease or a timeout, and it recurs on every drain pass for
  as long as the entry stays retryable. Confining the drain to one instance is the only mitigation, and
  handlers must be idempotent regardless. On a store that declares the lease protocol this obligation does
  not apply — the store's atomic lease acquisition fences the drain across hosts (S1).
- **Do not raise a store's lease duration above the drain's batch-processing timeout.** The drain takes its
  lease for exactly that timeout, which is also the longest it will hold an entry, so the term cannot lapse
  under a handler that is still running. Lengthening the lease without lengthening the timeout only delays
  recovery from a drain that died.

## Evidence (conformance)

Each guarantee is bound to a **real-infrastructure** test (a live container; the arms are not skipped when
infrastructure is present) that RED-detects a violation. Guarantee → test:

| Guarantee | Conformance test |
|---|---|
| D0 durability fault model — a persistence failure throws, never a silent no-op | `InboxStoreConformanceTestBase.ThrowNotNoOpOnPersistenceFailure` (safety: with a real, provider-side fault injected — never a mocked client — `CreateEntryAsync` throws AND a fresh store instance reads back nothing durable; liveness: with the fault removed, the identical call succeeds and IS durably readable from a second fresh instance, so a store that simply throws on everything cannot pass). Wired against real infra for SqlServer and Oracle (renames the backing table out from under the store), Postgres (same), MongoDB (a document validator no write can satisfy), Redis (`ACL … -@write`/`+@write`), DynamoDb (deletes and, on repair, recreates the table through the store's own auto-create path), and ElasticSearch (`index.blocks.write`). Verified RED, not just asserted: MongoDB, Redis, and ElasticSearch were each independently confirmed to fail this arm against a deliberately catch-and-swallow variant of `CreateEntryAsync` before being accepted. Firestore now exercises the arm through a test-owned severable TCP route to the real emulator: existing data survives the fault, a fresh direct client verifies no partial write, and the same write succeeds after restoration. Its 35 conformance arms passed locally with no skips, including under the nightly required filter (2026-10-12). The faulted Commit has a five-second RPC deadline and one attempt; streaming-read deadlines apply per attempt. The missing-hook RED was executed before this repair; a Firestore catch-and-swallow mutation has not been verified. **UNVERIFIED on CosmosDb** (exercised through a separate harness this arm does not reach at all) |
| D1 single committed processing / concurrent duplicates | `SqlServerTransactionalInboxExactlyOnceShould.TransactionalInbox_ConcurrentDuplicates_ProcessExactlyOnce` (+ Postgres, Cosmos equivalents) |
| D1a Processed is absorbing under the processing mark | `InboxStoreConformanceTestKit.ProcessedEntry_MustNotBeDemotedByTheProcessingMark`, bound by every provider's kit suite (SqlServer, Postgres, Oracle, CosmosDb, MongoDB, Elasticsearch, DynamoDB, Firestore, Redis, InMemory) — finalizes an entry, issues the durable processing mark over it, and reads the status back. A store whose mark writes over `Processed` fails; a store that does not implement the mark reports the arm as unverified rather than passing it. The claim-path half of the same property is `ProcessedEntry_MustNotBeReadmittedByTheClaimPath`; the two fail under different mutations |
| D2 atomic handler+mark across a crash | `…TransactionalInboxExactlyOnceShould.TransactionalInbox_CrashMidProcessing_RedeliversWithoutDuplicatingCommittedEffect` (SqlServer + Postgres) |
| T1 tenant-keyed dedup / isolation | `SqlServerInboxStoreDeploymentModeShould.Isolate_Rows_By_Tenant_On_The_Triple_Schema` + `Dedup_A_Genuine_Duplicate_Within_A_Single_Tenant_On_The_Triple_Schema` (+ Postgres `Isolate_Claims_By_Tenant_On_The_Triple_Schema`) |
| J1a key agreement / J2 key separation (J1b is UNVERIFIED and owed — see the control partition above) | `ADeduplicationKeyIsAPropertyOfTheMessageShould` — seven arms observing the identifier the middleware actually asks the store about, against a **real `MessageContext`** rather than a fake, which is load-bearing: a faked context has no minting behaviour, so a suite built on fakes cannot reach the defect. Safety (a minted identifier never becomes the key; a correlation id never becomes the key; two distinct events sharing a conversation get distinct keys) paired with liveness (an identity assigned by the wire deserializer IS used when the message carries none, so the arms cannot be satisfied by a resolver that resolves nothing). RED-proven by four mutations of the resolver: the original order restored, the typed identity arm removed, the correlation fallback restored, and the minted-value guard removed. **Not real-infrastructure, and it does not need to be** — the property under test is which identifier the resolver chooses, which no store participates in; the stores' own behaviour on a stable key is covered by the rows above. |
| K1 injective dedup key | `ElasticsearchInboxDocumentIdShould` + `FirestoreInboxDocumentIdShould` — safety (triples the previous join merged now render distinct ids; every id decodes back to its exact terms) paired with liveness (one triple renders one id; an ordinary triple stays legible), so neither a colliding join nor a per-call-unique id passes |
| Non-MT default deployment (liveness) | `SqlServerInboxStoreDeploymentModeShould.Claim_And_Dedup_A_Non_Multi_Tenant_Message_On_The_Pair_Schema` + `Read_Back_A_Non_Multi_Tenant_Entry_On_The_Pair_Schema` |
| H1 fail-closed mode↔schema | `SqlServerInboxStoreDeploymentModeShould.Fail_Closed_When_A_Multi_Tenant_Store_Runs_The_Pair_Schema` + `Fail_Closed_When_A_Non_Multi_Tenant_Store_Runs_The_Triple_Schema` (+ Postgres) |
| H2 fail-closed context↔mode | `TenantContextConsistencyGuardShould` (unit; safety arm throws on resolver+single-tenant, liveness arm starts clean on the default and on required-tenant mode) |
| Fail-closed on unresolved tenant | `PostgresInboxStoreFailsClosedOnNullAmbientTenantShould.FailClosed_BeforeTouchingSql_WhenMultiTenantAndAmbientTenantIsNull` |
| L1 lease reclaim / retry readmit | `SqlServerInboxStoreLeaseReclaimShould.Reclaim_the_message_after_the_lease_expires` + `Readmit_and_retry_a_failed_entry_on_redelivery` (+ Postgres) |
| T2 drain finalizes under the entry's own key | `InboxProcessorFinalizeKeyShould` — a namespaced entry is staged under the key both production writers use (the handler type fully qualified, the message type short), drained, and its persisted status / attempt state read back on the success, retry and dead-letter paths. A drain that finalizes with the message type addresses no row and fails all three. The positive-control arm asserts the probe type is namespaced, since a global-namespace probe has `FullName == Name` and would pass every arm vacuously |
| S1 single in-flight invocation (retry drain) | Two halves, bound separately, because they fail under different mutations. **The drain asks for the term:** `InboxProcessorDrainFenceShould.DispatchAnEntryOnlyOnce_WhenTwoProcessorsDrainTheSameFailedEntry` — two processors over one real store and one failed entry, with a counting handler; a drain that omits the acquisition dispatches twice and fails. Its positive control `.DispatchTheEntry_WhenASingleProcessorDrainsIt` asserts one processor still dispatches, so a drain that dispatches nothing cannot pass the safety arm vacuously. **The store refuses the second asker:** `InboxStoreConformanceTestKit.LiveLease_MustNotBeReclaimableByAnotherProcessor`, bound by every provider's kit suite. **Not held, and deliberately untested, on a store that declares no lease protocol** — there is no fence there to assert; the single-instance consumer obligation is the mitigation |
| L2 liveness under the fence — the drain's read admits an abandoned lease | `InboxStoreConformanceTestKit.ExpiredLease_MustBeReadmittedByTheRetryDrainRead` (liveness — an entry left `Processing` under an expired lease is returned by the drain's read, so a dead processor's entry is retried rather than stranded) paired with `.LiveLease_MustNotBeReadmittedByTheRetryDrainRead` (safety — an entry under a live lease is **not** offered for retry). The pair is the point, and this is the arm the S1 fence depends on: a read that admits only `Failed` strands every entry the fence parks in `Processing`, and a read that admits any `Processing` entry hands the drain work a healthy processor is still doing. Loosen the predicate and the safety arm fails; tighten it and the liveness arm does |
| S2 exactly one committed finalize, under the entry's own key AND its own tenant | The two halves fail under different mutations and are bound separately. **Key half:** `InboxProcessorFinalizeKeyShould` (the T2 row above). **Tenant half:** `InboxProcessorDrainInvariantsShould.FinalizeUnderTheEntrysOwnTenant_WhenDrainedOnAMultiTenantHost` paired with `.FinalizeUnderTheUntenantedTerm_WhenTheEntryBelongsToNoTenant` — the fake store resolves each mark's partition through `TenantScope.FromContext`, the conversion a real provider routes every statement through, so a mark committed after the per-entry scope is disposed is observed failing closed rather than judged by inspecting a string. The pair is the point: binding the entry's tenant only when one is present passes the first arm and fails the second. **Converse half (a finalize implies an invocation): UNVERIFIED.** The arm must run against a store that leases but does not scope its claim to a transaction - a leased-and-transactional store takes a different branch, so a green there proves nothing - and must assert BOTH that the handler was invoked AND that the external identifier was not recorded as deduplicated. An arm checking only the entry's status passes a fix that still poisons the deduplication record |
| L2 bounded attempts to terminal (retry drain) | A chain, each link with its own arm. **An outcome is recorded at all, on the shipped default degree:** `InboxProcessorDrainInvariantsShould.RecordAnOutcome_OnTheDefaultParallelProcessingDegree` — deliberately left at the default, since an arm that raises the degree tests a branch a default deployment does not run; it asserts the dispatch happened before asserting what was recorded, so a drain that dispatched nothing cannot pass. **Each drain consumes exactly one attempt, and the ceiling is terminal:** `InboxProcessorFinalizeKeyShould.RecordARetryAgainstTheEntry_WhenDispatchFailsBelowTheDeadLetterCeiling` and `.RecordTheDeadLetterOutcomeAgainstTheEntry_WhenDispatchFailsAtTheCeiling`. **No entry is stranded between the two ceilings:** `InboxProcessorRetryCeilingShould.PassConfiguredMaxAttemptsAsTheReFetchCeiling_NotAHardcodedLiteral` — a re-fetch ceiling below the dead-letter ceiling excludes an entry from re-selection while it is still short of terminal. **Every handler's entry for one message is drained in one run:** `InboxProcessorCompositeInFlightKeyShould.DrainEveryHandlerEntryForOneMessageInASingleRun`, with `.SeedTwoDistinctRowsForOneMessage_SoThereIsASiblingToSkip` as its positive control — an in-flight set keyed on the message id alone silently drops every sibling entry from the batch, and a dropped entry is never finalized |
| S3 a terminal mark implies an invocation (the composition) | **Stage side: BOUND, both directions.** `AnUnmarkedInboxExitMustReadAsRefusedShould.ReportRefused_WhenLightModeSuppressesADuplicate` asserts that a suppressed dispatch reports a refusal on the context, paired with `.ReportAdmitted_WhenLightModeRunsTheHandler` so a stage that reported a refusal unconditionally cannot pass the safety arm while stranding every message. Both RED-proven against their own mutations: removing the entry-level default reddens the refusal arm and leaves the liveness arm green; removing the upgrade beside the handler invocation reddens the liveness arm and leaves the refusal arm green. **Drain side: UNVERIFIED, and the S2 row above states exactly what that arm owes** — it must run against a store that leases without scoping its claim to a transaction, and must assert both that the handler was invoked AND that the external identifier was not recorded as deduplicated. The arms here bind what the STAGE reports; nothing yet binds what the DRAIN does with a refusal it reads. **The known gap in the property row is not covered by any arm and cannot be closed by one here:** a middleware ordered before the stage is consumer code, so the obligation is stated rather than enforced |
| L3 eventually handled, not merely eventually terminal | **UNVERIFIED. No arm binds it, and this row exists to say so rather than to let the L2 rows above read as covering it.** The arm would have to show a message admitted to the inbox reaching a SUCCESSFUL invocation, not merely a terminal state — which means its mutation is a subsystem that dead-letters on first attempt, since that is precisely what satisfies every L2 arm and fails this one. It is not a variant of the existing drain arms: each of those asserts that an outcome was recorded, and a dead-letter outcome is an outcome. Noting also what such an arm must NOT assume, because the property is conditional on the consumer: a handler that always throws, a dead-letter ceiling reached, and a message whose identity the inbox cannot key (J1b) each end terminally without a successful invocation and are not violations |
| R1 claim-path retry by removal | `InboxStoreConformanceTestKit.FailedEntry_MustNotBeReadmittedByTheClaimPath` (safety — a staged `Failed` entry is refused by `TryClaim`, with the staged status read back so a store whose failure mark did nothing cannot pass vacuously) paired with `InboxStoreConformanceTestKit.ReleasedClaim_MustBeReadmittedForRedelivery` (liveness — a released entry is claimed again), both bound by every provider's kit suite. The pair is the point: safety alone passes on a store whose claim always returns false, and liveness alone passes on one that admits everything |

## Provider maturity

| Provider | Dedup / claim primitive | Transactional exactly-once-state seam | Drain single-in-flight (S1) | Real-infra conformance |
|---|---|---|---|---|
| SqlServer | `MERGE … WITH (UPDLOCK, HOLDLOCK)` on the composite key; `UPDLOCK, HOLDLOCK` claim-check | ✅ `IScopedTransactionalInboxStore` (single local transaction) | ✅ held — `MERGE` lease CAS on `LeaseExpiresAtUtc` vs `SYSUTCDATETIME()`; drain read admits expired-lease `Processing` | ✅ full (deployment-mode, exactly-once, lease-reclaim suites) |
| Postgres | `INSERT … ON CONFLICT` on the composite key; `FOR UPDATE` claim-check | ✅ `IScopedTransactionalInboxStore` | ✅ held — lease CAS on `lease_expires_at` vs `NOW()`; drain read admits expired-lease `Processing` | ✅ full |
| Oracle | `MERGE` on the composite key; `FOR UPDATE` claim-check; `BindByName` forced | dedup + claim path (see capability matrix) | ✅ held — insert-then-update lease CAS vs `SYS_EXTRACT_UTC(SYSTIMESTAMP)`; drain read admits expired-lease `Processing` | ✅ deployment-mode + isolation |
| CosmosDb | single-partition `CreateItem` first-writer-wins (never `UpsertItem`) | ✅ transactional-batch exactly-once-state | ❌ **not held** — declares no lease protocol, so nothing fences two drains; the drain read stays `Failed`-only. Run the drain single-instance | ✅ concurrent-redelivery |
| DynamoDb | `PutItem` first-writer-wins under an `attribute_not_exists` condition on the partition key; condition-guarded finalize | none — no `IScopedTransactionalInboxStore` seam | ❌ **not held** — declares no lease protocol, so nothing fences two drains; the drain read stays `Failed`-only. Run the drain single-instance | ✅ conformance kit (non-skipped real-infra container) |
| MongoDB | `FindOneAndUpdate` upsert on the composite key | transactional path requires a replica-set multi-document transaction | ✅ held — two-stage lease CAS vs the server's `$$NOW`; drain read admits expired-lease `Processing` | 🚧 isolation verified; treat transactional exactly-once as **UNVERIFIED** without a replica set |
| Redis | Lua script (atomic read-decide-write) + leases | single-writer | ✅ held — Lua lease CAS vs the server's `TIME`; drain read admits expired-lease `Processing` | isolation + lease-reclaim verified |
| Firestore | `CreateAsync` first-writer-wins on the composed document id (constant-prefixed, so it cannot land in the provider's reserved `__.*__` id namespace); precondition-guarded conditional finalize/delete | none — no `IScopedTransactionalInboxStore` seam | ❌ **not held** — declares no lease protocol, so nothing fences two drains; the drain read stays `Failed`-only. Run the drain single-instance | ✅ conformance + release-race (non-skipped real-infra emulator) |
| Elasticsearch | `OpType.Create` first-writer-wins on the composed document id; `IfSeqNo`/`IfPrimaryTerm` conditional delete | none — no `IScopedTransactionalInboxStore` seam | ❌ **not held** — declares no lease protocol, so nothing fences two drains; the drain read stays `Failed`-only. Run the drain single-instance | ✅ conformance + claim-atomicity + release-race (non-skipped real-infra container) |
| InMemory | in-process lock + bounded dedup window | n/a (development / testing) | ✅ held — lease CAS under the store lock against the injected `TimeProvider`; drain read admits expired-lease `Processing`. Single-process only: it fences concurrent drains inside one process, which is the only scope this store has | ✅ unit |

> A cell marked **UNVERIFIED** means the guarantee is *intended* but the non-skipped real-infra arm proving it
> is not yet green for that provider / configuration — do not depend on it in production until it is.

## Known gaps

- **The Cosmos DB inbox offers no lease.** It declares the claim protocol only. A lease
  requires the expiry comparison and the write it gates to be decided in one atomic step against the
  store's own clock, because that is the only clock every competing processor agrees on. Cosmos exposes
  no item-level conditional write that reads server time: an ETag makes the *write* atomic without making
  the *decision* correct, so two processors whose clocks differ by more than the remaining lease can both
  conclude the lease has expired, and the one whose write lands is not necessarily the one that reasoned
  correctly. A lease built that way would reclaim a live lease and run the handler concurrently — turning
  a stall into a double-execution, which is worse than the gap it closes. The server-side script that
  could decide it correctly is not a mechanism this store can rely on going forward.
  **The scope of that argument is a lease whose expiry a client clock decides, and that is the only
  construction it rules out.** It says nothing against a lease whose expiry the *server* decides — one
  where the store itself removes the term once it elapses, so no participant compares timestamps. None
  is implemented for this provider today, and the provider table records the lease protocol as not
  held, so the consequence below is the current behaviour rather than a permanent limit.
  **Consequence:** an entry whose processor dies mid-handler stays `Processing` until an operator clears
  it, and the message is not re-admitted. **What holds regardless:** the terminal mark is still
  single-committed — the claim is a first-writer-wins insert, so two processors never both finalize one
  entry. This is a liveness gap, not a correctness one. **If you need a bounded stall, choose a provider
  that declares the lease protocol** (see the provider table).
- **Delivery is at-least-once, not exactly-once.** The inbox makes the *committed effect* once via dedup;
  duplicate deliveries still reach the receiver and are filtered. Idempotent handlers make this correct.
- **Context↔mode enforcement (H2) is startup-only.** The H2 guard fires via `ValidateOnStart`, which runs
  when an `IHost` is started. In host-less / serverless wiring (Azure Functions, AWS Lambda, a manually
  driven `IOutboxProcessor`) the guard does not fire, and there is **no** per-store first-use floor for
  context↔mode (unlike the H1 schema handshake, which the per-store `EnsureSchema` still enforces). The
  silent cross-tenant-loss configuration — a resolving `ITenantContext` left in single-tenant mode — is
  therefore not caught in that path; the consumer obligation above is the mitigation.
- **Non-transactional `TryClaim` tombstone.** A processor that claims a message and crashes before calling
  `TryMarkAsProcessed` / `Release` leaves a Processing row behind. On the **lease** protocol the lease window
  bounds the stall and a replacement processor reclaims it. On the **claim** protocol there is no lease and no
  reaper, so the row is stranded until an operator removes it and the message is not re-admitted; a consumer
  that needs a bounded stall should choose a provider declaring the lease protocol.
- **On a store that declares no lease protocol, two concurrently running inbox processors will both
  dispatch the same failed entry (S1).** The drain's read is a plain query, not a claim: it selects failed
  entries and dispatches them without taking a term on any of them. Where the store offers
  `ILeasedInboxStore`, the drain now takes that term per entry immediately before dispatch, and the store's
  atomic compare-and-set admits exactly one processor — so S1 holds there, across hosts. Where the store
  offers no lease protocol there is nothing to take: both processors dispatch, both marks are idempotent,
  and neither reports a conflict, so the duplicate is invisible to every caller. The window is **not
  bounded** by any lease or timeout and recurs on every pass. **Run the retry drain single-instance on
  those providers** (see the provider table for which). This is a property of the *drain*, not of the
  stores: the receive path's D1 is unaffected either way.
- **A drain fenced by a lease depends on its store's read admitting expired leases, and only leased
  providers implement that.** Taking a term parks the entry in `Processing`. A read that admitted only
  `Failed` would then never select that entry again if the drain died mid-handler, turning a bounded
  duplicate into permanent silent loss. Every provider declaring the lease protocol therefore also returns
  expired-lease `Processing` entries from its estate-wide failed-entry read, and the conformance kit fails
  one that does not. A consumer supplying a **custom** `IInboxStore` that declares `ILeasedInboxStore` owes
  the same behaviour; a custom leased store whose read stays `Failed`-only will strand entries, and the kit
  is what detects it.
- **Optional capabilities are not uniform across providers.** Not every provider implements every optional
  inbox capability (the scoped transactional exactly-once-state seam, backoff-scheduled retry). A consumer that
  requires a specific capability should confirm the chosen provider advertises it rather than assuming parity.
- **Transactional exactly-once-state requires a transaction-capable backing store.** Providers whose
  transactional seam needs infrastructure that may be absent (for example a MongoDB replica set) fall back to
  the non-transactional dedup path, which is at-least-once delivery with idempotent dedup but not the single
  atomic handler+mark transition — treat the transactional guarantee as unavailable there until verified.
  **MongoDB checks this at startup, not on first use.** Setting `MongoDbInboxOptions.EnableTransactions` true
  against a deployment that is not a replica set or sharded cluster fails options resolution (a `hello`
  round trip against the configured server, run once via `IValidateOptions` + `ValidateOnStart`), naming the
  option and the reason — a consumer never learns this from a live message failing under load. A
  builder-supplied `IMongoClient`/client factory is unprobed: that consumer owns the client's connection and
  is responsible for pointing it at a topology that supports the flag.

## Retention (GDPR right-to-erasure via bounded age)

Inbox entries can carry data-subject content in their payload, and this surface carries no per-subject
encryption key -- the encryption seam here is single-context, documented as such on `AddInboxEncryption`.
So the erasure guarantee on this surface is **bounded retention provably elapsing**, not key destruction:
a processed entry is deleted once it has aged past a configured retention bound, independent of which
subject it belongs to.

**Stated so it can be falsified.** Given `AddInboxRetention` is registered (in addition to
`AddRetentionEnforcement`) with `RetentionDays = N`: after an enforcement pass evaluated at time `T`,
every entry whose `ProcessedAt` is at or before `T - N days` is absent from the store, while an entry
whose `ProcessedAt` is after that bound is unaffected and still present.

**How it is achieved.** `InboxRetentionContributor` (`Excalibur.Compliance`) is registered against the
`IRetentionContributor` seam that `IRetentionEnforcementService` already orchestrates and schedules --
the same seam the erasure side's `IErasureContributor` plugs into. On each pass it computes the cutoff
from `InboxRetentionOptions.RetentionDays` and calls the store-agnostic
`IInboxStoreAdmin.CleanupAllTenantsProcessedEntriesAsync(cutoff, cancellationToken)` -- the same
estate-wide, age-based deletion primitive every first-party inbox provider already implements. A dry
run, or `RetentionDays <= 0`, never deletes and reports zero records cleaned, honoring
`IRetentionContributor`'s "never report success while deleting nothing" contract.

**Consumer obligation.** Retention on this surface is opt-in: call `AddInboxRetention()` in addition to
`AddRetentionEnforcement()`. Without it, enforcement runs honestly inert for the inbox -- it logs a
warning and reports zero records cleaned rather than silently claiming success.

**Known gap.** Deletion is by age only, not by data subject: a specific subject's entry cannot be erased
on demand ahead of the retention bound elapsing. A consumer with a strict per-request erasure SLA on
inbox payloads should set `RetentionDays` short enough to satisfy it, or avoid placing
subject-identifying data in inbox payloads.

**Evidence.** `InboxRetentionContributorShould` -- safety: an entry past its retention bound is gone;
liveness: an entry still within the bound survives -- run against the real in-memory inbox store.
