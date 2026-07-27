#!/usr/bin/env bash
# task-delay-syncwait-gate — block NEW raw `await Task.Delay(...)` sync-waits in test code.
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
        fi
    done
}

run_gate() {
    local mode="$1" base="$2" report_only="$3"

    if ! git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
        echo "task-delay-syncwait-gate: error — not inside a git work tree" >&2
        return 2
    fi

    local hits
    hits="$(tds_diff_text "$mode" "$base" | tds_scan_diff)"

    echo "=== task-delay sync-wait gate (mode=$mode, base=$base) ==="
    if [ -z "$hits" ]; then
        echo "✅ No NEW raw sync-wait Task.Delay added in test code. Determinism gate clean."
        return 0
    fi

    echo "❌ NEW raw sync-wait Task.Delay(...) added in test code:"
    printf '%s\n' "$hits" | sed 's/^/    /'
    echo ""
    echo "Tests MUST NOT synchronize on a fixed wall-clock delay before asserting a background"
    echo "condition (flaky under CI thread-pool starvation). Poll the real condition via"
    echo "  Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync / AwaitSignalAsync"
    echo "with a generous bounded timeout. See docs/testing/async-test-standards.md."
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
        echo "✅ task-delay-syncwait-gate self-test PASSED (flags planted sync-wait; ignores allowlist/perf/pragma/WhenAny/comment)."
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
