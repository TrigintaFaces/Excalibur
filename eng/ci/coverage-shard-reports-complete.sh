#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
#
# Coverage aggregation completeness gate.
#
# WHY THIS EXISTS. Every coverage shard used to upload its report under the SAME internal path
# (merged/coverage.cobertura.xml). The aggregation job downloaded them with merge-multiple, so six
# concurrent writers landed on one file: the result was a corrupt 31MB document that ReportGenerator
# could not parse -- and ReportGenerator EXITED 0 anyway, having read nothing. The threshold script
# then died parsing the corrupt input, so the coverage number was never computed. A blocking gate
# reported "failure" for a reason that had nothing to do with coverage.
#
# The structural fix is upstream (one artifact per shard, extracted to its own directory). This gate
# is the arm that proves the fix is still holding: it answers "did the aggregation actually receive
# one report from EVERY shard?" -- and REFUSES distinctly when it did not.
#
# A missing report and a low coverage number are OPPOSITE problems with opposite fixes, and the
# aggregation could not previously tell them apart. `Missing reports fail distinctly from "coverage
# below threshold"` is Item 2.4's acceptance criterion; this is that criterion, mechanised.
#
# EXIT CODES -- distinct on purpose. Do not collapse them.
#   0  every expected shard reported
#   3  REFUSE: at least one expected shard produced no report (an INCOMPLETE measurement)
#   4  REFUSE: the expected shard set could not be determined (an UNKNOWABLE measurement)
#
# 3 and 4 are both REFUSE, not PASS and not "coverage is low". An absent report is not a clean
# report.
set -euo pipefail

EXIT_INCOMPLETE=3
EXIT_UNKNOWABLE=4

COVERAGE_ROOT="coverage"
# Defaults follow the LIVE aggregation. They used to name quality-gates.yml's coverage-shards job,
# which re-ran the same six unit shards purely to collect coverage a second time; that duplicate is
# gone and coverage is now aggregated from the unit shards that already produce it. A default naming
# a deleted job would REFUSE (exit 4) rather than pass, which is the safe direction -- but a gate
# whose defaults point at nothing is one careless invocation away from being noise.
WORKFLOW=".github/workflows/ci.yml"
MATRIX_JOB="unit-tests"
REPORT_NAME="coverage.cobertura.xml"
# Artifact directory prefix. The shard's report lands under <prefix><shard>/ once the run's
# artifacts are downloaded without merge-multiple. Parameterised because the same completeness
# question is asked of more than one workflow, and hardcoding one workflow's naming is how a gate
# ends up silently inapplicable to the place it was moved to.
ARTIFACT_PREFIX="code-coverage-unit-"
SELF_TEST=0

usage() {
    cat <<'EOF'
Usage: coverage-shard-reports-complete.sh [options]
  --coverage-root DIR   directory the shard artifacts were extracted into (default: coverage)
  --workflow FILE       workflow declaring the shard matrix (default: .github/workflows/ci.yml)
  --job NAME            matrix job key to read the shard names from (default: unit-tests)
  --self-test           prove this gate is non-vacuous, then exit
EOF
}

while [ $# -gt 0 ]; do
    case "$1" in
        --coverage-root) COVERAGE_ROOT="$2"; shift 2 ;;
        --workflow)      WORKFLOW="$2"; shift 2 ;;
        --job)           MATRIX_JOB="$2"; shift 2 ;;
        --report-name)   REPORT_NAME="$2"; shift 2 ;;
        --artifact-prefix) ARTIFACT_PREFIX="$2"; shift 2 ;;
        --self-test)     SELF_TEST=1; shift ;;
        -h|--help)       usage; exit 0 ;;
        *) printf 'unknown argument: %s\n' "$1" >&2; usage >&2; exit "$EXIT_UNKNOWABLE" ;;
    esac
done

