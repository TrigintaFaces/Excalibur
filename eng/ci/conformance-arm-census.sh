#!/usr/bin/env bash
# Conformance arm census.
#
# Emits, per shipped conformance kit: the arms it declares, and for every suite deriving that kit,
# how many of those arms are actually wired to a test runner.
#
# WHY THIS EXISTS. A kit's arms carry no runner attribute -- the deriver supplies it. So a deriver
# that omits a wrapper does not fail: the arm silently never runs, and the suite is green. Absence
# reads as clean. Only OutboxStoreConformanceTestKit and SnapshotStoreConformanceTestKit carry a
# ConformanceSuite_ShouldWireEveryArm arm that turns that silence into a named failure; for every
# other kit this census is the only thing that reports it.
#
# It also emits the per-kit arm counts that consumer-facing documentation quotes, so those figures
# can be generated rather than hand-copied. A hand-copied count is wrong the moment an arm is added.
#
# Modes:
#   (default)     report the census; always exit 0
#   --check       exit 1 if any deriver leaves an arm unwired  (advisory gate, opt-in)
#   --self-test   prove the census is non-vacuous; exit 3 on failure
#
# Exit codes: 0 ok  1 --check found unwired arms  3 --self-test failed
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
KIT_DIR="src/Excalibur/Excalibur.Testing.Conformance/Conformance"

# An arm is a PUBLIC virtual parameterless member returning void, Task or ValueTask. VOID is
# load-bearing and was missing: a kit whose arms are synchronous is not a hypothetical -- DbConformance
# TestKit is 9 void arms and RetryPolicyConformanceTestKit is 8, so a Task-only pattern reported BOTH
# as zero arms, and PersistenceProviderConformanceTestKit as 6 of its 23. Zero arms is what a kit with
# no arms looks like, so the under-count read as clean -- this census's own stated failure mode,
# applied to itself. Public is load-bearing too: the
# kits' own reflection predicate uses BindingFlags.Public, which is exactly what excludes the
# protected lifecycle helpers (CleanupAsync, ResetDataAsync, CreateStoreForArmAsync) without
# needing to name them. A kit whose arms are protected is reported separately rather than as zero.
arm_pattern='^[[:space:]]*public virtual (async )?(void|Task|ValueTask) [A-Za-z_][A-Za-z0-9_]*\(\)'
protected_arm_pattern='^[[:space:]]*protected virtual (async )?(void|Task|ValueTask) [A-Za-z_][A-Za-z0-9_]*\(\)'

# Lifecycle helpers are parameterless virtual Task members too, so a pattern that finds arms finds
# these as well. For a PUBLIC-arm kit they fall out for free -- they are protected, and the public
# pattern never sees them. For a PROTECTED-arm kit there is no such luck: they land in the count and
# inflate it by one per helper. Named explicitly rather than inferred, so adding a helper is a
# deliberate edit here rather than a silent +1 in every doc figure generated from this census.
lifecycle_helpers='^(CleanupAsync|ResetDataAsync|CreateStoreForArmAsync|InitializeAsync|DisposeAsync)$'

arms_of() { grep -cE "$arm_pattern" "$1" 2>/dev/null || true; }
arm_names_of() {
	grep -oE "$arm_pattern" "$1" 2>/dev/null \
		| sed -E 's/.*(void|Task|ValueTask) //; s/\(\)$//' || true
}

# A file is a PROBE, not a deriver, only when EVERY derivation in it is on a `private` (nested)
# class -- a targeted fixture for the kit's own helpers or for one property, which is not claiming
# to cover the contract.
#
# The test is deliberately a narrow NEGATIVE. The obvious positive form ("does this file declare a
# public top-level class deriving the kit?") is structurally blind to a base list that wraps to the
# next line, and to extra modifiers or interfaces. That blindness demotes real provider suites to
# probes, which HIDES gaps -- strictly worse than over-reporting them.
is_probe_only() {
	local file="$1" kit="$2" all priv
	all="$(grep -cE "class[[:space:]]+[A-Za-z0-9_]+.*:.*$kit" "$file" 2>/dev/null || true)"
	priv="$(grep -cE "private[[:space:]].*class[[:space:]]+[A-Za-z0-9_]+.*:.*$kit" "$file" 2>/dev/null || true)"
	[ "${all:-0}" -gt 0 ] && [ "${priv:-0}" -eq "${all:-0}" ]
}

