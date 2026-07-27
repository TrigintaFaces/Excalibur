#!/usr/bin/env bash
# docs-csharp-phantom-gate.sh — HUNK-scoped CI gate over C# snippets in the docs surface.
#
# Runs docs-csharp-extract.py restricted to the snippets a diff ACTUALLY TOUCHED (by changed
# line, not just changed file), and fails only on NEW phantom framework APIs there. Editing an
# unrelated line in a doc does NOT re-flag pre-existing snippets elsewhere in that file.
# Pre-existing phantoms are a separate, tracked cleanup — not this gate's job.
# Intentional teaching placeholders opt out per-block with a `ignore` / `no-compile` fence
# token (```csharp ignore). CI-only, NOT pre-commit (gate-lean direction).
#
# Environment overrides:
#   DOCS_GATE_BASE_REF        base ref for the diff (default: origin/main)
#   DOCS_GATE_CHANGED_LINES   explicit `path:line` entries -> hunk scope (self-test)
#   DOCS_GATE_CHANGED_FILES   explicit path list -> WHOLE-FILE scope (self-test / CI override)
#   DOCS_GATE_REPO            repo root (default: two levels up from this script)
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO="${DOCS_GATE_REPO:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
TOOL="$SCRIPT_DIR/docs-csharp-extract.py"
BASE_REF="${DOCS_GATE_BASE_REF:-origin/main}"

# A doc file the gate cares about: docs/** or docs-site/** markdown, or any README*.md(x).
DOC_FILTER='^(docs/|docs-site/).*\.(md|mdx)$|(^|/)README[^/]*\.(md|mdx)$'

linesfile="$(mktemp)"
filesfile="$(mktemp)"
trap 'rm -f "$linesfile" "$filesfile"' EXIT

# Parse `git diff -U0` hunk headers for one file into `path:line` added-line entries.
emit_hunk_lines() {
    local f="$1"
    (cd "$REPO" && git diff -U0 "${BASE_REF}...HEAD" -- "$f" 2>/dev/null) \
        | awk -v f="$f" '
            /^@@/ {
                plus = $3; sub(/^\+/, "", plus)        # +c,d  ->  c,d
                n = split(plus, a, ",")
                start = a[1] + 0
                cnt = (n > 1 ? a[2] + 0 : 1)           # omitted count == 1
                for (i = 0; i < cnt; i++) print f ":" (start + i)
            }'
}

mode="lines"
if [ -n "${DOCS_GATE_CHANGED_LINES:-}" ]; then
    printf '%s\n' ${DOCS_GATE_CHANGED_LINES} > "$linesfile"
elif [ -n "${DOCS_GATE_CHANGED_FILES:-}" ]; then
    mode="files"
    printf '%s\n' ${DOCS_GATE_CHANGED_FILES} | grep -E "$DOC_FILTER" > "$filesfile" || true
else
    changed="$(cd "$REPO" && git diff --name-only --diff-filter=AM "${BASE_REF}...HEAD" 2>/dev/null \
        | grep -E "$DOC_FILTER" || true)"
    if [ -n "$changed" ]; then
        while IFS= read -r f; do
            [ -n "$f" ] && emit_hunk_lines "$f" >> "$linesfile"
        done <<< "$changed"
    fi
fi

if [ "$mode" = "files" ]; then
    if [ ! -s "$filesfile" ]; then
        echo "docs-csharp-phantom-gate: no changed doc files vs ${BASE_REF} — nothing to gate. PASS."
        exit 0
    fi
    echo "docs-csharp-phantom-gate: scanning $(grep -c . "$filesfile") changed doc file(s) [whole-file] vs ${BASE_REF}"
    python3 "$TOOL" --repo "$REPO" --gate-files "$filesfile"
    rc=$?
else
    if [ ! -s "$linesfile" ]; then
        echo "docs-csharp-phantom-gate: no changed doc snippets vs ${BASE_REF} — nothing to gate. PASS."
        exit 0
    fi
    echo "docs-csharp-phantom-gate: scanning $(grep -c . "$linesfile") changed line(s) [hunk] vs ${BASE_REF}"
    python3 "$TOOL" --repo "$REPO" --gate-lines "$linesfile"
    rc=$?
fi

if [ "$rc" -ne 0 ]; then
    echo "" >&2
    echo "docs-csharp-phantom-gate: FAIL — a changed C# snippet references a phantom framework API (above)." >&2
    echo "  Fix the snippet to use a real API, or — if the type is an intentional placeholder —" >&2
    echo "  mark the fence:  \`\`\`csharp ignore" >&2
fi
exit "$rc"
