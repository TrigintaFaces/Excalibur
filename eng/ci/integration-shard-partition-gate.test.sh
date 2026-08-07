#!/usr/bin/env bash
# integration-shard-partition-gate.test.sh — proves the gate is non-vacuous.
#
# A partition gate that can only say PASS is worthless, and so is one that can only say FAIL. Each
# arm below drives an isolated fixture tree through the real gate and asserts the exit code, so the
# gate is shown to DISCRIMINATE rather than merely to run.
#
# Exit 0 = every arm passed. Exit 1 = the gate has stopped detecting something it must detect.

set -uo pipefail

GATE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/integration-shard-partition-gate.sh"
FAILED=0

# A solution filter holding the given project paths.
write_filter() {
    local path="$1"; shift
    local projects="" p
    for p in "$@"; do
        [ -n "$projects" ] && projects="$projects, "
        projects="$projects\"$p\""
    done
    printf '{ "solution": { "path": "x.sln", "projects": [ %s ] } }\n' "$projects" > "$path"
}

# Runs the gate over a fresh fixture dir; echoes its exit code.
run_case() {
    local dir; dir="$(mktemp -d)"
    mkdir -p "$dir/eng/ci/shards"
    ( cd "$dir" && git init -q . 2>/dev/null )
    "$@" "$dir/eng/ci/shards"
    local ec
    ( cd "$dir" && SHARD_DIR="$dir/eng/ci/shards" PARENT_FILE="$dir/eng/ci/shards/IntegrationTests.slnf" \
        bash "$GATE" ) >/dev/null 2>&1
    ec=$?
    rm -rf "$dir"
    echo "$ec"
}

expect() {
    local label="$1" want="$2" got="$3"
    if [ "$got" = "$want" ]; then
        printf 'SELF-TEST: PASS -- %s\n' "$label"
    else
        printf 'SELF-TEST: FAIL -- %s (expected exit %s, got %s)\n' "$label" "$want" "$got" >&2
        FAILED=1
    fi
}

# --- fixtures -------------------------------------------------------------------------------------
SUP='tests\\Shared\\Tests.Shared\\Tests.Shared.csproj'
A='tests\\integration\\A.Tests\\A.Tests.csproj'
B='tests\\integration\\B.Tests\\B.Tests.csproj'
C='tests\\integration\\C.Tests\\C.Tests.csproj'

f_clean() { write_filter "$1/IntegrationTests.slnf" "$SUP" "$A" "$B"
            write_filter "$1/IntegrationTests-One.slnf" "$SUP" "$A"
            write_filter "$1/IntegrationTests-Two.slnf" "$SUP" "$B"; }

f_gap()   { write_filter "$1/IntegrationTests.slnf" "$SUP" "$A" "$B" "$C"
            write_filter "$1/IntegrationTests-One.slnf" "$SUP" "$A"
            write_filter "$1/IntegrationTests-Two.slnf" "$SUP" "$B"; }

f_extra() { write_filter "$1/IntegrationTests.slnf" "$SUP" "$A"
            write_filter "$1/IntegrationTests-One.slnf" "$SUP" "$A"
            write_filter "$1/IntegrationTests-Two.slnf" "$SUP" "$C"; }

f_dup()   { write_filter "$1/IntegrationTests.slnf" "$SUP" "$A" "$B"
            write_filter "$1/IntegrationTests-One.slnf" "$SUP" "$A" "$B"
            write_filter "$1/IntegrationTests-Two.slnf" "$SUP" "$B"; }

f_empty() { write_filter "$1/IntegrationTests.slnf" "$SUP" "$A"
            write_filter "$1/IntegrationTests-One.slnf" "$SUP" "$A"
            write_filter "$1/IntegrationTests-Two.slnf" "$SUP"; }

f_none()  { write_filter "$1/IntegrationTests.slnf" "$SUP" "$A"; }

# Separator style must not decide the verdict: same partition, forward slashes.
f_slash() { write_filter "$1/IntegrationTests.slnf" 'tests/Shared/Tests.Shared/Tests.Shared.csproj' 'tests/integration/A.Tests/A.Tests.csproj'
            write_filter "$1/IntegrationTests-One.slnf" 'tests/Shared/Tests.Shared/Tests.Shared.csproj' 'tests/integration/A.Tests/A.Tests.csproj'; }

# --- arms -----------------------------------------------------------------------------------------
expect "a correct partition is GREEN"                      0 "$(run_case f_clean)"
expect "a project in NO shard is RED (runs nowhere)"       1 "$(run_case f_gap)"
expect "a project in no parent is RED (runs unknown)"      1 "$(run_case f_extra)"
expect "a test assembly in TWO shards is RED (runs twice)" 1 "$(run_case f_dup)"
expect "a shard with no test assembly is RED (no-op)"      1 "$(run_case f_empty)"
expect "no shards at all REFUSES, not passes"              2 "$(run_case f_none)"
expect "separator style does not change the verdict"       0 "$(run_case f_slash)"

if [ "$FAILED" -eq 0 ]; then
    printf 'SELF-TEST: all arms passed -- the integration shard partition gate is non-vacuous.\n'
    exit 0
fi
printf 'SELF-TEST: at least one arm failed.\n' >&2
exit 1
