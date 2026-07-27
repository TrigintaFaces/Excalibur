#!/usr/bin/env bash
# duplicate-xml-doc-tags.harness-lock.sh — INDEPENDENT (author≠impl) lock for duplicate-xml-doc-tags.sh
#
# HOME: .claude/harness/ (S883). Bead: qcizyz (enforcement arm for b9dwlt). S890.
# Author: TestsDeveloper — INDEPENDENT of the impl author (who wrote both the gate and its .test.sh).
#         This is the author≠impl seat the S890 Non-negotiable requires ("every harness deliverable =
#         impl + non-vacuous self-test + independent author≠impl *.harness-lock.sh"), carried OPEN
#         during IMPLEMENT because TestsDeveloper's session was dead. Filled now the seat is live.
#
# WHY A SEPARATE LOCK, NOT A COPY OF THE SELF-TEST
#   The .test.sh drives the gate through the ROOT-walk path with dir fixtures. This lock is
#   deliberately different so it can fail where a same-author test would share the author's blind spot:
#     1. It drives the gate through the FILE-argument path (a *.cs passed directly, as the pre-commit
#        hook does for staged files) — the fast path the self-test's dir fixtures don't isolate.
#     2. It adds the GENERATED-EXCLUSION liveness arm: a *.g.cs carrying a real duplicate must be
#        IGNORED (exit 0). A gate that flagged generated files would redden every build — a liveness
#        failure a safety-only test never sees.
#     3. It certifies the sprint's core property — the gate cannot report CLEAN(0) it did not earn, and
#        cannot cry VIOLATION(1) on clean input — and PROVES ITS OWN ARMS NON-VACUOUS against mutant
#        gates (always-clean / always-violation). A lock that also passes an always-clean mutant is the
#        S889 vacuity defect; this one fails the mutants, on purpose, and asserts that it does.
#
# CONTRACT under test (duplicate-xml-doc-tags.sh):
#   0  clean            — no doc block carries a duplicate <summary>/<remarks>/<value>
#   1  violation        — at least one doc block carries a duplicate
#   2  cannot-evaluate  — no C# files under the roots (must NOT read as clean)
#
# Usage:  bash eng/ci/duplicate-xml-doc-tags.harness-lock.sh
# Exit:   0 all arms pass · 1 an arm failed
set -uo pipefail

GATE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/duplicate-xml-doc-tags.sh"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
pass=0; fail=0
ok() { printf '  ✓ %s\n' "$1"; pass=$((pass + 1)); }
no() { printf '  ✗ %s — %s\n' "$1" "$2" >&2; fail=$((fail + 1)); }

is_clean()     { [ "$1" = 0 ]; }
is_violation() { [ "$1" = 1 ]; }
is_refuse()    { [ "$1" != 0 ] && [ "$1" != 1 ]; }   # E_ENV(2) or any non-evaluated code

# run <gate-path> <arg...> -> prints exit code
run() { bash "$@" >/dev/null 2>&1; echo $?; }

# fixtures — write a .cs file with the given body
cs() { mkdir -p "$(dirname "$1")"; printf '%s\n' "$2" > "$1"; }

DUP_SUMMARY='/// <summary>first</summary>
/// <summary>second</summary>
public int A;'
CLEAN_TWO_BLOCKS='/// <summary>alpha</summary>
public int A;

/// <summary>beta</summary>
public int B;'
DUP_VALUE='/// <value>one</value>
/// <value>two</value>
public int P { get; }'

# mutant gates: the two ways this gate can lie
M_CLEAN="$TMP/mutant-always-clean.sh";    printf '#!/usr/bin/env bash\nexit 0\n' > "$M_CLEAN"
M_VIOL="$TMP/mutant-always-violation.sh"; printf '#!/usr/bin/env bash\nexit 1\n' > "$M_VIOL"

echo "duplicate-xml-doc-tags.harness-lock.sh — INDEPENDENT lock (author≠impl)"
echo

