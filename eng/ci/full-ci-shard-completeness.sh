#!/usr/bin/env bash
# full-ci-shard-completeness.sh — shard-completeness gate for the local full-suite runner
#
# WHAT THIS EXISTS TO PREVENT
# ---------------------------
# A local full-suite run reported "All 10 shards GREEN", that report was integrated into a
# sprint plan, and IntegrationTests had never run. The omission produced NO LINE, so it was
# invisible by construction: the runner reported per-shard exit codes but never asserted that the
# set of shards RUN equals the set the suite DEFINES. An absent shard is not a failure signal —
# it is silence, and silence read as green.
#
# Same class as the shard-internal case where an assembly that never reported was masked by a
# sibling's pass line. There the unit was the assembly; here it is the shard.
#
# THE THREE STATES — REFUSE IS NOT A PASS
#   0 PASS     every expected shard reported a result
#   1 FAIL     an expected shard is MISSING or reported no result
#   2 REFUSE   this script could not determine the expected set, so it measured NOTHING
#
# REFUSE exists because the failure mode of a completeness checker is to go green when its own
# oracle is empty: "0 missing shards" is trivially true when the expected set is 0. That is the
# safety-arm-satisfied-by-inaction trap applied to the checker itself, so it is a hard REFUSE.
#
# THE ORACLE IS THE WORKFLOW DIRECTORY — the same source CI actually uses, never a hand list.
# A hand-maintained list is precisely what failed: the runner's table hardcoded 10 shards and
# omitted the 11th, and nothing could detect that because the list WAS the definition.
#
# Scope is .github/workflows/ AS A WHOLE, not one file: shards live in sibling workflows too
# (the performance shard runs in its own), so scanning only the main pipeline under-counts the
# expected set — which is the same defect as the runner it polices, one level up.
#
# UNKNOWN SHARD => REFUSE, NEVER IGNORE. Aggregate filters (TestsOnly, ShippingOnly, ...) are not
# per-shard runs and are excluded BY NAME below. If a workflow gains a shard this script cannot
# classify, it REFUSEs rather than silently dropping it — so a future shard cannot be omitted the
# same way IntegrationTests was.
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# THE ORACLE IS EVERY WORKFLOW, NOT JUST ci.yml.
# My first version scoped this to ci.yml and it was WRONG in the direction that matters: it
# reported PerformanceTests as "not a CI shard" when it runs in its own workflow
# (.github/workflows/performance-tests.yml:81). A completeness oracle that under-counts the
# expected set is the same defect as the runner it polices, one level up — so the scope is the
# whole workflow directory, and the glob is stated rather than implied.
CI_WORKFLOWS="${CI_YML_OVERRIDE:-$REPO/.github/workflows}"
CI_YML="$CI_WORKFLOWS"   # retained name for the REFUSE messages below

# Aggregate / non-shard solution filters. These are whole-suite or packaging filters, not one of
# the per-shard test runs the local runner is expected to execute one at a time.
AGGREGATES="TestsOnly UnitTests ShippingOnly CoreOnly ReleaseBlocking SamplesOnly BenchmarksOnly"

# A per-shard filter is one CI runs tests against individually. Recognised families, as data:
#   UnitTests-*          the unit shards
#   ConformanceTests     conformance kit
#   PerformanceTests     perf + benchmarks
#   IntegrationTests     real-infra shard  <- the one that was omitted
#   IntegrationTests-*   the real-infra shard, split; see below
#
# IntegrationTests-* was added when the integration suite was split across per-shard filters to get it
# off the critical path. The parent IntegrationTests filter still exists and is still built by nightly
# and the committed-content gates, so both forms are legitimate and both must be recognised.
#
# This gate REFUSED rather than dropping the two new names, which is the behaviour it was written for:
# it named them and said to classify them here. Recorded because the refusal is the gate working, not
# the gate being in the way -- a silent drop is exactly how IntegrationTests went missing originally.
is_known_shard() {
  case "$1" in
    UnitTests-*|IntegrationTests-*|ConformanceTests|PerformanceTests|IntegrationTests) return 0 ;;
    *) return 1 ;;
  esac
}
is_aggregate() {
  local n="$1" a
  for a in $AGGREGATES; do [ "$n" = "$a" ] && return 0; done
  return 1
}

