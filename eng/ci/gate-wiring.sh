#!/usr/bin/env bash
# gate-wiring.sh — the STRUCTURAL meta-gate: every enforcement gate under eng/ci and eng/hooks must
# have a CALLER that actually runs it. A gate nobody invokes enforces nothing; it is an advertised
# control that is silently inert. This is the check that was described in a comment for months while
# two real gates sat with zero callers behind a passing battery.
#
# WHAT COUNTS AS A GATE (enumerated, not sampled):
#   every  eng/ci/*.{sh,py,ps1}  and  eng/hooks/*.sh  EXCEPT
#     *.test.{sh,py,ps1}     — a self-test proves a gate is non-vacuous; it does not WIRE the gate.
#     *.harness-lock.sh      — a lock, run by the orchestrator's lock battery, not a standalone gate.
#     *.fixture.{sh,py,ps1}  — test scaffolding.
#     harness-gates-ci.sh    — the orchestrator itself (the top caller, named directly by the workflow).
#
#   ON THE EXTENSION SET. This enumerated '*.sh' alone until it was measured: eng/ci held 46 .sh, 9 .py
#   and 27 .ps1 non-test gates, so 36 of 82 sat outside the population BY CONSTRUCTION and an unwired
#   .py or .ps1 gate could not be detected at all. Enumerating by file extension is a proxy for "is an
#   enforcement gate", and the proxy silently excluded the majority of them. Widening it found five real
#   orphans, one release-critical: a validator asserting the shipping filter names every packable
#   project, running nowhere, so a newly added package could have been built by nothing and shipped
#   never. If a gate is ever written in a fourth language, add it here — the population must follow what
#   a gate IS, not what it happens to be written in.
#
# WHAT COUNTS AS A CALLER (a surface that RUNS a gate):
#     .github/workflows/*.yml           — CI entry points
#     eng/hooks/pre-commit|pre-push|prepare-commit-msg|post-checkout|post-merge — git hooks
#     eng/ci/harness-gates-ci.sh        — the CI-authoritative orchestrator (invokes gates via a loop
#                                         over a list, so a gate's NAME in that list IS its wiring)
#     .claude/harness/*.harness-lock.sh — locks that invoke a bare gate
#   A gate's OWN *.test.sh / *.fixture.sh is NOT a caller of it — testing a gate is not running it in
#   production. That distinction is the whole point: a gate whose only reference is its own test is
#   still orphaned. Comment-only mentions (a line beginning with #) are not callers either.
#
# KNOWN-ORPHAN BASELINE (gate-wiring-baseline.txt): a committed ledger of gates that currently have no
# caller and are accepted as tracked debt. A NEW gate with no caller and no baseline entry FAILS this
# gate (exit 1) — that is the safety arm. Baselining is how the liveness arm is preserved: this check
# ships LIVE against pre-existing debt instead of redding the whole battery on day one and being switched
# off. The baseline is a SHRINK target — wiring a gate should delete its line. A baseline entry that is
# no longer an orphan (gate wired, or gate removed) is reported as STALE (a warning, not a failure, so
# that wiring a gate in a parallel change never reds CI on a race).
#
# ENV OVERRIDES (used by the self-test to run hermetically over a fixture tree):
#   GW_ROOT      — repo root to scan (default: this script's repo root)
#   GW_BASELINE  — baseline file (default: $GW_ROOT/eng/ci/gate-wiring-baseline.txt)
#
# Exit: 0 = every gate is wired or baselined | 1 = at least one UN-baselined orphan | 64 = environment error.

# OPEN QUESTION -- READ THIS BEFORE TRUSTING AN ORPHAN COUNT FROM A DIFFERENT MACHINE.
# On 2026-09-13 this gate reported 36 orphans (28 un-baselined) on the Linux CI runner and 9 orphans
# (1 un-baselined) in the development repository, at the same commit, with the same 118-gate
# population. That discrepancy is NOT EXPLAINED. Two hypotheses were tested and BOTH were refuted by
# measurement, so do not re-run them:
#
#   1. "the mirror is missing caller files."  Refuted: all 27 flipped gates are invoked on
#      non-comment lines of .github/workflows/**, eng/hooks/pre-commit or harness-gates-ci.sh, and
#      four of them are named by harness-gates-ci.sh -- the script that was EXECUTING this gate when
#      it reported them orphaned. Had those surfaces been absent, 43 FURTHER gates would also have
#      flipped, including this one.
#   2. "the pipefail/SIGPIPE race in is_wired."  Refuted: see the measurement at that function.
#      Replaying the old idiom over the real corpus produced zero SIGPIPEs.
#
# No third theory is offered here on purpose. What the run now does instead is STATE ITS OWN SCOPE --
# how many caller surfaces it found, bucketed, how many lines each corpus received, and which
# surfaces contributed nothing. Compare those numbers between the two machines before theorising:
# an orphan count published without its caller set is unfalsifiable, and working out after the fact
# what a past run had actually read is what made this expensive.

