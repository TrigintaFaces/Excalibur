#!/usr/bin/env bash
# bd-status-tokens.test.sh -- proof that the gate can fail, and that it can pass.
#
# A gate asserted only on its safety half is satisfied by one that rejects everything;
# only on its liveness half, by one that accepts everything. Both arms.
#
# The liveness arms are the ones that matter here. This gate's whole subject is a tool
# that answers "zero" to every question it doesn't understand. A gate that inherits that
# behaviour -- flagging every token, or flagging none -- would be the same bug wearing a
# different hat, and every safety arm above would still pass.
#
# Each fixture line below contains a deliberately-bad token, so each carries the explicit
# per-line opt-out. The fix for a self-referential scanner hit belongs at the AUTHORING
# SITE, never in a weakened scanner.
#
# Fixtures live in temp trees. Nothing mutates the working copy: a test that mutates a
# shared file and git-checkout-restores it destroys another agent's uncommitted work.

set -uo pipefail

GATE="$(cd "$(dirname "$0")" && pwd)/bd-status-tokens.sh"
passed=0; failed=0
ok()  { printf '  ok  : %s\n' "$1"; passed=$((passed + 1)); }
bad() { printf '  FAIL: %s\n' "$1" >&2; failed=$((failed + 1)); }

# $1 = file content -> echoes the gate's exit code
run_on() {
    t=$(mktemp -d); mkdir -p "$t/eng"
    printf '%s\n' "$1" > "$t/eng/probe.sh"
    ( BD_STATUS_ROOT="$t" BD_STATUS_SCAN="eng" bash "$GATE" >/dev/null 2>&1 )
    rc=$?
    rm -rf "$t"
    echo "$rc"
}

expect() {  # $1 = expected rc, $2 = actual rc, $3 = description
    if [ "$2" -eq "$1" ]; then ok "$3"; else bad "$3 -- expected exit $1, got $2"; fi
}

echo "SAFETY -- every silently-empty token must be REJECTED"

rc=$(run_on 'bd list --status banana')  # bd-status-ok
expect 1 "$rc" "nonsense token 'banana' -> RED"

rc=$(run_on 'bd list --status in-progress --json')  # bd-status-ok
expect 1 "$rc" "hyphen 'in-progress' -> RED (the lethal, obvious misspelling)"

rc=$(run_on 'bd list --status OPEN')  # bd-status-ok
expect 1 "$rc" "wrong case 'OPEN' -> RED"

rc=$(run_on 'bd list --status open,in_progress')  # bd-status-ok
expect 1 "$rc" "comma list -> RED (bd matches nothing and exits 0)"

rc=$(run_on 'bd list --status in_progress/ready_for_tests --json')  # bd-status-ok
expect 1 "$rc" "slash list -> RED"

rc=$(run_on 'bd update x --status done')  # bd-status-ok
expect 1 "$rc" "'done' -> RED (bd has 'closed'; 'done' matches nothing)"

rc=$(run_on 'bd list --status ready_for_integration')  # bd-status-ok
expect 1 "$rc" "'ready_for_integration' -> RED (not a bd status)"

echo "LIVENESS -- every REAL token must be ACCEPTED, or the gate is a wall"

for good in open in_progress blocked closed; do
    rc=$(run_on "bd list --status $good --json")
    expect 0 "$rc" "'$good' -> GREEN"
done

rc=$(run_on 'bd list --status "$STATUS" --json')  # bd-status-ok
expect 0 "$rc" 'runtime variable $STATUS -> GREEN (cannot be judged statically)'

rc=$(run_on 'documents bd list --status banana as a trap  # bd-status-ok')  # bd-status-ok
expect 0 "$rc" "explicit per-line opt-out lets us WRITE DOWN the lesson"

rc=$(run_on 'bd list --status open'$'\r')
expect 0 "$rc" "CRLF: 'open\\r' is not a false violation (tpu8m2)"

# The prose line must sit ALONGSIDE a real command. A fixture of nothing but prose has zero
# `--status` command occurrences, and the gate rightly hard-errors (64) rather than call an
# empty enumeration clean. Asserting 0 there would have been asserting the f5-sweep bug.
rc=$(run_on 'bd list --status open --json
an unrecognized --status token is a silent lie')
expect 0 "$rc" "PROSE about --status alongside a valid command -> GREEN (no false 'token' violation)"

echo "HARD ERRORS -- a broken scan is never a clean verdict"

t=$(mktemp -d); mkdir -p "$t/eng"; printf 'echo nothing here\n' > "$t/eng/probe.sh"
( BD_STATUS_ROOT="$t" BD_STATUS_SCAN="eng" bash "$GATE" >/dev/null 2>&1 ); rc=$?
rm -rf "$t"
expect 64 "$rc" "zero '--status' occurrences -> hard error 64, never 'clean' (the f5-sweep bug)"

rc=0; BD_STATUS_ROOT="/nonexistent/$$" bash "$GATE" >/dev/null 2>&1 || rc=$?
expect 64 "$rc" "missing root -> usage error 64"

echo "INTEGRATION -- this repo"
rc=0; bash "$GATE" >/dev/null 2>&1 || rc=$?
expect 0 "$rc" "repo has no unrecognized --status token"

echo
echo "=== $passed passed, $failed failed ==="
[ "$failed" -eq 0 ] || exit 1
echo "bd-status-tokens lock is GREEN"
