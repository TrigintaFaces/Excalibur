#!/usr/bin/env bash
# harness-gates-ci.sh — CI-authoritative orchestrator for the gate self-tests, harness locks, and the
# gate-wiring meta-gate. The shipped workflow calls ONLY this one generic-named
# script: `run: bash eng/ci/harness-gates-ci.sh`.
#
# WHY A SCRIPT AND NOT INLINE WORKFLOW STEPS: `.github/workflows/**` is MIRRORED to a public repo and
# scanned by `no-beads-in-workflows` — which forbids the literal tokens `gate-wiring`, `premise-triage`,
# `bd-*`, `.beads/`, `.claude/` in any shipped workflow (they name private tracker/gate machinery that
# does not exist downstream). Naming those scripts directly in the YAML freezes EVERY commit (the gate
# globs the working tree). So the private refs live HERE, in eng/ci/ — which is neither workflow-scanned
# nor mirror-excluded — and the workflow names only this generic orchestrator. (Learned from a
# tree-freeze post-mortem; the 3 architectural-review guards it produced are enforced below.)
#
# WHY CI IS AUTHORITATIVE: these gates previously ran ONLY in eng/hooks/pre-commit — bypassable
# (--no-verify), absent on a fresh clone, and a drift-prone installed COPY. Running them in CI makes an
# unwired lock / a vacuous gate / a broken installer fail the PR un-bypassably.
#
# GUARD 3 (no masked pass — 3-state, no-pipe-masked-commit): every gate's REAL exit is captured
# directly (`rc=$?` on the next line, never through a pipe/`;`-tail), accumulated, and ANY failure makes
# THIS script exit 1 → the CI job goes red. A wired gate must be unable to report a PASS it did not earn.
# The paired self-test (harness-gates-ci.test.sh) plants a failing gate and asserts this script exits 1.
#
# Exit: 0 = every gate/lock/assert passed | 1 = at least one failed (names printed) | 64 = environment error.

set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." 2>/dev/null && pwd)"
[ -n "$REPO_ROOT" ] && cd "$REPO_ROOT" || { echo "harness-gates-ci: cannot locate repo root" >&2; exit 64; }

# Overridable so the self-test can point the orchestrator at a temp fixture tree (guard-3 non-vacuity)
# without mutating the working copy.

fails=0
ran=0
refused=0
run() {  # run <label> <command...> — captures the REAL exit (no pipe/;-mask), accumulates on failure.
    local label="$1"; shift
    echo "── $label"
    "$@"
    local rc=$?
    ran=$((ran + 1))
    # THREE states, reported distinctly — but REFUSE stays NON-ZERO: a gate that could not be
    # evaluated must never read as a pass.
    #
    # The defect was never fatality; it was DISTINGUISHABILITY. A gate that could not evaluate (exit 2:
    # missing tool, unreadable input, no baseline) was annotated ::error::RED, identical to a gate that
    # found a real defect. So a REFUSE sent someone to investigate healthy code, or taught the team to
    # discount RED annotations generally — and nobody learned the gate DID NOT LOOK.
    #
    # What is deliberately NOT changed: a REFUSE still drives this orchestrator to a non-zero exit.
    # Making it non-fatal would mean exiting 0 on a gate that never evaluated — a PASS it did not earn,
    # the exact defect the three-state model exists to prevent. arm C of harness-gates-ci.test.sh
    # asserts exit 1 on a raw `exit 2` and remains true, literally unmodified.
    #
    # A well-formed gate that owns a committed baseline (see shipped-ddl-sweep-gate.sh) resolves its own
    # refusals BEFORE returning: baselined REFUSE -> 0, unbaselined REFUSE -> 1. It hands us 0 or 1 and
    # never reaches this branch. Reaching it means the gate has NOT resolved its refusals, and non-zero
    # is the correct answer.
    case "$rc" in
        0) echo "   GREEN: $label" ;;
        2) echo "::warning::REFUSE ($rc): $label — the gate COULD NOT EVALUATE. This is not a defect it found, and not a clean bill of health."
           refused=$((refused + 1)) ;;
        *) echo "::error::RED ($rc): $label"
           fails=$((fails + 1)) ;;
    esac
}

echo "==== harness-gates-ci: CI-authoritative gate wiring ===="

