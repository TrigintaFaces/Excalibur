#!/usr/bin/env bash
# duplicate-xml-doc-tags.test.sh — non-vacuous self-test for duplicate-xml-doc-tags.sh
#
# HOME: .claude/harness/ (S883). Bead: qcizyz.
#
# Every arm runs the REAL script as a subprocess against throwaway .cs fixtures. Each SAFETY arm
# (a duplicate tag is REFUSED) is paired with a LIVENESS arm (a clean block, and two DISTINCT
# single-tag blocks, are ACCEPTED) — a gate that fails on every file, or that merges adjacent
# members into one block, would pass the safety arms alone; only the liveness/precision arms expose it.
#
# Usage:  bash eng/ci/duplicate-xml-doc-tags.test.sh
# Exit:   0 all arms pass · 1 an arm failed
set -uo pipefail

SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/duplicate-xml-doc-tags.sh"
PASS=0; FAIL=0
readonly E_OK=0 E_VIOLATION=1 E_ENV=2

ok()  { PASS=$((PASS + 1)); printf '  PASS  %s\n' "$1"; }
bad() { FAIL=$((FAIL + 1)); printf '  FAIL  %s\n     -> %s\n' "$1" "$2" >&2; }

run_on() { local dir="$1"; ( bash "$SCRIPT" "$dir" ) >/dev/null 2>"$dir/.stderr"; printf '%s' "$?"; }

# $1=dir $2=filename $3=body
write_cs() { mkdir -p "$1"; printf '%s\n' "$3" > "$1/$2"; }

CLEAN_MEMBER='/// <summary>Does the thing.</summary>
/// <remarks>One paragraph.</remarks>
public void DoThing() { }'

echo "duplicate-xml-doc-tags.sh — self-test"
echo

# A-safety (cannot-evaluate): a root with NO .cs must be E_ENV, never a vacuous 0.
dir="$(mktemp -d)"; printf 'not csharp\n' > "$dir/readme.txt"
rc="$(run_on "$dir")"
[ "$rc" = "$E_ENV" ] && ok "A-safety: no .cs under root exits E_ENV($E_ENV), not 0" \
  || bad "A-safety: empty root must exit E_ENV" "got $rc"
rm -rf "$dir"

# B-safety: two <remarks> in ONE block -> VIOLATION.
dir="$(mktemp -d)"
write_cs "$dir" "DupRemarks.cs" '/// <summary>X.</summary>
/// <remarks>first</remarks>
/// <remarks>second</remarks>
public int X;'
rc="$(run_on "$dir")"
{ [ "$rc" = "$E_VIOLATION" ] && grep -q 'remarks> x2' "$dir/.stderr"; } \
  && ok "B-safety: two <remarks> in one block is REFUSED (E_VIOLATION)" \
  || bad "B-safety: duplicate <remarks> must be refused" "got $rc: $(cat "$dir/.stderr")"
rm -rf "$dir"

# B2-safety: two <summary> in one block -> VIOLATION.
dir="$(mktemp -d)"
write_cs "$dir" "DupSummary.cs" '/// <summary>one</summary>
/// <summary>two</summary>
public int Y;'
rc="$(run_on "$dir")"
{ [ "$rc" = "$E_VIOLATION" ] && grep -q 'summary> x2' "$dir/.stderr"; } \
  && ok "B2-safety: two <summary> in one block is REFUSED" \
  || bad "B2-safety: duplicate <summary> must be refused" "got $rc: $(cat "$dir/.stderr")"
rm -rf "$dir"

# B3-safety: two <value> in one block -> VIOLATION.
dir="$(mktemp -d)"
write_cs "$dir" "DupValue.cs" '/// <summary>P.</summary>
/// <value>a</value>
/// <value>b</value>
public int P { get; }'
rc="$(run_on "$dir")"
{ [ "$rc" = "$E_VIOLATION" ] && grep -q 'value> x2' "$dir/.stderr"; } \
  && ok "B3-safety: two <value> in one block is REFUSED" \
  || bad "B3-safety: duplicate <value> must be refused" "got $rc: $(cat "$dir/.stderr")"
rm -rf "$dir"

# C-liveness: a clean block (one of each tag) -> PASS. Without this, "refuse dups" is satisfied
# by a gate that refuses everything.
dir="$(mktemp -d)"; write_cs "$dir" "Clean.cs" "$CLEAN_MEMBER"
rc="$(run_on "$dir")"
[ "$rc" = "$E_OK" ] && ok "C-liveness: a clean single-tag block PASSES (E_OK)" \
  || bad "C-liveness: clean doc block must pass" "got $rc: $(cat "$dir/.stderr")"
rm -rf "$dir"

# C2-precision: TWO distinct members, each with its OWN single <remarks>, separated by code.
# A gate that merged adjacent /// runs into one block would wrongly flag this. Must PASS.
dir="$(mktemp -d)"
write_cs "$dir" "TwoMembers.cs" '/// <summary>A.</summary>
/// <remarks>ra</remarks>
public int A;

/// <summary>B.</summary>
/// <remarks>rb</remarks>
public int B;'
rc="$(run_on "$dir")"
[ "$rc" = "$E_OK" ] && ok "C2-precision: two members each with one <remarks> do NOT over-fire" \
  || bad "C2-precision: distinct blocks must not be merged" "got $rc: $(cat "$dir/.stderr")"
rm -rf "$dir"

# C3-precision: a duplicate tag inside a GENERATED file (*.Designer.cs) must be IGNORED, while a
# sibling real .cs keeps cs_files>0. Must PASS (generated files are not hand-authored surface).
dir="$(mktemp -d)"
write_cs "$dir" "Real.cs" "$CLEAN_MEMBER"
write_cs "$dir" "Resources.Designer.cs" '/// <summary>gen</summary>
/// <summary>gen2</summary>
public int G;'
rc="$(run_on "$dir")"
[ "$rc" = "$E_OK" ] && ok "C3-precision: duplicate tag in *.Designer.cs is excluded (PASS)" \
  || bad "C3-precision: generated files must be excluded" "got $rc: $(cat "$dir/.stderr")"
rm -rf "$dir"

echo
printf 'passed %d · failed %d\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ] || exit 1
