# Sprint 891 — Architectural Guidance (COMPASS / GUIDE)

**Author:** SoftwareArchitect · **Phase:** GUIDE · **Baseline HEAD:** `5a44f8f3e` (code state; spec authored at `f0b2e19d1` = same code + retro/plan docs)
**Sources:** `management/sprints/sprint-891-plan.md`, `management/specs/sprint-891-spec.md`. This doc does not create a second source of truth — it pins the **3 SA seam rulings** the spec deferred to GUIDE, sets execution priority, and reinforces the non-negotiables. Per-bead premise-reconfirm stays delegated to implementers at IMPLEMENT (non-negotiable #1).

---

## Execution priority (dispatch order at IMPLEMENT)

1. **Wave-0 first — it gates everything.** The 4 P0s (`jxp2yq`, `9pxz2t`, `0p6z0v`, `hv8fjh`) + `w1u1c9` are the instruments/mesh actively degrading *this very run* (2 ProductManager hangs + a 14× poller leak happened during S891 planning). Land Wave-0 before the correctness lanes consume it. Platform owns all four.
2. **Then the correctness lanes** (O+P, A, T, Docs) in parallel — subject to the seam rulings below.

---

## SA SEAM RULINGS (the 3 the spec deferred to GUIDE)

### Seam 1 — `jxp2yq` gate-wiring topology: **CI is the authoritative home; pre-commit is a fast local mirror, never the sole home.**

**WWMD:** a gate is only trustworthy where it **cannot be silently skipped**. Pre-commit is bypassable (`--no-verify`) AND not-installed-on-a-fresh-clone (the S890 installed-vs-tracked divergence lesson). So:

- **Wire all 4 gates (f5-sweep, blocking-bead-gate, hooks-wiring/gate-wiring, premise-triage) into a CI workflow** (a dedicated `.github/workflows/gates.yml` or an existing quality job) that runs on PR/push against the branch. This is the **authoritative trigger** AC1/AC2 require: a CI log shows the gate ran; a planted violation **fails the CI job** (exit 1).
- **Commit-time gates (operate on staged content) MAY ALSO live at pre-commit** for fast local feedback — but pre-commit is a *mirror*, never the authoritative/sole home.
- **AC3 fresh-clone rule:** CI asserts the **installer** works (install-hooks into a temp dir + `--check`), and runs the gate itself in CI directly — no dependency on a developer's installed hook (per `verify-hooks-current.sh:15-19`).
- **Non-vacuity (nu00yn folds in here):** the wiring check must be RED on the unwired state and enumerate **ALL** gates including `.claude/harness/*` locks (nu00yn: gate-wiring ARM1 was blind to harness locks). A gate present but invoked by nothing must make the wiring gate FAIL.
- **ADR-worthy:** yes — a short ADR recording the topology (CI-authoritative / pre-commit-fast-mirror / installer-asserted-in-CI). Platform drafts; I review at REVIEW_ARCH. Coordinate with DocumentationWriter for the ADR record.

### Seam 2 — Lane A ↔ Lane O+P shared outbox/event-store files: **serialize by owner; behavior before structure.** (load-bearing — the flagged collision)

Metz M1 (`owxhc8` outbox mapping), M3/M13 (envelope / `TenantScopedEventStore`), Liskov L11/L12 touch **outbox/event-store mapping files owned by Lane O+P**. Ruling (coordinate-before-parallel-work + Tidy-First):

- **The O+P (Backend) owner owns those files. One editor at a time — reserve-before-edit; a reserve conflict is a STOP-and-PM-sequences, never a parallel edit.**
- **Edit order: Backend lands the outbox/event-store CORRECTNESS fix FIRST (behavior)** — fence-CAS, `MarkFailed` post-condition, etc. — reserves, lands, releases. **THEN Frontend does the audit-seam / single-injected-home REFACTOR on top (structure).** Rationale: (a) a behavioral correctness fix must not be churned/blocked by a structural refactor; (b) the refactor should restructure the **correct** (post-fix) code, not the pre-fix code.
- **The single-injected-home refactor is a STRUCTURE-only change — separate from behavior (Tidy First).** It must not change behavior; the Liskov/Metz **conformance-lock proves behavior is preserved** across the refactor. WWMD for "single injected home" = one composition-root registration site (`TryAdd`, `Decorate` for cross-cutting) — the Microsoft DI convention.
- **Files that are Lane-A-only (no outbox/event-store overlap) proceed in parallel now** — this ruling scopes ONLY the shared mapping/envelope files.

### Seam 3 — `9pxz2t` poll-opcom single-instance: **per-session single-instance guard + orphan-reap, both mesh-safe (safety ∧ liveness).**

- **Prevent new duplicates:** a **single-instance guard keyed on the session** (a lock/PID file per `session_dir`) so ≤1 poller per *live* agent. WWMD = a lockfile / named single-instance mutex.
- **Reap existing leaks:** a poller whose owning session is **provably dead** (heartbeat stale AND no session activity — the mtime-gate applied to reaping) self-exits.
- **LIVENESS guard (non-negotiable):** the mechanism must **never** kill the poller serving a *live* agent, and reaping must not race a REWAKE (composes with `0p6z0v`). Key the guard on the session, not a global lock.
- **Non-vacuity:** safety arm = plant N duplicate pollers → reaped to 1; liveness arm = the live agent's heartbeat/rewake **still fires** after capping. Both arms, or it's vacuous.

> **Great-minds panel:** considered for Seam 2 (Metz/Hickey on the single-injected-home abstraction) — **deliberately skipped at GUIDE** (cost discipline; these are ownership/order/topology rulings, not novel abstraction design). The abstraction shape is an IMPLEMENT-time detail I'll review at REVIEW_ARCH.

---

## Non-negotiables (reinforced — dispatch to all lanes at IMPLEMENT)

1. **Premise re-confirm vs committed HEAD `5a44f8f3e` per bead** — close-as-satisfied any phantom whose premise no longer reproduces (`hv8fjh` explicitly: AC0 = confirm the swallow still exists; if `pre-commit:343` already fail-louds, close-as-satisfied with evidence).
2. **Blast-radius the REAL extent** — grep the real indicator, not a proxy; **F-5 sweep on any type-contract change across ALL test projects + benchmarks + shipped DDL** (`docs-site`/`samples`).
3. **Non-skipped real-infra locks binding the DEFAULT serializer/client** for provider lanes (O+P fence-CAS on Firestore/Redis/DynamoDb/Cosmos).
4. **Structural non-vacuity for gate/guard work** — the violation inexpressible, not merely asserted; **every safety arm paired with a liveness arm.** (`7o2vuu`: regex `[[:space:]]`/literal-tab + a **tab-indented fixture** self-test arm; PR bar = `dbo.OutboxMessages` EVALUATES ok/FAIL, not REFUSE. `rzr5zs` key-escrow: fail-closed below M-of-N threshold, RED below / GREEN at threshold.)
5. **Cross-lane seam coordination** — per Seam 2 above; SA has ruled it once here.
6. **Build to these seam rulings; signal `ready_for_integration` per bead; PM is the only committer.**

---

## Per-lane next actions (at IMPLEMENT-GO)

- **Platform (G):** Wave-0 P0s first (`jxp2yq` per Seam 1, `9pxz2t` per Seam 3, `0p6z0v` investigation+mitigation, `hv8fjh` reconfirm-then-fix), then gate/harness hardening incl `7o2vuu`. Draft the gate-wiring ADR.
- **Backend (O+P):** lands the shared outbox/event-store correctness fixes FIRST (Seam 2), reserves the mapping files, signals when released. Real-infra locks binding the DEFAULT client.
- **Frontend (A):** Lane-A-only seams in parallel now; the outbox/event-store-touching refactors (Metz M1/M3/M13, Liskov L11/L12) WAIT for Backend's release, then structure-only refactor with a conformance-lock proving no behavior change.
- **Tests (T):** tenancy/crypto/erasure + `rzr5zs` quorum; also authors the cross-lane regression/conformance locks (author≠impl).
- **Docs:** doc-snippet compile fixes; owns the gate-wiring ADR record with Platform.

**Liveness:** ProductManager hung (operator-restart pending); SPEC was PM-carried inline. Wave-0 `0p6z0v`/`9pxz2t` fix the very instruments degrading the mesh. I remain seam owner for any mid-sprint architecture question and will run REVIEW_ARCH (ORACLE) on the delivered work.