# Read the shard names from the workflow's own matrix, so the expected set cannot drift away from
# the set that actually runs. Scoped to the one job: the file declares more than one matrix.
expected_shards() {
    local workflow="$1" job="$2"
    awk -v job="  ${job}:" '
        $0 == job                        { in_job = 1; next }
        in_job && /^  [A-Za-z0-9_-]+:/   { in_job = 0 }
        in_job && /^[[:space:]]*- \{[[:space:]]*name:/ {
            line = $0
            sub(/^[[:space:]]*- \{[[:space:]]*name:[[:space:]]*/, "", line)
            sub(/[,}].*$/, "", line)
            gsub(/[[:space:]"]/, "", line)
            if (line != "") print line
        }
    ' "$workflow"
}

run_gate() {
    local coverage_root="$1" workflow="$2" job="$3"

    if [ ! -f "$workflow" ]; then
        printf '::error::REFUSE: workflow %s not found -- the expected shard set is unknowable.\n' "$workflow" >&2
        return "$EXIT_UNKNOWABLE"
    fi

    local shards
    shards="$(expected_shards "$workflow" "$job")"
    if [ -z "$shards" ]; then
        printf '::error::REFUSE: no shard names parsed from %s job %s -- the expected shard set is unknowable. An empty expectation would make this gate vacuous, so it REFUSES instead of passing.\n' "$workflow" "$job" >&2
        return "$EXIT_UNKNOWABLE"
    fi

    if [ ! -d "$coverage_root" ]; then
        printf '::error::REFUSE: coverage root %s does not exist -- no shard reported.\n' "$coverage_root" >&2
        return "$EXIT_INCOMPLETE"
    fi

    local expected=0 found=0 missing=""
    while IFS= read -r shard; do
        [ -n "$shard" ] || continue
        expected=$((expected + 1))
        # The artifact for shard X extracts to <root>/coverage-shard-X/**. Match on that directory
        # so two shards cannot satisfy each other's expectation.
        local hits
        hits="$(find "${coverage_root}" -type f -name "$REPORT_NAME" -path "*${ARTIFACT_PREFIX}${shard}/*" 2>/dev/null | wc -l | tr -d '[:space:]')"
        if [ "$hits" -eq 0 ]; then
            missing="${missing}${missing:+, }${shard}"
        else
            found=$((found + 1))
            printf 'coverage completeness: shard %-22s reported (%s file(s))\n' "$shard" "$hits"
        fi
    done <<EOF
$shards
EOF

    if [ -n "$missing" ]; then
        printf '::error::REFUSE: coverage aggregation is INCOMPLETE -- %s of %s shards reported. Missing: %s. This is NOT a coverage-below-threshold failure; the number would be computed from a partial measurement and must not be trusted or re-baselined against.\n' \
            "$found" "$expected" "$missing" >&2
        return "$EXIT_INCOMPLETE"
    fi

    printf 'coverage completeness: PASS -- all %s shards reported.\n' "$expected"
    return 0
}

# ---------------------------------------------------------------- self-test (safety AND liveness)
#
# A completeness gate that can only ever say PASS is worthless, and so is one that can only ever say
# REFUSE. Both arms are asserted here. `testing-patterns` section 3: the liveness arm is the one that
# gets forgotten, and the only one that fails when the gate is silently inert.
if [ "$SELF_TEST" -eq 1 ]; then
    tmp="$(mktemp -d)"
    trap 'rm -rf "$tmp"' EXIT

    wf="${tmp}/wf.yml"
    cat > "$wf" <<'EOF'
jobs:
  coverage-shards:
    strategy:
      matrix:
        shard:
          - { name: alpha, filter: "a.slnf" }
          - { name: beta, filter: "b.slnf" }
  other-job:
    strategy:
      matrix:
        shard:
          - { name: SHOULD_NOT_BE_READ, filter: "x.slnf" }
EOF

    # The awk scope is relative to a two-space job indent; normalise the fixture to that shape.
    sed -i 's/^  coverage-shards:/  coverage-shards:/' "$wf" 2>/dev/null || true
    fixture_wf="${tmp}/scoped.yml"
    tail -n +2 "$wf" > "$fixture_wf"

    parsed="$(expected_shards "$fixture_wf" "coverage-shards" | tr '\n' ' ' | sed 's/ $//')"
    if [ "$parsed" != "alpha beta" ]; then
        printf 'SELF-TEST: FAIL -- matrix parse produced [%s], expected [alpha beta]. Either the parser is broken or it leaked into a sibling job, which would silently change what this gate expects.\n' "$parsed" >&2
        exit 1
    fi
    printf 'SELF-TEST: PASS -- shard names parsed, and scoped to their own job (SHOULD_NOT_BE_READ excluded)\n'

    # LIVENESS: a complete set is GREEN. Without this arm a gate that refuses everything looks healthy.
    root="${tmp}/cov"
    mkdir -p "${root}/${ARTIFACT_PREFIX}alpha/merged" "${root}/${ARTIFACT_PREFIX}beta/merged"
    : > "${root}/${ARTIFACT_PREFIX}alpha/merged/${REPORT_NAME}"
    : > "${root}/${ARTIFACT_PREFIX}beta/merged/${REPORT_NAME}"
    status=0; run_gate "$root" "$fixture_wf" "coverage-shards" >/dev/null || status=$?
    if [ "$status" -ne 0 ]; then
        printf 'SELF-TEST: FAIL -- a COMPLETE set was refused (exit %s). This gate would block every green run.\n' "$status" >&2
        exit 1
    fi
    printf 'SELF-TEST: PASS -- a complete shard set is GREEN (liveness)\n'

    # SAFETY: one shard missing REFUSES, with the incomplete code specifically.
    rm -rf "${root}/${ARTIFACT_PREFIX}beta"
    status=0; run_gate "$root" "$fixture_wf" "coverage-shards" >/dev/null 2>&1 || status=$?
    if [ "$status" -ne "$EXIT_INCOMPLETE" ]; then
        printf 'SELF-TEST: FAIL -- a MISSING shard exited %s, expected %s. The gate is vacuous: a partial measurement would be reported as a coverage number.\n' "$status" "$EXIT_INCOMPLETE" >&2
        exit 1
    fi
    printf 'SELF-TEST: PASS -- a missing shard REFUSES with the incomplete code (safety)\n'

    # SAFETY: one shard cannot satisfy another's expectation.
    mkdir -p "${root}/${ARTIFACT_PREFIX}alpha/merged/extra"
    : > "${root}/${ARTIFACT_PREFIX}alpha/merged/extra/${REPORT_NAME}"
    status=0; run_gate "$root" "$fixture_wf" "coverage-shards" >/dev/null 2>&1 || status=$?
    if [ "$status" -ne "$EXIT_INCOMPLETE" ]; then
        printf 'SELF-TEST: FAIL -- two reports from ONE shard satisfied a two-shard expectation (exit %s). Counting files instead of shards is how a partial run reports a full number.\n' "$status" >&2
        exit 1
    fi
    printf 'SELF-TEST: PASS -- a duplicate from one shard does not satisfy another shard (safety)\n'

    # UNKNOWABLE: an unparseable expectation REFUSES rather than passing on an empty set.
    : > "${tmp}/empty.yml"
    status=0; run_gate "$root" "${tmp}/empty.yml" "coverage-shards" >/dev/null 2>&1 || status=$?
    if [ "$status" -ne "$EXIT_UNKNOWABLE" ]; then
        printf 'SELF-TEST: FAIL -- an EMPTY expectation exited %s, expected %s. A gate that expects nothing passes everything.\n' "$status" "$EXIT_UNKNOWABLE" >&2
        exit 1
    fi
    printf 'SELF-TEST: PASS -- an undeterminable shard set REFUSES rather than expecting nothing\n'

    printf 'SELF-TEST: the coverage completeness gate is non-vacuous.\n'
    exit 0
fi

status=0
run_gate "$COVERAGE_ROOT" "$WORKFLOW" "$MATRIX_JOB" || status=$?
exit "$status"