# SELF-TEST SEAM (guard 3): when HGCI_TEST_GATE is set, run ONLY that one controllable command as the
# gate battery and propagate its outcome. This lets harness-gates-ci.test.sh prove the exit is HONEST
# (a failing gate -> this script exits 1; a passing gate -> exit 0) hermetically, without the slow real
# battery. Unset in production, so it never affects the real CI run. The real battery's completeness is
# enforced by gate-wiring's caller-of-record ARM (guard 2); its correctness by the dogfood full run.
if [ -n "${HGCI_TEST_GATE:-}" ]; then
    run "self-test gate" bash -c "$HGCI_TEST_GATE"
    echo "==== harness-gates-ci (test mode): $ran check(s) EXECUTED, $((ran - refused)) reached a VERDICT, $fails FAILED, $refused REFUSED ===="
    # REFUSE must be checked HERE too, not only in the production summary below. When three-state
    # reporting was added, this branch still tested `$fails` alone — and because a REFUSE no longer
    # increments `fails`, a refusing gate exited 0 through this path. That is precisely the
    # "PASS it did not earn" regression the three-state model forbids, reintroduced by the change that
    # was meant to improve it. Arm C and arm F both caught it; this line is why they stay.
    [ "$refused" -eq 0 ] || { echo "::error::harness-gates-ci: $refused gate(s) COULD NOT EVALUATE — not a pass."; exit 1; }
    [ "$fails" -eq 0 ] || { echo "::error::harness-gates-ci: $fails FAILED"; exit 1; }
    echo "harness-gates-ci (test mode): GREEN."
    exit 0
fi

# ── 1. STRUCTURAL meta-gate (gate-wiring): every enforcement gate under eng/ci + eng/hooks must have a
#       CALLER that runs it. A gate nobody invokes enforces nothing — an advertised control that is
#       silently inert. gate-wiring.sh enumerates the gates, resolves each one's caller (a workflow, a
#       git hook, or THIS orchestrator's loop lists), and fails on any gate that has none and is not in
#       the accepted known-orphan baseline. It is self-covering: gate-wiring.sh is itself a gate, and it
#       is wired precisely by the invocation on the next line. Non-vacuity: gate-wiring.test.sh (run in
#       section 3) plants an unwired fixture gate and asserts this detector rejects it.
run "gate-wiring meta-gate" bash eng/ci/gate-wiring.sh

# ── 1b. WALL-CLOCK NESTED DEADLINES. Python, so it cannot live in the bash-only loop below.
#       Blocks on ONE shape: an inner deadline shorter than a wait in the same method, which cannot
#       be correct -- the deadline can stop that wait from ever completing, and the test then fails
#       reporting a stopwatch rather than a defect. Two of those shipped red mains on 2026-08-07.
#       Short and long deadlines are reported and NOT enforced: they are unreviewed candidates, and
#       blocking on those would make this a gate people route around.
run "wallclock-deadline-sweep self-test" python3 eng/ci/wallclock-deadline-sweep.py --self-test
run "wallclock-deadline-sweep (real tree)" python3 eng/ci/wallclock-deadline-sweep.py --gate

# ── 1c. CONTAINER IMAGE PINNING. A :latest tag, or no tag, means the image can change completely
#       between two runs of the SAME commit -- and the resulting failure is indistinguishable from a
#       flake, because nothing in the repository changed. Ratcheted rather than absolute: digests are
#       the stronger pin but would ROT here, since dependabot's docker ecosystem reads Dockerfiles and
#       compose files and cannot parse a C# string. The bar is a concrete tag.
run "container-image-pinning self-test" python3 eng/ci/container-image-pinning-gate.py --self-test
run "container-image-pinning (real tree)" python3 eng/ci/container-image-pinning-gate.py --gate

# ── 1d. RELEASE RECEIPT non-vacuity. The receipt itself only runs during a release, which is rare
#       enough that a self-test gated behind one would sit unproven for months. Its arms are
#       hermetic, so they run here on every push instead.
run "release-receipt self-test" python3 eng/ci/release-receipt.py --self-test