# ── derive the expected shard set from the workflow directory ────────────────────────────────────────────────────
derive_expected() {
  local src
  if [ -d "$CI_WORKFLOWS" ]; then src="$CI_WORKFLOWS"
  elif [ -r "$CI_WORKFLOWS" ]; then src="$CI_WORKFLOWS"
  else echo "REFUSE_REASON=cannot read workflow source $CI_WORKFLOWS"; return 2; fi
  local names unknown="" n
  names="$(grep -rhoE 'eng/ci/shards/[A-Za-z0-9._-]+\.slnf' "$src" 2>/dev/null \
            | sed -e 's#.*/##' -e 's/\.slnf$//' | sort -u)"
  [ -n "$names" ] || { echo "REFUSE_REASON=no shard filters found in $src (parse produced nothing)"; return 2; }
  for n in $names; do
    if is_aggregate "$n"; then continue; fi
    if is_known_shard "$n"; then printf '%s\n' "$n"; else unknown="$unknown $n"; fi
  done > "$TMP_EXPECTED"
  if [ -n "$unknown" ]; then
    echo "REFUSE_REASON=unclassifiable shard filter(s) in the workflows:$unknown — classify them in this script rather than letting them be dropped"
    return 2
  fi
  [ -s "$TMP_EXPECTED" ] || { echo "REFUSE_REASON=expected shard set is EMPTY after classification — a completeness check with an empty oracle passes vacuously"; return 2; }
  return 0
}

# ── normalise the reported set ───────────────────────────────────────────────────────────────────
# Accepts shard names on stdin or in a --results file, one per line, in any of the shapes a run
# report actually uses: a bare name, a .slnf filename, or a markdown summary-table row.
normalise_reported() {
  sed -e 's/|/ /g' \
      -e 's#eng/ci/shards/##g' \
      -e 's/\.slnf//g' \
      | tr -s ' \t' '\n' \
      | grep -oE '^(UnitTests-[A-Za-z0-9-]+|IntegrationTests-[A-Za-z0-9-]+|ConformanceTests|PerformanceTests|IntegrationTests)$' \
      | sort -u
}

# ── ASSEMBLY-LEVEL COMPLETENESS (the same contract, one level down) ──────────────────────────────
#
# The shard arm above answers "did every shard run?". This arm answers "within a shard, did every
# test assembly report?" — because a shard that reports `Failed: 0` while half its assemblies
# never produced a line is the SAME defect, and the incident that produced the shard arm was
# itself detected as "55 expected assemblies, 54 reported, Transport.Tests MISSING with ZERO
# Failed! lines". A missing assembly is silence, not a pass.
#
# ONE CONTRACT, NOT TWO. This deliberately shares the three states, the REFUSE semantics and the
# self-test harness above rather than forking a parallel gate with its own vocabulary.
#
# THE ORACLE IS THE .slnf PLUS THE PROJECT FILES — never a hand list, same rule as the shard arm.
# Two traps, both real in this repo and both measured before this was written:
#   1. The assembly name is NOT the .csproj filename. 91 of 94 test projects set <AssemblyName>
#      explicitly. Reading the filename would produce a confident, wrong expected set.
#   2. A .slnf contains projects that emit NO test line — support libraries (Tests.Shared,
#      Excalibur.Dispatch.Testing) and the src projects under test. They are distinguished by an
#      explicit <IsTestProject>false</IsTestProject>; projects under tests/ default to true via
#      tests/Directory.Build.props, which also sets OutputType=Exe (xUnit v3 requires it).
# Anything we cannot classify REFUSEs rather than being silently dropped.

