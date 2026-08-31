#!/usr/bin/env bash
# conformance-arm-skip-gate.sh — a conformance arm that declined to run for a capability the store
# under test ACTUALLY HAS. This gate detects one.
#
# THE DEFECT CLASS:
#
#   A conformance kit verifies a contract with a required core and a set of optional capabilities. An
#   arm exercising an optional capability has to do something when the component does not provide it,
#   and the obvious thing — return — is the one thing that cannot happen silently: every test runner
#   reports an arm that returned early EXACTLY as it reports an arm that ran and passed.
#
#   The shipped kits already make that distinction observable. An arm that cannot run reports through
#   the kit's skip hook, which records the suite, the arm, the capability it wanted, and why it gave
#   up. That record is a REPORTING surface. Nothing asks it anything. So the mechanism that can tell a
#   verified capability from an unverified one exists, is correct, and has never decided anything.
#
#   This gate is what asks it. And it asks the ONE question that separates the tolerable case from the
#   intolerable one:
#
#     TOLERABLE    the store genuinely does not implement the capability. The arm has nothing to
#                  exercise; declining is correct and the recorded reason is true.
#
#     INTOLERABLE  the store DOES implement the capability and the arm still failed to reach it. The
#                  suite then reports a pass over behaviour nobody verified, and the recorded reason —
#                  "this store does not provide X" — is false about a store that provides X.
#
#   Failing on EVERY skip would be wrong. Most skips in this tree are the first kind, and a gate that
#   red-flagged them would be reverted within a day and would deserve it. The whole value is in the
#   separation.
#
# WHAT IT ASSERTS: for every capability-typed skip site declared by a shipped kit, and every suite
#   deriving that kit, the capability is either absent from the store under test (tolerable) or
#   reachable through that store's capability lookup (so the arm binds and no skip occurs).
#
# HOW THE ORACLE IS DERIVED, AND WHY IT IS SOURCE AND NOT A RUN:
#
#   The skip record names the suite, the arm and the capability. It does NOT name the store. So even a
#   gate reading a live ledger could not answer "does that store implement this capability?" from the
#   ledger — it would have to go to the source for the store type and its interfaces, which is what
#   this gate does directly. The relation is fully decidable from the tree:
#
#     1. the kit's contract interface, read from the kit's own store-factory return type — not a
#        naming convention, and not a table that goes stale the day a kit is added;
#     2. the suite's store: a type the suite CONSTRUCTS whose interface closure contains that
#        contract. Binding on the constructed type is what stops one suite's store being credited to
#        its neighbour;
#     3. the store's interface closure, resolved TRANSITIVELY. A backend here names a composite
#        interface in its base list and reaches the contract only through it, so a one-hop check reads
#        a covered store as unrelated to the kit that covers it;
#     4. whether the capability is reachable. The kits gate on the contract's service-resolution hook,
#        whose default answers for anything the instance itself implements. A store that does not
#        narrow that hook therefore reaches every capability it declares, and the arm binds. A store
#        that DOES declare its own hook, and whose body never names the capability, cannot return it —
#        so the arm declines while the store implements it. That is the defect, and it is the only
#        shape in which it can occur.
#
# WHY AN UNRESOLVABLE STORE REFUSES RATHER THAN PASSES:
#
#   A suite whose store cannot be resolved is a suite this gate has NOTHING to say about. Reporting
#   that as clean is the exact failure the gate exists to prevent, one level up: an instrument with no
#   subject rendering a pass. It refuses instead, and a refusal is never a pass to the caller.
#
# THE ONE STRUCTURAL EXEMPTION, WHICH IS NOT AN ALLOWLIST:
#
#   The kit library's own test project derives the kits to prove the kits' wiring machinery works. Those
#   derivations are PRIVATE NESTED classes whose store factory throws on purpose -- the fixture exists to
#   check that an arm is reachable, so it must never reach a store. Such a file is not a provider claiming
#   to cover the contract, and demanding a store of it puts a refusal in front of every run for a subject
#   that does not exist.
#
#   So a file whose EVERY derivation of a kit is on a private nested class is reported as a probe and not
#   judged. This is a structural property read out of the file, not a list of names: no file is named
#   anywhere, a new probe is exempt the day it is written, and a probe that grows a real provider suite
#   stops being exempt the same day. Probes are PRINTED on every run, so the exemption is visible rather
#   than silent, and a kit left with only probes still REFUSES -- an exempt subject is not a covered one.
#
#   The test is deliberately a narrow NEGATIVE, matching the sibling arm census. The obvious positive form
#   -- "does this file declare a public top-level class deriving the kit?" -- is blind to a base list that
#   wraps onto the next line, and that blindness would demote real provider suites to probes, HIDING
#   findings. A file this parse cannot read at all is treated as a deriver, so the failure direction is
#   over-reporting a subject, never dropping one.
#
# WHAT IT DOES NOT CLAIM:
#
#   Whether a kit's arms are wired to a runner at all is a different question, answered by the sibling
#   arm census. This gate speaks only about arms that DO run and then decline. And a skip site naming
#   no capability — gated on a factory hook returning nothing rather than on a capability interface —
#   carries no capability for the oracle to test, so it is counted and reported and never used to
#   fail: an unnamed capability is a question this instrument cannot ask, and pretending otherwise
#   would put a guess behind a red build.
#
# EXIT CODES (every one mapped by the caller; a non-0/1 is NEVER a pass):
#   0  PASS    scanned; every declined arm is a genuine absence, and every capability a store has is
#              reachable by the arm that needs it
#   1  FAIL    an arm declines for a capability its store implements
#   2  REFUSE  could not evaluate (no src / no tests tree / no kit dir / zero skip sites or zero
#              derivers seen == blind / a suite's store or a kit's contract unresolvable)
#   3  REFUSE  --self-test failed (the gate itself is broken or vacuous)
#   *  REFUSE  unknown arg == could-not-evaluate
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
KIT_SUBDIR="src/Excalibur/Excalibur.Testing.Conformance/Conformance"
CENSUS_PY="$(dirname "${BASH_SOURCE[0]}")/conformance-arm-skip-census.py"

