#!/usr/bin/env bash
# gate-denominator-gate — every enforcement gate must report WHAT IT EXAMINED, not only what it found.
#
# WHY
#   A gate that prints "0 violations" and nothing else is indistinguishable from a gate whose
#   matcher stopped matching. One of ours was exactly that: its true output was
#   "0 violations across 0 candidates examined" and it printed only the first half, so a
#   drifted population read as a clean tree for as long as nobody looked.
#
#   The defence is one counter and one line per gate — `EXAMINED: <n> <label>` — emitted at the
#   moment of the verdict, over the real tree. It degrades safely and automatically: when a regex
#   silently stops matching because the population's formatting drifted, the count falls toward
#   zero and says so, with nobody having anticipated anything. A self-test cannot do this: it
#   feeds the gate a fixture that by construction still matches.
#
# WHAT THIS GATE CHECKS
#   Every gate script under eng/ci either
#     (a) emits the marker — a `gate_denominator` / `gate_denominator_may_be_empty` call, or a
#         literal `EXAMINED:` line it prints itself (python gates do this directly), or
#     (b) is listed in gate-denominator-baseline.txt as tracked debt or as NOT-A-GATE.
#
#   The baseline's debt section is a SHRINK TARGET. Migrating a gate deletes its line.
#   A brand-new gate is expected to emit the marker; appending it to the baseline is an
#   explicit, reviewed admission of debt.
#
# THREE-STATE CONTRACT (REFUSE != PASS)
#   0  PASS    — a non-empty population of gate scripts was examined and every one is covered
#   1  FAIL    — at least one gate script neither emits the marker nor is baselined
#   2  REFUSE  — no gate scripts found, or the baseline is unreadable: no verdict is possible
#   3  self-test failed
#
# Usage: gate-denominator-gate.sh [--root <dir>] [--self-test]
set -uo pipefail

E_PASS=0; E_FAIL=1; E_REFUSE=2; E_SELFTEST=3
GATE_NAME="gate-denominator-gate"

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=/dev/null
. "$HERE/gate-denominator.sh"

BASELINE_NAME="gate-denominator-baseline.txt"

# gdg_is_gate_script <basename> -> 0 if it is a gate we hold to the contract
gdg_is_gate_script() {
    case "$1" in
        *.test.sh|*.harness-lock.sh|*fixture*|gate-denominator.sh) return 1 ;;
    esac
    return 0
}

# gdg_emits_marker <file> -> 0 if the script emits a denominator
gdg_emits_marker() {
    grep -qE 'gate_denominator(_may_be_empty)?[[:space:]]|EXAMINED:' "$1"
}

# gdg_baseline_names <baseline-file> -> basenames, one per line (comments/blanks stripped)
gdg_baseline_names() {
    sed -e 's/#.*//' -e 's/[[:space:]]*$//' "$1" | grep -v '^[[:space:]]*$'
}

