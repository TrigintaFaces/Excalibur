#!/usr/bin/env bash
# staged-secret-scan.sh — BLOCKING staged secret scan.
#
# Greps the ADDED lines of the staged diff (`git diff --cached -U0`, `^+` content lines only) against
# the secret-pattern corpus documented in .claude/rules/security/security-rules.md + security/dotnet.md.
# No network, no binary, no external tool — self-contained, reuses the existing corpus.
#
#   * A staged added line matching any high-confidence secret shape → exit 1 (commit rejected).
#   * A clean staged tree → exit 0 (commit allowed). This is the load-bearing LIVENESS half: a scanner
#     that blocked everything would be an inert control — it must let clean commits through.
#   * `# pragma: allowlist secret` on a line EXEMPTS that line (documented example tokens). The exemption
#     is at the AUTHORING site — the scanner is NEVER relaxed to clear a doc token (security-rules.md).
#   * Cannot evaluate (no git, not a repo, unreadable index) → exit 4 (REFUSE), NEVER exit 0.
#     A scanner that could not run is not a scanner that found nothing. This control stops an
#     IRREVERSIBLE harm — a disclosed credential cannot be recalled, and rotation is the cheapest
#     outcome available — so its caller BLOCKS on REFUSE rather than routing around it. Contrast a
#     recoverable-hygiene gate (a desynced tracker is fixable after the fact), which fails open + loud.
#
# Reports the offending file + matched pattern LABEL only — never dumps the secret value into hook output.
#
# Usage:  bash eng/ci/staged-secret-scan.sh
#
# Exit codes — every code declared here maps to exactly one of {PASS, FAIL, REFUSE}, and the call site
# maps every declared code explicitly. The catch-all is REFUSE: the ways a script can fail to run are
# not enumerable, so anything undeclared must never be read as a verdict.
#   0  PASS    — evaluated, no secret-shaped token in the staged added lines
#   1  FAIL    — evaluated, a secret-shaped token IS staged (caller rejects the commit)
#   4  REFUSE  — could not be evaluated (caller BLOCKS; see above)

set -uo pipefail

# shellcheck source=/dev/null
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/gate-denominator.sh"

# Secret-pattern corpus. Each entry "Label|ERE", length-bounded/anchored to stay high-confidence and
# avoid false-positive wedging. The `# pragma` on each line exempts these REGEX definitions from the
# scanner when this file is itself committed (they are patterns, not secrets).
PATTERNS=(
  "AWS access key id|AKIA[0-9A-Z]{16}"                       # pragma: allowlist secret
  "AWS temporary key id|ASIA[0-9A-Z]{16}"                    # pragma: allowlist secret
  "GitHub PAT (classic)|ghp_[A-Za-z0-9]{36,}"                # pragma: allowlist secret
  "GitHub OAuth token|gho_[A-Za-z0-9]{36,}"                  # pragma: allowlist secret
  "GitHub fine-grained PAT|github_pat_[A-Za-z0-9_]{30,}"     # pragma: allowlist secret
  "OpenAI/Anthropic API key|sk-(ant-)?[A-Za-z0-9_-]{20,}"    # pragma: allowlist secret
  "Stripe live secret key|sk_live_[A-Za-z0-9]{20,}"          # pragma: allowlist secret
  "Slack token|xox[baprs]-[A-Za-z0-9-]{10,}"                 # pragma: allowlist secret
  "Private key (PEM)|-----BEGIN [A-Z ]*PRIVATE KEY-----"     # pragma: allowlist secret
  "Azure storage AccountKey|AccountKey=[A-Za-z0-9+/=]{40,}"  # pragma: allowlist secret
  "Azure storage connection string|DefaultEndpointsProtocol=https?;AccountName=[A-Za-z0-9]"  # pragma: allowlist secret
  "NuGet API key|oy2[a-z0-9]{43}"                            # pragma: allowlist secret
  "Google API key|AIza[0-9A-Za-z_-]{35}"                     # pragma: allowlist secret
)

