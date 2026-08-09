#!/usr/bin/env bash
# build-entrypoint-compose.test.sh — lock the `dotnet test` composition in the build entry point.
#
# WHY THIS EXISTS
#   The composite action used to compose its own `dotnet test`, and the entry point composed a
#   different one. Two implementations of the same command is drift waiting to happen, and the drift
#   would be SILENT: every trap below produces a run that still starts, still passes, and simply
#   ignores a setting.
#
#   Now there is one implementation and the action calls it. These arms assert the traps the old
#   implementation described in its own comments, because a comment does not fail.
#
#   Each arm inspects the COMPOSED ARGV rather than running anything. That is the only place these
#   defects are visible: at runtime they are invisible by construction.
#
# A FAILED PROBE IS REPORTED SEPARATELY FROM A WRONG COMPOSITION, and that distinction was learned
# the hard way. The first version piped pwsh straight into a grep, so a probe that produced nothing
# looked exactly like a composition missing every setting: twelve arms failed with empty values and
# not one of them said why. That is the same defect these arms exist to catch, committed by the tool
# doing the catching -- an unmeasured result reported as a measured failure.

set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT" || { echo "cannot cd to repo root" >&2; exit 64; }
FAILED=0
LAST_RAW=""

if ! command -v pwsh >/dev/null 2>&1; then
    echo "SELF-TEST: REFUSE -- pwsh is not on PATH, so the entry point cannot be asked what it" >&2
    echo "  composes. Nothing was measured; that is not a pass." >&2
    exit 2
fi

# ANSI is stripped and the match is not anchored: pwsh emits escape sequences on some hosts and not
# others, and a leading escape makes an anchored '^\[build\]' miss a line that is present and correct.
compose() {
    LAST_RAW="$(pwsh -NoProfile -NonInteractive -File eng/build.ps1 \
        -Test -NoRestore -NoBuild -ShowTestCommand \
        -Project eng/ci/shards/UnitTests-Core.slnf "$@" 2>&1)"
    printf '%s' "$LAST_RAW" \
        | sed 's/\x1b\[[0-9;]*[a-zA-Z]//g' \
        | grep 'dotnet test' \
        | head -1
}

probe_ok() {
    [ -n "$1" ] && return 0
    {
        echo "SELF-TEST: REFUSE -- the entry point produced no composed command, so NOTHING was"
        echo "  measured. This is a failed probe, not a wrong composition. Raw output:"
        printf '%s\n' "$LAST_RAW" | sed 's/^/    | /'
    } >&2
    FAILED=1
    return 1
}

assert_contains() {
    local label="$1" line="$2" needle="$3"
    if printf '%s' "$line" | grep -qF -- "$needle"; then
        printf 'SELF-TEST: PASS -- %s\n' "$label"
    else
        printf 'SELF-TEST: FAIL -- %s\n    wanted: %s\n    got   : %s\n' "$label" "$needle" "$line" >&2
        FAILED=1
    fi
}

assert_separator_count() {
    local label="$1" line="$2" want="$3" n
    # A standalone ' -- ' more than once means an earlier branch emitted its own separator and
    # everything a later branch added was swallowed.
    n="$(printf '%s' "$line" | grep -oE ' -- ' | wc -l | tr -d '[:space:]')"
    if [ "$n" = "$want" ]; then
        printf 'SELF-TEST: PASS -- %s\n' "$label"
    else
        printf 'SELF-TEST: FAIL -- %s (expected %s separator(s), found %s)\n    %s\n' \
            "$label" "$want" "$n" "$line" >&2
        FAILED=1
    fi
}

echo "== composing against the real entry point =="

both="$(compose -Coverage -TestSessionTimeout 1500000 \
    -ExtraRunSettings 'xUnit.MaxParallelThreads=1 xUnit.ParallelizeTestCollections=false' \
    -MaxCpuCount 1 -BlameTimeout 5m -ResultsPrefix unit-core -ResultsDirectory ./TestResults)"

if probe_ok "$both"; then
    assert_separator_count "coverage + session timeout + extras emit exactly ONE separator" "$both" 1
    assert_contains "coverage format is a RunSetting" "$both" 'Format=cobertura'
    assert_contains "session timeout is a RunSetting" "$both" 'RunConfiguration.TestSessionTimeout=1500000'
    assert_contains "extra run settings are word-split, not one token" "$both" \
        'xUnit.MaxParallelThreads=1 xUnit.ParallelizeTestCollections=false'
    assert_contains "blame timeout is present" "$both" '--blame-hang-timeout 5m'
    assert_contains "results prefix reaches the trx logger" "$both" 'trx;LogFilePrefix=unit-core'
    assert_contains "results directory is honoured" "$both" '--results-directory ./TestResults'

    # -m:N BEFORE the separator. After it, dotnet parses it as a setting name and it silently does
    # nothing -- the trap the original implementation called out by name.
    assert_contains "-m:N precedes the RunSettings separator" "${both%% -- *}" '-m:1'
fi

# The session timeout must apply WITHOUT coverage. It once lived inside the coverage branch, leaving
# it inert on exactly the runs most likely to wedge.
nocov="$(compose -TestSessionTimeout 900000)"
if probe_ok "$nocov"; then
    assert_contains "session timeout applies without coverage" "$nocov" 'RunConfiguration.TestSessionTimeout=900000'
    assert_separator_count "a settings-only run still emits ONE separator" "$nocov" 1
fi

# Nothing CI-shaped set: a contributor's plain run must not grow a separator at all.
plain="$(compose)"
if probe_ok "$plain"; then
    assert_separator_count "a plain contributor run emits NO separator" "$plain" 0
    assert_contains "a plain run still logs trx" "$plain" 'trx;LogFilePrefix=build'
fi

if [ "$FAILED" -eq 0 ]; then
    printf 'SELF-TEST: all arms passed -- the test-command composition is locked.\n'
    exit 0
fi
printf 'SELF-TEST: at least one arm failed.\n' >&2
exit 1
