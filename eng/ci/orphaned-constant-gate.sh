#!/usr/bin/env bash
# orphaned-constant-gate.sh — a COMPUTED cadence/cap constant asserted in a COMMENT that appears
# NOWHERE in that file's CODE is prose orphaned by a parameterising refactor. This gate detects it.
#
# THE DEFECT CLASS (a real one, found by a human mid-incident at ~90 minutes / four refuted theories):
#
#   A literal cap is turned into a variable (7200 becomes an env-overridable IDLE_WINDOW_SECONDS, the
#   code becomes 86400), the code is updated, and the PROSE stating the old computed value is not:
#       # the hard cap is ~2h  (7200 * 1s)      ... 7200 ticks @1s ...
#   The comment stays plausible because it describes what the code USED to do. It is not executed, not
#   testable, and no other gate covers it — so "7200 ticks" outlives the code that made it true, and the
#   next reader trusts a number the program no longer contains.
#
# WHY THIS SUBSET, AND NOT "ANY NUMBER IN A COMMENT" (the AC4 crux — read before widening):
#
#   "any comment number absent from the code" is UNUSABLY noisy: a healthy ops script's comments are full
#   of legitimate prose numbers — measured counts ("~570 live pollers"), line deltas ("+125 lines"), a
#   sprint number, a message id — none of which are code constants and all of which are absent from the
#   code. Flagging them is the noise that gets a gate switched off. The DECIDABLE, low-false-positive
#   subset is a number presented as a COMPUTED CADENCE/CAP: a digit run that is an arithmetic operand
#   (`N * …` / `… * N`) or carries the cadence unit (`N tick(s)`). That is exactly the shape the real
#   ghost took, and — verified against the file it came from — it fires on the orphaned cap while staying
#   silent on every prose number in the same file. A cadence constant that IS still in the code (a log
#   interval `300 ticks` where the code holds `300`) is correctly silent; only the absent one fires.
#
# EXCLUDED, with reason (AC4):
#   * a digit run NOT in `N *` / `* N` / `N tick(s)` context — narrative prose, out of scope by design;
#   * < 3 digits            — 1-2 digit cadences (`10*30s`, `5 ticks`) are too weak a signal to net;
#   * a 4-digit year        — 19xx / 20xx are dates even inside an expression;
#   * a number the code contains — an in-code cadence constant is documentation, not an orphan.
#
# SCOPE (AC5): production scans SHELL scripts (`*.sh`) under `.claude/hooks`, `.claude/harness`, and `eng/`
# — the ops-script surface where cadence/cap prose actually lives (the real ghost was a `.claude/hooks`
# cap comment; `eng/` shell scripts carry no cadence idiom, so an `eng/`-only scope would be VACUOUS —
# proven by ARM8). Because it reads `.claude/**` it is a STAYS-LOCAL gate (private paths are not on the
# public mirror): its CALLER (AC1) is the local pre-commit / local harness, NOT a mirror workflow. The
# `.claude`-dependent self-test arms self-skip where that history is absent (a shallow/mirror clone), so
# the lock stays green anywhere; the synthetic arms carry the class there. EXCLUDED from default scope:
# C#/markdown (a comment number in prose docs is far likelier a legitimate cross-reference than a stale
# constant). Widen or narrow via ORPHCONST_ROOTS.
#
# EXIT CODES (every one mapped by the caller; a non-0/1 is NEVER a pass):
#   0  PASS    scanned, at least one computed cadence/cap number evaluated, none orphaned
#   1  FAIL    a computed cadence/cap number in a comment appears in no code line of its file
#   2  REFUSE  could not evaluate (no git / zero files / zero cadence-numbers seen == parser blind)
#   3  REFUSE  --self-test failed (the gate itself is broken or vacuous)
#   *  REFUSE  unknown arg == could-not-evaluate
#
# There is deliberately NO suppression cap and NO allowlist: a gate that can mute itself into a PASS is
# the false-safety class this exists to remove.
#
# TESTABILITY SEAM (an independent author != impl lock binds THIS surface):
#   ORPHCONST_ROOTS      scan roots for the production sweep    (default: "eng")
#   ORPHCONST_REPO_ROOT  repo root; unset => git toplevel       (lets a hermetic fixture skip git)
#   ORPHCONST_MIN_NUMS   non-vacuity floor of cadence-numbers   (default: 1)

set -uo pipefail

E_PASS=0; E_FAIL=1; E_REFUSE=2; E_SELFTEST=3

REPO_ROOT="${ORPHCONST_REPO_ROOT:-$(git rev-parse --show-toplevel 2>/dev/null || echo .)}"
cd "$REPO_ROOT" || exit $E_REFUSE

ORPHCONST_ROOTS="${ORPHCONST_ROOTS:-.claude/hooks .claude/harness eng}"
ORPHCONST_MIN_NUMS="${ORPHCONST_MIN_NUMS:-1}"

