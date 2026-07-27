#!/usr/bin/env bash
# pre-commit-dispatch-gate.sh — asserts eng/hooks/pre-commit reads every gate's exit code HONESTLY.
#
# THE INVARIANT (e5juti): a non-verdict exit (2 syntax-error, 124 timeout, 127 not-found, 143 killed)
# from a dispatched gate MUST NOT be read as PASS. The hook captures each gate's exit into a `_rc`
# variable and branches on it; if that branch only blocks on `-eq 1`, then a gate that FAILED TO RUN
# (any non-1 code) silently permits the commit it exists to block — the failure mode is inverted
# exactly when the gate is most broken (r4dzl2 class).
#
# No existing gate looks at this. gate-wiring checks a gate has a caller; f5-sweep checks siblings;
# nothing checks that the HOOK reads its gates' exit codes honestly. That gap is why r4dzl2 needed a
# human sweep and why the sweep missed a site 200 lines below the cluster it enumerated.
#
# WHAT IT DOES
#   1. Enumerate EVERY capture-then-branch site — every `<var>_rc=$?` — NOT the narrower
#      `&& _rc=0 || _rc=$?` idiom (which misses `; sddl_rc=$?` and a bare `gate_rc=$?`; enumerating by
#      the narrow idiom is the exact sample-not-enumerate defect this gate exists to prevent).
#   2. For each, classify how its branch treats a NON-VERDICT exit:
#        case "$v" in … *) … esac     -> SAFE  (three-state: the catch-all handles non-verdict exits,
#                                                whether by blocking or a documented fail-open-loud)
#        if [ "$v" -ne 0 ] …           -> SAFE  (fail-closed: ALL non-zero block; extra `-ne N`
#                                                exemptions are intentional known-good codes)
#        if [ "$v" -eq 1 ] …           -> VIOLATION (ONLY 1 blocks; 2/124/127/143 fall through to PASS)
#        case "$v" with NO *) arm      -> VIOLATION (a non-verdict matches nothing -> falls past esac)
#        captured but never branched   -> VIOLATION (read into a var, never consulted)
#   3. Non-vacuity floor: >=1 capture site must be found, else REFUSE (the enumerate-zero-report-clean
#      defect — a green on a file with no dispatch sites is not a pass).
#
# The rule is NOT "must be a case statement": `-ne 0` is fail-closed and CORRECT (freeze guard). The
# two-prong test applies to this gate's own findings — a site is a VIOLATION only if it genuinely reads
# a non-verdict exit as PASS.
#
# Exit codes (every code maps to exactly one of {PASS, FAIL, REFUSE}):
#   0  PASS    — every dispatch site handles non-verdict exits (three-state or fail-closed)
#   1  FAIL    — >=1 site reads a non-verdict exit as PASS
#   2  REFUSE  — could not evaluate (hook not found / zero dispatch sites found)
#
# Overrides (for the self-test — never used in production):
#   PRECOMMIT_DISPATCH_HOOK        path to the hook file to audit (default eng/hooks/pre-commit)
#   PRECOMMIT_DISPATCH_REPO_ROOT   repo root for resolving a relative hook path

set -uo pipefail

