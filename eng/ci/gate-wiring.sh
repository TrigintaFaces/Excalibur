#!/usr/bin/env bash
# gate-wiring.sh — the STRUCTURAL meta-gate: every enforcement gate under eng/ci and eng/hooks must
# have a CALLER that actually runs it. A gate nobody invokes enforces nothing; it is an advertised
# control that is silently inert. This is the check that was described in a comment for months while
# two real gates sat with zero callers behind a passing battery.
#
# WHAT COUNTS AS A GATE (enumerated, not sampled):
#   every  eng/ci/*.sh  and  eng/hooks/*.sh  EXCEPT
#     *.test.sh          — a self-test proves a gate is non-vacuous; it does not WIRE the gate.
#     *.harness-lock.sh  — a lock, run by the orchestrator's lock battery, not a standalone gate.
#     *.fixture.sh       — test scaffolding.
#     harness-gates-ci.sh— the orchestrator itself (it is the top caller, named directly by the workflow).
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

set -uo pipefail

GW_ROOT="${GW_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." 2>/dev/null && pwd)}"
[ -n "$GW_ROOT" ] && [ -d "$GW_ROOT" ] || { echo "gate-wiring: cannot locate repo root" >&2; exit 64; }
BASELINE="${GW_BASELINE:-$GW_ROOT/eng/ci/gate-wiring-baseline.txt}"

[ -d "$GW_ROOT/eng/ci" ] || { echo "::error::gate-wiring: no eng/ci under $GW_ROOT — cannot evaluate" >&2; exit 64; }

# ── Enumerate candidate gates (basenames) ──────────────────────────────────────────────────────────
gates=()
for f in "$GW_ROOT"/eng/ci/*.sh "$GW_ROOT"/eng/hooks/*.sh; do
    [ -f "$f" ] || continue
    b="$(basename "$f")"
    case "$b" in
        *.test.sh|*.harness-lock.sh|*.fixture.sh|harness-gates-ci.sh) continue ;;
    esac
    gates+=("$b")
done

# ── Enumerate caller surfaces (files that RUN gates) ───────────────────────────────────────────────
callers=()
for c in "$GW_ROOT"/.github/workflows/*.yml \
         "$GW_ROOT"/.github/actions/*/action.yml \
         "$GW_ROOT"/eng/hooks/pre-commit "$GW_ROOT"/eng/hooks/pre-push \
         "$GW_ROOT"/eng/hooks/prepare-commit-msg "$GW_ROOT"/eng/hooks/post-checkout \
         "$GW_ROOT"/eng/hooks/post-merge "$GW_ROOT"/eng/ci/harness-gates-ci.sh \
         "$GW_ROOT"/.claude/harness/*.harness-lock.sh \
         "$GW_ROOT"/.claude/skills/*/SKILL.md ; do
    [ -f "$c" ] && callers+=("$c")
done

# is_wired <gate-basename> — true if the gate's FULL FILENAME (its <name>.sh) appears as a whole token
# on a non-comment line of any caller. A real caller RUNS the gate by its .sh filename; matching the
# full ".sh" filename — not the bare stem — is what makes a self-test reference inert. A caller (e.g. the
# orchestrator's loop list) that names "<name>.test.sh" contains the token "<name>.test.sh", which is NOT
# the token "<name>.sh" — so a gate whose only mention is its own self-test stays ORPHAN, instead of being
# falsely counted as wired because the stem happened to appear before the ".test.sh" suffix. The
# whole-token boundary still stops "f5-sweep.sh" from matching inside "f5-sweep-extra.sh", and the ".sh"
# anchor additionally stops it from matching inside "f5-sweep.test.sh"/"f5-sweep.fixture.sh".
is_wired() {
    local b="$1" cf esc="${1//./\\.}"   # escape '.' so the extension is matched literally, not as any-char
    local re="(^|[^A-Za-z0-9_-])${esc}([^A-Za-z0-9_-]|\$)"
    for cf in "${callers[@]:-}"; do
        [ -n "$cf" ] || continue
        case "$cf" in
            *.md)
                # A skill IS a caller surface: the gates for the LOCAL runners (a full-suite shard
                # completeness check, a build-skip assert) are invoked from the procedure that runs
                # them, not from a workflow — so omitting skills reported those gates as orphans
                # while they were being invoked on every run.
                #
                # Prose is not invocation, though. In markdown, require the mention to be shaped like
                # a command: `bash <gate>`, `pwsh <gate>`, `./<gate>`, or `sh <gate>`. A sentence that
                # merely names the gate does not wire it, for the same reason a comment does not.
                if grep -qE "(^|[^A-Za-z0-9_-])(bash|sh|pwsh|\./)[[:space:]]*[^[:space:]]*${esc}([^A-Za-z0-9_-]|\$)" "$cf" 2>/dev/null; then
                    return 0
                fi
                ;;
            *)
                if grep -nE "$re" "$cf" 2>/dev/null | grep -qvE '^[0-9]+:[[:space:]]*#'; then
                    return 0
                fi
                ;;
        esac
    done
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
