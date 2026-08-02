#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
#
# run-filtered-tests.sh — run a FILTERED `dotnet test` and refuse a run that executed nothing.
#
# WHY THIS EXISTS
#   `dotnet test --filter <X>` exits 0 when the filter matches NOTHING. It prints "No test matches the
#   given testcase filter" and reports success. A typo, a renamed test, a moved class, or a trait that
#   was never stamped therefore produces a green run that executed zero tests — and the exit code
#   carries none of that. The COUNT is the evidence; the exit code is not.
#
#   assert-tests-executed.sh already decides this correctly and is self-tested. The gap was never the
#   decision, it was the WIRING: it guarded one filtered invocation while the rest ran unguarded. This
#   wrapper exists so wiring it is one line per call site instead of six, because six lines of
#   exit-plumbing copied into every workflow is how the plumbing silently diverges.
#
#   It also fixes the plumbing once, correctly. The naive form
#       dotnet test ... | tee log
#   discards the test's exit status — the pipeline reports `tee`'s success. Here the real status is
#   captured from PIPESTATUS and it is the status that wins.
#
# USAGE
#   eng/ci/run-filtered-tests.sh --filter "<expr>" [--log <path>] -- <all other dotnet test args>
#
#   Example:
#     eng/ci/run-filtered-tests.sh --filter "Category=Unit" -- \
#       eng/ci/shards/UnitTests.slnf --configuration Release --no-build --blame-hang-timeout 5m
#
# EXIT
#   The test run's own exit code when the tests ran and something failed  (a real failure wins)
#   3  the run executed ZERO tests — REFUSE. Exit 0 with no tests is not a pass.
#   2  cannot evaluate (no filter given, no command given)
#   0  tests executed and passed
#
# Self-test: eng/ci/run-filtered-tests.sh --self-test

set -uo pipefail

readonly E_OK=0
readonly E_ENV=2
readonly E_ZERO_EXECUTED=3

SELF_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "${1:-}" = "--self-test" ]; then
    exec bash "$SELF_DIR/run-filtered-tests.test.sh"
fi

FILTER=""
LOG=""
# DOTNET_BIN exists for the self-test, which substitutes a stub so the wrapper's plumbing can be
# exercised without a real test run. Production callers never set it.
DOTNET_BIN="${DOTNET_BIN:-dotnet}"

while [ $# -gt 0 ]; do
    case "$1" in
        --filter) shift; [ $# -gt 0 ] || { echo "[run-filtered-tests] --filter needs a value" >&2; exit "$E_ENV"; }; FILTER="$1" ;;
        --log)    shift; [ $# -gt 0 ] || { echo "[run-filtered-tests] --log needs a value" >&2; exit "$E_ENV"; }; LOG="$1" ;;
        --)       shift; break ;;
        *)        echo "[run-filtered-tests] unknown option: $1" >&2; exit "$E_ENV" ;;
    esac
    shift
done

if [ -z "$FILTER" ]; then
    echo "[run-filtered-tests] CANNOT EVALUATE — no --filter given. Use plain 'dotnet test' for an unfiltered run." >&2
    exit "$E_ENV"
fi

if [ $# -eq 0 ]; then
    echo "[run-filtered-tests] CANNOT EVALUATE — no dotnet test arguments after '--'." >&2
    exit "$E_ENV"
fi

if [ -z "$LOG" ]; then
    # The X's must be the LAST characters of the template, with NO suffix after them.
    #
    # GNU mktemp (Linux) tolerates `...XXXXXX.log` and infers --suffix. BSD mktemp (macOS) does NOT:
    # it treats a template whose X's are not trailing as a LITERAL filename. So iteration 1 of the
    # soak created a file called `filtered-test-run.XXXXXX.log` verbatim and iteration 2 collided
    # with "mkstemp failed ... File exists". ubuntu passed while macos failed, which is the tell for
    # a GNU/BSD split rather than a logic error.
    #
    # The failure was worse than a missing log. Without the guard below, LOG stayed EMPTY and the
    # script carried on: `tee ""` failed, the redirect on the assert step failed, and the run ended
    # in "REFUSE: could not evaluate whether tests executed" — a message that blames the test run
    # for a plumbing fault, on a soak where every one of the ~40 assemblies had actually PASSED.
    # A harness that cannot create its own scratch file must say THAT, not cast doubt on the tests.
    if ! LOG="$(mktemp "${TMPDIR:-/tmp}/filtered-test-run.XXXXXX")" || [ -z "$LOG" ]; then
        echo "[run-filtered-tests] CANNOT EVALUATE — mktemp could not create a scratch log under" \
             "'${TMPDIR:-/tmp}'. This is a HARNESS/environment fault and says NOTHING about the" \
             "tests, which have not been run yet." >&2
        exit "$E_ENV"
    fi
fi

echo "[run-filtered-tests] filter: $FILTER"

# 2>&1 so the assert sees the whole run: the aggregate "Total:" line the console logger emits can land
# on either stream depending on logger and SDK version.
# --filter goes IMMEDIATELY AFTER `test`, never appended at the end. Several call sites end their
# argument list with `-- RunConfiguration.TestSessionTimeout=...`, and everything after that `--` is
# runsettings, not CLI options — appending the filter there would silently stop filtering and run the
# whole shard. Caught before wiring, by reading a call site rather than assuming the shape.
"$DOTNET_BIN" test --filter "$FILTER" "$@" 2>&1 | tee "$LOG"
TEST_EXIT="${PIPESTATUS[0]}"

# Invoked via `bash`, NOT by path. A path invocation requires the executable bit, which lives in
# git's index and NOT on the filesystem under Windows/NTFS -- so it is silently dropped by any
# file-copy-based mirror and the gate then dies with exit 126 (found, not executable) in CI while
# working locally. `bash <path>` ignores the mode entirely, which is how every other call site in
# this repo already invokes its gates. Do not "optimise" this back to a bare path.
bash "$SELF_DIR/assert-tests-executed.sh" --filter "$FILTER" < "$LOG"
ASSERT_EXIT=$?

# A REAL failure outranks the zero-executed refusal: if tests ran and failed, that is the finding, and
# reporting REFUSE instead would hide it behind a plumbing complaint.
if [ "$TEST_EXIT" -ne 0 ]; then
    echo "[run-filtered-tests] test run failed (exit $TEST_EXIT)" >&2
    exit "$TEST_EXIT"
fi

# The assertion has exactly one finding it is entitled to report: E_ZERO_EXECUTED. Any OTHER non-zero
# means the assertion did not run or malfunctioned -- 126 (found, not executable), 127 (not found), a
# crash. Those are REFUSALS, and reporting them as "executed NOTHING" states a finding that was never
# made: the run above may have executed and passed hundreds of tests. A gate that cannot run must say
# so, never invent a specific result. Both still fail closed; only the diagnosis differs.
if [ "$ASSERT_EXIT" -eq "$E_ZERO_EXECUTED" ]; then
    echo "[run-filtered-tests] the run reported success having executed NOTHING — treating as a failure, not a pass." >&2
    exit "$E_ZERO_EXECUTED"
fi

if [ "$ASSERT_EXIT" -ne 0 ]; then
    echo "[run-filtered-tests] REFUSE: could not evaluate whether tests executed —" \
         "'$SELF_DIR/assert-tests-executed.sh' exited $ASSERT_EXIT (126=not executable, 127=not found)." \
         "This is NOT a finding about the test run, which reported exit $TEST_EXIT." >&2
    exit "$E_ENV"
fi

exit "$E_OK"
