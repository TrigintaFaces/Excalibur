#!/usr/bin/env bash
# shipped-ddl-sweep-gate — the CI wiring for shipped-ddl-sweep. Runs the REAL sweep against the repo
# (the sweep itself was only ever exercised by its .test.sh in CI, never against the tree), and enforces
# a SET baseline of accepted REFUSEs with a LIVENESS arm.
#
# WHY A WRAPPER: harness-gates-ci's run() reds on ANY non-zero, which would collapse the sweep's three
# states to two and red the build on a REFUSE. A REFUSE is the gate WORKING (it could not evaluate a
# table's shipped DDL — a coverage gap it is announcing), not FAILING. This wrapper maps the three states:
#
#   PASS   (sweep 0)  everything evaluated and matched            -> gate 0
#   FAIL   (sweep 1)  a mapped table's shipped DDL DRIFTED from    -> gate 1 (RED: real consumer-facing bug)
#                     its write path
#   REFUSE (sweep 2)  a table could not be evaluated. Compared to  -> gate 0 if the REFUSE set == baseline;
#                     the SET baseline: a table ENTERING the set       gate 1 if a table ENTERED the set
#                     is a NEW gap (RED); the baselined set is           (a NEW coverage gap)
#                     tracked (bead per line) and does NOT red.
#
# SET, not COUNT: a count is blind to substitution — map one table (-1), a schema change adds an
# unmapped one (+1), count unchanged, the new gap invisible. The baseline is a SET.
#
# LIVENESS: a gate that skipped / timed out / exited early emits ZERO REFUSEs and a
# vacuously-satisfied baseline — the exact defect this class catches (a permanently-satisfied gate that
# never runs). So the wrapper PROVES the sweep executed: it must have emitted at least one evaluated-table
# ("ok") line. No report ⇒ FAIL, never a silent pass.
#
# Exit: 0 = ran, live, no drift, REFUSE set within baseline.  1 = drift / new REFUSE / did-not-run.
#       3 = --self-test failed (the wrapper itself is broken/vacuous).
set -uo pipefail

ROOT="$(git rev-parse --show-toplevel 2>/dev/null || echo .)"
cd "$ROOT" || exit 1
BASELINE="${SHIPPED_DDL_REFUSE_BASELINE:-eng/ci/shipped-ddl-sweep.refuse-baseline.txt}"
SWEEP="${SHIPPED_DDL_SWEEP:-eng/ci/shipped-ddl-sweep.sh}"

# Normalise a "REFUSE <label> — explanation" line to just the <label> (the table + provider tag the
# sweep prints), so it matches a baseline line verbatim.
_norm_refuse() { sed -E 's/^[[:space:]]*REFUSE[[:space:]]+//; s/[[:space:]]+—.*$//; s/[[:space:]]+has NO MAP.*$//; s/[[:space:]]+$//'; }

