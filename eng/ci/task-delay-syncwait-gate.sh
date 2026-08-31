#!/usr/bin/env bash
# task-delay-syncwait-gate — block clock dependence in test code, against a shrink-only baseline.
#
# TWO shapes, because a test comes to depend on the clock in two ways and catching one leaves the
# other free: a raw `await Task.Delay(...)` sync-wait, and a short deadline on a
# CancellationTokenSource that the test must beat. Both were measured failing shards here.
#
# Enforces the determinism standard in .claude/rules/quality/testing-patterns.md
# ("Tests MUST be deterministic ... Never depend on wall-clock timing. Poll for a
# condition with a bounded timeout") and docs/testing/async-test-standards.md:
# a test that waits for a background/async condition MUST poll it via
#   tests/Shared/Tests.Shared/Infrastructure/WaitHelpers.cs (WaitUntilAsync / AwaitSignalAsync)
# rather than a fixed `await Task.Delay(N)` followed by an assertion.
#
# DESIGN — WHOLE-TREE SCAN AGAINST A BASELINE, and the diff it replaced is why.
#
#   This gate used to diff HEAD~1...HEAD and flag only NEWLY ADDED lines, so the legitimate
#   pre-existing uses were grandfathered by the diff itself. That premise holds only where history
#   is granular. It is not: this workflow also runs on a public mirror whose every push is ONE
#   SQUASHED RELEASE COMMIT. There HEAD~1 is the PREVIOUS RELEASE, the "added lines" are the whole
#   release, and the gate reported ~50 long-standing uses as newly added -- 130,524 added lines
#   examined -- and took the default branch red. Nothing in that list was new.
#
#   A diff-based gate cannot distinguish the grandfathered population from the new one when the
#   history shape changes underneath it. So the diff is gone. This scans every tests/**/*.cs in the
#   tree and compares the hits against an enumerated baseline, which is the pattern this repository
#   already uses in sixteen places (see conformance-fork-gate.sh) and the analyzer-baseline pattern
#   generally. Its verdict depends on the CONTENT of the tree and on nothing else.
#
# A Task.Delay in test code is allowed (not flagged) when ANY holds:
#   * the file is an allowlisted determinism-infra file (WaitHelpers/TestTiming/
#     TestTimeouts/ContainerFixtureBase -- these legitimately implement the delay);
#   * the file is under tests/performance/** or benchmarks/** (throughput/rate pacing);
#   * the line is a comment (// or ///);
#   * the line is a Task.WhenAny(..., Task.Delay(...)) timeout-guard (bounded race,
#     not a sync-wait);
#   * the line carries the explicit justification pragma `// delay-ok:` (author
#     asserts a legitimate time-based semantic -- e.g. lease-TTL expiry, distinct
#     timestamps, simulated work -- and states the reason);
#   * the hit is enumerated in eng/ci/task-delay-syncwait.baseline.txt.
#
# THE BASELINE IS A DEBT LEDGER WITH A RATCHET, NOT AN ALLOWLIST. An unbaselined hit FAILS on its
# first appearance; a baseline entry matching no live hit ALSO FAILS and must be removed. So the
# file can only shrink, and it cannot re-admit a violation under a recycled line.
#
# --update-baseline regenerates it from the live tree. That is a DELIBERATE act whose diff is
# reviewed like any other change -- it is not a way to clear a red.
#
# Exit codes:
#   0  PASS    every hit is baselined, no stale entry (or --report-only)
#   1  FAIL    an unbaselined hit, or a stale baseline entry
#   2  REFUSE  usage / environment error, or zero files scanned (a gate that scanned nothing
#              must not report a pass it did not earn)
#   3  --self-test failed (the gate itself is broken / vacuous)
#
# Usage:
#   task-delay-syncwait-gate.sh [--report-only]
#   task-delay-syncwait-gate.sh --update-baseline
#   task-delay-syncwait-gate.sh --self-test
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=/dev/null
. "$SCRIPT_DIR/gate-denominator.sh"

# The tree under test is the work tree you are STANDING IN, falling back to this script's own
# repository. That is what lets the harness lock drive the real gate over throwaway fixture trees
# without a second code path -- the thing a test exercises should be the thing that ships.
REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || true)"
BASELINE_DEFAULT="${TDS_BASELINE_OVERRIDE:-$SCRIPT_DIR/task-delay-syncwait.baseline.txt}"

