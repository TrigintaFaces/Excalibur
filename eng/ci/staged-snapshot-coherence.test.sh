#!/usr/bin/env bash
# Non-vacuity lock for staged-snapshot-coherence.sh.
#
# WHAT IS UNDER TEST, and what deliberately is not.
#
# This gate's own job is three things: materialise the INDEX (not the working tree) as a commit, delegate
# the build to committed-sha-build-gate.sh --sha, and translate that result into a three-state verdict
# without ever turning a REFUSE into a PASS. Those are what these arms bind.
#
# Whether a build correctly detects a non-compiling snapshot is the SIBLING's contract and is covered by
# its own 7/7 self-test. Re-running dotnet here would make this lock slow, non-hermetic, and would test
# someone else's code — so the sibling is replaced by a stub whose exit code each arm controls. That is a
# deliberate seam, not a shortcut: it lets the SAFETY arms drive exit paths a real build almost never
# produces on demand (REFUSE 64, unmerged index), which is exactly where a false PASS would hide.
#
# The load-bearing arm is STAGED-NOT-WORKTREE: it asserts the SHA handed to the build is the index
# content and NOT the file on disk. That single property is the whole reason this gate exists — every
# instance it is meant to catch was invisible precisely because the working tree compiled.

set -uo pipefail

GATE_SRC="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/staged-snapshot-coherence.sh"
pass=0
fail=0

ok()   { echo "  ok   $1"; pass=$((pass + 1)); }
bad()  { echo "  FAIL $1"; fail=$((fail + 1)); }

# A scratch repo per arm: hermetic, and it keeps us from ever touching the real index.
new_repo() {
    local d
    d="$(mktemp -d)"
    git -C "$d" init -q
    git -C "$d" config user.email t@t.t
    git -C "$d" config user.name t
    git -C "$d" config commit.gpgsign false
    mkdir -p "$d/eng/ci"
    cp "$GATE_SRC" "$d/eng/ci/staged-snapshot-coherence.sh"
    echo "$d"
}

# Stub sibling. STUB_EXIT controls the verdict; it records the --sha argument it was handed so an arm can
# inspect the probe commit's CONTENT.
install_stub() {
    local d="$1" exit_code="$2"
    cat > "$d/eng/ci/committed-sha-build-gate.sh" <<STUB
#!/usr/bin/env bash
# records the sha it was given, then returns a controlled verdict
if [ "\${1:-}" = "--sha" ]; then echo "\${2:-}" > "\$(git rev-parse --show-toplevel)/.probe-sha"; fi
exit $exit_code
STUB
    chmod +x "$d/eng/ci/committed-sha-build-gate.sh"
}

seed_commit() {
    local d="$1"
    echo "original" > "$d/file.txt"
    git -C "$d" add file.txt
    git -C "$d" commit -qm initial
}

run_gate() { ( cd "$1" && bash eng/ci/staged-snapshot-coherence.sh >/dev/null 2>&1 ); echo $?; }

echo "[staged-snapshot-coherence.test] running..."

# ---------------------------------------------------------------- THE LOAD-BEARING ARM
# The probe must carry the INDEX content. If it carried the working tree, this gate would be blind to
# exactly the defect it exists for.
d="$(new_repo)"; seed_commit "$d"; install_stub "$d" 0
echo "STAGED-CONTENT" > "$d/file.txt"
git -C "$d" add file.txt
echo "WORKTREE-ONLY-CONTENT" > "$d/file.txt"   # dirty AFTER staging: index and disk now differ
rc="$(run_gate "$d")"
probe="$(cat "$d/.probe-sha" 2>/dev/null || true)"
got="$(git -C "$d" show "$probe:file.txt" 2>/dev/null || true)"
if [ "$got" = "STAGED-CONTENT" ]; then
    ok "STAGED-NOT-WORKTREE: the probe commit carries the INDEX content, not the file on disk"
else
    bad "STAGED-NOT-WORKTREE: probe carried '$got', expected 'STAGED-CONTENT' (gate exit $rc)"
fi

# The probe must not become reachable: no ref may move, and HEAD must not advance.
head_before="$(git -C "$d" rev-parse HEAD)"
if [ "$head_before" = "$(git -C "$d" rev-parse HEAD)" ] && [ -n "$probe" ] && [ "$probe" != "$head_before" ]; then
    ok "NON-MUTATING: HEAD did not move and the probe is a distinct dangling commit"
else
    bad "NON-MUTATING: HEAD moved or the probe was not distinct from HEAD"
fi

