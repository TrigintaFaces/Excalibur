#!/usr/bin/env bash
# Non-vacuous arms for eng/ci/perf-regression-check.ps1.
#
# The gate under test replaced an inline workflow snippet that printed
# "No performance regressions detected" and exited 0 when it had compared
# nothing at all. These arms bind the three-state contract so that behaviour
# cannot return:
#
#   SAFETY   a real regression FAILS                              exit 1
#   LIVENESS a clean comparison PASSES                            exit 0
#   REFUSE   every path that compares nothing exits 2, not 0      exit 2
#
# The liveness arm is the one that matters most here. A gate rewritten to be
# strict is trivially "safe" by refusing everything; without an arm proving a
# clean run still passes, we would have swapped a gate that never fails for a
# gate that never passes.
#
# Exit codes are captured directly ($? on the line after the call, never through
# a pipe) because a pipeline reports the exit of its LAST command, which would
# silently mask the very value under test.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# PERF_GATE_PATH exists so these arms can be run against a deliberately mutated
# copy of the gate. A suite that has never been shown to go RED is not evidence;
# see the mutation proof in the bead. CI leaves this unset.
GATE="${PERF_GATE_PATH:-$SCRIPT_DIR/perf-regression-check.ps1}"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

PASSED=0
FAILED=0
EXECUTED=0

pass() { printf '  \033[0;32mPASS\033[0m  %s\n' "$1"; PASSED=$((PASSED + 1)); }
fail() { printf '  \033[0;31mFAIL\033[0m  %s\n' "$1"; FAILED=$((FAILED + 1)); }

# assert_exit <expected> <label> <results-dir> <baselines-file>
assert_exit() {
	local expected="$1" label="$2" results="$3" baselines="$4"
	EXECUTED=$((EXECUTED + 1))
	pwsh -NoProfile -File "$GATE" -ResultsPath "$results" -BaselinesPath "$baselines" >"$WORK/out.log" 2>&1
	local actual=$?
	if [ "$actual" -eq "$expected" ]; then
		pass "$label (exit $actual)"
	else
		fail "$label — expected exit $expected, got $actual"
		sed 's/^/        /' "$WORK/out.log" | tail -6
	fi
}

make_baselines() { # <path> <meanNs>
	cat >"$1" <<EOF
{ "regressionThreshold": 0.10,
  "baselines": { "dispatch": { "Bench_A": { "meanNs": $2, "allocatedBytes": 0 } } } }
EOF
}

make_result() { # <dir> <method> <meanNs>
	mkdir -p "$1"
	cat >"$1/x-report.json" <<EOF
{ "Benchmarks": [ { "Method": "$2", "Statistics": { "Mean": $3 } } ] }
EOF
}

echo "perf-regression-check.ps1 — three-state contract"
echo

command -v pwsh >/dev/null 2>&1 || { echo "  REFUSE: pwsh not on PATH; cannot execute the gate." >&2; exit 2; }

BASE="$WORK/baselines.json"
make_baselines "$BASE" 100

# ---- SAFETY: a real regression must FAIL -------------------------------------------
make_result "$WORK/regressed" "Bench_A" 200     # +100%, threshold 10%
assert_exit 1 "SAFETY: 100% slower than baseline FAILS" "$WORK/regressed" "$BASE"

# ---- LIVENESS: a clean comparison must PASS ----------------------------------------
# Without this arm a gate that refuses everything would look correct.
make_result "$WORK/clean" "Bench_A" 101         # +1%, under threshold
assert_exit 0 "LIVENESS: 1% slower still PASSES" "$WORK/clean" "$BASE"

make_result "$WORK/faster" "Bench_A" 50         # improvement
assert_exit 0 "LIVENESS: an improvement PASSES" "$WORK/faster" "$BASE"

# ---- REFUSE: the four paths that previously reported a false PASS ------------------
mkdir -p "$WORK/empty"
assert_exit 2 "REFUSE: zero result files (the originally filed defect)" "$WORK/empty" "$BASE"

assert_exit 2 "REFUSE: baselines file missing" "$WORK/clean" "$WORK/does-not-exist.json"

echo '{ "regressionThreshold": 0.10, "baselines": {} }' >"$WORK/empty-baselines.json"
assert_exit 2 "REFUSE: baselines file has zero entries" "$WORK/clean" "$WORK/empty-baselines.json"

make_result "$WORK/renamed" "Bench_RENAMED" 100  # benchmark exists, matches no baseline
assert_exit 2 "REFUSE: results present but nothing matched a baseline" "$WORK/renamed" "$BASE"

# ---- Boundary: exactly at the threshold is not a regression ------------------------
make_result "$WORK/exact" "Bench_A" 110          # exactly +10%, must not FAIL
assert_exit 0 "BOUNDARY: exactly at threshold does not FAIL" "$WORK/exact" "$BASE"

# ---- Self-test: prove the harness itself can report a failure ----------------------
# A test file whose assertions cannot fail is the same defect as the gate it guards.
EXECUTED=$((EXECUTED + 1))
pwsh -NoProfile -File "$GATE" -ResultsPath "$WORK/regressed" -BaselinesPath "$BASE" >/dev/null 2>&1
if [ $? -eq 0 ]; then
	fail "SELF-TEST: a known regression reported PASS — the harness is not discriminating"
else
	pass "SELF-TEST: harness distinguishes FAIL from PASS on known input"
fi

echo
echo "  checks EXECUTED: $EXECUTED   passed: $PASSED   failed: $FAILED"
[ "$FAILED" -eq 0 ] || exit 1
echo "  ALL GREEN"
