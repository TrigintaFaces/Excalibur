#!/usr/bin/env bash
# cosmos-fixture-pattern-gate.sh — every Cosmos emulator fixture must use the ruled client pattern.
#
# WHAT THIS EXISTS TO PREVENT
# ---------------------------
# One fixture drifted from four and nobody noticed until every one of its 17 tests failed.
#
# The emulator advertises databaseAccountEndpoint=http://127.0.0.1:8081 in writableLocations, so
# after the initial account read the SDK routes every data-plane call to THAT address rather than
# to the mapped port. Four fixtures avoided this by using the container's own connection string and
# HttpClient; the fifth hand-built a connection string and omitted the HttpClientFactory, and its
# calls went nowhere -- 219x GatewayCalls(0,0) against a container that reported healthy the whole
# time, because health lives on 8080 and the data plane on 8081.
#
# It cost six people an evening to rediscover by hand what this grep answers in a second. The
# instance is not the point: the point is fixture number six, added three sprints from now by
# someone who never read that thread.
#
# THE RULED PATTERN (SoftwareArchitect, candidate 2 -- "match the four"):
#     _container.GetConnectionString()                    <- not a hand-built AccountEndpoint=
#     .WithHttpClientFactory(() => _container.HttpClient)  <- present
#
# THREE STATES, matching the other gates in this directory:
#   0 PASS     every Cosmos fixture uses the ruled pattern
#   1 FAIL     a fixture has drifted
#   2 REFUSE   no fixtures could be found, so NOTHING was measured (an empty oracle passes vacuously)
set -uo pipefail

# shellcheck source=/dev/null
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/gate-denominator.sh"

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCAN_ROOT="${COSMOS_FIXTURE_SCAN_ROOT:-$REPO/tests/integration}"

# A Cosmos emulator fixture is one that CONSTRUCTS the container. A file that merely mentions Cosmos
# is not a fixture, and keying on the constructor is what makes this list self-maintaining -- a new
# fixture is enrolled by the act of building a container, not by being added to a list here.
find_fixtures() {
  grep -rl "CosmosDbBuilder()" "$SCAN_ROOT" --include="*.cs" 2>/dev/null | sort
}

