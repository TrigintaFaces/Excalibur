#!/usr/bin/env bash
# shard-hang-timeout-gate.sh — every shard invocation MUST carry a hang timeout.
#
# WHY THIS EXISTS
#   A test host can WEDGE — not deadlock, SPIN. Measured: an xUnit-v3 + MTP host burned 1011.5 s of CPU
#   over ~45 min wall while producing zero output. Our GitHub workflows survive that because they pass
#   `--blame-hang-timeout` (plus RunConfiguration.TestSessionTimeout), which converts a wedge into a
#   REPORTED FAILURE. An ad-hoc `dotnet test <shard>.slnf` with neither blocks INDEFINITELY and silently
#   consumes an entire phase — the runner never fails, it just never returns.
#
#   The second-order damage is worse than the stall: the orphaned host survives killing the vstest/dotnet
#   chain and keeps an exclusive handle on the test project's output DLLs, so every SUBSEQUENT build of
#   that project dies with MSB3021 "being used by another process" — a BUILD BREAK wearing a
#   test-failure exit code, in a LATER shard than the one that leaked it.
#
#   A convention telling runners to pass the flag is not a control; prose has no exit code. This is the
#   exit code.
#
# WHAT IT REQUIRES
#   Any invocation that runs `dotnet test` against a `.slnf` shard must also pass `--blame-hang-timeout`.
#   Scope is deliberately EVERY producer of such an invocation, not just the workflows: the wedge that
#   motivated this was in a LOCAL runner, and the workflows were already compliant.
#
# WHAT IT DOES NOT REQUIRE
#   `RunConfiguration.TestSessionTimeout` — it is a belt-and-braces second bound, but a session timeout
#   alone does not produce the per-hang dump, and requiring both would fail truthful minimal runners.
#   The load-bearing property is "a wedge terminates", and --blame-hang-timeout supplies it.
#
# OPT-OUT (per line, deliberate and visible):  # pragma: no-hang-timeout <reason>
#
# Exit: 0 = every shard invocation is bounded · 1 = an unbounded invocation found · 2 = cannot evaluate
#
# Overridable for the self-test:  SCAN_ROOT (default: tracked runner/workflow/skill files)
#
# Usage: eng/ci/shard-hang-timeout-gate.sh [--self-test]

set -uo pipefail

readonly E_OK=0
readonly E_FOUND=1
readonly E_ENV=2

if [ "${1:-}" = "--self-test" ]; then
    exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/shard-hang-timeout-gate.test.sh"
fi

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || true)"
[ -n "$REPO_ROOT" ] || { echo "[shard-hang-timeout-gate] CANNOT EVALUATE — not in a git repo." >&2; exit "$E_ENV"; }

SELF="$(basename "${BASH_SOURCE[0]}")"

if [ -n "${SCAN_ROOT:-}" ]; then
    mapfile -t FILES < <(find "$SCAN_ROOT" -type f \( -name '*.sh' -o -name '*.ps1' -o -name '*.yml' -o -name '*.yaml' -o -name '*.md' \) 2>/dev/null \
        | grep -vE "(/${SELF}$|\.test\.sh$)" | LC_ALL=C sort)
else
    # Pre-filter with `git grep -l`: only files that contain the literal `dotnet test` can possibly hold
    # an invocation, so scanning the rest is pure cost (541 files -> a handful; 23s -> under a second).
    # This narrows the FILE set, never the RULE — any file the rule could fire on still reaches the loop.
    mapfile -t FILES < <(cd "$REPO_ROOT" && git grep -l --fixed-strings 'dotnet test' -- \
        'eng/*' '.github/workflows/*' '.claude/skills/*' 'scripts/*' 'docs/*' 2>/dev/null \
        | grep -E '\.(sh|ps1|yml|yaml|md)$' \
        | grep -vE "(/${SELF}$|\.test\.sh$)" | LC_ALL=C sort)
fi

