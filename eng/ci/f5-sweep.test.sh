#!/usr/bin/env bash
# f5-sweep.test.sh — regression lock for eng/ci/f5-sweep.sh (S855 / bd-0i2jsu).
#
# f5-sweep.sh is the mechanical pre-REVIEW gate for the F-5 cross-project sibling sweep
# (.claude/rules/process/f5-cross-project-test-sweep.md). This lock proves the gate is
# NON-VACUOUS: it must flag the real footgun (a stale sibling test in a SECOND project)
# and must not false-positive on triaged/unrelated/substring matches — and the gate's own
# built-in --self-test must itself fail when the gate is mutated.
#
# Non-vacuity (each fails on a broken/mutated gate):
#   A. gate --self-test                         -> exit 0   (built-in behavioral fixtures pass)
#   B. MUTANT gate (stoplist broken)            -> --self-test exit 3   (the self-test catches it)
#   C. MUTANT gate (substring match: -w dropped)-> --self-test exit 3   (word-boundary is load-bearing)
# Static guards (grep the gate source):
#   D. drives the test tree via `git ls-files`  (NOT a recursive FS walk — perf + correctness)
#   E. whole-word fixed-string grep (`-w` + `-F`) on the sweep (no substring false-positives)
#   F. has a stoplist + a changeset-exclusion (triaged files are not re-flagged)
#
# Run: bash eng/ci/f5-sweep.test.sh   (exit 0 = all green; non-zero = a lock failed)

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# Gate under test; overridable (F5_GUARD) so a non-vacuity proof can point it at a mutant copy.
GUARD="${F5_GUARD:-$SCRIPT_DIR/f5-sweep.sh}"

FAILURES=0
pass() { printf '  [PASS] %s\n' "$1"; }
fail() { printf '  [FAIL] %s\n' "$1" >&2; FAILURES=$((FAILURES + 1)); }

[ -f "$GUARD" ] || { printf 'FATAL: gate not found at %s\n' "$GUARD" >&2; exit 3; }

WORK="$(mktemp -d 2>/dev/null || echo "${TMPDIR:-/tmp}/f5sweeptest.$$")"
mkdir -p "$WORK"
cleanup() { rm -rf "$WORK" 2>/dev/null || true; }
trap cleanup EXIT

echo "f5-sweep.test.sh — locking $GUARD"

# --- A. built-in self-test passes on the real gate -------------------------
if bash "$GUARD" --self-test >/dev/null 2>&1; then
    pass "A: gate --self-test green (built-in non-vacuous fixtures pass)"
else
    fail "A: gate --self-test did NOT pass on the unmodified gate"
fi

# --- B. mutate the stoplist -> the self-test MUST catch it ------------------
mutant_b="$WORK/f5-mutant-stoplist.sh"
# Drop CancellationToken from the stoplist; the self-test asserts it is filtered out.
sed 's/CancellationToken|//' "$GUARD" > "$mutant_b"
chmod +x "$mutant_b"
bash "$mutant_b" --self-test >/dev/null 2>&1
rc=$?
if [ "$rc" -eq 3 ]; then
    pass "B: self-test FAILS (exit 3) on a stoplist-mutant gate (self-test is non-vacuous)"
else
    fail "B: self-test did NOT fail on the stoplist mutant (got exit $rc, expected 3) — VACUOUS"
fi

# --- C. mutate word-boundary (-w -> nothing) -> self-test MUST catch it -----
mutant_c="$WORK/f5-mutant-wordbound.sh"
# Remove the -w flag from the sibling-finder grep so substring matches leak.
sed 's/grep -rIlwF/grep -rIlF/' "$GUARD" > "$mutant_c"
chmod +x "$mutant_c"
bash "$mutant_c" --self-test >/dev/null 2>&1
rc=$?
if [ "$rc" -eq 3 ]; then
    pass "C: self-test FAILS (exit 3) on a word-boundary-mutant gate (substring guard is real)"
else
    fail "C: self-test did NOT fail on the word-boundary mutant (got exit $rc, expected 3)"
fi

# --- D/E/F. static guards on the gate source -------------------------------
if grep -q 'git ls-files' "$GUARD"; then
    pass "D: gate drives the test tree via 'git ls-files' (no recursive FS walk)"
else
    fail "D: gate does not use 'git ls-files' — risks slow/incorrect FS-walk sweep"
fi

if grep -Eq 'grep -[a-zA-Z]*w[a-zA-Z]*F' "$GUARD"; then
    pass "E: gate uses whole-word fixed-string grep (-w -F)"
else
    fail "E: gate sweep is not whole-word fixed-string (-w -F) — substring false-positives"
fi

if grep -q 'F5_STOPLIST' "$GUARD" && grep -q 'changed_list' "$GUARD"; then
    pass "F: gate has a stoplist + changeset-exclusion (triaged files not re-flagged)"
else
    fail "F: gate missing stoplist or changeset-exclusion"
fi

# --- G/H/I. tak38o precision invariants (must not regress) ------------------
if grep -q 'mode="committed"' "$GUARD"; then
    pass "G: gate defaults to COMMITTED scope (not the dirty working tree — bd-tak38o)"
else
    fail "G: gate does not default to committed scope — risks the 66k dirty-tree blowout"
fi

if grep -q 'base\.\.\.HEAD' "$GUARD"; then
    pass "H: committed scope uses base...HEAD (the change, not everyone's WIP)"
else
    fail "H: gate does not scope via base...HEAD"
fi

if grep -q 'F5_MAX_HITS_PER_TOKEN' "$GUARD" && grep -q 'SUPPRESSED' "$GUARD"; then
    pass "I: gate has a per-token hit cap (generic tokens suppressed, can't blow up the report)"
else
    fail "I: gate missing the per-token hit cap (bd-tak38o backstop)"
fi

echo ""
if [ "$FAILURES" -eq 0 ]; then
    echo "✅ f5-sweep.test.sh: ALL GREEN"
    exit 0
fi
echo "❌ f5-sweep.test.sh: $FAILURES lock(s) FAILED" >&2
exit 1
