# S872 VERIFY — Spec Traceability Matrix (TRACEPOINT)

**Date:** 2026-07-05 · **Verifier:** TestsDeveloper · **HEAD:** committed, all 10 shards GREEN

S872 had no single spec doc; acceptance criteria are **bead-level** (58 lane beads / 71 tagged, 5 reservoirs: AVW / WWMD / exactly-once / serializer / vacuous-test). CUT/refactor beads have **no behavioral AC** — verified by zero-ref grep + build-green, not by tests (correctly). The matrix below traces the **behavioral / safety-critical deliverables** to their verifying tests.

## Load-bearing deliverables → verifying tests

| Deliverable (beads) | Acceptance criterion | Verifying test(s) | Status |
|---|---|---|---|
| **E1 exactly-once** (qzp9k7 + 7blrs0/6soccx/32xzdg) | Transactional inbox = exactly-once: crash mid-processing rolls back (handler write + processed-mark atomically), redelivers once, duplicate suppressed; concurrent dup → 1 effect | `SqlServerTransactionalInboxExactlyOnceShould` (3) + `PostgresTransactionalInboxExactlyOnceShould` (3) + `ExactlyOnceMessagingCompositionE2EShould` (2) — real-infra, non-skipped | ✅ FULL (6/6 + 2/2) |
| **Saga-purge** (w8aqq3/ghkgtu/kmw1dh/qr2pwl) | `PurgeCompletedBeforeAsync` deletes completed+before-cutoff, retains running/newer; indexed `CompletedAt` column; cloud stores capability-gate | `SagaStoreConformanceTestBase` purge region → InMemory 2/2 + real-infra SqlServer/Postgres/Mongo derivers; cloud derivers assert `NotSupportedException` (DIM) | ✅ FULL |
| **Canonical serializer** (lz95ur/jyp40l + adoption cluster) | Single frozen canonical `Events` serializer (camelCase + enum-as-string, null-omit); every event-persistence store routes through it; AOT-safe cctor | `EventSerializationDefaultsShould` (contract 2/2) + `EventStoreCanonicalSerializerGuardTests` (structural, 6/6, no store builds own options) | ✅ FULL · AOT reflection-off regression lock = **P3 fast-follow (vitlc9)** |
| **Outbox full-field round-trip** (bvymbh/da8mc3) | Universal `OutboundMessage` round-trips every consumer-supplied field | `OutboxStoreConformanceTestBase.StageMessage_RoundTripsEveryConsumerSuppliedField_OnReload` (all derivers) | ✅ (real-infra deriver) |
| **BatchProcessor error-observability** (7b71dl/w0c33m-B1) | `dispatch.microbatch.batch.errors` counter fires on every fault (both `shutdown` arms); `OnBatchError` fail-open; caller never throws | `MinimizeAllocationsInErrorHandling` (counter==N, callback-per-fault, no-throw, alloc-bounded) | ✅ |
| **Vacuous-test remediation** (cf9mz3/yoghbe/vy7cpy) | tautologies → real-contract assertions | `HistogramTimerShould` (17), `AntiPatternVerificationShould` DecisionMatrix (15), `CachingCoverageBoostShould` (33) | ✅ |
| **Object-pool alloc** (w0c33m-B2) | pooled path allocates ≥10× less | `VerifyObjectPoolingReducesAllocations` (ArrayPool, deterministic) | ✅ |
| **Task.Delay sweep** (2zdvmu) | sync-waits → WaitHelpers polling + enforcement gate | 12 conversions + `eng/ci/task-delay-syncwait-gate.sh` (non-vacuous `--self-test`) | ✅ |

## Observability-staleness check (Step 1b) — CLEAN

- **Saga-purge:** no log/metric template asserts the old `UpdatedUtc`/proxy-column behavior (purge keys on `CompletedAt`).
- **GooglePubSub oversized:** DeadLetter EventIds (23700+) are the *legitimate* DLQ path; oversized settlement (`Nack` w/ DLQ, `Ack`-drop w/o) has no contradicting template.
- **Canonical adoption:** no residual "own serializer" template in adopted stores.

## Coverage summary

| | Behavioral deliverables | Covered | Gaps |
|---|---|---|---|
| Safety-critical (exactly-once, saga-purge, canonical) | 3 | 3 FULL | 0 |
| Other behavioral (outbox/batch/vacuous/pool/sweep) | 5 | 5 | 0 |
| CUT/refactor (no AC) | ~40 | grep + build-green verified | 0 |

**VERDICT: FULL COVERAGE of behavioral/safety-critical ACs. 0 critical gaps.**

**Non-critical / tracked:**
- **vitlc9 (P3):** AOT reflection-off regression lock for `EventSerializationDefaults.Canonical` — proposed dedicated reflection-off test project (sync-ci-cd, PM sanction pending). Fix itself verified by contract test + committed.

**Orphaned tests:** none — every S872 test traces to a deliverable.
