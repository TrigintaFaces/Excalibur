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

| # | Property | Statement |
|---|---|---|
| **D1** | Single committed processing | For a given `(MessageId, HandlerType[, TenantId])`, at most one transaction ever commits the "processed" state transition. Concurrent duplicates serialize; the loser observes "already processed" and skips the handler. |
| **D1a** | Processed is absorbing | Once a `(MessageId, HandlerType[, TenantId])` has reached `Processed`, no store write moves it out of that state. Every write that would change the status of a processed entry — the failure mark and the in-flight processing mark alike — refuses the transition and leaves the entry as it found it, rather than reporting an error. This is what D1 rests on: a demoted entry is re-admittable, so a single overwriting write turns effectively-once processing back into repeated processing with no duplicate visible to any caller. |
| **D2** | Atomic handler+mark | On the transactional path the handler's writes and the processed-mark commit or roll back together — never one without the other — provided the handler enlists its writes through the supplied transaction/scope. |
| **L1** | Lease-bounded reclaim | A claimed-but-unprocessed message is not re-claimable within its lease, and **is** re-claimable after it. A crashed processor cannot strand a message forever (subject to the non-transactional tombstone gap below). |
| **R1** | Claim-path retry by removal | On the claim protocol, `TryClaim` is refused whenever an entry exists for the key — `Processing`, `Processed` or `Failed` alike — and retry is reached by `Release`, which removes the non-terminal row so the next redelivery claims it again. A `Failed` entry is owned by the estate-wide retry drain and is not re-claimable: the protocol carries no term, so a redelivery admitted alongside the drain could not be fenced against it and the handler would run twice. This differs from the lease protocol, where acquisition does re-admit a `Failed` entry because recording the failure clears the term. |
| **T1** | Tenant-keyed dedup | When multi-tenant, the lock-check, the conflict/merge key, and the synthesized insert all carry the resolved `TenantId`; two tenants sharing a message id can never collide. |
| **T2** | Estate-wide retry drain | `IInboxStoreAdmin.GetAllTenantsFailedEntriesAsync` applies **no** tenant term on any provider, by contract rather than by omission. The retry sweeper has no tenant of its own, so a tenant-confined variant would drain only whichever partition happened to be ambient and let every other tenant's failed entries accumulate unretried. The caller re-establishes each entry's own tenant from `InboxEntry.TenantId` before acting on it. |
| **S1** | Single in-flight invocation (retry drain) | For a given `(MessageId, HandlerType[, TenantId])`, at most one handler invocation is in flight at any instant — across concurrent drains, across a redelivery racing a drain, and across hosts. The drain is a **second, independent processor of the same rows** as the receive path, so this is not implied by the receive path's D1: two drains reading the same `Failed` entry would each dispatch it, and both marks would be idempotent, so nothing would report the duplicate. **Held only where the store declares the lease protocol** — the drain's read takes no term, so the store's atomic lease acquisition is the entire fence. On a store without that protocol S1 does **not** hold and the single-instance consumer obligation applies. |
| **S2** | Exactly one committed finalize (retry drain) | Every successful handler invocation performed by the drain is followed by **exactly one** committed `Processing → Processed` transition, recorded under that entry's own `(MessageId, HandlerType)` **and under that entry's own tenant**. Both halves are load-bearing and fail differently: a finalize under the wrong key addresses no row, and a finalize under the wrong tenant — including a *cleared* ambient, which means "no tenant was established" rather than "this row has no tenant" — is refused by a multi-tenant store after the handler has already run. |
| **L2** | Bounded attempts to terminal (retry drain) | Every `Failed` entry reaches a terminal state — `Processed`, or dead-lettered — within a bounded number of attempts. Each drain of an entry must consume exactly one attempt (a transient short-circuit that never reached the handler consumes none), and an entry that is re-admitted without its attempt count advancing is unbounded, not merely slow. |
| **K1** | Injective dedup key | On a provider that addresses an entry by one composed id rather than a composite column key, distinct `(TenantId, MessageId, HandlerType)` triples never render the same id. Two halves, established differently. Against the **separator** it is structural: each term is percent-encoded, and the encoder's output alphabet excludes the separator, so it cannot occur inside a term and the id is decodable. Against the **encoder** it is guarded: the percent-encoder is not injective over all strings — it folds every unpaired surrogate onto one replacement character — so terms that are not well-formed text are refused rather than admitted. A term set whose composed id exceeds the provider's id limit is likewise refused, never truncated, since a truncated key is one two messages could share. A join that is merely *unlikely* to collide is not sufficient: a collision here is a silently dropped message on the success path, not an error. |
| **H1** | Fail-closed mode↔schema handshake | A store whose deployment mode disagrees with its physical schema (multi-tenant store on the pair schema, or single-tenant store on the triple schema) fails fast at startup rather than running a predicate-less or malformed query. |
| **H2** | Fail-closed context↔mode coupling | Registering a resolving tenant context while the deployment stays in single-tenant mode is rejected at startup, closing the silent cross-tenant-loss configuration. |

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
| D1 single committed processing / concurrent duplicates | `SqlServerTransactionalInboxExactlyOnceShould.TransactionalInbox_ConcurrentDuplicates_ProcessExactlyOnce` (+ Postgres, Cosmos equivalents) |
| D1a Processed is absorbing under the processing mark | `InboxStoreConformanceTestKit.ProcessedEntry_MustNotBeDemotedByTheProcessingMark`, bound by every provider's kit suite (SqlServer, Postgres, Oracle, CosmosDb, MongoDB, Elasticsearch, DynamoDB, Firestore, Redis, InMemory) — finalizes an entry, issues the durable processing mark over it, and reads the status back. A store whose mark writes over `Processed` fails; a store that does not implement the mark reports the arm as unverified rather than passing it. The claim-path half of the same property is `ProcessedEntry_MustNotBeReadmittedByTheClaimPath`; the two fail under different mutations |
| D2 atomic handler+mark across a crash | `…TransactionalInboxExactlyOnceShould.TransactionalInbox_CrashMidProcessing_RedeliversWithoutDuplicatingCommittedEffect` (SqlServer + Postgres) |
| T1 tenant-keyed dedup / isolation | `SqlServerInboxStoreDeploymentModeShould.Isolate_Rows_By_Tenant_On_The_Triple_Schema` + `Dedup_A_Genuine_Duplicate_Within_A_Single_Tenant_On_The_Triple_Schema` (+ Postgres `Isolate_Claims_By_Tenant_On_The_Triple_Schema`) |
| K1 injective dedup key | `ElasticsearchInboxDocumentIdShould` + `FirestoreInboxDocumentIdShould` — safety (triples the previous join merged now render distinct ids; every id decodes back to its exact terms) paired with liveness (one triple renders one id; an ordinary triple stays legible), so neither a colliding join nor a per-call-unique id passes |
| Non-MT default deployment (liveness) | `SqlServerInboxStoreDeploymentModeShould.Claim_And_Dedup_A_Non_Multi_Tenant_Message_On_The_Pair_Schema` + `Read_Back_A_Non_Multi_Tenant_Entry_On_The_Pair_Schema` |
| H1 fail-closed mode↔schema | `SqlServerInboxStoreDeploymentModeShould.Fail_Closed_When_A_Multi_Tenant_Store_Runs_The_Pair_Schema` + `Fail_Closed_When_A_Non_Multi_Tenant_Store_Runs_The_Triple_Schema` (+ Postgres) |
| H2 fail-closed context↔mode | `TenantContextConsistencyGuardShould` (unit; safety arm throws on resolver+single-tenant, liveness arm starts clean on the default and on required-tenant mode) |
| Fail-closed on unresolved tenant | `PostgresInboxStoreFailsClosedOnNullAmbientTenantShould.FailClosed_BeforeTouchingSql_WhenMultiTenantAndAmbientTenantIsNull` |
| L1 lease reclaim / retry readmit | `SqlServerInboxStoreLeaseReclaimShould.Reclaim_the_message_after_the_lease_expires` + `Readmit_and_retry_a_failed_entry_on_redelivery` (+ Postgres) |
| T2 drain finalizes under the entry's own key | `InboxProcessorFinalizeKeyShould` — a namespaced entry is staged under the key both production writers use (the handler type fully qualified, the message type short), drained, and its persisted status / attempt state read back on the success, retry and dead-letter paths. A drain that finalizes with the message type addresses no row and fails all three. The positive-control arm asserts the probe type is namespaced, since a global-namespace probe has `FullName == Name` and would pass every arm vacuously |
| S1 single in-flight invocation (retry drain) | Two halves, bound separately, because they fail under different mutations. **The drain asks for the term:** `InboxProcessorDrainFenceShould.DispatchAnEntryOnlyOnce_WhenTwoProcessorsDrainTheSameFailedEntry` — two processors over one real store and one failed entry, with a counting handler; a drain that omits the acquisition dispatches twice and fails. Its positive control `.DispatchTheEntry_WhenASingleProcessorDrainsIt` asserts one processor still dispatches, so a drain that dispatches nothing cannot pass the safety arm vacuously. **The store refuses the second asker:** `InboxStoreConformanceTestKit.LiveLease_MustNotBeReclaimableByAnotherProcessor`, bound by every provider's kit suite. **Not held, and deliberately untested, on a store that declares no lease protocol** — there is no fence there to assert; the single-instance consumer obligation is the mitigation |
| L2 liveness under the fence — the drain's read admits an abandoned lease | `InboxStoreConformanceTestKit.ExpiredLease_MustBeReadmittedByTheRetryDrainRead` (liveness — an entry left `Processing` under an expired lease is returned by the drain's read, so a dead processor's entry is retried rather than stranded) paired with `.LiveLease_MustNotBeReadmittedByTheRetryDrainRead` (safety — an entry under a live lease is **not** offered for retry). The pair is the point, and this is the arm the S1 fence depends on: a read that admits only `Failed` strands every entry the fence parks in `Processing`, and a read that admits any `Processing` entry hands the drain work a healthy processor is still doing. Loosen the predicate and the safety arm fails; tighten it and the liveness arm does |
| S2 exactly one committed finalize, under the entry's own key AND its own tenant | The two halves fail under different mutations and are bound separately. **Key half:** `InboxProcessorFinalizeKeyShould` (the T2 row above). **Tenant half:** `InboxProcessorDrainInvariantsShould.FinalizeUnderTheEntrysOwnTenant_WhenDrainedOnAMultiTenantHost` paired with `.FinalizeUnderTheUntenantedTerm_WhenTheEntryBelongsToNoTenant` — the fake store resolves each mark's partition through `TenantScope.FromContext`, the conversion a real provider routes every statement through, so a mark committed after the per-entry scope is disposed is observed failing closed rather than judged by inspecting a string. The pair is the point: binding the entry's tenant only when one is present passes the first arm and fails the second |
| L2 bounded attempts to terminal (retry drain) | A chain, each link with its own arm. **An outcome is recorded at all, on the shipped default degree:** `InboxProcessorDrainInvariantsShould.RecordAnOutcome_OnTheDefaultParallelProcessingDegree` — deliberately left at the default, since an arm that raises the degree tests a branch a default deployment does not run; it asserts the dispatch happened before asserting what was recorded, so a drain that dispatched nothing cannot pass. **Each drain consumes exactly one attempt, and the ceiling is terminal:** `InboxProcessorFinalizeKeyShould.RecordARetryAgainstTheEntry_WhenDispatchFailsBelowTheDeadLetterCeiling` and `.RecordTheDeadLetterOutcomeAgainstTheEntry_WhenDispatchFailsAtTheCeiling`. **No entry is stranded between the two ceilings:** `InboxProcessorRetryCeilingShould.PassConfiguredMaxAttemptsAsTheReFetchCeiling_NotAHardcodedLiteral` — a re-fetch ceiling below the dead-letter ceiling excludes an entry from re-selection while it is still short of terminal. **Every handler's entry for one message is drained in one run:** `InboxProcessorCompositeInFlightKeyShould.DrainEveryHandlerEntryForOneMessageInASingleRun`, with `.SeedTwoDistinctRowsForOneMessage_SoThereIsASiblingToSkip` as its positive control — an in-flight set keyed on the message id alone silently drops every sibling entry from the batch, and a dropped entry is never finalized |
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

- **The Cosmos DB inbox offers no lease, and cannot.** It declares the claim protocol only. A lease
  requires the expiry comparison and the write it gates to be decided in one atomic step against the
  store's own clock, because that is the only clock every competing processor agrees on. Cosmos exposes
  no item-level conditional write that reads server time: an ETag makes the *write* atomic without making
  the *decision* correct, so two processors whose clocks differ by more than the remaining lease can both
  conclude the lease has expired, and the one whose write lands is not necessarily the one that reasoned
  correctly. A lease built that way would reclaim a live lease and run the handler concurrently — turning
  a stall into a double-execution, which is worse than the gap it closes. The server-side script that
  could decide it correctly is not a mechanism this store can rely on going forward.
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
