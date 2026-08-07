#!/usr/bin/env bash
# release-test-verdict-gate.sh — a release may only promote a SHA whose tests actually passed.
#
# WHAT THIS EXISTS TO PREVENT
# ---------------------------
# Nothing in the release path consulted the test verdict. Measured:
#
#   ci.yml           the ONLY workflow that runs tests, and it skips the whole matrix on a
#                    documentation-only commit
#   official-build   packs, validates, hashes, attests, uploads. No test step. No path filter.
#                    Runs on every push to main, CONCURRENTLY with ci.yml -- on the commit that
#                    prompted this, both started in the same second.
#   release          REFUSEs unless the OFFICIAL BUILD succeeded, and never mentions ci.yml.
#
# So a release could promote packages built from code whose tests had failed, or had never run.
#
# The reachable sequence: push a source change and let its tests go red; push any *.md or docs/
# change; that commit's ci.yml skips the entire matrix and reports success, official-build is green
# because it never tested anything, and the tip of main now reads green. A documentation commit
# laundered a red parent into a releasable state.
#
# THE SKIP ITSELF IS CORRECT AND IS KEPT. On a docs-only commit the code is byte-identical to its
# parent, so re-running the suite proves nothing. What was missing is that such a commit has no test
# verdict OF ITS OWN -- it inherits its parent's. This gate makes that inheritance explicit instead
# of letting "skipped" read as "passed".
#
# THE THREE STATES -- REFUSE IS NOT A PASS
#   0 GREEN    this SHA, or the nearest ancestor that actually ran tests, passed them
#   1 RED      that verdict is a failure -- do not release
#   2 REFUSE   no verdict could be established, so nothing was measured
#
# REFUSE exists because the failure mode of a verdict-checker is to go green when it cannot find a
# verdict at all. "No failing run found" is trivially true when no run was found. That is the
# safety-satisfied-by-inaction trap applied to this gate, so it is a hard REFUSE.
#
# THE WALK. Starting at the SHA and following first parents, the first commit whose ci.yml run
# actually EXECUTED the test matrix decides the verdict. A commit whose matrix was skipped carries
# no verdict and is passed over. The walk is bounded; exhausting it REFUSEs rather than guessing.
#
# Usage:
#   release-test-verdict-gate.sh --sha <sha> [--repo owner/name] [--max-walk N]
#   release-test-verdict-gate.sh --self-test
#
# Overridable so the policy can be tested without a network:
#   RTV_VERDICT_CMD   a command taking a sha and echoing GREEN | RED | SKIPPED | NONE.
#                     Defaults to the gh-based implementation below.

set -uo pipefail

readonly E_GREEN=0
readonly E_RED=1
readonly E_REFUSE=2

TAG='[release-test-verdict-gate]'

if [ "${1:-}" = "--self-test" ]; then
    exec bash "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/release-test-verdict-gate.test.sh"
fi

SHA=""
REPO="${GITHUB_REPOSITORY:-}"
MAX_WALK=25

while [ $# -gt 0 ]; do
    case "$1" in
        --sha)      SHA="${2:-}"; shift 2 ;;
        --repo)     REPO="${2:-}"; shift 2 ;;
        --max-walk) MAX_WALK="${2:-}"; shift 2 ;;
        -h|--help)  sed -n '1,50p' "${BASH_SOURCE[0]}"; exit 0 ;;
        *) echo "$TAG unknown argument: $1" >&2; exit "$E_REFUSE" ;;
    esac
done

[ -n "$SHA" ] || { echo "$TAG CANNOT EVALUATE — no --sha given." >&2; exit "$E_REFUSE"; }

