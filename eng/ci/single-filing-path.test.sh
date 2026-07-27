#!/usr/bin/env sh
# single-filing-path.test.sh — `bd create` must be called from exactly one place.
#
# Two filing paths produced 678 duplicate-prone auto-beads. The single path is
# .claude/tools/beads/bd-file.sh; everything else must call it, not `bd create`.
#
# Non-vacuity: a planted `bd create` in a scanned file must make this fail.
set -u
ROOT="$(git rev-parse --show-toplevel 2>/dev/null || echo .)"
ALLOW=".claude/tools/beads/bd-file.sh"
SCAN=".claude/skills .claude/agents .claude/workers .claude/rules .claude/hooks AGENTS.md CLAUDE.md"

scan() {   # $1 = extra file to include (for the mutant arm), may be empty
    hits=""
    for f in $(cd "$ROOT" && grep -rl "bd create" $SCAN 2>/dev/null) ${1:-}; do
        case "$f" in
            "$ALLOW") continue ;;
            */beads-task-planner/*) continue ;;   # documents the underlying CLI, banner-marked
        esac
        # strip comments/quote-prefixed prose, then look for a real invocation
        n=$(sed -E 's/^[[:space:]]*[|>#].*$//' "$ROOT/$f" 2>/dev/null \
            | grep -cE '(^|[^[:alnum:]_`])bd create ')
        [ "${n:-0}" -gt 0 ] && hits="$hits $f"
    done
    printf '%s' "$hits"
}

FAIL=0
real="$(scan)"
if [ -n "$real" ]; then
    echo "  [FAIL] these call \`bd create\` directly instead of bd-file.sh:$real" >&2
    FAIL=1
else
    echo "  [PASS] no agent-facing file calls \`bd create\` directly"
fi

# non-vacuity: plant one and assert the same scan finds it.
#
# The cleanup MUST be a trap, not a line after the scan. A bare `rm` on the happy path is
# skipped whenever the script dies between the plant and the rm -- a timeout, a Ctrl-C, a
# `set -e` trip inside scan(). This leaked for seven hours: a live `bd create` mutant sat
# untracked in .claude/rules, the corpus that FORBIDS `bd create`, one `git add` from being
# committed as a rule. This lock now runs from the pre-commit hook, so an interrupted commit
# is no longer hypothetical.
#
# Register the trap BEFORE writing the file. A trap installed after the plant has the same
# hole, one line narrower.
TMP="$ROOT/.claude/rules/_single_filing_mutant.md"
trap 'rm -f "$TMP"' EXIT INT TERM HUP
printf 'bd create "planted" -t bug -p 1\n' > "$TMP"
mutant="$(scan)"
rm -f "$TMP"
if [ -n "$mutant" ]; then
    echo "  [PASS] a planted \`bd create\` is detected (the check is non-vacuous)"
else
    echo "  [FAIL] a planted \`bd create\` went undetected — this check proves nothing" >&2
    FAIL=1
fi
exit "$FAIL"
