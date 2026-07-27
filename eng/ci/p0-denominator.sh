#!/usr/bin/env bash
# p0-denominator.sh -- machine-computable P0 accounting denominator (qc7mhv). NO hand-classification.
#
# A "src P0" count was a hand-classification: 38 of 40 open P0s carried no labels, so every count
# was a human eyeballing the list. That number is neither reproducible nor auditable. This runs the
# fixed, documented predicate in p0-classify.py over the current open-P0 set and prints the counts,
# so the denominator is a function of the tracker state alone -- reproducible, no human in the loop.
#
# Usage:
#   p0-denominator.sh                 # open P0s (default)
#   BD_P0_STATUS=in_progress p0-denominator.sh
#   BD_P0_JSON=<file> p0-denominator.sh   # classify a saved `bd list -p 0 --json` (used by the test)
#
# Exit: 0 classified | 3 tracker unreadable (never treated as an empty tracker) | 64 usage/env error.
#
# gate-wiring: not-a-ci-gate (reporting tool)
#   This REPORTS a denominator; it is not a pass/fail repo-state gate (exit 0 is "classified", not
#   "clean"). It is invoked by a human/accounting step, not wired as a `run:` gate, so it is exempt
#   from gate-wiring.sh ARM2. Its lock p0-denominator.test.sh is STILL required to have a caller
#   (ARM1) -- the classifier's correctness stays regression-locked.

set -uo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLASSIFY="$HERE/p0-classify.py"
[ -f "$CLASSIFY" ] || { echo "p0-denominator: missing $CLASSIFY" >&2; exit 64; }

STATUS="${BD_P0_STATUS:-open}"

# BD_P0_JSON lets the test feed a fixture without a live tracker; otherwise read bd directly.
# avoids the daemon EOF/flap class (1rknoh) for a read-only accounting query.
if [ -n "${BD_P0_JSON:-}" ]; then
    [ -f "$BD_P0_JSON" ] || { echo "p0-denominator: no such fixture: $BD_P0_JSON" >&2; exit 64; }
    RAW="$(cat "$BD_P0_JSON")"
else
    # `2>/dev/null || true` used to sit here: it discarded bd's diagnosis AND its exit status, so a
    # failed read arrived at the classifier as an empty string. The classifier's refusal to report a
    # denominator of 0 on unreadable input is what saved this — but it was compensating for a fact
    # the caller had already destroyed. Keep the exit; keep the reason (svacnv AC3).
    set +e
    RAW="$(bd list --status "$STATUS" -p 0 --json 2>&1)"
    _BD_RC=$?
    set -e
    if [ "$_BD_RC" -ne 0 ]; then
        echo "p0-denominator: FATAL -- \`bd list -p 0 --json\` exited $_BD_RC." >&2
        echo "                An unreadable tracker is not an empty one; refusing to report a denominator." >&2
        echo "                bd said: $RAW" >&2
        exit 3
    fi
fi

printf '%s' "$RAW" | python3 "$CLASSIFY"
rc=$?
if [ "$rc" -eq 3 ]; then
    echo "p0-denominator: FATAL -- \`bd list -p 0 --json\` was unreadable. An unreadable tracker is not" >&2
    echo "                an empty one; refusing to report a denominator of 0." >&2
fi
exit "$rc"
