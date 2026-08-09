#!/usr/bin/env bash
# orphan-test-project-gate.sh — every integration test project on disk must be a member of the
# integration solution filter, so CI actually BUILDS it.
#
# WHY THIS EXISTS
#   A test project committed to disk but NOT listed in the solution filter CI builds is HEAD-INVISIBLE:
#   nothing compiles it, so a missing <ProjectReference>, a broken using, or any compile error inside it
#   never surfaces on a clean checkout of committed HEAD. The break ships green. (Observed in
#   practice: tests committed without their ProjectReference → HEAD-invisible build breaks.)
#
#   Membership in the built filter is the root fix: once CI builds the project, a missing ProjectReference
#   becomes a normal compile error at CI time instead of an invisible one.
#
# WHAT IT CHECKS
#   Every  tests/**/*Integration*.Tests.csproj  on disk appears (by csproj basename) in
#   eng/ci/shards/IntegrationTests.slnf. An orphan (on disk, in no filter) FAILS the gate.
#
# Exit codes (the spa-gate.sh / bd-export-comments.sh contract):
#   0  the property holds: every integration test csproj is in the filter
#   1  the property is FALSE: an orphan integration test csproj is not in the filter
#   2  the property could not be EVALUATED: the repo root / filter is missing
#
# Overridable for the self-test (drive an isolated fixture tree):
#   TESTS_ROOT   (default <repo>/tests)                          — where to enumerate csprojs
#   FILTER_FILE  (default <repo>/eng/ci/shards/IntegrationTests.slnf) — the membership oracle
#
# Usage:  eng/ci/orphan-test-project-gate.sh [--self-test]

set -uo pipefail

readonly E_OK=0
readonly E_ORPHAN=1
readonly E_ENV=2

if [ "${1:-}" = "--self-test" ]; then
    exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/orphan-test-project-gate.test.sh"
fi

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || true)"
TESTS_ROOT="${TESTS_ROOT:-${REPO_ROOT}/tests}"
FILTER_FILE="${FILTER_FILE:-${REPO_ROOT}/eng/ci/shards/IntegrationTests.slnf}"

[ -n "$REPO_ROOT" ]     || { echo "[orphan-test-project-gate] CANNOT EVALUATE — not in a git repo." >&2; exit "$E_ENV"; }
[ -d "$TESTS_ROOT" ]    || { echo "[orphan-test-project-gate] CANNOT EVALUATE — no tests dir at $TESTS_ROOT." >&2; exit "$E_ENV"; }
[ -f "$FILTER_FILE" ]   || { echo "[orphan-test-project-gate] CANNOT EVALUATE — no filter at $FILTER_FILE." >&2; exit "$E_ENV"; }

# Enumerate integration test csprojs on disk (skip build output + any worktree copies under .dts/.claude).
orphans=0
while IFS= read -r csproj; do
    base="$(basename "$csproj")"
    # The filter lists projects by relative path; match on the csproj basename (unique per project).
    if ! grep -qF "$base" "$FILTER_FILE"; then
        echo "[orphan-test-project-gate] ORPHAN: $csproj is on disk but NOT in $(basename "$FILTER_FILE")." >&2
        echo "                           CI never builds it → a break inside it is invisible on committed HEAD." >&2
        orphans=$((orphans + 1))
    fi
done < <(find "$TESTS_ROOT" -type f -name "*Integration*.Tests.csproj" \
             -not -path "*/bin/*" -not -path "*/obj/*" 2>/dev/null | LC_ALL=C sort)

if [ "$orphans" -gt 0 ]; then
    echo "[orphan-test-project-gate] FAIL — $orphans orphan integration test project(s). Add each to" >&2
    echo "                           eng/ci/shards/IntegrationTests.slnf (and any other shard it belongs to)." >&2
    exit "$E_ORPHAN"
fi

echo "[orphan-test-project-gate] OK — every integration test project on disk is in $(basename "$FILTER_FILE")."
exit "$E_OK"
