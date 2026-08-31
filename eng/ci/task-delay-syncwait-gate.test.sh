#!/usr/bin/env bash
# task-delay-syncwait-gate.test.sh — non-vacuous harness lock for task-delay-syncwait-gate.sh.
#
# The gate scans EVERY tests/**/*.cs in the tree (git ls-files) for a raw `await Task.Delay(...)`
# sync-wait or a sub-ten-second CancellationTokenSource deadline, and compares the hits against a
# shrink-only baseline. Exempt: a `// delay-ok` / `// deadline-ok` justification, a
# Task.WhenAny(...Task.Delay...) timeout-guard, a comment, allowlisted determinism-infra /
# tests/performance / benchmarks files, and any hit enumerated in the baseline.
#
# It used to diff HEAD~1...HEAD. It no longer does, because on a squashed-release mirror HEAD~1 is
# the previous RELEASE and the "added lines" are the entire release -- so these arms build plain
# trees plus a baseline file, and there is no base to pass.
#
#   LIVENESS  an unbaselined sync-wait                      -> exit 1
#   LIVENESS  an unbaselined sub-10s CTS deadline           -> exit 1
#   SAFETY    the same hit, baselined                       -> exit 0
#   RATCHET   a baseline entry matching no live hit         -> exit 1
#   SAFETY    delay-ok / WhenAny / comment / 30s bound      -> exit 0
#   SAFETY    a sync-wait under tests/performance/**        -> exit 0
#   VACUITY   a tree with no test files at all              -> exit 2 (REFUSE, never a pass)
#   ENV       run outside a git work tree                   -> exit 2 (not a silent pass)
#   DEDUP     N identical lines in one file                 -> ONE baseline entry
#
# MUTATION PROOF. An arm that only ever exits 0 today proves nothing, so each safety/liveness arm is
# re-run against a MUTATED COPY of the gate whose logic is broken in the one way that arm exists to
# catch, and the arm must flip. A suite never shown to go RED is not evidence.
#
# TDS_GATE_PATH overrides the gate under test (that is how the mutation arms point at the copies).
#
# Usage: bash eng/ci/task-delay-syncwait-gate.test.sh   (exit 0 = all green)

set -uo pipefail

# ── Git-env isolation — MUST precede the first git call ─────────────────────────────────────────
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

SYNCWAIT_LINE='public class BarShould { public async Task T() { await Task.Delay(1234); r.ShouldBe(1); } }'

# make_fixture <mode> -> path to a git repo whose committed tree carries that mode's test file.
# The gate reads `git ls-files`, so the fixture must be COMMITTED, not merely written.
make_fixture() {
    local mode="$1" dir; dir="$WORK/$mode"
    rm -rf "$dir"; mkdir -p "$dir/tests/unit/Foo.Tests"
    case "$mode" in
        violation)
            printf '%s\n' "$SYNCWAIT_LINE" > "$dir/tests/unit/Foo.Tests/BarShould.cs" ;;
        deadline)
            printf 'public class BarShould { public async Task T() { using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)); } }\n' \
                > "$dir/tests/unit/Foo.Tests/BarShould.cs" ;;
        clean)
            cat > "$dir/tests/unit/Foo.Tests/BarShould.cs" <<'EOF'
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
            ;;
        perf)
            mkdir -p "$dir/tests/performance/Perf"
            printf 'public class PacingShould { public async Task T() { await Task.Delay(100); } }\n' \
                > "$dir/tests/performance/Perf/PacingShould.cs"
            printf 'public class OkShould { }\n' > "$dir/tests/unit/Foo.Tests/OkShould.cs" ;;
        dedup)
            # SIX byte-identical sync-waits in ONE file. The baseline key is (path, stripped text),
            # so they must collapse to a single entry -- that dedup is the reason the baseline is
            # readable at all, and a keying regression would silently inflate it sixfold.
            { echo 'public class BarShould { public async Task T() {'
              for _ in 1 2 3 4 5 6; do echo '        await Task.Delay(50);'; done
              echo '} }'; } > "$dir/tests/unit/Foo.Tests/BarShould.cs" ;;
        empty)
            rm -rf "$dir/tests"; printf 'root\n' > "$dir/README.md" ;;
    esac
    ( cd "$dir" && git init -q && git config user.email tds@test.local && git config user.name tds-test \
        && git add -A && git commit -qm fixture ) >/dev/null 2>&1
    printf '%s' "$dir"
}

