#!/usr/bin/env bash
# task-delay-syncwait-gate — block NEW clock dependence in test code.
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
# This is the mechanical enforcement companion to the one-time sweep:
# a documented standard without a gate decays as new tests copy the flaky pattern.
#
# DESIGN — diff-based (catches NEW introductions only). Like f5-sweep.sh it inspects the
# ADDED lines of the change under test (not the whole tree), so the many LEGITIMATE
# pre-existing Task.Delay uses (perf pacing, work-simulation in fakes, lease-TTL /
# distinct-timestamp semantics, Task.WhenAny timeout-guards) are grandfathered and only
# a newly-added raw sync-wait fails the gate.
#
# A new Task.Delay in test code is allowed (not flagged) when ANY holds:
#   * the file is an allowlisted determinism-infra file (WaitHelpers/TestTiming/
#     TestTimeouts/ContainerFixtureBase — these legitimately implement the delay);
#   * the file is under tests/performance/** or benchmarks/** (throughput/rate pacing);
#   * the added line is a comment (// or ///);
#   * the added line is a Task.WhenAny(..., Task.Delay(...)) timeout-guard (bounded race,
#     not a sync-wait);
#   * the added line carries the explicit justification pragma `// delay-ok:` (author
#     asserts a legitimate time-based semantic — e.g. lease-TTL expiry, distinct
#     timestamps, simulated work — and states the reason).
#
# Exit codes:
#   0  no new raw sync-wait Task.Delay added (or --report-only)
#   1  new raw sync-wait Task.Delay added without justification (gate fail)
#   2  usage / environment error
#   3  --self-test failed (the gate itself is broken / vacuous)
#
# Usage:
#   task-delay-syncwait-gate.sh [--base <ref>] [--staged] [--working] [--all] [--report-only]
#   task-delay-syncwait-gate.sh --self-test
set -uo pipefail

# ---------------------------------------------------------------------------
# Core (pure, testable) predicate
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
# Operates on ONE added code line (without the leading diff '+').
tds_line_is_violation() {
    local line="$1"
    # Must contain a Task.Delay( call.
    printf '%s' "$line" | grep -qE '\bTask\.Delay[[:space:]]*\(' || return 1
    # Not a comment line.
    printf '%s' "$line" | grep -qE '^[[:space:]]*(//|///|\*)' && return 1
    # Not a Task.WhenAny(...Task.Delay...) timeout-guard.
    printf '%s' "$line" | grep -qE '\bWhenAny\b' && return 1
    # Not explicitly justified with the delay-ok pragma.
    printf '%s' "$line" | grep -qE '//[[:space:]]*delay-ok' && return 1
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
# THE BAR IS FIVE SECONDS, and it is a bar on NEW code only, like the rest of this gate. Under it,
# say why. Generous bounds are the recommended shape and the majority here already: 58 use 30s.
# A duration that IS the semantic under test -- a batch timeout, a lease TTL, a rate -- is
# legitimate and carries the pragma rather than being contorted.
tds_line_is_short_deadline() {
    local line="$1"
    printf '%s' "$line" | grep -qE 'new CancellationTokenSource\(TimeSpan\.From' || return 1
    printf '%s' "$line" | grep -qE '^[[:space:]]*(//|///|\*)' && return 1
    printf '%s' "$line" | grep -qE '//[[:space:]]*deadline-ok' && return 1
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
    printf '%s' "$line" | grep -qE 'TimeSpan\.FromMilliseconds\(' && return 0
    printf '%s' "$line" | grep -qE 'TimeSpan\.FromSeconds\([0-9]\)' && return 0
    return 1
}

# ---------------------------------------------------------------------------
# Diff plumbing
# ---------------------------------------------------------------------------

tds_diff_text() {
    local mode="$1" base="$2"
    case "$mode" in
        committed) git diff "$base...HEAD" -- 'tests/**/*.cs' ;;
        staged)    git diff --cached        -- 'tests/**/*.cs' ;;
        working)   git diff                 -- 'tests/**/*.cs' ;;
        all)
            git diff "$base...HEAD"  -- 'tests/**/*.cs'
            git diff --cached        -- 'tests/**/*.cs'
            git diff                 -- 'tests/**/*.cs'
            ;;
    esac 2>/dev/null
}