_list_sh() {
    local pat base got
    for pat in $1; do
        base="${pat%%\*\*}"; base="${base%/}"; [ -n "$base" ] || base="$pat"
        case "$pat" in
            /*|[A-Za-z]:[\\/]*)
                if [ -d "$base" ]; then find "$base" -type f -name '*.sh' 2>/dev/null
                elif [ -f "$pat" ]; then printf '%s\n' "$pat"; fi ;;
            *)
                got="$(git ls-files -- "$pat" 2>/dev/null | grep -E '\.sh$' || true)"
                if [ -n "$got" ]; then printf '%s\n' "$got"
                elif [ -d "$base" ]; then find "$base" -type f -name '*.sh' 2>/dev/null
                elif [ -f "$pat" ]; then printf '%s\n' "$pat"; fi ;;
        esac
    done
}

# ── scan ONE file: emit `NUM <n>` for every computed cadence/cap number evaluated and ───────────────
# `ORPHAN <n>:<line>` for every one that appears in no code line. One awk process per file.
_scan() {
    awk '
        function is_year(t) { return (t ~ /^(19|20)[0-9][0-9]$/) }
        # collect computed cadence/cap numbers (>=3 digits) from a comment string into cand[]
        function collect(s, cand,    tmp, tok) {
            tmp = s
            while (match(tmp, /[0-9][0-9][0-9]+[ \t]*\*/)) {                 # N *
                tok = substr(tmp, RSTART, RLENGTH); gsub(/[^0-9]/, "", tok)
                if (!is_year(tok)) cand[tok] = 1
                tmp = substr(tmp, RSTART + RLENGTH)
            }
            tmp = s
            while (match(tmp, /\*[ \t]*[0-9][0-9][0-9]+/)) {                 # * N
                tok = substr(tmp, RSTART, RLENGTH); gsub(/[^0-9]/, "", tok)
                if (!is_year(tok)) cand[tok] = 1
                tmp = substr(tmp, RSTART + RLENGTH)
            }
            tmp = s
            while (match(tmp, /[0-9][0-9][0-9]+[ \t]*ticks?/)) {            # N tick(s)
                tok = substr(tmp, RSTART, RLENGTH); gsub(/[^0-9]/, "", tok)
                if (!is_year(tok)) cand[tok] = 1
                tmp = substr(tmp, RSTART + RLENGTH)
            }
        }
        {
            line = $0
            code = line; cmt = ""
            if (match(line, /(^|[ \t])#/)) { code = substr(line, 1, RSTART - 1); cmt = substr(line, RSTART) }
            code_all = code_all " " code
            if (cmt != "") { cmt_line[++nc] = cmt; cmt_no[nc] = NR }
        }
        END {
            n = split(code_all, _c, /[^0-9]+/)
            for (i = 1; i <= n; i++) if (_c[i] != "") code_num[_c[i]] = 1
            for (j = 1; j <= nc; j++) {
                delete cand
                collect(cmt_line[j], cand)
                for (t in cand) {
                    print "NUM " t
                    if (!(t in code_num)) print "ORPHAN " t ":" cmt_no[j]
                }
            }
        }
    ' "$1"
}

sweep() {
    local files num_total=0 orphans=0 scanned=0
    files="$(_list_sh "$ORPHCONST_ROOTS" | sort -u)"
    if [ -z "$files" ]; then
        echo "orphaned-constant-gate: REFUSE — enumerated zero .sh files under {$ORPHCONST_ROOTS}."
        echo "  A scan that reads no source is a broken query, not a clean result."
        return $E_REFUSE
    fi

    echo "=== orphaned-constant gate: does every computed cadence/cap number in a comment exist in code? ==="
    echo ""

    local f line n
    while IFS= read -r f; do
        [ -f "$f" ] || continue
        local hit=0
        while IFS= read -r line; do
            case "$line" in
                "NUM "*)    num_total=$((num_total + 1)); hit=1 ;;
                "ORPHAN "*) n="${line#ORPHAN }"
                    echo "  ✗ FAIL  $f: comment asserts computed value '${n%%:*}' (line ${n##*:}) — it appears in NO code line of this file."
                    echo "          A parameterising refactor left the prose stating a cadence/cap the code no longer contains."
                    orphans=$((orphans + 1)) ;;
            esac
        done < <(_scan "$f")
        [ "$hit" -eq 1 ] && scanned=$((scanned + 1))
    done <<< "$files"

    echo ""
    echo "files-with-cadence-numbers=$scanned cadence-numbers-evaluated=$num_total orphans=$orphans"

    if [ "$num_total" -lt "$ORPHCONST_MIN_NUMS" ]; then
        echo "orphaned-constant-gate: REFUSE — evaluated $num_total cadence-numbers (floor=$ORPHCONST_MIN_NUMS)."
        echo "  An enumeration this thin means the parser saw no computed comment numerics; that is not a pass."
        return $E_REFUSE
    fi
    if [ "$orphans" -gt 0 ]; then
        echo "orphaned-constant-gate: FAIL — a comment asserts a computed cadence/cap the code no longer contains."
        return $E_FAIL
    fi
    echo "orphaned-constant-gate: PASS — every computed cadence/cap number in a comment appears in its file's code."
    return $E_PASS
}