# ---------------------------------------------------------------------------
# Core (pure, testable) predicates -- UNCHANGED from the diff-based gate. They were argued at
# length and are correct; only the population they are applied to has changed.
# ---------------------------------------------------------------------------

# tds_is_allowlisted_file <path>  -> 0 (allowlisted) / 1 (subject to the gate)
tds_is_allowlisted_file() {
    local f="$1"
    case "$f" in
        */Tests.Shared/Infrastructure/WaitHelpers.cs|*/Tests.Shared/Infrastructure/TestTiming.cs|\
*/Tests.Shared/Infrastructure/TestTimeouts.cs|*/Tests.Shared/Fixtures/ContainerFixtureBase.cs)
            return 0 ;;
        tests/performance/*|*/tests/performance/*|benchmarks/*|*/benchmarks/*)
            return 0 ;;
    esac
    return 1
}

# tds_line_is_violation <line>  -> 0 (a raw sync-wait Task.Delay) / 1 (not)
tds_line_is_violation() {
    local line="$1"
    # Must contain a Task.Delay( call. `(^|[^A-Za-z0-9_])` is the word boundary GNU grep spells `\b`
    # -- bash's ERE has no `\b`, and these run once per candidate line, so a subprocess apiece cost
    # six minutes of process spawning on a 5,600-file tree. Same language, no fork.
    [[ $line =~ (^|[^A-Za-z0-9_])Task\.Delay[[:space:]]*\( ]] || return 1
    # Not a comment line.
    [[ $line =~ ^[[:space:]]*(//|///|\*) ]] && return 1
    # Not a Task.WhenAny(...Task.Delay...) timeout-guard.
    [[ $line =~ (^|[^A-Za-z0-9_])WhenAny([^A-Za-z0-9_]|$) ]] && return 1
    # Not explicitly justified with the delay-ok pragma.
    [[ $line =~ //[[:space:]]*delay-ok ]] && return 1
    return 0
}

# tds_line_is_short_deadline <line>  -> 0 (a short wall-clock deadline) / 1 (not)
#
# The SECOND way a test comes to depend on the clock, and the one Task.Delay does not cover. A
# CancellationTokenSource built with a short duration is a deadline the test must beat, and when the
# operation under test ends on that token it is the exit mechanism as well -- so the duration is
# load-bearing rather than a safety net. On a busy agent the test loses the race and reports the
# code as broken.
#
# Measured, twice in one day: a subscriber test gave itself 50ms to reach a park, and its timer
# fired inside a setup call that sat outside the cancellation handling, so the call threw at the
# caller. A health-check test inherited a 100ms threshold and was really asserting that the agent
# had completed a crypto round-trip in a tenth of a second.
#
# THE BAR IS TEN SECONDS. Under it, say why. Generous bounds are the recommended shape and the
# majority here already: 58 use 30s. A duration that IS the semantic under test -- a batch timeout,
# a lease TTL, a rate -- is legitimate and carries the pragma rather than being contorted.
tds_line_is_short_deadline() {
    local line="$1"
    [[ $line == *"new CancellationTokenSource(TimeSpan.From"* ]] || return 1
    [[ $line =~ ^[[:space:]]*(//|///|\*) ]] && return 1
    [[ $line =~ //[[:space:]]*deadline-ok ]] && return 1
    # Milliseconds is always under the bar. Seconds: any SINGLE-DIGIT value.
    #
    # This was below 5, and 5 is exactly what got through. A subscription token carrying
    # FromSeconds(5) sat inside a test that then waited 30s scaled -- 90s on CI -- for a signal that
    # token had already made impossible. The polling interval was 50ms, so the window was ~100x what
    # it needed, and it still lost once on a loaded runner and took main red with it.
    #
    # Moving the bound to 5 would only relocate the same argument to 6. Under ten seconds is the
    # line, because the defect is not the specific number: it is an unexplained wall-clock deadline
    # in a test, and anything genuinely deliberate says so with `// deadline-ok:` and passes.
    [[ $line == *"TimeSpan.FromMilliseconds("* ]] && return 0
    [[ $line =~ TimeSpan\.FromSeconds\([0-9]\) ]] && return 0
    return 1
}

# ---------------------------------------------------------------------------
# Whole-tree scan
# ---------------------------------------------------------------------------

# tds_files <root> -> repo-relative paths of the C# files under tests/.
# git ls-files, so an untracked scratch file is out of scope: the gate judges committed content.
tds_files() {
    ( cd "$1" && git ls-files -- 'tests' 2>/dev/null | grep -E '\.cs$' )
}

# tds_hits <root> -> "path|line-text" for every live violation, DEDUPED.
#
# One key covers N byte-identical occurrences in the same file, so six identical `await
# Task.Delay(50);` in one file are one baseline entry. A line number would be a worse key: it churns
# on every unrelated edit above it, and the baseline would need regenerating constantly.
#
# The grep prefilter is not a second predicate -- it is a strict superset of what the two predicates
# below can match, so it only decides which lines are worth the (much slower) per-line evaluation.
tds_hits() {
    local root="$1" files
    files="$(tds_files "$root")"
    [ -n "$files" ] || return 0
    ( cd "$root" && printf '%s\n' "$files" | tr '\n' '\0' \
        | xargs -0 grep -HE 'Task\.Delay[[:space:]]*\(|new CancellationTokenSource\(TimeSpan\.From' 2>/dev/null ) \
    | while IFS= read -r raw; do
        local file line
        file="${raw%%:*}"
        line="${raw#*:}"
        tds_is_allowlisted_file "$file" && continue
        if tds_line_is_violation "$line" || tds_line_is_short_deadline "$line"; then
            printf '%s|%s\n' "$file" "$(printf '%s' "$line" | sed -E 's/^[[:space:]]+//; s/[[:space:]]+$//')"
        fi
    done | sort -u
}

tds_baseline_entries() {
    [ -f "$1" ] || return 0
    grep -vE '^[[:space:]]*(#|$)' "$1" 2>/dev/null || true
}

run_gate() {
    local root="$1" baseline="$2" report_only="$3"

    if [ -z "$root" ] || ! ( cd "$root" && git rev-parse --is-inside-work-tree >/dev/null 2>&1 ); then
        echo "task-delay-syncwait-gate: REFUSE — '$root' is not inside a git work tree." >&2
        return 2
    fi

    local files file_count line_count hits baselined
    files="$(tds_files "$root")"
    file_count="$(printf '%s' "$files" | grep -c . || true)"

    echo "=== task-delay sync-wait gate (whole-tree scan vs baseline) ==="

    # The DENOMINATOR. Unlike the diff this replaced, the population here is the TREE, so a zero is
    # never legitimate -- it means the path, the pathspec, or the repository changed underneath the
    # gate, and a clean verdict over nothing is the one outcome a gate must never produce.
    GATE_NAME="task-delay-syncwait-gate" gate_denominator "$file_count" "tests/**/*.cs file(s) scanned" || return 2

    line_count="$( ( cd "$root" && printf '%s\n' "$files" | tr '\n' '\0' | xargs -0 cat 2>/dev/null ) | wc -l | tr -d ' ' )"
    echo "EXAMINED: $line_count line(s) of test code"

    hits="$(tds_hits "$root")"
    baselined="$(tds_baseline_entries "$baseline")"

    # Set difference in both directions, one pass each: -F -x -v is "fixed strings, whole line,
    # inverted", so the first is exactly the hits matching no baseline entry and the second the
    # entries matching no hit. An empty pattern file matches nothing, which reports everything --
    # correct in both directions (no baseline => every hit is new; no hits => every entry is stale).
    local tmp_h tmp_b unbaselined stale
    tmp_h="$(mktemp)"; tmp_b="$(mktemp)"
    printf '%s\n' "$hits"      | grep -v '^$' > "$tmp_h"
    printf '%s\n' "$baselined" | grep -v '^$' > "$tmp_b"
    unbaselined="$(grep -Fxv -f "$tmp_b" "$tmp_h" || true)"
    stale="$(grep -Fxv -f "$tmp_h" "$tmp_b" || true)"
    rm -f "$tmp_h" "$tmp_b"

    local rc=0
    if [ -n "$unbaselined" ]; then
        rc=1
        echo "❌ FAIL — clock dependence in test code that is not in the baseline:"
        printf '%s\n' "$unbaselined" | sed 's/^/    /'
        echo ""
        echo "Tests MUST NOT synchronize on a fixed wall-clock delay before asserting a background"
        echo "condition (flaky under CI thread-pool starvation). Poll the real condition via"
        echo "  Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync / AwaitSignalAsync"
        echo "with a generous bounded timeout. See docs/testing/async-test-standards.md."
        echo ""
        echo "A CancellationTokenSource hit is a deadline the test must finish inside. Prefer a"
        echo "generous bound (30s is the shape most of this repo uses) or, better, cancel on the EVENT"
        echo "you are waiting for rather than after a duration. If the duration IS the semantic under"
        echo "test, say so:"
        echo "    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)); // deadline-ok: the batch timeout under test"
        echo ""
        echo "If this Task.Delay is a LEGITIMATE time-based semantic (lease-TTL expiry, distinct"
        echo "timestamps, simulated work in a fake, throughput pacing), append a justification:"
        echo "    await Task.Delay(50); // delay-ok: distinct-timestamp ordering, not a sync-wait"
        echo ""
        echo "The lines above are printed in baseline key form (<path>|<line>) so an ACKNOWLEDGED"
        echo "debt can be pasted into $(basename "$baseline") -- but removing the line is the fix."
    fi
    if [ -n "$stale" ]; then
        rc=1
        echo "❌ FAIL — stale baseline entries: these match no live hit and must be REMOVED." >&2
        printf '%s\n' "$stale" | sed 's/^/    /' >&2
        echo "A baseline that keeps dead entries can re-admit a violation under a recycled line." >&2
    fi

    if [ "$rc" -eq 0 ]; then
        local n
        n="$(printf '%s' "$hits" | grep -c . || true)"
        echo "✅ PASS — $n baselined hit(s); no new clock dependence, no stale entry."
    fi

    [ "$report_only" = "1" ] && return 0
    return "$rc"
}

