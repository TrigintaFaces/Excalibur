#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
#
# assert-tests-executed.sh — REFUSE a `dotnet test` run that matched ZERO tests.
#
# WHY THIS EXISTS:
#   `dotnet test --filter <X>` exits 0 when the filter matches NOTHING. It prints
#   "No test matches the given testcase filter" and returns success. So any CI gate that
#   trusts the exit code of a filtered run reports GREEN whether the tests ran or never
#   existed — a typo'd filter, a renamed test, or a trait that was never stamped all pass
#   FOREVER, and silently. This is the vacuous-green class this gate targets, in the
#   CI layer itself.
#
#   The correct signal is NOT the per-project "No test matches" line: a `.slnf` runs many
#   projects and dotnet prints that line for EVERY project with no matching test — even
#   when a sibling project matched and passed. The true signal is the AGGREGATE executed
#   count: did ANY project report `Total: >= 1`? If none did, the whole run matched
#   nothing and dotnet still exited 0 — REFUSE.
#
#   This logic was first written inline in eng/ci/real-infra-tenant-gate.sh:110 (which
#   cites this bead). This script EXTRACTS it into a shared, self-tested helper so every
#   gate-critical filtered run can wire the same protection instead of re-inventing it —
#   the advertised-but-unwired shape: the mechanism existed in one place; this makes it
#   reusable everywhere.
#
# USAGE:
#   Pipe the combined stdout+stderr of a `dotnet test` run into this script:
#
#     dotnet test <shard.slnf --blame-hang-timeout 10m> --filter "<X>" 2>&1 | tee run.log
#     eng/ci/assert-tests-executed.sh --filter "<X>" < run.log || exit 1
#
#   Exit 0  => at least one project reported Total >= 1 (tests actually ran).
#   Exit 3  => REFUSE: the filter matched zero tests across the whole run.
#
#   The exit code is DISTINCT from a test FAILURE: this helper answers only "did any test
#   run?", not "did they pass?". Chain it BEFORE trusting the run's own exit code.
#
# ── executed == expected, NOT executed > 0 ──────────────────────────────────────────────
#   `Total >= 1` is the ZERO-match property, and zero is not the only way a run can lie.
#   Measured: an ABORTED run printed
#
#       Passed! - Failed: 0, Passed: 5, Skipped: 0, Total: 5     (TEST_EXIT=1, "Test Run Aborted")
#
#   having executed 5 of 16 arms. `Total >= 1` passes it, because five is not zero — so the
#   gate certified a run that never reached eleven of its assertions. That is the same
#   vacuous-green class one notch in: the filter matched, the process died, and the partial
#   count reads exactly like a complete one.
#
#   Two additions, both fail-CLOSED:
#
#     --expect N   the AGGREGATE executed count (summed across every project's Total) must
#                  equal N exactly. Fewer => the run was truncated. MORE => the caller's
#                  expectation is stale against the suite, which is equally a reason to stop
#                  and look: a gate that silently tolerates drift in its own expected count
#                  stops being a count check at all.
#
#     abort arm    "Test Run Aborted" REFUSEs unconditionally, --expect or not. An aborted
#                  run is never a valid green even when the surviving count looks right;
#                  the arms that did not run are precisely the ones nobody inspected.
#
#   --expect is OPTIONAL and its ABSENCE preserves the original >= 1 behaviour verbatim, so
#   no existing caller changes meaning. A caller that knows its arm count opts in.
#
#     dotnet test <shard.slnf --blame-hang-timeout 10m> --filter "<X>" 2>&1 | tee run.log
#     eng/ci/assert-tests-executed.sh --filter "<X>" --expect 16 < run.log || exit 1
#
#   Exit 3 covers every REFUSE (zero match, count mismatch, abort) — one documented code,
#   still distinct from a test failure, because the caller's action is identical in all
#   three: do not trust this run.
set -euo pipefail

EXIT_REFUSE=3

# ── Self-test seam (gate-wiring token + CI non-vacuity) ─────────────────────────────────────────────
# `--self-test` runs the paired both-arms non-vacuity proof (assert-tests-executed.test.sh) and
# propagates its exit. This is the entry harness-gates-ci.sh names in its battery: naming THIS .sh
# file (not the .test.sh) is what makes gate-wiring.sh count the gate as wired to a real caller — a
# gate whose only reference is its own *.test.sh stays an orphan by design. Mirrors the
# `f5-sweep.sh --self-test` pattern already in the battery. Delegating (rather than duplicating the
# fixtures) keeps the safety+liveness arms defined in exactly one place, the .test.sh.
if [ "${1:-}" = "--self-test" ]; then
    _here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    exec bash "${_here}/assert-tests-executed.test.sh"
fi

