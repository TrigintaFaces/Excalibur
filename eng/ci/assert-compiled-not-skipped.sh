#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
#
# assert-compiled-not-skipped.sh — REFUSE a `dotnet build` that COMPILED ZERO SOURCE FILES.
#
# WHY THIS EXISTS:
#   This is the BUILD analogue of assert-tests-executed.sh. That gate exists because a filtered
#   `dotnet test` matching nothing still exits 0. This one exists because a SKIPPED project still
#   exits 0 — and prints "0 Error(s)" while compiling nothing at all.
#
#   Directory.Build.targets marks any project under \tests\ or \benchmarks\ as SkipProjectBuild
#   unless -p:BuildExamplesAndTests=true, and then does `<Compile Remove="**\*.cs" />`. The project
#   does not fail. It does not warn. It removes its own source and succeeds.
#
#   Measured, both arms, one variable: a SKIPPED project and a FULLY COMPILED one are INDISTINGUISHABLE
#   at every surface anyone reads — same exit 0, same "0 Error(s)", same warning count. Exactly ONE
#   line in the log tells them apart:
#
#       Skipping compile for <Project> (examples/tests/benchmarks disabled)
#
#   So every "the solution builds clean" claim in this repository is conditional on a property whose
#   value nobody states, and the evidence for it looks identical whether it held or not. Several seats
#   spent an evening disputing which of their builds had covered what, and the dispute was
#   unresolvable from the exit codes alone — because the exit codes do not carry the information.
#
#   THE CAUSE IS STILL UNKNOWN and this gate does not depend on knowing it: gate on the ARTIFACT, ship
#   without the explanation. Whatever makes the property vary, the log line is the ground truth.
#
# USAGE:
#   Pipe the combined stdout+stderr of a `dotnet build` into this script:
#
#     dotnet build <project-or-sln> -c Release 2>&1 | tee build.log
#     eng/ci/assert-compiled-not-skipped.sh build.log
#
#   Or read stdin:   dotnet build ... 2>&1 | eng/ci/assert-compiled-not-skipped.sh
#
#   ALLOWLIST: if a skipped project is expected in a given invocation, pass its name(s):
#     eng/ci/assert-compiled-not-skipped.sh build.log --expect-skipped Foo.Tests --expect-skipped Bar
#   An expected skip is reported and does not fail; an UNEXPECTED one refuses. This is deliberately a
#   SET, not a count — one project leaving the set while another enters keeps the count identical and
#   hides the new gap.
#
# Exit: 0 = every project in the log compiled (or its skip was expected)
#       1 = a project was SKIPPED and not expected — the build's "0 Error(s)" does not cover it
#       2 = cannot evaluate (no log, unreadable, or a log with no build output at all)
#
# Self-test: eng/ci/assert-compiled-not-skipped.sh --self-test

set -uo pipefail

readonly E_OK=0
readonly E_SKIPPED=1
readonly E_ENV=2

readonly SKIP_MARKER='Skipping compile for'

if [ "${1:-}" = "--self-test" ]; then
    exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/assert-compiled-not-skipped.test.sh"
fi

LOG=""
EXPECTED=()
while [ $# -gt 0 ]; do
    case "$1" in
        --expect-skipped) shift; [ $# -gt 0 ] || { echo "[assert-compiled-not-skipped] --expect-skipped needs a project name" >&2; exit "$E_ENV"; }; EXPECTED+=("$1") ;;
        -*) echo "[assert-compiled-not-skipped] unknown option: $1" >&2; exit "$E_ENV" ;;
        *) LOG="$1" ;;
    esac
    shift
done

CONTENT=""
if [ -n "$LOG" ]; then
    [ -f "$LOG" ] || { echo "[assert-compiled-not-skipped] CANNOT EVALUATE — log not found: $LOG" >&2; exit "$E_ENV"; }
    CONTENT="$(cat "$LOG" 2>/dev/null)"
else
    CONTENT="$(cat 2>/dev/null)"
fi

if [ -z "$CONTENT" ]; then
    echo "[assert-compiled-not-skipped] CANNOT EVALUATE — empty build log. An absent log is not a clean build." >&2
    exit "$E_ENV"
fi

