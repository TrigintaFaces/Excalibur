# S890 VERIFY — Requirement→Coverage Traceability (TRACEPOINT)

**Author:** TestsDeveloper · **Date:** 2026-07-18 · **Phase:** VERIFY (task 2631)
**Specs:** `management/specs/mini/sprint-890/lane-{A,B,C,D}-*.md`
Method: run → read → cite. Calibrated negatives (positive-control before asserting an absence).

> Convention: **CONFIRMED-PASS** = I ran the test/lock to a green result this phase. **MAPPED** = a
> covering test/lock exists (identified + non-vacuous by inspection) but was not re-run here (AV cost).
> **GAP** = no covering executable test/gate found.

---

## Lane A — Gate Honesty (BackendDeveloper)

| AC | Requirement (safety / liveness) | Coverage | Status |
|----|----------------------------------|----------|--------|
| AC-A1 `r4dzl2` P0 | Every harness gate distinguishes REFUSE from PASS (3-state at 6 pre-commit sites). Safety: exit-2 gate → commit refused. Liveness: clean → commit proceeds. | 3-state `case` pattern in `eng/hooks/pre-commit` (+241); `hooks-wiring.test.sh`, `gate-wiring.test.sh` (18-arm, real-repo integration arm) | MAPPED (gate-wiring CONFIRMED non-vacuous by review) |
| AC-A2 `svacnv` P0 | `bd` reads use `--no-daemon`. Safety: zero unflagged reads. Liveness: clean read still PASSES. | `bd-verified-write.test.sh` + `.harness-lock.sh`; `bd-flush-guard.test.sh` | CONFIRMED-PASS |
| AC-A3 `wqd1w1` P0 | Destructive-retry contained. Safety: re-measure FN rate N≥40. Liveness: no auto re-write. | `bd-verified-write.harness-lock.sh`; evidence `s890-wqd1w1-false-negative-N{50,150}.txt` | CONFIRMED-PASS |
| AC-A4 `l3g5tj` P1 | `bd-file.sh` readback no false FATALs. Safety: `--no-daemon`. Liveness: genuine miss still FATALs. | `bd-file-readback-nodaemon.harness-lock.sh` | CONFIRMED-PASS |
| AC-A5 `ckywco` P1 | Drift detector decoupled from its own source path (runs every commit via pre-push). | `hooks-wiring.test.sh`, `gate-wiring.test.sh`, `eng/hooks/pre-push` (+55) | MAPPED |

## Lane B — Tracker Durability (FrontendDeveloper)

| AC | Requirement | Coverage | Status |
|----|-------------|----------|--------|
| AC-B1 `3owddx` P1 | `bd update --notes` never destructively overwrites. Safety: overwrite non-empty w/o flag → refused. Liveness: `BD_NOTES_OVERWRITE_ACK=1` replace works. | `.claude/hooks/pre-tool-use.sh` notes-gate + `pre-tool-use.notes-gate.test.sh` | MAPPED (gate logic read-verified correct + non-vacuous; test times out only under AV) |
| AC-B2 `iz4rly` P1 | `comments.jsonl` commit cadence, read-back verified, export-not-import, explicit pathspec. | `bd-comment-clobber-guard.sh` + `.test.sh` + `.harness-lock.sh`; `bd-comment-cadence-check.sh` + `.test.sh`; `bd-export-comments.sh` | CONFIRMED-PASS (clobber-guard) / MAPPED (cadence) |

## Lane C — Hive Liveness (DocumentationWriter)

| AC | Requirement | Coverage | Status |
|----|-------------|----------|--------|
| AC-C1 `0p6z0v` P0 | Record converged finding (evidence, NOT a build task). Safety: liveness check IDs a dead agent. Liveness: does not flag a mid-turn-alive agent. | Evidence recorded in bead; mtime-gate discrimination in `verify-worker-liveness-before-dispatch.md`; launch-fault detection via session-collision-guard | MAPPED (evidence AC) |
| AC-C2 `rq6iry` P2 | 7200/86400 coupling — runtime-test the timeout, fix in the honored direction. | `poll-opcom.sh` (+35), `session-start.sh` | MAPPED |
| AC-C3 `69je5c` P1 | Directed wake revives idle session, or documented hard limit. | Documented limit (mesh detection-only, no recovery primitive) per rule updates | MAPPED (doc AC) |
| AC-C4 `w1u1c9` P1 | Buddy-ring re-link revival-safe. | `poll-opcom.sh`, ring config | MAPPED |
| AC-C5 `n5nced` P1 | Session-launch faults caught. Safety: detect dup-session. Liveness: no false-positive on single launch. Distinguish stale via mtime. | `session-collision-guard.sh` + `.test.sh` (187s) + **`session-collision-guard.harness-lock.sh` (author≠impl, 8/0)** — and **now WIRED**: REVIEW_ARCH found the guard ORPHANED (invoked by nothing → detection inert, the advertised-but-unwired class); F1/`1hf2sx` wired it into `.claude/hooks/session-start.sh` (4 refs at committed HEAD `6ee52082e`), so the launch-collision detection actually runs. | CONFIRMED-PASS (+ WIRED at HEAD) |
| AC-C6 `n00699` P1 | Mailbox-collision structural fix / documented restart. | same as C5 (guard now invoked at launch, F1 committed `6ee52082e`) | CONFIRMED-PASS (+ WIRED) |

