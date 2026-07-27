#!/usr/bin/env bash
# sql-predicate-gate.test.sh — regression lock for eng/ci/sql-predicate-gate.sh.
#
# The gate's thesis: a declared SQL predicate fragment that the emitted SQL never interpolates is a
# silently-absent tenant filter — the class that shipped a live cross-tenant DELETE. This lock is the
# gate's WIRED control: it proves the gate produces all THREE verdicts on inputs it controls, and — the
# strongest arm — that it FIRES on the REAL historical defect and is SILENT on its fix.
#
# Behavioral 3-state (hermetic, via the SQLPRED_SRC_ROOTS override; no git, absolute-path find):
#   A  planted defect (fragment declared, never interpolated) -> gate exit 1  (FAIL)
#   B  clean pair (fragment interpolated)                     -> gate exit 0  (PASS, no false positive)
#   C  empty scan root                                        -> gate exit 2  (REFUSE, never a pass)
# A (defect->FAIL) and B (clean->PASS) are a safety+liveness pair: a gate that flagged everything fails
# B; one that flagged nothing fails A. Three distinct exit codes for three scenarios cannot be a no-op.
#
# Real-defect control (the non-vacuity proof against reality, not a synthetic mutant):
#   D  the exact commit that shipped the cross-tenant DELETE  -> gate FAIL(1), names the fragment
#   E  the fix of that commit (current source)               -> gate PASS(0)
#   D/E are SKIPPED (not failed) when the historical blob is unreachable — a shallow clone or the public
#   mirror may not carry the pre-fix history. The hermetic arms A/B/C are the mirror-safe backbone.
#
# Per-variable (masking) control — the vacuity a naive per-file count would hit:
#   F  one interpolated + one un-interpolated fragment in ONE file -> gate FAIL(1) on the un-interpolated
#
# No-bridge control (the exact false-negative this gate was hardened against):
#   G  a stray .Format(...)/.Append(...) elsewhere in the file must NOT mask an un-interpolated fragment
#
# Static guards (grep the gate source — the seam guards must not regress):
#   H  3-state exit contract present (E_PASS=0 / E_FAIL=1 / E_REFUSE=2)
#   I  testability seam present (SQLPRED_SRC_ROOTS override)
#   J  non-vacuity floor present (SQLPRED_MIN_FRAGS) + NO suppression cap in code
#   K  no internal tracker/sprint/ADR ids in the mirrored gate source (public-surface hygiene)
#
# Run: bash eng/ci/sql-predicate-gate.test.sh   (exit 0 = all green; non-zero = a lock failed)

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GATE="${SQLPRED_GATE:-$SCRIPT_DIR/sql-predicate-gate.sh}"

FAILURES=0
pass() { printf '  [PASS] %s\n' "$1"; }
fail() { printf '  [FAIL] %s\n' "$1" >&2; FAILURES=$((FAILURES + 1)); }
skip() { printf '  [SKIP] %s\n' "$1"; }

[ -f "$GATE" ] || { printf 'FATAL: gate not found at %s\n' "$GATE" >&2; exit 3; }

WORK="$(mktemp -d 2>/dev/null || echo "${TMPDIR:-/tmp}/sqlpredtest.$$")"
mkdir -p "$WORK"
cleanup() { rm -rf "$WORK" 2>/dev/null || true; }
trap cleanup EXIT

echo "sql-predicate-gate.test.sh — locking $GATE"

run_gate() {  # $1 = src root ; prints exit code
    SQLPRED_SRC_ROOTS="$1" SQLPRED_MIN_FRAGS=1 SQLPRED_REPO_ROOT="$PWD" \
      bash "$GATE" --sweep >/dev/null 2>&1
    echo $?
}

# ── A. planted defect -> FAIL(1) ─────────────────────────────────────────────
AD="$WORK/a"; mkdir -p "$AD"
cat > "$AD/Bad.cs" <<'EOF'
var tenantPredicate = scope.IsScoped ? " AND TENANTID = :TenantId" : string.Empty;
var sql = $"""
   DELETE FROM {qualifiedTable}
   WHERE AGGREGATEID = :AggregateId
     AND VERSION < :Version
   """;
EOF
rc="$(run_gate "$AD")"
[ "$rc" -eq 1 ] && pass "A: planted un-interpolated fragment -> FAIL(1)" \
                || fail "A: planted defect did NOT FAIL (got $rc, expected 1)"

# ── B. clean pair -> PASS(0) ─────────────────────────────────────────────────
BD="$WORK/b"; mkdir -p "$BD"
cat > "$BD/Good.cs" <<'EOF'
var tenantPredicate = scope.IsScoped
    ? " AND TENANTID = :TenantId"
    : " AND TENANTID IS NULL";
var sql = $"""
   DELETE FROM {qualifiedTable}
   WHERE AGGREGATEID = :AggregateId
     AND VERSION < :Version{tenantPredicate}
   """;
EOF
rc="$(run_gate "$BD")"
[ "$rc" -eq 0 ] && pass "B: interpolated fragment -> PASS(0) (no false positive)" \
                || fail "B: clean pair did NOT PASS (got $rc, expected 0)"

# ── C. empty scan root -> REFUSE(2) ──────────────────────────────────────────
CD="$WORK/c"; mkdir -p "$CD"
rc="$(run_gate "$CD")"
[ "$rc" -eq 2 ] && pass "C: empty scan -> REFUSE(2) (empty enumeration is not a clean result)" \
                || fail "C: empty scan did NOT REFUSE (got $rc, expected 2)"

