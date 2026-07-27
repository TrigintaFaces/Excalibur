#!/usr/bin/env bash
# decomposition-coverage-gate.test.sh — non-vacuous self-test, as the bead requires:
# "a planted unmapped id makes it RED, and a fully mapped pair makes it GREEN."
#
# The comma-range arms are the ones that matter most. A naive extractor loses the tail of `AC-16,17`
# on BOTH sides, so the two sides agree and the gate reports full coverage while an id is invisible
# to it — a false GREEN produced by the very tokenisation bug this gate was written to avoid.
#
# exit 0 all arms pass · 1 an arm failed · 2 cannot evaluate

set -u
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GATE="$HERE/decomposition-coverage-gate.sh"
[ -f "$GATE" ] || { echo "REFUSE: gate not found" >&2; exit 2; }
command -v python3 >/dev/null 2>&1 || { echo "REFUSE: python3 required" >&2; exit 2; }

pass=0; fail=0
_ok()  { printf '  ok    %s\n' "$1"; pass=$((pass+1)); }
_bad() { printf '  FAIL  %s\n' "$1"; fail=$((fail+1)); }

T="$(mktemp -d)" || exit 2
trap 'rm -rf "$T"' EXIT

run() { bash "$GATE" "$1" "$2" >/dev/null 2>&1; echo $?; }

printf '\nLIVENESS — a fully mapped pair must be GREEN\n'

printf '# spec\n- FR-1 thing\n- AC-2 other\n' > "$T/s1.md"
printf '# decomp\n| FR-1 | MS-A |\n| AC-2 | MS-B |\n' > "$T/d1.md"
[ "$(run "$T/s1.md" "$T/d1.md")" = "0" ] && _ok "L1 fully mapped -> exit 0" || _bad "L1 fully mapped -> exit 0"

printf '# spec\n- AC-11a and AC-11b\n' > "$T/s2.md"
printf '# decomp\n| AC-11a | MS-A |\n| AC-11b | MS-B |\n' > "$T/d2.md"
[ "$(run "$T/s2.md" "$T/d2.md")" = "0" ] \
    && _ok "L2 suffixed ids (AC-11a/AC-11b) treated as distinct and mapped -> exit 0" \
    || _bad "L2 suffixed ids treated as distinct and mapped -> exit 0"

# An id only in the decomposition is reported, not failed — a mini-spec may add a sub-id.
printf '# spec\n- FR-1\n' > "$T/s3.md"
printf '# decomp\n| FR-1 | MS-A |\n| FR-9 | MS-B |\n' > "$T/d3.md"
[ "$(run "$T/s3.md" "$T/d3.md")" = "0" ] \
    && _ok "L3 extra id in decomposition only -> reported, still exit 0" \
    || _bad "L3 extra id in decomposition only -> reported, still exit 0"

printf '\nSAFETY — a planted unmapped id must be RED\n'

printf '# spec\n- FR-1\n- FR-2\n- AC-3\n' > "$T/s4.md"
printf '# decomp\n| FR-1 | MS-A |\n' > "$T/d4.md"
[ "$(run "$T/s4.md" "$T/d4.md")" = "1" ] && _ok "S1 planted unmapped ids -> exit 1" \
                                         || _bad "S1 planted unmapped ids -> exit 1"

# The failure must NAME the id. "Coverage incomplete" with no id leaves nobody able to act.
if bash "$GATE" "$T/s4.md" "$T/d4.md" 2>&1 | grep -q 'FR-2'; then
    _ok "S2 failure names each unmapped id"
else
    _bad "S2 failure names each unmapped id"
fi

printf '\nTOKENISATION — the comma-range trap, in both directions\n'

# The tail of a range in the SPEC must be seen. A naive extractor drops AC-17 and calls this GREEN.
printf '# spec\n- AC-16,17 both required\n' > "$T/s5.md"
printf '# decomp\n| AC-16 | MS-A |\n' > "$T/d5.md"
[ "$(run "$T/s5.md" "$T/d5.md")" = "1" ] \
    && _ok "T1 spec 'AC-16,17' vs decomp 'AC-16' -> RED (tail not lost)" \
    || _bad "T1 spec 'AC-16,17' vs decomp 'AC-16' -> RED (tail not lost)"

# ...and the tail of a range in the DECOMPOSITION must count as coverage, or the gate cries wolf.
printf '# spec\n- AC-16 and AC-17\n' > "$T/s6.md"
printf '# decomp\n| AC-16,17 | MS-A |\n' > "$T/d6.md"
[ "$(run "$T/s6.md" "$T/d6.md")" = "0" ] \
    && _ok "T2 decomp 'AC-16,17' covers spec AC-16 + AC-17 -> GREEN" \
    || _bad "T2 decomp 'AC-16,17' covers spec AC-16 + AC-17 -> GREEN"