E_PASS=0; E_FAIL=1; E_REFUSE=2

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="${PRECOMMIT_DISPATCH_REPO_ROOT:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
HOOK="${PRECOMMIT_DISPATCH_HOOK:-eng/hooks/pre-commit}"
case "$HOOK" in /*) : ;; *) HOOK="$REPO_ROOT/$HOOK" ;; esac

if [ ! -f "$HOOK" ]; then
    echo "pre-commit-dispatch-gate: REFUSE — hook file not found: $HOOK" >&2
    echo "  This is NOT 'all dispatch sites honest'. Fix the path; do not route around the gate." >&2
    exit "$E_REFUSE"
fi

# --- enumerate capture lines: "<var>_rc=$?" (the branch-on-exit-code sites) --------------------------
# Each captured line has exactly one `<var>_rc=$?`. Record "lineno var" pairs, sorted by line.
mapfile -t CAP_LINES < <(grep -nE '[a-z_]+_rc=\$\?' "$HOOK" | sort -t: -k1,1n)

if [ "${#CAP_LINES[@]}" -eq 0 ]; then
    echo "pre-commit-dispatch-gate: REFUSE — zero '<var>_rc=\$?' capture sites found in $HOOK." >&2
    echo "  An empty enumeration is not a clean bill of health (the enumerate-zero defect)." >&2
    exit "$E_REFUSE"
fi

TOTAL_LINES="$(wc -l < "$HOOK")"

# parse "lineno:content" -> arrays
declare -a LNO VAR
for entry in "${CAP_LINES[@]}"; do
    n="${entry%%:*}"
    var="$(printf '%s\n' "$entry" | grep -oE '[a-z_]+_rc=\$\?' | head -1)"; var="${var%=*}"
    LNO+=("$n"); VAR+=("$var")
done

# region for capture i = [ LNO[i] , LNO[i+1]-1 ]  (last -> EOF). Branch handling sits between captures.
violations=0
report=""

for i in "${!LNO[@]}"; do
    v="${VAR[$i]}"
    start="${LNO[$i]}"
    j=$((i + 1))
    if [ "$j" -lt "${#LNO[@]}" ]; then end=$(( ${LNO[$j]} - 1 )); else end="$TOTAL_LINES"; fi
    region="$(sed -n "${start},${end}p" "$HOOK")"

    # verdict lines = region lines that reference this var AND carry a test/case token.
    # $v appears as "$v", "${v" or "${v:-0}"; anchor on a non-word boundary after the name.
    vref="\\\$\{?${v}([^A-Za-z0-9_]|\})"

    verdict="unknown"

    # 1. case-form?  case "$v" … in
    case_off="$(printf '%s\n' "$region" | grep -nE "case[[:space:]]+\"?${vref}" | head -1 | cut -d: -f1)"
    if [ -n "$case_off" ]; then
        # slice from the `case` line to the first following `esac`, look for a *) catch-all.
        rel_esac="$(printf '%s\n' "$region" | tail -n +"$case_off" | grep -nE '(^|[[:space:]])esac([[:space:]]|;|$)' | head -1 | cut -d: -f1)"
        if [ -n "$rel_esac" ]; then
            esac_off=$((case_off + rel_esac - 1))
            caseblk="$(printf '%s\n' "$region" | sed -n "${case_off},${esac_off}p")"
        else
            caseblk="$(printf '%s\n' "$region" | tail -n +"$case_off")"
        fi
        if printf '%s\n' "$caseblk" | grep -qE '^[[:space:]]*\*\)'; then
            verdict="SAFE:three-state-case"
        else
            verdict="VIOLATION:case-without-catchall"
        fi
    # 2. fail-closed if-form:  [ "$v" … -ne 0 … ]
    elif printf '%s\n' "$region" | grep -qE "\[[^]]*${vref}[^]]*-ne[[:space:]]+0"; then
        verdict="SAFE:fail-closed-ne0"
    # 3. single-code block:  [ "$v" … -eq <n> … ]  (only that code blocks; non-verdict passes)
    elif printf '%s\n' "$region" | grep -qE "\[[^]]*${vref}[^]]*-eq[[:space:]]+[0-9]"; then
        verdict="VIOLATION:eq-single-code"
    # 4. captured but never consulted in a verdict position
    else
        verdict="VIOLATION:captured-never-read"
    fi

    case "$verdict" in
        SAFE:*) : ;;
        VIOLATION:*)
            violations=$((violations + 1))
            report="${report}  ${v} (hook:${start})  ${verdict#VIOLATION:}"$'\n'
            ;;
    esac
done

echo "pre-commit-dispatch-gate: audited ${#LNO[@]} dispatch site(s) in ${HOOK#$REPO_ROOT/}"

if [ "$violations" -ne 0 ]; then
    echo "✗ ${violations} dispatch site(s) read a NON-VERDICT exit as PASS:" >&2
    printf '%s' "$report" >&2
    echo "  Fix: a case with a *) catch-all, OR 'if [ \"\$v\" -ne 0 ]' (fail-closed). Never '-eq 1' alone." >&2
    exit "$E_FAIL"
fi

echo "✓ every dispatch site handles non-verdict exits (three-state or fail-closed)"
exit "$E_PASS"
