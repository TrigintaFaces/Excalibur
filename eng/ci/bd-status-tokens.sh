#!/usr/bin/env bash
# bd-status-tokens.sh -- an unrecognized `--status` token is a silent, cheerful lie.
#
# (These four lines document the very tokens this gate rejects, so each opts out explicitly.
#  The fix for a self-referential scanner hit belongs at the AUTHORING SITE -- never in a
#  weakened scanner. A gate relaxed to let its own docs through stops catching the real thing.)
#
#   bd list --status open              -> 390 issues                                  # bd-status-ok
#   bd list --status banana            -> exit 0, ZERO issues                         # bd-status-ok
#   bd list --status in-progress       -> exit 0, ZERO issues  <- obvious spelling    # bd-status-ok
#   bd list --status open,in_progress  -> exit 0, ZERO issues  <- the natural way      # bd-status-ok
#   bd list           (no filter)      -> 9203 issues
#
# bd accepts the token, matches nothing, and reports success. Zero results is
# indistinguishable from "nothing matched", so a gate, hook, or report built on a bad
# token passes FOREVER -- and its self-test passes too, because a self-test that plants
# nothing finds nothing.
#
# `in-progress` is the lethal one: it is the obvious spelling of the status, it is wrong,
# and it means "no beads" with a success exit. A gate built on it is blind and cheerful.
#
# We cannot patch bd (third-party binary). So we make the bad token unwriteable HERE:
# any tracked file containing an unrecognized --status token fails this gate.
#
# The valid set is bd's OWN declaration, `bd list --help`:
#     Filter by status (open, in_progress, blocked, closed)
# Not a query result -- a query for a bad token returns zero, and a query for a good one
# flaps. Never enumerate a valid set by asking the flapping instrument.
#
# Exit: 0 clean | 1 a bad token is committed | 64 broken enumeration (never a silent pass).

set -uo pipefail

VALID="open in_progress blocked closed"

ROOT="${BD_STATUS_ROOT:-.}"
# Scan roots. A bad token in an agent spec is as harmful as one in a shell script: the
# agent runs the command verbatim and gets a confident empty answer.
SCAN="${BD_STATUS_SCAN:-eng .claude scripts .github}"

cd "$ROOT" 2>/dev/null || { echo "bd-status-tokens: no such root: $ROOT" >&2; exit 64; }

# A line may be a comment that DOCUMENTS the trap (blocking-bead-gate.sh explains it, and
# so does this file). Those must not fail the gate, or we cannot write down the lesson.
# Opt out explicitly and visibly, per line.
ALLOW='bd-status-ok'

tmp="$(mktemp)"; trap 'rm -f "$tmp"' EXIT

found=0
for d in $SCAN; do
    [ -e "$d" ] || continue
    # -I: skip binaries. Strip CR: a trailing \r turns "open" into "open\r", an unrecognized
    # token, and would make this gate report a violation that is only a line ending.
    #
    # Require `bd` on the line. Without it the pattern matches PROSE -- "an unrecognized
    # --status token" scores a violation named `token`. A gate that cannot tell a command
    # from a sentence about a command reports noise, and a noisy gate gets switched off.
    grep -rInE -- 'bd[^|]*--status[= ]+[^ ]+' "$d" 2>/dev/null | tr -d '\r' >> "$tmp" || true
done

# An empty scan is a broken instrument, not a clean repo. The whole point of this gate is
# that "found nothing" is the failure mode we are policing; it must not be our own verdict.
if [ ! -s "$tmp" ]; then
    echo "bd-status-tokens: found ZERO '--status' occurrences under {$SCAN}." >&2
    echo "                  A repo that uses bd has some. Refusing to report clean." >&2
    exit 64
fi

echo "bd-status-tokens: valid = {$VALID}   (source: bd list --help)"
echo

while IFS= read -r line; do
    case "$line" in *"$ALLOW"*) continue ;; esac

    # Extract with builtins. A `sed` fork per matched line is ~4000 processes on this repo;
    # on Windows that is minutes, and a gate slow enough to time out is a gate nobody runs.
    rest="${line#*--status}"
    rest="${rest#[= ]}"; rest="${rest#[= ]}"
    tok="${rest%% *}"
    tok="${tok#[\"\'\`]}"; tok="${tok%%[\"\'\`]*}"
    [ -n "$tok" ] || continue
    # A shell/template variable is resolved at runtime; we cannot judge it here.
    case "$tok" in '$'*|'<'*|'${'*|'%'*|'&'*) continue ;; esac

    ok=0
    for v in $VALID; do [ "$tok" = "$v" ] && ok=1 && break; done
    if [ "$ok" -eq 0 ]; then
        loc="$(printf '%s' "$line" | cut -d: -f1,2)"
        printf '  BAD  %-16s %s\n' "$tok" "$loc"
        found=$((found + 1))
    fi
done < "$tmp"

echo
if [ "$found" -gt 0 ]; then
    echo "FAIL: $found unrecognized --status token(s)." >&2
    echo "      bd accepts these and returns ZERO with exit 0. Every one is silently blind." >&2
    echo "      Valid: $VALID   For multi-status, use 'bd list --json' with NO filter + filter client-side." >&2  # bd-status-ok
    echo "      To document the trap on purpose, put '$ALLOW' on the line." >&2
    exit 1
fi

echo "bd-status-tokens: every --status token is one bd recognizes."  # bd-status-ok