# A single invocation may span lines via a trailing `\` (sh/markdown) or a trailing backtick (PowerShell),
# or a YAML block scalar's indentation. Collapse continuations so the flag is seen on the SAME logical
# line as the `dotnet test` that must carry it — otherwise a correctly-bounded multi-line invocation
# reads as a violation (a false positive that would train people to disable this gate).
collapse_continuations() {
    sed -e ':a' -e '/[\\`]$/{N;s/[\\`]\n[[:space:]]*/ /;ba' -e '}' "$1" 2>/dev/null
}

found=0
scanned=0
for f in "${FILES[@]}"; do
    [ -f "$f" ] || { if [ -f "$REPO_ROOT/$f" ]; then f="$REPO_ROOT/$f"; else continue; fi; }
    scanned=$((scanned + 1))

    while IFS= read -r line; do
        # Only invocations that actually run a shard.
        case "$line" in
            *"dotnet test"*".slnf"*) ;;
            *) continue ;;
        esac
        # PROSE vs COMMAND. A sentence that MENTIONS `dotnet test` and `.slnf` ("never run dotnet test at
        # solution root — always shard .slnf files") is not an invocation, and flagging it is a false
        # positive that teaches people to disable the gate. A command is a line whose FIRST token is
        # `dotnet test`, once decoration is stripped: markdown table cells (|), code fences/inline
        # backticks, list bullets, quote markers, shell prompts, comment leaders, and YAML `- run:`.
# A markdown table cell or an inline-code span puts a REAL command mid-line, so the check runs
        # per SEGMENT (split on | and `), not per line. A segment counts as an invocation only when it
        # BEGINS with `dotnet test` and names a shard that has an actual stem — `foo/Bar.slnf`, never the
        # bare `.slnf` that appears in prose like "a local `dotnet test .slnf` had no hang timeout".
        is_command=0
        while IFS= read -r seg; do
            seg="$(printf '%s' "$seg" \
                | sed -e 's/^[[:space:]]*//' \
                      -e 's/^[|>#*-][[:space:]]*//' \
                      -e 's/^[[:space:]]*//' \
                      -e 's/^run:[[:space:]]*//' \
                      -e 's/^\$[[:space:]]*//' \
                      -e 's/^[[:space:]]*//')"
            case "$seg" in
                "dotnet test"*) ;;
                *) continue ;;
            esac
            if printf '%s' "$seg" | grep -qE '[A-Za-z0-9_-][A-Za-z0-9_./-]*\.slnf'; then
                is_command=1
                break
            fi
        done < <(printf '%s\n' "$line" | tr '|`' '\n\n')
        [ "$is_command" -eq 1 ] || continue
        # Commented-out prose/examples in markdown prose lines still count: a documented command is a
        # command someone will paste. The only exemption is an explicit, reasoned pragma.
        case "$line" in
            *"pragma: no-hang-timeout"*) continue ;;
        esac
        case "$line" in
            *"--blame-hang-timeout"*) continue ;;
        esac
        echo "[shard-hang-timeout-gate] UNBOUNDED SHARD INVOCATION: ${f#"$REPO_ROOT"/}" >&2
        echo "    $(echo "$line" | sed -e 's/^[[:space:]]*//' | cut -c1-160)" >&2
        found=1
    done < <(collapse_continuations "$f")
done

if [ "$scanned" -eq 0 ]; then
    echo "[shard-hang-timeout-gate] CANNOT EVALUATE — scanned 0 files (bad SCAN_ROOT or empty index)." >&2
    exit "$E_ENV"
fi

if [ "$found" -ne 0 ]; then
    echo "[shard-hang-timeout-gate] FAIL — add --blame-hang-timeout to the invocation(s) above." >&2
    echo "                          A shard without one does not fail on a wedge; it hangs forever." >&2
    exit "$E_FOUND"
fi

echo "[shard-hang-timeout-gate] PASS — $scanned file(s) scanned, every shard invocation is bounded."
exit "$E_OK"
