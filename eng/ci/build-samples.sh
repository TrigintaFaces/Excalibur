#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: Apache-2.0
#
# build-samples.sh -- CI gate: build ALL sample projects with -warnaserror
#
# This script discovers every *.csproj under samples/ (excluding obj/ dirs)
# and builds each one individually in Release configuration with warnings as errors.
# Any build failure causes the script to exit non-zero, blocking the CI gate.
#
# Usage:
#   ./eng/ci/build-samples.sh              # Build all samples
#   ./eng/ci/build-samples.sh --parallel   # Build all samples in parallel (faster, noisier output)
#
# Exit codes:
#   0 = All samples built successfully
#   1 = One or more samples failed to build

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
SAMPLES_DIR="$REPO_ROOT/samples"

PARALLEL=false
if [[ "${1:-}" == "--parallel" ]]; then
    PARALLEL=true
fi

# Discover all sample projects, excluding obj/ directories
mapfile -t PROJECTS < <(find "$SAMPLES_DIR" -name "*.csproj" -not -path "*/obj/*" | sort)

TOTAL=${#PROJECTS[@]}
PASSED=0
FAILED=0
FAILED_PROJECTS=()

echo "============================================"
echo " Sample Compilation CI Gate"
echo "============================================"
echo "Discovered $TOTAL sample projects"
echo "Configuration: Release"
echo "Warnings as errors: enabled"
echo ""

build_project() {
    local project="$1"
    local index="$2"
    local name
    name=$(basename "$project" .csproj)
    local rel_path="${project#"$REPO_ROOT/"}"

    echo "[$index/$TOTAL] Building $name ($rel_path)..."

    if dotnet build "$project" -c Release -warnaserror --nologo -v quiet 2>&1; then
        echo "  PASS: $name"
        return 0
    else
        echo "  FAIL: $name"
        return 1
    fi
}

if $PARALLEL; then
    # Parallel mode: build all projects concurrently, collect results
    PIDS=()
    TEMP_DIR=$(mktemp -d)
    trap 'rm -rf "$TEMP_DIR"' EXIT

    for i in "${!PROJECTS[@]}"; do
        project="${PROJECTS[$i]}"
        idx=$((i + 1))
        (
            if build_project "$project" "$idx"; then
                touch "$TEMP_DIR/pass_$idx"
            else
                touch "$TEMP_DIR/fail_$idx"
                basename "$project" .csproj > "$TEMP_DIR/failname_$idx"
            fi
        ) &
        PIDS+=($!)
    done

    # Wait for all builds
    for pid in "${PIDS[@]}"; do
        wait "$pid" 2>/dev/null || true
    done

    # Collect results
    PASSED=$(find "$TEMP_DIR" -name "pass_*" | wc -l)
    FAILED=$(find "$TEMP_DIR" -name "fail_*" | wc -l)
    for f in "$TEMP_DIR"/failname_*; do
        [ -f "$f" ] && FAILED_PROJECTS+=("$(cat "$f")")
    done
else
    # Sequential mode: build one at a time
    for i in "${!PROJECTS[@]}"; do
        project="${PROJECTS[$i]}"
        idx=$((i + 1))

        if build_project "$project" "$idx"; then
            PASSED=$((PASSED + 1))
        else
            FAILED=$((FAILED + 1))
            FAILED_PROJECTS+=("$(basename "$project" .csproj)")
        fi
    done
fi

echo ""
echo "============================================"
echo " Results"
echo "============================================"
echo "Total:  $TOTAL"
echo "Passed: $PASSED"
echo "Failed: $FAILED"

if [[ $FAILED -gt 0 ]]; then
    echo ""
    echo "Failed projects:"
    for proj in "${FAILED_PROJECTS[@]}"; do
        echo "  - $proj"
    done
    echo ""
    echo "GATE: FAILED"
    exit 1
fi

echo ""
echo "GATE: PASSED (all $TOTAL samples build with 0 errors, 0 warnings)"
exit 0