filter_desc="(unspecified filter)"
expect=""
while [ "$#" -gt 0 ]; do
    case "$1" in
        --filter) filter_desc="${2:-}"; shift 2 ;;
        --filter=*) filter_desc="${1#--filter=}"; shift ;;
        --expect) expect="${2:-}"; shift 2 ;;
        --expect=*) expect="${1#--expect=}"; shift ;;
        *) shift ;;
    esac
done

# A malformed --expect must REFUSE, never silently degrade to the weaker >= 1 check. Treating
# `--expect abc` as "no expectation given" would turn a typo into a permanently weaker gate that
# still reports success — the failure mode this whole script exists to stop.
if [ -n "$expect" ] && ! [[ "$expect" =~ ^[0-9]+$ ]]; then
    echo "REFUSE (assert-tests-executed): --expect '${expect}' is not a non-negative integer." >&2
    exit "$EXIT_REFUSE"
fi

# Read the run output from stdin. Reading it whole (not streaming) is deliberate: the
# aggregate signal can appear in any project's block, so the whole run must be seen.
output="$(cat)"

# The aggregate signal: ANY project reporting a non-zero executed Total. `-i` because the
# console logger's casing has varied across dotnet SDK versions; the digit class [1-9]
# excludes the vacuous "Total: 0". The leading (^|[^[:alnum:]]) boundary anchors the WORD
# "Total" so a substring like "Subtotal: 5" (which contains "total: 5") does NOT false-pass a
# run that never reported a real executed Total.
#
# A HERESTRING, not a pipe. Under `set -o pipefail`, `printf … | grep -q` fails on large input for a
# reason that has nothing to do with the match: `grep -q` exits at the FIRST hit, the `printf` still
# writing upstream takes SIGPIPE, and the pipeline reports 141. A real test log is far larger than the
# 64 KiB pipe buffer and reports its Total EARLY, which is the worst case — the writer is guaranteed
# to still be going. Reproduced: an 800 KB string whose match is on line 1 returns 141 through a pipe
# and 0 through a herestring. The direction here is fail-CLOSED (a passing run would be declared a
# zero-match REFUSE), so it costs a false alarm rather than a false green — but it is still a gate
# reporting on its own plumbing instead of on the run.
# ── ABORT ARM (unconditional) ───────────────────────────────────────────────────────────────────
# Checked BEFORE the count arms, because an aborted run's count is not evidence of anything: the
# arms it never reached are exactly the ones nobody looked at. This fires whether or not --expect
# was passed, so even legacy callers stop inheriting an aborted run as a green.
if grep -qiE '(^|[^[:alnum:]])Test Run Aborted' <<<"$output"; then
    echo "REFUSE (assert-tests-executed): the run reported 'Test Run Aborted' for filter '${filter_desc}'." >&2
    echo "  An aborted run is never a valid green: the arms it did not reach are unexamined, and the" >&2
    echo "  surviving executed count reads identically to a complete one." >&2
    exit "$EXIT_REFUSE"
fi

if grep -qiE '(^|[^[:alnum:]])Total:[[:space:]]*[1-9]' <<<"$output"; then
    # Zero-match is excluded. If the caller declared an expected arm count, the run must match it
    # EXACTLY; without --expect, behaviour is unchanged from the original >= 1 contract.
    if [ -n "$expect" ]; then
        # Sum every project's executed Total. A .slnf run prints one per project, so the aggregate —
        # not any single line — is the executed count. `|| true` because grep exits 1 on no match
        # under `set -e`, and awk supplies the 0 for that case.
        executed="$(grep -oiE '(^|[^[:alnum:]])Total:[[:space:]]*[0-9]+' <<<"$output" \
                    | grep -oE '[0-9]+$' \
                    | awk '{s += $1} END { print s + 0 }' || true)"
        executed="${executed:-0}"
        if [ "$executed" -ne "$expect" ]; then
            echo "REFUSE (assert-tests-executed): executed != expected for filter '${filter_desc}'." >&2
            echo "  executed=${executed} expected=${expect}" >&2
            if [ "$executed" -lt "$expect" ]; then
                echo "  The run was TRUNCATED — it exited before reaching every arm (abort, crash, hang" >&2
                echo "  timeout, or a filter that silently stopped matching part of the suite)." >&2
            else
                echo "  MORE tests ran than declared: the caller's --expect is stale against the suite." >&2
                echo "  Update it deliberately — a count check that tolerates its own drift is not one." >&2
            fi
            exit "$EXIT_REFUSE"
        fi
    fi
    exit 0
fi

echo "REFUSE (assert-tests-executed): the filter '${filter_desc}' matched ZERO tests across the whole run." >&2
echo "  No project reported 'Total: >= 1'. dotnet test exits 0 on a zero match, so this run is a false green." >&2
echo "  Cause is usually a stale/typo'd filter, a renamed test, or a trait that was never stamped." >&2
exit "$EXIT_REFUSE"
