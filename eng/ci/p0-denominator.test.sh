#!/usr/bin/env bash
# p0-denominator.test.sh -- non-vacuous lock for the machine-computable P0 denominator (qc7mhv).
#
# The whole point of qc7mhv is that the count is HUMAN-FREE and REPRODUCIBLE. So the lock proves:
#   DETERMINISM  the same input yields the same counts (no hand-classification -> stable output).
#   CORRECTNESS  a known src bead buckets src; a known tooling bead buckets tooling; a label wins
#                over the heuristic; an unscorable bead is reported UNCLASSIFIED, not guessed.
#   SAFETY       an unreadable tracker exits 3 (never a silent denominator of 0).
# Fixtures are inline JSON; no live tracker, no working-copy mutation.

set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DENOM="$HERE/p0-denominator.sh"
passed=0; failed=0
ok()  { printf '  ok  : %s\n' "$1"; passed=$((passed + 1)); }
bad() { printf '  FAIL: %s\n' "$1" >&2; failed=$((failed + 1)); }

run() {  # $1 = json content; echoes the denominator stdout, sets RC
    local f; f="$(mktemp)"; printf '%s' "$1" > "$f"
    OUT="$(BD_P0_JSON="$f" bash "$DENOM" 2>/dev/null)"; RC=$?
    rm -f "$f"
}

FIX='[
  {"id":"AAA","title":"IEventStore AppendAsync drops the aggregate version on reload","description":"src/ event store bug","labels":[]},
  {"id":"BBB","title":"gate-wiring.sh false PROOF-ONLY on a premise-triage hook","description":"eng/ci ci.yml workflow","labels":[]},
  {"id":"CCC","title":"labelled as source explicitly","description":"no keywords either way","labels":["area:src"]},
  {"id":"DDD","title":"labelled tooling explicitly","description":"IEventStore aggregate serializer","labels":["area:tooling"]},
  {"id":"EEE","title":"zzzz qqqq wwww","description":"nothing recognisable here","labels":[]}
]'

echo "CORRECTNESS -- the fixed rule buckets each bead deterministically"
run "$FIX"
[ "$RC" -eq 0 ] && printf '%s\n' "$OUT" | grep -q '^total=5 src=2 tooling=2 unclassified=1$' \
    && ok "counts: total=5 src=2 tooling=2 unclassified=1 (AAA=src BBB=tool CCC=src[label] DDD=tool[label] EEE=unclassified)" \
    || bad "unexpected counts (rc=$RC): $(printf '%s' "$OUT" | head -1)"

# A label must OVERRIDE the heuristic: DDD's text screams src (IEventStore/aggregate/serializer)
# but area:tooling wins. Prove it landed in tooling, not src.
printf '%s\n' "$OUT" | grep -qE '^  tooling: .*DDD' \
    && ok "explicit area:tooling label overrides a src-shaped body (DDD -> tooling)" \
    || bad "label did not override the heuristic (DDD not in tooling)"

printf '%s\n' "$OUT" | grep -qE '^  unclassified: .*EEE' \
    && ok "an unscorable bead is reported UNCLASSIFIED, not folded into a bucket (EEE)" \
    || bad "EEE was silently bucketed instead of reported unclassified"

echo "DETERMINISM -- same input, same output (the property that makes it human-free)"
run "$FIX"; A="$OUT"
run "$FIX"; B="$OUT"
[ "$A" = "$B" ] && ok "two runs over the same set produce byte-identical output" \
                || bad "output is not deterministic across runs"

echo "SAFETY -- an unreadable tracker is not an empty one"
run 'this is not json and has no bracket'
[ "$RC" -eq 3 ] && ok "unreadable tracker -> exit 3 (never a silent denominator of 0)" \
               || bad "unreadable tracker did not exit 3 (rc=$RC)"

run '[]'
[ "$RC" -eq 0 ] && printf '%s\n' "$OUT" | grep -q '^total=0 ' \
    && ok "a genuinely empty set -> total=0 (distinct from the unreadable case)" \
    || bad "empty set not reported as total=0 (rc=$RC)"

echo
echo "=== $passed passed, $failed failed ==="
[ "$failed" -eq 0 ] || exit 1
echo "p0-denominator lock is GREEN"
