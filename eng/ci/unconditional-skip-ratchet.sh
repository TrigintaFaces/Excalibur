#!/usr/bin/env bash
# unconditional-skip-ratchet.sh — a test may not be born already skipped.
#
# WHAT THIS EXISTS TO PREVENT
# ---------------------------
# A test whose FIRST statement is a bare Assert.Skip(...) with no guard can never execute. It is
# not "skipped when infrastructure is missing" -- it is skipped always, forever, including on a
# machine where the infrastructure is present and healthy.
#
# This is the most expensive shape of green there is, for three compounding reasons:
#
#   1. The suite exits 0 and the shard reports success while asserting NOTHING.
#   2. The fixture still runs. A class doing this was measured at 24m32s wall-clock to produce
#      11 real results and 8 skips, because InitializeAsync starts a real container per test and
#      the test then skips. We pay full infrastructure cost for zero verification.
#   3. The skip MESSAGE typically blames missing infrastructure. When that message is emitted on a
#      host where the infrastructure IS up, it actively misdirects the next person triaging CI --
#      they read "infra unavailable", believe it, and stop looking.
#
# THE RATCHET, and why it is not a blanket ban:
#   There is a large existing population. Failing on all of it would block every commit and the
#   only fast way out would be to delete the skips, converting silent no-ops into unverified
#   assertions and a red suite -- strictly worse. So this gate BASELINES the current counts and
#   fails only on an INCREASE or a NEW file. The population can shrink and can never grow.
#   Lower a baseline number when you make a test execute; the gate then holds you to the new floor.
#
# THREE STATES, matching the other gates in this directory:
#   0 PASS     no file exceeds its baseline, and no unbaselined file has an unconditional skip
#   1 FAIL     a file grew, or a new offender appeared
#   2 REFUSE   the scan matched no test files at all, so NOTHING was measured (an empty oracle
#              passes vacuously, and a silent vacuous pass is the defect this gate is about)
set -uo pipefail

# shellcheck source=/dev/null
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/gate-denominator.sh"

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCAN_ROOT="${SKIP_RATCHET_SCAN_ROOT:-$REPO/tests}"
BASELINE="${SKIP_RATCHET_BASELINE:-$REPO/eng/ci/unconditional-skip-baseline.txt}"