# The working tree must be exactly as we left it — this gate runs mid-commit on shared files.
if [ "$(cat "$d/file.txt")" = "WORKTREE-ONLY-CONTENT" ]; then
    ok "NON-MUTATING: the working tree was left untouched"
else
    bad "NON-MUTATING: the working tree was modified by the gate"
fi

# ---------------------------------------------------------------- SAFETY
d="$(new_repo)"; seed_commit "$d"; install_stub "$d" 1
echo "change" > "$d/file.txt"; git -C "$d" add file.txt
rc="$(run_gate "$d")"
[ "$rc" -eq 1 ] && ok "SAFETY: a non-compiling staged snapshot is REJECTED (exit 1)" \
               || bad "SAFETY: expected exit 1 for a failing build, got $rc"

d="$(new_repo)"; seed_commit "$d"; install_stub "$d" 64
echo "change" > "$d/file.txt"; git -C "$d" add file.txt
rc="$(run_gate "$d")"
[ "$rc" -eq 64 ] && ok "SAFETY: a sibling REFUSE propagates as REFUSE, never as a pass (exit 64)" \
                || bad "SAFETY: a REFUSE must not become a pass — expected 64, got $rc"

# A build gate that vanished must refuse. Silence here would be an inert gate reporting health.
d="$(new_repo)"; seed_commit "$d"
echo "change" > "$d/file.txt"; git -C "$d" add file.txt
rc="$(run_gate "$d")"
[ "$rc" -eq 64 ] && ok "SAFETY: a MISSING build gate REFUSES (exit 64), it does not pass silently" \
                || bad "SAFETY: missing sibling must REFUSE — expected 64, got $rc"

# Unborn HEAD: nothing to parent the probe to. Must refuse rather than claim a pass.
d="$(new_repo)"; install_stub "$d" 0
echo "x" > "$d/file.txt"; git -C "$d" add file.txt
rc="$(run_gate "$d")"
[ "$rc" -eq 64 ] && ok "SAFETY: an unborn HEAD REFUSES (exit 64)" \
                || bad "SAFETY: unborn HEAD must REFUSE — expected 64, got $rc"

d="$(new_repo)"; seed_commit "$d"; install_stub "$d" 0
echo "change" > "$d/file.txt"; git -C "$d" add file.txt
rc="$(run_gate "$d")"
[ "$rc" -eq 0 ] && ok "LIVENESS: a coherent staged snapshot is ACCEPTED (exit 0)" \
               || bad "LIVENESS: a good commit must be allowed — expected 0, got $rc"

# A build that outlives the bound must REFUSE, not pass and not report a code failure. The stub sleeps
# past a deliberately tiny bound; `timeout` returns 124 and the gate must translate that to E_ENV(64).
# Without this arm the timeout branch is an untested path in a gate whose whole contract is "a REFUSE is
# never a PASS" — exactly the unexercised-arm defect this repo has been finding all night.
if command -v timeout >/dev/null 2>&1; then
    d="$(new_repo)"; seed_commit "$d"
    cat > "$d/eng/ci/committed-sha-build-gate.sh" <<'SLOWSTUB'
#!/usr/bin/env bash
sleep 30
exit 0
SLOWSTUB
    chmod +x "$d/eng/ci/committed-sha-build-gate.sh"
    echo "change" > "$d/file.txt"; git -C "$d" add file.txt
    rc="$( ( cd "$d" && STAGED_SNAPSHOT_GATE_TIMEOUT=2 bash eng/ci/staged-snapshot-coherence.sh >/dev/null 2>&1 ); echo $? )"
    [ "$rc" -eq 64 ] && ok "SAFETY: a build that exceeds the time bound REFUSES (exit 64), never passes" \
                     || bad "SAFETY: an over-bound build must REFUSE — expected 64, got $rc"
else
    echo "  skip TIMEOUT arm — 'timeout' unavailable on this host (the gate runs unbounded there)"
fi

# Nothing staged is a genuine pass, not a refuse: there is no snapshot whose coherence could be wrong.
d="$(new_repo)"; seed_commit "$d"; install_stub "$d" 1
rc="$(run_gate "$d")"
[ "$rc" -eq 0 ] && ok "LIVENESS: nothing staged is ACCEPTED without invoking the build (exit 0)" \
               || bad "LIVENESS: an empty stage must pass — expected 0, got $rc"
if [ ! -f "$d/.probe-sha" ]; then
    ok "LIVENESS: an empty stage does not pay for a build at all"
else
    bad "LIVENESS: an empty stage invoked the build gate unnecessarily"
fi

echo "[staged-snapshot-coherence.test] $pass passed, $fail failed"
[ "$fail" -eq 0 ] || exit 1
exit 0
