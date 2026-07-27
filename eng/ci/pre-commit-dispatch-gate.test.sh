#!/usr/bin/env bash
# pre-commit-dispatch-gate.test.sh — regression lock for eng/ci/pre-commit-dispatch-gate.sh (e5juti).
#
# The gate's thesis: a non-verdict exit (2/124/127/143) from a dispatched gate must NOT be read as PASS.
# This lock drives the gate over hermetic FIXTURE hooks (via PRECOMMIT_DISPATCH_HOOK) and proves all
# three verdicts, safety AND liveness paired (testing-patterns §3):
#   SAFETY   — a fixture that reads a non-verdict exit as PASS is FLAGGED (FAIL). A gate that flagged
#              nothing would fail these arms.
#   LIVENESS — a fixture whose sites are all honest PASSES. A gate that flagged everything would fail
#              these arms — and would be useless, blocking every correct hook.
#   REFUSE   — a hook with zero dispatch sites is REFUSE, never a silent PASS (enumerate-zero defect).
#
# Run: bash eng/ci/pre-commit-dispatch-gate.test.sh   (exit 0 = all green)

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GATE="${PRECOMMIT_DISPATCH_GATE:-$SCRIPT_DIR/pre-commit-dispatch-gate.sh}"

FAILURES=0
pass() { printf '  [PASS] %s\n' "$1"; }
fail() { printf '  [FAIL] %s\n' "$1" >&2; FAILURES=$((FAILURES + 1)); }

[ -f "$GATE" ] || { printf 'FATAL: gate not found at %s\n' "$GATE" >&2; exit 3; }

WORK="$(mktemp -d 2>/dev/null || echo "${TMPDIR:-/tmp}/pcdispatch.$$")"
mkdir -p "$WORK"
cleanup() { rm -rf "$WORK" 2>/dev/null || true; }
trap cleanup EXIT

echo "pre-commit-dispatch-gate.test.sh — locking $GATE"

# run the gate against a fixture hook; prints its exit code.
run_gate() {  # $1 = fixture hook path
    PRECOMMIT_DISPATCH_HOOK="$1" bash "$GATE" >/dev/null 2>&1
    echo $?
}

# ── LIVENESS A: fail-closed `-ne 0` site -> PASS(0) ─────────────────────────
cat > "$WORK/a" <<'EOF'
#!/usr/bin/env bash
bash "$GUARD" && freeze_rc=0 || freeze_rc=$?
if [ "${freeze_rc:-0}" -ne 0 ]; then exit 1; fi
EOF
rc="$(run_gate "$WORK/a")"
[ "$rc" -eq 0 ] && pass "A: fail-closed (-ne 0) site -> PASS(0)" \
    || fail "A: fail-closed site did NOT PASS (got $rc, expected 0)"

# ── LIVENESS B: three-state `case … *)` site -> PASS(0) ─────────────────────
cat > "$WORK/b" <<'EOF'
#!/usr/bin/env bash
bash "$GUARD" && secret_rc=0 || secret_rc=$?
case "${secret_rc:-0}" in
    0) ;;
    1) echo blocked; exit 1 ;;
    *) echo REFUSE; exit 1 ;;
esac
EOF
rc="$(run_gate "$WORK/b")"
[ "$rc" -eq 0 ] && pass "B: three-state case (with *) catch-all) -> PASS(0)" \
    || fail "B: three-state case did NOT PASS (got $rc, expected 0)"

# ── SAFETY C: `-eq 1` single-code site -> FAIL(1) ──────────────────────────
# The r4dzl2 security hole: only exit 1 blocks; a gate that exits 2 (syntax error) or 127 passes.
cat > "$WORK/c" <<'EOF'
#!/usr/bin/env bash
bash "$GUARD" && dup_rc=0 || dup_rc=$?
if [ "${dup_rc:-0}" -eq 1 ]; then exit 1; fi
EOF
rc="$(run_gate "$WORK/c")"
[ "$rc" -eq 1 ] && pass "C: -eq 1 single-code site -> FAIL(1) (the non-verdict-as-PASS hole)" \
    || fail "C: -eq 1 site was NOT flagged (got $rc, expected 1)"

