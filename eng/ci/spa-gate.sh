#!/usr/bin/env bash
# Dashboard SPA gate.
#
# Runs the SPA's own quality gates -- which no pipeline invoked before this script existed --
# and then proves the SHIPPED assets were built from the CURRENT source.
#
#   vitest        the SPA's unit tests
#   svelte-check  the SPA's type gate
#   asset-drift   the committed wwwroot/ matches what ClientApp/src compiles to
#
# The drift check does NOT diff file contents. Vite content-hashes every emitted asset
# filename (index.<hash>.js), so a real source change RENAMES the asset. Comparing the
# emitted filename set against the committed filename set is therefore exact, and it cannot
# be fooled by line endings -- a plain `git diff --exit-code -- wwwroot/` reports the tree
# dirty on any Windows checkout, because vite writes LF and the checkout holds CRLF. A gate
# that red-lights a clean tree gets disabled, and then the control is gone with a green badge
# on top.
#
# The build is written to a temporary directory. This script never mutates tracked assets.
#
# Usage:
#   eng/ci/spa-gate.sh              # run every check (CI entry point)
#   eng/ci/spa-gate.sh --drift-only # only the asset-drift check
#   eng/ci/spa-gate.sh --self-test  # prove the drift check is non-vacuous (see spa-gate.test.sh)

set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
SPA_DIR="${REPO_ROOT}/src/Excalibur/Excalibur.Operations.Dashboard.Spa"
CLIENT_APP="${SPA_DIR}/ClientApp"
COMMITTED_ASSETS="${SPA_DIR}/wwwroot/assets"

# Exit codes are part of this script's contract, because its fixture asserts on them. A gate that
# returns the same status for "the property is false" and "I could not evaluate the property" cannot
# be fixtured: a build failure would masquerade as a detected defect, and the fixture would pass
# while proving nothing.
readonly EXIT_DRIFT=1        # the property is FALSE: committed assets were not built from this source
readonly EXIT_ENV=2          # the property could not be EVALUATED: toolchain/deps missing
readonly EXIT_SOURCE=3       # the SPA source itself does not compile

drift() { printf 'SPA GATE: FAIL -- %s\n' "$*" >&2; exit "$EXIT_DRIFT"; }
env_fail() { printf 'SPA GATE: CANNOT EVALUATE -- %s\n' "$*" >&2; exit "$EXIT_ENV"; }
source_fail() { printf 'SPA GATE: FAIL -- %s\n' "$*" >&2; exit "$EXIT_SOURCE"; }
info() { printf 'SPA GATE: %s\n' "$*"; }

[ -d "$CLIENT_APP" ] || env_fail "SPA source not found at ${CLIENT_APP}"

# Preflight. Without this, `npx vite` fails and the gate reports "the SPA source does not compile" --
# a confident, wrong diagnosis of a missing `npm ci`.
[ -d "${CLIENT_APP}/node_modules" ] \
    || env_fail "dependencies are not installed. Run: (cd ${CLIENT_APP} && npm ci)"

run_tests() {
    info "vitest (the SPA's unit tests)"
    # `basic` was deprecated in vitest 3 and REMOVED in 4; it fails to load as a custom reporter,
    # which the gate reports as "vitest reported failures" -- a tooling fault wearing a test failure's
    # clothes. `dot` is the concise CI reporter in 4.
    (cd "$CLIENT_APP" && npx vitest run --reporter=dot) || source_fail "vitest reported failures"
}

run_typecheck() {
    info "svelte-check (the SPA's type gate)"
    (cd "$CLIENT_APP" && npx svelte-check --tsconfig ./tsconfig.json --threshold error) \
        || source_fail "svelte-check reported errors"
}

