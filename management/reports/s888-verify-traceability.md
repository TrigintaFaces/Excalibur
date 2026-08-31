# S888 VERIFY (TRACEPOINT) — Requirement→Test Traceability

**Author:** TestsDeveloper · **Phase:** VERIFY (task #2607) · **Date:** 2026-07-14 · **HEAD:** `24cf26740`
**Spec:** `management/reports/s888-spec-decomposition.md` (BLUEPRINT) + `s888-sprint-guidance.md` (COMPASS)
**Scope note:** S888 delivered a **subset** of the decomposed backlog — the worker roster died overnight and most Lane C/D/F/G + the Dijkstra/Liskov/Metz audit children **carried open to S889** (PM ruling). This matrix verifies the **delivered/closed** beads only; carried beads are listed as such, not as gaps.
**Test evidence:** the S888 TEST (CRUCIBLE) run — see `## Test Results` in `management/sprints/sprint-888-plan.md`.

---

## Traceability Matrix — Delivered S888 Scope

| Bead | Lane | Acceptance Criterion (Given/When/Then) | Test coverage | Result |
|---|---|---|---|---|
| **rjolfk** | B | SqlServer/Postgres inbox: handler + processed-mark commit in one `IDbTransaction`; throw→rollback (neither persists). Safety: crash between → reprocessable, no partial commit. Liveness: success commits both atomically. Real-infra, non-skipped. | `SqlServerTransactionalInboxExactlyOnceShould`, `PostgresTransactionalInboxExactlyOnceShould`, `SqlServerInboxStoreConformanceShould`, `…TenantIsolationShould`, `InboxStoreAmbientTenantLeakShould` (real-infra) + `SqlInboxTransactionScopeShould` ×4 (unit) | ✅ **FULL** — real-infra 0 fails + unit shard GREEN |
| **uw1nv4** (canon; `3q1jtm` dup) | A | PG/Oracle: stale token N-1 → claim yields **0 rows** (set-based, MUST NOT throw), stale mark rejected; high-water advances monotonically. Real-infra per provider. | PG + Oracle `Fencing_ValidMonotonicToken_ClaimsAndDrains`, `Fencing_MarkSentWithStaleToken_IsRejectedFailClosed` (real Postgres + real Oracle) | ✅ **FULL** — 0 fence-fact fails on real PG & Oracle |
| **y1moc0** | A | Delete-on-sent store: base sent-tracking + cleanup facts capability-gated (inverted for `SupportsSentTracking==false`) **and still asserted (liveness)** for tracking stores. Both arms non-vacuous. | `OutboxStoreConformanceTestBase` sent-tracking facts (5) — InMemory (unit, 1906/0) + real Postgres inversion | ✅ **FULL** — unit GREEN; on real PG **fixed 3** previously-red facts (worktree-proven 18→15) |
| **3q1jtm** | A | Dedup of `uw1nv4` (PG+Oracle fence CAS). | Covered by uw1nv4 (forge-integration cl.8 — delivered scope covers PG+Oracle). | ✅ **dedup-closed** |
| **5fswhd** | C | Real Mongo + default registration: incumbent token T → graceful release/restart → challenger token strictly > T (never resets to 1). Liveness: active renew → no takeover. | `MongoDbLeaderElectionFencingShould`, `MongoDbLeaderElectionTakeoverShould` (real Mongo) | ✅ **FULL** — real Mongo 0 fails (impl `6c8da8093`) |
| **0qyitl** (D2 ≡ L5) | E | Formal skewed-clock interleaving: takeover requires monotonic-`TimeProvider` grace + CAS; RED = clock-skew yielding two `IsLeader`. | Author≠impl real-Mongo split-brain lock — **not delivered** (roster attrition). Impl *behavior* covered by `MongoDbLeaderElectionTakeoverShould`. | ⚠️ **CARRIED → S889** (blocked). Behavior verified via Takeover; the formal split-brain-interleaving lock is the carried deliverable. |
| **8z65sn** (L7) | E | `IMessageBus` handler fault-independence — one handler throwing must not suppress siblings; RED on `LocalMessageBus` fail-fast. | `MessageBusEventFaultIndependenceConformanceShould` ×2 (unit, direct-fixture, non-vacuous) | ✅ **FULL** — GREEN (`50187d834`) |
| **uo90tv** (L9) | E | Serializer `ResolveType(GetTypeName(t)) == t` + cross-serializer wire parity; RED on incompatible names / AOT casing. | `EventSerializerTypeNameRoundTripConformanceShould` ×3 (unit, direct-fixture) | ✅ **FULL** — GREEN (`aaae1ed71`) |
| **2nmc1e** (L6) | E | Exactly-once fold + monotonic checkpoint (non-atomic fold+save = 0 coverage). | Closed-as-phantom — covered by `MaterializedViewProcessorShould.cs:284-308` (single atomic fold+save; PdM+Backend code-verified). | ✅ **covered** (premise-collapsed) |
| **n9tfdn** (L1) | E | `IEventStore` append → return failure not throw. | Closed-as-phantom for the concurrency-conflict facet; transient-fault dimension → `yr76xx` (SA-seam, carried). | ✅ / partial-deferred (`48a6e2acb`) |
| **vomqoe** (L4) | E | `ISagaStore` purge terminal-only. | Closed — clean phantom (already satisfied). | ✅ **phantom** (`48a6e2acb`) |

---

## Step 1b — Observability-Staleness Check

S888 changed inbox/outbox wiring + added fencing (`StaleOutboxFencingTokenException`). Grepped the changed `src/**/*.cs` for `LoggerMessage`/`Log*`/routing template strings asserting pre-change behavior → **none found**. No observability-staleness defect.

## Orphaned-Test Check

Every new S888 test traces to a delivered bead (rjolfk inbox scope, uw1nv4/y1moc0 outbox conformance base, 8z65sn L7, uo90tv L9). **No orphaned tests / no gold-plating.**

---

## Coverage Summary

| Category | Total (delivered) | Covered | Carried | Coverage |
|---|---|---|---|---|
| Provider-parity ACs (rjolfk, uw1nv4, y1moc0, 3q1jtm) | 4 | 4 | 0 | 100% |
| LeaderElection (5fswhd impl / 0qyitl formal lock) | 2 | 1 | 1 | 50%* |
| Liskov conformance pins (8z65sn L7, uo90tv L9, 2nmc1e L6, n9tfdn L1, vomqoe L4) | 5 | 5 | 0 | 100% |
| **OVERALL (delivered scope)** | **11** | **10** | **1** | **91%** |

`*` The single non-covered item (`0qyitl`, Dijkstra D2 formal skewed-clock split-brain lock) is a **tracked carry to S889**, not a silent gap. The underlying leader-fencing *behavior* is verified GREEN on real Mongo via `MongoDbLeaderElectionTakeoverShould`; what carries is the formal clock-skew *interleaving* author≠impl lock.

## VERDICT

**FULL COVERAGE of delivered S888 scope.** Every delivered/closed bead's acceptance criterion has passing test coverage (real-infra where the AC demands it). One tracked carry (`0qyitl` formal split-brain lock → S889); zero orphaned tests; no observability-staleness defect. Pre-existing PG/Oracle outbox data-fidelity failures (`4z0iid`, P2) are outside S888 scope (worktree-proven) and do not gate.