# ── D/E. REAL historical defect + its fix (skippable) ────────────────────────
DEFECT_SHA="927677fc0"; FIX_SHA="9cabce4db"
DEFECT_PATH="src/Excalibur/Excalibur.EventSourcing.Oracle/Requests/DeleteSnapshotsOlderThanRequest.cs"
DD="$WORK/d"; mkdir -p "$DD"
if git -C "$PWD" cat-file -e "$DEFECT_SHA:$DEFECT_PATH" 2>/dev/null; then
    git -C "$PWD" show "$DEFECT_SHA:$DEFECT_PATH" > "$DD/DeleteSnapshotsOlderThanRequest.cs" 2>/dev/null
    rc="$(run_gate "$DD")"
    [ "$rc" -eq 1 ] && pass "D: the real commit that shipped the cross-tenant DELETE -> FAIL(1)" \
                    || fail "D: the real historical defect did NOT FAIL (got $rc, expected 1) — gate is vacuous"
else
    skip "D: historical defect blob unreachable (shallow clone / mirror) — hermetic arm A covers the class"
fi
ED="$WORK/e"; mkdir -p "$ED"
if git -C "$PWD" cat-file -e "HEAD:$DEFECT_PATH" 2>/dev/null; then
    git -C "$PWD" show "HEAD:$DEFECT_PATH" > "$ED/DeleteSnapshotsOlderThanRequest.cs" 2>/dev/null
    rc="$(run_gate "$ED")"
    [ "$rc" -eq 0 ] && pass "E: the fixed current source of that file -> PASS(0)" \
                    || fail "E: the fixed source did NOT PASS (got $rc, expected 0) — a false positive on real code"
else
    skip "E: current file unreachable"
fi

# ── F. per-variable masking -> FAIL(1) on the un-interpolated sibling ────────
FD="$WORK/f"; mkdir -p "$FD"
cat > "$FD/Mixed.cs" <<'EOF'
var goodPredicate = scope.IsScoped ? " AND TENANTID = :TenantId" : " AND TENANTID IS NULL";
var badPredicate  = scope.IsScoped ? " AND STATUS = :Status" : string.Empty;
var sql = $"""
   SELECT * FROM {qualifiedTable}
   WHERE AGGREGATEID = :AggregateId{goodPredicate}
   """;
EOF
rc="$(run_gate "$FD")"
[ "$rc" -eq 1 ] && pass "F: mixed file -> FAIL(1) (a correct sibling does not mask a defective one)" \
                || fail "F: per-variable masking not caught (got $rc, expected 1)"

# ── G. no method-name bridge (the false-negative this gate was hardened against) ─
GD="$WORK/g"; mkdir -p "$GD"
cat > "$GD/Bridge.cs" <<'EOF'
var qualifiedTable = OracleTableName.Format(schema, table);
var tenantPredicate = scope.IsScoped ? " AND TENANTID = :TenantId" : string.Empty;
var sql = $"""
   DELETE FROM {qualifiedTable}
   WHERE AGGREGATEID = :AggregateId
     AND VERSION < :Version
   """;
EOF
rc="$(run_gate "$GD")"
[ "$rc" -eq 1 ] && pass "G: a stray .Format(...) does NOT mask the un-interpolated fragment -> FAIL(1)" \
                || fail "G: a method-name bridge masked the real-defect shape (got $rc, expected 1)"

# ── Static guards on the gate source ─────────────────────────────────────────
grep -Eq '^E_PASS=0; E_FAIL=1; E_REFUSE=2' "$GATE" \
    && pass "H: gate declares the 3-state exit contract" \
    || fail "H: gate 3-state exit contract missing or altered"

grep -q 'SQLPRED_SRC_ROOTS' "$GATE" \
    && pass "I: gate exposes the testability seam (SQLPRED_SRC_ROOTS)" \
    || fail "I: gate missing the env-override testability seam"

# Strip comments before the negative grep: the gate DOCUMENTS "deliberately NO suppression cap" in prose.
if grep -q 'SQLPRED_MIN_FRAGS' "$GATE" && ! sed -E 's/#.*$//' "$GATE" | grep -qE 'MAX_HITS|SUPPRESS|ALLOWLIST'; then
    pass "J: gate has the non-vacuity floor (SQLPRED_MIN_FRAGS) and NO suppression cap in code"
else
    fail "J: gate missing SQLPRED_MIN_FRAGS floor, or introduced a suppression cap in code"
fi

# K: the gate is on the public mirror (eng/**). No internal tracker/sprint/ADR ids may leak into it.
if grep -EnZ 'bd-[a-z0-9]{6}|[^A-Za-z]S[0-9]{3}[^0-9]|[Ss]print[- ]?[0-9]|ADR-?[0-9]' "$GATE" >/dev/null 2>&1; then
    fail "K: gate source leaks an internal tracker/sprint/ADR id (public-surface hygiene)"
    grep -EnH 'bd-[a-z0-9]{6}|[^A-Za-z]S[0-9]{3}[^0-9]|[Ss]print[- ]?[0-9]|ADR-?[0-9]' "$GATE" >&2 || true
else
    pass "K: gate source carries no internal tracker/sprint/ADR ids (mirror-clean)"
fi

echo ""
if [ "$FAILURES" -eq 0 ]; then
    echo "✅ sql-predicate-gate.test.sh: ALL GREEN"
    exit 0
fi
echo "❌ sql-predicate-gate.test.sh: $FAILURES lock(s) FAILED" >&2
exit 1
