#!/usr/bin/env bash
# task-delay-syncwait-gate.test.sh — non-vacuous self-test for task-delay-syncwait-gate.sh.
#
# The gate blocks a NEW raw `await Task.Delay(...)` sync-wait ADDED in tests/**/*.cs (via git diff,
# committed scope base...HEAD). Exempt: a `// delay-ok` justification, a Task.WhenAny(...Task.Delay...)
# timeout-guard, a comment, and allowlisted determinism-infra / tests/performance / benchmarks files.
#
#   SAFETY    a diff that ADDS `await Task.Delay(1000);` to a unit test  -> exit 1  (violation found)
#   LIVENESS  a diff that adds a WaitUntilAsync poll (no Task.Delay)     -> exit 0  (clean)
#   EDGE      a diff that adds `await Task.Delay(10); // delay-ok: ...`  -> exit 0  (justified)
#   EDGE      a diff that adds `await Task.WhenAny(t, Task.Delay(to));`  -> exit 0  (timeout-guard)
#   EDGE      a Task.Delay ADDED under tests/performance/**              -> exit 0  (allowlisted dir)
#   ENV       run outside a git work tree                               -> exit 2  (not a silent pass)
#
# Every arm drives the REAL gate over an ISOLATED temp git repo with a real base→HEAD commit, asserting
# an EXACT exit code — so a gate that flagged everything (or nothing) is caught.
#
# TDS_GATE_PATH overrides the gate under test so a non-vacuity proof can point these arms at a mutated
# copy (a suite never shown to go RED is not evidence).
#
# Usage: bash eng/ci/task-delay-syncwait-gate.test.sh   (exit 0 = all green)

set -uo pipefail

# ── Git-env isolation (xy3hze) — MUST precede the first git call ────────────────────────────────
# git EXPORTS GIT_INDEX_FILE / GIT_DIR / GIT_WORK_TREE into every hook and every child process. This
# script `git init`s its own throwaway fixture repos — an inherited GIT_INDEX_FILE is absolute and WINS
# over the fixture, so `git add` would write the CALLER'S index. Unset before the first git call.
unset GIT_INDEX_FILE GIT_DIR GIT_WORK_TREE GIT_OBJECT_DIRECTORY GIT_COMMON_DIR

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GATE="${TDS_GATE_PATH:-$SCRIPT_DIR/task-delay-syncwait-gate.sh}"

PASS=0; FAIL=0
ok()  { PASS=$((PASS + 1)); printf '  PASS  %s\n' "$1"; }
bad() { FAIL=$((FAIL + 1)); printf '  FAIL  %s\n     -> %s\n' "$1" "$2" >&2; }

[ -f "$GATE" ] || { printf 'FATAL: gate not found at %s\n' "$GATE" >&2; exit 3; }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK" 2>/dev/null || true' EXIT

# make_fixture <mode> -> path to a git repo whose HEAD commit ADDS a test file for that mode.
#   The base commit has only a README; the second (HEAD) commit adds the mode's test file, so
#   `git diff HEAD~1...HEAD` is exactly the added test lines the gate inspects.
#     violation | clean | pragma | whenany | perf
make_fixture() {
    local mode="$1" dir; dir="$WORK/$mode"
    rm -rf "$dir"; mkdir -p "$dir"
    (
        cd "$dir" || exit 1
        git init -q
        git config user.email tds@test.local
        git config user.name  tds-test
        echo root > README.md
        git add -A && git commit -qm base

        local unit="tests/unit/Foo.Tests"; mkdir -p "$unit"
        case "$mode" in
            violation)
                printf 'public class BarShould\n{\n    public async Task T()\n    {\n        await Svc.StartAsync();\n        await Task.Delay(1000);\n        result.ShouldBe(1);\n    }\n}\n' > "$unit/BarShould.cs" ;;
            clean)
                printf 'public class BarShould\n{\n    public async Task T()\n    {\n        await Svc.StartAsync();\n        var found = await WaitHelpers.WaitUntilAsync(() => ready, timeout);\n        found.ShouldBeTrue();\n    }\n}\n' > "$unit/BarShould.cs" ;;
            pragma)
                printf 'public class BarShould\n{\n    public async Task T()\n    {\n        await Task.Delay(10); // delay-ok: distinct timestamps, not a sync-wait\n        a.ShouldBeLessThan(b);\n    }\n}\n' > "$unit/BarShould.cs" ;;
            whenany)
                printf 'public class BarShould\n{\n    public async Task T()\n    {\n        var c = await Task.WhenAny(work, Task.Delay(timeout));\n        c.ShouldBe(work);\n    }\n}\n' > "$unit/BarShould.cs" ;;
            perf)
                mkdir -p "tests/performance/Perf"
                printf 'public class PacingShould\n{\n    public async Task T()\n    {\n        await Task.Delay(100);\n        throughput.ShouldBeGreaterThan(1000);\n    }\n}\n' > "tests/performance/Perf/PacingShould.cs" ;;
        esac
        git add -A && git commit -qm change
    ) >/dev/null 2>&1
    printf '%s' "$dir"
}

run() {  # $1 = fixture dir. Runs the REAL gate over base...HEAD inside it. Sets RC.
    ( cd "$1" && bash "$GATE" --base HEAD~1 ) >/dev/null 2>&1
    RC=$?
}

echo "task-delay-syncwait-gate.sh — self-test"
echo

d="$(make_fixture violation)"; run "$d"
[ "$RC" -eq 1 ] && ok "safety: a NEW 'await Task.Delay(1000);' added to a unit test is REJECTED (exit 1)" \
                || bad "safety: a new raw sync-wait must be rejected with exit 1" "got exit $RC"

d="$(make_fixture clean)"; run "$d"
[ "$RC" -eq 0 ] && ok "liveness: a diff adding a WaitUntilAsync poll (no Task.Delay) is accepted (exit 0)" \
                || bad "liveness: a clean diff must be accepted (exit 0)" "got exit $RC"

d="$(make_fixture pragma)"; run "$d"
[ "$RC" -eq 0 ] && ok "edge: a Task.Delay carrying '// delay-ok' is justified → clean (exit 0)" \
                || bad "edge: a // delay-ok line must exit 0" "got exit $RC"

d="$(make_fixture whenany)"; run "$d"
[ "$RC" -eq 0 ] && ok "edge: a Task.WhenAny(..., Task.Delay(...)) timeout-guard is not a sync-wait (exit 0)" \
                || bad "edge: a WhenAny timeout-guard must exit 0" "got exit $RC"

d="$(make_fixture perf)"; run "$d"
[ "$RC" -eq 0 ] && ok "edge: a Task.Delay added under tests/performance/** is allowlisted (exit 0)" \
                || bad "edge: an allowlisted perf-dir Task.Delay must exit 0" "got exit $RC"

# ENV: outside a git work tree the gate must refuse (exit 2), never a silent pass.
envdir="$WORK/not-a-repo"; mkdir -p "$envdir"
( cd "$envdir" && bash "$GATE" --base HEAD~1 ) >/dev/null 2>&1; RC=$?
[ "$RC" -eq 2 ] && ok "env: outside a git work tree the gate exits E_ENV(2), not a silent pass" \
                || bad "env: non-git tree must exit 2" "got exit $RC"

echo
printf 'passed %d · failed %d\n' "$PASS" "$FAIL"
if [ "$FAIL" -eq 0 ]; then echo "  ALL GREEN"; else exit 1; fi