# ── Census ──────────────────────────────────────────────────────────────────────────────────────────
# One python pass over the kit dir, src/ and tests/. It emits tab-separated records and renders no
# verdict; the shell decides, so the exit-code contract stays in one readable place.
#
#   NOCONTRACT <TAB> kit                                   kit's contract interface unreadable
#   NODERIVER  <TAB> kit                                   kit declares declined arms, no suite derives it
#   PROBE      <TAB> kit <TAB> suite                       the kit's own fixture, not a provider (reported, exempt)
#   NOSTORE    <TAB> kit <TAB> suite                       suite constructs no type implementing the contract
#   UNNAMED    <TAB> kit <TAB> arm                         skip site names no capability (reported, never fatal)
#   TOLERABLE  <TAB> suite <TAB> arm <TAB> cap <TAB> store genuine absence
#   BINDS      <TAB> suite <TAB> arm <TAB> cap <TAB> store store has it and the arm reaches it
#   LIE        <TAB> suite <TAB> arm <TAB> cap <TAB> store store has it and the arm cannot reach it
#   COUNTS     <TAB> kits <TAB> sites <TAB> suites

# ── Evaluation ──────────────────────────────────────────────────────────────────────────────────────

run_gate() {
	local root="$1" kitdir="${2:-$KIT_SUBDIR}"

	if [ ! -d "$root/src" ]; then
		echo "REFUSE: no src tree at $root/src -- cannot evaluate." >&2; return 2
	fi
	if [ ! -d "$root/tests" ]; then
		echo "REFUSE: no tests tree at $root/tests -- cannot evaluate." >&2; return 2
	fi
	if [ ! -d "$root/$kitdir" ]; then
		echo "REFUSE: shipped conformance kits not found at $root/$kitdir -- cannot evaluate." >&2; return 2
	fi
	if [ ! -f "$CENSUS_PY" ]; then
		echo "REFUSE: the census program is missing at $CENSUS_PY -- cannot evaluate." >&2; return 2
	fi

	local records
	records="$(python3 "$CENSUS_PY" "$root" "$kitdir")" || {
		echo "REFUSE: the census failed to run -- cannot evaluate." >&2; return 2; }

	local kits sites suites
	IFS=$'\t' read -r _ kits sites suites <<<"$(printf '%s\n' "$records" | grep '^COUNTS' | head -1)"

	if [ "${sites:-0}" -eq 0 ]; then
		echo "REFUSE: no kit under $kitdir declares a skip site. Either the kits stopped reporting" >&2
		echo "        declined arms, or this census is blind to how they do it. Not a pass." >&2
		return 2
	fi
	if [ "${suites:-0}" -eq 0 ]; then
		echo "REFUSE: no suite derives any kit that declares a skip site -- the test-side scan is" >&2
		echo "        blind and has nothing to report on. Not a pass." >&2
		return 2
	fi

	# Printed before any verdict: the exemption is visible on every run, including the runs it does
	# not change. An exemption nobody can see is an allowlist.
	local probes
	probes="$(printf '%s\n' "$records" | grep '^PROBE' || true)"
	if [ -n "$probes" ]; then
		echo "exempt as the kits' own fixtures (every derivation private+nested, no store by design):"
		printf '%s\n' "$probes" | while IFS=$'\t' read -r _ kit suite; do
			echo "  - $suite (derives $kit)"
		done
	fi

	local blind
	blind="$(printf '%s\n' "$records" | grep -E '^(NOCONTRACT|NODERIVER|NOSTORE)' || true)"
	if [ -n "$blind" ]; then
		echo "REFUSE: the oracle is underivable for these, so no verdict is rendered over them:" >&2
		printf '%s\n' "$blind" | while IFS=$'\t' read -r what a b; do
			case "$what" in
				NOCONTRACT) echo "  - $a: its store-factory return type does not name a contract interface." >&2 ;;
				NODERIVER)  echo "  - $a: declares declined arms and no suite derives it (a probe is not a deriver)." >&2 ;;
				NOSTORE)    echo "  - $a / $b: the suite constructs no type implementing the contract." >&2 ;;
			esac
		done
		return 2
	fi

	local lies tolerable binds unnamed
	lies="$(printf '%s\n' "$records" | grep -c '^LIE' || true)"
	tolerable="$(printf '%s\n' "$records" | grep -c '^TOLERABLE' || true)"
	binds="$(printf '%s\n' "$records" | grep -c '^BINDS' || true)"
	unnamed="$(printf '%s\n' "$records" | grep -c '^UNNAMED' || true)"

	echo "conformance arm skips: $binds arm/store pairs bind; $tolerable decline for a capability the"
	echo "store genuinely lacks; $lies decline for a capability the store HAS."
	if [ "$unnamed" -gt 0 ]; then
		echo "($unnamed skip site(s) name no capability; reported, not judged -- see the header.)"
	fi

	if [ "$lies" -gt 0 ]; then
		echo "FAIL: an arm declined for a capability its store implements. Each line below is a pass" >&2
		echo "reported over behaviour nobody verified, with a recorded reason that is false:" >&2
		printf '%s\n' "$records" | grep '^LIE' | while IFS=$'\t' read -r _ suite arm cap store; do
			echo "  - $suite.$arm declined [$cap]; $store implements $cap but narrows its lookup." >&2
		done
		return 1
	fi
	echo "PASS: every declined arm is a genuine absence."
	return 0
}

