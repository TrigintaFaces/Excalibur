# S891 VERIFY — Requirement→Coverage Traceability (TRACEPOINT)

**Author:** TestsDeveloper · **Date:** 2026-07-18 · **Phase:** VERIFY (task 2642)
**Spec:** `management/specs/sprint-891-spec.md` · **HEAD:** `976d8f106` · Method: run → read → cite.

> **CONFIRMED-PASS** = test/lock run to green THIS sprint (TEST or VERIFY phase). **MAPPED** = covering
> test/lock exists + verified non-vacuous by inspection, not re-run this phase (AV cost / already
> integrator+SA-verified at HEAD). **CARVED** = bead not delivered (owner hung through IMPLEMENT) →
> tracked to S892 by PM ruling; AC not met, but NOT a silent drop.

---

## Delivered surface (17 beads closed at HEAD `976d8f106`)

### Wave-0 keystones (P0)

| Bead | AC (safety / liveness) | Covering test | Status |
|---|---|---|---|
| `jxp2yq` | AC1 4 gates invoked by a real trigger · AC2 planted violation → pipeline FAIL · AC3 REFUSE→warn, FAIL→block (3-state) | `harness-gates-ci.test.sh` 5/5 (A fail→exit1, B pass→exit0, **C REFUSE≠PASS**, D accumulator load-bearing, E masking-resistance) + `gate-wiring.test.sh` INTEGRATION arm 12 (repo locks all wired) | **CONFIRMED-PASS** |
| `9pxz2t` | AC1 poller count bounded, orphans reaped · AC2 live agent still heartbeats (safety∧liveness) | `poll-opcom-singleton.test.sh` 6/6 (A atomic-1-of-20, **B live-owner keeps serving**, C stale reclaimed, **F live-slow NOT killed**, E TOCTOU mutant caught) | **CONFIRMED-PASS** |
| `0p6z0v` | AC1 identify REWAKE-hang mechanism · AC2 measurable mitigation instrument | `session-liveness-probe.test.sh` 8/8 (REWAKE-without-START discriminator; **E fresh-mtime not false-flagged**, F mtime load-bearing) | **CONFIRMED-PASS** (+ jlo4fs P2 follow-up tracked) |
| `hv8fjh` | AC0 reconfirm swallow at HEAD · AC1 genuine flush FAIL (exit1) → commit FAILS LOUD, never silent-proceed | `bd-verified-write.harness-lock.sh` + `bd-flush-guard.test.sh` (S890 family, present + wired at HEAD; gate-wiring arm 12 confirms wired) | **MAPPED** |

### Lane-G hardening (P1/P2)

| Bead | AC | Covering test | Status |
|---|---|---|---|
| `1hf2sx` P1 | session-collision-guard WIRED (was orphaned) | `gate-wiring.test.sh` INTEGRATION arm 12 (all-wired) + `session-collision-guard.harness-lock.sh` | **CONFIRMED-PASS** (arm 12) |
| `nu00yn` P1 | gate-wiring ARM enumerates `.claude/harness/*.harness-lock.sh` | `gate-wiring.test.sh` arms 19-22 (unwired→RED, wired→GREEN, CI-unhostable+pre-commit→GREEN, orphaned→RED) | **CONFIRMED-PASS** |
| `exhkgt` P1 | f5-sweep wired to a real trigger (not self-test-only) | `harness-gates-ci.test.sh` (f5-sweep in the CI orchestrator) + gate-wiring arm 12 | **CONFIRMED-PASS** |
| `w1u1c9` P1 | buddy-ring self-heal: dead node skipped, reaper acts on DEAD only, all-dead→escalate | `ring-repair.test.sh` 9/9 (safety∧liveness paired, **Z pick-DEAD regression caught**) | **CONFIRMED-PASS** |
| `xeo795` P1 | RED-on-non-atomic ClaimDueTimeouts conformance lock (concurrent-claim) | `SagaTimeoutStoreConformanceTestBase` → InMemory derivation 32/32 (safety no-duplicate ∧ liveness no-missing); SqlServer+Oracle derivations = integration shard | **CONFIRMED-PASS** (InMemory) |
| `2gzlvg` P2 | gate-wiring evaluates the COMMIT (staged∪HEAD), not lingering WIP | `gate-wiring.test.sh` arms 23 (untracked excluded→no false freeze) + 24 (staged unwired STILL RED, non-vacuous) | **CONFIRMED-PASS** |
| `5uhy2j` P2 | shipped-ddl-sweep INSERT paren-list arm (extractor reads INSERT cols) | `shipped-ddl-sweep.test.sh` 7/7 (G omission→FAIL, H match→PASS, I empty→REFUSE, F no-suppression-cap non-vacuity floor) | **CONFIRMED-PASS** (VERIFY re-run) |
| `qcizyz` P2 | XML dup/orphaned-tag enforcement arm (compiler-silent → gated) | `duplicate-xml-doc-tags.test.sh` 7/7 (B/B2/B3 dup-tag REFUSED = safety, C clean→PASS = liveness, C3 Designer.cs excluded = precision) | **CONFIRMED-PASS** (VERIFY re-run) |

