#!/usr/bin/env bash
# gate-denominator — the ONE line every gate must print, and the reason a zero is not a green.
#
# THE FAILURE THIS EXISTS TO KILL
#   A gate reported "0 violations" and was believed. Its true output was
#   "0 violations across 0 candidates examined" and it printed only the first half.
#   The population had drifted out of the matcher's shape; the regex matched nothing;
#   the gate said PASS. A self-test cannot notice this — the self-test feeds the gate its
#   OWN synthetic fixture, which by construction still matches. Only the real denominator,
#   measured against the real tree at the moment of the verdict, degrades toward zero when
#   the population drifts, and says so with nobody having anticipated the drift.
#
#   `0 violations / 4,213 candidates examined` has earned its green.
#   `0 violations / 0 candidates examined`     is visibly broken to any reader, no cleverness needed.
#
# CONTRACT
#   gate_denominator <count> <label...>
#       Prints   EXAMINED: <count> <label>       (stdout, machine-readable, one line)
#       Returns  0  when count > 0   — the gate may now render its verdict
#                2  when count == 0  — REFUSE. NOT a PASS. The caller must exit 2.
#                2  when count is not a number — a broken counter is also a REFUSE.
#
#   gate_denominator_may_be_empty <count> <label...>
#       Same line, but a zero returns 0. ONLY for a gate whose population is a DIFF
#       (a change touching none of the subject files legitimately examines nothing).
#       Never for a whole-tree scan: there, zero means the matcher broke.
#       The distinct name is the point — a reader sees the waiver at the call site.
#
# USAGE
#   . "$(dirname "$0")/gate-denominator.sh"
#   gate_denominator "$candidate_count" "SQL predicate fragment(s)" || exit 2
#
# Set GATE_NAME before calling to name the gate in the REFUSE message.
#
# Exit codes when RUN directly:  0 self-test passed · 3 self-test failed · 2 usage

gate_denominator() {
    local n="${1-}"; shift 2>/dev/null || true
    local label="${*:-candidate(s)}"
    case "$n" in
        ''|*[!0-9]*)
            printf 'EXAMINED: ? %s\n' "$label"
            printf 'REFUSE: %s produced a non-numeric denominator (%s) — a counter that broke is not a PASS.\n' \
                "${GATE_NAME:-${0##*/}}" "${n:-<empty>}" >&2
            return 2 ;;
    esac
    printf 'EXAMINED: %s %s\n' "$n" "$label"
    if [ "$n" -eq 0 ]; then
        printf 'REFUSE: %s examined 0 %s — a verdict over an empty population is not a PASS. The matcher, the path, or the population changed.\n' \
            "${GATE_NAME:-${0##*/}}" "$label" >&2
        return 2
    fi
    return 0
}

gate_denominator_may_be_empty() {
    local n="${1-}"; shift 2>/dev/null || true
    local label="${*:-candidate(s)}"
    case "$n" in
        ''|*[!0-9]*)
            printf 'EXAMINED: ? %s\n' "$label"
            printf 'REFUSE: %s produced a non-numeric denominator (%s).\n' "${GATE_NAME:-${0##*/}}" "${n:-<empty>}" >&2
            return 2 ;;
    esac
    printf 'EXAMINED: %s %s (diff-scoped; an empty diff legitimately examines none)\n' "$n" "$label"
    return 0
}

# --------------------------------------------------------------------------
# Self-test — runs when this file is EXECUTED rather than sourced.
# Non-vacuous in both directions: the zero arm must REFUSE and the non-zero arm
# must PASS, or the helper is a rubber stamp in one direction.
# --------------------------------------------------------------------------
_gate_denominator_self_test() {
    local pass=1 out rc

    out="$(gate_denominator 4213 'SQL predicate fragment(s)' 2>/dev/null)"; rc=$?
    [ "$rc" -eq 0 ] || { echo "self-test FAIL: a non-empty population did not return 0 (got $rc)" >&2; pass=0; }
    [ "$out" = 'EXAMINED: 4213 SQL predicate fragment(s)' ] || {
        echo "self-test FAIL: line was '$out'" >&2; pass=0; }

    out="$(gate_denominator 0 'SQL predicate fragment(s)' 2>/dev/null)"; rc=$?
    [ "$rc" -eq 2 ] || { echo "self-test FAIL: an EMPTY population returned $rc, not REFUSE(2) — this is the whole point" >&2; pass=0; }
    [ "$out" = 'EXAMINED: 0 SQL predicate fragment(s)' ] || {
        echo "self-test FAIL: zero case did not print its denominator: '$out'" >&2; pass=0; }

    gate_denominator '' 'x' >/dev/null 2>&1; [ $? -eq 2 ] || { echo "self-test FAIL: empty count not REFUSE" >&2; pass=0; }
    gate_denominator 'many' 'x' >/dev/null 2>&1; [ $? -eq 2 ] || { echo "self-test FAIL: non-numeric count not REFUSE" >&2; pass=0; }

    gate_denominator_may_be_empty 0 'changed file(s)' >/dev/null 2>&1
    [ $? -eq 0 ] || { echo "self-test FAIL: diff-scoped waiver rejected a legitimately empty diff" >&2; pass=0; }
    out="$(gate_denominator_may_be_empty 7 'changed file(s)' 2>/dev/null)"
    case "$out" in 'EXAMINED: 7 changed file(s)'*) ;; *) echo "self-test FAIL: waiver line was '$out'" >&2; pass=0 ;; esac

    if [ "$pass" -eq 1 ]; then
        echo "gate-denominator self-test PASSED (non-zero -> 0 with the count printed; zero -> REFUSE(2) with the count printed; non-numeric -> REFUSE; diff waiver allows zero)."
        return 0
    fi
    echo "gate-denominator self-test FAILED." >&2
    return 3
}

# Arg handling ONLY when executed directly. When SOURCED, $1 belongs to the CALLING gate —
# reading it here made this file run its own self-test and exit 0 out of a gate invoked with
# --self-test, i.e. a helper answering for its caller. That is the false-green class this
# whole mechanism exists to remove, so it is guarded rather than commented.
if [ "${BASH_SOURCE[0]}" = "${0}" ]; then
    case "${1-}" in
        --self-test) _gate_denominator_self_test; exit $? ;;
        '') : ;;
        *) echo "gate-denominator.sh: unknown arg '$1' (only --self-test)" >&2; exit 2 ;;
    esac
fi
