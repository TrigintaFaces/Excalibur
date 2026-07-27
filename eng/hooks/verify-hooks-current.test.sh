#!/usr/bin/env bash
# verify-hooks-current.test.sh — non-vacuous self-test for verify-hooks-current.sh
#
# WHY THIS SHAPE
#   Every arm invokes the REAL script as a subprocess, with real argv, real files, and real
#   HOOKS_SRC_DIR / HOOKS_DEST_DIR pointing at a throwaway tree. It never touches this repo's
#   .git/hooks. "Has a self-test" != "non-vacuous" (tpu8m2): a self-test that exercises a
#   function's internals but never crosses the parser->shell boundary proves nothing.
#
#   Every SAFETY arm (the gate refuses/reports a bad state) is paired with a LIVENESS arm (the
#   gate accepts a good one). A gate that reports drift unconditionally is indistinguishable
#   from a working gate unless the liveness arm exists.
#
# Usage:  bash eng/hooks/verify-hooks-current.test.sh
# Exit:   0 all arms pass · 1 an arm failed

set -uo pipefail

SCRIPT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/verify-hooks-current.sh"
PASS=0
FAIL=0

# Exit contract, mirrored from spa-gate.sh:
#   0 = property holds  ·  1 = property is FALSE (drift)  ·  2 = could not EVALUATE
readonly E_OK=0
readonly E_DRIFT=1
readonly E_ENV=2

ok()  { PASS=$((PASS + 1)); printf '  PASS  %s\n' "$1"; }
bad() { FAIL=$((FAIL + 1)); printf '  FAIL  %s\n     -> %s\n' "$1" "$2" >&2; }

# A throwaway src/dest pair. Never the real eng/hooks or .git/hooks.
#   $1 = canonical hook body ("" => do not create the canonical hook)
#   $2 = installed hook body ("" => not installed)
make_tree() {
    local canon="$1" installed="$2" dir
    dir="$(mktemp -d)"
    mkdir -p "$dir/src" "$dir/dest"
    [ -n "$canon" ]     && printf '%s\n' "$canon"     > "$dir/src/pre-commit"
    [ -n "$installed" ] && printf '%s\n' "$installed" > "$dir/dest/pre-commit"
    printf '%s' "$dir"
}

run() {
    local dir="$1"; shift
    HOOKS_SRC_DIR="$dir/src" HOOKS_DEST_DIR="$dir/dest" HOOKS_LIST="pre-commit" \
        bash "$SCRIPT" "$@" >/dev/null 2>&1
    printf '%s' "$?"
}

echo "verify-hooks-current.sh — self-test"
echo

# ---------------------------------------------------------------------------
# A. --check: the read-only property. "Cannot evaluate" is not "the property holds".
# ---------------------------------------------------------------------------

# LIVENESS: identical hooks -> the property HOLDS. Without this, a gate that always
# reports drift passes every safety arm below.
dir="$(make_tree "canonical body" "canonical body")"
rc="$(run "$dir" --check)"
[ "$rc" = "$E_OK" ] \
    && ok "A-liveness: --check with identical hooks exits 0" \
    || bad "A-liveness: identical hooks must exit 0" "got $rc"
rm -rf "$dir"

# SAFETY: installed hook diverges -> DRIFT.
dir="$(make_tree "canonical body" "STALE body")"
rc="$(run "$dir" --check)"
[ "$rc" = "$E_DRIFT" ] \
    && ok "A-safety: --check reports drift (exit 1) when installed diverges" \
    || bad "A-safety: divergence must exit E_DRIFT($E_DRIFT)" "got $rc"
rm -rf "$dir"

# SAFETY: installed hook absent -> DRIFT (it should be installed on a dev machine).
dir="$(make_tree "canonical body" "")"
rc="$(run "$dir" --check)"
[ "$rc" = "$E_DRIFT" ] \
    && ok "A-safety: --check reports drift when the installed hook is missing" \
    || bad "A-safety: missing installed hook must exit E_DRIFT($E_DRIFT)" "got $rc"
rm -rf "$dir"

# SAFETY: CANONICAL hook absent -> CANNOT EVALUATE, never "current".
# Delete eng/hooks/pre-commit and the old code logged "skipping" and exited 0.
dir="$(make_tree "" "some installed body")"
rc="$(run "$dir" --check)"
[ "$rc" = "$E_ENV" ] \
    && ok "A-safety: --check with NO canonical hook exits E_ENV($E_ENV), not 0" \
    || bad "A-safety: missing canonical hook must exit E_ENV($E_ENV)" "got $rc"
rm -rf "$dir"

# ---------------------------------------------------------------------------
# B. --heal: a destructive mode. It must be asked for by name, never inferred.
# ---------------------------------------------------------------------------

# LIVENESS: an explicit --heal on a drifted tree actually reinstalls, and exits 0.
dir="$(make_tree "canonical body" "STALE body")"
rc="$(run "$dir" --heal)"
healed="$(cat "$dir/dest/pre-commit" 2>/dev/null)"
if [ "$rc" = "$E_OK" ] && [ "$healed" = "canonical body" ]; then
    ok "B-liveness: explicit --heal reinstalls the canonical hook and exits 0"
else
    bad "B-liveness: --heal must reinstall and exit 0" "exit=$rc content='$healed'"
fi
rm -rf "$dir"

# SAFETY: an UNKNOWN argument must NOT be treated as --heal. The old default
# `MODE="${1:---heal}"` silently rewrote .git/hooks on a typo like --chek.
dir="$(make_tree "canonical body" "STALE body")"
rc="$(run "$dir" --chek)"
after="$(cat "$dir/dest/pre-commit" 2>/dev/null)"
if [ "$rc" = "$E_ENV" ] && [ "$after" = "STALE body" ]; then
    ok "B-safety: an unknown arg exits E_ENV($E_ENV) and MUTATES NOTHING"
else
    bad "B-safety: unknown arg must exit E_ENV and not mutate" "exit=$rc content='$after'"
fi
rm -rf "$dir"

# SAFETY: --check must never mutate the installed hook.
dir="$(make_tree "canonical body" "STALE body")"
run "$dir" --check >/dev/null
after="$(cat "$dir/dest/pre-commit" 2>/dev/null)"
[ "$after" = "STALE body" ] \
    && ok "B-safety: --check is read-only (installed hook untouched)" \
    || bad "B-safety: --check must not mutate" "content became '$after'"
rm -rf "$dir"

# SAFETY: bare invocation (no args) is READ-ONLY. The default inverted to --check by ruling:
# two senior operators independently ran this with no argument believing it a diagnostic and
# both mutated .git/hooks. A verb that reads as inspection must not write on its default path.
dir="$(make_tree "canonical body" "STALE body")"
rc="$(run "$dir")"
after="$(cat "$dir/dest/pre-commit" 2>/dev/null)"
if [ "$after" = "STALE body" ]; then
    ok "B-safety: bare invocation is READ-ONLY (mutates nothing)"
else
    bad "B-safety: bare invocation must NOT mutate" "exit=$rc content='$after'"
fi
rm -rf "$dir"

echo
printf 'passed %d · failed %d\n' "$PASS" "$FAIL"
[ "$FAIL" -eq 0 ] || exit 1
