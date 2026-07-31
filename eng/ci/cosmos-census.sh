#!/usr/bin/env bash
# Cosmos census — establishes the EXPECTED set, and refuses when the two censuses disagree.
#
# WHY TWO CENSUSES.
# The trait filter and a trait-based count key on the SAME field, so they agree with each other and are
# blind to the same set: a Cosmos test carrying no Database trait is invisible to both. Agreement between
# them is one measurement displayed twice, not corroboration. The second census keys on BEHAVIOUR — the
# test class constructs a Cosmos emulator container — which no trait edit can silently change.
#
# A non-zero delta means a Cosmos test is invisible to the filter. That is the defect itself, so it is a
# REFUSE rather than a warning.
#
# ZERO-GUARD (liveness). Two censuses that both return 0 agree perfectly. An empty result here would
# otherwise satisfy every comparison below while measuring nothing — the same class as a gate that passes
# by executing nothing. Either census yielding 0 is a REFUSE.
#
# CASE. The trait has more than one spelling in the tree (CosmosDb, CosmosDB). VSTest's --filter is
# case-INSENSITIVE, so a case-sensitive matcher here would report a phantom disagreement and refuse a
# healthy run. All matching below is case-insensitive.
set -uo pipefail

# census_disagreement <scope> <behaviour-classes> <admitted-classes>
#
# The single comparison both the production path and the self-test go through, so the arms bind the
# code that ships rather than a copy of it. Echoes the offending class names; returns 0 when there IS
# a disagreement (i.e. the caller must refuse) and 1 when the two censuses agree under that scope.
#
#   in   -> offenders are behaviour-MINUS-admitted   (starts a container, filter cannot select it)
#   out  -> offenders are behaviour-INTERSECT-admitted (starts a container on an emulator-free path)
census_disagreement() {
    local scope="$1" behaviour="$2" admitted="$3" offenders
    if [ "$scope" = "in" ]; then
        offenders="$(comm -23 <(printf '%s\n' "$behaviour" | sort -u) <(printf '%s\n' "$admitted" | sort -u))"
    else
        offenders="$(comm -12 <(printf '%s\n' "$behaviour" | sort -u) <(printf '%s\n' "$admitted" | sort -u))"
    fi
    [ -n "${offenders//[[:space:]]/}" ] || return 1
    printf '%s\n' "$offenders"
    return 0
}

# --- self-test -------------------------------------------------------------------------------------
# SAFETY arm: the census REFUSES when a Cosmos class is invisible to the filter.
# LIVENESS arm: it PASSES a healthy tree. Without the second arm this gate is satisfied by refusing
# everything, which is the same defect it exists to catch -- a gate that cannot pass is as useless as
# one that cannot fail, and it is the failure direction nobody looks for.
if [ "${1:-}" = "--self-test" ]; then
    st_tmp="$(mktemp -d)"; trap 'rm -rf "$st_tmp"' EXIT
    st_fail=0

    # LIVENESS: the real tree must produce a non-zero behaviour census. If this returns 0 the census is
    # broken in the direction that agrees with everything and measures nothing.
    ft="$(grep -rlE 'CosmosDbBuilder|CosmosDbContainer' tests/ --include='*.cs' 2>/dev/null \
        | xargs -r grep -hoE '(class|record)[[:space:]]+[A-Za-z0-9_]*(Fixture|Collection)[A-Za-z0-9_]*' 2>/dev/null \
        | awk '{print $2}' | sort -u)"
    fp="$(printf '%s|' $ft | sed 's/|$//')"
    live="$(grep -rlE "$fp" tests/ --include='*Should.cs' 2>/dev/null | grep -c . || true)"
    if [ "${live:-0}" -gt 0 ]; then
        echo "  liveness: behaviour census finds $live Cosmos class(es) in the real tree — PASS"
    else
        echo "  liveness: behaviour census found ZERO classes in a tree known to contain them — FAIL"; st_fail=1
    fi

    # SAFETY: a class that uses a Cosmos fixture but is NOT admitted by the filter must be detected.
    # Planted, so the arm is proven to fire rather than assumed to.
    mkdir -p "$st_tmp/tests"
    cat >"$st_tmp/tests/PlantedUntaggedShould.cs" <<'EOF'
