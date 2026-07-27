#!/usr/bin/env bash
# fabricated-utc-gate.sh — no tool may stamp a LITERAL 'Z' on a LOCAL timestamp (48ay30).
#
# WHY THIS EXISTS
#   `git log --date=format:'%Y-%m-%dT%H:%M:%SZ'` renders the commit's date in its OWN (local) timezone
#   and then appends a literal 'Z' — claiming UTC while showing a local wall-clock. A 5-hour skew
#   from exactly this fabrication nearly exonerated a freeze breach: the timestamps read as UTC but were
#   CDT, so a "before the stop" commit looked "after" it. A fabricated instant is worse than an honest one.
#
# WHAT IT FORBIDS (the fabrication) vs ALLOWS (the truthful forms)
#   FORBID : git … --date=format:'…Z'  /  --date=format-local:'…Z'   (renders local tz, lies with a 'Z')
#   ALLOW  : git … --date=format:'…%z'                      (emits the REAL offset, e.g. -05:00)
#            git … --format=%cI / %cd with an ISO/strict style        (git's own true-offset ISO)
#            git … --format=%ct  (epoch — no timezone to fabricate)
#            date -u +'…Z'                                            (-u forces UTC → the 'Z' is TRUE)
#
#   The rule is precise: a literal trailing 'Z' is a lie ONLY when the producer renders LOCAL time. So the
#   gate targets exactly `--date=format[-local]:` format strings that end in a literal Z, and `date +…Z`
#   invocations that lack `-u`. A truthful `date -u …Z` and a `%z` real-offset form both pass.
#
# Exit: 0 = no fabrication found · 1 = a fabricated-UTC pattern found · 2 = cannot evaluate
#
# Overridable for the self-test:  SCAN_ROOT (default: tracked files under eng/ .claude/ scripts/)
#
# Usage: eng/ci/fabricated-utc-gate.sh [--self-test]

set -uo pipefail

readonly E_OK=0
readonly E_FOUND=1
readonly E_ENV=2

if [ "${1:-}" = "--self-test" ]; then
    exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/fabricated-utc-gate.test.sh"
fi

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || true)"
[ -n "$REPO_ROOT" ] || { echo "[fabricated-utc-gate] CANNOT EVALUATE — not in a git repo." >&2; exit "$E_ENV"; }

# The set of files to scan. Default = tracked shell/ps1/py under the tooling dirs (where commit-date
# tooling lives). SCAN_ROOT overrides it for the self-test (a fixture dir).
# Exclude this gate's OWN file (its comments document the forbidden pattern) and any *.test.sh (test
# harnesses legitimately carry the offender pattern as a fixture — the self-test scans its temp fixtures
# via SCAN_ROOT). A `# pragma: allow-local-z` on a specific line also opts that line out (per-line, below).
SELF="$(basename "${BASH_SOURCE[0]}")"
if [ -n "${SCAN_ROOT:-}" ]; then
    mapfile -t FILES < <(find "$SCAN_ROOT" -type f \( -name '*.sh' -o -name '*.ps1' -o -name '*.py' \) 2>/dev/null \
        | grep -vE "(/${SELF}$|\.test\.sh$)" | LC_ALL=C sort)
else
    mapfile -t FILES < <(cd "$REPO_ROOT" && git ls-files 'eng/*.sh' 'eng/**/*.sh' '.claude/**/*.sh' 'scripts/**' 2>/dev/null \
        | grep -vE "(/${SELF}$|\.test\.sh$)" | LC_ALL=C sort)
fi

found=0
for f in "${FILES[@]}"; do
    [ -f "$f" ] || { [ -f "$REPO_ROOT/$f" ] && f="$REPO_ROOT/$f" || continue; }
    # (1) git --date=format[-local]:'…Z'  — a literal Z on git's local-tz render.
    if grep -nE "date=format(-local)?:[^ ]*Z['\"]" "$f" 2>/dev/null | grep -v 'pragma: allow-local-z' > /tmp/fu_hits1 2>/dev/null && [ -s /tmp/fu_hits1 ]; then
        while IFS= read -r hit; do
            echo "[fabricated-utc-gate] FABRICATED UTC: $f: $hit" >&2
            echo "                      --date=format renders LOCAL tz; a literal 'Z' lies. Use %z (real offset) or 'date -u'." >&2
            found=$((found + 1))
        done < /tmp/fu_hits1
    fi
    # (2) `date +…Z` WITHOUT -u on the same invocation (local wall-clock with a fabricated Z).
    #     Stage 1: a `date` call with a `+FORMAT` whose format ends in a literal Z. Stage 2: drop any
    #     line that also passes `-u` to date (that Z is TRUE). Simple + sound; over-match is caught by
    #     the self-test's liveness arms (a truthful `date -u …Z` must still pass).
    if grep -nE "(^|[^A-Za-z_])date[[:space:]][^|]*\+[\"']?%?[^\"' ]*Z" "$f" 2>/dev/null \
         | grep -vE "(^|[^A-Za-z_])date[[:space:]]+-u([[:space:]]|$)" | grep -v 'pragma: allow-local-z' > /tmp/fu_hits2 2>/dev/null && [ -s /tmp/fu_hits2 ]; then
        while IFS= read -r hit; do
            echo "[fabricated-utc-gate] FABRICATED UTC: $f: $hit" >&2
            echo "                      'date +…Z' without -u stamps LOCAL time as UTC. Add -u, or drop the Z." >&2
            found=$((found + 1))
        done < /tmp/fu_hits2
    fi
done
rm -f /tmp/fu_hits1 /tmp/fu_hits2 2>/dev/null || true

if [ "$found" -gt 0 ]; then
    echo "[fabricated-utc-gate] FAIL — $found fabricated-UTC pattern(s). A literal 'Z' on a local timestamp" >&2
    echo "                      misrepresents the instant (a 5h skew nearly exonerated a freeze breach)." >&2
    exit "$E_FOUND"
fi
echo "[fabricated-utc-gate] OK — no fabricated-UTC (literal-Z-on-local) timestamp patterns."
exit "$E_OK"
