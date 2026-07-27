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
- **Duplicate window bounded by the lease.** A processor that claims a message and then crashes before
  marking it holds the claim only until its lease/visibility window expires; after that the message is
  re-claimable. The window is **bounded by the configured lease**, not eliminated.
- **Tenant isolation (opt-in).** With multi-tenancy enabled, the claim/dedup key carries the resolved
  `TenantId`, so two tenants presenting the same `(MessageId, HandlerType)` never deduplicate against each
  other. With multi-tenancy disabled the key is the bare pair and no tenant discriminator is emitted.

> **Consumer obligation:** message handlers **MUST be idempotent.** At-least-once delivery means a handler
> can observe the same message more than once across a crash; the inbox's dedup makes the *committed effect*
> once, but a handler must not assume it is invoked exactly once.

Sub-guarantees (invariants):

| # | Property | Statement |
|---|---|---|
| **D1** | Single committed processing | For a given `(MessageId, HandlerType[, TenantId])`, at most one transaction ever commits the "processed" state transition. Concurrent duplicates serialize; the loser observes "already processed" and skips the handler. |
| **D2** | Atomic handler+mark | On the transactional path the handler's writes and the processed-mark commit or roll back together — never one without the other — provided the handler enlists its writes through the supplied transaction/scope. |
| **L1** | Lease-bounded reclaim | A claimed-but-unprocessed message is not re-claimable within its lease, and **is** re-claimable after it. A crashed processor cannot strand a message forever (subject to the non-transactional tombstone gap below). |
| **T1** | Tenant-keyed dedup | When multi-tenant, the lock-check, the conflict/merge key, and the synthesized insert all carry the resolved `TenantId`; two tenants sharing a message id can never collide. |
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
3. **Mode↔schema handshake (H1)** — `InboxSchemaContract.Verify`
   (`Excalibur.Inbox/DependencyInjection/InboxSchemaContract.cs`) centralizes the four-combination check as
   pure logic; `InboxSchemaValidationHostedService` runs it at startup so a mismatch fails before the first
   message. The per-store `EnsureSchema` check is the host-less floor.
4. **Context↔mode coupling (H2)** — `TenantContextConsistencyValidator`
   (`Excalibur.Dispatch.Abstractions/ContextValues`, wired by `AddDefaultTenantContext` via
   `IValidateOptions<TenantContextOptions>` + `ValidateOnStart`) rejects a resolving `ITenantContext`
   registered while `RequireTenant` is false — the configuration that would apply the single-tenant schema yet
   route multiple tenants through the same keyed rows. Unlike the H1 mode↔schema handshake — which also has a
   per-store first-use floor (`EnsureSchema`) — this guard is **startup-only** via `ValidateOnStart` and has
   **no** per-store floor, so it does not fire in host-less wiring (see Known gaps).
5. **Lease reclaim (L1)** — `TryClaim` stamps a lease; a failed entry is readmitted for retry and a crashed
   processor's claim is reclaimable once the lease elapses.

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
- **On the non-transactional `TryClaim` path, mark or fail every claimed message.** A processor that claims a
  message and crashes before marking it leaves a Processing row until the lease expires (see Known gaps).
- **In host-less / serverless wiring, a consumer MUST ensure `RequireTenant` matches their `ITenantContext`
  registration** (a resolving context requires multi-tenant mode) — the startup consistency guard (H2) does
  not fire without an `IHost`.

## Evidence (conformance)

Each guarantee is bound to a **real-infrastructure** test (a live container; the arms are not skipped when
infrastructure is present) that RED-detects a violation. Guarantee → test:

