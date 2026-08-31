#!/usr/bin/env bash
# conformance-fork-gate.sh — a conformance suite that derives a kit we do NOT publish is verifying a
# contract no consumer has. This gate detects a NEW one.
#
# THE DEFECT CLASS (a real one, and the worst-shaped verification defect available):
#
#   Excalibur.Testing.Conformance is a PUBLISHED package. Every arm in it is a contract we impose on any
#   consumer who implements their own provider. When our own providers instead derive a private base that
#   lives under tests/, three things become true at once and none of them is visible from either side:
#
#     - the contract we publish is verified by nobody, including us;
#     - our providers pass a suite no consumer can obtain;
#     - and the two drift apart indefinitely with nothing detecting it.
#
#   That is strictly worse than shipping no kit at all, because it manufactures confidence in a contract
#   that was never exercised. A consumer reads the kit's arms as the definition of conformance. If our
#   providers are held to a different set, the published arms are a claim we have not tested and the
#   internal arms are a standard we do not ship.
#
#   It hides because BOTH suites are green. The internal base has derivers and passes; the shipped kit
#   compiles and publishes. Nothing in a build, a test run, or a coverage report compares the two — the
#   only artifact that could is a scan that knows which types are inside the package boundary.
#
# WHAT THIS GATE ASSERTS: every concrete conformance suite under tests/ derives a kit declared INSIDE the
#   shipped package, or is a named, enumerated exception in the baseline below.
#
# WHY A BASELINE, WHEN A SUPPRESSION CAP WOULD BE FALSE SAFETY (read before removing it):
#
#   A cap mutes an UNKNOWN quantity — it lets a gate absorb a defect it never named, which is the
#   false-safety class gates exist to remove. An enumerated baseline is the opposite: every exception is
#   written down by name, it cannot absorb anything it does not already list, and a new fork is a FAIL on
#   its first appearance. The tree carries known forks today; without the enumeration this gate would be
#   red on arrival, could not be wired, and would therefore detect nothing at all. An inert gate is not a
#   stricter gate.
#
#   The baseline is SHRINK-ONLY by construction: an entry that no longer corresponds to a real fork is a
#   FAIL, not a silent pass. So the list cannot rot, and cannot re-admit a fork under a recycled name.
#   It is a debt ledger with a ratchet, not an escape hatch.
#
# SCOPE: C# under tests/. Base-list extraction is NEWLINE-INSENSITIVE — C# permits the base list to wrap
#   onto a later line, and in this repository an entire provider family does exactly that. A line-oriented
#   scan reports those suites as having no base at all, which reads as clean. That is not a tuning
#   preference; it is the difference between seeing the family and not seeing it.
#
# EXIT CODES (every one mapped by the caller; a non-0/1 is NEVER a pass):
#   0  PASS    scanned, suites evaluated, no unbaselined fork and no stale baseline entry
#   1  FAIL    a suite derives a conformance kit outside the shipped package, or a baseline entry is stale
#   2  REFUSE  could not evaluate (no shipped kits found / no tests tree / zero suites seen == blind)
#   3  REFUSE  --self-test failed (the gate itself is broken or vacuous)
#   *  REFUSE  unknown arg == could-not-evaluate
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SHIPPED_DIR="src/Excalibur/Excalibur.Testing.Conformance"
TESTS_DIR="tests"

# ── Baseline ────────────────────────────────────────────────────────────────────────────────────────
# Conformance bases that live under tests/ and are derived by real suites TODAY. Each line is a debt
# entry: the family is verified against a private contract instead of the one we publish. Removing a
# line is the fix; adding one requires a deliberate edit here and should be argued, not assumed.
#
# An entry that stops matching a real fork FAILS this gate — see the shrink-only note above.
BASELINE_DEFAULT="$REPO_ROOT/eng/ci/conformance-fork-baseline.txt"

usage_refuse() { echo "REFUSE: unknown argument '${1:-}'" >&2; exit 2; }

