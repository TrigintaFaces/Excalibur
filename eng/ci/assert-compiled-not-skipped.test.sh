#!/usr/bin/env bash
# assert-compiled-not-skipped.test.sh — non-vacuity lock, both arms.
#
# The gate it proves exists because a SKIPPED project and a COMPILED one are indistinguishable at every
# surface anyone reads. So this lock must prove the gate can tell them apart — and, just as importantly,
# that it does NOT simply refuse everything (a gate that fails every build is removed within a day, and
# then the real defect ships unguarded).
#
set -uo pipefail

GATE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/assert-compiled-not-skipped.sh"
[ -x "$GATE" ] || chmod +x "$GATE" 2>/dev/null

TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
pass=0; fail=0

check() { # name expected_exit logfile [extra args...]
    local name="$1" expected="$2" logf="$3"; shift 3
    bash "$GATE" "$logf" "$@" >/dev/null 2>&1
    local rc=$?
    if [ "$rc" -eq "$expected" ]; then echo "  ok   $name (exit $rc)"; pass=$((pass + 1))
    else echo "  FAIL $name — expected $expected, got $rc"; fail=$((fail + 1)); fi
}

echo "[assert-compiled-not-skipped.test] running..."

# A real skipped-project build: note it exits 0 and says "0 Error(s)" — this is the whole problem.
cat > "$TMP/skipped.log" <<'EOF'
MSBuild version 17.9.0 for .NET
  Skipping compile for Excalibur.MultiTenancy.Tests (examples/tests/benchmarks disabled)
  Excalibur.MultiTenancy.Tests -> D:\...\Excalibur.MultiTenancy.Tests.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
EOF

cat > "$TMP/compiled.log" <<'EOF'
MSBuild version 17.9.0 for .NET
  Excalibur.MultiTenancy.Tests -> D:\...\Excalibur.MultiTenancy.Tests.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
EOF

# ── SAFETY: the defect — a build that looks identical to a good one ─────────────────────────────────
check "SAFETY: a SKIPPED project is REFUSED despite exit 0 / 0 Error(s)" 1 "$TMP/skipped.log"

# ── LIVENESS: the good build must still pass, or the gate gets deleted ──────────────────────────────
check "LIVENESS: a genuinely compiled build PASSES" 0 "$TMP/compiled.log"

# ── LIVENESS: a DECLARED skip is allowed (the escape must work, or callers bypass the gate) ─────────
check "LIVENESS: an --expect-skipped project is ACCEPTED" 0 "$TMP/skipped.log" --expect-skipped Excalibur.MultiTenancy.Tests

# ── SAFETY: the allowlist is a SET, not a count — a DIFFERENT skip must still refuse ────────────────
# Without this, one project leaving the expected set while another enters keeps the count identical and
# hides the new gap. This is the arm that makes the allowlist honest.
cat > "$TMP/other.log" <<'EOF'
MSBuild version 17.9.0 for .NET
  Skipping compile for Excalibur.Dispatch.Benchmarks (examples/tests/benchmarks disabled)
Build succeeded.
    0 Error(s)
EOF
check "SAFETY: a DIFFERENT skip is refused even when another is allowlisted" 1 "$TMP/other.log" --expect-skipped Excalibur.MultiTenancy.Tests

# ── SAFETY: an empty log REFUSES — an absent build is not a clean build ─────────────────────────────
: > "$TMP/empty.log"
check "SAFETY: empty log REFUSES (exit 2)" 2 "$TMP/empty.log"

# ── SAFETY: a log with no MSBuild output REFUSES rather than reporting 'no skips found' ─────────────
echo "hello, this is not a build log" > "$TMP/garbage.log"
check "SAFETY: non-build log REFUSES (exit 2), never a vacuous PASS" 2 "$TMP/garbage.log"

# ── SAFETY: a missing file REFUSES ─────────────────────────────────────────────────────────────────
check "SAFETY: missing log REFUSES (exit 2)" 2 "$TMP/does-not-exist.log"

echo "[assert-compiled-not-skipped.test] $pass passed, $fail failed"
[ "$fail" -eq 0 ] || exit 1
exit 0