## Lane D — Shipped Schema (SoftwareArchitect)

| AC | Requirement | Coverage | Status |
|----|-------------|----------|--------|
| AC-D1-AC1 `34k958` P1 | **Safety:** shipped `OutboxMessages` DDL includes every column the code writes (full audit, not just `error_message`). | Hand-verified: all 3 shipped DDLs (docs-site/outbox.md, OutboxPattern/README, DatabaseInitializer.cs) carry the full drain set `{FencingToken,SentAt,LastError,LastAttemptAt,LeasedAt,LeasedBy,NextAttemptAt}` = canonical `001_CreateOutboxSchema.sql`. | CONFIRMED COMPLETE |
| AC-D1-AC3 `34k958` P1 | Rule clause-add (docs-site+samples DDL scope) to `f5-cross-project-test-sweep.md`. | Clause present ("the sweep covers the DDL we SHIP"). | CONFIRMED (prose) |
| **AC-D1-AC2 `34k958` P1** | **Liveness:** extend the sweep to grep `docs-site/**`+`samples/**` DDL vs `src/**` write paths; plant-mismatch → flagged; matching pair → NOT flagged (false-positive check). | **NONE.** Calibrated searches (control: `f5-sweep.sh` has 30 `tests/` hits) find **no executable shipped-DDL sweep** in `eng/`+`.claude/` — `f5-sweep.sh` has zero `docs-site`/`samples`/`CREATE TABLE`/`.sql`; the exact phrase `5uhy2j` quotes (`UPDATE SET col`) exists **only in prose** (specs/beads/rules), never in a `.sh`/`.py`. | **❌ GAP (at VERIFY) → ✅ RESOLVED** at HEAD `6ee52082e` |

---

## VERDICT: GAPS FOUND (1 critical-to-theme, non-shipping-defect)

- **AC-D1-AC2 (P1, liveness) — UNCOVERED.** The consumer is protected *today* (AC1 corrected the DDL),
  but the **preventive enforcement** that stops the *next* docs/samples-vs-write-path drift — the entire
  point of lane D's liveness arm, and its DoD ("not merely that the sweep script exists uninvoked… the
  exact `ckywco`-class mistake this whole sprint exists to prevent, applied to a docs gate") — **is
  prose-only.** This is the sprint's own headline defect class, in the sprint's own lane D.
- **Tracking (corrected 2026-07-18 after SA+PdM ruling):** the base gap **IS tracked** — `exhkgt`
  (open, P1) = "f5-sweep not wired to a real trigger, only runs its own self-test" is the real base
  tracker; `5uhy2j` (open, P2) is the INSERT-paren-list increment. So this is category **(c) tracked**,
  NOT a silent drop. My earlier "under-tracked / only 5uhy2j" was wrong — I hadn't found `exhkgt`. The
  parked seed `.dts/pending/shipped-ddl-sweep.sh` exists (untracked, no control) = AC2 seed, not a
  deliverable. Remediation (SA-owned or PlatformDeveloper): make the sweep **executable + wired via
  `gate-wiring.sh`** with a non-vacuous safety+liveness self-test (RED on a planted omission, GREEN on a
  matching pair); independent author≠impl lock = TestsDeveloper. Build-now-vs-carry = PM's scope call.
  Do NOT close `exhkgt` as satisfied without the executable gate (the §6 error this finding is about).

- **✅ RESOLVED in-sprint (build-now) — HEAD `6ee52082e`.** PdM (AC author) ruled AC-D1-AC2 genuinely
  UNMET; SA + PM ruled BUILD-NOW. PlatformDeveloper authored the executable **`eng/ci/shipped-ddl-sweep.sh`**
  (extract shipped `CREATE TABLE` columns across `docs-site/**`+`samples/**` vs `src/**` write/read paths —
  INSERT paren-list + `UPDATE SET` + WHERE + OUTPUT; 3-state 0/1/2/3; **no suppression cap**; folds in
  `5uhy2j`) + a non-vacuous 7-arm `.test.sh` (guard-3 ARM7 production-path), **wired into `eng/hooks/pre-commit`
  (9 refs) + `gate-wiring.sh`** — the ckywco class is now closed by an executable, wired gate. TestsDeveloper
  authored the independent author≠impl **`eng/ci/shipped-ddl-sweep.harness-lock.sh`** (9·0: external 3-state
  drive [omission→FAIL+attribution / match→PASS / empty & unmapped→REFUSE] + always-PASS/always-DRIFT mutants
  rejected + guard-3 production arm F). Guard-3 SA-ruled SATISFIED for the commit. Deferred direct no-env
  `--sweep`-to-completion under AV exclusions tracked P3 `nglb0a`; gate-hardening `ayundp` (P3). All three
  verified at committed HEAD. **AC-D1-AC2 now MET, not carried.**

All other ACs map to existing, non-vacuous coverage (CONFIRMED-PASS where re-run this phase; MAPPED where
the covering test/lock exists and was verified non-vacuous by inspection).

## Independence locks added this phase (author≠impl, closing the S890 Non-negotiable)
- `session-collision-guard.harness-lock.sh` — 8/0, mutant-proven non-vacuous (C5/C6).
- `duplicate-xml-doc-tags.harness-lock.sh` — 8/0, mutant-proven non-vacuous (qcizyz).
Both UNCOMMITTED — PM to commit before REVIEW_CODE.