# ── Self-test ───────────────────────────────────────────────────────────────────────────────────────
# A gate that cannot fail is the defect it was written to find, so the failing arms are the
# load-bearing ones. Every arm below is a whole synthetic tree run through the real run_gate.

self_test() {
	local tmp fails=0
	tmp="$(mktemp -d)"
	trap 'rm -rf "$tmp"' RETURN

	arm() { # arm <name> <expected-rc> <root>
		local name="$1" want="$2" root="$3" got
		run_gate "$root" "kits" >/dev/null 2>&1
		got=$?
		if [ "$got" -ne "$want" ]; then
			echo "  self-test FAIL: $name -- expected exit $want, got $got" >&2
			fails=$((fails + 1))
		else
			echo "  self-test ok:   $name (exit $got)"
		fi
	}

	mk_base "$tmp/binds"

	# ---- PASS 1: every arm binds. The store implements both capabilities and narrows nothing, so the
	# default lookup answers for both and neither arm ever declines.
	store_full "$tmp/binds"
	arm "a store implementing every capability, narrowing nothing, PASSES" 0 "$tmp/binds"

	# ---- PASS 2: a genuine absence. The store implements neither optional capability, so both arms
	# decline truthfully. This arm is what stops the gate becoming "fail on every skip", which would
	# be wrong and would be reverted.
	cp -r "$tmp/binds" "$tmp/absent"
	store_bare "$tmp/absent"
	arm "a skip for a capability the store genuinely LACKS passes" 0 "$tmp/absent"

	# ---- FAIL 3: THE DEFECT. The store implements the admin capability and declares its own lookup
	# hook that can never return it, so the arm declines while the store has it. This is the arm that
	# makes the gate non-vacuous.
	cp -r "$tmp/binds" "$tmp/lie"
	store_narrowed "$tmp/lie"
	arm "a skip for a capability the store HAS fails" 1 "$tmp/lie"

	# ---- FAIL 3b: the same defect reached through a BASE class. A store inheriting a narrowed lookup
	# is as unreachable as one declaring it, and a check stopping at the leaf would call it clean.
	cp -r "$tmp/binds" "$tmp/lie_base"
	store_narrowed_base "$tmp/lie_base"
	arm "a narrowed lookup INHERITED from a base still fails" 1 "$tmp/lie_base"

	# ---- PASS 4: transitivity. The store reaches the contract only through a composite interface. A
	# one-hop check would not recognise it as this kit's store at all: it would resolve no store and
	# refuse, hiding whatever that store does.
	cp -r "$tmp/binds" "$tmp/transitive"
	store_composite "$tmp/transitive"
	arm "a store reaching the contract only through a composite interface is resolved" 0 "$tmp/transitive"

	# ---- FAIL 5: a PRIMARY-CONSTRUCTOR declaration whose base list WRAPS onto later lines. Both
	# shapes are invisible to the naive declaration pattern, and a census blind to them reports a
	# confident zero over every type declared that way.
	cp -r "$tmp/lie" "$tmp/primary"
	store_narrowed_primary "$tmp/primary"
	arm "a primary-constructor store with a wrapped base list is still judged (fails)" 1 "$tmp/primary"

	# ---- PASS 6: THE SHIPPED DEFAULT HOOK. The contract carries a default member answering from the
	# instance's own type, so it returns every capability the store implements and no arm declines.
	# A check reading "declares a hook whose text omits the capability" calls this a narrowing and
	# reports every store in the tree as lying -- the finding is entirely an artifact of the oracle.
	cp -r "$tmp/binds" "$tmp/reflective"
	contract_with_default_hook "$tmp/reflective"
	arm "the contract's reflective default hook is NOT a narrowing" 0 "$tmp/reflective"

	# ---- PASS 7: the same reflective answer written on the store itself.
	cp -r "$tmp/binds" "$tmp/reflective_store"
	store_reflective "$tmp/reflective_store"
	arm "a store answering from its own type is NOT a narrowing" 0 "$tmp/reflective_store"

	# ---- PASS 8: a decorator forwards what it does not add, so it answers whatever it wraps. Reading
	# a forwarding hook as a narrowing would report every decorated suite in the tree.
	cp -r "$tmp/binds" "$tmp/delegating"
	store_delegating "$tmp/delegating"
	arm "a decorator forwarding to the store it wraps is NOT a narrowing" 0 "$tmp/delegating"

	# ---- PASS 9: the structural exemption. A file whose every derivation is a private nested class
	# is the kit's own fixture; its store factory throws by design. Without the exemption this tree
	# REFUSES, which is honest and useless -- it would put a refusal in front of every run forever.
	cp -r "$tmp/binds" "$tmp/probe"
	probe_only_file "$tmp/probe"
	arm "a private nested probe with no store is exempt, not a refusal" 0 "$tmp/probe"

	# ---- FAIL 7: the exemption must not swallow a real finding. This file holds a PUBLIC deriver
	# whose store hides a capability it has, AND a private probe beside it. Demoting a file because it
	# merely CONTAINS a probe would hide the lie sitting next to it; only all-private is exempt.
	cp -r "$tmp/lie" "$tmp/probe_mixed"
	probe_beside_real_suite "$tmp/probe_mixed"
	arm "a probe beside a real suite does NOT exempt the file (still fails)" 1 "$tmp/probe_mixed"

	# ---- REFUSE 8: the suite constructs nothing implementing the contract, so the gate has no
	# subject. Reporting that as clean is an instrument with nothing to measure rendering a pass.
	cp -r "$tmp/binds" "$tmp/nostore"
	suite_unresolvable "$tmp/nostore"
	arm "an unresolvable store REFUSES (not a pass)" 2 "$tmp/nostore"

	# ---- REFUSE 7: the kit's contract cannot be read from its store factory.
	cp -r "$tmp/binds" "$tmp/nocontract"
	sed -i 's|protected abstract Task<IWidgetStore> CreateStoreAsync();|protected abstract Task<object> BuildIt();|' \
		"$tmp/nocontract/kits/WidgetStoreConformanceTestKit.cs"
	arm "an unreadable kit contract REFUSES (not a pass)" 2 "$tmp/nocontract"

	# ---- REFUSE 8: a kit with declined arms that no suite derives.
	cp -r "$tmp/binds" "$tmp/noderiver"
	rm "$tmp/noderiver/tests/Widget.Tests/SqlWidgetStoreConformanceShould.cs"
	arm "a kit with declined arms and no deriver REFUSES" 2 "$tmp/noderiver"

	# ---- REFUSE 9: no skip site anywhere == either the kits stopped reporting declined arms, or this
	# census is blind to how they report them. Either way there is nothing to certify.
	cp -r "$tmp/binds" "$tmp/nosites"
	sed -i 's|SkipArm(|Declined(|g' "$tmp/nosites/kits/WidgetStoreConformanceTestKit.cs"
	arm "zero skip sites REFUSES (the census is blind)" 2 "$tmp/nosites"

	# ---- REFUSE 10/11/12: a missing tree cannot be evaluated.
	cp -r "$tmp/binds" "$tmp/notests"; rm -rf "$tmp/notests/tests"
	arm "a missing tests tree REFUSES (not a pass)" 2 "$tmp/notests"
	cp -r "$tmp/binds" "$tmp/nokits"; rm -rf "$tmp/nokits/kits"
	arm "a missing kit directory REFUSES (not a pass)" 2 "$tmp/nokits"
	cp -r "$tmp/binds" "$tmp/nosrc"; rm -rf "$tmp/nosrc/src"
	arm "a missing src tree REFUSES (not a pass)" 2 "$tmp/nosrc"

	if [ "$fails" -ne 0 ]; then
		echo "self-test: FAILED ($fails arm(s))" >&2
		return 3
	fi
	echo "self-test: PASS (18 arms; four of them fail the gate, which is what proves it can)"
	return 0
}