set -uo pipefail

GW_ROOT="${GW_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." 2>/dev/null && pwd)}"
[ -n "$GW_ROOT" ] && [ -d "$GW_ROOT" ] || { echo "gate-wiring: cannot locate repo root" >&2; exit 64; }
BASELINE="${GW_BASELINE:-$GW_ROOT/eng/ci/gate-wiring-baseline.txt}"

[ -d "$GW_ROOT/eng/ci" ] || { echo "::error::gate-wiring: no eng/ci under $GW_ROOT — cannot evaluate" >&2; exit 64; }

# ── Enumerate candidate gates (basenames) ──────────────────────────────────────────────────────────
gates=()
for f in "$GW_ROOT"/eng/ci/*.sh "$GW_ROOT"/eng/ci/*.py "$GW_ROOT"/eng/ci/*.ps1 "$GW_ROOT"/eng/hooks/*.sh; do
    [ -f "$f" ] || continue
    b="$(basename "$f")"
    case "$b" in
        *.test.sh|*.test.py|*.test.ps1) continue ;;
        *.fixture.sh|*.fixture.py|*.fixture.ps1) continue ;;
        *.harness-lock.sh|harness-gates-ci.sh) continue ;;
    esac
    gates+=("$b")
done

# ── Enumerate caller surfaces (files that RUN gates) ───────────────────────────────────────────────
# A GATE IS ITSELF A CALLER SURFACE. One gate composing another is a real invocation: the doc
# phantom gate computes a diff scope once and runs both the type check and the member check over
# it, so the member gate IS run on every pull request while being invoked by no workflow directly.
# Omitting gates here reported it as an unwired orphan while it was executing. This does not create
# a loophole: a gate whose only caller is itself an orphan still fails, because that caller is
# enumerated and checked on its own.
callers=()
for c in "$GW_ROOT"/.github/workflows/*.yml \
         "$GW_ROOT"/.github/actions/*/action.yml \
         "$GW_ROOT"/eng/hooks/pre-commit "$GW_ROOT"/eng/hooks/pre-push \
         "$GW_ROOT"/eng/hooks/prepare-commit-msg "$GW_ROOT"/eng/hooks/post-checkout \
         "$GW_ROOT"/eng/hooks/post-merge "$GW_ROOT"/eng/ci/harness-gates-ci.sh \
         "$GW_ROOT"/.claude/harness/*.harness-lock.sh \
         "$GW_ROOT"/.claude/skills/*/SKILL.md          "$GW_ROOT"/eng/ci/*-gate.sh ; do
    [ -f "$c" ] && callers+=("$c")
done

# SCOPE DISCLOSURE. The verdict below is only as wide as the caller set above, and which of those
# surfaces EXIST is environment-dependent -- .claude/** does not travel to the mirrored copy this
# runs against in CI. An orphan count published without its caller set is unfalsifiable, and working
# out after the fact which surfaces a past run actually read cost a whole investigation that one
# printed line would have ended. So the run states what it read, bucketed, every time.
n_wf=0; n_hook=0; n_orch=0; n_gate=0; n_lock=0; n_skill=0
for c in "${callers[@]:-}"; do
    case "$c" in
        */harness-gates-ci.sh)  n_orch=$((n_orch + 1)) ;;
        *.harness-lock.sh)      n_lock=$((n_lock + 1)) ;;
        */SKILL.md)             n_skill=$((n_skill + 1)) ;;
        *"/.github/"*)          n_wf=$((n_wf + 1)) ;;
        *"/eng/hooks/"*)        n_hook=$((n_hook + 1)) ;;
        *)                      n_gate=$((n_gate + 1)) ;;
    esac
done