# ── self-test: NON-VACUOUS. Proven against the REAL historical ghost via git-history blobs, plus ─────
# hermetic synthetic arms. All mirror-safe (blobs read via git, not a live path).
self_test() {
    local tmp bad=0
    tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' RETURN
    orphans_in() { _scan "$1" | grep -c '^ORPHAN ' || true; }

    # ARM 1 (SAFETY, the REAL ghost): pre-repair poll-opcom.sh — comment "7200 * 1s / 7200 ticks", code
    # holds 86400 — must FIRE on 7200. Recovered from git history (the fixture the bead names).
    local GHOST_SHA="8da986c85^" GHOST_PATH=".claude/hooks/poll-opcom.sh"
    if git cat-file -e "$GHOST_SHA:$GHOST_PATH" 2>/dev/null; then
        git show "$GHOST_SHA:$GHOST_PATH" > "$tmp/ghost.sh" 2>/dev/null
        if _scan "$tmp/ghost.sh" | grep -q '^ORPHAN 7200:'; then
            echo "  ok  ARM1 real-ghost  — pre-repair poll-opcom.sh: '7200 ticks' absent from code -> FIRES"
        else
            echo "self-test ARM1 FAIL: the real historical ghost (7200 in comment, 86400 in code) was NOT detected." >&2; bad=1
        fi
    else
        echo "  --  ARM1 real-ghost  SKIPPED (history blob unreachable — shallow/mirror); synthetic arms cover the class"
    fi

    # ARM 2 (LIVENESS, the REPAIR + the prose-noise proof): current poll-opcom.sh — the cap comment now
    # states no number, and the file is FULL of legitimate prose numbers (~570 pollers, +125 lines, a
    # sprint number) plus its own in-code cadence ticks. Must be SILENT. This is the AC3 + AC4 arm.
    if git cat-file -e "HEAD:$GHOST_PATH" 2>/dev/null; then
        git show "HEAD:$GHOST_PATH" > "$tmp/fixed.sh" 2>/dev/null
        if [ "$(orphans_in "$tmp/fixed.sh")" -eq 0 ]; then
            echo "  ok  ARM2 repair+noise — current poll-opcom.sh (prose numbers + in-code cadence) -> SILENT"
        else
            echo "self-test ARM2 FAIL: the repaired poll-opcom.sh was flagged (false positive on legitimate prose)." >&2
            _scan "$tmp/fixed.sh" | grep '^ORPHAN ' >&2; bad=1
        fi
    else
        echo "  --  ARM2 repair+noise SKIPPED (HEAD blob unreachable)"
    fi

    # ARM 3 (SAFETY, synthetic): an orphaned computed cap must FIRE, in both the `N *` and `N ticks` forms.
    printf '# hard cap: 7200 * 1s under SSE; 7200 ticks otherwise\nTIMEOUT=86400\n' > "$tmp/a.sh"
    if _scan "$tmp/a.sh" | grep -q '^ORPHAN 7200:'; then
        echo "  ok  ARM3 synth-fire  — '7200 * 1s' / '7200 ticks' with code holding 86400 -> FIRES"
    else echo "self-test ARM3 FAIL: synthetic computed orphan '7200' not detected." >&2; bad=1; fi

    # ARM 4 (LIVENESS, in-code cadence): a computed number that IS in the code must be SILENT.
    printf '# log every 300 ticks\nINTERVAL=300\n' > "$tmp/b.sh"
    if [ "$(orphans_in "$tmp/b.sh")" -eq 0 ]; then
        echo "  ok  ARM4 in-code      — computed cadence present in code -> SILENT (no false positive)"
    else echo "self-test ARM4 FAIL: an in-code cadence number was flagged." >&2; bad=1; fi

    # ARM 5 (AC4 EXCLUSIONS): a prose number NOT in a computation context, a year even inside `*`, a
    # sub-3-digit cadence, and a version — all SILENT even though none appears in the code. (Tokens are
    # deliberately generic, not real work-item ids, so this fixture stays mirror-clean.)
    cat > "$tmp/x.sh" <<'EOF'
# Observed ~570 live workers; +125 lines changed; a batch of 882 items; version 1.1.0; an 8-bit field.
# A window of 2026 * 1 (a year in an expression) and a 10*30s tick cadence.
NOOP=1
EOF
    if [ "$(orphans_in "$tmp/x.sh")" -eq 0 ]; then
        echo "  ok  ARM5 ac4-bounds  — prose count/line-delta/sprint/version/year-in-expr/sub-3-digit all excluded"
    else
        echo "self-test ARM5 FAIL: an AC4-excluded token was flagged (the gate is noisy)." >&2
        _scan "$tmp/x.sh" | grep '^ORPHAN ' >&2; bad=1
    fi

    # ARM 6 (per-value): a file with one in-code + one orphaned computed number fires on the orphan only.
    printf '# caps: 7200 ticks then 86400 ticks\nX=86400\n' > "$tmp/m.sh"
    if _scan "$tmp/m.sh" | grep -q '^ORPHAN 7200:' && ! _scan "$tmp/m.sh" | grep -q '^ORPHAN 86400:'; then
        echo "  ok  ARM6 per-value    — orphan '7200 ticks' fires while in-code '86400 ticks' stays silent"
    else echo "self-test ARM6 FAIL: per-value discrimination wrong." >&2; bad=1; fi

    # ARM 7 (end-to-end env-root sweep): defect tree -> FAIL(1); clean tree -> PASS(0); empty -> REFUSE(2).
    local e2e="$tmp/e2e"; mkdir -p "$e2e/bad" "$e2e/good" "$e2e/empty"
    cp "$tmp/a.sh" "$e2e/bad/a.sh"; cp "$tmp/b.sh" "$e2e/good/b.sh"
    local rc_bad rc_good rc_empty
    ( ORPHCONST_ROOTS="$e2e/bad"   ORPHCONST_MIN_NUMS=1 sweep ) >/dev/null 2>&1; rc_bad=$?
    ( ORPHCONST_ROOTS="$e2e/good"  ORPHCONST_MIN_NUMS=1 sweep ) >/dev/null 2>&1; rc_good=$?
    ( ORPHCONST_ROOTS="$e2e/empty" ORPHCONST_MIN_NUMS=1 sweep ) >/dev/null 2>&1; rc_empty=$?
    if [ "$rc_bad" -ne "$E_FAIL" ]; then
        echo "self-test ARM7 FAIL: e2e sweep did not FAIL(1) on a planted orphan (got rc=$rc_bad)." >&2; bad=1
    elif [ "$rc_good" -ne "$E_PASS" ]; then
        echo "self-test ARM7 FAIL: e2e sweep did not PASS(0) on a clean tree (got rc=$rc_good)." >&2; bad=1
    elif [ "$rc_empty" -ne "$E_REFUSE" ]; then
        echo "self-test ARM7 FAIL: e2e sweep did not REFUSE(2) on an empty scan (got rc=$rc_empty)." >&2; bad=1
    else
        echo "  ok  ARM7 e2e         — env-root sweep: orphan=FAIL(1) clean=PASS(0) empty=REFUSE(2)"
    fi

    # ARM 8 (PRODUCTION-PATH non-vacuity): with no env override the gate must enumerate the REAL default
    # scope and evaluate computed cadence-numbers — else the seam is a fixture-only no-op against reality.
    # The default scope's cadence subject lives under .claude/hooks; where that is absent (shallow/mirror
    # clone), this arm self-skips (the synthetic arms carry the class), mirroring ARM1/ARM2.
    if [ -f ".claude/hooks/poll-opcom.sh" ]; then
        local prod
        prod="$( sweep 2>/dev/null | grep -oE 'cadence-numbers-evaluated=[0-9]+' | cut -d= -f2 )"
        if [ -n "$prod" ] && [ "$prod" -ge 1 ]; then
            echo "  ok  ARM8 prod-path   — real default-scope enumeration (no env) evaluates $prod cadence-numbers"
        else
            echo "self-test ARM8 FAIL: production-path enumeration evaluated ZERO cadence-numbers — fixture-only no-op." >&2; bad=1
        fi
    else
        echo "  --  ARM8 prod-path   SKIPPED (.claude/hooks absent — mirror/shallow clone); synthetic arms cover the class"
    fi

    if [ "$bad" -ne 0 ]; then
        echo "orphaned-constant-gate --self-test: FAILED (the gate is broken or vacuous)" >&2
        return $E_SELFTEST
    fi
    echo "orphaned-constant-gate --self-test: all arms pass (real-ghost + repair/noise + synth + ac4-bounds + per-value + e2e + prod-path)"
    return 0
}

case "${1:---sweep}" in
    --self-test) self_test; exit $? ;;
    --sweep)     sweep;     exit $? ;;
    -h|--help)   echo "usage: orphaned-constant-gate.sh [--sweep|--self-test]"; exit 0 ;;
    *)           echo "orphaned-constant-gate: unknown arg '$1'" >&2; exit $E_REFUSE ;;
esac