printf '# spec\n- FR-13,14,15 all apply\n' > "$T/s7.md"
printf '# decomp\n| FR-13 | MS-A |\n| FR-14 | MS-B |\n' > "$T/d7.md"
if bash "$GATE" "$T/s7.md" "$T/d7.md" 2>&1 | grep -q 'FR-15'; then
    _ok "T3 three-element range expands; the missing tail element is named"
else
    _bad "T3 three-element range expands; the missing tail element is named"
fi

printf '\nREFUSE — cannot-evaluate must be 2, never 0\n'

printf '# nothing here\n' > "$T/s8.md"
[ "$(run "$T/s8.md" "$T/d1.md")" = "2" ] \
    && _ok "R1 spec parses to ZERO ids -> exit 2 (not 'perfect coverage')" \
    || _bad "R1 spec parses to ZERO ids -> exit 2 (not 'perfect coverage')"

printf '# nothing here\n' > "$T/d8.md"
[ "$(run "$T/s1.md" "$T/d8.md")" = "2" ] \
    && _ok "R2 decomposition parses to ZERO ids -> exit 2" || _bad "R2 decomposition parses to ZERO ids -> exit 2"

[ "$(run "$T/missing.md" "$T/d1.md")" = "2" ] && _ok "R3 missing spec file -> exit 2" \
                                              || _bad "R3 missing spec file -> exit 2"

printf '\nEND-TO-END OVERRIDES — so the HOOK rejection branch can be driven, not just the gate\n'

# Why these exist: pre-commit calls the gate with NO arguments, so it always resolves the real
# sprint spec — which is currently fully mapped. Without an override the end-to-end path could only
# ever be exercised GREEN, and a rejection branch that has never rejected anything is an untested
# branch wearing a passing test. With them, the real hook was driven to exit 1 on planted ids.
[ "$(DECOMP_SPEC_OVERRIDE="$T/s4.md" DECOMP_DECOMP_OVERRIDE="$T/d4.md" bash "$GATE" >/dev/null 2>&1; echo $?)" = "1" ] \
    && _ok "E1 overrides drive the no-arg path to RED on planted unmapped ids" \
    || _bad "E1 overrides drive the no-arg path to RED on planted unmapped ids"

[ "$(DECOMP_SPEC_OVERRIDE="$T/s1.md" DECOMP_DECOMP_OVERRIDE="$T/d1.md" bash "$GATE" >/dev/null 2>&1; echo $?)" = "0" ] \
    && _ok "E2 overrides drive the no-arg path to GREEN on a mapped pair" \
    || _bad "E2 overrides drive the no-arg path to GREEN on a mapped pair"

# A half-set override must REFUSE rather than silently fall back to the real spec — a fallback would
# make a fixture run silently measure the wrong subject, which is tonight's dominant error class.
[ "$(DECOMP_SPEC_OVERRIDE="$T/s1.md" bash "$GATE" >/dev/null 2>&1; echo $?)" = "2" ] \
    && _ok "E3 spec override without decomposition override -> exit 2, no silent fallback" \
    || _bad "E3 spec override without decomposition override -> exit 2, no silent fallback"

# Explicit args must still win over the environment, or a stray exported var would redirect a
# deliberate invocation at a different subject.
[ "$(DECOMP_SPEC_OVERRIDE="$T/s4.md" DECOMP_DECOMP_OVERRIDE="$T/d4.md" bash "$GATE" "$T/s1.md" "$T/d1.md" >/dev/null 2>&1; echo $?)" = "0" ] \
    && _ok "E4 explicit arguments take precedence over the overrides" \
    || _bad "E4 explicit arguments take precedence over the overrides"

# ARM-COUNT ASSERTION - a suite that LOSES arms reports the same green as a complete one.
# Measured: a stray apostrophe inside a printf terminated the string, absorbed the two following
# lines as literal text, and two arms never executed as commands. The suite printed "13 passed,
# 0 failed" and exited 0 - and the arms it ate were the two that mattered. Nothing in a pass/fail
# count can reveal a test that never ran, so the expected total is pinned here and checked.
# If you add or remove an arm deliberately, update this number in the same commit.
EXPECTED_ARMS=15
_total=$((pass + fail))
if [ "$_total" -ne "$EXPECTED_ARMS" ]; then
    printf '
  ARM COUNT MISMATCH: %d arms ran, %d expected.
' "$_total" "$EXPECTED_ARMS"
    printf '  An arm was lost (quoting error, early return) or added without updating the count.
'
    printf '  A suite missing arms reports the same GREEN as a complete one - refusing.

'
    exit 1
fi

printf '\n  %d passed, %d failed\n\n' "$pass" "$fail"
[ "$fail" -eq 0 ] || exit 1
exit 0
