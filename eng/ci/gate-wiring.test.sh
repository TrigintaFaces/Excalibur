#!/usr/bin/env bash
# gate-wiring.test.sh — non-vacuous self-test for gate-wiring.sh.
#
#   SAFETY    a gate on disk with NO caller and NO baseline entry     -> exit 1 (orphan detected)
#   LIVENESS  a gate INVOKED by a caller (workflow / hook / orchestr.) -> exit 0 (not flagged)
#   BASELINE  an un-called gate that IS listed in the baseline         -> exit 0 (accepted debt)
#   STALE     a baselined gate that is actually wired                  -> exit 0 + a NOTE (never fatal)
#   ENV       a scan root with no eng/ci                               -> exit 64 (not a silent pass)
#   REAL      the real repository tree                                 -> exit 0 (every gate wired/baselined)
#
# Every arm drives the REAL gate-wiring.sh over an ISOLATED fixture tree (its own GW_ROOT + GW_BASELINE),
# asserting an EXACT exit code — so a detector that flagged nothing (or flagged everything) is caught.
#
# Usage: bash eng/ci/gate-wiring.test.sh   (exit 0 = all green)

set -uo pipefail

GATE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/gate-wiring.sh"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PASS=0; FAIL=0
ok()  { PASS=$((PASS + 1)); printf '  PASS  %s\n' "$1"; }
bad() { FAIL=$((FAIL + 1)); printf '  FAIL  %s\n     -> %s\n' "$1" "$2" >&2; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK" 2>/dev/null || true' EXIT

# Build a fixture repo tree:
#   $1 = scenario. Writes a gate under eng/ci, optionally a caller that runs it, optionally a baseline.
make_fixture() {
    local mode="$1" dir; dir="$WORK/$mode"
    rm -rf "$dir"
    mkdir -p "$dir/eng/ci" "$dir/eng/hooks" "$dir/.github/workflows"
    # A gate under test.
    printf '#!/usr/bin/env bash\nexit 0\n' > "$dir/eng/ci/sample-thing-gate.sh"
    # Always ship the orchestrator so the caller-set is realistic (it must NOT itself be enumerated).
    printf '#!/usr/bin/env bash\n# orchestrator\n' > "$dir/eng/ci/harness-gates-ci.sh"
    : > "$dir/eng/ci/gate-wiring-baseline.txt"
    case "$mode" in
        orphan) : ;;  # no caller, no baseline entry -> must FAIL
        wired-workflow)
            printf 'jobs:\n  x:\n    steps:\n      - run: bash eng/ci/sample-thing-gate.sh\n' \
                > "$dir/.github/workflows/ci.yml" ;;
        wired-orchestrator)
            printf '#!/usr/bin/env bash\nfor t in "eng/ci/sample-thing-gate.sh" ; do bash $t; done\n' \
                > "$dir/eng/ci/harness-gates-ci.sh" ;;
        baselined)
            printf 'sample-thing-gate.sh\n' > "$dir/eng/ci/gate-wiring-baseline.txt" ;;
        stale-baseline)
            # gate IS wired AND listed in the baseline -> not fatal, but a stale NOTE
            printf 'jobs:\n  x:\n    steps:\n      - run: bash eng/ci/sample-thing-gate.sh\n' \
                > "$dir/.github/workflows/ci.yml"
            printf 'sample-thing-gate.sh\n' > "$dir/eng/ci/gate-wiring-baseline.txt" ;;
        comment-only)
            # a caller that only MENTIONS the gate in a comment must NOT count as wiring
            printf '#!/usr/bin/env bash\n# TODO: wire eng/ci/sample-thing-gate.sh someday\n' \
                > "$dir/eng/ci/harness-gates-ci.sh" ;;
        own-test-only)
            # the gate's OWN test referencing it must NOT count as a caller
            printf '#!/usr/bin/env bash\nbash eng/ci/sample-thing-gate.sh\n' \
                > "$dir/eng/ci/sample-thing-gate.test.sh" ;;
        orchestrator-lists-testfile)
            # 816-class regression: the orchestrator (a REAL caller in the caller-set) lists the gate's
            # OWN <name>.test.sh self-test in its loop, NOT the production <name>.sh. A self-test entry
            # proves the gate is non-vacuous; it does NOT run the gate on committed src. The gate's stem
            # appears immediately before the ".test.sh" suffix, so a bare-stem whole-token match would
            # falsely count it as wired. It MUST stay ORPHAN (exit 1). Paired liveness: the
            # `wired-orchestrator` arm above (same orchestrator listing the real <name>.sh -> exit 0).
            printf '#!/usr/bin/env bash\nfor t in "eng/ci/sample-thing-gate.test.sh" ; do bash $t; done\n' \
                > "$dir/eng/ci/harness-gates-ci.sh" ;;
    esac
    printf '%s' "$dir"
}