# A log carrying no build output at all cannot be graded. Refusing here is the point: reporting "no
# skips found" over a log that never contained a build is the same vacuous green this gate targets.
build_output_lines="$(printf '%s' "$CONTENT" | grep -cE 'Error\(s\)|error [A-Z]+[0-9]+|Build succeeded|Build FAILED|MSBuild version' || true)"
if [ "${build_output_lines:-0}" -eq 0 ]; then
    echo "[assert-compiled-not-skipped] CANNOT EVALUATE — no MSBuild output in the log; nothing to grade." >&2
    exit "$E_ENV"
fi

# VERBOSITY REFUSAL — the gate's own blind spot, and it is the one that would have made it decorative.
#
# The skip marker is a <Message Importance="High">, and `dotnet build --verbosity quiet` DISCARDS it.
# Measured, one variable, same project: at `--verbosity normal` a skipped build emits the marker twice;
# at `--verbosity quiet` it emits it ZERO times and the log is otherwise identical — same "Build
# succeeded.", same "0 Error(s)". So over a quiet log this gate would find no marker and report PASS,
# which is precisely the false green it exists to prevent, reproduced inside the instrument.
#
# A quiet log is not evidence of no skips; it is evidence of nothing. Per-project evidence — a `->`
# output line, a compiler invocation, or the marker itself — is present at normal verbosity and absent
# at quiet, so its total absence means the log cannot answer the question and the honest verdict is
# REFUSE. Callers must pipe a build run at `--verbosity normal` (or higher).
# `grep -c`, NOT `grep -q`. Under `set -o pipefail` a `grep -q` exits at its FIRST match, the writer
# upstream takes SIGPIPE, and the pipeline reports 141 — so a log that DOES match reads as no-match,
# and the earlier the evidence appears the more reliably it misfires. Reproduced here: a normal-verbosity
# log carrying 24 evidence lines was REFUSED. `-c` consumes all input, so its status is the real answer.
evidence_lines="$(printf '%s' "$CONTENT" | grep -cE "(^|[[:space:]])-> |Csc|CSC|${SKIP_MARKER}" || true)"
if [ "${evidence_lines:-0}" -eq 0 ]; then
    echo "[assert-compiled-not-skipped] CANNOT EVALUATE — the log carries no per-project compile evidence." >&2
    echo "  This is what a '--verbosity quiet' build looks like: the skip marker is emitted at High" >&2
    echo "  importance and quiet discards it, so a SKIPPED project and a COMPILED one are identical here." >&2
    echo "  Re-run the build with --verbosity normal and pipe that log in. REFUSE is not a pass." >&2
    exit "$E_ENV"
fi

# Portability: `mapfile` is bash 4+, and macOS ships bash 3.2 -- this gate died there with
# "mapfile: command not found" and then "SKIPPED[@]: unbound variable", because bash 3.2 under
# `set -u` treats an empty array as unset. Both are avoided by streaming the lines through a
# plain `while read` instead of materialising an array. The loop body runs in the current shell
# (redirect at `done`, not a pipe), so `unexpected` and `skip_count` survive it.
skip_list="$(printf '%s' "$CONTENT" | grep -oE "${SKIP_MARKER} [^ ]+" | sed -E "s/${SKIP_MARKER} //" | LC_ALL=C sort -u)"

unexpected=0
skip_count=0
while IFS= read -r proj; do
    [ -n "$proj" ] || continue
    skip_count=$((skip_count + 1))
    ok=0
    for e in ${EXPECTED+"${EXPECTED[@]}"}; do
        [ "$proj" = "$e" ] && ok=1 && break
    done
    if [ "$ok" -eq 1 ]; then
        echo "[assert-compiled-not-skipped] expected skip: $proj"
    else
        if [ "$unexpected" -eq 0 ]; then
            echo "[assert-compiled-not-skipped] BUILD DID NOT COMPILE WHAT IT APPEARS TO HAVE COMPILED." >&2
            echo "  These projects were SKIPPED — their source was removed and they exited 0." >&2
            echo "  The build's success and its \"0 Error(s)\" say NOTHING about them:" >&2
        fi
        echo "    SKIPPED, NOT COMPILED: $proj" >&2
        unexpected=$((unexpected + 1))
    fi
done <<EOF
$skip_list
EOF

if [ "$unexpected" -ne 0 ]; then
    echo "  Fix: pass -p:BuildExamplesAndTests=true, or declare the skip with --expect-skipped <name>." >&2
    exit "$E_SKIPPED"
fi

echo "[assert-compiled-not-skipped] PASS — no unexpected skips; the build covers what it claims ($skip_count expected skip(s))."
exit "$E_OK"