| Guarantee | Conformance test |
|---|---|
| D1 single committed processing / concurrent duplicates | `SqlServerTransactionalInboxExactlyOnceShould.TransactionalInbox_ConcurrentDuplicates_ProcessExactlyOnce` (+ Postgres, Cosmos equivalents) |
| D2 atomic handler+mark across a crash | `…TransactionalInboxExactlyOnceShould.TransactionalInbox_CrashMidProcessing_RedeliversWithoutDuplicatingCommittedEffect` (SqlServer + Postgres) |
| T1 tenant-keyed dedup / isolation | `SqlServerInboxStoreDeploymentModeShould.Isolate_Rows_By_Tenant_On_The_Triple_Schema` + `Dedup_A_Genuine_Duplicate_Within_A_Single_Tenant_On_The_Triple_Schema` (+ Postgres `Isolate_Claims_By_Tenant_On_The_Triple_Schema`) |
| Non-MT default deployment (liveness) | `SqlServerInboxStoreDeploymentModeShould.Claim_And_Dedup_A_Non_Multi_Tenant_Message_On_The_Pair_Schema` + `Read_Back_A_Non_Multi_Tenant_Entry_On_The_Pair_Schema` |
| H1 fail-closed mode↔schema | `SqlServerInboxStoreDeploymentModeShould.Fail_Closed_When_A_Multi_Tenant_Store_Runs_The_Pair_Schema` + `Fail_Closed_When_A_Non_Multi_Tenant_Store_Runs_The_Triple_Schema` (+ Postgres) |
| H2 fail-closed context↔mode | `TenantContextConsistencyGuardShould` (unit; safety arm throws on resolver+single-tenant, liveness arm starts clean on the default and on required-tenant mode) |
| Fail-closed on unresolved tenant | `PostgresInboxStoreFailsClosedOnNullAmbientTenantShould.FailClosed_BeforeTouchingSql_WhenMultiTenantAndAmbientTenantIsNull` |
| L1 lease reclaim / retry readmit | `SqlServerInboxStoreLeaseReclaimShould.Reclaim_the_message_after_the_lease_expires` + `Readmit_and_retry_a_failed_entry_on_redelivery` (+ Postgres) |

## Provider maturity

| Provider | Dedup / claim primitive | Transactional exactly-once-state seam | Real-infra conformance |
|---|---|---|---|
| SqlServer | `MERGE … WITH (HOLDLOCK)` on the composite key; `UPDLOCK, HOLDLOCK` claim-check | ✅ `IScopedTransactionalInboxStore` (single local transaction) | ✅ full (deployment-mode, exactly-once, lease-reclaim suites) |
| Postgres | `INSERT … ON CONFLICT` on the composite key; `FOR UPDATE` claim-check | ✅ `IScopedTransactionalInboxStore` | ✅ full |
| Oracle | `MERGE` on the composite key; `FOR UPDATE` claim-check; `BindByName` forced | dedup + claim path (see capability matrix) | ✅ deployment-mode + isolation |
| CosmosDb | single-partition `CreateItem` first-writer-wins (never `UpsertItem`) | ✅ transactional-batch exactly-once-state | ✅ concurrent-redelivery |
| MongoDB | `FindOneAndUpdate` upsert on the composite key | transactional path requires a replica-set multi-document transaction | 🚧 isolation verified; treat transactional exactly-once as **UNVERIFIED** without a replica set |
| Redis | Lua script (atomic read-decide-write) + leases | single-writer | isolation + lease-reclaim verified |
| InMemory | in-process lock + bounded dedup window | n/a (development / testing) | ✅ unit |

> A cell marked **UNVERIFIED** means the guarantee is *intended* but the non-skipped real-infra arm proving it
> is not yet green for that provider / configuration — do not depend on it in production until it is.

## Known gaps

- **Delivery is at-least-once, not exactly-once.** The inbox makes the *committed effect* once via dedup;
  duplicate deliveries still reach the receiver and are filtered. Idempotent handlers make this correct.
- **Context↔mode enforcement (H2) is startup-only.** The H2 guard fires via `ValidateOnStart`, which runs
  when an `IHost` is started. In host-less / serverless wiring (Azure Functions, AWS Lambda, a manually
  driven `IOutboxProcessor`) the guard does not fire, and there is **no** per-store first-use floor for
  context↔mode (unlike the H1 schema handshake, which the per-store `EnsureSchema` still enforces). The
  silent cross-tenant-loss configuration — a resolving `ITenantContext` left in single-tenant mode — is
  therefore not caught in that path; the consumer obligation above is the mitigation.
- **Non-transactional `TryClaim` tombstone.** On the non-transactional path, a processor that claims a message
  and crashes before calling `TryMarkAsProcessed` / `MarkFailed` leaves a Processing row that is only
  reclaimable after its lease expires. There is no separate reaper; the lease window bounds the stall.
- **Optional capabilities are not uniform across providers.** Not every provider implements every optional
  inbox capability (the scoped transactional exactly-once-state seam, backoff-scheduled retry). A consumer that
  requires a specific capability should confirm the chosen provider advertises it rather than assuming parity.
- **Transactional exactly-once-state requires a transaction-capable backing store.** Providers whose
  transactional seam needs infrastructure that may be absent (for example a MongoDB replica set) fall back to
  the non-transactional dedup path, which is at-least-once delivery with idempotent dedup but not the single
  atomic handler+mark transition — treat the transactional guarantee as unavailable there until verified.