run_gate() { GW_ROOT="$1" GW_BASELINE="$1/eng/ci/gate-wiring-baseline.txt" bash "$GATE" >/dev/null 2>&1; RC=$?; }

echo "gate-wiring.sh — self-test"
echo

d="$(make_fixture orphan)";             run_gate "$d"
[ "$RC" -eq 1 ] && ok "safety: a gate with no caller and no baseline entry is REJECTED (exit 1)" \
                || bad "safety: an un-called, un-baselined gate must exit 1" "got exit $RC"

d="$(make_fixture wired-workflow)";     run_gate "$d"
[ "$RC" -eq 0 ] && ok "liveness: a gate invoked by a workflow is accepted (exit 0)" \
                || bad "liveness: a workflow-wired gate must exit 0" "got exit $RC"

d="$(make_fixture wired-orchestrator)"; run_gate "$d"
[ "$RC" -eq 0 ] && ok "liveness: a gate invoked via the orchestrator's loop list is accepted (exit 0)" \
                || bad "liveness: an orchestrator-wired gate must exit 0" "got exit $RC"

d="$(make_fixture baselined)";          run_gate "$d"
[ "$RC" -eq 0 ] && ok "baseline: an un-called gate listed in the baseline is accepted (exit 0)" \
                || bad "baseline: a baselined orphan must exit 0" "got exit $RC"

d="$(make_fixture stale-baseline)";     run_gate "$d"
[ "$RC" -eq 0 ] && ok "stale: a wired gate still in the baseline is non-fatal (exit 0)" \
                || bad "stale: a stale baseline entry must not fail" "got exit $RC"

d="$(make_fixture comment-only)";       run_gate "$d"
[ "$RC" -eq 1 ] && ok "safety: a comment-only mention does NOT count as a caller (exit 1)" \
                || bad "safety: a commented-out caller must not satisfy wiring" "got exit $RC"

d="$(make_fixture own-test-only)";      run_gate "$d"
[ "$RC" -eq 1 ] && ok "safety: a gate's OWN test is not a caller of it (exit 1)" \
                || bad "safety: a self-test must not count as production wiring" "got exit $RC"

d="$(make_fixture orchestrator-lists-testfile)"; run_gate "$d"
[ "$RC" -eq 1 ] && ok "safety(816): a real caller naming only the gate's <name>.test.sh does NOT wire it (exit 1)" \
                || bad "safety(816): a .test.sh self-test entry in a real caller must not count as wiring" "got exit $RC"

GW_ROOT="$WORK/nonexistent-root" bash "$GATE" >/dev/null 2>&1; RC=$?
[ "$RC" -eq 64 ] && ok "env: a scan root with no eng/ci exits E_ENV(64), not a silent pass" \
                 || bad "env: a missing eng/ci must exit 64" "got exit $RC"

bash "$GATE" >/dev/null 2>&1; RC=$?
[ "$RC" -eq 0 ] && ok "real: every gate in the real repo is wired or baselined (exit 0)" \
                || bad "real: the real repo must be clean (wire the gate or baseline it)" "got exit $RC"

echo
printf 'passed %d · failed %d\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ] || exit 1