# ── 3. Run the gate NON-VACUITY locks (authoritative in CI, not just pre-commit). All hermetic.
for t in \
    "eng/ci/gate-wiring.test.sh" \
    "eng/ci/f5-sweep.sh --self-test" \
    "eng/ci/f5-sweep.test.sh" \
    "eng/ci/assert-tests-executed.sh --self-test" \
    "eng/ci/run-filtered-tests.test.sh" \
    "eng/ci/premise-triage.test.sh" \
    "eng/ci/shipped-ddl-sweep.test.sh" \
    "eng/ci/shipped-ddl-sweep-gate.sh --self-test" \
    "eng/ci/sql-predicate-gate.test.sh" \
    "eng/ci/tenant-range-op-coverage-gate.sh --self-test" \
    "eng/ci/tenant-range-op-coverage-gate.test.sh" \
    "eng/ci/real-infra-tenant-gate.test.sh" \
    "eng/ci/orphaned-constant-gate.test.sh" \
    "eng/ci/inbox-decorator-seam-gate.sh --self-test" \
    "eng/ci/inbox-decorator-seam-gate.test.sh" \
    "eng/ci/package-metadata-gate.test.sh" \
    "eng/ci/orphan-test-project-gate.test.sh" \
    "eng/ci/build-entrypoint-compose.test.sh" \
    "eng/ci/integration-shard-partition-gate.sh --self-test" \
    "eng/ci/release-test-verdict-gate.sh --self-test" \
    "eng/ci/lockfile-drift-gate.test.sh" \
    "eng/ci/task-delay-syncwait-gate.test.sh" \
    "eng/ci/docs-csharp-extract.test.sh" \
    "eng/ci/pre-commit-dispatch-gate.test.sh" \
    "eng/ci/shard-hang-timeout-gate.test.sh" \
    "eng/ci/full-ci-shard-completeness.sh --self-test" \
    "eng/ci/cosmos-fixture-pattern-gate.sh --self-test" \
    "eng/ci/cosmos-fixture-pattern-gate.sh" \
    "eng/ci/integration-serial-runner-gate.sh --self-test" \
    "eng/ci/integration-serial-runner-gate.sh" \
    "eng/ci/unconditional-skip-ratchet.sh --self-test" \
    "eng/ci/unconditional-skip-ratchet.sh" \
    "eng/ci/committed-sha-build-gate.test.sh" \
    "eng/ci/aot-publish-validation-exit.test.sh" \
    "eng/ci/assert-compiled-not-skipped.test.sh" \
    "eng/hooks/verify-hooks-current.test.sh" ; do
    # Two conditions before adding an entry here, both learned the hard way:
    #
    #  1. It MUST be committed. A path that exists only in someone's working tree resolves
    #     fine locally and is unresolvable on a clean checkout, where it becomes an
    #     ::error:: and reds the battery -- the failure this list shipped with for months.
    #
    #  2. Its INPUTS must exist wherever this battery runs. A gate whose inputs live outside
    #     the published subset finds nothing, REFUSEs (correctly -- finding no inputs is not a
    #     clean bill of health), and reds the battery for a non-defect. Check what the gate
    #     reads, not just whether the gate itself is present.
    # shellcheck disable=SC2086 — intentional word-split: "<script> --flag"
    run "self-test: $t" bash $t
done

# ── 4. Run the HERMETIC harness locks (this orchestrator is their CI caller-of-record — the wire
#       half of that pairing). Locks needing a live bd daemon are excluded (tracked debt) so CI stays
#       hermetic. The enumeration below is the point gate-wiring's caller-of-record ARM verifies (guard 2).
# SCOPE: only locks whose subject is PUBLISHED. This battery runs where CI runs, and CI runs on the
# mirrored copy of this repository — which carries eng/** and .github/** but NOT .claude/**.
#
# Six locks were removed from this list because their SUBJECTS are agent-mesh / internal-process
# tooling that lives only under .claude/** and is deliberately not published:
#
#     blocking-bead-gate · reports-gitignore · session-collision-guard
#     poll-opcom-singleton · session-liveness-probe · ring-repair
#
# They are NOT deleted and NOT unguarded — each still has its lock next to the thing it guards and
# still runs locally. They were removed from THIS enumeration because CI cannot see their subjects
# by design, so listing them made the battery red on six impossible expectations.
#
# DO NOT "fix" that by moving them into eng/ci. That would publish the agent mesh to a public
# repository — the operator's directive is explicit that anything in eng/ci is published and that
# agent-mesh tooling does not belong there. The enumeration was wrong, not the file locations.
# If you are here because a lock looks "missing", it is not missing; it is out of scope for CI.
for l in \
    duplicate-xml-doc-tags \
    staged-secret-scan \
    no-beads-in-workflows ; do
    # Resolve from eng/ci as well as .claude/harness, for BOTH artifact kinds. eng/ci is the
    # mirrored path; .claude/** is not guaranteed to travel with the source we publish, so a
    # gate whose lock lives only under .claude/** grades nothing wherever that dir is absent.
    # The two kinds must stay symmetric: an eng/ci fallback for locks but not for tests means
    # a lock can be relocated to safety and a test cannot.
    lock=".claude/harness/$l.harness-lock.sh"
    [ -f "$lock" ] || lock="eng/ci/$l.harness-lock.sh"
    test_sh=".claude/harness/$l.test.sh"
    [ -f "$test_sh" ] || test_sh="eng/ci/$l.test.sh"
    if [ -f "$lock" ]; then
        run "harness-lock: $l" bash "$lock"
    elif [ -f "$test_sh" ]; then
        run "harness-test: $l" bash "$test_sh"
    else
        echo "::error::harness lock/test not found for: $l"
        fails=$((fails + 1))
    fi
