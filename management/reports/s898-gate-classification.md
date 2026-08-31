# S898 `ki5vjb` — Gate Audit + 3-Bucket Classification (Lane F1)

**Keystone:** audit every gate → classify (RELOCATE-TO-MIRROR-CI / STAYS-LOCAL-PRIVATE / STAYS-LOCAL-FAST)
per GUIDE Ruling 1 → relocate the belongs-in-CI set to required mirror checks → shrink pre-commit.
Baseline HEAD `80e0117838`. `core.hooksPath = eng/hooks` (git runs the working-tree pre-commit **live**).

## Mirror scope (settled from ground truth, `OPERATOR-DIRECTIVES.md:23-60,137-138`)
Mirrored (public, CI runs here): `src/`, `eng/`, `.github/workflows/`, **docs site, samples, tests**
("other vital folders" / "what a consumer can see").
**Private (NOT mirrored, CI cannot run here):** `.claude/`, `.dts/`, `.beads/`, `management/`.
→ **Discriminator:** a gate that reads any private path CANNOT be a mirror check. Everything else can.

## Classification

### Bucket 1 — RELOCATE-TO-MIRROR-CI (reads only mirrored paths → required PR check on the mirror)
| gate | reads | wired today? | action |
|---|---|---|---|
| duplicate-xml-doc-tags.sh | src | quality-gates.yml (blocking) | ✅ already a mirror check — confirm required-status |
| xml-doc-cref-gate.sh | src `/bin/**/*.xml` | quality-gates.yml (**advisory `\|\| true`**) | ARM to blocking after cref cleanup closes (tracked P2, S897) |
| spa-gate.sh | src (Dashboard.Spa) | ci.yml | ✅ already a mirror check |
| docs-csharp-phantom-gate.sh | docs/, docs-site/ | ci.yml | ✅ already a mirror check |
| **shipped-ddl-sweep.sh** | docs-site/, samples/, src | **only `.test.sh` runs; SWEEP runs NOWHERE** (`973uo7`,`5ur08m`,`fp91gw`) | **RELOCATE — wire the real sweep as a required check** ⚠ Lane-C/F2 overlap |
| **vacuous-validateonstart-gate.sh** | src | not wired to any workflow | **RELOCATE** |
| **orphan-test-project-gate.sh** | tests, eng/ci/shards | not wired | **RELOCATE** |
| **task-delay-syncwait-gate.sh** | tests | not wired | **RELOCATE** |
| **lockfile-drift-gate.sh** | `**/packages.lock.json` (mostly src) | not wired | **RELOCATE** |
| **build-samples.sh** | samples/ | check samples-ci.yml | RELOCATE/confirm |
| **no-internal-refs-gate.sh** | src/docs-site/samples scan; `.beads` corpus is OPTIONAL | **zero callers** (`jb4qyd`) | **RELOCATE (no refactor)** — pass 1 pattern-ERE (`:246`) is mirror-clean; pass 2 bare-id runs only if `.beads` present (`:247`), degrades gracefully. Just WIRE it (+ honesty log for pattern-only mode on mirror) |
| pre-commit-dispatch-gate.sh | eng/hooks/pre-commit | harness-gates-ci | ✅ mirror-runnable (hook file is on the mirror) |

