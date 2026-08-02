#!/usr/bin/env bash
# integration-serial-runner-gate.sh — every integration test project must resolve a SERIAL xunit runner config.
#
# WHAT THIS EXISTS TO PREVENT
# ---------------------------
# Integration suites start real containers. When their xunit collections run in parallel, many
# containers start at once and the kernel kills them -- exit 134 (SIGABRT) / 137 (SIGKILL). That
# surfaces as a wall of fixture-initialisation errors that read like product failures, so it gets
# debugged as "flaky infrastructure" rather than as the misconfiguration it is. One occurrence cost
# ~100 fixture-init failures across the integration shards.
#
# The shared tests/Directory.Build.props copies a PARALLEL runner config into test projects, which is
# correct and desirable for unit tests. Integration projects opt out by shipping their own SERIAL
# config, and the shared include is skipped when a project-local file exists.
#
# THE HAZARD THIS GATE CLOSES:
#   That opt-out is by REMEMBERING. Nothing stops a new integration project from shipping no local
#   config, silently inheriting unlimited parallel collection execution, and reintroducing the
#   container-kill failure mode. This gate makes the requirement structural: an integration project
#   without a serial config fails here instead of failing as mystery flake three sprints from now.
#
# WHY A SOURCE-LEVEL CHECK IS SUFFICIENT (the chain is closed, not assumed):
#   local config exists  =>  the shared props' Exists() condition is false
#                        =>  the parallel config is NOT copied
#                        =>  the ONLY runner config reaching bin/ is the local one.
#   So "a local file exists AND its content is serial" entails "the build output is serial". The
#   gate therefore checks committed source and needs no build. Both halves are required: presence
#   alone would pass a local file that is itself parallel.
#
# THREE STATES, matching the other gates in this directory:
#   0 PASS     every integration project resolves a serial runner config
#   1 FAIL     a project is missing one, or ships one that is not serial
#   2 REFUSE   no integration projects were found, so NOTHING was measured (an empty oracle passes
#              vacuously, and a silent vacuous pass is the failure this directory exists to reject)
set -uo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCAN_ROOT="${INTEGRATION_RUNNER_SCAN_ROOT:-$REPO/tests/integration}"
PROPS_FILE="${INTEGRATION_RUNNER_PROPS:-$REPO/tests/Directory.Build.props}"

# CHECK 2 — the shared props must not inject the root runner config into a project that ships its own.
#
# This one line protects EVERY test project, not only the integration ones checked below. Without the
# Exists() condition, the root config and the project's own config both target the same output path and
# the winner is decided by PreserveNewest, i.e. by file mtime. The root file is newer than the
# per-project files, so the root wins and KEEPS winning — an older project file can never overwrite it
# on a later build.
#
# Measured consequence when the condition was absent: 10 projects ship a local config and 4 were
# observed serving the ROOT parallel policy from bin/Release, including BOTH performance suites
# (parallelizeTestCollections=true, maxParallelThreads=0) while their source declared false/1.
# Unbounded parallel collections in a timing-sensitive suite is the documented cause of flaky
# throughput and latency assertions, so this is a correctness check, not a tidiness one.
#
# NOTE FOR ANYONE VERIFYING BY HAND: a stale bin/ still shows the OLD behaviour. Rebuild
# --no-incremental before concluding the condition does not work.
check_props_condition() {
  [ -f "$PROPS_FILE" ] || { echo "missing props file: ${PROPS_FILE#"$REPO"/}"; return 1; }
  # The include must be guarded by an Exists() test on a project-local xunit.runner.json.
  grep -qE "Condition=\"!Exists\(.*xunit\.runner\.json.*\)\"" "$PROPS_FILE" && return 0
  echo "${PROPS_FILE#"$REPO"/}: the shared xunit.runner.json include is NOT guarded by !Exists(project-local) — every project-local runner config loses the PreserveNewest race"
  return 1
}

# An integration project is one that HAS a project file under the integration tree. Keying on the
# project file is what makes this list self-maintaining -- a new suite is enrolled by the act of
# existing, not by being added to a list inside this script that someone must remember to update.
find_projects() {
  find "$SCAN_ROOT" -name "*.csproj" 2>/dev/null | sort
}