self_test() {
	local tmp pass=1
	tmp="$(mktemp -d)"
	trap 'rm -rf "$tmp"' RETURN

	cat >"$tmp/Kit.cs" <<'FIXTURE'
	public virtual async Task RealArm_One() { }
	public virtual Task RealArm_TwoNonAsync() { }
	public virtual async ValueTask RealArm_ThreeValueTask() { }
	public virtual void RealArm_FourVoid() { }
	protected virtual Task CleanupAsync() => Task.CompletedTask;
	protected virtual Task ResetDataAsync() => CleanupAsync();
	public virtual async Task NotAnArm_HasParams(string id) { }
	public async Task NotAnArm_NotVirtual() { }
FIXTURE

	local n; n="$(arms_of "$tmp/Kit.cs")"
	if [ "$n" -ne 4 ]; then
		echo "self-test FAIL: expected 4 arms, got $n" >&2; pass=0
	fi
	# A synchronous arm is an arm. Two shipped kits declare nothing else, so a pattern blind to void
	# reports them as zero -- indistinguishable from a kit that genuinely has no arms.
	if ! arm_names_of "$tmp/Kit.cs" | grep -q '^RealArm_FourVoid$'; then
		echo "self-test FAIL: blind to a void arm" >&2; pass=0
	fi
	# the non-async Task shape is the one a naive 'virtual async Task' grep misses
	if ! arm_names_of "$tmp/Kit.cs" | grep -q '^RealArm_TwoNonAsync$'; then
		echo "self-test FAIL: blind to a non-async Task arm" >&2; pass=0
	fi
	if arm_names_of "$tmp/Kit.cs" | grep -qE 'CleanupAsync|ResetDataAsync'; then
		echo "self-test FAIL: a protected lifecycle helper was counted as an arm" >&2; pass=0
	fi
	if arm_names_of "$tmp/Kit.cs" | grep -q 'HasParams'; then
		echo "self-test FAIL: a parameterised member was counted as an arm" >&2; pass=0
	fi
	if arm_names_of "$tmp/Kit.cs" | grep -q 'NotVirtual'; then
		echo "self-test FAIL: a non-virtual member was counted as an arm" >&2; pass=0
	fi
	# and it must find protected-arm kits rather than silently reporting zero
	local prot
	prot="$(grep -oE "$protected_arm_pattern" "$tmp/Kit.cs" 		| sed -E 's/.*(void|Task|ValueTask) //; s/\(\)$//' 		| grep -cvE "$lifecycle_helpers" || true)"
	if [ "${prot:-0}" -ne 0 ]; then
		echo "self-test FAIL: protected count should exclude every lifecycle helper; non-excluded count was $prot, expected 0" >&2; pass=0
	fi

	[ "$pass" -eq 1 ] || { echo "self-test: FAILED" >&2; exit 3; }
	echo "self-test: PASS (counts 4 arms incl. non-async Task and void; excludes helpers, params, non-virtual)"
}

main() {
	local mode="${1:-report}"
	cd "$REPO_ROOT"
	[ "$mode" = "--self-test" ] && { self_test; exit 0; }

	local gaps=0
	printf '%-46s %5s  %s\n' "KIT" "ARMS" "DERIVERS (wired/arms)"
	printf '%s\n' "-------------------------------------------------------------------------------"

	local kit kitname arms protected_arms derivers d dname wired unwired
	for kit in "$KIT_DIR"/*ConformanceTestKit.cs; do
		[ -e "$kit" ] || continue
		kitname="$(basename "$kit" .cs)"
		arms="$(arms_of "$kit")"
			protected_arms="$(grep -oE "$protected_arm_pattern" "$kit" 2>/dev/null 			| sed -E 's/.*(void|Task|ValueTask) //; s/\(\)$//' 			| grep -cvE "$lifecycle_helpers" || true)"

		if [ "$arms" -eq 0 ] && [ "$protected_arms" -gt 0 ]; then
			printf '%-46s %5s  %s\n' "$kitname" "$protected_arms" \
				"(arms are PROTECTED - wrappers not detectable by name; review by hand)"
			continue
		fi
		[ "$arms" -eq 0 ] && continue

		derivers=""
		probes=""
		while IFS= read -r cand; do
			[ -n "$cand" ] || continue
			if is_probe_only "$cand" "$kitname"; then
				probes="$probes $(basename "$cand" .cs)"
			else
				derivers="$derivers$cand"$'
'
			fi
		# Working-tree scan, NOT `git grep`. `git grep` reads the index, so a suite that exists on disk
		# but has not been staged is invisible to it — and a brand-new deriver is exactly the thing this
		# census is asked about. Reporting a just-added provider suite as absent is the census agreeing
		# with the gap it was written to find.
		done <<<"$(find tests -name '*.cs' -type f -not -path '*/obj/*' -not -path '*/bin/*' \
			-exec grep -l ": $kitname" {} + 2>/dev/null || true)"
		derivers="$(printf '%s' "$derivers" | sed '/^$/d')"
		if [ -z "$derivers" ]; then
			printf '%-46s %5s  %s\n' "$kitname" "$arms" "NO DERIVER - every arm unreachable${probes:+ (probes only:$probes)}"
			gaps=$((gaps + 1))
			continue
		fi

		printf '%-46s %5s\n' "$kitname" "$arms"
		while IFS= read -r d; do
			[ -n "$d" ] || continue
			dname="$(basename "$d" .cs)"
			wired=0
			unwired=""
			while IFS= read -r a; do
				[ -n "$a" ] || continue
				if grep -q "$a" "$d" 2>/dev/null; then
					wired=$((wired + 1))
				else
					unwired="$unwired $a"
				fi
			done < <(arm_names_of "$kit")

			if [ "$wired" -lt "$arms" ]; then
				printf '      %-40s %3s/%-3s  UNWIRED:%s\n' "$dname" "$wired" "$arms" "$unwired"
				gaps=$((gaps + 1))
			else
				printf '      %-40s %3s/%-3s\n' "$dname" "$wired" "$arms"
			fi
		done <<<"$derivers"
		[ -n "$probes" ] && printf '      %-40s %s
' "(probes, not derivers)" "$probes"
	done

	echo
	if [ "$gaps" -gt 0 ]; then
		echo "census: $gaps deriver(s)/kit(s) leave arms unreachable."
		[ "$mode" = "--check" ] && exit 1
	else
		echo "census: every deriver wires every arm."
	fi
	exit 0
}

main "$@"