> **4-bucket model (operator, #42898).** Test: *"if this is violated, must the change be BLOCKED from
> shipping?"* Yes → **Bucket 1 (RELOCATE-TO-CI)**. No, but it's useful analysis → **Bucket 3 (GATE→TOOL)**.
> Secrets only → **Bucket 2**. No value even as a tool → **Bucket 4 (RETIRE)**. Private-reading gates that
> can't run on the mirror get a *second life* as tools, not stranded as un-escapable local gates.

### Bucket 2 — STAYS-LOCAL-FAST (the ONE local gate: secrets must never reach the public mirror)
| gate | note |
|---|---|
| staged-secret-scan.sh | the one true **pre-copy** gate (Ruling 1.5); authoritative secret enforcement on the mirror = GitHub push-protection |

### Bucket 3 — GATE → TOOL (useful ANALYSIS, must NOT block → `.claude/tools/`, drop `exit 1`, keep the analysis)
| gate | why TOOL not CI (blocks shipping? → NO) | note |
|---|---|---|
| wwmd-reinvention-smells.sh | heuristic BCL-reinvention smell; false-positives expected, human-triaged | already advisory (`\|\| true`) — make it a first-class tool |
| f5-sweep.sh | pre-emptive stale-sibling finder; the *actual* failure (stale test) is caught by build/test in CI anyway | agents already run it at IMPLEMENT — first-class tool alongside `query-deps` |
| premise-triage.sh | freeze-aware bug-premise **decision** helper; self-declared NOT-a-gate | tool |
| p0-denominator.sh | reporting/metrics; self-declared NOT-a-gate | tool |
| validate-sprint-plan.sh | planning helper (reads `management/`, private) | tool |
| bd-status-tokens.sh | bd-token hygiene analysis (reads `.claude`) | tool |
| tracked-artifact-gate.sh | meta-analysis over `.claude/harness` scripts (private) | tool |
| fabricated-utc-gate.sh | fabricated-timestamp analysis; targets agent-mesh scripts, reads `.claude` (private) | tool — **⚠ judgment:** if the `src/` slice should hard-block, split that part to CI (flag SA) |
| blocking-bead-gate.sh | enforces "don't commit to a P0-blocked path" (reads `.beads`, private) | **⚠ judgment call — flag PM/SA:** it's currently *blocking* agent-coordination; operator says secret-scan is the ONE local gate, so it downgrades to a non-blocking tool ("is this path blocked?") OR retires. Not a unilateral call. |

### Bucket 4 — RETIRE (no value even as a tool)
None identified — every remaining gate carries analysis value; revisit any that prove redundant once the tools land.

### Harness infrastructure (NOT leaf gates — the CI gate-running machinery; stays as-is)
| script | role |
|---|---|
| harness-gates-ci.sh | the CI orchestrator (runs the non-vacuity battery on the mirror); **VERIFIED mirror-correct** (`:100-138` — private-only locks pruned, `eng/ci/` fallback for the 4 published locks; does NOT false-PASS) |
| gate-wiring.sh | structural meta-gate ("every gate has a caller"); runs inside the battery. Reads `.claude/harness` for caller-enumeration — acceptable (caller-completeness, not enforcement of shipped artifact) |
| pre-commit-dispatch-gate.sh | hook-honesty meta-check; reads `eng/hooks/pre-commit` (mirrored); already wired in the battery → effectively Bucket 1 |

## Headline findings (the "gates that don't gate" = Lane F1 core)
1. **Several gates have only their `.test.sh`/self-test wired** (proving the gate's *logic* on fixtures) while
   the **actual sweep runs nowhere on committed content**. The **must-block** ones → Bucket 1 (RELOCATE-TO-CI):
   shipped-ddl-sweep, vacuous-validateonstart, orphan-test-project, task-delay-syncwait, lockfile-drift,
   no-internal-refs, build-samples. The **advisory/analysis** ones → Bucket 3 (TOOL): f5-sweep, wwmd-smells.
   Self-test-green ≠ gate-runs (`gate-full-guard-suite §S895`: execute the gate, don't read it). **This is the
   false-safety class.**
2. ~~`harness-gates-ci.sh` invoked on the mirror but depends on `.claude/harness`~~ — **RETRACTED after read
   (`:100-138`): it is deliberately mirror-correct** (private-only locks removed from enumeration; `eng/ci/`
   fallback for the 4 published locks). Not a finding.
3. ~~`no-internal-refs-gate.sh` needs a `.beads`-independent refactor before it can relocate~~ — **CORRECTED
   after reading `:246-248`: NO refactor needed.** Pass 1 (pattern-ERE) is self-sufficient and mirror-clean;
   pass 2 (bare-id) runs only when `.beads` is present and degrades gracefully to a no-op on the mirror. It is
   **wire-ready today** (pattern-only on the mirror; bare-id stays local where the private corpus lives). The
   only polish is an explicit "pattern-only mode" log so the local-only bare-id coverage isn't a *silent* drop.

## Coordination / ownership (single-actor per `coordinate-before-parallel-work`)
- **shipped-ddl-sweep** appears in BOTH F1 (relocate) and F2 (`FrontendDeveloper`: "shipped-DDL sweep") and my
  Lane-C shipped-DDL workstream → **needs one owner for the script edit + one owner for the wiring.**
- **pre-commit hook shrink** (`qu40vf`, part of THIS keystone step 4) vs F2's "hook lean-out" → **one actor on
  `eng/hooks/pre-commit`.** Proposed: **F1 (me) owns the hook shrink** (it's ki5vjb step 4); F2 owns coverage gates.
- Every relocated gate ships a **planted-violation RED proof (safety+liveness)** — coordinate with **TestsDeveloper (E)**
  so the non-vacuity proof is author≠impl where the gate is load-bearing.

## Sequencing (self-referential risk, Ruling 1)
1. Land this map. 2. Wire the **non-overlapping** RELOCATE gates as required mirror checks (each with its self-test
as the CI non-vacuity proof). 3. Refactor no-internal-refs off `.beads`. 4. Shrink pre-commit **LAST**, after
relocations are proven green on the mirror (never rip up the hook before its replacements enforce).