check_project() {
  # Returns 0 conforming, 1 drifted. Echoes the reason when drifted.
  local csproj="$1" dir cfg
  dir="$(dirname "$csproj")"
  cfg="$dir/xunit.runner.json"

  if [ ! -f "$cfg" ]; then
    echo "${csproj#"$REPO"/}: missing-xunit.runner.json (would inherit the shared PARALLEL config)"
    return 1
  fi

  local problems=""
  # Tolerant of whitespace so formatting alone never fails a correct config.
  grep -qE '"parallelizeTestCollections"[[:space:]]*:[[:space:]]*false' "$cfg" \
    || problems="${problems} parallelizeTestCollections-not-false"
  grep -qE '"maxParallelThreads"[[:space:]]*:[[:space:]]*1([^0-9]|$)' "$cfg" \
    || problems="${problems} maxParallelThreads-not-1"

  if [ -n "$problems" ]; then
    echo "${cfg#"$REPO"/}:${problems}"
    return 1
  fi
  return 0
}

self_test() {
  local tmp fails=0 rc self
  tmp="$(mktemp -d)"
  trap 'rm -rf "$tmp"' RETURN
  # Re-invoke through an explicit interpreter on an ABSOLUTE path. A bare "$0" depends on the
  # execute bit surviving the checkout and on $0 still resolving from the current directory;
  # when either fails the arms exit 126/127 and the whole self-test reports FAIL for a gate that
  # is actually fine.
  self="$(cd "$(dirname "$0")" && pwd)/$(basename "$0")"
  printf '\nintegration-serial-runner-gate — self-test\n\n'

  # L1 LIVENESS — a conforming project must PASS. Without this arm a gate that rejects everything
  # (or one whose matcher can never match) would look identical to a healthy one.
  mkdir -p "$tmp/live/Good.Tests"
  echo '<Project />' > "$tmp/live/Good.Tests/Good.Tests.csproj"
  cat > "$tmp/live/Good.Tests/xunit.runner.json" <<'JSON'
{ "parallelizeTestCollections": false, "maxParallelThreads": 1 }
JSON
  INTEGRATION_RUNNER_SCAN_ROOT="$tmp/live" bash "$self" >/dev/null 2>&1; rc=$?
  if [ "$rc" -eq 0 ]; then printf '  L1 PASS  conforming project passes\n'
  else printf '  L1 FAIL  conforming project was rejected (rc=%s)\n' "$rc"; fails=$((fails+1)); fi

  # S1 SAFETY — a project with NO local config must FAIL. This is the exact drift the gate exists
  # for: the new suite that inherits the shared parallel config by omission.
  mkdir -p "$tmp/missing/Bad.Tests"
  echo '<Project />' > "$tmp/missing/Bad.Tests/Bad.Tests.csproj"
  INTEGRATION_RUNNER_SCAN_ROOT="$tmp/missing" bash "$self" >/dev/null 2>&1; rc=$?
  if [ "$rc" -eq 1 ]; then printf '  S1 PASS  missing config is rejected\n'
  else printf '  S1 FAIL  missing config was NOT rejected (rc=%s)\n' "$rc"; fails=$((fails+1)); fi

  # S2 SAFETY — presence is not enough. A local file that is itself PARALLEL must FAIL, or the gate
  # would be satisfied by a file whose content reintroduces the very failure mode.
  mkdir -p "$tmp/parallel/Bad.Tests"
  echo '<Project />' > "$tmp/parallel/Bad.Tests/Bad.Tests.csproj"
  cat > "$tmp/parallel/Bad.Tests/xunit.runner.json" <<'JSON'
{ "parallelizeTestCollections": true, "maxParallelThreads": 0 }
JSON
  INTEGRATION_RUNNER_SCAN_ROOT="$tmp/parallel" bash "$self" >/dev/null 2>&1; rc=$?
  if [ "$rc" -eq 1 ]; then printf '  S2 PASS  parallel local config is rejected\n'
  else printf '  S2 FAIL  parallel local config was NOT rejected (rc=%s)\n' "$rc"; fails=$((fails+1)); fi

  # P1 SAFETY — an UNGUARDED props include must FAIL. This is the regression that put the ROOT
  # parallel config into 4 projects' bin/Release, both performance suites among them.
  mkdir -p "$tmp/props/Good.Tests"
  echo '<Project />' > "$tmp/props/Good.Tests/Good.Tests.csproj"
  printf '{ "parallelizeTestCollections": false, "maxParallelThreads": 1 }\n' > "$tmp/props/Good.Tests/xunit.runner.json"
  printf '<Project>\n <ItemGroup>\n  <None Include="xunit.runner.json" CopyToOutputDirectory="PreserveNewest" />\n </ItemGroup>\n</Project>\n' > "$tmp/props-bad.props"
  INTEGRATION_RUNNER_SCAN_ROOT="$tmp/props" INTEGRATION_RUNNER_PROPS="$tmp/props-bad.props" bash "$self" >/dev/null 2>&1; rc=$?
  if [ "$rc" -eq 1 ]; then printf '  P1 PASS  unguarded props include is rejected\n'
  else printf '  P1 FAIL  unguarded props include NOT rejected (rc=%s)\n' "$rc"; fails=$((fails+1)); fi

  # P2 LIVENESS — a correctly guarded props include must PASS, or the gate rejects the fix itself.
  printf '<Project>\n <ItemGroup Condition="!Exists(&apos;$(MSBuildProjectDirectory)/xunit.runner.json&apos;)">\n  <None Include="xunit.runner.json" CopyToOutputDirectory="PreserveNewest" />\n </ItemGroup>\n</Project>\n' > "$tmp/props-good.props"
  INTEGRATION_RUNNER_SCAN_ROOT="$tmp/props" INTEGRATION_RUNNER_PROPS="$tmp/props-good.props" bash "$self" >/dev/null 2>&1; rc=$?
  if [ "$rc" -eq 0 ]; then printf '  P2 PASS  guarded props include is accepted\n'
  else printf '  P2 FAIL  guarded props include was rejected (rc=%s)\n' "$rc"; fails=$((fails+1)); fi

  # R1 REFUSE — an empty population must REFUSE (2), never PASS. A gate that reports success over
  # nothing is the defect this directory's three-state contract exists to make impossible.
  mkdir -p "$tmp/empty"
  INTEGRATION_RUNNER_SCAN_ROOT="$tmp/empty" bash "$self" >/dev/null 2>&1; rc=$?
  if [ "$rc" -eq 2 ]; then printf '  R1 PASS  empty population refuses\n'
  else printf '  R1 FAIL  empty population did not refuse (rc=%s)\n' "$rc"; fails=$((fails+1)); fi

  printf '\n'
  if [ "$fails" -eq 0 ]; then printf 'self-test: ALL ARMS PASS\n\n'; return 0; fi
  printf 'self-test: %s ARM(S) FAILED\n\n' "$fails"; return 1
}

