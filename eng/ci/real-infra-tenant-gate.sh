#!/usr/bin/env bash
# real-infra-tenant-gate.sh
#
# Authoritative, NON-SKIPPABLE real-infrastructure tenant-isolation gate.
#
# WHY THIS EXISTS: real-infra tenant tests previously ran only in a nightly job that was
# skippable (a workflow input), soft-failed (continue-on-error), and never ran on the
# push-to-main integration point — so a real-infra tenant-isolation RED hid on committed
# content. This gate PROMOTES that run to a required push-to-main check that reddens on a
# real tenant violation and REFUSEs (never silently passes) when infra is absent.
#
# THREE-STATE CONTRACT (REFUSE != PASS):
#   0  PASS    — >=1 selected test ran and all passed
#   1  FAIL    — >=1 selected test ran and at least one failed (a real tenant-isolation RED)
#   2  REFUSE  — the gate COULD NOT render a verdict:
#                  * real infrastructure (Docker) is not provisioned, OR
#                  * the trait filter matched ZERO tests (the curated set vanished / trait
#                    was never stamped) — a filter that matches nothing must NOT exit 0
#                    (the false-green class: an empty selection reporting success).
# A REFUSE must surface NON-GREEN on the required check. The workflow step MUST NOT wrap this
# in `|| true` or `continue-on-error: true`.
#
# SELECTION (pinned interface with Lane E / TestsDeveloper): the required gate runs the
# curated, tenant-isolation-critical subset  Category=Integration & Infra=Required  — narrow
# enough for the push-to-main integration point, while nightly keeps the broad
# Category=Integration|EndToEnd cadence. Every real-infra tenant lock carries [Trait("Infra","Required")].
#
# SEAMS (overridable by the self-test only; production uses the defaults):
#   RITG_DOCKER_PROBE   command that succeeds iff real infra is reachable   (default: docker info)
#   RITG_TEST_CMD       command that runs the suite and prints a dotnet-test-shaped summary
#                       (default: the real `dotnet test` invocation below)
#   RITG_FILTER         the trait filter                                    (default: pinned)
#   RITG_SLNF           the solution filter                                 (default: IntegrationTests.slnf)
set -uo pipefail

EXIT_PASS=0
EXIT_FAIL=1
EXIT_REFUSE=2

FILTER="${RITG_FILTER:-Category=Integration&Infra=Required}"
SLNF="${RITG_SLNF:-eng/ci/shards/IntegrationTests.slnf}"
DOCKER_PROBE="${RITG_DOCKER_PROBE:-docker info}"

log()   { printf '%s\n' "$*" >&2; }
err()   { printf '::error::%s\n' "$*" >&2; }

# ── SELF-TEST (real-path arm) ───────────────────────────────────────────────────────────────────
# Proves the gate's ACTUAL `dotnet test` invocation + output-parse + exit-mapping — the real path,
# not a stub. A gate whose whole job is killing false-greens, proven only against a stubbed command
# (real-infra-tenant-gate.test.sh), carries its own false-green gap: a wrong --filter or a wrong
# parse passes the stub and fails on real infra. This arm re-invokes the gate against Tests'
# permanent planted-violation fixture (Category=GateSelfTest — env-armed, Docker-free) and asserts
# the gate maps a REAL dotnet FAIL→1 and a REAL dotnet PASS→0.
#
# Docker-free but dotnet-required: it runs in CI AFTER the test project builds (a ci.yml step), NOT
# in the hermetic bash battery. The REFUSE/zero-match arms stay in the stubbed .test.sh (no dotnet).
if [ "${1:-}" = "--self-test" ]; then
    st_pass=0; st_fail=0
    st_arm() {
        local name="$1" expected="$2" armenv="${3:-}" rc
        if [ -n "$armenv" ]; then
            env "$armenv" RITG_DOCKER_PROBE=true RITG_FILTER='Category=GateSelfTest' bash "$0" >/dev/null 2>&1
        else
            env RITG_DOCKER_PROBE=true RITG_FILTER='Category=GateSelfTest' bash "$0" >/dev/null 2>&1
        fi
        rc=$?
        if [ "$rc" -eq "$expected" ]; then
            printf '  ✓ %-44s exit %s (expected %s)\n' "$name" "$rc" "$expected"; st_pass=$((st_pass + 1))
        else
            printf '  ✗ %-44s exit %s (EXPECTED %s)\n' "$name" "$rc" "$expected"; st_fail=$((st_fail + 1))
        fi
    }
    echo "real-infra-tenant-gate --self-test (REAL dotnet path vs Category=GateSelfTest):"
    st_arm "real dotnet PASS → gate PASS" 0 ""
    st_arm "real dotnet FAIL → gate FAIL" 1 "EXCALIBUR_TGO35C_SELFTEST=fail"
    echo "  ── $st_pass passed, $st_fail failed ──"
    [ "$st_fail" -eq 0 ] || { echo "real-infra-tenant-gate --self-test: RED"; exit 1; }
    echo "real-infra-tenant-gate --self-test: GREEN (real dotnet invocation+parse proven non-vacuous)"
    exit 0
