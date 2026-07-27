#!/usr/bin/env bash
# verify-hooks-current.sh — keep the INSTALLED git hooks current with the canonical eng/hooks copies.
#
# Why this exists: a guard was wired into the CANONICAL eng/hooks/pre-commit while the ACTIVE
# .git/hooks/pre-commit was a STALE copy (install-hooks never re-run after the guard landed), so the
# guard never fired on commit and the failure it prevented kept recurring.
# A guard existing is not enough — it must be INSTALLED. This script makes staleness self-healing (or
# loud), so a canonical hook update can never again silently fail to reach the active commit path.
#
# The guard originally named here has since been retired; the staleness problem it exposed has not,
# which is why this check is described by its mechanism rather than by that one incident.
#
# Modes:
#   --heal              reinstall any installed hook that diverges from its canonical eng/hooks copy.
#                       DESTRUCTIVE: rewrites the developer's active commit path. Must be asked for.
#   (default / --check) read-only: exit 1 (loud) if any installed hook is stale/missing. For the
#                       integrator's pre-flight and pre-push — where hooks are actually installed.
#
#   NOTE ON CI: `--check`'s property is "the hook the developer will execute matches the canonical
#   one." That property is NOT evaluable on a CI runner: a fresh clone has no installed hook, so
#   --check would report MISSING and exit 1 on every run, forever. Do not wire --check into CI.
#   To gate this in CI, run install-hooks.sh into a temp HOOKS_DEST_DIR and --check against that —
#   i.e. assert the INSTALLER works, which is the property a clone can actually know.
#
# Knobs (env, for the regression test): HOOKS_SRC_DIR (default eng/hooks),
#   HOOKS_DEST_DIR (default .git/hooks), HOOKS_LIST (default "pre-commit").
#
# EXIT CODES (the spa-gate.sh contract — "cannot evaluate" is not "the property holds")
#   0  the property holds: installed hooks are current (or were healed)
#   1  the property is FALSE: (--check) an installed hook is stale or missing
#   2  the property could not be EVALUATED: unknown argument, or a canonical hook is absent
#
#   A missing CANONICAL hook is exit 2, never exit 0. Deleting eng/hooks/pre-commit must not
#   make this gate certify that the hooks are up to date.

set -u

readonly E_OK=0
readonly E_DRIFT=1
readonly E_ENV=2

SRC_DIR="${HOOKS_SRC_DIR:-eng/hooks}"
HOOKS="${HOOKS_LIST:-pre-commit}"

# ── THE DESTINATION IS core.hooksPath WHEN IT IS SET, NOT .git/hooks ─────────────────────────────
# This script answers "is the hook git EXECUTES current with the canonical one?" — and `.git/hooks`
# is only the executed directory when `core.hooksPath` is UNSET. git does not try one and fall back
# to the other; hooksPath REPLACES .git/hooks entirely (control recorded in install-hooks.sh:76-80).
#
# So with `core.hooksPath = eng/hooks` — this repository's configuration — comparing canonical
# against `.git/hooks` compares the live file against an ORPHAN NOTHING RUNS. Every edit to a
# canonical hook then reports STALE while the executed hook is, by construction, perfectly current.
#
# That failure is push-BLOCKING (pre-push:56 refuses on exit 1) and it is a FALSE ALARM, which is
# the dangerous kind: a gate that cries wolf on correct work teaches the team to reach for
# `--no-verify`, and that is how a REAL staleness eventually ships. Observed live: a push refused
# with "every gate you believe protected those commits may not have run" when the commits had in
# fact been gated by the canonical 805-line hook, because git was executing it directly.
#
# When hooksPath points AT the canonical dir, src and dest are the same file: the comparison is
# trivially satisfied and the answer "the executed hooks are current" is exactly right — git runs
# the tracked file, so there is nothing to install and no way to be stale. The check does not
# weaken, it becomes correct: with hooksPath unset it still compares against `.git/hooks` and still
# catches the tracked-vs-installed divergence it was written for.
_resolve_dest_dir() {
    local configured
    configured="$(git config --get core.hooksPath 2>/dev/null || true)"
    if [ -n "$configured" ]; then
        printf '%s' "$configured"
    else
        printf '%s' ".git/hooks"
    fi
}
# HOOKS_DEST_DIR still wins when set explicitly — the self-test drives both branches through it.
DEST_DIR="${HOOKS_DEST_DIR:-$(_resolve_dest_dir)}"

log() { printf '[verify-hooks-current] %s\n' "$*" >&2; }

# --heal REWRITES the developer's active commit path. It must be asked for, never inferred from
# a typo: the old `MODE="${1:---heal}"` treated `--chek` as `--heal` and silently reinstalled.
MODE="--check"
case "${1:-}" in
    "")       MODE="--check" ;;
    --heal)   MODE="--heal" ;;
    --check)  MODE="--check" ;;
    *)        log "unknown argument: $1 (expected --check or --heal)"; exit "$E_ENV" ;;
esac

stale=0
for hook in $HOOKS; do
    src="$SRC_DIR/$hook"
    dest="$DEST_DIR/$hook"

    # A missing canonical hook means the gate cannot evaluate its property, and --heal has
    # nothing to install. Reporting "current" here is the vacuous-control defect.
    if [ ! -f "$src" ]; then
        log "CANNOT EVALUATE — canonical hook missing: $src"
        exit "$E_ENV"
    fi

    if [ -f "$dest" ] && cmp -s "$src" "$dest"; then
        continue   # installed hook already matches canonical
    fi

    stale=1
    if [ "$MODE" = "--check" ]; then
        if [ -f "$dest" ]; then
            log "STALE: installed '$dest' diverges from canonical '$src'"
        else
            log "MISSING: '$dest' is not installed (canonical '$src' exists)"
        fi
        continue
    fi

    # --heal: reinstall the canonical hook to the active path.
    mkdir -p "$DEST_DIR"
    if cp "$src" "$dest" && chmod +x "$dest"; then
        log "healed: reinstalled '$dest' from canonical '$src'"
    else
        log "ERROR: failed to reinstall '$dest' from '$src'"
        exit "$E_ENV"
    fi
done

if [ "$MODE" = "--check" ] && [ "$stale" -ne 0 ]; then
    log "one or more installed hooks are stale/missing — run: bash $SRC_DIR/install-hooks.sh"
    exit "$E_DRIFT"
fi

exit "$E_OK"