# Emits "<count> <repo-relative-path>" per offending file.
#
# DETECTION: an Assert.Skip is UNCONDITIONAL when the nearest preceding meaningful line (skipping
# blanks and // comments) is a bare "{" -- i.e. it is the first statement of a method body -- and
# the skip line itself carries no "if". A guarded skip (if (!x) Assert.Skip(...)) is preceded by
# the if, or carries it inline, and is correctly NOT counted: this gate targets the always-skip
# shape only, never a legitimate conditional.
scan() {
  # ONE awk pass over every file. A per-file awk spawn is thousands of processes and takes
  # minutes on this tree; a gate slow enough to be annoying is a gate someone eventually skips.
  # FNR==1 resets the per-file state, and results are emitted in FILENAME order at END.
  find "$SCAN_ROOT" -name "*.cs" -type f -print0 2>/dev/null \
  | xargs -0 awk -v root="$SCAN_ROOT/" '
      FNR == 1 { prev = "" }
      {
        stripped = $0; gsub(/^[ \t]+|[ \t]+$/, "", stripped)
        # Assert.SkipUnless(cond, "...") and Assert.SkipWhen(cond, "...") are CONDITIONAL BY
        # CONSTRUCTION -- the condition is argument 1, so they need no surrounding if. Counting
        # them is a false positive that would both overstate the problem and punish the correct
        # pattern. Only a BARE Assert.Skip("...") can never execute.
        if (stripped ~ /Assert\.Skip[ \t]*\(/ && stripped !~ /Assert\.Skip(Unless|When)[ \t]*\(/ \
            && stripped !~ /if[ \t]*\(/ && prev == "{") {
          rel = FILENAME; sub(root, "", rel); n[rel]++
        }
        if (stripped != "" && stripped !~ /^\/\//) prev = stripped
      }
      END { for (f in n) printf "%d %s\n", n[f], f }
    ' 2>/dev/null | sort -k2
}

self_test() {
  local tmp fails=0 rc self
  tmp="$(mktemp -d)"
  trap 'rm -rf "$tmp"' RETURN
  self="$(cd "$(dirname "$0")" && pwd)/$(basename "$0")"
  printf '\nunconditional-skip-ratchet — self-test\n\n'

  mk() { # mk <dir> <skipcount> ; writes a .cs with N unconditional skips
    mkdir -p "$1"; { echo "class T {"; for i in $(seq 1 "$2"); do
      echo "  [Fact]"; echo "  public void M$i()"; echo "  {"
      echo "    Assert.Skip(\"x\");"; echo "  }"; done; echo "}"; } > "$1/T.cs"
  }

  # L1 LIVENESS — a tree matching its baseline must PASS. Without this arm a gate that rejects
  # everything looks identical to a healthy one.
  mk "$tmp/live" 3; printf '3 T.cs\n' > "$tmp/live.txt"
  SKIP_RATCHET_SCAN_ROOT="$tmp/live" SKIP_RATCHET_BASELINE="$tmp/live.txt" bash "$self" >/dev/null 2>&1; rc=$?
  if [ "$rc" -eq 0 ]; then printf '  L1 PASS  at-baseline tree passes\n'
  else printf '  L1 FAIL  at-baseline tree rejected (rc=%s)\n' "$rc"; fails=$((fails+1)); fi

  # S1 SAFETY — an INCREASE over baseline must FAIL. This is the ratchet's whole purpose.
  mk "$tmp/grow" 4; printf '3 T.cs\n' > "$tmp/grow.txt"
  SKIP_RATCHET_SCAN_ROOT="$tmp/grow" SKIP_RATCHET_BASELINE="$tmp/grow.txt" bash "$self" >/dev/null 2>&1; rc=$?
  if [ "$rc" -eq 1 ]; then printf '  S1 PASS  growth over baseline is rejected\n'
  else printf '  S1 FAIL  growth NOT rejected (rc=%s)\n' "$rc"; fails=$((fails+1)); fi

  # S2 SAFETY — a NEW unbaselined offender must FAIL, or the ratchet is trivially bypassed by
  # putting the skip in a new file.
  mk "$tmp/newfile" 1; : > "$tmp/newfile.txt"
  SKIP_RATCHET_SCAN_ROOT="$tmp/newfile" SKIP_RATCHET_BASELINE="$tmp/newfile.txt" bash "$self" >/dev/null 2>&1; rc=$?
  if [ "$rc" -eq 1 ]; then printf '  S2 PASS  new unbaselined offender is rejected\n'
  else printf '  S2 FAIL  new offender NOT rejected (rc=%s)\n' "$rc"; fails=$((fails+1)); fi

  # L2 LIVENESS — a GUARDED skip must NOT be counted. Without this the gate would punish the
  # correct pattern and push authors back toward the unconditional one.
  mkdir -p "$tmp/guarded"
  { echo "class T {"; echo "  [Fact]"; echo "  public void M()"; echo "  {";
    echo "    if (!_available) Assert.Skip(\"infra down\");"; echo "  }"; echo "}"; } > "$tmp/guarded/T.cs"
  : > "$tmp/guarded.txt"
  SKIP_RATCHET_SCAN_ROOT="$tmp/guarded" SKIP_RATCHET_BASELINE="$tmp/guarded.txt" bash "$self" >/dev/null 2>&1; rc=$?
  if [ "$rc" -eq 0 ]; then printf '  L2 PASS  guarded skip is not counted\n'
  else printf '  L2 FAIL  guarded skip was counted (rc=%s)\n' "$rc"; fails=$((fails+1)); fi

  # L4 LIVENESS — Assert.SkipUnless / Assert.SkipWhen must NOT be counted. These carry their
  # condition as argument 1, so they are conditional by construction and need no enclosing if.
  # This arm exists because the gate's first version DID count them: it reported 173 offenders
  # when the true number was 46, which overstated the problem by 127 and would have condemned
  # the correct pattern. Regression-locks that fix.
  mkdir -p "$tmp/skipunless"
  { echo "class T {"; echo "  [Fact]"; echo "  public void M()"; echo "  {";
    echo "    Assert.SkipUnless(_dockerAvailable, \"Docker is not available\");"; echo "  }";
    echo "  [Fact]"; echo "  public void N()"; echo "  {";
    echo "    Assert.SkipWhen(_isCi, \"not on CI\");"; echo "  }"; echo "}"; } > "$tmp/skipunless/T.cs"
  : > "$tmp/skipunless.txt"
  SKIP_RATCHET_SCAN_ROOT="$tmp/skipunless" SKIP_RATCHET_BASELINE="$tmp/skipunless.txt" bash "$self" >/dev/null 2>&1; rc=$?
  if [ "$rc" -eq 0 ]; then printf '  L4 PASS  SkipUnless/SkipWhen are not counted\n'
  else printf '  L4 FAIL  SkipUnless/SkipWhen were counted (rc=%s)\n' "$rc"; fails=$((fails+1)); fi

  # L3 LIVENESS — a SHRINK below baseline must PASS, or nobody can ever fix one.
  mk "$tmp/shrink" 1; printf '3 T.cs\n' > "$tmp/shrink.txt"
  SKIP_RATCHET_SCAN_ROOT="$tmp/shrink" SKIP_RATCHET_BASELINE="$tmp/shrink.txt" bash "$self" >/dev/null 2>&1; rc=$?
  if [ "$rc" -eq 0 ]; then printf '  L3 PASS  shrink below baseline passes\n'
  else printf '  L3 FAIL  shrink rejected (rc=%s)\n' "$rc"; fails=$((fails+1)); fi

  # R1 REFUSE — no .cs files at all means nothing was measured. Must REFUSE(2), never PASS(0).
  mkdir -p "$tmp/empty"; : > "$tmp/empty.txt"
  SKIP_RATCHET_SCAN_ROOT="$tmp/empty" SKIP_RATCHET_BASELINE="$tmp/empty.txt" bash "$self" >/dev/null 2>&1; rc=$?
  if [ "$rc" -eq 2 ]; then printf '  R1 PASS  empty scan refuses\n'
  else printf '  R1 FAIL  empty scan did not refuse (rc=%s)\n' "$rc"; fails=$((fails+1)); fi

  printf '\n'
  if [ "$fails" -eq 0 ]; then printf 'self-test: ALL ARMS PASS\n\n'; return 0; fi
  printf 'self-test: %s ARM(S) FAILED\n\n' "$fails"; return 1
}

case "${1:-}" in
  --self-test) self_test; exit $? ;;
  --regenerate)
    scan > "$BASELINE"
    echo "Baseline written: ${BASELINE#"$REPO"/} ($(wc -l < "$BASELINE") file(s), $(awk '{s+=$1} END{print s+0}' "$BASELINE") skip(s))"
    exit 0 ;;
esac

# REFUSE before measuring: no inputs means no verdict is possible.
if [ -z "$(find "$SCAN_ROOT" -name '*.cs' -type f 2>/dev/null | head -1)" ]; then
  echo "REFUSE: no .cs files under ${SCAN_ROOT#"$REPO"/} — nothing was measured. This is NOT a pass." >&2
  exit 2
fi

[ -f "$BASELINE" ] || { echo "REFUSE: baseline not found at ${BASELINE#"$REPO"/}" >&2; exit 2; }

current="$(scan)"
violations=0
while read -r count path; do
  [ -z "${path:-}" ] && continue
  allowed="$(awk -v p="$path" '$2==p {print $1; exit}' "$BASELINE")"
  allowed="${allowed:-0}"
  if [ "$count" -gt "$allowed" ]; then
    echo "FAIL: $path has $count unconditional Assert.Skip (baseline $allowed)" >&2
    violations=$((violations+1))
  fi
done <<< "$current"

if [ "$violations" -ne 0 ]; then
  cat >&2 <<'MSG'

A test whose FIRST statement is a bare Assert.Skip can never execute, on any machine, ever.
The suite still exits 0, so this registers as a PASS while asserting nothing.

Fix one of two ways:
  - make the test execute against real infrastructure (preferred), or
  - guard the skip so it fires ONLY when the infrastructure is genuinely absent:
        if (!_available) Assert.Skip("...");
    A guarded skip is not counted by this gate.

This ratchet never asks you to fix the pre-existing population -- only not to grow it.
MSG
  exit 1
fi

files=$(printf '%s\n' "$current" | grep -c . || true)
# The denominator, in the standard machine-readable form: what was EXAMINED, not only what was
# FOUND. The no-source case already REFUSEs above; this states the earned denominator out loud.
# The DENOMINATOR is the .cs files SCANNED, not the files carrying a skip. Those are different
# numbers and only the first can catch the failure this line exists for: zero files carrying an
# unconditional skip is the DESIRED outcome, so it can never distinguish a clean tree from a scan
# that read nothing. The .cs count can, and a zero there is a REFUSE (also enforced above).
scanned_cs=$(find "$SCAN_ROOT" -name '*.cs' -type f 2>/dev/null | grep -c . || true)
case "$scanned_cs" in ''|*[!0-9]*) scanned_cs=0 ;; esac
gate_denominator "$scanned_cs" "C# file(s) scanned ($files carrying an unconditional skip)" || exit 2
echo "PASS: no file exceeds its unconditional-skip baseline ($files file(s) at or below floor)."
exit 0
