#!/usr/bin/env bash
# ci-enforce-flags.test.sh — author≠impl lock for Sprint 849 Lane F1 (kc148w / FR-A2.5 follow-up).
#
# Locks the S848-A2 lesson "no appears-enforced-but-isn't gates": every CI `_ENFORCE` flag must be
# either genuinely enforcing (mutant-proven non-vacuous, like ARCH_ENFORCE) OR removed grep-absent.
# Composes gate-full-guard-suite (enumerate, don't sample) + verify-before-claiming (the behavioral
# block independently REPRODUCES the enforcement RED rather than trusting the report).
#
# Static guards (fast, always run):
#   S1  AC-F1.1 — removed dead flags TRIM_ENFORCE / COVERAGE_ENFORCE are grep-absent as honoring code
#                 across .github/workflows + eng (the only allowed mention is the FLAGS.md "do not
#                 re-add" guard note).
#   S2  AC-F1.2 — PackageMapDriftTests is wired ENFORCING (the test exists under its non-report-only
#                 name and reads ARCH_ENFORCE), not the old dead `_ReportOnly` scaffolding.
# Behavioral non-vacuity proof (slow; opt-in via F1_RUN_DOTNET=1 — runs `dotnet test`):
#   B1  EC-F1.1 — clean tree + ARCH_ENFORCE=true  -> PASS (don't flip a flag onto a red tree)
#   B2  AC-F1.2 — phantom package-map entry + ARCH_ENFORCE=true -> FAIL (enforcement is load-bearing)
#   B3  AC-F1.2 — phantom package-map entry, no flag           -> PASS (report-only; the flag enforces)
#
# Run: bash eng/ci/ci-enforce-flags.test.sh           (static guards only)
#      F1_RUN_DOTNET=1 bash eng/ci/ci-enforce-flags.test.sh   (+ behavioral mutant proof)

set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$REPO_ROOT" || { echo "FATAL: cannot cd to repo root $REPO_ROOT" >&2; exit 3; }

FAILURES=0
pass() { printf '  [PASS] %s\n' "$1"; }
fail() { printf '  [FAIL] %s\n' "$1" >&2; FAILURES=$((FAILURES + 1)); }

PMT="tests/architecture/Boundary.Tests/Architecture/PackageMapDriftTests.cs"

printf '== F1 ci-enforce-flags regression lock ==\n'

# ── S1: removed dead flags are grep-absent as honoring code ──────────────────
# Exclude the FLAGS.md "do not re-add" guard note and THIS lock file itself (both name the flags to
# assert/forbid their reintroduction — the self-referential-scanner case from security-rules.md).
dead_hits="$(grep -rnE 'TRIM_ENFORCE|COVERAGE_ENFORCE' .github/workflows eng 2>/dev/null \
	| grep -vE 'eng/ci/FLAGS\.md|eng/ci/ci-enforce-flags\.test\.sh' || true)"
if [ -n "$dead_hits" ]; then
	fail "S1 TRIM_ENFORCE/COVERAGE_ENFORCE reappeared as honoring code:\n$dead_hits"
else
	pass "S1 dead flags TRIM_ENFORCE/COVERAGE_ENFORCE grep-absent (AC-F1.1)"
fi

# ── S2: PackageMapDrift wired ENFORCING (not report-only scaffolding) ────────
if [ ! -f "$PMT" ]; then
	fail "S2 PackageMapDriftTests not found at $PMT"
elif grep -q 'PackageMap_Should_Correlate_With_Existing_Projects' "$PMT" \
	&& grep -q 'ARCH_ENFORCE' "$PMT" \
	&& ! grep -q '_ReportOnly' "$PMT"; then
	pass "S2 PackageMapDrift wired to ARCH_ENFORCE, no _ReportOnly scaffolding (AC-F1.2)"
else
	fail "S2 PackageMapDrift not wired-enforcing (missing test/ARCH_ENFORCE, or stale _ReportOnly)"
fi

# ── Behavioral non-vacuity proof (opt-in) ────────────────────────────────────
if [ "${F1_RUN_DOTNET:-0}" = "1" ]; then
	MAP="management/package-map.yaml"
	PROJ="tests/architecture/Boundary.Tests/Boundary.Tests.csproj"
	FILTER="FullyQualifiedName~PackageMapDriftTests.PackageMap_Should_Correlate_With_Existing_Projects"
	MAP_BAK="$(mktemp)"
	cp "$MAP" "$MAP_BAK"
	restore_map() { cp "$MAP_BAK" "$MAP"; rm -f "$MAP_BAK"; }
	trap restore_map EXIT

	dotnet build "$PROJ" -v q >/dev/null 2>&1 || fail "build of Boundary.Tests failed"

	# B1: clean tree under enforcement must PASS (EC-F1.1 green-first).
	if ARCH_ENFORCE=true dotnet test "$PROJ" --no-build --filter "$FILTER" >/dev/null 2>&1; then
		pass "B1 clean tree + ARCH_ENFORCE=true -> PASS (EC-F1.1)"
	else
		fail "B1 clean tree + ARCH_ENFORCE=true expected PASS (don't flip onto a red tree)"
	fi

	printf '  - id: "NonExistent.Phantom.Project"\n' >> "$MAP"

	# B2: phantom entry under enforcement must FAIL (non-vacuous).
	if ARCH_ENFORCE=true dotnet test "$PROJ" --no-build --filter "$FILTER" >/dev/null 2>&1; then
		fail "B2 phantom + ARCH_ENFORCE=true expected FAIL (enforcement is vacuous!)"
	else
		pass "B2 phantom + ARCH_ENFORCE=true -> FAIL (enforcement load-bearing, AC-F1.2)"
	fi

	# B3: phantom entry without the flag stays report-only PASS.
	if dotnet test "$PROJ" --no-build --filter "$FILTER" >/dev/null 2>&1; then
		pass "B3 phantom, no flag -> PASS (report-only; the flag is what enforces)"
	else
		fail "B3 phantom, no flag expected report-only PASS"
	fi

	restore_map
	trap - EXIT
else
	printf '  [skip] behavioral mutant proof (set F1_RUN_DOTNET=1 to run dotnet test; verified green by author)\n'
fi

printf '== %s ==\n' "$([ "$FAILURES" -eq 0 ] && echo 'ALL GREEN' || echo "$FAILURES FAILED")"
[ "$FAILURES" -eq 0 ]