if [ "${1:-}" = "--self-test" ]; then
  self_test
  exit $?
fi

mapfile -t projects < <(find_projects)

if [ "${#projects[@]}" -eq 0 ]; then
  echo "REFUSE: no integration test projects found under ${SCAN_ROOT#"$REPO"/}" >&2
  echo "        Nothing was measured. This is NOT a pass." >&2
  exit 2
fi

drifted=0

# CHECK 2 runs first: if the props guard is gone, every project-local config is inert and the
# per-project results below would be reassuring but meaningless.
if ! props_reason="$(check_props_condition)"; then
  echo "FAIL: $props_reason" >&2
  drifted=$((drifted+1))
fi

for csproj in "${projects[@]}"; do
  if ! reason="$(check_project "$csproj")"; then
    echo "FAIL: $reason" >&2
    drifted=$((drifted+1))
  fi
done

if [ "$drifted" -ne 0 ]; then
  echo "" >&2
  echo "$drifted of ${#projects[@]} integration project(s) do not resolve a serial xunit runner config." >&2
  echo "Add an xunit.runner.json beside the .csproj containing:" >&2
  echo '    { "parallelizeTestCollections": false, "maxParallelThreads": 1 }' >&2
  echo "Without it the project inherits the shared PARALLEL config and its container fixtures" >&2
  echo "start concurrently, which the kernel kills (exit 134/137) as mystery fixture failures." >&2
  exit 1
fi

echo "PASS: all ${#projects[@]} integration project(s) resolve a serial xunit runner config."
exit 0
