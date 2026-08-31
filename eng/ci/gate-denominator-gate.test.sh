#!/usr/bin/env bash
# Harness test for the denominator contract: the helper, the meta-gate, and the live tree.
#
# The meta-gate's own --self-test proves it discriminates on synthetic trees. This file adds the
# two arms a self-test structurally cannot supply: that the helper behaves the same when SOURCED
# by a caller as when run standalone, and that the contract holds against the REAL eng/ci tree
# rather than a fixture the author built to match.
set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
pass=1

arm() { # arm <name> <expected-rc> <cmd...>
    local name="$1" want="$2"; shift 2
    "$@" >/dev/null 2>&1; local rc=$?
    if [ "$rc" -eq "$want" ]; then printf '  PASS  %s\n' "$name"
    else printf '  FAIL  %s (expected rc=%s, got %s)\n' "$name" "$want" "$rc" >&2; pass=0; fi
}

printf '\ngate-denominator contract — harness test\n\n'

arm "helper self-test"                     0 bash "$HERE/gate-denominator.sh" --self-test
arm "meta-gate self-test"                  0 bash "$HERE/gate-denominator-gate.sh" --self-test
arm "meta-gate on the REAL eng/ci tree"    0 bash "$HERE/gate-denominator-gate.sh"

# SOURCED behaviour. The helper reads $1 to find its own --self-test flag; when sourced, $1 belongs
# to the CALLING gate. An unguarded read made the helper run its own self-test and exit 0 out of a
# gate invoked with --self-test — a library answering a verdict on its caller's behalf, which is the
# false-green class this whole mechanism exists to remove. This arm holds that shut.
sourced_probe="$(mktemp)"
cat > "$sourced_probe" <<'PROBE'
. "$1/gate-denominator.sh"
shift
gate_denominator 3 "thing(s)" || exit 2
echo "caller-verdict-reached"
PROBE
out="$(bash "$sourced_probe" "$HERE" --self-test 2>&1)"; rc=$?
if [ "$rc" -eq 0 ] && printf '%s' "$out" | grep -q 'caller-verdict-reached'; then
    printf '  PASS  sourcing the helper does not hijack the caller --self-test\n'
else
    printf '  FAIL  a sourced helper consumed the caller args (rc=%s): %s\n' "$rc" "$out" >&2; pass=0
fi

# The zero arm, through a real sourcing caller: REFUSE(2), never PASS(0).
cat > "$sourced_probe" <<'PROBE'
. "$1/gate-denominator.sh"
gate_denominator 0 "thing(s)" || exit 2
echo "caller-verdict-reached"
PROBE
out="$(bash "$sourced_probe" "$HERE" 2>&1)"; rc=$?
if [ "$rc" -eq 2 ] && ! printf '%s' "$out" | grep -q 'caller-verdict-reached'; then
    printf '  PASS  an empty population stops the caller before its verdict (rc=2)\n'
else
    printf '  FAIL  an empty population let the caller render a verdict (rc=%s)\n' "$rc" >&2; pass=0
fi
rm -f "$sourced_probe"

# LIVENESS on the real tree: the migrated gates must actually be emitting the marker, not merely
# be absent from the baseline. Without this arm, deleting every gate would also produce a green.
emitters="$(grep -lE 'gate_denominator(_may_be_empty)?[[:space:]]' "$HERE"/*.sh 2>/dev/null | grep -vcE '(gate-denominator\.sh|\.test\.sh)$')"
if [ "${emitters:-0}" -ge 8 ]; then
    printf '  PASS  %s gate script(s) in the real tree call the helper\n' "$emitters"
else
    printf '  FAIL  only %s gate script(s) call the helper — the migration regressed\n' "${emitters:-0}" >&2; pass=0
fi

printf '\n'
[ "$pass" -eq 1 ] && { echo "gate-denominator contract: ALL ARMS PASS"; exit 0; }
echo "gate-denominator contract: FAILED" >&2; exit 1