check_fixture() {
  # Returns 0 conforming, 1 drifted. Echoes the reason when drifted.
  local f="$1" problems=""

  grep -q "WithHttpClientFactory" "$f" \
    || problems="${problems} missing-WithHttpClientFactory"

  # A hand-built connection string bypasses the container's own endpoint. This is the exact shape
  # that failed: AccountEndpoint=http://...:port assembled in the fixture.
  if grep -qE 'AccountEndpoint=http' "$f"; then
    problems="${problems} hand-built-connection-string"
  fi

  if [ -n "$problems" ]; then
    echo "${f#"$REPO"/}:${problems}"
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
  # is actually fine. Every gate whose self-test passes in CI uses this form.
  self="$(cd "$(dirname "$0")" && pwd)/$(basename "$0")"
  printf '\ncosmos-fixture-pattern-gate — self-test\n\n'

  # L1 LIVENESS — a conforming fixture must PASS. Without this arm a gate that rejects everything
  # looks perfect, which is the failure mode a pattern gate is most prone to.
  cat > "$tmp/Good.cs" <<'EOF'
var _container = new CosmosDbBuilder().WithImage("x").Build();
var cs = _container.GetConnectionString();
builder.WithHttpClientFactory(() => _container.HttpClient);
EOF
  rc=0; COSMOS_FIXTURE_SCAN_ROOT="$tmp" bash "$self" >/dev/null 2>&1 || rc=$?
  if [ "$rc" -eq 0 ]; then echo "  PASS  L1 LIVENESS conforming fixture -> exit 0"
  else echo "  FAIL  L1 LIVENESS conforming fixture -> exit $rc (expected 0)"; fails=$((fails+1)); fi

  # S1 SAFETY — the omission that actually happened: no HttpClientFactory.
  cat > "$tmp/BadNoFactory.cs" <<'EOF'
var _container = new CosmosDbBuilder().WithImage("x").Build();
var cs = _container.GetConnectionString();
EOF
  rc=0; COSMOS_FIXTURE_SCAN_ROOT="$tmp" bash "$self" >/dev/null 2>&1 || rc=$?
  if [ "$rc" -eq 1 ]; then echo "  PASS  S1 SAFETY   missing WithHttpClientFactory -> exit 1"
  else echo "  FAIL  S1 SAFETY   missing WithHttpClientFactory -> exit $rc (expected 1)"; fails=$((fails+1)); fi
  rm -f "$tmp/BadNoFactory.cs"

  # S2 SAFETY — the other half: a hand-built connection string.
  cat > "$tmp/BadHandBuilt.cs" <<'EOF'
var _container = new CosmosDbBuilder().WithImage("x").Build();
var cs = $"AccountEndpoint=http://{host}:{port}/;AccountKey=k;";
builder.WithHttpClientFactory(() => _container.HttpClient);
EOF
  rc=0; COSMOS_FIXTURE_SCAN_ROOT="$tmp" bash "$self" >/dev/null 2>&1 || rc=$?
  if [ "$rc" -eq 1 ]; then echo "  PASS  S2 SAFETY   hand-built connection string -> exit 1"
  else echo "  FAIL  S2 SAFETY   hand-built connection string -> exit $rc (expected 1)"; fails=$((fails+1)); fi
  rm -f "$tmp/BadHandBuilt.cs"

  # R1 REFUSE — no fixtures found means nothing was measured. Never a pass.
  rc=0; COSMOS_FIXTURE_SCAN_ROOT="$tmp/empty" bash "$self" >/dev/null 2>&1 || rc=$?
  if [ "$rc" -eq 2 ]; then echo "  PASS  R1 REFUSE   no fixtures found -> exit 2, distinct from PASS and FAIL"
  else echo "  FAIL  R1 REFUSE   no fixtures found -> exit $rc (expected 2)"; fails=$((fails+1)); fi

  printf '\n  %s\n\n' "$([ "$fails" -eq 0 ] && echo "4 passed, 0 failed" || echo "$fails failed")"
  [ "$fails" -eq 0 ] || return 1
  return 0
}

[ "${1:-}" = "--self-test" ] && { self_test; exit $?; }

FIXTURES="$(find_fixtures)"
if [ -z "$FIXTURES" ]; then
  echo "REFUSE: no Cosmos fixtures found under $SCAN_ROOT (searched for CosmosDbBuilder())" >&2
  echo "        Measured NOTHING. This is NOT a pass." >&2
  exit 2
fi

DRIFTED=""
N=0
for f in $FIXTURES; do
  N=$((N + 1))
  reason="$(check_fixture "$f")" || DRIFTED="${DRIFTED}${reason}"$'\n'
done

if [ -n "$DRIFTED" ]; then
  echo "FAIL: Cosmos fixture(s) drifted from the ruled client pattern:" >&2
  printf '%s' "$DRIFTED" | while IFS= read -r line; do [ -n "$line" ] && echo "  - $line" >&2; done
  echo "" >&2
  echo "  The emulator advertises 127.0.0.1:8081 in writableLocations, so the SDK routes data-plane" >&2
  echo "  calls there rather than to the mapped port. Use _container.GetConnectionString() and" >&2
  echo "  .WithHttpClientFactory(() => _container.HttpClient) -- the pattern the other fixtures use." >&2
  echo "  A drifted fixture fails EVERY test against a container that reports perfectly healthy." >&2
  exit 1
fi

# The denominator, in the standard machine-readable form: what was EXAMINED, not only what was
# FOUND. The zero case already REFUSEs above; this states the earned denominator out loud.
gate_denominator "$N" "Cosmos fixture(s)" || exit 2
echo "PASS: all $N Cosmos fixtures use the ruled client pattern"
exit 0
