#!/usr/bin/env bash
# release-test-verdict-gate.test.sh — proves the gate is non-vacuous.
#
# Each arm builds a real throwaway git history and drives the real gate over it with a stubbed
# verdict source, so the WALK -- the part with the judgement in it -- is exercised without a network.
#
# Arm 4 is the defect this gate was written for: a documentation-only commit sitting on top of a red
# one. Before this gate, that state was releasable.

set -uo pipefail

GATE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/release-test-verdict-gate.sh"
FAILED=0

# Builds a history of N commits, oldest first, and echoes the tip sha plus a stub script that maps
# each sha to the verdict named in VERDICTS (oldest first).
run_case() {
    local verdicts="$1" expected="$2" label="$3" maxwalk="${4:-25}"
    local dir; dir="$(mktemp -d)"
    (
        cd "$dir" || exit 9
        git init -q .
        git config user.email t@e; git config user.name t
        git config core.autocrlf false   # fixture noise only; keeps the arm output readable
        local i=0 sha map=""
        for v in $verdicts; do
            i=$((i + 1))
            echo "$i" > f.txt
            git add f.txt
            git commit -q -m "c$i"
            sha="$(git rev-parse HEAD)"
            map="${map}${sha} ${v}"$'\n'
        done
        printf '%s' "$map" > .verdicts

        # The stub: look the sha up in the map. An unknown sha is NONE, which is the honest default.
        cat > .stub.sh <<'STUB'
#!/usr/bin/env bash
sha="$1"
while read -r s v; do
    [ "$s" = "$sha" ] && { echo "$v"; exit 0; }
done < "$PWD/.verdicts"
echo NONE
STUB
        chmod +x .stub.sh 2>/dev/null || true

        RTV_VERDICT_CMD="bash $dir/.stub.sh" bash "$GATE" --sha "$(git rev-parse HEAD)" \
            --max-walk "$maxwalk" >/dev/null 2>&1
        exit $?
    )
    local ec=$?
    rm -rf "$dir"
    if [ "$ec" = "$expected" ]; then
        printf 'SELF-TEST: PASS -- %s\n' "$label"
    else
        printf 'SELF-TEST: FAIL -- %s (expected exit %s, got %s)\n' "$label" "$expected" "$ec" >&2
        FAILED=1
    fi
}

#          oldest ------------> newest        expect  label
run_case "GREEN"                       0 "a tested, passing tip is GREEN"
run_case "RED"                         1 "a tested, failing tip is RED"
run_case "GREEN SKIPPED"               0 "docs-only tip inherits a passing parent"
run_case "RED SKIPPED"                 1 "docs-only tip inherits a FAILING parent (the defect)"
run_case "RED SKIPPED SKIPPED SKIPPED" 1 "a run of docs commits cannot outlast a red ancestor"
run_case "GREEN SKIPPED SKIPPED"       0 "a run of docs commits still inherits a pass"
run_case "NONE"                        2 "no run for the tip REFUSES, never passes"
run_case "GREEN NONE"                  2 "an unfinished tip REFUSES even above a green parent"
run_case "SKIPPED SKIPPED"             2 "history that never tested REFUSES (nothing measured)"
run_case "RED SKIPPED SKIPPED"         2 "a walk bound too small to reach the verdict REFUSES" 1

if [ "$FAILED" -eq 0 ]; then
    printf 'SELF-TEST: all arms passed -- the release test-verdict gate is non-vacuous.\n'
    exit 0
fi
printf 'SELF-TEST: at least one arm failed.\n' >&2
exit 1
