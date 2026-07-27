#!/usr/bin/env bash
# no-beads-in-workflows.harness-lock.sh — w0jt1b regression lock (independent author≠impl).
#
# Operator directive: "anything related to beads must not be referenced in the github workflows."
# `.github/workflows/ci.yml` is COPIED to a downstream (public) repo. A beads/tracker reference there
# — a `bd` CLI call, a `.beads/` path, a private gate-script name, or a `bd-xxxxxx` bead id in a
# comment — both leaks internal tooling into a shipped file AND breaks the downstream checkout (the
# private scripts are not copied). The gate-selftests job that carried those references was REMOVED
# from ci.yml and its enforcement RELOCATED to the dev-only pre-commit hook. This lock guards that:
#
#   SAFETY   — grep every .github/workflows/** file for ANY beads/tracker reference -> ZERO hits.
#   LIVENESS — the relocated enforcement still FIRES on the dev side: eng/hooks/pre-commit invokes the
#              tracker/bd-durability gates. Absence-from-workflows ALONE is satisfiable by deleting all
#              enforcement; the liveness arm proves it MOVED, not vanished.
#   NON-VACUITY — the SAME safety grep on the pre-unwind ci.yml (commit a1d6041d8, which carried the
#              gate-selftests beads steps) -> RED. Proves the gate catches a reappearance.
#
# Scope is BEADS/tracker tokens only (per SA 28654). Bare sprint ids (S462.4 etc.) are the broader
# no-internal-refs concern (tracked separately as p1aw5t), NOT this lock's job.
#
# Run: bash eng/ci/no-beads-in-workflows.harness-lock.sh  (exit 0 = green; nonzero = a lock failed)

set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WF_DIR="${WORKFLOWS_DIR:-$ROOT/.github/workflows}"
PRECOMMIT="${PRECOMMIT_HOOK:-$ROOT/eng/hooks/pre-commit}"
PRE_UNWIND_REF="${PRE_UNWIND_REF:-a1d6041d8}"

# Beads/tracker reference patterns (ERE). A `bd` command, a .beads path, the private gate-script
# basenames, or a bd-xxxxxx bead id. `\bbd ` matches the CLI as a word (not "embed"/"forbid ").
BEADS_RE='(\bbd |\.beads/|bd-status-tokens|bd-export-comments|premise-triage|p0-(denominator|classify)|gate-wiring|bd-[a-z0-9]{6})'

# Mirror-exclusion tokens: the public mirror checkout EXCLUDES these private dirs, so ANY reference
# to one in a shipped workflow file-not-founds downstream — even a non-beads one. Widening the guard
# from "beads-scoped" to "mirror-exclusion-scoped" makes a future excluded-dir ref inexpressible at
# commit, not just a beads one. (PM 28694 item 4.)
EXCL_RE='(\.claude/|\.dts/|\.beads/)'

passed=0; failed=0
ok()  { printf '  ok  : %s\n' "$1"; passed=$((passed + 1)); }
bad() { printf '  FAIL: %s\n' "$1" >&2; failed=$((failed + 1)); }

[ -d "$WF_DIR" ] || { echo "FATAL: workflows dir not found at $WF_DIR" >&2; exit 3; }

echo "no-beads-in-workflows.harness-lock.sh — guarding $WF_DIR"
echo

# --- SAFETY 1: zero beads references anywhere under .github/workflows/ --------
hits="$(grep -rnE "$BEADS_RE" "$WF_DIR" 2>/dev/null || true)"
if [ -z "$hits" ]; then
    ok "SAFETY (beads): no beads/tracker reference in any .github/workflows/ file"
else
    bad "SAFETY (beads): beads/tracker reference(s) remain in a shipped workflow:"
    printf '%s\n' "$hits" | sed 's/^/         /' >&2
fi

# --- SAFETY 2: zero private-excluded-dir references (mirror-exclusion scope) --
excl_hits="$(grep -rnE "$EXCL_RE" "$WF_DIR" 2>/dev/null || true)"
if [ -z "$excl_hits" ]; then
    ok "SAFETY (mirror): no .claude/.dts/.beads reference in any .github/workflows/ file"
else
    bad "SAFETY (mirror): a workflow references a mirror-EXCLUDED private dir (file-not-found downstream):"
    printf '%s\n' "$excl_hits" | sed 's/^/         /' >&2
fi

# --- LIVENESS: the relocated enforcement fires in the dev-only pre-commit hook ---
# Absence-from-workflows is satisfiable by deleting all enforcement; prove it MOVED to the hook.
if [ ! -f "$PRECOMMIT" ]; then
    bad "LIVENESS: pre-commit hook not found at $PRECOMMIT — relocation target missing"
elif grep -qE '(bd-status-tokens|premise-triage|bd-file|bd sync|bd export|tracker|\.beads)' "$PRECOMMIT"; then
    ok "LIVENESS: the dev-only pre-commit hook still invokes the relocated tracker/bd enforcement"
else
    bad "LIVENESS: pre-commit hook invokes NO tracker/bd enforcement — enforcement vanished, not relocated"
fi

# --- NON-VACUITY: the same safety grep on the pre-unwind ci.yml must RED ------
pre_unwind_file="$(mktemp)"
git -C "$ROOT" show "$PRE_UNWIND_REF:.github/workflows/ci.yml" > "$pre_unwind_file" 2>/dev/null || true
if [ ! -s "$pre_unwind_file" ]; then
    # A missing anchor must not silently vacate the arm; flag it loudly (do not pass by default).
    bad "NON-VACUITY: could not read $PRE_UNWIND_REF:.github/workflows/ci.yml — cannot prove the grep bites"
else
    if grep -qE "$BEADS_RE" "$pre_unwind_file"; then
        ok "NON-VACUITY (beads): the pre-unwind ci.yml ($PRE_UNWIND_REF) DID carry beads refs -> the grep bites"
    else
        bad "NON-VACUITY (beads): the pre-unwind ci.yml had NO beads refs -> the safety grep is vacuous (wrong anchor?)"
    fi
    if grep -qE "$EXCL_RE" "$pre_unwind_file"; then
        ok "NON-VACUITY (mirror): the pre-unwind ci.yml ($PRE_UNWIND_REF) DID reference an excluded dir -> the grep bites"
    else
        bad "NON-VACUITY (mirror): the pre-unwind ci.yml had NO excluded-dir refs -> the mirror grep is vacuous (wrong anchor?)"
    fi
fi
rm -f "$pre_unwind_file"

echo
if [ "$failed" -eq 0 ]; then
    echo "=== $passed passed, 0 failed ==="
    echo "no-beads-in-workflows lock is GREEN"
    exit 0
fi
echo "=== $passed passed, $failed failed ===" >&2
exit 1