# is_wired <gate-basename> — true if the gate's FULL FILENAME (its <name>.sh) appears as a whole token
# on a non-comment line of any caller. A real caller RUNS the gate by its .sh filename; matching the
# full ".sh" filename — not the bare stem — is what makes a self-test reference inert. A caller (e.g. the
# orchestrator's loop list) that names "<name>.test.sh" contains the token "<name>.test.sh", which is NOT
# the token "<name>.sh" — so a gate whose only mention is its own self-test stays ORPHAN, instead of being
# falsely counted as wired because the stem happened to appear before the ".test.sh" suffix. The
# whole-token boundary still stops "f5-sweep.sh" from matching inside "f5-sweep-extra.sh", and the ".sh"
# anchor additionally stops it from matching inside "f5-sweep.test.sh"/"f5-sweep.fixture.sh".
# PERFORMANCE. This used to grep every caller separately for every gate — O(gates x callers) greps,
# which crossed two minutes once the gate population nearly doubled. The caller surfaces are identical
# for every gate, so they are flattened ONCE into two corpora and each gate costs two greps instead of
# one per caller (measured: >120s -> ~14s). Semantics are preserved exactly: whole-line comments are
# stripped from the code corpus up front, which is what the old per-file post-filter did.
CORPUS_DIR="$(mktemp -d 2>/dev/null)" || { echo "::error::gate-wiring: cannot create temp dir" >&2; exit 64; }
trap 'rm -rf "$CORPUS_DIR"' EXIT
CODE_CORPUS="$CORPUS_DIR/code"; MD_CORPUS="$CORPUS_DIR/md"
: >"$CODE_CORPUS"; : >"$MD_CORPUS"
# PROVENANCE PREFIX: every code-corpus line is tagged with the basename of the file it came from,
# because a gate MUST NOT be able to wire itself. eng/ci/*-gate.sh is part of the caller set (gates
# legitimately invoke other gates), which means a gate's own file is searched for its own name — and a
# routine `--help` line such as `echo "usage: my-gate.sh [--sweep]"` is code, not a comment, so it
# satisfied the token search and the gate reported WIRED while nothing invoked it. Measured: two gates
# were wired only by themselves, and for one of them this meta-gate went on to recommend deleting its
# baseline entry as "now wired" — advice that would have converted a tracked orphan into an invisible
# one. The tag lets is_wired ignore hits whose only source is the gate itself, at no extra pass.
# COUNTED IS NOT READ. Both appends below swallow their errors (2>/dev/null), so a caller that
# contributes NOTHING -- unreadable, or emptied by the comment strip -- is indistinguishable from one
# that was read, and a partially-built corpus produces exactly the shape of a missing-wiring bug. The
# surface count alone cannot see that, so measure what each caller actually CONTRIBUTED.
# ZERO ADDED PROCESSES. The first version of this check measured the corpus with `wc -l` before and
# after each caller -- 372 extra spawns, which cost 283 SECONDS on a Windows/MSYS box where an
# antivirus inspects every process creation. Latency is paid by every run, by everyone, forever
# (when-to-create-a-gate.md), so the emptiness test is done in the shell instead: capture what the
# caller contributes, test it, then append. Same spawn count as the plain append it replaced.
n_empty=0; empty_list=""
for cf in "${callers[@]:-}"; do
    [ -n "$cf" ] || continue
    _contrib=""
    case "$cf" in
        *.md) _contrib="$(<"$cf")"
              [ -n "$_contrib" ] && printf '%s\n' "$_contrib" >>"$MD_CORPUS" ;;
        *)    _contrib="$(grep -vE '^[[:space:]]*#' "$cf" | sed "s|^|$(basename "$cf")\||")"
              [ -n "$_contrib" ] && printf '%s\n' "$_contrib" >>"$CODE_CORPUS" ;;
    esac
    if [ -z "$_contrib" ]; then
        n_empty=$((n_empty + 1))
        empty_list="$empty_list ${cf#"$GW_ROOT"/}"
    fi
done

# A skill IS a caller surface: the gates for the LOCAL runners (a full-suite shard completeness check,
# a build-skip assert) are invoked from the procedure that runs them, not from a workflow — so omitting
# skills reported those gates as orphans while they were being invoked on every run. Prose is not
# invocation, though: in markdown the mention must be shaped like a command, for the same reason a
# comment does not wire a gate.
is_wired() {
    local esc="${1//./\\.}"   # escape '.' so the extension is matched literally, not as any-char
    # A hit counts only if it came from a file OTHER than the gate itself (see the provenance note
    # above): match the token, then drop the lines tagged with this gate's own basename. If anything
    # survives, a real caller names it.
    #
    # NEVER `| grep -q` HERE, and the reason is a measured hazard rather than style.
    # This was `grep -E ... | grep -qvE ... && return 0`. Under the `set -o pipefail` at the top of
    # this file that idiom can report a FALSE ORPHAN for a gate that genuinely matched: `grep -q`
    # exits the instant it sees its first line and closes the pipe; if the upstream `grep -E` still
    # has matches to write it dies of SIGPIPE (141); pipefail promotes 141 to the PIPELINE's status,
    # the `&&` never fires, and a WIRED gate is reported as having no caller.
    #
    # SCOPE OF THAT CLAIM, measured 2026-09-13 so nobody inherits more than was shown. The hazard is
    # real and, past the pipe buffer, CERTAIN -- not a rare race:
    #     3-line payload        SIGPIPE in   0 / 300 trials
    #     100000-line payload   SIGPIPE in 300 / 300 trials
    # But replaying the OLD idiom over THIS gate's real corpus (17,445 lines, 118 gates) produced
    # ZERO SIGPIPEs: a real gate filename matches only a handful of lines, so the upstream grep has
    # nothing left to write when the reader goes away. So this is a LATENT defect that a
    # high-match token would trip, NOT the explanation for any orphan count observed to date.
    # Do not cite it as one.
    #
    # Collect into a variable instead: both greps read to EOF, nobody closes a pipe early, no SIGPIPE
    # is possible, and the verdict is a function of the corpus rather than of scheduling. Do not
    # "optimise" this back with `head -n1` or `grep -m1` -- either re-introduces the early close.
    local hits
    hits="$(grep -E "(^|[^A-Za-z0-9_|-])${esc}([^A-Za-z0-9_-]|\$)" "$CODE_CORPUS" 2>/dev/null \
            | grep -vE "^${esc}\|")"
    [ -n "$hits" ] && return 0
    grep -qE "(^|[^A-Za-z0-9_-])(bash|sh|pwsh|python3?|\./)[[:space:]]*[^[:space:]]*${esc}([^A-Za-z0-9_-]|\$)" "$MD_CORPUS" 2>/dev/null && return 0
    return 1
}