ALLOWLIST_RE='pragma:[[:space:]]*allowlist[[:space:]]+secret'

readonly E_REFUSE=4

# REFUSE probe — establish that the scan CAN be evaluated before it is entitled to report anything.
# Without this, a git failure produced an empty staged-file list, the loop never ran, and the scanner
# fell through to "clean" — reporting PASS on a scan it never performed.
if ! command -v git >/dev/null 2>&1; then
    echo "staged-secret-scan: REFUSE — 'git' not found on PATH; the staged tree cannot be read." >&2
    echo "  This is NOT 'no secrets found'. Fix the environment; do not route around the scanner." >&2
    exit "$E_REFUSE"
fi
if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "staged-secret-scan: REFUSE — not inside a git work tree; the staged tree cannot be read." >&2
    echo "  This is NOT 'no secrets found'." >&2
    exit "$E_REFUSE"
fi

# Read the staged file list WITHOUT swallowing git's exit. A failure here is a REFUSE, not an
# empty list: `2>/dev/null || true` is precisely how a broken index became a clean bill of health.
if ! staged_files="$(git diff --cached --name-only --diff-filter=ACM)"; then
    echo "staged-secret-scan: REFUSE — could not read the staged file list (git failed)." >&2
    echo "  This is NOT 'no secrets found'." >&2
    exit "$E_REFUSE"
fi

hits=0
while IFS= read -r f; do
    [ -n "$f" ] || continue
    # Added content lines for this staged file, minus the +++ header and any allowlisted line.
    # Capture git's output and its exit SEPARATELY from the grep pipeline. `set -o pipefail` is on,
    # so a single `git … | grep … || true` conflates two different things: git FAILING (REFUSE — we
    # cannot see this file's staged content) and grep finding NO MATCH (a normal, expected 1). The
    # `|| true` masked both, so an unreadable file silently became a file with nothing in it.
    if ! diff_out="$(git diff --cached --no-color -U0 --diff-filter=ACM -- "$f")"; then
        echo "staged-secret-scan: REFUSE — could not read the staged diff for '$f' (git failed)." >&2
        echo "  This is NOT 'no secrets found' for that file." >&2
        exit "$E_REFUSE"
    fi
    # Only grep's no-match may be tolerated here — git's failure is already handled above.
    added="$(printf '%s\n' "$diff_out" \
                | grep -E '^\+' \
                | grep -vE '^\+\+\+' \
                | grep -viE "$ALLOWLIST_RE" || true)"
    [ -n "$added" ] || continue
    for entry in "${PATTERNS[@]}"; do
        label="${entry%%|*}"
        re="${entry#*|}"
        if printf '%s\n' "$added" | grep -qE -e "$re"; then
            if [ "$hits" -eq 0 ]; then
                echo "staged-secret-scan: secret-shaped token(s) found in staged changes:" >&2
            fi
            hits=1
            printf '  %s  [%s]\n' "$f" "$label" >&2
        fi
    done
done <<< "$staged_files"

if [ "$hits" -ne 0 ]; then
    echo "  → Remove the secret. If it is a documented example, add '# pragma: allowlist secret' on that line." >&2
    echo "    Never loosen this scanner to clear a token — an example relaxation also clears a real key." >&2
    exit 1
fi
# The DENOMINATOR — how many staged files this scan actually READ. A secret scanner is the worst
# possible place for a green over an empty population: "no secrets found" and "no files examined"
# are the same output otherwise, and the second is what a broken staged-file read produces. The
# population is a diff, so zero is legitimate (an empty commit stages nothing) and the may-be-empty
# form is chosen deliberately — but the count is printed either way so a reader can tell which
# green they are looking at.
staged_count="$(printf '%s
' "$staged_files" | grep -c . || true)"
case "$staged_count" in ''|*[!0-9]*) staged_count=0 ;; esac
gate_denominator_may_be_empty "$staged_count" "staged file(s)"
exit 0
