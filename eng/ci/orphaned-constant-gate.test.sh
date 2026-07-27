#!/usr/bin/env bash
# orphaned-constant-gate.test.sh — regression lock for eng/ci/orphaned-constant-gate.sh.
#
# The gate's thesis: a COMPUTED cadence/cap number a comment asserts (`N * …` / `N tick(s)`) that the
# code no longer contains is prose orphaned by a parameterising refactor — the class that cost ~90
# minutes and four refuted theories mid-incident. This lock is the gate's WIRED control: it proves the
# gate produces all THREE verdicts on inputs it controls, that it FIRES on the REAL historical ghost and
# is SILENT on its fix, and — the AC4 arm — that it does NOT fire on a file full of legitimate prose
# numbers (the noise that gets such a gate switched off).
#
# Behavioral 3-state (hermetic, via ORPHCONST_SRC override; no git):
#   A  planted orphan (comment cadence absent from code) -> gate exit 1  (FAIL)
#   B  clean (comment cadence present in code)           -> gate exit 0  (PASS, no false positive)
#   C  empty scan root                                   -> gate exit 2  (REFUSE, never a pass)
# A/B are a safety+liveness pair: flag-everything fails B, flag-nothing fails A.
#
# Real-defect control (non-vacuity against reality, not a synthetic mutant):
#   D  the pre-repair poll-opcom.sh (comment "7200 ticks", code 86400) -> gate FAIL(1), names 7200
#   E  the current poll-opcom.sh (cap comment states no number)        -> gate PASS(0)
#   D/E SKIP (not fail) when the .claude history blob is unreachable (shallow/mirror) — the hermetic
#   arms A/B/C are the mirror-safe backbone.
#
# AC4 noise control (the reason this gate can ship at all):
#   F  a file of legitimate prose numbers (counts, line-deltas, a sprint number, a version) -> PASS(0)
#
# Static guards (grep the gate source — the seam guards must not regress):
#   G  3-state exit contract present (E_PASS=0 / E_FAIL=1 / E_REFUSE=2)
#   H  testability seam present (ORPHCONST_ROOTS override)
#   I  non-vacuity floor present (ORPHCONST_MIN_NUMS) + NO suppression cap in code
#   J  no internal tracker/sprint/ADR ids in the gate source (public-surface hygiene — eng/ is mirrored)
#
# Run: bash eng/ci/orphaned-constant-gate.test.sh   (exit 0 = all green; non-zero = a lock failed)

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GATE="${ORPHCONST_GATE:-$SCRIPT_DIR/orphaned-constant-gate.sh}"

FAILURES=0
pass() { printf '  [PASS] %s\n' "$1"; }
fail() { printf '  [FAIL] %s\n' "$1" >&2; FAILURES=$((FAILURES + 1)); }
skip() { printf '  [SKIP] %s\n' "$1"; }

[ -f "$GATE" ] || { printf 'FATAL: gate not found at %s\n' "$GATE" >&2; exit 3; }

WORK="$(mktemp -d 2>/dev/null || echo "${TMPDIR:-/tmp}/orphconsttest.$$")"
mkdir -p "$WORK"
cleanup() { rm -rf "$WORK" 2>/dev/null || true; }
trap cleanup EXIT

echo "orphaned-constant-gate.test.sh — locking $GATE"

run_gate() {  # $1 = src root ; prints exit code
    ORPHCONST_ROOTS="$1" ORPHCONST_MIN_NUMS=1 ORPHCONST_REPO_ROOT="$PWD" \
      bash "$GATE" --sweep >/dev/null 2>&1
    echo $?
}

# ── A. planted orphan -> FAIL(1) ─────────────────────────────────────────────
AD="$WORK/a"; mkdir -p "$AD"
printf '# hard cap: 7200 * 1s under SSE; 7200 ticks otherwise\nTIMEOUT=86400\n' > "$AD/x.sh"
rc="$(run_gate "$AD")"
[ "$rc" -eq 1 ] && pass "A: planted orphaned cadence '7200' -> FAIL(1)" \
                || fail "A: planted orphan did NOT FAIL (got $rc, expected 1)"

# ── B. clean (cadence present in code) -> PASS(0) ────────────────────────────
BD="$WORK/b"; mkdir -p "$BD"
printf '# log every 300 ticks\nINTERVAL=300\n' > "$BD/x.sh"
rc="$(run_gate "$BD")"
[ "$rc" -eq 0 ] && pass "B: in-code cadence '300 ticks' -> PASS(0) (no false positive)" \
                || fail "B: clean file did NOT PASS (got $rc, expected 0)"