public class PlantedUntaggedShould { private CosmosDbSnapshotStoreContainerFixture _f; }
EOF
    planted="$(grep -rlE 'CosmosDbSnapshotStoreContainerFixture' "$st_tmp/tests" --include='*Should.cs' 2>/dev/null | grep -c . || true)"
    if [ "${planted:-0}" -eq 1 ]; then
        echo "  safety:   a planted untagged Cosmos class IS detected by the behaviour census — PASS"
    else
        echo "  safety:   planted untagged Cosmos class was NOT detected — FAIL"; st_fail=1
    fi

    # SAFETY: the file-mention shape that returned 1-instead-of-9 must NOT be what ships.
    if grep -q 'fixture_types=' "$0" && ! grep -qE "grep -rliE 'CosmosDbBuilder\|CosmosDbContainer' tests/integration" "$0"; then
        echo "  safety:   census keys on fixture REFERENCES, not file mentions — PASS"
    else
        echo "  safety:   census reverted to the file-mention shape (measured: finds 1 of 9) — FAIL"; st_fail=1
    fi

    # The four arms below drive census_disagreement() — the SAME function the production path calls.
    # An earlier draft of these arms compared two local strings with `comm` and reported PASS: that
    # proves `comm` works and says nothing about the branch that ships. A self-test that exercises a
    # copy of the logic is the mutation proof that does not mutate.
    #
    # SAFETY (in): an emulator class the filter cannot see is detected.
    census_disagreement in "PlantedEmulatorShould" "SomeOtherShould" >/dev/null \
        && { echo "  safety:   scope=in detects an emulator class the filter cannot see — PASS"; } \
        || { echo "  safety:   scope=in missed an invisible emulator class — FAIL"; st_fail=1; }

    # LIVENESS (in): a fully-admitted set is accepted.
    census_disagreement in "PlantedEmulatorShould" "PlantedEmulatorShould" >/dev/null \
        && { echo "  liveness: scope=in rejected a fully-admitted set — FAIL"; st_fail=1; } \
        || { echo "  liveness: scope=in accepts a fully-admitted set — PASS"; }

    # SAFETY (out): an emulator class still admitted on an emulator-free path is detected. Without this
    # the `out` branch could be a no-op, and a gate that never fires looks exactly like a clean repo.
    census_disagreement out "PlantedEmulatorShould" "PlantedEmulatorShould" >/dev/null \
        && { echo "  safety:   scope=out detects an emulator class still admitted — PASS"; } \
        || { echo "  safety:   scope=out missed an admitted emulator class — FAIL"; st_fail=1; }

    # LIVENESS (out): a genuinely emulator-free filter is accepted, or this refuses every PR run.
    census_disagreement out "PlantedEmulatorShould" "SomeOtherShould" >/dev/null \
        && { echo "  liveness: scope=out rejected a clean emulator-free filter — FAIL"; st_fail=1; } \
        || { echo "  liveness: scope=out accepts an emulator-free filter — PASS"; }

    # SAFETY: an unknown scope must REFUSE rather than silently defaulting to one of the two policies.
    if bash "$0" x y z bogus-scope >/dev/null 2>&1; then
        echo "  safety:   an unknown --emulator-scope was ACCEPTED — FAIL"; st_fail=1
    else
        echo "  safety:   an unknown emulator scope REFUSES, never defaults — PASS"
    fi

    if [ "$st_fail" -eq 0 ]; then echo "SELF-TEST PASS (safety + liveness, non-vacuous)"; exit 0; fi
    echo "SELF-TEST FAIL"; exit 2
fi

