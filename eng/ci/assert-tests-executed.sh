#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
#
# Require complete passing VSTest console summaries. Total includes skipped tests,
# so Total > 0 alone cannot establish execution. Any failed/skipped/inconsistent
# summary or abort refuses, including when a sibling project passed.
#
# Usage: assert-tests-executed.sh --filter description [--expect count] < run.log
# Exit 0: positive passing executed count, equal to --expect when supplied.
# Exit 3: incomplete, absent, unsuccessful, or malformed evidence.
# Also retain the runner's exit code. Console counts cannot prove test identity;
# the TRX gate and independent discovery evidence provide that stronger check.
#
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
expect_supplied=false
while [ "$#" -gt 0 ]; do
    case "$1" in
        --filter) [ "$#" -ge 2 ] || exit "$EXIT_REFUSE"; filter_desc="$2"; shift 2 ;;
        --filter=*) filter_desc="${1#--filter=}"; shift ;;
        --expect) [ "$#" -ge 2 ] || exit "$EXIT_REFUSE"; expect="$2"; expect_supplied=true; shift 2 ;;
        --expect=*) expect="${1#--expect=}"; expect_supplied=true; shift ;;
        *) echo "REFUSE (assert-tests-executed): unknown argument '$1'." >&2; exit "$EXIT_REFUSE" ;;
    esac
done

# A malformed --expect must REFUSE, never silently degrade to the weaker >= 1 check. Treating
# `--expect abc` as "no expectation given" would turn a typo into a permanently weaker gate that
# still reports success — the failure mode this whole script exists to stop.
if $expect_supplied && ! [[ "$expect" =~ ^[0-9]{1,18}$ ]]; then
    echo "REFUSE (assert-tests-executed): --expect '${expect}' is not a non-negative integer." >&2
    exit "$EXIT_REFUSE"
fi

# Read the run output from stdin. Reading it whole (not streaming) is deliberate: the
# aggregate signal can appear in any project's block, so the whole run must be seen.
output="$(cat)"

# Read complete log evidence before deciding; avoid pipelines that can truncate on SIGPIPE.
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

# Total includes skipped cases. Require complete successful VSTest summary rows,
# never an arbitrary 'Total:' substring or a nonzero discovered-but-unexecuted set.
summaries="$(grep -E '^[[:space:]]*(Passed!|Failed!|Skipped!)' <<<"$output" || true)"
executed=0
while IFS= read -r summary; do
    [ -n "$summary" ] || continue
    if [[ ! "$summary" =~ ^[[:space:]]*Passed![[:space:]]+-[[:space:]]+Failed:[[:space:]]*([0-9]+),[[:space:]]*Passed:[[:space:]]*([0-9]+),[[:space:]]*Skipped:[[:space:]]*([0-9]+),[[:space:]]*Total:[[:space:]]*([0-9]+), ]]; then
        echo "REFUSE (assert-tests-executed): malformed test summary." >&2
        exit "$EXIT_REFUSE"
    fi
    for count in "${BASH_REMATCH[@]:1}"; do
        if [ "${#count}" -gt 18 ]; then
            echo 'REFUSE (assert-tests-executed): counter exceeds supported integer range.' >&2
            exit "$EXIT_REFUSE"
        fi
    done
    failed=$((10#${BASH_REMATCH[1]}))
    passed=$((10#${BASH_REMATCH[2]}))
    skipped=$((10#${BASH_REMATCH[3]}))
    total=$((10#${BASH_REMATCH[4]}))
    if [ "$failed" -ne 0 ] || [ "$skipped" -ne 0 ] || [ "$passed" -ne "$total" ]; then
        echo "REFUSE (assert-tests-executed): failed, skipped, or inconsistent required test results." >&2
        exit "$EXIT_REFUSE"
    fi
    if [ "$passed" -gt "$((9223372036854775807 - executed))" ]; then
        echo 'REFUSE (assert-tests-executed): aggregate counter overflow.' >&2
        exit "$EXIT_REFUSE"
    fi
    executed=$((executed + passed))
done <<<"$summaries"

if [ "$executed" -gt 0 ]; then
    # Zero-match is excluded. If the caller declared an expected arm count, the run must match it
    # EXACTLY; without --expect, behaviour is unchanged from the original >= 1 contract.
    if [ -n "$expect" ]; then
        # Sum is computed from each complete passing summary above.
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
echo "  No project reported a complete passing summary with executed tests. A discovered Total is insufficient." >&2
echo "  Cause is usually a stale/typo'd filter, a renamed test, or a trait that was never stamped." >&2
exit "$EXIT_REFUSE"