# ── SAFETY D: `case` with NO *) catch-all -> FAIL(1) ───────────────────────
cat > "$WORK/d" <<'EOF'
#!/usr/bin/env bash
bash "$GUARD" && clob_rc=0 || clob_rc=$?
case "${clob_rc:-0}" in
    0) ;;
    1) exit 1 ;;
esac
EOF
rc="$(run_gate "$WORK/d")"
[ "$rc" -eq 1 ] && pass "D: case without *) catch-all -> FAIL(1) (non-verdict falls past esac)" \
    || fail "D: catch-all-less case was NOT flagged (got $rc, expected 1)"

# ── SAFETY E: captured but never read -> FAIL(1) ───────────────────────────
cat > "$WORK/e" <<'EOF'
#!/usr/bin/env bash
bash "$GUARD"; ghost_rc=$?
echo "done"
EOF
rc="$(run_gate "$WORK/e")"
[ "$rc" -eq 1 ] && pass "E: captured-but-never-read -> FAIL(1)" \
    || fail "E: captured-never-read was NOT flagged (got $rc, expected 1)"

# ── SAFETY F: a violation HIDDEN among safe sites -> FAIL(1) ────────────────
# The exact r4dzl2 miss: 5 safe sites clustered, 1 broken site far below. Enumerate, don't sample.
cat > "$WORK/f" <<'EOF'
#!/usr/bin/env bash
bash "$G1" && a_rc=0 || a_rc=$?
if [ "${a_rc:-0}" -ne 0 ]; then exit 1; fi
bash "$G2" && b_rc=0 || b_rc=$?
case "${b_rc:-0}" in 0) ;; 1) exit 1 ;; *) exit 1 ;; esac
# ... 200 lines later, a site added by a different bead ...
bash "$G3" && z_rc=0 || z_rc=$?
if [ "${z_rc:-0}" -eq 1 ]; then exit 1; fi
EOF
rc="$(run_gate "$WORK/f")"
[ "$rc" -eq 1 ] && pass "F: violation hidden among safe sites -> FAIL(1) (enumerate, don't sample)" \
    || fail "F: hidden violation NOT caught (got $rc, expected 1)"

# ── REFUSE G: hook with zero dispatch sites -> REFUSE(2) ────────────────────
cat > "$WORK/g" <<'EOF'
#!/usr/bin/env bash
echo "no gate dispatch here"
EOF
rc="$(run_gate "$WORK/g")"
[ "$rc" -eq 2 ] && pass "G: zero dispatch sites -> REFUSE(2) (enumerate-zero is not a clean pass)" \
    || fail "G: zero-site hook did NOT REFUSE (got $rc, expected 2)"

# ── REFUSE H: missing hook file -> REFUSE(2) ───────────────────────────────
rc="$(run_gate "$WORK/does-not-exist")"
[ "$rc" -eq 2 ] && pass "H: missing hook file -> REFUSE(2)" \
    || fail "H: missing hook did NOT REFUSE (got $rc, expected 2)"

# ── Static guard I: gate declares the 3-state exit contract ────────────────
if grep -Eq '^E_PASS=0; E_FAIL=1; E_REFUSE=2' "$GATE"; then
    pass "I: gate declares 3-state exit contract (E_PASS=0 / E_FAIL=1 / E_REFUSE=2)"
else
    fail "I: gate 3-state exit contract missing or altered"
fi

echo ""
if [ "$FAILURES" -eq 0 ]; then
    echo "✅ pre-commit-dispatch-gate.test.sh: ALL GREEN"
    exit 0
fi
echo "❌ pre-commit-dispatch-gate.test.sh: $FAILURES lock(s) FAILED" >&2
exit 1