run_gate() {
    local root="$1" baseline="$1/$BASELINE_NAME"
    local covered=0 total=0 baselined
    local uncovered=()

    if [ ! -r "$baseline" ]; then
        echo "REFUSE: $GATE_NAME cannot read its baseline at $baseline — no verdict is possible." >&2
        return $E_REFUSE
    fi
    baselined="$(gdg_baseline_names "$baseline")"

    local f base
    for f in "$root"/*.sh "$root"/*.py; do
        [ -e "$f" ] || continue
        base="${f##*/}"
        gdg_is_gate_script "$base" || continue
        total=$((total + 1))
        if printf '%s\n' "$baselined" | grep -qxF "$base"; then continue; fi
        if gdg_emits_marker "$f"; then covered=$((covered + 1)); else uncovered+=("$base"); fi
    done

    gate_denominator "$total" "gate script(s) under $root" || return $E_REFUSE

    if [ ${#uncovered[@]} -gt 0 ]; then
        echo "$GATE_NAME: FAIL — ${#uncovered[@]} gate script(s) render a verdict without reporting what they examined:" >&2
        printf '  %s\n' "${uncovered[@]}" >&2
        echo "  Fix: source gate-denominator.sh and call gate_denominator <count> <label> before the verdict," >&2
        echo "  or add the basename to $BASELINE_NAME with a reason." >&2
        return $E_FAIL
    fi
    echo "$GATE_NAME: PASS — $covered gate script(s) emit a denominator; the remainder are baselined as tracked debt or not gates."
    return $E_PASS
}

# --------------------------------------------------------------------------
# Self-test. Fixtures are REAL cited excerpts from this tree, not invented shapes:
#   the covered fixture is the marker call as it appears in a migrated gate;
#   the uncovered fixture is the verdict line of a gate that prints only what it FOUND —
#   copied from sql-predicate-gate.sh:225 as it read before migration:
#     echo "sql-predicate-gate: PASS — every declared predicate fragment is interpolated or concatenated into SQL."
#   which is the exact shape that lets a drifted population read as a clean tree.
# --------------------------------------------------------------------------
self_test() {
    local pass=1 tmp rc out
    tmp="$(mktemp -d)"

    cat > "$tmp/emitting-gate.sh" <<'FIXTURE_COVERED'
. "$(dirname "$0")/gate-denominator.sh"
gate_denominator "$count" "SQL predicate fragment(s)" || exit 2
echo "covered-gate: PASS"
FIXTURE_COVERED

    cat > "$tmp/silent-gate.sh" <<'FIXTURE_UNCOVERED'
echo "sql-predicate-gate: PASS — every declared predicate fragment is interpolated or concatenated into SQL."
FIXTURE_UNCOVERED

    cat > "$tmp/some-gate.test.sh" <<'FIXTURE_SELFTEST'
echo "this is a self-test file and must not be held to the contract"
FIXTURE_SELFTEST

    printf '# baseline\n' > "$tmp/$BASELINE_NAME"

    # S1 SAFETY: a gate printing only what it FOUND is rejected.
    out="$(run_gate "$tmp" 2>&1)"; rc=$?
    if [ "$rc" -eq $E_FAIL ] && printf '%s' "$out" | grep -q 'silent-gate.sh'; then
        echo "  S1 PASS  a gate that reports no denominator is rejected"
    else
        echo "  S1 FAIL  expected FAIL(1) naming silent-gate.sh, got rc=$rc" >&2; pass=0
    fi
    # ...and must NOT have flagged the covered one, or it flags everything.
    if printf '%s' "$out" | grep -q 'emitting-gate.sh'; then
        echo "  S1b FAIL a gate that DOES emit the marker was flagged (the gate flags everything)" >&2; pass=0
    else
        echo "  S1b PASS a gate that emits the marker is not flagged"
    fi
    # ...and must not hold a .test.sh to the contract.
    if printf '%s' "$out" | grep -q 'some-gate.test.sh'; then
        echo "  S1c FAIL a .test.sh file was held to the gate contract" >&2; pass=0
    else
        echo "  S1c PASS self-test files are excluded from the population"
    fi

    # L1 LIVENESS: baselining the offender passes. Without this arm a gate that rejects
    # everything unconditionally would score a green on S1 alone.
    echo 'silent-gate.sh' >> "$tmp/$BASELINE_NAME"
    out="$(run_gate "$tmp" 2>&1)"; rc=$?
    if [ "$rc" -eq $E_PASS ]; then echo "  L1 PASS  a fully covered/baselined tree passes"
    else echo "  L1 FAIL  expected PASS(0), got rc=$rc: $out" >&2; pass=0; fi

    # L2: the verdict itself carries a denominator — this gate obeys its own contract.
    if printf '%s' "$out" | grep -q 'EXAMINED: 2 gate script'; then
        echo "  L2 PASS  the gate prints its own denominator"
    else
        echo "  L2 FAIL  the gate did not print 'EXAMINED: 2 gate script(s)': $out" >&2; pass=0
    fi

    # R1 REFUSE: an empty population is not a PASS.
    local empty="$tmp/empty"; mkdir -p "$empty"; printf '# baseline\n' > "$empty/$BASELINE_NAME"
    out="$(run_gate "$empty" 2>&1)"; rc=$?
    if [ "$rc" -eq $E_REFUSE ]; then echo "  R1 PASS  an empty population REFUSEs (exit 2), distinct from PASS"
    else echo "  R1 FAIL  expected REFUSE(2) on an empty tree, got rc=$rc" >&2; pass=0; fi

    # R2 REFUSE: an unreadable baseline is not a PASS.
    local nob="$tmp/nobaseline"; mkdir -p "$nob"; : > "$nob/x-gate.sh"
    run_gate "$nob" >/dev/null 2>&1; rc=$?
    if [ "$rc" -eq $E_REFUSE ]; then echo "  R2 PASS  a missing baseline REFUSEs"
    else echo "  R2 FAIL  expected REFUSE(2) with no baseline, got rc=$rc" >&2; pass=0; fi

    rm -rf "$tmp"
    [ "$pass" -eq 1 ] && { echo "$GATE_NAME self-test PASSED."; return 0; }
    echo "$GATE_NAME self-test FAILED." >&2; return $E_SELFTEST
}

main() {
    local root="$HERE"
    while [ $# -gt 0 ]; do
        case "$1" in
            --self-test) self_test; exit $? ;;
            --root) root="$2"; shift 2 ;;
            -h|--help) sed -n '2,32p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
            *) echo "$GATE_NAME: unknown arg '$1'" >&2; exit $E_REFUSE ;;
        esac
    done
    run_gate "$root"; exit $?
}

main "$@"
