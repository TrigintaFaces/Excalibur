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

# ── Git-env isolation (xy3hze) — MUST precede the first git call ────────────────────────────────
# git EXPORTS GIT_INDEX_FILE / GIT_DIR / GIT_WORK_TREE into every hook and every child process.
# This script `git init`s its own throwaway fixture repos — but an inherited GIT_INDEX_FILE is an
# ABSOLUTE PATH and WINS over the repo you are standing in, so `git add` inside the fixture writes
# the CALLER'S index instead. `git init` does not rescue you; neither does `cd`.
#
# Measured consequence, S890: run from a normal shell this script passed; run from pre-commit (where
# git had exported GIT_INDEX_FILE) every arm failed AND it staged its own fixtures — including an
# AWS-shaped token and an RSA private-key header — into the real repo's index, one arm at a time.
# The standalone GREEN is the disguise: the only environment that reproduces it is the one the gate
# actually runs in, and that is the one nobody tested.
#
# Unset before the first git call, NOT inside the subshells (they inherit it either way).
unset GIT_INDEX_FILE GIT_DIR GIT_WORK_TREE GIT_OBJECT_DIRECTORY GIT_COMMON_DIR

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

# --- J. an ALL-SUPPRESSED run must NOT report clean ------------------------
#
# Arm I above asserts the cap EXISTS. Nothing asserted what the cap does to the VERDICT, and that
# gap is the defect (fmvdpg): `total_hits` accumulates only the actionable tokens, so a run in which
# every token exceeds the cap leaves `total_hits == 0`, prints "✅ ... F-5 clean", and exits 0.
#
# The cap is a defensible answer to the 66k-line blowout (bd-tak38o): suppression may reduce the
# NOISE. It may not decide the VERDICT. A suppressed token is an UN-TRIAGED token — the gate has not
# looked at it, and "I did not look" is not "there is nothing there."
#
# This arm is behavioral, not a grep: it builds a real repository whose only changed contract token
# has more siblings than the cap, and asks the gate for its exit code. It is RED against the gate as
# shipped, and it is the arm whose absence let a green tick stand over `CompletedAt` — 49 siblings,
# cap 25, suppressed — the very token f5-cross-project-test-sweep.md:125 names as the canonical
# recurrence, and the column that was silently deleting saga state (ckgz9p) while the gate reported
# clean.
repo="$WORK/allsuppressed"
mkdir -p "$repo/src/Pkg" "$repo/tests/ProjA" "$repo/tests/ProjB"
(
    cd "$repo" || exit 1
    git init -q . 2>/dev/null
    git config user.email f5@test.local
    git config user.name  f5-test

    # Two sibling test projects assert the OLD contract. Neither is touched by the change below,
    # so both are exactly what the F-5 sweep exists to surface.
    printf 'public class FooShould { void T() { var x = saga.CompletedAt; } }\n' > tests/ProjA/FooShould.cs
    printf 'public class BarShould { void T() { var y = saga.CompletedAt; } }\n' > tests/ProjB/BarShould.cs
    printf 'public class Untouched { }\n' > src/Pkg/Untouched.cs
    git add -A && git commit -qm base

    # The change: a src/** declaration of the contract token the siblings reference.
    #
    # NESTED under src/Pkg/, not flat in src/. The gate selects changed files with the pathspec
    # 'src/**/*.cs', and git's `/**/` requires AT LEAST ONE intervening directory — a flat
    # src/Saga.cs is never matched. The first draft of this fixture put it flat, and arm J duly
    # "failed"… because the gate had nothing to sweep, not because it suppressed anything. Arm K
    # is what caught that. Real src/** paths are always nested, so this is a property of the
    # fixture, not a defect in the gate.
    printf 'public class Saga { public DateTimeOffset? CompletedAt { get; set; } }\n' > src/Pkg/Saga.cs
    git add -A && git commit -qm change
)

if [ -d "$repo/.git" ]; then
    # cap=1 with 2 siblings ⇒ the sole contract token is suppressed ⇒ nothing is actionable.
    # The stale siblings are still there. The gate must NOT call that clean.
    j_out="$( cd "$repo" && F5_MAX_HITS_PER_TOKEN=1 bash "$GUARD" --base HEAD~1 2>&1 )"
    j_rc=$?

    if [ "$j_rc" -ne 0 ]; then
        pass "J: an all-suppressed run gates (exit $j_rc) — suppression reduces noise, not the verdict"
    else
        fail "J: an all-suppressed run exited 0 — the gate reported clean while every token went UN-TRIAGED"
        printf '       gate said: %s\n' "$(printf '%s' "$j_out" | tr '\n' ' ' | cut -c1-160)" >&2
    fi

    # The positive control for arm J. Without it, "exit != 0" is unfalsifiable: a gate that is broken,
    # or a fixture whose token never extracted at all, would also fail to report clean, for reasons
    # that have nothing to do with suppression. Same repo, same siblings, a cap high enough that the
    # token is ACTIONABLE — the gate must still gate, and it must name the token.
    k_out="$( cd "$repo" && F5_MAX_HITS_PER_TOKEN=25 bash "$GUARD" --base HEAD~1 2>&1 )"
    k_rc=$?

    if [ "$k_rc" -ne 0 ] && printf '%s' "$k_out" | grep -q 'CompletedAt'; then
        pass "K: control — the same fixture UNDER the cap gates and names 'CompletedAt' (token extracts; J is about suppression)"
    else
        fail "K: control FAILED — fixture never produced an actionable hit (rc=$k_rc). Arm J proves nothing."
        printf '       gate said: %s\n' "$(printf '%s' "$k_out" | tr '\n' ' ' | cut -c1-160)" >&2
    fi
else
    fail "J/K: could not build the synthetic repository fixture (git unavailable?) — arms did not run"
fi

echo ""
if [ "$FAILURES" -eq 0 ]; then
    echo "✅ f5-sweep.test.sh: ALL GREEN"
    exit 0
fi
echo "❌ f5-sweep.test.sh: $FAILURES lock(s) FAILED" >&2
exit 1