# ── Self-test fixtures ──────────────────────────────────────────────────────────────────────────────
# Written by printf rather than a here-document so the arms above read as a list of properties.

mk_base() {
	local r="$1"
	mkdir -p "$r/kits" "$r/src/Widget" "$r/tests/Widget.Tests"
	printf '%s\n' \
		'public abstract class WidgetStoreConformanceTestKit : ConformanceTestKit' \
		'{' \
		'	protected abstract Task<IWidgetStore> CreateStoreAsync();' \
		'' \
		'	public virtual async Task Admin_ShouldReportCounts()' \
		'	{' \
		'		var store = await CreateStoreForArmAsync();' \
		'		if (store.GetService(typeof(IWidgetStoreAdmin)) is not IWidgetStoreAdmin admin)' \
		'		{' \
		'			SkipArm(nameof(Admin_ShouldReportCounts), typeof(IWidgetStoreAdmin), "Admin not supported");' \
		'			return;' \
		'		}' \
		'	}' \
		'' \
		'	public virtual async Task Fencing_StaleTokenShouldBeRefused()' \
		'	{' \
		'		var store = await CreateStoreForArmAsync();' \
		'		if (store.GetService(typeof(IFencedWidgetStore)) is not IFencedWidgetStore fenced)' \
		'		{' \
		'			SkipArm(nameof(Fencing_StaleTokenShouldBeRefused), typeof(IFencedWidgetStore), "No fencing.");' \
		'			return;' \
		'		}' \
		'	}' \
		'}' > "$r/kits/WidgetStoreConformanceTestKit.cs"
	printf '%s\n' \
		'public interface IWidgetStore { }' \
		'public interface IWidgetStoreAdmin { }' \
		'public interface IFencedWidgetStore : IWidgetStore { }' \
		'public interface ICompositeWidgetStore : IWidgetStore { }' > "$r/src/Widget/Contracts.cs"
	printf '%s\n' \
		'public sealed class SqlWidgetStoreConformanceShould : WidgetStoreConformanceTestKit' \
		'{' \
		'	protected override Task<IWidgetStore> CreateStoreAsync() =>' \
		'		Task.FromResult<IWidgetStore>(new SqlWidgetStore());' \
		'}' > "$r/tests/Widget.Tests/SqlWidgetStoreConformanceShould.cs"
}

