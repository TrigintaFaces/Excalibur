#!/usr/bin/env bash
# orphan-test-project-gate.test.sh — non-vacuous self-test for orphan-test-project-gate.sh (0he3g1).
#
#   SAFETY    an integration test csproj on disk but NOT in the filter  -> exit 1 (orphan detected)
#   LIVENESS  every integration test csproj present in the filter        -> exit 0 (no false-positive)
#   ENV       a missing filter / tests dir                               -> exit 2 (not a silent pass)
#
# Every arm drives the REAL gate over an ISOLATED fixture (its own TESTS_ROOT + FILTER_FILE), asserting
# an EXACT exit code — so "cannot evaluate" can never masquerade as "the property holds", and a gate that
# rejected everything (or accepted everything) is caught.
#
# Usage: bash eng/ci/orphan-test-project-gate.test.sh   (exit 0 = all green)

set -uo pipefail

# ── Git-env isolation (xy3hze) — MUST precede the first git call ────────────────────────────────
# git EXPORTS GIT_INDEX_FILE / GIT_DIR / GIT_WORK_TREE into every hook and every child process.
# This script `git init`s its own throwaway fixture repos — but an inherited GIT_INDEX_FILE is an
# ABSOLUTE PATH and WINS over the repo you are standing in, so `git add` inside the fixture writes
# the CALLER'S index instead. `git init` does not rescue you; neither does `cd`.
#
# Measured consequence, S890: run from a normal shell this script passed; run from pre-commit (where
# git had exported GIT_INDEX_FILE) every arm failed AND it staged its own fixtures — including an
# AWS-shaped token and an RSA private-key header — into the real repo's index, one arm at a time.
# The standalone GREEN is the disguise: the only environment that reproduces it is the one the gate
# actually runs in, and that is the one nobody tested.
#
# Unset before the first git call, NOT inside the subshells (they inherit it either way).
unset GIT_INDEX_FILE GIT_DIR GIT_WORK_TREE GIT_OBJECT_DIRECTORY GIT_COMMON_DIR

GATE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/orphan-test-project-gate.sh"
PASS=0; FAIL=0
ok()  { PASS=$((PASS + 1)); printf '  PASS  %s\n' "$1"; }
bad() { FAIL=$((FAIL + 1)); printf '  FAIL  %s\n     -> %s\n' "$1" "$2" >&2; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK" 2>/dev/null || true' EXIT

# Build a fixture: a tests tree with one integration test project, and a filter that lists it (or not).
#   $1 = "in-filter" | "orphan" | "no-filter" | "no-tests"
make_fixture() {
    local mode="$1" dir; dir="$WORK/$mode"
    rm -rf "$dir"; mkdir -p "$dir/tests/integration/Foo.Integration.Tests" "$dir/eng/ci/shards"
    if [ "$mode" != "no-tests" ]; then
        printf '<Project/>\n' > "$dir/tests/integration/Foo.Integration.Tests/Foo.Integration.Tests.csproj"
    fi
    case "$mode" in
        in-filter) printf '{ "solution": { "projects": [ "tests\\\\integration\\\\Foo.Integration.Tests\\\\Foo.Integration.Tests.csproj" ] } }\n' > "$dir/eng/ci/shards/IntegrationTests.slnf" ;;
        orphan)    printf '{ "solution": { "projects": [ "tests\\\\unit\\\\Other.Tests\\\\Other.Tests.csproj" ] } }\n' > "$dir/eng/ci/shards/IntegrationTests.slnf" ;;
        no-filter) : ;;  # no slnf written
        no-tests)  printf '{ "solution": { "projects": [] } }\n' > "$dir/eng/ci/shards/IntegrationTests.slnf" ;;
    esac
    printf '%s' "$dir"
}

run() {  # $1 = fixture dir. Runs the REAL gate against the fixture's TESTS_ROOT + FILTER_FILE. Sets RC.
    local d="$1"
    ( cd "$d" && git init -q 2>/dev/null; \
      TESTS_ROOT="$d/tests" FILTER_FILE="$d/eng/ci/shards/IntegrationTests.slnf" bash "$GATE" ) >/dev/null 2>&1
    RC=$?
}

echo "orphan-test-project-gate.sh — self-test (0he3g1)"
echo

d="$(make_fixture in-filter)"; run "$d"
[ "$RC" -eq 0 ] && ok "liveness: a test project listed in the filter is accepted (exit 0)" \
                || bad "liveness: an in-filter project must be accepted" "got exit $RC"

d="$(make_fixture orphan)"; run "$d"
[ "$RC" -eq 1 ] && ok "safety: an orphan test project (on disk, not in filter) is REJECTED (exit 1)" \
                || bad "safety: an orphan must be rejected with exit 1" "got exit $RC"

d="$(make_fixture no-filter)"; run "$d"
[ "$RC" -eq 2 ] && ok "env: a missing filter exits E_ENV(2), not a silent pass" \
                || bad "env: missing filter must exit 2" "got exit $RC"

d="$(make_fixture no-tests)"; run "$d"
[ "$RC" -eq 0 ] && ok "liveness: no integration test projects → vacuously clean (exit 0)" \
                || bad "no-tests must exit 0" "got exit $RC"

echo
printf 'passed %d · failed %d\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ] || exit 1
