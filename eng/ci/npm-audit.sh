#!/usr/bin/env bash
# npm dependency audit across every lockfile in the repo.
#
# The npm half of the audit story. Its .NET counterpart is NuGetAudit in Directory.Build.props, and
# this deliberately matches its disposition: findings do NOT fail ordinary builds, because an advisory
# published overnight against an untouched package would break work that has nothing to do with it.
# That is NuGet's own documented caution and it applies identically here.
#
# So the layering is:
#   PR          Dependency Review blocks NEWLY INTRODUCED vulnerable deps (security.yml, high+)
#   nightly     this script fails on anything high+ already present
#   continuous  Dependabot opens update PRs (dependabot.yml, npm ecosystem, all three lockfiles)
#
# Exit codes follow the convention used by the sibling gates:
#   0  every directory audited, nothing at or above the threshold
#   1  the property is FALSE: at least one directory has a finding at or above the threshold
#   2  the property could not be EVALUATED: npm missing, or a directory could not be audited
set -uo pipefail

readonly EXIT_FOUND=1
readonly EXIT_ENV=2
AUDIT_LEVEL="${NPM_AUDIT_LEVEL:-high}"

# Every directory with a lockfile. Derived from the repo rather than hardcoded, so a fourth lockfile
# is covered the day it lands instead of the day someone remembers this file.
discover_dirs() {
    git ls-files '*package-lock.json' | grep -v node_modules | xargs -r -n1 dirname
}

# Reads an `npm audit --json` document and prints "crit high mod low" counts.
counts_from_json() {
    python3 -c '
import json,sys
try: m=json.load(sys.stdin).get("metadata",{}).get("vulnerabilities",{})
except Exception: sys.exit(9)
print(m.get("critical",0), m.get("high",0), m.get("moderate",0), m.get("low",0))
'
}

# The threshold decision, in ONE place. Both the real path and the self-test call THIS -- a self-test
# that re-implements the comparison proves only that two copies of the logic agree, and would pass
# while the shipped path was wrong.
# Returns EXIT_FOUND when the counts breach AUDIT_LEVEL, 0 otherwise.
exceeds_threshold() { # crit high mod low
    local crit="$1" high="$2" mod="$3" low="$4"
    case "$AUDIT_LEVEL" in
        critical) [ "$crit" -gt 0 ] ;;
        high)     [ $((crit + high)) -gt 0 ] ;;
        moderate) [ $((crit + high + mod)) -gt 0 ] ;;
        *)        [ $((crit + high + mod + low)) -gt 0 ] ;;
    esac && return "$EXIT_FOUND"
    return 0
}

audit_dir() {
    local dir="$1" json counts crit high mod low
    # npm audit exits non-zero WHEN IT FINDS THINGS, so a non-zero exit is not itself an error.
    # Only unparseable output means the audit could not be evaluated.
    json="$(cd "$dir" && npm audit --json 2>/dev/null)" || true
    if ! counts="$(printf '%s' "$json" | counts_from_json)"; then
        printf '::error::npm audit produced no parseable result for %s -- the audit did not run.\n' "$dir" >&2
        return "$EXIT_ENV"
    fi
    read -r crit high mod low <<<"$counts"
    printf '  %-56s critical=%s high=%s moderate=%s low=%s\n' "$dir" "$crit" "$high" "$mod" "$low"
    exceeds_threshold "$crit" "$high" "$mod" "$low"
}