update_baseline() {
    local root="$1" baseline="$2" hits
    hits="$(tds_hits "$root")"
    { cat <<'HDR'
# Clock dependence in test code that exists TODAY -- one entry per distinct violating line.
#
# Read eng/ci/task-delay-syncwait-gate.sh for why this file exists. In short: a test that
# synchronizes on a fixed `await Task.Delay(N)` before asserting a background condition, or gives
# itself a sub-ten-second CancellationTokenSource deadline to beat, is racing the CI runner. Under
# thread-pool starvation it loses and reports working code as broken.
#
# THIS FILE IS A DEBT LEDGER WITH A RATCHET, NOT AN ALLOWLIST.
#   - A hit NOT listed here is a FAIL on its first appearance.
#   - An entry listed here that matches no live hit is ALSO a FAIL -- remove it.
# So the list can only shrink. Removing a line is the fix; adding one should be argued, not assumed.
#
# THE FIX for an entry is to DELETE THE LINE IT NAMES: poll the real condition with
# Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync under a generous bounded timeout, or widen
# the deadline. Where the duration genuinely IS the semantic under test (a lease TTL, a batch
# timeout, distinct timestamps, simulated work), say so on the line with `// delay-ok:` or
# `// deadline-ok:` -- the predicates honor both, and the entry then leaves this file for free.
#
# FORMAT: <repo-relative-path>|<the line, leading and trailing whitespace stripped>
#
# ENTRIES ARE DEDUPED. One entry covers every byte-identical occurrence of that line in that file,
# which is why six identical `await Task.Delay(50);` calls in one file appear here once. A line
# number would have been a worse key: it churns on every unrelated edit above it.
#
# Regenerate with `task-delay-syncwait-gate.sh --update-baseline`. That is a DELIBERATE act whose
# diff is reviewed like any other change -- never a way to clear a red.
HDR
      printf '%s\n' "$hits"
    } > "$baseline"
    echo "wrote $(printf '%s' "$hits" | grep -c . || true) entry(ies) to $baseline"
}

