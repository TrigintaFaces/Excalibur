#!/usr/bin/env bash
# Non-vacuity fixture for eng/ci/spa-gate.sh.
#
# A gate nobody has fixtured is a badge, not a control. This asks the only question that
# matters about a gate: WHAT MAKES IT GREEN WHILE THE PROPERTY IS FALSE?
#
#   positive control  clean tree                  -> exit 0            (assets in sync)
#   negative control  source edited, assets stale -> exit EXIT_DRIFT   (drift detected)
#
# BOTH controls assert an EXACT exit code, never merely "non-zero". An earlier version of this
# fixture accepted any non-zero status as "drift detected", so a missing `npm ci` -- which makes
# `npx vite` fail -- would have satisfied the negative control and the fixture would have PASSED
# while proving nothing. That is the same defect the gate exists to prevent, one level up: a
# control that cannot tell "the property is false" from "I could not evaluate the property".
#
# The probe is a side effect (console.log) in the SPA entry module, NOT a comment and NOT an
# unused export: comments are stripped and unused exports are tree-shaken, so either would leave
# the bundle byte-identical, the drift check would (correctly) stay green, and the fixture would
# prove nothing while appearing to pass.
#
# The source file is restored from a private `cp` backup, never `git checkout` -- a whole-file
# checkout would silently destroy any uncommitted work in that file.

set -uo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
GATE="${REPO_ROOT}/eng/ci/spa-gate.sh"
ENTRY="${REPO_ROOT}/src/Excalibur/Excalibur.Operations.Dashboard.Spa/ClientApp/src/main.ts"
BACKUP="${ENTRY}.spa-gate-selftest.bak"

readonly EXIT_DRIFT=1
readonly EXIT_ENV=2

pass() { printf 'SELF-TEST: PASS -- %s\n' "$*"; }
fail() { printf 'SELF-TEST: FAIL -- %s\n' "$*" >&2; restore; exit 1; }

# A restoration proof is only meaningful from a CLEAN baseline. If a file already has pending
# changes when its arm starts, the post-restore `git status` says nothing about whether the
# restore worked -- yet it reads as "the harness left the tree dirty", which is a confident
# accusation of the wrong component.
#
# The case that makes this load-bearing: a committed blob that is not its own normalized form.
# A `text`-attributed file carrying a stray CRLF cleans to a DIFFERENT hash than the blob it
# was committed as, so git calls it modified the moment it re-reads the content. On a CRLF
# checkout the smudge hides it and the tree looks clean; on an LF checkout it does not. That
# lands as "left modified after restore" on Linux CI, byte-exact restore and all, and is
# unreproducible on Windows. Assert the baseline so the arm names the real fault.
#
# `git status` alone CANNOT answer this. It short-circuits on the index's cached stat data, so a
# file whose content already disagrees with its blob reports CLEAN until something changes the
# file's inode or mtime -- and the `cp`/`mv -f` below is exactly that. The dirt would then first
# become visible AFTER the restore, i.e. it would read as the restore's fault. Hash the content
# directly, which no stat cache can mask, and check it BEFORE touching anything.
require_clean_baseline() {
    local f="$1" head_blob worktree_blob
    head_blob="$(git rev-parse "HEAD:$(git ls-files --full-name -- "$f")" 2>/dev/null || true)"
    worktree_blob="$(git hash-object --path="$f" "$f" 2>/dev/null || true)"
    if [ -z "$(git status --porcelain -- "$f")" ] \
       && { [ -z "$head_blob" ] || [ "$head_blob" = "$worktree_blob" ]; }; then
        return 0
    fi
    printf 'SELF-TEST: FAIL -- %s is ALREADY dirty BEFORE this arm ran.\n' "$f" >&2
    printf '  A restoration proof needs a clean baseline, so this run is no verdict on the restore.\n' >&2
    printf '    committed blob : %s\n' "${head_blob:-<none>}" >&2
    printf '    working tree   : %s\n' "${worktree_blob:-<unreadable>}" >&2
    if [ -n "$head_blob" ] && [ "$head_blob" != "$worktree_blob" ] \
       && [ -z "$(git status --porcelain -- "$f")" ]; then
        printf '  `git status` calls this file clean and its CONTENT disagrees with its blob, so the\n' >&2
        printf '  committed blob is not its own normalized form -- e.g. build output committed with a\n' >&2
        printf '  stray CRLF under a `text` attribute. It reads clean on a CRLF checkout and modified\n' >&2
        printf '  on an LF one. Fix the ARTIFACT (rebuild/normalize it and commit), not this check.\n' >&2
    fi
    exit 1
}

restore() {
    if [ -f "$BACKUP" ]; then
        mv -f "$BACKUP" "$ENTRY"
        printf 'SELF-TEST: restored %s from private backup\n' "$(basename "$ENTRY")"
    fi
}
trap restore EXIT

[ -f "$ENTRY" ] || fail "SPA entry module not found at ${ENTRY}"

run_gate() {
    # `bash "$GATE"`, not "$GATE": the executable bit lives in git's index, not on an NTFS
    # filesystem, so a file-copy mirror drops it and the gate exits 126 in CI while passing locally.
    bash "$GATE" --drift-only >/dev/null 2>&1
    printf '%s' "$?"
}

