#!/usr/bin/env bash
# Non-vacuous arms for eng/ci/lockfile-drift-gate.sh.
#
# Each arm builds a THROWAWAY git repository in a temp directory and runs the real
# gate inside it. Nothing here touches the host repository, so the arms are safe to
# run while other agents have uncommitted work — a test that mutated tracked files
# to prove a point would be the hazard it is testing for.
#
#   SAFETY   a drifted lock file FAILS                        exit 1
#   LIVENESS an undrifted lock file PASSES                    exit 0
#   REFUSE   no tracked lock files exits 2, never 0           exit 2
#   SCOPE    worktree clones are not counted as coverage
#
# Exit codes are captured on the line after the call, never through a pipe: a
# pipeline reports its LAST command's status, which would mask the value under test.

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# GATE_PATH lets these arms run against a deliberately mutated copy, so the suite
# can be shown to go RED. A suite never shown to fail is not evidence.
GATE="${LOCKFILE_GATE_PATH:-$SCRIPT_DIR/lockfile-drift-gate.sh}"

PASSED=0; FAILED=0; EXECUTED=0
pass() { printf '  \033[0;32mPASS\033[0m  %s\n' "$1"; PASSED=$((PASSED + 1)); }
fail() { printf '  \033[0;31mFAIL\033[0m  %s\n' "$1"; FAILED=$((FAILED + 1)); }

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# new_repo <name> -> path to a fresh git repo with one commit
new_repo() {
	local d="$WORK/$1"
	mkdir -p "$d"
	git -C "$d" init -q
	git -C "$d" config user.email t@example.com
	git -C "$d" config user.name t
	echo "root" >"$d/README.md"
	git -C "$d" add -A >/dev/null 2>&1
	git -C "$d" commit -qm init >/dev/null 2>&1
	echo "$d"
}

run_gate() { # <repo> ; sets RC
	EXECUTED=$((EXECUTED + 1))
	( cd "$1" && bash "$GATE" >"$WORK/out.log" 2>&1 )
	RC=$?
}

echo "lockfile-drift-gate.sh — three-state contract"
echo

# ---- REFUSE: repo with no tracked lock files ---------------------------------------
R="$(new_repo refuse)"
run_gate "$R"
[ "$RC" -eq 2 ] && pass "REFUSE: no tracked lock files (exit 2, not 0)" \
	|| { fail "REFUSE arm — expected 2, got $RC"; sed 's/^/        /' "$WORK/out.log"; }

# ---- LIVENESS: committed, unmodified lock file --------------------------------------
# Without this arm, a gate that refused or failed unconditionally would look correct.
R="$(new_repo clean)"
mkdir -p "$R/proj"
echo '{"version":1,"dependencies":{"net10.0":{}}}' >"$R/proj/packages.lock.json"
git -C "$R" add -A >/dev/null 2>&1; git -C "$R" commit -qm lock >/dev/null 2>&1
run_gate "$R"
[ "$RC" -eq 0 ] && pass "LIVENESS: committed, unmodified lock file PASSES (exit 0)" \
	|| { fail "LIVENESS arm — expected 0, got $RC"; sed 's/^/        /' "$WORK/out.log"; }

# ---- SAFETY: the same repo, lock file rewritten -------------------------------------
echo '{"version":1,"dependencies":{"net10.0":{"Pkg":{"resolved":"9.9.9"}}}}' >"$R/proj/packages.lock.json"
run_gate "$R"
[ "$RC" -eq 1 ] && pass "SAFETY: rewritten lock file FAILS (exit 1)" \
	|| { fail "SAFETY arm — expected 1, got $RC"; sed 's/^/        /' "$WORK/out.log"; }

# ---- SCOPE: a worktree clone must not count as coverage -----------------------------
# If clones counted, a repo with zero real lock files would report PASS on the
# strength of throwaway copies — coverage theatre.
R="$(new_repo clones)"
mkdir -p "$R/.claude/worktrees/agent-x/proj" "$R/.dts/wt-y/proj"
echo '{}' >"$R/.claude/worktrees/agent-x/proj/packages.lock.json"
echo '{}' >"$R/.dts/wt-y/proj/packages.lock.json"
git -C "$R" add -A >/dev/null 2>&1; git -C "$R" commit -qm clones >/dev/null 2>&1
run_gate "$R"
[ "$RC" -eq 2 ] && pass "SCOPE: worktree clones do not count as coverage (exit 2)" \
	|| { fail "SCOPE arm — expected 2, got $RC"; sed 's/^/        /' "$WORK/out.log"; }

# ---- Reported count must match reality ----------------------------------------------
R="$(new_repo counted)"
mkdir -p "$R/a" "$R/b"
echo '{}' >"$R/a/packages.lock.json"; echo '{}' >"$R/b/packages.lock.json"
git -C "$R" add -A >/dev/null 2>&1; git -C "$R" commit -qm two >/dev/null 2>&1
run_gate "$R"
EXECUTED=$((EXECUTED + 1))
if grep -q "lock files CHECKED : 2" "$WORK/out.log"; then
	pass "REPORTING: verdict states how many files it actually checked"
else
	fail "REPORTING: expected 'lock files CHECKED : 2'"; sed 's/^/        /' "$WORK/out.log"
fi

echo
# Reported separately on purpose: one gate invocation can back zero or one
# assertion, and collapsing them would let the headline number drift from the
# number of things actually checked.
echo "  gate invocations: $EXECUTED   assertions EXECUTED: $((PASSED + FAILED))   passed: $PASSED   failed: $FAILED"
[ "$FAILED" -eq 0 ] || exit 1
echo "  ALL GREEN"
