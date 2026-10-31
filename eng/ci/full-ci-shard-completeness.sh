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

# A solution filter some workflow RESTORES or BUILDS but never runs tests against. Such a filter is
# not a shard the local runner must execute, and counting it as one is not a harmless over-count:
# nobody curates a test filter for a shard nobody tests, so running it admits whatever happens to be
# in it. Measured 2026-10-30: IntegrationTests is referenced only by committed-content-gates.yml, at
# --restore and --build, and running its tests locally pulled in Cosmos emulator fixtures that the
# PR path deliberately moved to nightly, plus the whole Excalibur integration assembly UNSPLIT --
# the same assembly IntegrationTests-Excalibur exists to split six ways precisely because serially
# it exceeds one session's test budget by design.
#
# This is DATA with a VERIFIED claim, not an exclusion list. is_build_only_stale() below re-derives
# the claim from the workflows on every run, so the moment someone wires a test job for one of these
# the gate REFUSES instead of silently continuing to skip it. An exclusion nobody re-checks is how
# the omission this gate was written for happened in the first place.
BUILD_ONLY_SHARDS="IntegrationTests"

is_build_only() {
  local n="$1" b
  for b in $BUILD_ONLY_SHARDS; do [ "$n" = "$b" ] && return 0; done
  return 1
}

# Returns 0 (stale) when a BUILD_ONLY shard is in fact referenced by something that runs tests.
is_build_only_stale() {
  local n="$1" src="$2"
  grep -rhE "eng/ci/shards/${n}\.slnf" "$src" 2>/dev/null \
    | grep -qE 'dotnet[[:space:]]+test|solution-filter:|test-filter|--filter'
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
  local stale=""
  for n in $names; do
    if is_aggregate "$n"; then continue; fi
    if is_build_only "$n"; then
      if is_build_only_stale "$n" "$src"; then stale="$stale $n"; fi
      continue
    fi
    if is_known_shard "$n"; then printf '%s\n' "$n"; else unknown="$unknown $n"; fi
  done > "$TMP_EXPECTED"
  if [ -n "$stale" ]; then
    echo "REFUSE_REASON=shard(s) classified BUILD_ONLY in this script are now referenced by a test invocation:$stale — the classification is stale; remove them from BUILD_ONLY_SHARDS so they are expected to report"
    return 2
  fi
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

# The assemblies a shard's TRX files say reported. One TRX per assembly (LogFilePrefix), and each
# carries the assembly path in UnitTest/@storage, so this set is authoritative in a way the console
# never is.
#
# WHY THIS EXISTS. The console-derived set above keys on the " - Name.dll (tfm)" suffix of the
# terminal line, and that suffix is lost when two writers reach stdout at once. Measured on a real
# 14-shard run: two shards reported asm_exit=1 naming an assembly MISSING, and both assemblies had in
# fact run and passed. Their terminal lines had been clobbered --
#
#   Passed! - Failed: 0, Passed: 11, ... Duration: 4 sResults File: ...UnitTests-...trx
#   Passed! - Failed: 0, Passed: 84, ... Duration: 228 ms
#
# -- the first with the following line written into it, the second simply truncated. Those counts,
# 11 and 84, matched the two assemblies' TRX result counts exactly. The gate said "silence, not a
# pass" about assemblies that had reported, because it was reading a surface that can lose the name.
#
# SCOPED TO THE RUN UNDER TEST BY A TIME FLOOR, and the scoping is load-bearing.
#
# This used to glob every "$dir/${shard}"_*.trx, which cannot tell a file this run wrote from one a
# PREVIOUS run left behind. CI never noticed: each runner starts with an empty results directory. A
# local whole-tree run accumulates. Measured 2026-10-30 -- the directory held 2,492 TRX, 118 matching
# one shard of which only 49 belonged to the run under test, and the union therefore reported "all 69
# expected assemblies reported" for a run that had LOST 21 assemblies to premature MSBuild node exit
# (0 Failed! lines, exit 1 from MSB4166). Archiving only the older files and re-running the identical
# command returned exit 1 and named all 21. A gate whose only deployment is the local pre-alpha
# rehearsal, and which cannot fail there, is the check-that-cannot-fail shape.
#
# WHY A TIME FLOOR AND NOT THE LOG'S OWN TRX NAMES. The log does name its TRX ("Results File: ..."),
# and keying on that is tighter -- but it breaks the one case this union exists for. The clobbering
# this function survives is interleaved stdout, which merges or truncates exactly those lines, so a
# TRX that the log fails to name is the normal shape of the defect rather than an edge case. The
# self-test's A11 arm plants precisely that: a TRX the clobbered log does not mention.
#
# A PRIOR run's TRX and THIS run's TRX are indistinguishable by content -- same shard, same assembly
# name -- so freshness is the only available discriminator. The floor is, in order:
#   1. $COMPLETENESS_RUN_START, when the caller states it (seconds since epoch). Preferred: a caller
#      that reuses one log path per shard cannot rely on birth time, because truncating a file with
#      `>` does NOT reset its creation timestamp.
#   2. the log's own birth time, which is correct whenever the log is created fresh per run.
#   3. unscoped, when the filesystem reports no birth time. Stated in the output rather than assumed,
#      so an unscoped union is never silently mistaken for a scoped one.
trx_reported_assemblies() {
  local shard="$1" log="$2" dir f floor m
  dir="$(dirname "$log")"
  floor="${COMPLETENESS_RUN_START:-}"
  if [ -z "$floor" ]; then floor="$(stat -c %W "$log" 2>/dev/null || echo 0)"; fi
  case "$floor" in ''|'?'|*[!0-9]*) floor=0 ;; esac
  for f in "$dir/${shard}"_*.trx; do
    [ -r "$f" ] || continue
    if [ "$floor" -ne 0 ]; then
      m="$(stat -c %Y "$f" 2>/dev/null || echo 0)"
      case "$m" in ''|*[!0-9]*) m=0 ;; esac
      [ "$m" -ge "$floor" ] || continue
    fi
    # The TRX is written by whichever platform ran the shard, so storage may use either separator.
    # Backslashes are normalised with tr -- the doubled form is required: a single one makes tr warn
    # "unescaped backslash at end of string" and pass it through unchanged, which silently defeats the
    # basename and makes every TRX name unmatchable.
    grep -oE 'storage="[^"]*"' "$f" | sed 's/^storage="//; s/"$//' | tr '\\' '/' | sed 's#.*/##; s/[.]dll$//'
  done | sort -u
}