store_full() {
	printf '%s\n' \
		'public sealed class SqlWidgetStore : IWidgetStore, IWidgetStoreAdmin, IFencedWidgetStore' \
		'{' \
		'	public Task StoreAsync() => Task.CompletedTask;' \
		'}' > "$1/src/Widget/SqlWidgetStore.cs"
}

store_bare() {
	printf '%s\n' \
		'public sealed class SqlWidgetStore : IWidgetStore' \
		'{' \
		'	public Task StoreAsync() => Task.CompletedTask;' \
		'}' > "$1/src/Widget/SqlWidgetStore.cs"
}

store_narrowed() {
	printf '%s\n' \
		'public sealed class SqlWidgetStore : IWidgetStore, IWidgetStoreAdmin, IFencedWidgetStore' \
		'{' \
		'	public object? GetService(Type serviceType) =>' \
		'		serviceType == typeof(IFencedWidgetStore) ? this : null;' \
		'}' > "$1/src/Widget/SqlWidgetStore.cs"
}

store_narrowed_base() {
	printf '%s\n' \
		'public abstract class NarrowWidgetStoreBase' \
		'{' \
		'	public object? GetService(Type serviceType) => null;' \
		'}' \
		'' \
		'public sealed class SqlWidgetStore : NarrowWidgetStoreBase, IWidgetStore, IWidgetStoreAdmin, IFencedWidgetStore' \
		'{' \
		'	public Task StoreAsync() => Task.CompletedTask;' \
		'}' > "$1/src/Widget/SqlWidgetStore.cs"
}