# Build the declaration index ONCE, in a single pass over the tree. Everything downstream reads this
# file, so cost is O(files) rather than O(files x bases) -- the naive shape does not terminate here.
#
# Each emitted row is:   <abstract|concrete> TAB <declared-name> TAB <base-list> TAB <file>
#
# The extraction is deliberately NEWLINE-INSENSITIVE: slurp mode plus /s, so a base list that wraps onto
# a later line is still one match. Comments are stripped first so a type named only in documentation is
# never mistaken for a derivation. C# 12 primary constructors are handled -- a parameter list may sit
# between the identifier and the colon, and a pattern that requires the colon to follow the name is
# structurally blind to every primary-constructor type in the tree.
build_index() {
	local root="$1" out="$2"
	find "$root/$TESTS_DIR" -name '*.cs' -type f \
		-not -path '*/obj/*' -not -path '*/bin/*' -print0 2>/dev/null \
	| perl -0 -ne '
		BEGIN { $/ = "\0"; }
		chomp;
		my $f = $_;
		open(my $fh, "<", $f) or next;
		local $/;
		my $t = <$fh>;
		close $fh;
		next unless defined $t;
		$t =~ s{/\*.*?\*/}{ }gs;      # block + doc comments
		$t =~ s{//[^\n]*}{ }g;        # line comments
		while ($t =~ /((?:public|internal|private|protected|sealed|abstract|partial|static|file)\s+)*
		               \b(?:class|record)\s+([A-Za-z0-9_]+)\s*
		               (?:<[^>{]*>)?\s*
		               (?:\([^)]*\))?\s*
		               :\s*([^{]+?)\s*(?:\{|where\s)/gsx) {
			my ($mods, $name, $bases) = ($1 // "", $2, $3);
			my $kind = ($& =~ /\babstract\b/) ? "abstract" : "concrete";
			$bases =~ s/\s+/ /g;
			print join("\t", $kind, $name, $bases, $f), "\n";
		}
	' > "$out"
}

# First file declaring a CONCRETE class whose base list names $base; empty if none.
concrete_deriver_of() {
	local idx="$1" base="$2"
	awk -F'\t' -v b="$base" '
		$1 == "concrete" && $3 ~ ("(^|[^A-Za-z0-9_])" b "([^A-Za-z0-9_]|$)") { print $4; exit }
	' "$idx"
}

# Names of conformance kit types declared INSIDE the shipped package == the oracle.
shipped_kits() {
	local root="$1"
	grep -rhoE '(class|record)[[:space:]]+[A-Za-z0-9_]*Conformance[A-Za-z0-9_]*' \
		"$root/$SHIPPED_DIR" --include=*.cs 2>/dev/null \
		| sed -E 's/^(class|record)[[:space:]]+//' | sort -u
}

# Abstract conformance bases declared under tests/ == candidate forks.
internal_bases() {
	local root="$1"
	grep -rhoE 'abstract[[:space:]]+class[[:space:]]+[A-Za-z0-9_]*Conformance[A-Za-z0-9_]*' \
		"$root/$TESTS_DIR" --include=*.cs 2>/dev/null \
		| sed -E 's/.*class[[:space:]]+//' | sort -u
}

run_gate() {
	local root="$1" baseline="$2"
	local kits bases forks=() stale=() seen=0 idx

	[ -d "$root/$SHIPPED_DIR" ] || { echo "REFUSE: shipped package not found at $SHIPPED_DIR" >&2; return 2; }
	[ -d "$root/$TESTS_DIR" ]   || { echo "REFUSE: tests tree not found at $TESTS_DIR" >&2; return 2; }

	kits="$(shipped_kits "$root")"
	[ -n "$kits" ] || { echo "REFUSE: zero conformance kits found in the shipped package -- the scan is blind" >&2; return 2; }

	bases="$(internal_bases "$root")"

	idx="$(mktemp)" || { echo "REFUSE: cannot create index" >&2; return 2; }
	build_index "$root" "$idx"

	local baselined=""
	[ -f "$baseline" ] && baselined="$(grep -vE '^[[:space:]]*(#|$)' "$baseline" 2>/dev/null || true)"

	local b deriver
	while IFS= read -r b; do
		[ -n "$b" ] || continue
		# A name that is ALSO declared in the shipped package is not a fork.
		if printf '%s\n' "$kits" | grep -qxF "$b"; then continue; fi
		deriver="$(concrete_deriver_of "$idx" "$b")"
		if [ -n "$deriver" ]; then
			seen=$((seen + 1))
			if printf '%s\n' "$baselined" | grep -qxF "$b"; then continue; fi
			forks+=("$b|$deriver")
		fi
	done < <(printf '%s\n' "$bases")

	# Shrink-only ratchet: a baseline entry with no corresponding live fork is stale.
	if [ -n "$baselined" ]; then
		while IFS= read -r b; do
			[ -n "$b" ] || continue
			if ! printf '%s\n' "$bases" | grep -qxF "$b"; then stale+=("$b"); continue; fi
			[ -n "$(concrete_deriver_of "$idx" "$b")" ] || stale+=("$b")
		done < <(printf '%s\n' "$baselined")
	fi

	# Blindness floor, with a POSITIVE CONTROL. A zero here is ambiguous between "no fork exists" and
	# "the extraction is broken", and those must not report the same way. The control is the shipped
	# kits' own derivers: if the tree declares conformance bases, resolves no fork, AND cannot resolve a
	# single suite deriving a kit we KNOW is present, then the instrument has proven nothing and the
	# zero is not a finding.
	local control=0 k
	while IFS= read -r k; do
		[ -n "$k" ] || continue
		if [ -n "$(concrete_deriver_of "$idx" "$k")" ]; then control=$((control + 1)); break; fi
	done < <(printf '%s\n' "$kits")

	if [ "$seen" -eq 0 ] && [ "$control" -eq 0 ] && [ -z "$baselined" ] && [ -n "$bases" ]; then
		echo "REFUSE: conformance bases exist under $TESTS_DIR, no fork resolved, and the positive control" >&2
		echo "        (a suite deriving a shipped kit) resolved nothing either -- the query is blind." >&2
		rm -f "$idx"; return 2
	fi

	local rc=0
	if [ "${#forks[@]}" -gt 0 ]; then
		rc=1
		echo "FAIL: conformance suites derive a kit that is NOT in the shipped package." >&2
		echo "      A consumer implementing this provider cannot obtain the contract these suites verify." >&2
		for f in "${forks[@]}"; do
			echo "      - ${f%%|*}   (e.g. ${f##*|})" >&2
		done
		echo "      Fix: derive ${SHIPPED_DIR##*/}'s kit, or add the name to $(basename "$baseline") with a reason." >&2
	fi
	if [ "${#stale[@]}" -gt 0 ]; then
		rc=1
		echo "FAIL: stale baseline entries -- these no longer name a live fork and must be REMOVED." >&2
		echo "      A baseline that keeps dead names can re-admit a fork under a recycled name." >&2
		for s in "${stale[@]}"; do echo "      - $s" >&2; done
	fi

	[ "$rc" -eq 0 ] && echo "PASS: $seen baselined conformance fork(s); no new fork, no stale entry."
	rm -f "$idx"
	return "$rc"
}

# ── Self-test ───────────────────────────────────────────────────────────────────────────────────────
self_test() {
	local tmp rc fails=0
	tmp="$(mktemp -d)"
	trap 'rm -rf "$tmp"' RETURN

	mk() { # mk <root> ; builds a minimal tree with a shipped kit
		mkdir -p "$1/$SHIPPED_DIR/Conformance" "$1/$TESTS_DIR/suite"
		cat > "$1/$SHIPPED_DIR/Conformance/WidgetStoreConformanceTestKit.cs" <<'EOF'
public abstract class WidgetStoreConformanceTestKit { public virtual Task A() => Task.CompletedTask; }
EOF
	}

	arm() { # arm <name> <expected-rc> <root>
		local name="$1" want="$2" root="$3" got
		run_gate "$root" "${4:-/nonexistent-baseline}" >/dev/null 2>&1
		got=$?
		if [ "$got" -ne "$want" ]; then
			echo "  self-test FAIL: $name -- expected exit $want, got $got" >&2
			fails=$((fails + 1))
		else
			echo "  self-test ok:   $name (exit $got)"
		fi
	}

	# LIVENESS 1 — a suite deriving the SHIPPED kit is clean.
	mk "$tmp/clean"
	cat > "$tmp/clean/$TESTS_DIR/suite/GoodShould.cs" <<'EOF'
public sealed class GoodShould : WidgetStoreConformanceTestKit { }
EOF
	arm "clean tree (derives shipped kit) PASSES" 0 "$tmp/clean"

	# SAFETY 2 — a NEW fork on one line FAILS.
	mk "$tmp/fork1"
	cat > "$tmp/fork1/$TESTS_DIR/suite/ForkBase.cs" <<'EOF'
public abstract class WidgetStoreConformanceTestBase { }
EOF
	cat > "$tmp/fork1/$TESTS_DIR/suite/ForkShould.cs" <<'EOF'
public sealed class ForkShould : WidgetStoreConformanceTestBase { }
EOF
	arm "new fork (single-line base list) FAILS" 1 "$tmp/fork1"

	# SAFETY 3 — a NEW fork whose base list WRAPS onto the next line FAILS. This is the arm that proves
	# the scan is newline-insensitive; a line-oriented gate passes this tree and is therefore blind.
	mk "$tmp/fork2"
	cat > "$tmp/fork2/$TESTS_DIR/suite/ForkBase.cs" <<'EOF'
public abstract class WidgetStoreConformanceTestBase<T> { }
EOF
	cat > "$tmp/fork2/$TESTS_DIR/suite/WrappedShould.cs" <<'EOF'
public sealed class WrappedShould
    : WidgetStoreConformanceTestBase<int>, IAsyncLifetime
{
}
EOF
	arm "new fork (WRAPPED base list) FAILS" 1 "$tmp/fork2"

	# LIVENESS 4 — a baselined fork PASSES (otherwise the gate is unwirable on a tree with known debt).
	printf 'WidgetStoreConformanceTestBase\n' > "$tmp/bl.txt"
	arm "baselined fork PASSES" 0 "$tmp/fork1" "$tmp/bl.txt"

	# SAFETY 5 — a STALE baseline entry FAILS (the shrink-only ratchet).
	printf 'WidgetStoreConformanceTestBase\nGhostConformanceTestBase\n' > "$tmp/bl2.txt"
	arm "stale baseline entry FAILS" 1 "$tmp/fork1" "$tmp/bl2.txt"

	# SAFETY 6 — an abstract-only intermediate is NOT a deriver, so it must not raise a fork.
	# The tree deliberately ALSO carries a real shipped-kit suite: a tree whose only conformance types
	# are abstract is degenerate, and the blindness floor rightly REFUSES it. Testing the narrow property
	# requires a tree where the instrument is demonstrably able to resolve a suite.
	mk "$tmp/absonly"
	cat > "$tmp/absonly/$TESTS_DIR/suite/GoodShould.cs" <<'EOF'
public sealed class GoodShould : WidgetStoreConformanceTestKit { }
EOF
	cat > "$tmp/absonly/$TESTS_DIR/suite/AbsBase.cs" <<'EOF'
public abstract class WidgetStoreConformanceTestBase { }
public abstract class MiddleShould : WidgetStoreConformanceTestBase { }
EOF
	arm "abstract-only intermediate is not a fork" 0 "$tmp/absonly"

	# REFUSE 6b — a tree that declares conformance bases but resolves NO suite is blind, not clean.
	# This is the arm that stops a broken regex from reading as a green tree.
	mk "$tmp/deadbase"
	cat > "$tmp/deadbase/$TESTS_DIR/suite/AbsBase.cs" <<'EOF'
public abstract class WidgetStoreConformanceTestBase { }
EOF
	arm "bases present but zero suites resolved REFUSES" 2 "$tmp/deadbase"

	# REFUSE 7 — no shipped package == cannot evaluate; must NOT report clean.
	mkdir -p "$tmp/blind/$TESTS_DIR"
	arm "missing shipped package REFUSES (not a pass)" 2 "$tmp/blind"

	if [ "$fails" -gt 0 ]; then
		echo "self-test: $fails arm(s) failed" >&2
		return 3
	fi
	echo "self-test: all arms passed"
	return 0
}

case "${1:-}" in
	"")           cd "$REPO_ROOT" && run_gate "$REPO_ROOT" "$BASELINE_DEFAULT"; exit $? ;;
	--self-test)  self_test; exit $? ;;
	-h|--help)    sed -n '2,40p' "${BASH_SOURCE[0]}"; exit 0 ;;
	*)            usage_refuse "$1" ;;
esac