### .NET correctness (P2/P3)

| Bead | AC | Covering test | Status |
|---|---|---|---|
| `5uajzo` P2 | InMemoryInboxStore eviction fails CLOSED (throw) not silent-evict; live in-window marker never dropped | `InMemoryInboxStoreEvictionShould` (in 604 pass) — safety (live marker kept) ∧ liveness (past-retention evictable) | **CONFIRMED-PASS** |
| `ojuxox` P2 | Durable workflow signal inbox: `UNIQUE(InstanceId,SignalId)` ships in DDL | `001_CreateWorkflowSignalInboxSchema.sql` (schema); saga/workflow conformance exercises the store | **MAPPED** (DDL ships; integration-tested where the workflow inbox runs) |
| `ywodwj` P2 | TimeProvider seam: scheduling reads injected clock, not `UtcNow` on decision path | `RecurringDispatchSchedulerClockShould` (in 4647 pass) — reject-past + clamp-past vs **injected clock** | **CONFIRMED-PASS** |
| `bfak2b` P3 | AwsLambda cold-start → `IOptions<AwsLambdaOptions>` (no bespoke IEnvironment; WWMD) | `AwsLambdaColdStartOptimizerShould` (318 pass) — injected-option decision path + env→option default wiring (RED if `Configure` dropped) | **CONFIRMED-PASS** |
| `lexyk5` P2 | samples README production snippet de-phantomed | `samples/11-real-world/README.md` (doc — validated at DOCS/SAMPLES phase) | **N/A-test (doc)** |

---

## Coverage summary — delivered surface

```
                         Total   CONFIRMED   MAPPED   N/A-doc
Wave-0 P0:                 4         3          1         0
Lane-G P1/P2:              8         8          0         0
.NET P2/P3:               5         3          1         1
─────────────────────────────────────────────────────────────
DELIVERED:               17        14          2         1
```

- **.NET regression:** 5601 passed / 0 failed / 0 skipped (4 projects).
- **Harness self-tests:** 5/5 GREEN + 2 gate self-tests (5uhy2j, qcizyz) re-run at VERIFY.
- Every safety AC has its paired liveness arm (testing-patterns §3); non-vacuity mutant-proven on all Lane-G gates.

## CARVED to S892 (NOT delivered — tracked, PM-ruled, not silent gaps)

- **Lane O+P (Backend, ~25):** outbox fencing/MarkFailed/CAS + projection-DI (`vmy75v`, `lz7us9`, `j1wfzu`, `dvp6ve`, `xdcr3t`, `egm9wd`, `su6232`, …) — BackendDeveloper hung through IMPLEMENT.
- **Lane T (Tests, ~14):** key-escrow M-of-N quorum (`rzr5zs`), crypto-shred honesty (`vlky2n`), tenant markers, narrow DTOs — TestsDeveloper hung through IMPLEMENT.
- **Lane A (Frontend):** any conformance/single-home beads not landed (Frontend delivered its non-overlap Lane-A work; the O+P-overlapping seams wait on Backend → S892).

These ACs are **UNMET but TRACKED** (beads remain `open`, carried to S892 PLAN). Not counted against S891's delivered-surface coverage.

## Known-tracked follow-ups (not VERIFY blockers)
- **jlo4fs (P2)** — `session-liveness-probe.sh:100` false-DEAD on alive-idle; SA-ruled fix = `poll-opcom.sh` touch `$POLL_LOG` per heartbeat (probe untouched, contract valid once mtime kept fresh).
- **7o2vuu** — shipped-ddl-sweep ERE `[ \t]` tab-match (Lane-G) — covered by the tab-indented fixture arm in `shipped-ddl-sweep.test.sh`.

## VERDICT: **FULL COVERAGE of the delivered surface.**
Every delivered bead maps to a non-vacuous covering test/gate (14 CONFIRMED-PASS, 2 MAPPED, 1 doc). No
orphaned tests. Carved lanes are tracked to S892, not silently dropped. Clear to REVIEW.