# run <fixture-dir> [baseline-file] [gate] -- sets RC and OUT.
run() {
    local root="$1" bl="${2:-}" gate="${3:-$GATE}"
    if [ -n "$bl" ]; then
        OUT="$( cd "$root" && TDS_BASELINE_OVERRIDE="$bl" bash "$gate" 2>&1 )"
    else
        OUT="$( cd "$root" && TDS_BASELINE_OVERRIDE="$WORK/empty-baseline.txt" bash "$gate" 2>&1 )"
    fi
    RC=$?
}

: > "$WORK/empty-baseline.txt"

# mutate <name> <sed-expression> -> path to a copy of the gate with its logic broken.
# The mutation must be in the gate's LOGIC, not its output, or the arm proves nothing.
mutate() {
    local name="$1"
    local expr="$2"
    local out="$WORK/mutant-$name.sh"
    # The gate sources gate-denominator.sh from ITS OWN directory, so a mutant living in $WORK needs
    # a copy beside it. Without this every mutant exits 2 (REFUSE, helper missing) and every mutation
    # arm "flips" for a reason that has nothing to do with the logic under test.
    cp "$SCRIPT_DIR/gate-denominator.sh" "$WORK/gate-denominator.sh"
    sed "$expr" "$GATE" > "$out"
    printf '%s' "$out"
}

echo "task-delay-syncwait-gate.sh — harness lock (whole-tree scan vs baseline)"
echo

# ── LIVENESS: an unbaselined sync-wait FAILS ────────────────────────────────────────────────────
V="$(make_fixture violation)"; run "$V"
[ "$RC" -eq 1 ] && ok "liveness: an unbaselined 'await Task.Delay(1234);' is REJECTED (exit 1)" \
                || bad "liveness: an unbaselined sync-wait must exit 1" "got exit $RC"$'\n'"$OUT"

# MUTATION PROOF for that arm: neuter the sync-wait predicate; the arm must stop failing.
M="$(mutate nodelay 's#^    \[\[ \$line =~ (\^|\[\^A-Za-z0-9_\])Task.*$#    return 1#')"
run "$V" "" "$M"
[ "$RC" -eq 0 ] && ok "mutation: with the sync-wait predicate broken the same tree PASSES → the arm above is real" \
                || bad "mutation: a broken sync-wait predicate should have flipped the violation arm to 0" "got exit $RC"

# ── LIVENESS: an unbaselined short CTS deadline FAILS ───────────────────────────────────────────
D="$(make_fixture deadline)"; run "$D"
[ "$RC" -eq 1 ] && ok "liveness: an unbaselined 'new CancellationTokenSource(TimeSpan.FromSeconds(3))' is REJECTED (exit 1)" \
                || bad "liveness: an unbaselined short deadline must exit 1" "got exit $RC"$'\n'"$OUT"

M="$(mutate nodeadline 's#^    \[\[ \$line == \*"new CancellationTokenSource.*$#    return 1#')"
run "$D" "" "$M"
[ "$RC" -eq 0 ] && ok "mutation: with the deadline predicate broken the same tree PASSES → the arm above is real" \
                || bad "mutation: a broken deadline predicate should have flipped the deadline arm to 0" "got exit $RC"

# ── SAFETY: the same hit PASSES once baselined ──────────────────────────────────────────────────
printf 'tests/unit/Foo.Tests/BarShould.cs|%s\n' "$SYNCWAIT_LINE" > "$WORK/bl-ok.txt"
run "$V" "$WORK/bl-ok.txt"
[ "$RC" -eq 0 ] && ok "safety: a baselined hit is accepted (exit 0) — the gate is wirable on a tree with known debt" \
                || bad "safety: a baselined hit must exit 0" "got exit $RC"$'\n'"$OUT"

# MUTATION PROOF: make the baseline reader return nothing; the baselined tree must go RED.
M="$(mutate nobaseline 's#^tds_baseline_entries() {#tds_baseline_entries() { return 0;#')"
run "$V" "$WORK/bl-ok.txt" "$M"
[ "$RC" -eq 1 ] && ok "mutation: with the baseline ignored the baselined tree FAILS → the baseline is really consulted" \
                || bad "mutation: ignoring the baseline should have flipped the baselined arm to 1" "got exit $RC"

# ── RATCHET: a baseline entry matching no live hit FAILS ────────────────────────────────────────
{ printf 'tests/unit/Foo.Tests/BarShould.cs|%s\n' "$SYNCWAIT_LINE"
  printf 'tests/unit/Foo.Tests/GhostShould.cs|await Task.Delay(999);\n'; } > "$WORK/bl-stale.txt"