# Resolve a .csproj to the assembly name it will actually report under, or "" if it is not a test project.
project_assembly_name() {
  local proj="$1" is_test asm
  [ -r "$proj" ] || return 2
  is_test="$(grep -oE '<IsTestProject>[^<]*' "$proj" | head -1 | sed 's/.*>//' | tr -d '[:space:]' | tr 'A-Z' 'a-z')"
  if [ "$is_test" = "false" ]; then return 1; fi
  if [ -z "$is_test" ]; then
    # No explicit setting: only projects under tests/ inherit IsTestProject=true.
    case "$proj" in */tests/*|tests/*) ;; *) return 1 ;; esac
  fi
  asm="$(grep -oE '<AssemblyName>[^<]*' "$proj" | head -1 | sed 's/.*>//' | tr -d '[:space:]')"
  [ -n "$asm" ] || asm="$(basename "$proj" .csproj)"
  printf '%s\n' "$asm"
  return 0
}

# Derive the expected assembly set for one shard from its .slnf.
derive_expected_assemblies() {
  local shard="$1" slnf proj rc
  slnf="$REPO/eng/ci/shards/${shard}.slnf"
  [ -r "$slnf" ] || { echo "REFUSE_REASON=cannot read shard filter $slnf"; return 2; }
  local projects
  projects="$(grep -oE '"[^"]+\.csproj"' "$slnf" | tr -d '"' | tr '\\' '/' | sort -u)"
  [ -n "$projects" ] || { echo "REFUSE_REASON=no projects parsed from $slnf — an empty oracle passes vacuously"; return 2; }
  : > "$TMP_EXPECTED"
  local unreadable=""
  for proj in $projects; do
    rc=0; project_assembly_name "$REPO/$proj" >> "$TMP_EXPECTED" || rc=$?
    [ "$rc" -eq 2 ] && unreadable="$unreadable $proj"
  done
  if [ -n "$unreadable" ]; then
    echo "REFUSE_REASON=unreadable project file(s) referenced by $slnf:$unreadable — cannot determine whether they emit a test assembly"
    return 2
  fi
  sort -u -o "$TMP_EXPECTED" "$TMP_EXPECTED"
  [ -s "$TMP_EXPECTED" ] || { echo "REFUSE_REASON=expected assembly set for $shard is EMPTY after classification — a completeness check with an empty oracle passes vacuously"; return 2; }
  return 0
}

# Extract the assemblies that ACTUALLY reported from a run log. dotnet test emits one terminal
# line per assembly: "Passed!  - Failed: 0, ... - Excalibur.Dispatch.Tests.dll (net10.0)".
# Keyed on that line specifically: an assembly that merely appears in build output has not reported.
normalise_reported_assemblies() {
  grep -oE '(Passed!|Failed!)[^-]*-[^-]*- +[A-Za-z0-9._-]+\.dll' \
    | grep -oE '[A-Za-z0-9._-]+\.dll$' \
    | sed 's/\.dll$//' \
    | sort -u
}

usage() {
  cat <<EOF
usage: $(basename "$0") --results <file>     compare reported shards against the workflows
       $(basename "$0") --shards "a b c"     compare an explicit list
       $(basename "$0") --expected           print the derived expected set and exit
       $(basename "$0") --assemblies <shard>              print the shard's expected assembly set
       $(basename "$0") --assembly-results <shard> <log>  compare a shard run log against it
       $(basename "$0") --self-test          prove this guard is non-vacuous
exit: 0 PASS · 1 FAIL (missing shard/assembly) · 2 REFUSE (oracle undeterminable — NOT a pass)
EOF
}

TMP_EXPECTED="$(mktemp)"; TMP_REPORTED="$(mktemp)"
trap 'rm -f "$TMP_EXPECTED" "$TMP_REPORTED"' EXIT

# ── self-test: the guard must go RED on an omission and GREEN on a complete set ──────────────────
self_test() {
  local fails=0 rc out
  printf '\nfull-ci-shard-completeness — self-test\n\n'

  local expected_all
  if ! out="$(derive_expected)"; then
    printf '  REFUSE  cannot derive the expected set from %s: %s\n' "$CI_YML" "$out"
    printf '          The self-test measured NOTHING. Not a pass and not a fail.\n\n'
    return 2
  fi
  expected_all="$(tr '\n' ' ' < "$TMP_EXPECTED")"
  printf '  oracle: %s\n\n' "$expected_all"

  # S1 LIVENESS — a complete set must PASS. Without this arm a guard that always fails looks fine.
  rc=0; printf '%s\n' $expected_all | bash "$0" --results /dev/stdin >/dev/null 2>&1 || rc=$?
  if [ "$rc" -eq 0 ]; then echo "  PASS  S1 LIVENESS complete shard set -> exit 0"
  else echo "  FAIL  S1 LIVENESS complete shard set -> exit $rc (expected 0)"; fails=$((fails+1)); fi

  # S2 SAFETY — drop IntegrationTests, the shard that was actually omitted in the incident this gate exists to prevent. MUST go red.
  local minus_one
  minus_one="$(printf '%s\n' $expected_all | grep -v '^IntegrationTests$')"
  rc=0; printf '%s\n' "$minus_one" | bash "$0" --results /dev/stdin >/dev/null 2>&1 || rc=$?
  if [ "$rc" -eq 1 ]; then echo "  PASS  S2 SAFETY   IntegrationTests omitted -> exit 1 (the real-world omission is caught)"
  else echo "  FAIL  S2 SAFETY   IntegrationTests omitted -> exit $rc (expected 1)"; fails=$((fails+1)); fi

  # S2b SAFETY — dropping ANY shard must fail, not just the famous one.
  # `head -1` is WRONG here: the sorted oracle begins with IntegrationTests, so it would pick the
  # same shard as S2 and this arm would duplicate it while LOOKING independent. Two green arms
  # testing one input is the vacuity this file exists to police, so pick a shard that is provably
  # NOT the S2 subject.
  local minus_first first
  first="$(printf '%s\n' $expected_all | tr ' ' '\n' | grep -v '^IntegrationTests$' | grep -v '^$' | head -1)"
  if [ -z "$first" ]; then
    echo "  REFUSE  S2b cannot run: oracle has no shard other than IntegrationTests to drop"
    fails=$((fails+1)); first="__none__"
  fi
  minus_first="$(printf '%s\n' $expected_all | tr ' ' '\n' | grep -v "^${first}$" | grep -v '^$')"
  rc=0; printf '%s\n' "$minus_first" | bash "$0" --results /dev/stdin >/dev/null 2>&1 || rc=$?
  if [ "$rc" -eq 1 ]; then echo "  PASS  S2b SAFETY  '$first' omitted -> exit 1 (not special-cased to one shard)"
  else echo "  FAIL  S2b SAFETY  '$first' omitted -> exit $rc (expected 1)"; fails=$((fails+1)); fi

  # S3 REFUSE != PASS — an undeterminable oracle must REFUSE, never report a clean run.
  rc=0; CI_YML_OVERRIDE=/nonexistent/ci.yml bash "$0" --results /dev/null >/dev/null 2>&1 || rc=$?
  if [ "$rc" -eq 2 ]; then echo "  PASS  S3 REFUSE   unreadable ci.yml -> exit 2, distinct from both PASS and FAIL"
  else echo "  FAIL  S3 REFUSE   unreadable ci.yml -> exit $rc (expected 2)"; fails=$((fails+1)); fi

  # S4 EMPTY-INPUT — zero shards reported is the "runner never ran" case; must FAIL, not pass.
  rc=0; bash "$0" --results /dev/null >/dev/null 2>&1 || rc=$?
  if [ "$rc" -eq 1 ]; then echo "  PASS  S4 EMPTY    no shards reported -> exit 1 (an empty run is not a green run)"
  else echo "  FAIL  S4 EMPTY    no shards reported -> exit $rc (expected 1)"; fails=$((fails+1)); fi

  # S5 SHAPE — the real report is a markdown table; the parser must read what the runner emits.
  local table="| # | Shard | Passed | Failed |"$'\n'
  local s
  for s in $expected_all; do table="$table| 1 | $s | 10 | 0 |"$'\n'; done
  rc=0; printf '%s' "$table" | bash "$0" --results /dev/stdin >/dev/null 2>&1 || rc=$?
  if [ "$rc" -eq 0 ]; then echo "  PASS  S5 SHAPE    markdown summary-table rows parse -> exit 0"
  else echo "  FAIL  S5 SHAPE    markdown summary-table rows -> exit $rc (expected 0)"; fails=$((fails+1)); fi

  # ── assembly arm ───────────────────────────────────────────────────────────────────────────────
  # Same three states, one level down. A6 is the liveness arm and it is the one that matters:
  # without it, a gate that reported every assembly missing would pass every safety arm here.
  local probe_shard="UnitTests-Core" asm_all
  if ! out="$(derive_expected_assemblies "$probe_shard")"; then
    printf '  REFUSE  A-arms cannot run: %s\n' "${out#REFUSE_REASON=}"
    fails=$((fails+1))
  else
    asm_all="$(cat "$TMP_EXPECTED")"

    # A6 LIVENESS — a log in which every expected assembly reported must PASS.
    local full_log="" a
    for a in $asm_all; do full_log="${full_log}Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10, Duration: 1 s - ${a}.dll (net10.0)"$'\n'; done
    printf '%s' "$full_log" > "$TMP_REPORTED.log"
    rc=0; bash "$0" --assembly-results "$probe_shard" "$TMP_REPORTED.log" >/dev/null 2>&1 || rc=$?
    if [ "$rc" -eq 0 ]; then echo "  PASS  A6 LIVENESS every expected assembly reported -> exit 0"
    else echo "  FAIL  A6 LIVENESS every expected assembly reported -> exit $rc (expected 0)"; fails=$((fails+1)); fi

    # A7 SAFETY — drop ONE assembly's line. This is the real-world shape: the shard still exits 0
    # and still prints zero failures, and only this check can see the hole.
    local dropped first_asm
    first_asm="$(printf '%s\n' $asm_all | head -1)"
    dropped="$(grep -v "\- ${first_asm}\.dll" "$TMP_REPORTED.log")"
    printf '%s\n' "$dropped" > "$TMP_REPORTED.log2"
    rc=0; bash "$0" --assembly-results "$probe_shard" "$TMP_REPORTED.log2" >/dev/null 2>&1 || rc=$?
    if [ "$rc" -eq 1 ]; then echo "  PASS  A7 SAFETY   '$first_asm' silently absent -> exit 1"
    else echo "  FAIL  A7 SAFETY   '$first_asm' silently absent -> exit $rc (expected 1)"; fails=$((fails+1)); fi

    # A8 SAFETY — a log with ZERO test lines (the shard never ran, or died before reporting) must
    # FAIL. `Failed: 0` is trivially true of a run that produced nothing.
    : > "$TMP_REPORTED.log3"
    rc=0; bash "$0" --assembly-results "$probe_shard" "$TMP_REPORTED.log3" >/dev/null 2>&1 || rc=$?
    if [ "$rc" -eq 1 ]; then echo "  PASS  A8 EMPTY    no assembly reported -> exit 1 (an empty shard is not a green shard)"
    else echo "  FAIL  A8 EMPTY    no assembly reported -> exit $rc (expected 1)"; fails=$((fails+1)); fi

    # A9 REFUSE — an unknown shard has no derivable oracle. REFUSE, never PASS.
    rc=0; bash "$0" --assembly-results "NoSuchShard-$$" "$TMP_REPORTED.log" >/dev/null 2>&1 || rc=$?
    if [ "$rc" -eq 2 ]; then echo "  PASS  A9 REFUSE   unknown shard -> exit 2, distinct from PASS and FAIL"
    else echo "  FAIL  A9 REFUSE   unknown shard -> exit $rc (expected 2)"; fails=$((fails+1)); fi

    # A10 ORACLE SHAPE — the expected set must EXCLUDE support libraries that emit no test line.
    # If Tests.Shared ever enters the oracle, every run FAILs for a project that cannot report,
    # and the gate gets disabled as noise. That is how a real gate dies.
    if printf '%s\n' $asm_all | grep -qx "Tests.Shared"; then
      echo "  FAIL  A10 ORACLE  Tests.Shared (IsTestProject=false) leaked into the expected set"; fails=$((fails+1))
    else
      echo "  PASS  A10 ORACLE  support libraries excluded from the expected set"
    fi
    rm -f "$TMP_REPORTED.log" "$TMP_REPORTED.log2" "$TMP_REPORTED.log3"
  fi

  printf '\n  %s\n\n' "$([ "$fails" -eq 0 ] && echo "11 passed, 0 failed" || echo "$fails failed")"
  [ "$fails" -eq 0 ] || return 1
  return 0
}

MODE=""; RESULTS=""; SHARDS=""; SHARD=""
while [ $# -gt 0 ]; do
  case "$1" in
    --self-test) MODE=selftest ;;
    --expected)  MODE=expected ;;
    --results)   MODE=compare; RESULTS="${2:-}"; shift ;;
    --shards)    MODE=compare; SHARDS="${2:-}"; shift ;;
    --assemblies)       MODE=asmexpected; SHARD="${2:-}"; shift ;;
    --assembly-results) MODE=asmcompare;  SHARD="${2:-}"; RESULTS="${3:-}"; shift 2 ;;
    -h|--help)   usage; exit 0 ;;
    *) echo "unknown argument: $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done
[ -n "$MODE" ] || { usage >&2; exit 2; }
[ "$MODE" = selftest ] && { self_test; exit $?; }

# ── assembly-level modes ─────────────────────────────────────────────────────────────────────────
if [ "$MODE" = asmexpected ] || [ "$MODE" = asmcompare ]; then
  [ -n "$SHARD" ] || { echo "REFUSE: no shard named" >&2; usage >&2; exit 2; }
  if ! REFUSE="$(derive_expected_assemblies "$SHARD")"; then
    echo "REFUSE: ${REFUSE#REFUSE_REASON=}" >&2
    echo "        Measured NOTHING. This is NOT a pass — do not report the shard as complete." >&2
    exit 2
  fi
  [ "$MODE" = asmexpected ] && { cat "$TMP_EXPECTED"; exit 0; }

  [ -n "$RESULTS" ] && [ -r "$RESULTS" ] || { echo "REFUSE: run log not readable: ${RESULTS:-<none>}" >&2; exit 2; }
  normalise_reported_assemblies < "$RESULTS" > "$TMP_REPORTED"

  A_MISSING="$(comm -23 "$TMP_EXPECTED" "$TMP_REPORTED")"
  A_EXTRA="$(comm -13 "$TMP_EXPECTED" "$TMP_REPORTED")"
  A_EXP="$(wc -l < "$TMP_EXPECTED" | tr -d ' ')"
  A_REP="$(wc -l < "$TMP_REPORTED" | tr -d ' ')"

  if [ -n "$A_MISSING" ]; then
    echo "FAIL: shard '$SHARD' is INCOMPLETE — $A_REP of $A_EXP expected assemblies reported." >&2
    echo "MISSING (in the .slnf, but produced NO Passed!/Failed! line — silence, not a pass):" >&2
    printf '  - %s\n' $A_MISSING >&2
    [ -n "$A_EXTRA" ] && { echo "also reported but not in the .slnf:" >&2; printf '  - %s\n' $A_EXTRA >&2; }
    echo "Do NOT report this shard as green. A zero-failure line from a shard that lost an assembly is the defect." >&2
    exit 1
  fi
  echo "PASS: all $A_EXP expected assemblies in '$SHARD' reported"
  [ -n "$A_EXTRA" ] && echo "note: also reported, not in the .slnf: $(echo $A_EXTRA)"
  exit 0
fi

if ! REFUSE="$(derive_expected)"; then
  echo "REFUSE: ${REFUSE#REFUSE_REASON=}" >&2
  echo "        Measured NOTHING. This is NOT a pass — do not report the run as complete." >&2
  exit 2
fi
[ "$MODE" = expected ] && { cat "$TMP_EXPECTED"; exit 0; }

if [ -n "$SHARDS" ]; then printf '%s\n' $SHARDS | normalise_reported > "$TMP_REPORTED"
elif [ -n "$RESULTS" ]; then
  [ -r "$RESULTS" ] || { echo "REFUSE: results file not readable: $RESULTS" >&2; exit 2; }
  normalise_reported < "$RESULTS" > "$TMP_REPORTED"
else normalise_reported > "$TMP_REPORTED"; fi

MISSING="$(comm -23 "$TMP_EXPECTED" "$TMP_REPORTED")"
EXTRA="$(comm -13 "$TMP_EXPECTED" "$TMP_REPORTED")"
N_EXP="$(wc -l < "$TMP_EXPECTED" | tr -d ' ')"
N_REP="$(wc -l < "$TMP_REPORTED" | tr -d ' ')"

if [ -n "$MISSING" ]; then
  echo "FAIL: the run is INCOMPLETE — $N_REP of $N_EXP expected shards reported." >&2
  echo "MISSING (defined by ci.yml, never executed — produced NO line, which is why this is invisible without a check):" >&2
  printf '  - %s\n' $MISSING >&2
  [ -n "$EXTRA" ] && { echo "also reported but not a ci.yml shard:" >&2; printf '  - %s\n' $EXTRA >&2; }
  echo "Do NOT report this run as green. An omitted shard is silence, not a pass." >&2
  exit 1
fi
echo "PASS: all $N_EXP ci.yml-defined shards reported ($(tr '\n' ' ' < "$TMP_EXPECTED"))"
[ -n "$EXTRA" ] && echo "note: also reported, not a ci.yml shard: $(echo $EXTRA)"
exit 0