# ── the fetch half: what verdict does THIS sha carry? ────────────────────────────────────────────
#
# GREEN/RED only when the test matrix actually produced a result. A run whose unit and integration
# jobs are all 'skipped' is a documentation-only run: it concluded success, and that success is
# about the documentation, not about the code.
gh_verdict() {
    local sha="$1" runs jobs ran
    [ -n "$REPO" ] || { echo NONE; return; }

    runs="$(gh api "repos/${REPO}/actions/runs?head_sha=${sha}&per_page=100" \
              --jq '[.workflow_runs[] | select(.path == ".github/workflows/ci.yml")]
                    | sort_by(.run_attempt) | last
                    | if . == null then "none none" else "\(.id) \(.status)/\(.conclusion // "pending")" end' \
              2>/dev/null)" || { echo NONE; return; }
    [ -n "$runs" ] && [ "$runs" != "none none" ] || { echo NONE; return; }

    local run_id state
    run_id="${runs%% *}"; state="${runs##* }"
    case "$state" in
        completed/*) ;;
        *) echo NONE; return ;;                       # still running: no verdict yet, never a pass
    esac

    # Did the matrix actually run? Count test jobs that reached a real conclusion.
    jobs="$(gh api --paginate "repos/${REPO}/actions/runs/${run_id}/jobs?per_page=100" \
              --jq '.jobs[] | select(.name | test("^(Unit Tests|Integration Tests)")) | .conclusion' \
              2>/dev/null)" || { echo NONE; return; }

    ran="$(printf '%s\n' "$jobs" | grep -cE '^(success|failure)$' || true)"
    if [ "${ran:-0}" -eq 0 ]; then echo SKIPPED; return; fi

    if printf '%s\n' "$jobs" | grep -qE '^failure$'; then echo RED; else
        case "$state" in completed/success) echo GREEN ;; *) echo RED ;; esac
    fi
}

VERDICT_CMD="${RTV_VERDICT_CMD:-gh_verdict}"

# ── the policy half: walk first parents until a commit carries a verdict ─────────────────────────
current="$SHA"
walked=0
while [ "$walked" -lt "$MAX_WALK" ]; do
    verdict="$($VERDICT_CMD "$current")"
    case "$verdict" in
        GREEN)
            if [ "$current" = "$SHA" ]; then
                echo "$TAG GREEN — ${SHA} ran its test matrix and passed."
            else
                echo "$TAG GREEN — ${SHA} carries no test verdict of its own (its matrix was skipped, so nothing about the code changed). The nearest ancestor that did run tests, ${current}, passed."
            fi
            exit "$E_GREEN" ;;
        RED)
            if [ "$current" = "$SHA" ]; then
                echo "::error::$TAG RED — ${SHA} ran its test matrix and FAILED. A release must not promote packages built from code whose tests did not pass." >&2
            else
                echo "::error::$TAG RED — ${SHA} carries no test verdict of its own, and the nearest ancestor that did run tests, ${current}, FAILED. A documentation-only commit does not re-test the code beneath it; it inherits this verdict." >&2
            fi
            exit "$E_RED" ;;
        SKIPPED)
            parent="$(git rev-parse --verify --quiet "${current}^" 2>/dev/null || true)"
            if [ -z "$parent" ]; then
                # A shallow clone has no ancestors to walk, and that is a DIFFERENT problem from a
                # commit genuinely having no parent. Saying so is the difference between a fixable
                # message and a confusing one: actions/checkout defaults to depth 1, so this is the
                # most likely way for the gate to refuse in CI, and the fix is a checkout setting.
                if [ "$(git rev-parse --is-shallow-repository 2>/dev/null)" = "true" ]; then
                    echo "::error::$TAG REFUSE — ${current} skipped its test matrix, so its verdict must be inherited from an ancestor, but this is a SHALLOW clone and the ancestor is not present. Check out with enough history (fetch-depth: 0, or a depth greater than the run of documentation commits) and re-run. This is a checkout problem, not a test failure." >&2
                else
                    echo "::error::$TAG REFUSE — ${current} skipped its test matrix and has no parent to inherit a verdict from. Nothing was measured; that is not a pass." >&2
                fi
                exit "$E_REFUSE"
            fi
            current="$parent"
            walked=$((walked + 1)) ;;
        NONE)
            echo "::error::$TAG REFUSE — no completed ci.yml run could be found for ${current}. A missing verdict is not a passing one; re-run CI for this commit, or wait for it to finish, before releasing." >&2
            exit "$E_REFUSE" ;;
        *)
            echo "::error::$TAG REFUSE — the verdict source returned '${verdict}', which this gate cannot interpret." >&2
            exit "$E_REFUSE" ;;
    esac
done

echo "::error::$TAG REFUSE — walked ${MAX_WALK} commits from ${SHA} without finding one that ran its test matrix. Either the history is entirely documentation, or the walk bound is too small; either way nothing has been measured." >&2
exit "$E_REFUSE"