run "$V" "$WORK/bl-stale.txt"
[ "$RC" -eq 1 ] && ok "ratchet: a stale baseline entry is REJECTED (exit 1) — the list can only shrink" \
                || bad "ratchet: a stale baseline entry must exit 1" "got exit $RC"$'\n'"$OUT"
case "$OUT" in
    *GhostShould*) ok "ratchet: the failure NAMES the stale entry, so it can be removed without a hunt" ;;
    *)             bad "ratchet: the stale entry must be printed" "output did not name GhostShould" ;;
esac

# MUTATION PROOF: drop the stale-detection half; the ratchet arm must go green.
M="$(mutate nostale 's#^    stale="\$(grep -Fxv -f "\$tmp_h" "\$tmp_b" || true)"#    stale=""#')"
run "$V" "$WORK/bl-stale.txt" "$M"
[ "$RC" -eq 0 ] && ok "mutation: with stale-detection removed the ratchet arm PASSES → the ratchet is real" \
                || bad "mutation: removing stale-detection should have flipped the ratchet arm to 0" "got exit $RC"

# ── SAFETY: the justified / guarded / commented / generous shapes ───────────────────────────────
C="$(make_fixture clean)"; run "$C"
[ "$RC" -eq 0 ] && ok "safety: delay-ok pragma, WhenAny guard, comment and a 30s bound are all accepted (exit 0)" \
                || bad "safety: the exempt shapes must exit 0 with no baseline" "got exit $RC"$'\n'"$OUT"

# ── SAFETY: allowlisted directory ───────────────────────────────────────────────────────────────
P="$(make_fixture perf)"; run "$P"
[ "$RC" -eq 0 ] && ok "safety: a bare sync-wait under tests/performance/** is allowlisted (exit 0)" \
                || bad "safety: an allowlisted perf-dir sync-wait must exit 0" "got exit $RC"$'\n'"$OUT"

M="$(mutate noallowlist 's#^tds_is_allowlisted_file() {#tds_is_allowlisted_file() { return 1;#')"
run "$P" "" "$M"
[ "$RC" -eq 1 ] && ok "mutation: with the allowlist disabled the perf tree FAILS → the exemption is really applied" \
                || bad "mutation: disabling the allowlist should have flipped the perf arm to 1" "got exit $RC"

# ── VACUITY: a scan of zero files must REFUSE, never pass ───────────────────────────────────────
E="$(make_fixture empty)"; run "$E"
[ "$RC" -eq 2 ] && ok "vacuity: a tree with no tests/**/*.cs REFUSES (exit 2) — a scan of nothing is not a pass" \
                || bad "vacuity: zero files scanned must exit 2" "got exit $RC"$'\n'"$OUT"

# ── ENV: outside a git work tree ────────────────────────────────────────────────────────────────
envdir="$WORK/not-a-repo"; mkdir -p "$envdir"
OUT="$( cd "$envdir" && bash "$GATE" 2>&1 )"; RC=$?
[ "$RC" -eq 2 ] && ok "env: outside a git work tree the gate REFUSES (exit 2), not a silent pass" \
                || bad "env: a non-git tree must exit 2" "got exit $RC"$'\n'"$OUT"

# ── DEDUP: N identical lines in one file collapse to ONE baseline entry ─────────────────────────
DD="$(make_fixture dedup)"
OUT="$( cd "$DD" && TDS_BASELINE_OVERRIDE="$WORK/empty-baseline.txt" bash "$GATE" 2>&1 )"
n="$(printf '%s\n' "$OUT" | grep -c 'BarShould\.cs|await Task\.Delay(50);' || true)"
[ "$n" -eq 1 ] && ok "dedup: six identical sync-waits in one file report as ONE entry, not six" \
               || bad "dedup: identical lines must collapse to one key" "reported $n time(s)"

# ── The gate's own self-test must be green ──────────────────────────────────────────────────────
bash "$GATE" --self-test >/dev/null 2>&1; RC=$?
[ "$RC" -eq 0 ] && ok "self-test: the gate's own --self-test passes (exit 0)" \
                || bad "self-test: the gate's --self-test must pass" "got exit $RC"

echo
printf 'passed %d · failed %d\n' "$PASS" "$FAIL"
if [ "$FAIL" -eq 0 ]; then echo "  ALL GREEN"; else exit 1; fi