# evaluate <sweep_output> <sweep_rc> <baseline_file> -> prints result, returns gate exit code.
_evaluate() {
    local out="$1" rc="$2" baseline_file="$3"
    local ok_count current baseline new_refuse closed

    # LIVENESS: the sweep must have evaluated at least one table (an "ok" line). A run that produced no
    # such line skipped, hung, or died before reporting — a zero-REFUSE from that is not a pass.
    ok_count="$(printf '%s\n' "$out" | grep -c 'ok ')"
    if [ "$ok_count" -eq 0 ]; then
        echo "::error::shipped-ddl-sweep-gate: LIVENESS FAIL — the sweep produced no evaluated-table lines; it did not run or died before reporting. A silent zero-REFUSE is not a pass."
        return 1
    fi

    # Real drift: a mapped table's shipped DDL no longer matches its write path.
    if [ "$rc" -eq 1 ]; then
        echo "::error::shipped-ddl-sweep-gate: FAIL — a shipped DDL DRIFTED from its write path (real consumer-facing drift). See FAIL lines above."
        return 1
    fi
    if [ "$rc" -ne 0 ] && [ "$rc" -ne 2 ]; then
        echo "::error::shipped-ddl-sweep-gate: unexpected sweep exit $rc"; return 1
    fi

    # SET baseline: which tables does the sweep currently REFUSE on, vs the accepted set?
    current="$(printf '%s\n' "$out" | grep -E '^[[:space:]]*REFUSE[[:space:]]' | _norm_refuse | sort -u)"
    baseline="$(grep -vE '^[[:space:]]*#|^[[:space:]]*$' "$baseline_file" 2>/dev/null | sed -E 's/[[:space:]]+$//' | sort -u)"

    new_refuse="$(comm -23 <(printf '%s\n' "$current") <(printf '%s\n' "$baseline") | grep -vE '^$' || true)"
    if [ -n "$new_refuse" ]; then
        echo "::error::shipped-ddl-sweep-gate: NEW REFUSE(s) not in the baseline — a coverage gap appeared. MAP the table, or (with a written reason + a filed bead) add it to $BASELINE. NEW:"
        printf '   %s\n' "$new_refuse"
        return 1
    fi

    closed="$(comm -13 <(printf '%s\n' "$current") <(printf '%s\n' "$baseline") | grep -vE '^$' || true)"
    if [ -n "$closed" ]; then
        echo "::warning::shipped-ddl-sweep-gate: baselined REFUSE(s) no longer refuse — delete them from $BASELINE (a reviewable diff, the gap was closed):"
        printf '   %s\n' "$closed"
    fi

    echo "shipped-ddl-sweep-gate: OK — sweep ran ($ok_count tables evaluated), no drift, REFUSE set within baseline ($(printf '%s\n' "$current" | grep -cvE '^$') tracked)."
    return 0
}

# ── self-test: the wrapper's own non-vacuity. Every arm must be able to FAIL, or the gate is theatre.
if [ "${1:-}" = "--self-test" ]; then
    pass=1
    live="  ok  dbo.Foo — 3 cols all declared"
    # (a) LIVENESS: a report with no evaluated-table line must FAIL, even with an empty REFUSE set.
    if _evaluate "$(printf 'random noise\nno tables here\n')" 2 "/dev/null" >/dev/null 2>&1; then
        echo "self-test FAIL: LIVENESS arm passed on a report with zero evaluated tables (did-not-run looked clean)" >&2; pass=0
    fi
    # (b) DRIFT: sweep exit 1 must FAIL regardless of the sets.
    if _evaluate "$live" 1 "/dev/null" >/dev/null 2>&1; then
        echo "self-test FAIL: DRIFT arm passed on sweep exit 1 (a real drift was tolerated)" >&2; pass=0
    fi
    # (c) NEW REFUSE: a REFUSE table absent from the baseline must FAIL.
    tmpb="$(mktemp)"; printf 'known_table (x)\n' > "$tmpb"
    if _evaluate "$(printf '%s\n  REFUSE  brand_new_table (y) — has NO MAP entry.\n' "$live")" 2 "$tmpb" >/dev/null 2>&1; then
        echo "self-test FAIL: a NEW REFUSE not in the baseline passed (SET arm blind to a new gap)" >&2; pass=0
    fi
    # (d) BASELINED REFUSE: a REFUSE table IN the baseline must PASS (the gate working, not failing).
    printf 'baselined_table (z)\n' > "$tmpb"
    if ! _evaluate "$(printf '%s\n  REFUSE  baselined_table (z) — has NO MAP entry.\n' "$live")" 2 "$tmpb" >/dev/null 2>&1; then
        echo "self-test FAIL: a BASELINED REFUSE reddened the gate (a REFUSE must be the gate working, not failing)" >&2; pass=0
    fi
    rm -f "$tmpb"
    if [ "$pass" -eq 1 ]; then
        echo "✅ shipped-ddl-sweep-gate self-test PASSED (liveness · drift · new-refuse · baselined-refuse arms all non-vacuous)."
        exit 0
    fi
    echo "❌ shipped-ddl-sweep-gate self-test FAILED." >&2
    exit 3
fi

# ── real run
sweep_out="$(bash "$SWEEP" 2>&1)"; sweep_rc=$?
printf '%s\n' "$sweep_out"
_evaluate "$sweep_out" "$sweep_rc" "$BASELINE"