done

# ── 5. Assert the hook-drift check is functional (verify-hooks-current). HERMETIC + safe everywhere:
#       simulate an install into a TEMP dir (never touch the real .git/hooks in a local run), assert
#       --check passes on a current copy and FAILS on drift (non-vacuity). Under CI only (ephemeral
#       runner) also smoke the REAL installer.
_installer_assert() {
    local tmp; tmp="$(mktemp -d)"; local rc
    cp eng/hooks/pre-commit "$tmp/pre-commit" 2>/dev/null || { echo "cannot copy canonical hook"; rm -rf "$tmp"; return 1; }
    HOOKS_DEST_DIR="$tmp" bash eng/hooks/verify-hooks-current.sh --check >/dev/null 2>&1; rc=$?
    if [ "$rc" -ne 0 ]; then echo "verify --check FAILED on a current copy (rc=$rc)"; rm -rf "$tmp"; return 1; fi
    printf '# planted drift\n' >> "$tmp/pre-commit"
    HOOKS_DEST_DIR="$tmp" bash eng/hooks/verify-hooks-current.sh --check >/dev/null 2>&1; rc=$?
    rm -rf "$tmp"
    if [ "$rc" -eq 0 ]; then echo "verify --check PASSED on drift — the check is VACUOUS"; return 1; fi
    # CI-only real installer smoke (ephemeral .git/hooks; never run locally where it would clobber)
    if [ "${CI:-}" = "true" ]; then
        bash eng/hooks/install-hooks.sh >/dev/null 2>&1 || { echo "install-hooks.sh failed"; return 1; }
        bash eng/hooks/verify-hooks-current.sh --check >/dev/null 2>&1 || { echo "installer produced drifted hooks"; return 1; }
    fi
    return 0
}
run "installer-assert (verify-hooks-current non-vacuity)" _installer_assert

# ── 6. FUNCTIONAL audit: run the dispatch-honesty gate against the REAL eng/hooks/pre-commit,
#       not just its hermetic self-test. This is the anti-inert wiring (the self-test-only class: a gate wired
#       only by a self-test trigger validates itself and nothing else, forever). If any capture-then-
#       branch site in the real hook reads a non-verdict exit as PASS, the gate exits non-zero and this
#       job goes red — CI-authoritative, un-bypassable.
run "pre-commit-dispatch-honesty (real hook)" bash eng/ci/pre-commit-dispatch-gate.sh

# FUNCTIONAL audit: run the tenant-range-op coverage gate against the REAL src/Excalibur tree, not
# just its hermetic self-test above. Self-test-only wiring validates the gate's detection logic but
# never casts the regression net over production source (the self-test-only class: a gate wired only by a
# self-test trigger validates itself and nothing else, forever). This bare run scans src and fails
# the job if any tenant-partitioned range DELETE/UPDATE is uncurated or flows no TenantScope.
run "tenant-range-op coverage (real src tree)" bash eng/ci/tenant-range-op-coverage-gate.sh

# FUNCTIONAL: run the shipped-DDL sweep against the REAL repo (docs-site + samples), not just its
# hermetic self-test above. The sweep existed but was invoked by nothing against the tree, so consumer-
# facing DDL drift (a CREATE TABLE a consumer runs verbatim) had no backstop — the same wired-by-nothing
# class the tenant-range gate above also fixes. The wrapper maps the sweep's THREE states honestly: a real drift
# (a mapped table's shipped DDL no longer matches its write path) reds; a REFUSE (a table it could not
# evaluate) is compared to the committed SET baseline, so a NEW unmapped table reds while the tracked
# baseline does not; and a liveness arm reds if the sweep produced no report at all (a gate that never
# runs has a vacuously-satisfied baseline).
run "shipped-ddl-sweep (real repo, set-baselined)" bash eng/ci/shipped-ddl-sweep-gate.sh