main() {
    command -v npm >/dev/null 2>&1 || { printf '::error::npm not found; the audit could not run.\n' >&2; exit "$EXIT_ENV"; }

    local dirs found=0 unevaluated=0 n=0
    dirs="$(discover_dirs)"
    [ -n "$dirs" ] || { printf '::error::no package-lock.json found. An audit over zero lockfiles is not a clean audit.\n' >&2; exit "$EXIT_ENV"; }

    printf 'npm audit (threshold: %s)\n' "$AUDIT_LEVEL"
    # Every directory is audited before reporting. Stopping at the first finding would report one
    # problem when there are three, and the count is what tells you whether it is getting better.
    while IFS= read -r dir; do
        n=$((n + 1))
        audit_dir "$dir"
        case $? in
            "$EXIT_FOUND") found=$((found + 1)) ;;
            "$EXIT_ENV")   unevaluated=$((unevaluated + 1)) ;;
        esac
    done <<<"$dirs"

    printf '%s lockfile(s) audited.\n' "$n"
    [ "$unevaluated" -eq 0 ] || { printf '::error::%s director(ies) could not be audited. Not a clean result.\n' "$unevaluated" >&2; exit "$EXIT_ENV"; }
    [ "$found" -eq 0 ]       || { printf '::error::%s director(ies) have vulnerabilities at or above %s. Run `npm audit fix`, or `npm audit` in the directory to see them.\n' "$found" "$AUDIT_LEVEL" >&2; exit "$EXIT_FOUND"; }
    printf 'npm audit: clean at %s and above across %s lockfile(s).\n' "$AUDIT_LEVEL" "$n"
}

# --self-test drives the decision logic with canned audit documents. It needs no network and no
# install, and it proves the gate can FAIL -- a gate only ever observed passing is not evidence.
self_test() {
    local fails=0
    check() { # name expected-exit json -- drives counts_from_json AND exceeds_threshold, the shipped ones
        local got rc=0
        got="$(printf '%s' "$3" | counts_from_json)" || { printf 'SELF-TEST: FAIL -- %s: unparseable\n' "$1" >&2; fails=$((fails+1)); return; }
        read -r c h m l <<<"$got"
        exceeds_threshold "$c" "$h" "$m" "$l" || rc=$?
        if [ "$rc" = "$2" ]; then printf 'SELF-TEST: PASS -- %s\n' "$1"
        else printf 'SELF-TEST: FAIL -- %s (expected %s, got %s)\n' "$1" "$2" "$rc" >&2; fails=$((fails+1)); fi
    }

    AUDIT_LEVEL=high
    check "clean tree is GREEN"                      0 '{"metadata":{"vulnerabilities":{"critical":0,"high":0,"moderate":0,"low":0}}}'
    check "a CRITICAL is RED"                        1 '{"metadata":{"vulnerabilities":{"critical":1,"high":0,"moderate":0,"low":0}}}'
    check "a HIGH is RED"                            1 '{"metadata":{"vulnerabilities":{"critical":0,"high":1,"moderate":0,"low":0}}}'
    check "a MODERATE alone is GREEN at high"        0 '{"metadata":{"vulnerabilities":{"critical":0,"high":0,"moderate":9,"low":0}}}'

    # Unparseable output must be distinguishable from a clean audit: "npm printed nothing" and
    # "npm found nothing" are the same string to a naive reader, and only one of them is a pass.
    if printf 'not json' | counts_from_json >/dev/null 2>&1
    then printf 'SELF-TEST: FAIL -- garbage parsed as a result\n' >&2; fails=$((fails+1))
    else printf 'SELF-TEST: PASS -- unparseable output is REFUSED, not read as clean\n'; fi

    # The discovery half: the gate must actually find the repo's lockfiles.
    local n; n="$(discover_dirs | grep -c .)"
    if [ "$n" -ge 1 ]; then printf 'SELF-TEST: PASS -- discovery finds %s lockfile(s)\n' "$n"
    else printf 'SELF-TEST: FAIL -- discovery found no lockfiles; the gate would audit nothing\n' >&2; fails=$((fails+1)); fi

    [ "$fails" -eq 0 ] || { printf 'SELF-TEST: %s arm(s) failed\n' "$fails" >&2; exit 1; }
    printf 'SELF-TEST: all arms passed\n'
}

case "${1:-}" in
    --self-test) self_test ;;
    *)           main ;;
esac
