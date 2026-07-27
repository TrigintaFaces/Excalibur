#!/usr/bin/env bash
# fabricated-utc-gate.test.sh — non-vacuous self-test for fabricated-utc-gate.sh (48ay30).
#
#   SAFETY   a git `--date=format:'…Z'` (local tz + literal Z)      -> exit 1 (fabrication caught)
#   SAFETY   a `date +'…Z'` without -u (local wall-clock + Z)       -> exit 1 (fabrication caught)
#   LIVENESS a git `--date=format:'…%z'` (the REAL offset)          -> exit 0 (truthful, accepted)
#   LIVENESS a `date -u +'…Z'` (-u forces UTC → the Z is TRUE)      -> exit 0 (truthful, accepted)
#   LIVENESS a `--format=%ct` epoch (no tz to fabricate)            -> exit 0 (accepted)
#
# Each arm drives the REAL gate over an ISOLATED fixture (SCAN_ROOT), asserting an EXACT exit code, so a
# gate that rejected everything (or accepted everything) is caught, and a truthful UTC stamp is not a
# false-positive.
#
# Usage: bash eng/ci/fabricated-utc-gate.test.sh   (exit 0 = all green)

set -uo pipefail

GATE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/fabricated-utc-gate.sh"
PASS=0; FAIL=0
ok()  { PASS=$((PASS + 1)); printf '  PASS  %s\n' "$1"; }
bad() { FAIL=$((FAIL + 1)); printf '  FAIL  %s\n     -> %s\n' "$1" "$2" >&2; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK" 2>/dev/null || true' EXIT

# $1 = arm name, $2 = the line of script to plant in a fixture .sh
plant() {
    local name="$1" line="$2"
    local dir="$WORK/$name"
    mkdir -p "$dir"
    { printf '#!/usr/bin/env bash\n'; printf '%s\n' "$line"; } > "$dir/stamp.sh"
    printf '%s' "$dir"
}
run() { SCAN_ROOT="$1" bash "$GATE" >/dev/null 2>&1; RC=$?; }

echo "fabricated-utc-gate.sh — self-test (48ay30)"
echo

# SAFETY: git --date=format:'…Z'
run "$(plant s1 "git log -1 --date=format:'%Y-%m-%dT%H:%M:%SZ' --format=%cd")"
[ "$RC" -eq 1 ] && ok "safety: git --date=format:'…Z' (local tz + literal Z) is REJECTED (exit 1)" \
                || bad "safety: --date=format:'…Z' must be rejected" "got exit $RC"

# SAFETY: date +'…Z' without -u
run "$(plant s2 "ts=\$(date +'%Y-%m-%dT%H:%M:%SZ')")"
[ "$RC" -eq 1 ] && ok "safety: date +'…Z' without -u (local wall-clock) is REJECTED (exit 1)" \
                || bad "safety: date +'…Z' without -u must be rejected" "got exit $RC"

# LIVENESS: git --date=format:'…%z' (real offset)
run "$(plant l1 "git log -1 --date=format:'%Y-%m-%dT%H:%M:%S%z' --format=%cd")"
[ "$RC" -eq 0 ] && ok "liveness: --date=format:'…%z' (REAL offset) is accepted (exit 0)" \
                || bad "liveness: real-offset form must pass" "got exit $RC"

# LIVENESS: date -u +'…Z' (truthful UTC)
run "$(plant l2 "ts=\$(date -u +'%Y-%m-%dT%H:%M:%SZ')")"
[ "$RC" -eq 0 ] && ok "liveness: date -u +'…Z' (-u forces UTC, Z is TRUE) is accepted (exit 0)" \
                || bad "liveness: truthful date -u …Z must pass (no false-positive)" "got exit $RC"

# LIVENESS: epoch %ct
run "$(plant l3 "git log -1 --format=%ct")"
[ "$RC" -eq 0 ] && ok "liveness: epoch --format=%ct (no tz to fabricate) is accepted (exit 0)" \
                || bad "liveness: epoch form must pass" "got exit $RC"

echo
printf 'passed %d · failed %d\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ] || exit 1
