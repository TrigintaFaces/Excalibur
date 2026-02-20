#!/bin/bash
# Coverage enforcement script for Excalibur.Dispatch
# Usage: ./eng/run-coverage.sh [--threshold 95] [--component Transport] [--skip-build] [--html]

set -e

# Defaults
THRESHOLD=95
COMPONENT=""
SKIP_BUILD=false
GENERATE_HTML=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --threshold)
            THRESHOLD="$2"
            shift 2
            ;;
        --component)
            COMPONENT="$2"
            shift 2
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --html)
            GENERATE_HTML=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

echo "=== Excalibur Coverage Runner ==="

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SOLUTION_ROOT="$(dirname "$SCRIPT_DIR")"
TESTS_DIR="$SOLUTION_ROOT/tests"
ARTIFACTS_DIR="$SOLUTION_ROOT/artifacts/coverage"
RUNSETTINGS_PATH="$TESTS_DIR/coverage.runsettings"

# Ensure artifacts directory exists
mkdir -p "$ARTIFACTS_DIR"

# Build filter
FILTER=""
if [ -n "$COMPONENT" ]; then
    FILTER="--filter \"Component=$COMPONENT\""
    echo "Filtering tests by Component: $COMPONENT"
fi

# Build if requested
if [ "$SKIP_BUILD" = false ]; then
    echo ""
    echo "Building solution..."
    dotnet build "$SOLUTION_ROOT/Excalibur.sln" -v q -c Release
fi

# Run tests with coverage
echo ""
echo "Running tests with coverage collection..."
eval dotnet test "$SOLUTION_ROOT/Excalibur.sln" \
    --collect:"XPlat Code Coverage" \
    --settings "$RUNSETTINGS_PATH" \
    --results-directory "$ARTIFACTS_DIR" \
    -c Release --no-build -v q $FILTER || true

TEST_EXIT_CODE=$?

# Find coverage files
echo ""
echo "Collecting coverage results..."
COVERAGE_COUNT=$(find "$ARTIFACTS_DIR" -name "coverage.cobertura.xml" | wc -l)

if [ "$COVERAGE_COUNT" -eq 0 ]; then
    echo "No coverage files found!"
    exit 1
fi

echo "Found $COVERAGE_COUNT coverage file(s)"

# Generate merged report
REPORT_TYPES="TextSummary"
if [ "$GENERATE_HTML" = true ]; then
    REPORT_TYPES="TextSummary;Html;Cobertura"
fi

COVERAGE_PATTERN="$ARTIFACTS_DIR/**/coverage.cobertura.xml"
REPORT_DIR="$ARTIFACTS_DIR/report"

echo ""
echo "Generating coverage report..."
reportgenerator \
    -reports:"$COVERAGE_PATTERN" \
    -targetdir:"$REPORT_DIR" \
    -reporttypes:"$REPORT_TYPES" \
    -assemblyfilters:"+Excalibur.Dispatch.*;+Excalibur.*" \
    -classfilters:"-*.Tests.*;-*.Benchmarks.*"

# Read and display summary
SUMMARY_PATH="$REPORT_DIR/Summary.txt"
if [ -f "$SUMMARY_PATH" ]; then
    echo ""
    echo "=== Coverage Summary ==="
    head -20 "$SUMMARY_PATH"

    # Extract line coverage percentage
    LINE_COVERAGE=$(grep "Line coverage:" "$SUMMARY_PATH" | grep -oP '\d+(?=%)')

    if [ -n "$LINE_COVERAGE" ]; then
        echo ""
        echo "=== Coverage Gate ==="
        echo "Threshold: $THRESHOLD%"
        echo "Actual:    $LINE_COVERAGE%"

        if [ "$LINE_COVERAGE" -lt "$THRESHOLD" ]; then
            echo ""
            echo "COVERAGE GATE FAILED! Line coverage $LINE_COVERAGE% is below threshold $THRESHOLD%"
            exit 1
        else
            echo ""
            echo "COVERAGE GATE PASSED!"
        fi
    fi
fi

# Copy summary to artifacts
cp "$SUMMARY_PATH" "$ARTIFACTS_DIR/summary.txt" 2>/dev/null || true

if [ "$GENERATE_HTML" = true ]; then
    HTML_REPORT="$REPORT_DIR/index.html"
    if [ -f "$HTML_REPORT" ]; then
        echo ""
        echo "HTML report generated at: $HTML_REPORT"
    fi
fi

echo ""
echo "Coverage analysis complete."

exit $TEST_EXIT_CODE