fi

# ── 1. Real-infra probe. Docker unreachable ⇒ REFUSE (environmental, NOT a test failure). ──
if ! ${DOCKER_PROBE} >/dev/null 2>&1; then
    err "REFUSE: real infrastructure (Docker) is not provisioned — the authoritative real-infra tenant gate cannot render a verdict. REFUSE != PASS (a green here would be the false-safety class this gate exists to kill)."
    exit $EXIT_REFUSE
fi

# ── 2. Run the curated real-infra tenant subset (serial: -m:1 for Docker stability). ──
run_suite() {
    if [ -n "${RITG_TEST_CMD:-}" ]; then
        eval "${RITG_TEST_CMD}"
        return $?
    fi
    # --no-build: the workflow builds the test project in a prior step (as nightly.yml does), so the
    # gate runs the already-built assemblies. This keeps the gate fast and matches the CI pattern.
    dotnet test "${SLNF}" \
        --configuration Release \
        --no-build \
        -m:1 \
        --blame-hang-timeout 5m \
        --logger "console;verbosity=minimal" \
        --filter "${FILTER}" \
        -- RunConfiguration.TestSessionTimeout=1800000
}

log "real-infra-tenant-gate: filter='${FILTER}' slnf='${SLNF}'"
output="$(run_suite 2>&1)"; suite_rc=$?
printf '%s\n' "$output"

# ── 3. Zero-match ⇒ REFUSE — delegated to the shared assert-tests-executed helper. ──
#      A `.slnf` runs MANY projects; `dotnet test` prints "No test matches the given testcase filter"
#      for EVERY project that has no matching test — even when ANOTHER project in the same run matched
#      and passed. So the true zero-match signal is the AGGREGATE executed count (did ANY project report
#      Total >= 1?), NOT the per-project phrase. That logic was FIRST written inline here; it now lives
#      in the shared, self-tested eng/ci/assert-tests-executed.sh so every filtered gate wires the same
#      protection instead of re-inventing it. This gate is that helper's real production consumer.
_GATE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if ! printf '%s' "$output" | bash "${_GATE_DIR}/assert-tests-executed.sh" --filter "${FILTER}"; then
    err "REFUSE: the trait filter '${FILTER}' matched ZERO tests across the whole run (no project reported Total >= 1). The curated set is empty (trait not stamped, or the tests were removed). A gate that matches nothing must NOT pass."
    exit $EXIT_REFUSE
fi

# ── 4. Verdict from the suite exit code (>=1 test ran). ──
if [ "$suite_rc" -ne 0 ]; then
    err "FAIL: a real-infra tenant-isolation test failed (suite exit ${suite_rc}) — a real cross-tenant RED on committed content."
    exit $EXIT_FAIL
fi

log "real-infra-tenant-gate: PASS — all selected real-infra tenant tests green."
exit $EXIT_PASS
