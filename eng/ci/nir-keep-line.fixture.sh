#!/usr/bin/env bash
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
#
# nir-keep-line.fixture.sh
#
#   A COMPONENT fixture for no-internal-refs-gate.sh's `nir_keep_line`.
#
# ─── WHAT THIS IS NOT ────────────────────────────────────────────────────────
#   This does NOT drive the assembled gate. It calls `nir_keep_line` directly
#   and never invokes `run_gate`, so it CANNOT detect a defect in the pipeline
#   that connects scanning to filtering to reporting.
#
#   That limitation is the same one that let a whole class of defects survive:
#   the gate's own self-test also exercises components and never the assembled
#   path. A passing run here is evidence about ONE function and nothing more.
#
#   An end-to-end fixture is a separate artifact, and it IS buildable: `run_gate`
#   takes no tree argument, but it binds to the CURRENT WORKING DIRECTORY's git
#   repo rather than to this one. `cd` into a throwaway `git init` tree with
#   planted, COMMITTED fixtures and it scans that tree instead. (Planted files
#   must be committed — the scan reads tracked content, so untracked fixtures
#   are invisible.) That was established by running it, after three of us had
#   each concluded from reading that it was impossible.
#
#   This file deliberately does not pretend to be that artifact.
#
# ─── WHY IT EXISTS ANYWAY ────────────────────────────────────────────────────
#   `nir_keep_line` decides which scanned lines are eligible to be reported.
#   A widening elsewhere in the gate is INERT unless this function agrees, and
#   that exact mismatch shipped once already. This pins its contract so the
#   next widening cannot land silently inert.
#
# ─── INVARIANTS THIS FILE MUST KEEP ──────────────────────────────────────────
#   1. MIXED EXPECTED OUTPUT.  At least one KEEP case and one DISCARD case run
#      together. If the function fails to load, every call returns non-zero and
#      the table prints all-DISCARD — byte-identical to a real all-discard
#      result. A KEEP verdict is the only proof the instrument loaded at all.
#      Never reduce this to a uniform-expectation table.
#
#   2. DYNAMIC EXTRACTION.  The function is located by matching its closing
#      brace, never by a fixed line offset. A fixed window silently truncates
#      when the function grows, producing a syntax error and a dead function
#      whose failure looks like a result.
#
# Usage:  bash eng/ci/nir-keep-line.fixture.sh
#         exit 0 = every closed surface behaves as pinned
#         exit 1 = a pinned surface regressed
set -uo pipefail

GATE="${1:-eng/ci/no-internal-refs-gate.sh}"

if [ ! -f "$GATE" ]; then
    printf 'nir-keep-line.fixture: gate not found: %s\n' "$GATE" >&2
    exit 2
fi

# ── Invariant 2: locate the function dynamically, never a fixed window ───────
start="$(grep -n '^nir_keep_line()' "$GATE" | head -1 | cut -d: -f1)"
if [ -z "$start" ]; then
    printf 'nir-keep-line.fixture: nir_keep_line() not found in %s\n' "$GATE" >&2
    exit 2
fi
end="$(awk -v s="$start" 'NR>s && /^}/ {print NR; exit}' "$GATE")"
if [ -z "$end" ]; then
    printf 'nir-keep-line.fixture: could not find the closing brace of nir_keep_line()\n' >&2
    exit 2
fi

extracted="$(mktemp)"
trap 'rm -f "$extracted"' EXIT
sed -n "${start},${end}p" "$GATE" > "$extracted"
# shellcheck disable=SC1090
source "$extracted"

if ! declare -f nir_keep_line >/dev/null; then
    printf 'nir-keep-line.fixture: nir_keep_line failed to load — results would be meaningless\n' >&2
    exit 2
fi

# ── The cases. Columns: expected | surface | sample line ─────────────────────
#
# PINNED means the gate's current behaviour is asserted and a change fails this
# fixture. GAP means the rule requires coverage the gate does not yet provide;
# these are reported loudly and do NOT fail, so this file never blesses the gap
# as correct nor blocks on work that is tracked elsewhere.
#
run_case() {
    local expect="$1" kind="$2" surface="$3" line="$4" actual
    if nir_keep_line "$line"; then actual="KEEP"; else actual="DISCARD"; fi

    if [ "$actual" = "$expect" ]; then
        printf '  ok       %-8s %-34s -> %s\n' "$kind" "$surface" "$actual"
        return 0
    fi
    if [ "$kind" = "GAP" ]; then
        printf '  GAP      %-8s %-34s -> %s (rule requires %s)\n' "$kind" "$surface" "$actual" "$expect"
        return 0
    fi
    printf '  REGRESS  %-8s %-34s -> %s (pinned %s)\n' "$kind" "$surface" "$actual" "$expect"
    return 1
}

printf 'nir_keep_line component fixture (%s:%s-%s)\n' "$GATE" "$start" "$end"

rc=0

# Invariant 1: KEEP cases and DISCARD cases in the same run. A load failure
# makes every one of these print DISCARD, so the KEEP rows are the control.
run_case KEEP    PINNED ".cs XML doc comment" \
    'src/X/Bar.cs:10:/// per ADR-056' || rc=1

run_case KEEP    PINNED ".cs string literal" \
    'src/X/Bar.cs:11:    throw new Exception("per ADR-057");' || rc=1

run_case KEEP    PINNED "package README" \
    'src/X/README.md:3:see ADR-058' || rc=1

run_case DISCARD PINNED ".cs non-doc comment" \
    'src/X/Bar.cs:12:// internal note ADR-059' || rc=1

# Surfaces the rule names as public that the gate does not yet keep.
# Expected KEEP so the day they are closed this fixture turns green by itself.
run_case KEEP    GAP    ".csproj <Description>" \
    'src/X/Foo.csproj:6:  <Description>see ADR-055</Description>' || rc=1

run_case KEEP    GAP    ".resx <value>" \
    'src/X/Resources.resx:12:  <value>per ADR-054</value>' || rc=1

# A plain .md under src/ is discarded while src/**/README.md is kept — only the
# path differs. This surface includes ARCHITECTURE.md, which the project mandates
# for guarantee-critical subsystems and explicitly places under this rule.
run_case KEEP    GAP    "src/**/ARCHITECTURE.md" \
    'src/X/ARCHITECTURE.md:4:the duplicate window is bounded per ADR-060' || rc=1

printf '\n'
if [ "$rc" -eq 0 ]; then
    printf 'PASS — every PINNED surface behaves as recorded.\n'
    printf 'This says nothing about the assembled pipeline; run_gate is never invoked here.\n'
else
    printf 'FAIL — a PINNED surface changed behaviour.\n'
fi
exit "$rc"