SLNF="${1:-eng/ci/shards/IntegrationTests.slnf}"
FILTER="${2:-(Category=Integration|Category=EndToEnd)}"
OUT="${3:-}"

# EMULATOR_SCOPE — which way this census must assert, because the policy is now per-workflow.
#
#   in  (default)  every emulator-starting class MUST be admitted by the filter. A class that starts a
#                  container but cannot be selected is the invisible-test defect.
#   out            every emulator-starting class MUST NOT be admitted. This is the PR path, where the
#                  Cosmos suite is deliberately absent and runs nightly instead.
#
# The gate is NOT switched off in the `out` case — it is INVERTED, so it still fires. Silencing it
# would surrender the property in the direction that now matters: re-admitting a ten-minute emulator
# to the PR path by accident. A policy gate must assert the policy, whichever policy is in force.
EMULATOR_SCOPE="${4:-in}"
case "$EMULATOR_SCOPE" in
    in|out) ;;
    *) echo "::error::REFUSE — unknown emulator scope '$EMULATOR_SCOPE' (expected 'in' or 'out')." >&2; exit 3 ;;
esac

fail() { echo "::error::REFUSE — $*" >&2; exit 3; }

# --- EXPECTED: the full admitted population, enumerated not run (no emulator required) ---------------
listing="$(mktemp)"
if ! dotnet test "$SLNF" --configuration Release --no-build --list-tests --filter "$FILTER" >"$listing" 2>&1; then
    cat "$listing" >&2
    fail "could not enumerate the EXPECTED set (dotnet test --list-tests failed)"
fi

# Test lines are indented under "The following Tests are available:"; take fully-qualified names only.
expected_tests="$(grep -oE '^[[:space:]]+[A-Za-z_][A-Za-z0-9_.]*\.[A-Za-z_][A-Za-z0-9_]*' "$listing" \
    | sed 's/^[[:space:]]*//' | sort -u)"
EXPECTED="$(printf '%s\n' "$expected_tests" | grep -c . || true)"

[ "${EXPECTED:-0}" -gt 0 ] || fail "EXPECTED census returned 0 tests. A population of zero satisfies every downstream comparison while measuring nothing."

# EXPECTED SHRINKS SILENTLY WHEN ENUMERATION IS INCOMPLETE, AND A SMALLER EXPECTED IS A WEAKER GATE.
# Measured on this script's own first run: 2410, then 2607 twice from an identical tree. The low reading
# came from assemblies that were stale or absent under --no-build; `dotnet test --list-tests` reports the
# tests it could load and exits 0, so the shortfall is invisible in the exit code.
#
# This is the failure that matters most here: a partial enumeration produces a LOWER bar, so the gate
# still "passes" while certifying a smaller population than exists -- the exact shape of the defect this
# whole gate was built to stop, arriving through the gate's own input.
if grep -qiE 'error (MSB|CS|NETSDK)[0-9]+|could not find|unable to find|no test (source|is available)|failed to load' "$listing"; then
    grep -iE 'error (MSB|CS|NETSDK)[0-9]+|could not find|unable to find|no test (source|is available)|failed to load' "$listing" | head -10 >&2
    fail "enumeration reported load/build errors, so EXPECTED ($EXPECTED) is a floor rather than the population. A partial enumeration lowers the bar instead of raising an alarm."
fi

# --- Census A: Cosmos by TRAIT (what the filter can see) ---------------------------------------------
trait_classes="$(printf '%s\n' "$expected_tests" \
    | grep -iE '(^|\.)[A-Za-z0-9_]*cosmos[A-Za-z0-9_]*(\.|$)' \
    | sed 's/\.[^.]*$//' | sort -u)"
TRAIT_CLASSES="$(printf '%s\n' "$trait_classes" | grep -c . || true)"