# ── Load the baseline (non-comment, non-blank basenames) ───────────────────────────────────────────
baseline=()
if [ -f "$BASELINE" ]; then
    while IFS= read -r line; do
        line="${line%%#*}"; line="$(printf '%s' "$line" | tr -d '[:space:]')"
        [ -n "$line" ] && baseline+=("$line")
    done < "$BASELINE"
fi
in_baseline() { local x="$1" e; for e in "${baseline[@]:-}"; do [ "$e" = "$x" ] && return 0; done; return 1; }

# ── Compute orphans ────────────────────────────────────────────────────────────────────────────────
orphans=()
for g in "${gates[@]:-}"; do
    [ -n "$g" ] || continue
    is_wired "$g" || orphans+=("$g")
done

# New (un-baselined) orphans → failure.  Baselined orphans → tracked debt (reported, not fatal).
new_orphans=()
for o in "${orphans[@]:-}"; do
    [ -n "$o" ] || continue
    in_baseline "$o" || new_orphans+=("$o")
done

# Stale baseline entries: listed as an accepted orphan but no longer an orphan (wired or gate gone).
is_orphan() { local x="$1" e; for e in "${orphans[@]:-}"; do [ "$e" = "$x" ] && return 0; done; return 1; }
stale=()
for e in "${baseline[@]:-}"; do
    [ -n "$e" ] || continue
    is_orphan "$e" || stale+=("$e")
done

echo "gate-wiring: ${#gates[@]} gate(s) enumerated, ${#orphans[@]} orphan(s), ${#baseline[@]} baselined."
echo "gate-wiring: caller scope -- ${#callers[@]} surface(s): ${n_wf} workflow/action, ${n_hook} git hook," \
     "${n_orch} orchestrator, ${n_gate} gate, ${n_lock} harness lock, ${n_skill} skill."
# The two corpora are reported SEPARATELY on purpose: the skill surfaces feed the markdown corpus and
# contribute nothing to the code corpus, so one combined number invites dividing a code-line count by a
# surface count that includes them.
echo "gate-wiring: corpus -- code $(wc -l <"$CODE_CORPUS") line(s) from $((${#callers[@]} - n_skill)) surface(s)," \
     "markdown $(wc -l <"$MD_CORPUS") line(s) from ${n_skill}; ${n_empty} surface(s) contributed NOTHING."
if [ "$n_empty" -gt 0 ]; then
    echo "gate-wiring: NOTE -- these caller surfaces were counted but contributed no corpus lines:"
    for e in $empty_list; do echo "   (empty) $e"; done
fi
[ "$n_orch" -eq 0 ] && echo "gate-wiring: NOTE -- harness-gates-ci.sh was NOT among the callers; a gate wired only there reads as an orphan."
[ "$n_wf" -eq 0 ] && echo "gate-wiring: NOTE -- no workflow/action surfaces were found; a gate wired only in CI reads as an orphan."

if [ "${#stale[@]}" -gt 0 ]; then
    echo "gate-wiring: NOTE — ${#stale[@]} stale baseline entry(ies) (now wired or removed); delete from ${BASELINE##*/}:"
    for s in "${stale[@]}"; do echo "   - $s"; done
fi

if [ "${#new_orphans[@]}" -gt 0 ]; then
    echo "::error::gate-wiring: ${#new_orphans[@]} gate(s) have NO caller and are NOT baselined — a gate that nothing invokes enforces nothing:" >&2
    for o in "${new_orphans[@]}"; do echo "   ✗ $o" >&2; done
    echo "   Fix: wire the gate into a caller (a workflow, a git hook, or harness-gates-ci.sh), OR — if" >&2
    echo "        it is accepted tracked debt — add its basename to ${BASELINE##*/}." >&2
    exit 1
fi

echo "gate-wiring: every enumerated gate is wired or baselined."
exit 0