# tds_scan_diff  (stdin: a unified git diff)  -> prints "file:added-line" violations
# Tracks the current +++ file; for each added ('+', not '+++') line, applies the
# file allowlist and the line predicate.
tds_scan_diff() {
    awk '
        /^\+\+\+ / {
            f=$2; sub(/^b\//,"",f); sub(/^\.\//,"",f); next
        }
        /^--- / { next }
        /^\+/ {
            line=substr($0,2)
            print f "\x1f" line
        }
    ' | while IFS=$'\x1f' read -r file line; do
        [ -n "$file" ] || continue
        if tds_is_allowlisted_file "$file"; then continue; fi
        if tds_line_is_violation "$line"; then
            printf '%s:%s\n' "$file" "$(printf '%s' "$line" | sed -E 's/^[[:space:]]+//')"
        elif tds_line_is_short_deadline "$line"; then
            printf '%s:[deadline] %s\n' "$file" "$(printf '%s' "$line" | sed -E 's/^[[:space:]]+//')"
        fi
    done
}

run_gate() {
    local mode="$1" base="$2" report_only="$3"

    if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
        echo "task-delay-syncwait-gate: error — not inside a git work tree" >&2
        return 2
    fi

    # The base must exist before it is diffed against. git reports an unknown revision on stderr and
    # produces no output, and the diff plumbing below sends stderr to /dev/null -- so an unresolvable
    # base yielded an empty diff, an empty diff yielded no violations, and the gate printed a clean
    # pass having compared nothing at all. That is the one outcome a gate must never produce: a green
    # it did not earn. It happens in CI for ordinary reasons, not exotic ones -- a shallow clone that
    # never fetched the base, a force-push that orphaned it, a deleted branch.
    #
    # An unresolvable base is therefore a REFUSAL, not a pass and not a violation: nothing was
    # measured, so there is nothing to report either way. Only the modes that consume the base are
    # checked; --staged and --working diff against the index and the work tree and never touch it.
    case "$mode" in
        committed|all)
            if ! git rev-parse --verify --quiet "${base}^{commit}" >/dev/null 2>&1; then
                echo "task-delay-syncwait-gate: REFUSE — base '$base' does not resolve to a commit." >&2
                echo "  NOTHING was compared. This is not a pass: the gate could not run, which is a" >&2
                echo "  different outcome from running and finding nothing." >&2
                echo "  Usually a shallow clone (try: git fetch --unshallow, or fetch-depth: 0 in CI), a" >&2
                echo "  force-push that orphaned the ref, or a deleted branch." >&2
                return 2
            fi
            ;;
    esac

    local hits
    hits="$(tds_diff_text "$mode" "$base" | tds_scan_diff)"

    echo "=== task-delay sync-wait gate (mode=$mode, base=$base) ==="
    if [ -z "$hits" ]; then
        echo "✅ No NEW sync-wait or short deadline added in test code. Determinism gate clean."
        return 0
    fi

    echo "❌ NEW clock dependence added in test code (sync-wait, or a [deadline] under five seconds):"
    printf '%s\n' "$hits" | sed 's/^/    /'
    echo ""
    echo "Tests MUST NOT synchronize on a fixed wall-clock delay before asserting a background"
    echo "condition (flaky under CI thread-pool starvation). Poll the real condition via"
    echo "  Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync / AwaitSignalAsync"
    echo "with a generous bounded timeout. See docs/testing/async-test-standards.md."
    echo ""
    echo "A [deadline] line is a CancellationTokenSource the test must finish inside. Prefer a"
echo "generous bound (30s is the shape most of this repo uses) or, better, cancel on the EVENT"
echo "you are waiting for rather than after a duration. If the duration IS the semantic under"
echo "test, say so:"
echo "    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)); // deadline-ok: the batch timeout under test"
echo ""
echo "If this Task.Delay is a LEGITIMATE time-based semantic (lease-TTL expiry, distinct"
    echo "timestamps, simulated work in a fake, throughput pacing), append a justification:"
    echo "    await Task.Delay(50); // delay-ok: distinct-timestamp ordering, not a sync-wait"

    [ "$report_only" = "1" ] && return 0
    return 1
}

# ---------------------------------------------------------------------------
# Self-test (non-vacuous: MUST flag a planted sync-wait, MUST ignore the allowlist)
# ---------------------------------------------------------------------------

