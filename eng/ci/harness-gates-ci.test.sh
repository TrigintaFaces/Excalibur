#!/usr/bin/env bash
# harness-gates-ci.test.sh — non-vacuous control for harness-gates-ci.sh (jxp2yq guard 3).
# Proves the orchestrator's exit is HONEST: a gate that FAILS makes the orchestrator exit 1 (safety —
# no masked pass), a gate that PASSES lets it exit 0 (liveness), and a REFUSE (exit 2) is NOT a pass
# (S890 3-state). Uses the HGCI_TEST_GATE seam so it is fast + hermetic (the real slow battery is proven
# by the dogfood full run; its completeness by gate-wiring's nu00yn ARM).

set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ORCH="${HGCI_ORCH:-$HERE/harness-gates-ci.sh}"
[ -f "$ORCH" ] || { echo "FATAL: orchestrator not found: $ORCH" >&2; exit 2; }
pass=1

run_orch() {  # run_orch <HGCI_TEST_GATE value> -> echoes the orchestrator's exit code
    HGCI_TEST_GATE="$1" bash "$ORCH" >/dev/null 2>&1
    echo $?
}

echo "harness-gates-ci.test.sh — guard-3 honest-exit proof"

# A safety: a FAILING gate (exit 1) must make the orchestrator exit 1 (failure not masked)
rc="$(run_orch 'exit 1')"
if [ "$rc" -eq 1 ]; then echo "  [PASS] A safety: failing gate -> orchestrator exit 1 (no masked pass)"; else echo "  [FAIL] A safety: failing gate -> orchestrator exit $rc (expected 1)"; pass=0; fi

# B liveness: a PASSING gate (exit 0) must let the orchestrator exit 0
rc="$(run_orch 'exit 0')"
if [ "$rc" -eq 0 ]; then echo "  [PASS] B liveness: passing gate -> orchestrator exit 0"; else echo "  [FAIL] B liveness: passing gate -> orchestrator exit $rc (expected 0)"; pass=0; fi

# C REFUSE != PASS: a gate exiting 2 (cannot-evaluate/REFUSE) is a FAILURE, not a pass (S890 3-state)
rc="$(run_orch 'exit 2')"
if [ "$rc" -eq 1 ]; then echo "  [PASS] C 3-state: REFUSE (exit 2) -> orchestrator exit 1 (REFUSE != PASS)"; else echo "  [FAIL] C 3-state: REFUSE gate -> orchestrator exit $rc (expected 1)"; pass=0; fi

# D non-vacuity: the orchestrator's verdict DIFFERS on gate outcome (else the exit is a constant/masked)
fail_rc="$(run_orch 'exit 1')"; pass_rc="$(run_orch 'exit 0')"
if [ "$fail_rc" -ne "$pass_rc" ]; then echo "  [PASS] D non-vacuity: exit tracks the gate ($pass_rc pass vs $fail_rc fail) — accumulator load-bearing"; else echo "  [FAIL] D non-vacuity: exit is constant ($fail_rc) regardless of gate outcome — MASKED"; pass=0; fi

# F REFUSE is DISTINGUISHABLE from FAIL, and STILL non-zero (ag9fpw AC2a+2b, both asserted in ONE arm).
#   Asserting only the label would pass for an implementation that relabelled REFUSE and quietly made it
#   exit 0 — which is the regression this arm exists to prevent. So: the annotation must say REFUSE, the
#   summary must count it separately, AND the exit must remain non-zero. Arm C above is untouched.
refuse_out="$(HGCI_TEST_GATE='exit 2' bash "$ORCH" 2>&1)"; refuse_rc=$?
if [ "$refuse_rc" -ne 0 ] \
   && printf '%s' "$refuse_out" | grep -q 'REFUSE' \
   && printf '%s' "$refuse_out" | grep -q '1 REFUSED' \
   && ! printf '%s' "$refuse_out" | grep -q '::error::RED (2)'; then
    echo "  [PASS] F 3-state reporting: REFUSE is labelled + counted separately AND still non-zero (rc=$refuse_rc)"
else
    echo "  [FAIL] F 3-state reporting: rc=$refuse_rc; REFUSE must be labelled, counted separately, and non-zero"
    pass=0
fi

# G liveness for F: a FAIL must NOT be reported as a REFUSE (else 'distinguishable' is satisfied by
#   calling everything a refusal, which would hide real defects — the inverse of the bug being fixed).
fail_out="$(HGCI_TEST_GATE='exit 1' bash "$ORCH" 2>&1)"
if printf '%s' "$fail_out" | grep -q '::error::RED (1)' && printf '%s' "$fail_out" | grep -q '0 REFUSED'; then
    echo "  [PASS] G 3-state reporting: a real FAIL is still reported as RED, not relabelled REFUSE"
else
    echo "  [FAIL] G 3-state reporting: a failing gate was not reported as RED / was counted as refused"
    pass=0
fi

# E masking-resistance: a gate whose failing command is followed by a trivially-true tail must STILL
#    fail the orchestrator (run() uses direct arg exec + $?, never a pipe/;-tail that would mask it)
rc="$(run_orch 'false')"
if [ "$rc" -eq 1 ]; then echo "  [PASS] E masking-resistance: a bare failing command -> orchestrator exit 1"; else echo "  [FAIL] E masking-resistance: failing command masked -> exit $rc"; pass=0; fi

echo
if [ "$pass" -eq 1 ]; then echo "✅ harness-gates-ci.test.sh: ALL GREEN"; else echo "❌ harness-gates-ci.test.sh: FAIL"; fi
[ "$pass" -eq 1 ]
