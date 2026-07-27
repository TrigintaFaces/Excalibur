#!/usr/bin/env bash
# real-infra-tenant-gate.test.sh — non-vacuity self-test for real-infra-tenant-gate.sh.
#
# Proves the gate's THREE-STATE mechanism (0 PASS / 1 FAIL / 2 REFUSE) deterministically,
# WITHOUT real Docker, by injecting the RITG_DOCKER_PROBE + RITG_TEST_CMD seams. This is the
# permanent proof the gate stays non-vacuous after the real locks green (the ki5vjb condition:
# every relocated gate reddens on a planted violation — safety AND liveness arms).
#
# The gate is the LOGIC under test; the injected suite output is the planted fixture.
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
GATE="$here/real-infra-tenant-gate.sh"

pass=0; fail=0

# run_arm <name> <expected_exit> ; env for the gate is set by the caller
run_arm() {
    local name="$1" expected="$2"
    bash "$GATE" >/dev/null 2>&1
    local rc=$?
    if [ "$rc" -eq "$expected" ]; then
        printf '  ✓ %-52s exit %s (expected %s)\n' "$name" "$rc" "$expected"
        pass=$((pass + 1))
    else
        printf '  ✗ %-52s exit %s (EXPECTED %s)\n' "$name" "$rc" "$expected"
        fail=$((fail + 1))
    fi
}

echo "real-infra-tenant-gate self-test:"

# ── SAFETY 1: Docker unreachable ⇒ REFUSE (2), never PASS. ──
RITG_DOCKER_PROBE="false" \
RITG_TEST_CMD="echo unreachable" \
    run_arm "REFUSE on no-Docker (infra absent)" 2

# ── SAFETY 2 (LOAD-BEARING): filter matched ZERO tests ⇒ REFUSE (2), NOT the dotnet exit-0. ──
# This is the 885jxd/9h66ez false-green: dotnet test exits 0 on an empty filter.
RITG_DOCKER_PROBE="true" \
RITG_TEST_CMD='echo "No test matches the given testcase filter \`Category=Integration&Infra=Required\`"; exit 0' \
    run_arm "REFUSE on zero-match filter (dotnet exit-0 trap)" 2

# ── SAFETY 3: a summary reporting Total: 0 ⇒ REFUSE (defensive, distinct emission shape). ──
RITG_DOCKER_PROBE="true" \
RITG_TEST_CMD='echo "Passed!  - Failed: 0, Passed: 0, Skipped: 0, Total: 0"; exit 0' \
    run_arm "REFUSE on Total:0 executed" 2

# ── SAFETY 4: a real test failed ⇒ FAIL (1), distinct from REFUSE. ──
RITG_DOCKER_PROBE="true" \
RITG_TEST_CMD='echo "Failed!  - Failed: 1, Passed: 2, Skipped: 0, Total: 3"; exit 1' \
    run_arm "FAIL on a real tenant-isolation RED" 1

# ── SAFETY 5 (regression — real-path bug the stub-only proof missed): a MULTI-PROJECT slnf prints
#    "No test matches" for non-matching projects ALONGSIDE a real "Total: N>=1" for the one that
#    matched. The gate must read the AGGREGATE (a project ran) and NOT REFUSE on the per-project
#    "No test matches" phrase. Caught by the real dotnet --self-test; locked here so it can't regress. ──
RITG_DOCKER_PROBE="true" \
RITG_TEST_CMD='printf "No test matches the given testcase filter in Excalibur.Inbox.Oracle.Tests.dll\nNo test matches the given testcase filter in Excalibur.Dispatch.Integration.Tests.dll\nPassed!  - Failed: 0, Passed: 2, Skipped: 0, Total: 2 - Excalibur.Integration.Tests.dll\n"; exit 0' \
    run_arm "PASS on multi-project partial no-match (NOT REFUSE)" 0

# ── LIVENESS: >=1 test ran and all passed ⇒ PASS (0). Proves the gate is not always-red. ──
RITG_DOCKER_PROBE="true" \
RITG_TEST_CMD='echo "Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3"; exit 0' \
    run_arm "PASS when the curated set runs green (liveness)" 0

echo "  ── $pass passed, $fail failed ──"
[ "$fail" -eq 0 ] || { echo "real-infra-tenant-gate self-test: RED"; exit 1; }
echo "real-infra-tenant-gate self-test: GREEN (non-vacuous: 3-state PASS/FAIL/REFUSE proven)"
exit 0
