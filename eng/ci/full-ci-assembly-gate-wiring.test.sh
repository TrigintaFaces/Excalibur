#!/usr/bin/env bash
# full-ci-assembly-gate-wiring.test.sh — the assembly-completeness gate must have a CALLER.
#
# WHY THIS EXISTS
# ---------------
# `full-ci-shard-completeness.sh` gained `--assemblies` / `--assembly-results` with an 11-arm
# self-test that passed 11/11 — and ZERO callers. Two reviewers found it independently within
# minutes of each other.
#
# That combination is worse than a plain unwired gate, and this is the lesson worth keeping:
#
#     a WIRED SELF-TEST in front of an UNWIRED GATE manufactures the belief the gate is live.
#
# CI ran `--self-test`, printed green, and that green read as "the assembly check is working."
# It was not working. It could not fire. Nothing in the repo invoked its production function.
# A self-test proves a gate CAN detect; only a caller makes it detect ANYTHING.
#
# So this lock asserts the one property the self-test structurally cannot: that the runner which
# produces shard logs actually feeds them to the gate.
#
# THREE STATES, same contract as the gate it guards:
#   0 PASS     the gate is invoked by its runner
#   1 FAIL     the gate exists but nothing calls its production function
#   2 REFUSE   the runner document could not be read, so NOTHING was measured
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
RUNNER="${RUNNER_OVERRIDE:-$REPO/.claude/skills/full-ci-run/SKILL.md}"
GATE="$REPO/eng/ci/full-ci-shard-completeness.sh"

# The production function, as opposed to the self-test. `--self-test` deliberately does NOT count:
# counting it is exactly the false-green this file exists to prevent.
PRODUCTION_FLAG="--assembly-results"

fail=0

# ── REFUSE arm — an unreadable runner means we measured nothing, not that wiring is fine. ────────
if [ ! -r "$RUNNER" ]; then
  echo "REFUSE: cannot read the runner document: $RUNNER" >&2
  echo "        Measured NOTHING. This is NOT a pass." >&2
  exit 2
fi
if [ ! -r "$GATE" ]; then
  echo "REFUSE: cannot read the gate itself: $GATE" >&2
  exit 2
fi

# ── the gate must actually implement the flag we are asserting a caller for ──────────────────────
# Without this, deleting the flag from the gate would make the wiring check vacuously satisfiable
# by a stale mention in the runner.
if ! grep -q -- "$PRODUCTION_FLAG" "$GATE"; then
  echo "REFUSE: $GATE does not implement $PRODUCTION_FLAG — the thing being wired does not exist" >&2
  exit 2
fi

# ── SAFETY/LIVENESS — the runner must invoke the production function ─────────────────────────────
if grep -q -- "$PRODUCTION_FLAG" "$RUNNER"; then
  echo "PASS: the full-suite runner invokes $PRODUCTION_FLAG"
else
  echo "FAIL: eng/ci/full-ci-shard-completeness.sh implements $PRODUCTION_FLAG, but the runner" >&2
  echo "      ($RUNNER) never calls it." >&2
  echo "      The gate's --self-test will still pass. That green means the gate CAN detect a" >&2
  echo "      missing assembly, not that anything is asking it to. Wire it or delete it." >&2
  fail=1
fi

# ── the runner must also check the gate's exit code, not merely mention it ───────────────────────
# A documented command nobody branches on is a suggestion, not a gate.
if grep -q -- "$PRODUCTION_FLAG" "$RUNNER" && ! grep -qE 'ASM_EXIT|assembly-completeness' "$RUNNER"; then
  echo "FAIL: the runner invokes $PRODUCTION_FLAG but never binds or checks its exit code." >&2
  echo "      An unchecked gate invocation is decorative." >&2
  fail=1
fi

[ "$fail" -eq 0 ] || exit 1
exit 0