# Build the SPA from the CURRENT source into a throwaway dir (never touches committed wwwroot/),
# then prove BOTH the shipped assets AND the shipped index.html were built from it.
run_drift_check() {
    info "asset + template drift (committed wwwroot/ vs ClientApp/src)"

    [ -d "$COMMITTED_ASSETS" ] || env_fail "no committed assets at ${COMMITTED_ASSETS}"
    [ -f "${SPA_DIR}/wwwroot/index.html" ] || env_fail "no committed wwwroot/index.html"

    local out_dir
    out_dir="$(mktemp -d)"
    trap 'rm -rf "$out_dir"' RETURN

    (cd "$CLIENT_APP" && npx vite build --outDir "$out_dir" --emptyOutDir --logLevel error >/dev/null 2>&1) \
        || source_fail "vite build failed -- the SPA source does not compile"
    [ -d "${out_dir}/assets" ] || source_fail "vite emitted no assets/ directory"
    [ -f "${out_dir}/index.html" ] || source_fail "vite emitted no index.html"

    # (1) Asset filename drift — vite content-hashes every emitted asset, so comparing the emitted
    #     filename set against the committed set is exact and CRLF-immune (names, not contents).
    local emitted committed
    emitted="$( cd "${out_dir}/assets" && ls -1 | LC_ALL=C sort )"
    committed="$( cd "$COMMITTED_ASSETS" && ls -1 | LC_ALL=C sort )"
    if [ "$emitted" != "$committed" ]; then
        {
            printf 'SPA GATE: FAIL -- the shipped assets were NOT built from the current source.\n\n'
            printf '  committed in wwwroot/assets/:\n'
            printf '%s\n' "$committed" | sed 's/^/    /'
            printf '\n  emitted by the current ClientApp/src:\n'
            printf '%s\n' "$emitted" | sed 's/^/    /'
            printf '\n  Vite content-hashes each asset filename, so a differing name means the source\n'
            printf '  changed and wwwroot/ was never rebuilt. The dashboard would ship its old behavior.\n\n'
            printf '  Fix:  cd %s && npm run build   (then commit wwwroot/)\n' "ClientApp"
        } >&2
        exit "$EXIT_DRIFT"
    fi

    # (2) index.html TEMPLATE drift (sthdvg). A template-only edit — a CSP <meta>, a <script>/<link>,
    #     an inline config block — changes wwwroot/index.html WITHOUT renaming any hashed asset, so
    #     check (1) stays green while the shipped index.html is stale (e.g. an old CSP header ships).
    #     Compare CONTENT, line-ending-normalized: the checkout holds CRLF and vite writes LF, so a
    #     raw diff would false-red on any Windows checkout (the same CRLF hazard (1) avoids by design).
    #     Vite's index.html is deterministic for a given source, so a normalized mismatch is real drift.
    local emitted_idx committed_idx
    emitted_idx="$(tr -d '\r' < "${out_dir}/index.html")"
    committed_idx="$(tr -d '\r' < "${SPA_DIR}/wwwroot/index.html")"
    if [ "$emitted_idx" != "$committed_idx" ]; then
        {
            printf 'SPA GATE: FAIL -- wwwroot/index.html was NOT rebuilt from the current source.\n\n'
            printf '  A template-only change (a CSP <meta>, a <script>/<link>, an inline config block)\n'
            printf '  edits index.html without renaming any hashed asset, so the asset-name check above\n'
            printf '  cannot see it. The dashboard would ship a stale index.html (e.g. an old CSP).\n\n'
            printf '  Fix:  cd %s && npm run build   (then commit wwwroot/)\n' "ClientApp"
        } >&2
        exit "$EXIT_DRIFT"
    fi

    info "assets + index.html in sync ($(printf '%s\n' "$emitted" | wc -l | tr -d ' ') asset files + index.html)"
    return 0
}

case "${1:-}" in
    --drift-only) run_drift_check ;;
    --self-test)  exec bash "${REPO_ROOT}/eng/ci/spa-gate.test.sh" ;;
    "")           run_tests; run_typecheck; run_drift_check; info "all checks passed" ;;
    *)            env_fail "unknown argument: $1" ;;
esac