store_composite() {
	printf '%s\n' \
		'public sealed class SqlWidgetStore : ICompositeWidgetStore, IWidgetStoreAdmin, IFencedWidgetStore' \
		'{' \
		'	public Task StoreAsync() => Task.CompletedTask;' \
		'}' > "$1/src/Widget/SqlWidgetStore.cs"
}

store_narrowed_primary() {
	printf '%s\n' \
		'public sealed class SqlWidgetStore(string connection) :' \
		'	IWidgetStore,' \
		'	IWidgetStoreAdmin,' \
		'	IFencedWidgetStore' \
		'{' \
		'	public object? GetService(Type serviceType) =>' \
		'		serviceType == typeof(IFencedWidgetStore) ? this : null;' \
		'}' > "$1/src/Widget/SqlWidgetStore.cs"
}

contract_with_default_hook() {
	printf '%s\n' \
		'public interface IWidgetStore : IServiceProvider' \
		'{' \
		'	object? IServiceProvider.GetService(Type serviceType) =>' \
		'		serviceType.IsInstanceOfType(this) ? this : null;' \
		'}' \
		'public interface IWidgetStoreAdmin { }' \
		'public interface IFencedWidgetStore : IWidgetStore { }' \
		'public interface ICompositeWidgetStore : IWidgetStore { }' > "$1/src/Widget/Contracts.cs"
}