# ---------------------------------------------------------------- positive control
printf 'SELF-TEST: positive control -- clean tree, drift check must exit 0\n'
status="$(run_gate)"
case "$status" in
    0)             pass "clean tree is GREEN" ;;
    "$EXIT_ENV")   fail "the gate CANNOT EVALUATE the property here (exit ${EXIT_ENV}). This is an
                          environment problem, NOT drift. Run: (cd .../ClientApp && npm ci)" ;;
    "$EXIT_DRIFT") fail "the drift check reports drift on a clean tree (false positive; it would be
                          disabled within a week)" ;;
    *)             fail "the gate exited ${status} on a clean tree -- neither pass, drift, nor a
                          recognised environment failure" ;;
esac

# ---------------------------------------------------------------- negative control
printf 'SELF-TEST: negative control -- source edited, assets stale, drift check must exit %s\n' "$EXIT_DRIFT"
require_clean_baseline "$ENTRY"
cp "$ENTRY" "$BACKUP"
printf '\nconsole.log("__spa_gate_selftest_probe__");\n' >> "$ENTRY"

status="$(run_gate)"
restore
trap - EXIT

case "$status" in
    "$EXIT_DRIFT") pass "stale assets are RED (exit ${EXIT_DRIFT}, the drift code specifically)" ;;
    0)             printf 'SELF-TEST: FAIL -- the drift check is VACUOUS: GREEN with edited source and stale assets\n' >&2; exit 1 ;;
    *)             printf 'SELF-TEST: FAIL -- the gate exited %s, not the drift code. It failed for the
                          WRONG REASON, so this run proves nothing about the drift check.\n' "$status" >&2; exit 1 ;;
esac

# ---------------------------------------------------------------- restoration proof
if [ -n "$(git status --porcelain -- "$ENTRY")" ]; then
    printf 'SELF-TEST: FAIL -- %s was left modified after restore\n' "$ENTRY" >&2
    exit 1
fi
pass "source restored; git status clean for the probed file"

# -------------------------------------------- index.html TEMPLATE-only negative control (sthdvg)
# A template-only edit -- a CSP <meta>, a <script>/<link>, an inline config block -- changes
# wwwroot/index.html WITHOUT renaming any hashed asset, so the asset-filename check (1) stays GREEN
# while a stale index.html ships (e.g. an old CSP header). This arm proves check (2) catches that:
# mutate ONLY the committed index.html (no source edit, so the fresh build emits the SAME assets and
# a DIFFERENT-from-committed index.html) and require the drift code SPECIFICALLY -- an EXIT_ENV here
# would mean the toolchain, not the template gate, and must not satisfy the control.
INDEX="${REPO_ROOT}/src/Excalibur/Excalibur.Operations.Dashboard.Spa/wwwroot/index.html"
INDEX_BACKUP="${INDEX}.spa-gate-selftest.bak"

[ -f "$INDEX" ] || fail "committed wwwroot/index.html not found at ${INDEX}"

restore_index() {
    if [ -f "$INDEX_BACKUP" ]; then
        mv -f "$INDEX_BACKUP" "$INDEX"
        printf 'SELF-TEST: restored %s from private backup\n' "$(basename "$INDEX")"
    fi
}
trap restore_index EXIT

printf 'SELF-TEST: index.html template-only negative control -- stale committed index.html, drift check must exit %s\n' "$EXIT_DRIFT"
require_clean_baseline "$INDEX"
cp "$INDEX" "$INDEX_BACKUP"
# A stale CSP <meta> the fresh vite build will NOT emit: it changes index.html CONTENT (normalized,
# so not a CRLF artifact) but renames NO hashed asset, so only check (2) can see it.
printf '\n<meta http-equiv="Content-Security-Policy" content="__spa_gate_selftest_stale_csp__">\n' >> "$INDEX"

status="$(run_gate)"
restore_index
trap - EXIT

case "$status" in
    "$EXIT_DRIFT") pass "stale index.html (template-only, no asset rename) is RED (exit ${EXIT_DRIFT}, the drift code)" ;;
    0)             printf 'SELF-TEST: FAIL -- the index.html drift check is VACUOUS: GREEN with a stale committed index.html\n' >&2; exit 1 ;;
    "$EXIT_ENV")   printf 'SELF-TEST: FAIL -- the gate CANNOT EVALUATE (exit %s) on the index.html arm; toolchain, not template drift\n' "$EXIT_ENV" >&2; exit 1 ;;
    *)             printf 'SELF-TEST: FAIL -- the gate exited %s on a stale index.html, not the drift code -- wrong reason, proves nothing\n' "$status" >&2; exit 1 ;;
esac

if [ -n "$(git status --porcelain -- "$INDEX")" ]; then
    printf 'SELF-TEST: FAIL -- %s was left modified after restore\n' "$INDEX" >&2
    exit 1
fi
pass "index.html restored; git status clean for the template file"

printf 'SELF-TEST: the drift check is non-vacuous.\n'