# ── A · SAFETY (file-arg path): a file with two <summary> in one block -> VIOLATION, and the arm
#      must REJECT an always-clean gate. Passes the .cs FILE directly (the staged-file path). ────────
cs "$TMP/a/Dup.cs" "$DUP_SUMMARY"
rc="$(run "$GATE" "$TMP/a/Dup.cs")"
is_violation "$rc" && ok "A safety: two <summary> in one block (file-arg) -> VIOLATION(1)" \
    || no "A safety" "real gate returned $rc, contract demands 1"
rc="$(run "$M_CLEAN" "$TMP/a/Dup.cs")"
is_violation "$rc" \
    && no "A non-vacuity" "an always-CLEAN mutant SLIPPED PAST the safety arm (returned $rc) — arm is vacuous" \
    || ok "A non-vacuity: safety arm REJECTS the always-CLEAN mutant (mutant returned $rc, arm demands 1)"

# ── B · SAFETY (<value>): the third policed tag is caught too — the .test.sh headlines <remarks>. ──
cs "$TMP/v/DupVal.cs" "$DUP_VALUE"
rc="$(run "$GATE" "$TMP/v/DupVal.cs")"
is_violation "$rc" && ok "B safety: two <value> in one block -> VIOLATION(1)" \
    || no "B safety" "real gate returned $rc, contract demands 1"

# ── C · LIVENESS (precision): two SEPARATE single-<summary> blocks must NOT be a false duplicate.
#      This is the arm that catches a gate that merges adjacent blocks. + reject always-violation. ──
cs "$TMP/c/Clean.cs" "$CLEAN_TWO_BLOCKS"
rc="$(run "$GATE" "$TMP/c")"
is_clean "$rc" && ok "C liveness: two distinct single-tag blocks -> CLEAN(0) (no block-merge false positive)" \
    || no "C liveness" "real gate returned $rc, contract demands 0"
rc="$(run "$M_VIOL" "$TMP/c")"
is_clean "$rc" \
    && no "C non-vacuity" "an always-VIOLATION mutant SLIPPED PAST the liveness arm (returned $rc) — arm is vacuous" \
    || ok "C non-vacuity: liveness arm REJECTS the always-VIOLATION mutant (mutant returned $rc, arm demands 0)"

# ── D · LIVENESS (generated-file exclusion): a *.g.cs with a REAL duplicate, ALONGSIDE a clean
#      hand-authored .cs, must leave the verdict CLEAN — the generated dup is IGNORED. (A clean
#      sibling is required: the gate excludes generated files BEFORE counting, so a root holding ONLY
#      a *.g.cs is cannot-evaluate(2), not a false CLEAN — that path is exercised by arm E.)
#      A gate that flagged generated files would redden every build. ────────────────────────────────
cs "$TMP/d/Thing.g.cs" "$DUP_SUMMARY"
cs "$TMP/d/Real.cs"    '/// <summary>only one</summary>
public int R;'
rc="$(run "$GATE" "$TMP/d")"
is_clean "$rc" && ok "D liveness: a duplicate inside a *.g.cs is EXCLUDED (clean sibling present) -> CLEAN(0)" \
    || no "D liveness" "real gate returned $rc, contract demands 0 (generated files are not first-party doc surface)"

# ── E · REFUSE: a root with NO C# files is cannot-evaluate; MUST be E_ENV, never a silent CLEAN,
#      and the arm must REJECT an always-clean gate (the sprint's headline seam). ────────────────────
mkdir -p "$TMP/e"; printf 'not csharp\n' > "$TMP/e/readme.txt"
rc="$(run "$GATE" "$TMP/e")"
is_refuse "$rc" && ok "E refuse: a root with no .cs -> REFUSE/E_ENV (not a silent CLEAN)" \
    || no "E refuse" "real gate returned $rc, contract demands cannot-evaluate (not 0/1)"
rc="$(run "$M_CLEAN" "$TMP/e")"
is_refuse "$rc" \
    && no "E non-vacuity" "an always-CLEAN mutant SLIPPED PAST the refuse arm (returned $rc) — arm is vacuous" \
    || ok "E non-vacuity: refuse arm REJECTS the always-CLEAN mutant (mutant returned $rc, arm demands REFUSE)"

echo
printf 'passed %d · failed %d\n' "$pass" "$fail"
[ "$fail" -eq 0 ] || exit 1