# FUNCTIONAL: run the shard hang-timeout gate against the REAL tree. Self-test-only wiring would
# validate the detector and never cast the net over the runners and documented commands that actually
# wedge (the self-test-only class). The defect it guards is silent by construction: an unbounded
# `dotnet test <shard>.slnf` does not fail on a wedged MTP host, it consumes the phase forever, and the
# orphan it leaks then poisons LATER shards with MSB3021 build breaks wearing a test-failure exit code.
# Three states: 0 every invocation bounded · 1 an unbounded invocation exists · 2 REFUSE (scanned no
# files — a zero over an empty file set is not a clean tree).
run "shard-hang-timeout (real tree)" bash eng/ci/shard-hang-timeout-gate.sh

# The hang timeout bounds the wedge; it does NOT clean up after one. A wedged MTP host survives the
# kill of its vstest/dotnet parent and keeps an exclusive handle on the test project's output DLLs,
# so a LATER shard building that project dies MSB3021 with no Failed! line to point at. The reaper is
# the cleanup half. Its self-test is what keeps the safety property honest: the one rule that must
# never regress is "a host whose parent is still ALIVE is a running test and is never killed" —
# without it the reaper would abort the very run it protects. Self-test only; the live reap is a
# between-shards action, not a CI gate.
if command -v pwsh >/dev/null 2>&1; then
    run "self-test: reap-orphan-test-hosts" pwsh -NoProfile -File eng/ci/Reap-OrphanTestHosts.ps1 -SelfTest
else
    echo "::warning::reap-orphan-test-hosts self-test SKIPPED — pwsh not on PATH (a skip is not a pass)"
fi

# ddl-pack-completeness answers the INVERSE question to shipped-ddl-sweep, and neither can see the
# other's class. The sweep asks "does the DDL we ship declare every column the code writes?" — it
# compares DDL that IS shipped. This asks "can a consumer obtain the DDL at all?" A column-drift
# check over an unpacked .sql reports clean forever, because there is nothing shipped to compare.
# Three states: 0 every shipping package packs its DDL · 1 a package ships .sql it never packs
# · 2 REFUSE (empty population — a zero over zero packages is not a clean tree).
# NOT in the lock list above: that loop's runner is `run "self-test: $t" bash $t`, which prepends bash
# unconditionally. Every other entry there is a shell script, so the prefix is correct for them and wrong
# for this one — `bash python3 …` makes bash open the python3 BINARY as a shell script and exit 126. That
# produced a PERMANENT false RED against a gate whose self-test actually passes, on every platform, and a
# battery that reds for a non-defect teaches everyone to stop reading it. It carries its own interpreter,
# so it gets its own run call. Self-test FIRST: a gate's non-vacuity must report before its verdict is
# believed. Keep this above the real-repo run.
run "self-test: ddl-pack-completeness" python3 eng/ci/ddl-pack-completeness.py --self-test

run "ddl-pack-completeness (real repo)" python3 eng/ci/ddl-pack-completeness.py

# Report what EXECUTED, not what is configured. A battery that reaches green by dropping entries
# prints the same "0 failures" as a healthy one, so the failure count alone cannot distinguish a
# passing suite from an empty one. The executed count is what makes this line falsifiable: if a
# future edit silently shrinks the enumeration, this number drops and the drop is visible in the log.
echo "==== harness-gates-ci: $ran check(s) EXECUTED, $((ran - refused)) reached a VERDICT, $fails FAILED, $refused REFUSED ===="
[ "$ran" -gt 0 ] || { echo "::error::harness-gates-ci: 0 checks executed — an empty battery is not a pass"; exit 1; }
# REFUSED is reported separately from FAILED so a run where several gates could not
# evaluate is visible as such, rather than reading as several discovered defects. It is still non-zero:
# a battery that refused everything must never exit 0.
[ "$refused" -eq 0 ] || { echo "::error::harness-gates-ci: $refused gate(s) COULD NOT EVALUATE — non-zero because an unevaluated gate is not a pass. These are NOT $refused defects."; exit 1; }
[ "$fails" -eq 0 ] || { echo "::error::harness-gates-ci: $fails gate/lock/assert FAILED"; exit 1; }
echo "harness-gates-ci: all gates, locks, and asserts GREEN."
