#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
#
# Writes a standard job summary block.
#
# WHY. A required status check that fails and explains nothing costs more than one that does not
# exist: it blocks the merge, and the only way to learn why is to open the raw log of a job that has
# already scrolled past. Two of this repository's four required checks wrote no summary at all, so a
# CodeQL or secret-scan failure surfaced as a greyed-out merge button and nothing else.
#
# WHAT IT WILL NOT DO. It never prints a field it was not given. A summary that reports "0 failures"
# because nobody passed a count is worse than one that omits the line, because a fabricated zero
# reads exactly like a measured one -- the defect this pipeline has spent a sprint removing. Absent
# values are omitted, and --verdict is mandatory.
#
# USAGE
#   job-summary.sh --title "Scan for Secrets" --verdict pass \
#                  [--detail "text"]... [--count "name=value"]... \
#                  [--next "what to do when this fails"] [--artifact "name"]
#
# EXIT CODES
#   0  summary written
#   2  REFUSE: no verdict given. A summary that does not say pass or fail is decoration.
set -uo pipefail

TITLE=""
VERDICT=""
NEXT=""
DETAILS=()
COUNTS=()
ARTIFACTS=()
SELF_TEST=0

while [ $# -gt 0 ]; do
    case "$1" in
        --title)    TITLE="${2:-}"; shift 2 ;;
        --verdict)  VERDICT="${2:-}"; shift 2 ;;
        --detail)   DETAILS+=("${2:-}"); shift 2 ;;
        --count)    COUNTS+=("${2:-}"); shift 2 ;;
        --artifact) ARTIFACTS+=("${2:-}"); shift 2 ;;
        --next)     NEXT="${2:-}"; shift 2 ;;
        --self-test) SELF_TEST=1; shift ;;
        *) printf 'job-summary: unknown argument %s\n' "$1" >&2; exit 2 ;;
    esac
done

emit() {
    local out="${GITHUB_STEP_SUMMARY:-/dev/stdout}"
    {
        printf '### %s\n\n' "${TITLE:-job}"

        case "$VERDICT" in
            pass)   printf '**Result:** passed\n\n' ;;
            fail)   printf '**Result:** FAILED\n\n' ;;
            refuse) printf '**Result:** REFUSED — could not evaluate. This is not a pass.\n\n' ;;
            *)      printf '**Result:** %s\n\n' "$VERDICT" ;;
        esac

        # Commit is always known from the environment, so it is always reported: a summary read a day
        # later is worthless if it does not say which tree it describes.
        [ -n "${GITHUB_SHA:-}" ] && printf -- '- commit: `%s`\n' "${GITHUB_SHA:0:9}"
        [ -n "${GITHUB_WORKFLOW:-}" ] && printf -- '- workflow: %s\n' "$GITHUB_WORKFLOW"

        for c in ${COUNTS+"${COUNTS[@]}"}; do
            printf -- '- %s: %s\n' "${c%%=*}" "${c#*=}"
        done
        for d in ${DETAILS+"${DETAILS[@]}"}; do
            printf -- '- %s\n' "$d"
        done
        for a in ${ARTIFACTS+"${ARTIFACTS[@]}"}; do
            printf -- '- artifact: `%s`\n' "$a"
        done

        if [ -n "${GITHUB_SERVER_URL:-}" ] && [ -n "${GITHUB_REPOSITORY:-}" ] && [ -n "${GITHUB_RUN_ID:-}" ]; then
            printf -- '- [full log](%s/%s/actions/runs/%s)\n' "$GITHUB_SERVER_URL" "$GITHUB_REPOSITORY" "$GITHUB_RUN_ID"
        fi

        # The next action is printed only on a non-pass. On a pass there is nothing to do, and a
        # standing instruction under a green result trains people to skip the block entirely.
        if [ -n "$NEXT" ] && [ "$VERDICT" != "pass" ]; then
            printf '\n**Next:** %s\n' "$NEXT"
        fi
        printf '\n'
    } >> "$out"
}

if [ "$SELF_TEST" -eq 1 ]; then
    fail=0
    tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT

    # LIVENESS: a populated summary contains what it was given.
    GITHUB_STEP_SUMMARY="$tmp/a.md" GITHUB_SHA=abcdef1234567890 \
        bash "$0" --title "T" --verdict fail --count "findings=3" --next "do X" >/dev/null 2>&1
    grep -q "FAILED" "$tmp/a.md" && grep -q "findings: 3" "$tmp/a.md" && grep -qF "Next:** do X" "$tmp/a.md" \
        && echo "  ok  : LIVENESS -- verdict, counts and next action are written" \
        || { echo "  FAIL: a populated summary lost content"; cat "$tmp/a.md"; fail=1; }

    # SAFETY: a count that was NOT provided is absent, not zero. A fabricated zero reads exactly like
    # a measured one, which is the whole reason this script exists.
    GITHUB_STEP_SUMMARY="$tmp/b.md" bash "$0" --title "T" --verdict pass >/dev/null 2>&1
    if grep -qE ": 0$|failures: |findings: " "$tmp/b.md"; then
        echo "  FAIL: a count appeared that was never supplied"; fail=1
    else
        echo "  ok  : SAFETY -- an unsupplied count is omitted, never printed as zero"
    fi

    # SAFETY: no verdict REFUSES rather than emitting a summary that says nothing.
    GITHUB_STEP_SUMMARY="$tmp/c.md" bash "$0" --title "T" >/dev/null 2>&1
    [ "$?" -eq 2 ] && echo "  ok  : SAFETY -- a missing verdict REFUSES (exit 2)" \
        || { echo "  FAIL: a verdict-less summary was accepted"; fail=1; }

    # SAFETY: the next action is suppressed on a pass, so a green block carries no standing to-do.
    GITHUB_STEP_SUMMARY="$tmp/d.md" bash "$0" --title "T" --verdict pass --next "should not appear" >/dev/null 2>&1
    grep -q "should not appear" "$tmp/d.md" \
        && { echo "  FAIL: next action printed under a passing verdict"; fail=1; } \
        || echo "  ok  : SAFETY -- next action is suppressed on a pass"

    [ "$fail" -eq 0 ] && { echo "SELF-TEST: the summary emitter is non-vacuous."; exit 0; }
    echo "SELF-TEST FAILED" >&2; exit 1
fi

if [ -z "$VERDICT" ]; then
    printf 'job-summary: REFUSE -- no --verdict given. A summary that does not say whether the job passed is decoration, not a report.\n' >&2
    exit 2
fi

emit