self_test() {
    local pass=1

    # --- Predicate: line classification ------------------------------------
    # MUST flag a bare sync-wait.
    if ! tds_line_is_violation '            await Task.Delay(1000);'; then
        echo "self-test FAIL: did not flag a bare 'await Task.Delay(1000);' sync-wait" >&2; pass=0
    fi
    # MUST ignore a WhenAny timeout-guard.
    if tds_line_is_violation '  var c = await Task.WhenAny(task, Task.Delay(timeout));'; then
        echo "self-test FAIL: flagged a Task.WhenAny(...Task.Delay...) timeout-guard" >&2; pass=0
    fi
    # MUST ignore a comment line.
    if tds_line_is_violation '        // await Task.Delay(200) replaced with poll'; then
        echo "self-test FAIL: flagged a comment line mentioning Task.Delay" >&2; pass=0
    fi
    # MUST ignore an explicitly justified line.
    if tds_line_is_violation '        await Task.Delay(10); // delay-ok: distinct timestamps'; then
        echo "self-test FAIL: flagged a line carrying the // delay-ok justification pragma" >&2; pass=0
    fi
    # MUST ignore a line with no Task.Delay at all.
    if tds_line_is_violation '        var found = await WaitHelpers.WaitUntilAsync(() => x, t);'; then
        echo "self-test FAIL: flagged a line with no Task.Delay" >&2; pass=0
    fi

    # --- Base resolution: an unresolvable base must REFUSE, never pass -----
    # No set -e here, so the non-zero return is captured rather than fatal.
    local base_rc
    run_gate "all" "0000000000000000000000000000000000000000" "0" >/dev/null 2>&1
    base_rc=$?
    if [ "$base_rc" -ne 2 ]; then
        echo "self-test FAIL: an unresolvable base returned $base_rc, expected 2 (REFUSE)." >&2
        echo "                A gate that cannot resolve its base has compared nothing, so a clean" >&2
        echo "                verdict from it is a green it did not earn." >&2
        pass=0
    fi

    # --- Predicate: short wall-clock deadlines -----------------------------
    if ! tds_line_is_short_deadline '        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));'; then
        echo "self-test FAIL: did not flag a 50ms deadline, the exact shape that failed a shard" >&2; pass=0
    fi
    if ! tds_line_is_short_deadline '  var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));'; then
        echo "self-test FAIL: did not flag a 2s deadline" >&2; pass=0
    fi
    # The one that got through: 5s was one second outside the old bound and it took main red.
    if ! tds_line_is_short_deadline '  using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));'; then
        echo "self-test FAIL: did not flag the 5s deadline that escaped the previous bound" >&2; pass=0
    fi
    if ! tds_line_is_short_deadline '  using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(9));'; then
        echo "self-test FAIL: did not flag a 9s deadline (single-digit seconds is the bar)" >&2; pass=0
    fi
    # LIVENESS at the boundary: ten seconds and above must still pass, or the bar is meaningless.
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

    # --- Whole-diff scan: planted violation + grandfathered allowlist ------
    local diff_fixture scan
    diff_fixture="$(cat <<'EOF'
diff --git a/tests/unit/Excalibur.Foo.Tests/NewFlakyShould.cs b/tests/unit/Excalibur.Foo.Tests/NewFlakyShould.cs
--- a/tests/unit/Excalibur.Foo.Tests/NewFlakyShould.cs
+++ b/tests/unit/Excalibur.Foo.Tests/NewFlakyShould.cs
@@ -1,3 +1,5 @@
     await svc.StartAsync(ct);
+        await Task.Delay(500);
+        result.ShouldBe(1);
diff --git a/tests/performance/Perf/PacingShould.cs b/tests/performance/Perf/PacingShould.cs
--- a/tests/performance/Perf/PacingShould.cs
+++ b/tests/performance/Perf/PacingShould.cs
@@ -1,2 +1,3 @@
+        await Task.Delay(100); // throughput pacing
diff --git a/tests/unit/Excalibur.Foo.Tests/JustifiedShould.cs b/tests/unit/Excalibur.Foo.Tests/JustifiedShould.cs
--- a/tests/unit/Excalibur.Foo.Tests/JustifiedShould.cs
+++ b/tests/unit/Excalibur.Foo.Tests/JustifiedShould.cs
@@ -1,2 +1,3 @@
+        await Task.Delay(10); // delay-ok: distinct timestamps
EOF
)"
    scan="$(printf '%s\n' "$diff_fixture" | tds_scan_diff)"

    # MUST flag the planted unit-test sync-wait.
    if ! printf '%s\n' "$scan" | grep -q 'tests/unit/Excalibur.Foo.Tests/NewFlakyShould.cs'; then
        echo "self-test FAIL: whole-diff scan did not flag the planted sync-wait in NewFlakyShould.cs" >&2; pass=0
    fi
    # MUST NOT flag the tests/performance addition (allowlisted dir).
    if printf '%s\n' "$scan" | grep -q 'tests/performance/'; then
        echo "self-test FAIL: whole-diff scan flagged a tests/performance addition (allowlisted)" >&2; pass=0
    fi
    # MUST NOT flag the justified addition.
    if printf '%s\n' "$scan" | grep -q 'JustifiedShould.cs'; then
        echo "self-test FAIL: whole-diff scan flagged a // delay-ok justified addition" >&2; pass=0
    fi

    if [ "$pass" -eq 1 ]; then
        echo "✅ task-delay-syncwait-gate self-test PASSED (flags planted sync-wait AND short deadline; ignores allowlist/perf/pragma/WhenAny/comment/generous-bound; REFUSES an unresolvable base)."
        return 0
    fi
    echo "❌ task-delay-syncwait-gate self-test FAILED." >&2
    return 3
}

# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

usage() { sed -n '2,32p' "$0" | sed 's/^# \{0,1\}//'; }

main() {
    local mode="committed" base="" report_only="0"
    while [ $# -gt 0 ]; do
        case "$1" in
            --self-test) self_test; exit $? ;;
            --base)      base="$2"; shift 2 ;;
            --committed) mode="committed"; shift ;;
            --staged)    mode="staged"; shift ;;
            --working)   mode="working"; shift ;;
            --all)       mode="all"; shift ;;
            --report-only) report_only="1"; shift ;;
            -h|--help)   usage; exit 0 ;;
            *) echo "task-delay-syncwait-gate: unknown arg '$1'" >&2; usage; exit 2 ;;
        esac
    done

    if [ -z "$base" ]; then
        base="$(git rev-parse HEAD~1 2>/dev/null || echo HEAD)"
    fi

    run_gate "$mode" "$base" "$report_only"
    exit $?
}

main "$@"