# Terminal lines that report a result but name no assembly. Such a line is not evidence that an
# assembly ran, and not evidence that one did not.
count_unattributable_result_lines() {
  grep -cE '(Passed!|Failed!).*Duration:[^-]*$' || true
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

arm_ok()  { arms=$((arms+1)); echo "  PASS  $*"; }
arm_bad() { arms=$((arms+1)); fails=$((fails+1)); echo "  FAIL  $*"; }

# ── self-test: the guard must go RED on an omission and GREEN on a complete set ──────────────────
self_test() {
  local fails=0 arms=0 rc out
  local BS
  BS=$(printf '%b' '\\')   # one literal backslash, kept out of printf format strings
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
  if [ "$rc" -eq 0 ]; then arm_ok "S1 LIVENESS complete shard set -> exit 0"
  else arm_bad "S1 LIVENESS complete shard set -> exit $rc (expected 0)"; fi

  # S2 SAFETY -- drop an INTEGRATION shard. The incident this gate exists to prevent was an
  # omitted integration shard reported as "all shard TESTS green", so the FAMILY is what matters
  # even though the specific name has moved: IntegrationTests is now classified BUILD_ONLY (no
  # workflow runs tests against it), so dropping it correctly no longer fails, and asserting that
  # it does would be asserting the old classification rather than the current behaviour. Pointed
  # at IntegrationTests-Excalibur, which IS expected to report.
  local minus_one
  minus_one="$(printf '%s\n' $expected_all | grep -v '^IntegrationTests-Excalibur$')"
  rc=0; printf '%s\n' "$minus_one" | bash "$0" --results /dev/stdin >/dev/null 2>&1 || rc=$?
  if [ "$rc" -eq 1 ]; then arm_ok "S2 SAFETY   an integration shard omitted -> exit 1 (the real-world omission is caught)"
  else arm_bad "S2 SAFETY   an integration shard omitted -> exit $rc (expected 1)"; fi

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
  if [ "$rc" -eq 1 ]; then arm_ok "S2b SAFETY  '$first' omitted -> exit 1 (not special-cased to one shard)"
  else arm_bad "S2b SAFETY  '$first' omitted -> exit $rc (expected 1)"; fi

  # S3 REFUSE != PASS — an undeterminable oracle must REFUSE, never report a clean run.
  rc=0; CI_YML_OVERRIDE=/nonexistent/ci.yml bash "$0" --results /dev/null >/dev/null 2>&1 || rc=$?
  if [ "$rc" -eq 2 ]; then arm_ok "S3 REFUSE   unreadable ci.yml -> exit 2, distinct from both PASS and FAIL"
  else arm_bad "S3 REFUSE   unreadable ci.yml -> exit $rc (expected 2)"; fi

  # S4 EMPTY-INPUT — zero shards reported is the "runner never ran" case; must FAIL, not pass.
  rc=0; bash "$0" --results /dev/null >/dev/null 2>&1 || rc=$?
  if [ "$rc" -eq 1 ]; then arm_ok "S4 EMPTY    no shards reported -> exit 1 (an empty run is not a green run)"
  else arm_bad "S4 EMPTY    no shards reported -> exit $rc (expected 1)"; fi

  # S5 SHAPE — the real report is a markdown table; the parser must read what the runner emits.
  local table="| # | Shard | Passed | Failed |"$'\n'
  local s
  for s in $expected_all; do table="$table| 1 | $s | 10 | 0 |"$'\n'; done
  rc=0; printf '%s' "$table" | bash "$0" --results /dev/stdin >/dev/null 2>&1 || rc=$?
  if [ "$rc" -eq 0 ]; then arm_ok "S5 SHAPE    markdown summary-table rows parse -> exit 0"
  else arm_bad "S5 SHAPE    markdown summary-table rows -> exit $rc (expected 0)"; fi

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
    if [ "$rc" -eq 0 ]; then arm_ok "A6 LIVENESS every expected assembly reported -> exit 0"
    else arm_bad "A6 LIVENESS every expected assembly reported -> exit $rc (expected 0)"; fi

    # A7 SAFETY — drop ONE assembly's line. This is the real-world shape: the shard still exits 0
    # and still prints zero failures, and only this check can see the hole.
    local dropped first_asm
    first_asm="$(printf '%s\n' $asm_all | head -1)"
    dropped="$(grep -v "\- ${first_asm}\.dll" "$TMP_REPORTED.log")"
    printf '%s\n' "$dropped" > "$TMP_REPORTED.log2"
    rc=0; bash "$0" --assembly-results "$probe_shard" "$TMP_REPORTED.log2" >/dev/null 2>&1 || rc=$?
    if [ "$rc" -eq 1 ]; then arm_ok "A7 SAFETY   '$first_asm' silently absent -> exit 1"
    else arm_bad "A7 SAFETY   '$first_asm' silently absent -> exit $rc (expected 1)"; fi

    # A8 SAFETY — a log with ZERO test lines (the shard never ran, or died before reporting) must
    # FAIL. `Failed: 0` is trivially true of a run that produced nothing.
    : > "$TMP_REPORTED.log3"
    rc=0; bash "$0" --assembly-results "$probe_shard" "$TMP_REPORTED.log3" >/dev/null 2>&1 || rc=$?
    if [ "$rc" -eq 1 ]; then arm_ok "A8 EMPTY    no assembly reported -> exit 1 (an empty shard is not a green shard)"
    else arm_bad "A8 EMPTY    no assembly reported -> exit $rc (expected 1)"; fi

    # A8b SAFETY — a COMPLETE set of assemblies in a log that was ABORTED must FAIL. This is the shape
    # that got past this gate for real: every assembly present, zero failures printed, and the run
    # truncated by a session timeout. Built from the A6 liveness log so the ONLY difference is the abort
    # marker — otherwise this arm could pass for the wrong reason.
    { cat "$TMP_REPORTED.log"; printf 'Aborting test run: test run timeout of 3600000 milliseconds exceeded.\nTest Run Aborted.\n'; } > "$TMP_REPORTED.log4"
    rc=0; bash "$0" --assembly-results "$probe_shard" "$TMP_REPORTED.log4" >/dev/null 2>&1 || rc=$?
    if [ "$rc" -eq 1 ]; then arm_ok "A8b ABORT   complete assemblies + abort marker -> exit 1 (truncated is not green)"
    else arm_bad "A8b ABORT   complete assemblies + abort marker -> exit $rc (expected 1)"; fi

    # A11 LIVENESS — the real defect this gate produced. One assembly's terminal line loses its
    # " - Name.dll (tfm)" suffix to interleaved output, but the assembly DID run and left a TRX.
    # Before the TRX union this reported FAIL and named an assembly that had passed.
    local clobbered trx_dir
    clobbered="$(grep -v "\- ${first_asm}\.dll" "$TMP_REPORTED.log")"
    trx_dir="$(dirname "$TMP_REPORTED.log")"
    {
      printf '%s\n' "$clobbered"
      printf 'Passed!  - Failed:     0, Passed:    11, Skipped:     0, Total:    11, Duration: 4 s\n'
    } > "$trx_dir/clob.log"
    printf '<TestRun><TestDefinitions><UnitTest storage="d:%sbin%s%s.dll" /></TestDefinitions></TestRun>\n' \
      "$BS" "$BS" "$(printf '%s' "$first_asm" | tr 'A-Z' 'a-z')" > "$trx_dir/${probe_shard}_selftest.trx"
    rc=0; bash "$0" --assembly-results "$probe_shard" "$trx_dir/clob.log" >/dev/null 2>&1 || rc=$?
    if [ "$rc" -eq 0 ]; then arm_ok "A11 LIVENESS a clobbered line resolved by its TRX -> exit 0 (no false MISSING)"
    else arm_bad "A11 LIVENESS a clobbered line resolved by its TRX -> exit $rc (expected 0)"; fi

    # A12 SAFETY — the TRX must not become a blanket excuse. With the line clobbered AND no TRX to
    # attribute it, a lost name and a lost assembly are indistinguishable: REFUSE, never PASS.
    rm -f "$trx_dir/${probe_shard}_selftest.trx"
    rc=0; bash "$0" --assembly-results "$probe_shard" "$trx_dir/clob.log" >/dev/null 2>&1 || rc=$?
    if [ "$rc" -eq 2 ]; then arm_ok "A12 REFUSE   clobbered line with no TRX -> exit 2 (not a pass, not a fail)"
    else arm_bad "A12 REFUSE   clobbered line with no TRX -> exit $rc (expected 2)"; fi

    # A13 SAFETY — a TRX for a DIFFERENT assembly must not satisfy the missing one. Without this the
    # union could be satisfied by any TRX at all, which is the vacuity the union risks introducing.
    printf '<TestRun><TestDefinitions><UnitTest storage="d:%sbin%ssome.other.assembly.dll" /></TestDefinitions></TestRun>\n' \
      "$BS" "$BS" > "$trx_dir/${probe_shard}_selftest.trx"
    rc=0; bash "$0" --assembly-results "$probe_shard" "$TMP_REPORTED.log2" >/dev/null 2>&1 || rc=$?
    rm -f "$trx_dir/${probe_shard}_selftest.trx"
    if [ "$rc" -eq 1 ]; then arm_ok "A13 SAFETY  an unrelated TRX does not satisfy a missing assembly -> exit 1"
    else arm_bad "A13 SAFETY  an unrelated TRX does not satisfy a missing assembly -> exit $rc (expected 1)"; fi

    # A9 REFUSE — an unknown shard has no derivable oracle. REFUSE, never PASS.
    rc=0; bash "$0" --assembly-results "NoSuchShard-$$" "$TMP_REPORTED.log" >/dev/null 2>&1 || rc=$?
    if [ "$rc" -eq 2 ]; then arm_ok "A9 REFUSE   unknown shard -> exit 2, distinct from PASS and FAIL"
    else arm_bad "A9 REFUSE   unknown shard -> exit $rc (expected 2)"; fi

    # A10 ORACLE SHAPE — the expected set must EXCLUDE support libraries that emit no test line.
    # If Tests.Shared ever enters the oracle, every run FAILs for a project that cannot report,
    # and the gate gets disabled as noise. That is how a real gate dies.
    if printf '%s\n' $asm_all | grep -qx "Tests.Shared"; then
      arm_bad "A10 ORACLE  Tests.Shared (IsTestProject=false) leaked into the expected set"
    else
      arm_ok "A10 ORACLE  support libraries excluded from the expected set"
    fi
    rm -f "$TMP_REPORTED.log" "$TMP_REPORTED.log2" "$TMP_REPORTED.log3"
  fi

  printf '\n  %s\n\n' "$([ "$fails" -eq 0 ] && echo "$arms passed, 0 failed" || echo "$((arms-fails)) passed, $fails failed")"
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
  # An assembly that ran leaves evidence on the console, in a TRX, or both. One that never ran leaves
  # neither, so the UNION is the right reading: it removes the false MISSING caused by a clobbered
  # console line without weakening what this gate catches.
  normalise_reported_assemblies < "$RESULTS" > "$TMP_REPORTED"
  trx_reported_assemblies "$SHARD" "$RESULTS" >> "$TMP_REPORTED"
  sort -u -o "$TMP_REPORTED" "$TMP_REPORTED"

  # TRX storage names are lower-cased by the runner, so the COMPARISON is case-insensitive while the
  # REPORT keeps the .slnf's own casing.
  TMP_EXP_LC="$(mktemp)"; TMP_REP_LC="$(mktemp)"
  tr 'A-Z' 'a-z' < "$TMP_EXPECTED" | sort -u > "$TMP_EXP_LC"
  tr 'A-Z' 'a-z' < "$TMP_REPORTED" | sort -u > "$TMP_REP_LC"

  MISSING_LC="$(comm -23 "$TMP_EXP_LC" "$TMP_REP_LC")"
  A_EXTRA="$(comm -13 "$TMP_EXP_LC" "$TMP_REP_LC")"
  A_MISSING="$(awk 'NR==FNR{ if(length($0)) m[$0]; next } (tolower($0) in m)' <(printf '%s
' "$MISSING_LC") "$TMP_EXPECTED")"
  A_EXP="$(wc -l < "$TMP_EXP_LC" | tr -d ' ')"
  A_REP="$(wc -l < "$TMP_REP_LC" | tr -d ' ')"
  rm -f "$TMP_EXP_LC" "$TMP_REP_LC"

  # Still looks incomplete, the log carries result lines naming no assembly, and no TRX is beside it
  # to resolve them: REFUSE. A lost name and a lost assembly are indistinguishable from here, and
  # calling that FAIL is how this gate once named two innocent assemblies.
  if [ -n "$A_MISSING" ]; then
    UNATTRIBUTABLE="$(count_unattributable_result_lines < "$RESULTS")"
    TRX_FOUND="$(trx_reported_assemblies "$SHARD" "$RESULTS" | grep -c . || true)"
    if [ "${UNATTRIBUTABLE:-0}" -gt 0 ] && [ "${TRX_FOUND:-0}" -eq 0 ]; then
      echo "REFUSE: shard '$SHARD' has $UNATTRIBUTABLE result line(s) naming no assembly, and no TRX beside $RESULTS to resolve them." >&2
      echo "A clobbered terminal line and a missing assembly are indistinguishable here. This measured NOTHING: not a pass, not a fail." >&2
      printf '  unresolved: %s
' $A_MISSING >&2
      exit 2
    fi
  fi

  if [ -n "$A_MISSING" ]; then
    echo "FAIL: shard '$SHARD' is INCOMPLETE — $A_REP of $A_EXP expected assemblies reported." >&2
    echo "MISSING (in the .slnf, but produced NO Passed!/Failed! line — silence, not a pass):" >&2
    printf '  - %s\n' $A_MISSING >&2
    [ -n "$A_EXTRA" ] && { echo "also reported but not in the .slnf:" >&2; printf '  - %s\n' $A_EXTRA >&2; }
    echo "Do NOT report this shard as green. A zero-failure line from a shard that lost an assembly is the defect." >&2
    exit 1
  fi
  # EVERY ASSEMBLY REPORTING IS NOT THE SAME AS THE RUN HAVING FINISHED, and this gate once passed a
  # shard that had been ABORTED. A session-timeout abort prints a summary line that reads exactly like a
  # green one -- "Passed!  - Failed: 0, Passed: 3597" -- with "Test Run Aborted." two lines below it, and
  # every expected assembly HAS reported, because the assembly did run; it just did not finish. Measured:
  # 3,772 tests discovered against 3,598 reported, so 174 never executed behind a passing completeness
  # check and a zero-failure summary.
  #
  # This is FAIL rather than REFUSE: an abort marker is determinate. We are not unable to measure whether
  # the run completed -- we have measured that it did not.
  if ABORTED="$(grep -niE 'Test Run Aborted|Aborting test run|test run timeout of [0-9]+ milliseconds exceeded' "$RESULTS" | head -3)" && [ -n "$ABORTED" ]; then
    echo "FAIL: shard '$SHARD' was ABORTED -- the run did not finish, so its result is TRUNCATED." >&2
    echo "Every expected assembly reported, which is why the completeness check above passes: the" >&2
    echo "assemblies ran, they just did not finish. A zero-failure summary from a truncated run counts" >&2
    echo "only the tests that got to execute." >&2
    printf '  %s\n' "$ABORTED" >&2
    echo "Do NOT report this shard as green. Re-run it with a session budget that fits, or split it." >&2
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