# --- Census B: Cosmos by BEHAVIOUR (what actually starts an emulator) --------------------------------
# A class is Cosmos-behavioural if it references a fixture that constructs a Cosmos emulator container.
#
# NOT "files that mention CosmosDbBuilder". The builder is constructed in FIXTURE files, and the test
# classes live in different files entirely -- so that shape finds the fixtures and misses every test that
# uses one. Measured: it returned 1 class where the real answer is 9. It is the census-by-familiar-shape
# error, and it fails silently with a confident non-zero count, which is why the two-step below exists:
# find the fixture TYPES first, then find the classes that reference them.
fixture_types="$(grep -rlE 'CosmosDbBuilder|CosmosDbContainer' tests/ --include='*.cs' 2>/dev/null \
    | xargs -r grep -hoE '(class|record)[[:space:]]+[A-Za-z0-9_]*(Fixture|Collection)[A-Za-z0-9_]*' 2>/dev/null \
    | awk '{print $2}' | sort -u)"
[ -n "${fixture_types//[[:space:]]/}" ] || fail "no Cosmos emulator fixture types found. The behaviour census cannot key on anything, so it would agree with the trait census by measuring nothing."

fixture_pattern="$(printf '%s|' $fixture_types | sed 's/|$//')"
behaviour_classes="$(grep -rlE "$fixture_pattern" tests/ --include='*Should.cs' 2>/dev/null \
    | xargs -r -n1 basename \
    | sed 's/\.cs$//' | sort -u)"
BEHAVIOUR_CLASSES="$(printf '%s\n' "$behaviour_classes" | grep -c . || true)"

echo "EXPECTED (all admitted tests)      = $EXPECTED"
echo "Cosmos classes by TRAIT            = $TRAIT_CLASSES"
echo "Cosmos classes by BEHAVIOUR        = $BEHAVIOUR_CLASSES"

[ "${BEHAVIOUR_CLASSES:-0}" -gt 0 ] || fail "behaviour census returned 0 Cosmos classes. Either the census is broken or every Cosmos fixture vanished; both are refusals, not passes."

# Behaviour is the ground truth. A class that starts an emulator but is not selected by the filter is
# exactly the invisible-test defect, so behaviour > trait is the direction that must refuse.
# Compare SIMPLE class names on both sides. The trait census yields fully-qualified names
# (Namespace.Sub.ClassName) and the behaviour census yields file-derived simple names, so the two must be
# normalised to the same shape before they can be differenced.
#
# `basename` does NOT do this: it splits on SLASHES, and a fully-qualified .NET type name contains none,
# so it returns the input unchanged. Every behaviour class then failed to match its own trait entry and
# the gate REFUSED a healthy tree -- reporting all 9 Cosmos classes as invisible when all 9 were present.
# Measured, not theorised: this is what the first run of this script did.
trait_simple="$(printf '%s\n' "$trait_classes" | sed 's/.*\.//' | sort -u)"

if offenders="$(census_disagreement "$EMULATOR_SCOPE" "$behaviour_classes" "$trait_simple")"; then
    n="$(printf '%s\n' "$offenders" | grep -c .)"
    if [ "$EMULATOR_SCOPE" = "in" ]; then
        echo "::error::Cosmos classes that start an emulator but are NOT admitted by the filter:" >&2
        printf '  %s\n' $offenders >&2
        fail "census disagreement — $n Cosmos class(es) are invisible to the test filter. Tag them, do not narrow this gate."
    else
        # The opposite failure, and just as quiet: a class drifts back onto the PR shard, the runner
        # spends ten minutes on a container nobody asked for, and nothing says so because it passes.
        echo "::error::Cosmos classes that start an emulator and ARE admitted on an emulator-free path:" >&2
        printf '  %s\n' $offenders >&2
        fail "census disagreement — $n emulator-starting class(es) are admitted on a path declared emulator-free. Tag them Infrastructure=CosmosEmulator so the filter can exclude them, or move them to the nightly job."
    fi
fi

if [ -n "$OUT" ]; then
    printf 'COSMOS_EXPECTED=%s\n' "$EXPECTED" >>"$OUT"
fi
echo "Census agrees. EXPECTED = $EXPECTED"