# ---------------------------------------------------------------------------
# Self-test -- predicate arms, plus whole-tree arms over real temp fixture repositories.
# ---------------------------------------------------------------------------

self_test() {
    local pass=1

    # --- Predicate: line classification ------------------------------------
    if ! tds_line_is_violation '            await Task.Delay(1000);'; then
        echo "self-test FAIL: did not flag a bare 'await Task.Delay(1000);' sync-wait" >&2; pass=0
    fi
    if tds_line_is_violation '  var c = await Task.WhenAny(task, Task.Delay(timeout));'; then
        echo "self-test FAIL: flagged a Task.WhenAny(...Task.Delay...) timeout-guard" >&2; pass=0
    fi
    if tds_line_is_violation '        // await Task.Delay(200) replaced with poll'; then
        echo "self-test FAIL: flagged a comment line mentioning Task.Delay" >&2; pass=0
    fi
    if tds_line_is_violation '        await Task.Delay(10); // delay-ok: distinct timestamps'; then
        echo "self-test FAIL: flagged a line carrying the // delay-ok justification pragma" >&2; pass=0
    fi
    if tds_line_is_violation '        var found = await WaitHelpers.WaitUntilAsync(() => x, t);'; then
        echo "self-test FAIL: flagged a line with no Task.Delay" >&2; pass=0
    fi

    # --- Predicate: short wall-clock deadlines -----------------------------
    if ! tds_line_is_short_deadline '        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));'; then
        echo "self-test FAIL: did not flag a 50ms deadline, the exact shape that failed a shard" >&2; pass=0
    fi
    if ! tds_line_is_short_deadline '  var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));'; then
        echo "self-test FAIL: did not flag a 2s deadline" >&2; pass=0
    fi
    if ! tds_line_is_short_deadline '  using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));'; then
        echo "self-test FAIL: did not flag the 5s deadline that escaped the previous bound" >&2; pass=0
    fi
    if ! tds_line_is_short_deadline '  using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(9));'; then
        echo "self-test FAIL: did not flag a 9s deadline (single-digit seconds is the bar)" >&2; pass=0
    fi
    if tds_line_is_short_deadline '  using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));'; then
        echo "self-test FAIL: flagged a 10s deadline; the bar is single-digit seconds, not all seconds" >&2; pass=0
    fi
    if tds_line_is_short_deadline '  using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // deadline-ok: the timeout under test'; then
        echo "self-test FAIL: flagged a justified 5s deadline; the escape hatch must still work" >&2; pass=0
    fi
    if tds_line_is_short_deadline '        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));'; then
        echo "self-test FAIL: flagged a generous 30s bound, which is the recommended shape" >&2; pass=0
    fi
    if tds_line_is_short_deadline '  var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10)); // deadline-ok: batch timeout under test'; then
        echo "self-test FAIL: flagged a line carrying the deadline-ok justification" >&2; pass=0
    fi
    if tds_line_is_short_deadline '        // new CancellationTokenSource(TimeSpan.FromMilliseconds(50)) replaced by a signal'; then
        echo "self-test FAIL: flagged a comment" >&2; pass=0
    fi
    if tds_line_is_short_deadline '        var cts = new CancellationTokenSource();'; then
        echo "self-test FAIL: flagged an untimed token source, which is the fix not the defect" >&2; pass=0
    fi

    # --- File allowlist ----------------------------------------------------
    if ! tds_is_allowlisted_file 'tests/Shared/Tests.Shared/Infrastructure/WaitHelpers.cs'; then
        echo "self-test FAIL: WaitHelpers.cs not allowlisted" >&2; pass=0
    fi
    if ! tds_is_allowlisted_file 'tests/performance/Foo/BarShould.cs'; then
        echo "self-test FAIL: tests/performance/** not allowlisted" >&2; pass=0
    fi
    if tds_is_allowlisted_file 'tests/unit/Excalibur.Foo.Tests/BazShould.cs'; then
        echo "self-test FAIL: a normal unit test file was wrongly allowlisted" >&2; pass=0
    fi

    # --- Whole-tree arms over real fixture repositories --------------------
    local tmp
    tmp="$(mktemp -d)"
    # shellcheck disable=SC2064
    trap "rm -rf '$tmp'" RETURN

    tds_mkrepo() { # tds_mkrepo <dir>  -- commit whatever is there, so git ls-files sees it
        ( cd "$1" && git init -q && git config user.email t@t.local && git config user.name t \
            && git add -A && git commit -qm fixture ) >/dev/null 2>&1
    }
    arm() { # arm <name> <want-rc> <root> [baseline]
        local name="$1" want="$2" root="$3" got
        run_gate "$root" "${4:-/nonexistent-baseline}" "0" >/dev/null 2>&1
        got=$?
        if [ "$got" -ne "$want" ]; then
            echo "self-test FAIL: $name — expected exit $want, got $got" >&2
            pass=0
        fi
    }

    local sw="$tmp/syncwait" dl="$tmp/deadline" clean="$tmp/clean" perf="$tmp/perf" empty="$tmp/empty"
    local sw_line='public class BarShould { public async Task T() { await Task.Delay(1234); r.ShouldBe(1); } }'

    # LIVENESS 1 — an unbaselined planted sync-wait FAILS.
    mkdir -p "$sw/tests/unit/Foo.Tests"
    printf '%s\n' "$sw_line" > "$sw/tests/unit/Foo.Tests/BarShould.cs"
    tds_mkrepo "$sw"
    arm "an unbaselined sync-wait FAILS" 1 "$sw"

    # LIVENESS 2 — an unbaselined planted short CTS deadline FAILS.
    mkdir -p "$dl/tests/unit/Foo.Tests"
    printf 'public class BarShould { public async Task T() { using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)); } }\n' \
        > "$dl/tests/unit/Foo.Tests/BarShould.cs"
    tds_mkrepo "$dl"
    arm "an unbaselined 3s CancellationTokenSource deadline FAILS" 1 "$dl"

    # SAFETY 3 — the SAME tree PASSES once the hit is baselined.
    printf 'tests/unit/Foo.Tests/BarShould.cs|%s\n' "$sw_line" > "$tmp/bl.txt"
    arm "a baselined hit PASSES" 0 "$sw" "$tmp/bl.txt"

    # RATCHET 4 — a baseline entry matching no live hit FAILS.
    { printf 'tests/unit/Foo.Tests/BarShould.cs|%s\n' "$sw_line"
      printf 'tests/unit/Foo.Tests/GhostShould.cs|await Task.Delay(999);\n'; } > "$tmp/bl2.txt"
    arm "a stale baseline entry FAILS" 1 "$sw" "$tmp/bl2.txt"

    # SAFETY 5 — pragma / WhenAny / comment / generous-bound PASS with NO baseline at all.
    mkdir -p "$clean/tests/unit/Foo.Tests"
    cat > "$clean/tests/unit/Foo.Tests/BarShould.cs" <<'EOF'
public class BarShould
{
    public async Task T()
    {
        await Task.Delay(10); // delay-ok: distinct timestamps
        var c = await Task.WhenAny(work, Task.Delay(timeout));
        // await Task.Delay(200) replaced with a poll
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var found = await WaitHelpers.WaitUntilAsync(() => ready, t);
    }
}
EOF
    tds_mkrepo "$clean"
    arm "pragma / WhenAny / comment / generous-bound PASS with an empty baseline" 0 "$clean"

    # SAFETY 6 — an allowlisted directory is exempt even carrying a bare sync-wait.
    mkdir -p "$perf/tests/performance/Perf" "$perf/tests/unit/Foo.Tests"
    printf 'public class PacingShould { public async Task T() { await Task.Delay(100); } }\n' \
        > "$perf/tests/performance/Perf/PacingShould.cs"
    printf 'public class OkShould { }\n' > "$perf/tests/unit/Foo.Tests/OkShould.cs"
    tds_mkrepo "$perf"
    arm "a bare sync-wait under tests/performance/** is allowlisted, PASSES" 0 "$perf"

    # VACUITY 7 — a tree with no test files at all must REFUSE(2), never PASS(0).
    mkdir -p "$empty"
    printf 'root\n' > "$empty/README.md"
    tds_mkrepo "$empty"
    arm "zero files scanned REFUSES with exit 2 (a scan of nothing is not a pass)" 2 "$empty"

    if [ "$pass" -eq 1 ]; then
        echo "✅ task-delay-syncwait-gate self-test PASSED (flags an unbaselined sync-wait AND short deadline; honors the baseline; FAILS a stale entry; ignores allowlist/perf/pragma/WhenAny/comment/generous-bound; REFUSES a scan of zero files)."
        return 0
    fi
    echo "❌ task-delay-syncwait-gate self-test FAILED." >&2
    return 3
}

# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

usage() { sed -n '2,60p' "$0" | sed 's/^# \{0,1\}//'; }

main() {
    local report_only="0"
    while [ $# -gt 0 ]; do
        case "$1" in
            --self-test)       self_test; exit $? ;;
            --update-baseline) update_baseline "$REPO_ROOT" "$BASELINE_DEFAULT"; exit $? ;;
            --report-only)     report_only="1"; shift ;;
            -h|--help)         usage; exit 0 ;;
            *) echo "task-delay-syncwait-gate: REFUSE — unknown arg '$1'" >&2; usage; exit 2 ;;
        esac
    done
    run_gate "$REPO_ROOT" "$BASELINE_DEFAULT" "$report_only"
    exit $?
}

main "$@"
