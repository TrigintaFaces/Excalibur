# S894 VERIFY / TRACEPOINT — Requirement-to-Test Traceability

**Date:** 2026-07-21 · **Verifier:** TestsDeveloper (TRACEPOINT) · **Committed HEAD:** `e5d9cadeb`
**Scope:** S894 = backlog-clearance (~150 beads, 9 disjoint single-owner lanes). No monolithic feature spec; traceability centers on the **guarantee-path keystones** (each bead's AC ↔ its delivered real-infra test). The outbox conformance contract (Lane 2, the sprint headline) is the densest requirement surface — the shared `OutboxStoreConformanceTestBase` (53 methods, run per-provider) *is* the requirement↔test mapping.

## Step 1b — Observability staleness (routing/behavior change)

The outbox changes (exactly-once `Status` guard, routing-field round-trip) touch persistence, not routing-behavior flips. Grep of the changed outbox source for stale `Log*`/telemetry templates asserting OLD behavior → **clean** (only compiled `bin/**/*.xml` doc artifacts match, no source template drift). **No staleness defect.**

## Outbox conformance contract (Lane 2 — headline)

| Requirement (IOutboxStore contract) | Conformance test | Coverage |
|---|---|---|
| **Full-field round-trip** — persisted→reloaded MUST preserve every consumer field (PartitionKey/GroupKey/TargetTransports/IsMultiTransport/ScheduledAt/Headers…) (`n20aqx`) | `StageMessage_RoundTripsEveryConsumerSuppliedField_OnReload` | ✅ FULL — SqlServer/Mongo/Postgres/ES/Redis/Marten (gttg9d fixed Mongo/Redis/ES drops) |
| **Claim disjointness / at-most-once-per-claim** (Liskov L2 — concurrent claimers get disjoint sets) | `GetUnsentMessages_ConcurrentClaimers_ReceiveDisjointSets` | ✅ SqlServer/Mongo/Postgres · ⏳ ES pending-`03koal` |
| **One-winner MarkSent (exactly-once)** — no double-delivery | `ConcurrentMarkSent_OnlyOneSucceeds` | ✅ SqlServer(`ruxhyi` fix)/Mongo/Postgres · ⏳ ES pending-`03koal` |
| **Leadership fencing** — superseded leader fenced fail-closed; row not lost/deleted (f5zutu TOCTOU); claim yields empty (not throw); exception reports HighWaterToken | `Fencing_MarkSentWithStaleToken_IsRejectedFailClosed`, `Fencing_SupersededLeaderCannotMutateOrLoseMessage_AfterHandover`, `Fencing_ValidMonotonicToken_ClaimsAndDrains`, `Fencing_RejectionException_ReportsHighWaterToken` | ✅ SqlServer/Mongo/Postgres (gttg9d fixed Mongo claim-empty + SqlServer/Mongo HighWaterToken) · Redis/ES self-skip (do not advertise `IFencedOutboxStore` — legitimate) |
| **Duplicate-id → `InvalidOperationException`** (Liskov cross-provider consistency) | `StageMessage_DuplicateId_ThrowsInvalidOperationException` | ✅ FULL (`qtdr4v` fixed SqlServer wrap, all 3 stage paths) |
| **Batch-size validation** → `ArgumentOutOfRangeException` | `GetUnsentMessages_WithInvalidBatchSize_ThrowsArgumentOutOfRangeException` | ✅ FULL (`hlnrxt` fixed SqlServer) |
| **FIFO by CreatedAt** (claim output order provider-specific) | `GetUnsentMessages_OrdersByCreatedAt` | ✅ FULL (`qwjjz8` — assert CreatedAt-monotonic-with-staging, not claim-output-order) |
| **at-least-once re-claim (R1/R2/R3)** — failure-anchored floor, dispatcher-ownership guard, monotonic attempts | `wseau9` conformance arms + `CreateStoreWithReclaimFloorAsync`/`TryReserveMessageUnderForeignDispatcherAsync` hooks | ✅ Mongo/Redis (SqlServer/Postgres inherit) |

**Provider result (real infra, isolation):** SqlServer 54/54 · Mongo 55/55 · Postgres 54/54 · Redis 49p/5skip · ES 39p/15skip — **zero fail, zero committed-RED.**

## Other lane keystones (AC ↔ delivered real-infra lock)

| Bead | Requirement / AC | Test (real infra) | Coverage |
|---|---|---|---|
| `9x2tv1` | Inbox cross-tenant dedup isolation — tenant B's `(msgId,handler)` NOT deduped vs A; A's own dup IS deduped | `MongoDbInboxStoreTenantIsolationShould`, `RedisInboxStoreTenantIsolationShould` | ✅ FULL (2/2 real Mongo + 2/2 real Redis; non-vacuous by contrast) |
| `c4i8n7` | Saga find-or-create — no concurrent double-insert | `SqlServerSagaStoreConcurrencyConformanceShould` | ✅ FULL (HOLDLOCK mechanism-assert RED-proven + 2-session + 32-concurrent liveness) |
| `e6batc` | Key-escrow — no lone-master recovery; M-of-N quorum seal | `SqlServerKeyEscrowQuorumRecoveryShould` | ✅ FULL (5/5 real SqlServer; below-threshold fails closed) |
| `as4sb4` | Projection ciphertext-at-rest | `EncryptingProjectionStoreCiphertextAtRestShould` | ✅ FULL (real AES-256-GCM) |
| `4f8lyo` | PG projection filter/OrderBy SQLi closed | `PostgresProjectionStoreSqlInjectionShould` | ✅ FULL (8/8 real Postgres) |
| `2mtb74` | Oracle `MarkFailedAsync` reserve-token guard | (S893 real-Oracle lock) | ✅ covered |
| `scyy8a` | Inbox prerequisite validator — claim-required contract | `InboxPrerequisiteValidatorShould` (Data + Data.SqlServer) | ✅ FULL (F-5 sibling flipped) |

## Coverage summary

|                       | Covered | Pending-tracked | Missing |
|-----------------------|---------|-----------------|---------|
| Outbox contract reqs  | 7/7 on implementing providers | ES/Redis 2 (03koal) | 0 |
| Lane keystone ACs     | 7/7    | 0               | 0 |

**VERDICT (initial): FULL COVERAGE of guarantee-path requirements on implementing providers; all gaps tracked-pending (no silent skips, no committed-RED, no uncovered MUST).**

## REVIEW_CODE Correction (2026-07-21, ProjectReviewer SENTINEL #35250)

This traceability was built from working-tree test runs and **overstated coverage in two places** that the independent REVIEW_CODE clean-worktree/adversarial pass surfaced. Corrected here (retraction reaches the artifact):

- **Committed-HEAD build cert was working-tree-based.** My TEST "committed HEAD green" did not clean-worktree-build the committed SHA `e5d9cadeb`; REVIEW_CODE found the committed HEAD does **not** compile (`ObservabilityEventId.TraceEnrichmentFailed` stranded uncommitted since 07-19 — pre-existing, non-outbox). Discipline correction: build the committed SHA, not the working tree (tracked `k947g1`). Fix committing @`c0f6f87c6` (PM).
- **Fencing coverage was NOT full — a durability-after-cleanup gap (B2).** SqlServer derives the fence high-water from `MAX(FencingToken)` over the outbox table, which `CleanupSentMessages` deletes → after cleanup a superseded leader's stale token passes (`MAX→NULL→0`). **No conformance arm runs cleanup between advancing the high-water and the stale-claim** → the hole is untested. Owner (arm): TestsDeveloper — `Fencing_HighWaterSurvivesCleanup` (SqlServer-specific; Mongo durable via `::fence` doc). Pending SA fix-vs-carry ruling.
- **7iu2xc(B) is worse than "carried" — a committed VACUOUS lock arm (B3).** `TenantScopedTieredReadThroughShould.FailClosed_WhenNoAmbientTenant_AndNeverQueryCold` (Frontend `acfc63f16`) asserts only no-ambient-tenant fail-closed — never the real cross-tenant cold-read attack — so it manufactures false tenant-isolation confidence (passes pre- and post-fix). The real non-vacuous (B) lock (real-LocalStack-S3, RED on HEAD) is authored + uncommitted, to land coupled with the cold-tenant-filter fix. Resolution behind SA tenancy-seam ruling.

## Gaps — all tracked (no silent skips)

- **`03koal`** (P1 → S895): ES claim-disjointness + one-winner MarkSent + cleanup-batch + failed-query; Redis `GetStatistics`. SA-ruled **required** (ES fix via `if_seq_no`/`if_primary_term` CAS). Honest per-provider documented-pending skip via `PendingConformanceGaps` hook — implementing providers still run+pass. `gttg9d` stays OPEN carrying these.
- **`7iu2xc(B)`** (P1 → next): cold-tier cross-tenant read (lock authored, uncommitted-coupled with the e6t62k cold-tenant-filter fix).
- **`r9bsvg`** (P3 → S895): conformance coverage for 2 additional SqlServer stage paths.
- **`2zw4bd`** (P2): 6 pre-existing Middleware audit-exporter failures (NOT S894 — baseline-proven).

## Orphaned tests

None. Every outbox conformance method maps to an `IOutboxStore` contract requirement; every keystone lock maps to its bead's AC. No gold-plating, no scope-creep tests found.
