#!/usr/bin/env bash
# Message identity has ONE source: the name a type declares, read through MessageNameHelper.
#
# The failure this exists to stop is not a bug in any one file -- it is that naming a message is a
# decision each writer makes for itself. Four derivations were found by inventory and unified; a
# fifth was found afterwards by a reviewer, and a sixth, seventh and eighth by this gate. An
# inventory measures the past. This measures every commit.
#
# A "violation" is an assignment of a message-identity field from a CLR type member. Naming a type
# in an error message or a metrics label is NOT identity and is not matched.
#
# PASS(0) the population did not grow · FAIL(1) a new site appeared · REFUSE(2) could not evaluate.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
BASELINE="${BASELINE_OVERRIDE:-$ROOT/eng/ci/message-identity-single-source.baseline.txt}"
SCAN_ROOT="${SCAN_ROOT_OVERRIDE:-$ROOT/src}"

# An identity field taking its value from the CLR type rather than the declared name.
PATTERN='(MessageType|EventType)[[:space:]]*=[[:space:]]*[^;]*\.GetType\(\)\.(FullName|Name|AssemblyQualifiedName)'

[ -d "$SCAN_ROOT" ] || { echo "::error::[message-identity] CANNOT EVALUATE -- scan root missing: $SCAN_ROOT"; exit 2; }
[ -f "$BASELINE" ] || { echo "::error::[message-identity] CANNOT EVALUATE -- baseline missing: $BASELINE"; exit 2; }

hits="$(grep -rEn --include='*.cs' "$PATTERN" "$SCAN_ROOT" 2>/dev/null \
        | grep -v '/obj/' | grep -v '/bin/' \
        | sed "s|^$ROOT/||" \
        | sed -E 's/:[0-9]+:.*//' \
        | sort -u)"

# The assignment form above is not the only way to name a message from its CLR type. An
# implementation of the naming contract can simply RETURN one, which no assignment pattern can
# see: one serializer returned an assembly-qualified name from GetTypeName while the two other
# implementations of the same interface returned the declared name, and this gate passed.
contract_hits="$(grep -rEn --include='*.cs' -A 6 'string GetTypeName' "$SCAN_ROOT" 2>/dev/null \
        | grep -E '[.](FullName|AssemblyQualifiedName)' \
        | grep -v '/obj/' | grep -v '/bin/' \
        | sed "s|^$ROOT/||" \
        | sed -E 's/[-:][0-9]+[-:].*//' \
        | sort -u)"

hits="$(printf '%s\n%s\n' "$hits" "$contract_hits" | grep -v '^$' | sort -u)"

# A store must persist the name it is handed, not work one out. Ten stores each deriving their own
# agreed by convention rather than by construction, and an inventory of the places that name a
# message has already missed several. Naming now happens once, in AsNamedEvents, before any event in
# a batch is written; a store that reaches for the helper again is this defect returning.
store_hits="$(grep -rEn --include='*.cs' 'MessageNameHelper[.]GetName' "$SCAN_ROOT" 2>/dev/null \
        | grep -E 'EventSourcing[.][A-Za-z]+/' \
        | grep -v '/obj/' | grep -v '/bin/' \
        | sed "s|^$ROOT/||" \
        | sed -E 's/:[0-9]+:.*//' \
        | sort -u)"

hits="$(printf '%s\n%s\n' "$hits" "$store_hits" | grep -v '^$' | sort -u)"

# A scan that finds nothing at all is far more likely to be a broken query than a clean tree.
control="$(grep -rEn --include='*.cs' 'GetType\(\)\.' "$SCAN_ROOT" 2>/dev/null | grep -vc '/obj/')"
if [ "${control:-0}" -lt 50 ]; then
  echo "::error::[message-identity] CANNOT EVALUATE -- positive control found only ${control:-0} GetType() uses under $SCAN_ROOT."
  echo "  The scan is not reading the tree it thinks it is. NOTHING was checked; that is not a clean bill of health."
  exit 2
fi

baseline="$(grep -vE '^\s*#|^\s*$' "$BASELINE" | sort -u)"
new="$(comm -23 <(printf '%s\n' "$hits") <(printf '%s\n' "$baseline"))"
gone="$(comm -13 <(printf '%s\n' "$hits") <(printf '%s\n' "$baseline"))"

echo "EXAMINED: $(printf '%s\n' "$hits" | grep -c . ) file(s) deriving a message identity from a CLR type (control: $control GetType() uses)"

if [ -n "$new" ]; then
  echo "::error::[message-identity] a NEW site derives message identity from the CLR type:"
  printf '  %s\n' $new
  echo
  echo "  A message's identity is the name it declares. A name taken from the type changes when the"
  echo "  namespace, assembly or assembly version changes, and everything already written under the old"
  echo "  one becomes unreadable -- silently, because the write still succeeds."
  echo "  Use MessageNameHelper.GetName(type). If this genuinely is not an identity (an error message,"
  echo "  a metrics label), rename the field or add the file to:"
  echo "    $BASELINE"
  exit 1
fi

if [ -n "$gone" ]; then
  echo "[message-identity] baseline shrank -- these no longer derive identity from the CLR type:"
  printf '  %s\n' $gone
  echo "  Remove them from the baseline to lock the improvement in."
fi

echo "✅ PASS -- no new site derives message identity from a CLR type."
exit 0