store_reflective() {
	printf '%s\n' \
		'public sealed class SqlWidgetStore : IWidgetStore, IWidgetStoreAdmin, IFencedWidgetStore' \
		'{' \
		'	public object? GetService(Type serviceType) =>' \
		'		serviceType.IsInstanceOfType(this) ? this : null;' \
		'}' > "$1/src/Widget/SqlWidgetStore.cs"
}

store_delegating() {
	printf '%s\n' \
		'public sealed class SqlWidgetStore : IWidgetStore, IWidgetStoreAdmin, IFencedWidgetStore' \
		'{' \
		'	private IWidgetStore Inner { get; init; }' \
		'' \
		'	public object? GetService(Type serviceType) => Inner.GetService(serviceType);' \
		'}' > "$1/src/Widget/SqlWidgetStore.cs"
}

probe_only_file() {
	printf '%s\n' \
		'public sealed class KitWiringShould' \
		'{' \
		'	private sealed class FullyWiredWidgetProbe : WidgetStoreConformanceTestKit' \
		'	{' \
		'		protected override Task<IWidgetStore> CreateStoreAsync() =>' \
		'			throw new NotSupportedException("never resolved");' \
		'	}' \
		'}' > "$1/tests/Widget.Tests/KitWiringShould.cs"
}

probe_beside_real_suite() {
	printf '%s\n' \
		'public sealed class SqlWidgetStoreConformanceShould : WidgetStoreConformanceTestKit' \
		'{' \
		'	protected override Task<IWidgetStore> CreateStoreAsync() =>' \
		'		Task.FromResult<IWidgetStore>(new SqlWidgetStore());' \
		'' \
		'	private sealed class WiringProbe : WidgetStoreConformanceTestKit' \
		'	{' \
		'		protected override Task<IWidgetStore> CreateStoreAsync() =>' \
		'			throw new NotSupportedException("never resolved");' \
		'	}' \
		'}' > "$1/tests/Widget.Tests/SqlWidgetStoreConformanceShould.cs"
}

suite_unresolvable() {
	printf '%s\n' \
		'public sealed class SqlWidgetStoreConformanceShould : WidgetStoreConformanceTestKit' \
		'{' \
		'	protected override Task<IWidgetStore> CreateStoreAsync() =>' \
		'		Task.FromResult<IWidgetStore>(Resolve());' \
		'}' > "$1/tests/Widget.Tests/SqlWidgetStoreConformanceShould.cs"
}

case "${1:-}" in
	"")          run_gate "$REPO_ROOT"; exit $? ;;
	--self-test) self_test; exit $? ;;
	-h|--help)   sed -n '2,80p' "${BASH_SOURCE[0]}"; exit 0 ;;
	*)           echo "REFUSE: unknown argument '${1}'. Usage: $(basename "$0") [--self-test]" >&2; exit 2 ;;
esac