# ── C. empty scan root -> REFUSE(2) ──────────────────────────────────────────
CD="$WORK/c"; mkdir -p "$CD"
rc="$(run_gate "$CD")"
[ "$rc" -eq 2 ] && pass "C: empty scan -> REFUSE(2) (empty enumeration is not a clean result)" \
                || fail "C: empty scan did NOT REFUSE (got $rc, expected 2)"

# ── D/E. REAL historical ghost + its repair (skippable) ──────────────────────
GHOST_SHA="8da986c85^"; GHOST_PATH=".claude/hooks/poll-opcom.sh"
DD="$WORK/d"; mkdir -p "$DD"
if git -C "$PWD" cat-file -e "$GHOST_SHA:$GHOST_PATH" 2>/dev/null; then
    git -C "$PWD" show "$GHOST_SHA:$GHOST_PATH" > "$DD/poll-opcom.sh" 2>/dev/null
    rc="$(run_gate "$DD")"
    [ "$rc" -eq 1 ] && pass "D: pre-repair poll-opcom.sh (comment 7200, code 86400) -> FAIL(1)" \
                    || fail "D: the real historical ghost did NOT FAIL (got $rc, expected 1) — gate is vacuous"
else
    skip "D: historical ghost blob unreachable (shallow/mirror) — hermetic arm A covers the class"
fi
ED="$WORK/e"; mkdir -p "$ED"
if git -C "$PWD" cat-file -e "HEAD:$GHOST_PATH" 2>/dev/null; then
    git -C "$PWD" show "HEAD:$GHOST_PATH" > "$ED/poll-opcom.sh" 2>/dev/null
    rc="$(run_gate "$ED")"
    [ "$rc" -eq 0 ] && pass "E: current poll-opcom.sh (cap comment states no number) -> PASS(0)" \
                    || fail "E: the repaired file did NOT PASS (got $rc, expected 0) — a false positive on real prose"
else
    skip "E: current file unreachable"
fi

# ── F. AC4 noise control: legitimate prose numbers -> PASS(0) ─────────────────
FD="$WORK/f"; mkdir -p "$FD"
cat > "$FD/x.sh" <<'EOF'
# Observed ~570 live workers; +125 lines changed; a batch of 882 items; version 1.1.0; an 8-bit field.
# It hid for six cycles; ~40 retractions; the template is 682 lines.
NOOP=1
EOF
rc="$(run_gate "$FD")"
# expected: REFUSE(2) — no cadence-number to evaluate (prose is correctly NOT netted) — never FAIL(1).
[ "$rc" -ne 1 ] && pass "F: a file of legitimate prose numbers is NOT flagged (rc=$rc, not FAIL) — the AC4 noise bound holds" \
                || fail "F: prose numbers were flagged as orphans (got FAIL) — the gate is noisy"

# ── Static guards on the gate source ─────────────────────────────────────────
grep -Eq '^E_PASS=0; E_FAIL=1; E_REFUSE=2' "$GATE" \
    && pass "G: gate declares the 3-state exit contract" \
    || fail "G: gate 3-state exit contract missing or altered"

grep -q 'ORPHCONST_ROOTS' "$GATE" \
    && pass "H: gate exposes the testability seam (ORPHCONST_ROOTS)" \
    || fail "H: gate missing the env-override testability seam"

if grep -q 'ORPHCONST_MIN_NUMS' "$GATE" && ! sed -E 's/#.*$//' "$GATE" | grep -qE 'MAX_HITS|SUPPRESS|ALLOWLIST'; then
    pass "I: gate has the non-vacuity floor (ORPHCONST_MIN_NUMS) and NO suppression cap in code"
else
    fail "I: gate missing ORPHCONST_MIN_NUMS floor, or introduced a suppression cap in code"
fi

if grep -EnZ 'bd-[a-z0-9]{6}|[^A-Za-z]S[0-9]{3}[^0-9]|[Ss]print[- ]?[0-9]|ADR-?[0-9]' "$GATE" >/dev/null 2>&1; then
    fail "J: gate source leaks an internal tracker/sprint/ADR id (public-surface hygiene)"
    grep -EnH 'bd-[a-z0-9]{6}|[^A-Za-z]S[0-9]{3}[^0-9]|[Ss]print[- ]?[0-9]|ADR-?[0-9]' "$GATE" >&2 || true
else
    pass "J: gate source carries no internal tracker/sprint/ADR ids (mirror-clean)"
fi

echo ""
if [ "$FAILURES" -eq 0 ]; then
    echo "✅ orphaned-constant-gate.test.sh: ALL GREEN"
    exit 0
fi
echo "❌ orphaned-constant-gate.test.sh: $FAILURES lock(s) FAILED" >&2
exit 1
