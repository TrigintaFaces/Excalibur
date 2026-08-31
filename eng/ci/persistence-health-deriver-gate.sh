#!/usr/bin/env bash
# persistence-health-deriver-gate.sh — a provider that SHIPS the health contract and is verified by
# nobody. This gate detects a NEW one.
#
# THE DEFECT CLASS:
#
#   IPersistenceProviderHealth is a contract we publish. PersistenceProviderConformanceTestKit, in the
#   shipped Excalibur.Testing.Conformance package, is how a consumer checks their own provider meets it.
#   When one of OUR providers implements the contract and no suite derives that kit against it, the
#   provider ships unverified — and the arms we impose on consumers are arms we do not ourselves run.
#
#   This is not theoretical. The required "Provider" key in GetMetricsAsync was missing or misspelled in
#   several implementations at once. The ones with derivers failed CI. The ones without were found only
#   by a hand census, and one carried the identical defect to the provider sitting beside it. A hand
#   census is not repeatable and does not run on the change that reopens the gap.
#
#   It hides because nothing is red. The provider compiles, publishes, and has tests of its own. The kit
#   compiles and publishes. No build, test run, or coverage report compares the set of implementors
#   against the set of things verified — that comparison is the only artifact that could see it.
#
# WHAT THIS GATE ASSERTS: every type under src/ that declares IPersistenceProviderHealth is bound by at
#   least one suite under tests/ that derives PersistenceProviderConformanceTestKit.
#
# WHY IT BINDS BY TYPE NAME AND NOT BY FILE OR PACKAGE:
#
#   A census keyed to filename cannot distinguish two same-named provider types in one package, and a
#   deriver that binds the wrong one reports a pass for an implementation it never exercised. Binding on
#   the constructed type name is what makes "covered" mean the thing that is actually covered. The tree
#   currently ships one such type per package; the gate does not depend on that staying true.
#
# WHY THE DECLARATION MATCH IS NEWLINE-INSENSITIVE (do not "simplify" this to a line regex):
#
#   C# permits a base list to wrap onto a later line, and in this repository several provider families do
#   exactly that. A line-oriented scan reports those types as declaring nothing, which reads as clean.
#   Measured: a single-line pattern sees 9 of the 11 implementors and silently omits three whose base
#   lists wrap. That is not a tuning preference — it is the difference between seeing a family and not.
#
# WHAT THIS GATE DOES NOT ASSERT (stated so a green is not over-read):
#
#   That the deriver is non-vacuous. The kit's health arms early-return when the provider declines the
#   capability, so a suite that does not declare IPersistenceProviderHealth in RequiredCapabilities
#   reports Passed having asserted nothing. This gate proves a deriver EXISTS; it does not prove the
#   deriver's arms can fail. Those are different properties and only the first is mechanically checkable
#   from the source tree.
#
# EXIT CODES (every one mapped by the caller; a non-0/1 is NEVER a pass):
#   0  PASS    scanned, implementors found, every one bound by a kit deriver
#   1  FAIL    an implementor has no deriver binding it
#   2  REFUSE  could not evaluate (no src tree / no tests tree / zero implementors seen == blind)
#   3  REFUSE  --self-test failed (the gate itself is broken or vacuous)
#   *  REFUSE  unknown arg == could-not-evaluate
set -uo pipefail

# shellcheck source=/dev/null
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/gate-denominator.sh"

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

CONTRACT="IPersistenceProviderHealth"
KIT="PersistenceProviderConformanceTestKit"

# ── Census helpers ──────────────────────────────────────────────────────────────────────────────────

# Emits the bare type name of every type whose base list names $CONTRACT, one per line.
# The base list is matched across newlines; see the header for why that is load-bearing.
census_implementors() {
	local src_dir="$1"
	python3 - "$src_dir" "$CONTRACT" <<'PYEOF'
import os, re, sys

# LF only. Text-mode stdout translates to CRLF on Windows, and a trailing CR makes every emitted
# name unmatchable by a whole-line compare downstream while the output still reads correctly by eye.
# That failure mode reports "nothing is covered" with total confidence, so it is fixed at the source
# here and defended again in the shell.
sys.stdout.reconfigure(newline="\n")

src_dir, contract = sys.argv[1], sys.argv[2]

# class/record/struct NAME [<generics>] [(primary ctor)] : BASES {
# re.S so the base list may wrap. The primary-constructor group is optional and makes this a strict
# superset of the naive pattern -- a C# 12 primary constructor puts a parameter list between the
# identifier and the colon, which a pattern requiring the colon to follow the name cannot match.
decl = re.compile(
    r'(?:class|record|struct)\s+(\w+)(?:<[^>]*>)?\s*(?:\([^)]*\))?\s*:\s*([^{]*?)\{',
    re.S)
word = re.compile(r'\b' + re.escape(contract) + r'\b')

for root, dirs, files in os.walk(src_dir):
    dirs[:] = [d for d in dirs if d not in ('obj', 'bin')]
    for f in files:
        if not f.endswith('.cs'):
            continue
        try:
            s = open(os.path.join(root, f), encoding='utf-8', errors='replace').read()
        except OSError:
            continue
        for m in decl.finditer(s):
            if word.search(m.group(2)):
                print(m.group(1))
PYEOF
}

# Emits the bare type name of every concrete provider a kit deriver constructs, one per line.
census_covered() {
	local tests_dir="$1"
	python3 - "$tests_dir" "$KIT" <<'PYEOF'
import os, re, sys

# LF only -- see the note in census_implementors.
sys.stdout.reconfigure(newline="\n")

tests_dir, kit = sys.argv[1], sys.argv[2]

decl = re.compile(
    r'(?:class|record|struct)\s+(\w+)(?:<[^>]*>)?\s*(?:\([^)]*\))?\s*:\s*([^{]*?)\{',
    re.S)
kit_word = re.compile(r'\b' + re.escape(kit) + r'\b')
# What the suite hands the kit. Binding on the CONSTRUCTED type is what makes "covered" name the
# implementation actually exercised, rather than the file or package it happens to sit in.
constructed = re.compile(r'\bnew\s+([A-Za-z_]\w*)\s*[\(\{]')

for root, dirs, files in os.walk(tests_dir):
    dirs[:] = [d for d in dirs if d not in ('obj', 'bin')]
    for f in files:
        if not f.endswith('.cs'):
            continue
        try:
            s = open(os.path.join(root, f), encoding='utf-8', errors='replace').read()
        except OSError:
            continue
        if not any(kit_word.search(m.group(2)) for m in decl.finditer(s)):
            continue
        for m in constructed.finditer(s):
            print(m.group(1))
PYEOF
}

# ── Evaluation ──────────────────────────────────────────────────────────────────────────────────────

run_gate() {
	local root="$1"
	local src_dir="$root/src"
	local tests_dir="$root/tests"

	if [ ! -d "$src_dir" ]; then
		echo "REFUSE: no src tree at $src_dir -- cannot evaluate." >&2
		return 2
	fi

	if [ ! -d "$tests_dir" ]; then
		echo "REFUSE: no tests tree at $tests_dir -- cannot evaluate." >&2
		return 2
	fi

	local implementors covered
	# tr -d is defence in depth against the CRLF trap the censuses already avoid at the source: a
	# reintroduced text-mode write would otherwise make every name unmatchable and report a confident,
	# total false FAIL.
	implementors="$(census_implementors "$src_dir" | tr -d '\r' | sort -u)"
	covered="$(census_covered "$tests_dir" | tr -d '\r' | sort -u)"

	if [ -z "$implementors" ]; then
		# Zero implementors is indistinguishable from a census that silently matched nothing. A gate
		# that cannot see its own subject must not report a pass.
		echo "REFUSE: found no type declaring $CONTRACT under $src_dir. Either the contract was renamed" >&2
		echo "        or the declaration match no longer discriminates; a zero here is blindness, not" >&2
		echo "        cleanliness." >&2
		return 2
	fi

	local unverified=""
	local count=0
	while IFS= read -r impl; do
		[ -z "$impl" ] && continue
		count=$((count + 1))
		if ! printf '%s\n' "$covered" | grep -qxF "$impl"; then
			unverified="${unverified}${impl}"$'\n'
		fi
	done <<< "$implementors"

	if [ -n "$unverified" ]; then
		echo "FAIL: these types declare $CONTRACT but no suite derives $KIT against them." >&2
		echo "      They ship a contract nothing verifies, while the same arms are imposed on any" >&2
		echo "      consumer writing their own provider." >&2
		echo >&2
		printf '%s' "$unverified" | sed 's/^/        /' >&2
		echo >&2
		echo "      Add a suite deriving $KIT whose CreateProvider constructs the named type, and" >&2
		echo "      declare $CONTRACT in its RequiredCapabilities -- without that declaration the" >&2
		echo "      kit's health arms early-return and report Passed having asserted nothing." >&2
		return 1
	fi

	# The denominator, in the standard machine-readable form: what was EXAMINED, not only what was
	# FOUND. The zero case is already REFUSEd above; this states the earned denominator out loud.
	gate_denominator "$count" "type(s) declaring $CONTRACT" || return 2
	echo "PASS: $count type(s) declaring $CONTRACT; every one is bound by a $KIT deriver."
	return 0
}

# ── Self-test ───────────────────────────────────────────────────────────────────────────────────────
# Proves the gate is non-vacuous: it must FAIL on a planted uncovered implementor and PASS once that
# implementor is covered. A gate that cannot go red on its own defect is a success signal with nothing
# behind it.

self_test() {
	local tmp status rc=0
	tmp="$(mktemp -d)"
	trap 'rm -rf "$tmp"' RETURN

	mkdir -p "$tmp/src/Widget" "$tmp/tests/Widget.Tests"

	# A wrapped base list, deliberately: the naive line-oriented pattern cannot see this, so the
	# self-test also pins that the newline-insensitive match is the one in force.
	cat > "$tmp/src/Widget/WidgetPersistenceProvider.cs" <<'CSEOF'
public sealed class WidgetPersistenceProvider :
	IPersistenceProvider,
	IPersistenceProviderHealth
{
}
CSEOF

	# ARM 1 -- SAFETY: an uncovered implementor must FAIL.
	run_gate "$tmp" >/dev/null 2>&1
	status=$?
	if [ "$status" -ne 1 ]; then
		echo "SELF-TEST FAIL: an uncovered implementor must exit 1, got $status." >&2
		rc=1
	fi

	# ARM 2 -- LIVENESS: once covered, the SAME tree must PASS. Without this arm a gate that fails
	# unconditionally -- including one whose census matches nothing and calls everything uncovered --
	# would satisfy arm 1 perfectly.
	cat > "$tmp/tests/Widget.Tests/WidgetPersistenceProviderConformanceShould.cs" <<'CSEOF'
public sealed class WidgetPersistenceProviderConformanceShould : PersistenceProviderConformanceTestKit
{
	protected override IPersistenceProvider CreateProvider() => new WidgetPersistenceProvider();
}
CSEOF

	run_gate "$tmp" >/dev/null 2>&1
	status=$?
	if [ "$status" -ne 0 ]; then
		echo "SELF-TEST FAIL: a covered implementor must exit 0, got $status." >&2
		rc=1
	fi

	# ARM 3 -- the binding is by TYPE, not by file or package. A deriver that constructs a DIFFERENT
	# provider must not count as coverage for this one, which is the same-name trap that lets a suite
	# report a pass for an implementation it never exercised.
	cat > "$tmp/tests/Widget.Tests/WidgetPersistenceProviderConformanceShould.cs" <<'CSEOF'
public sealed class WidgetPersistenceProviderConformanceShould : PersistenceProviderConformanceTestKit
{
	protected override IPersistenceProvider CreateProvider() => new SomeOtherPersistenceProvider();
}
CSEOF

	run_gate "$tmp" >/dev/null 2>&1
	status=$?
	if [ "$status" -ne 1 ]; then
		echo "SELF-TEST FAIL: a deriver binding a different type must not count as coverage; expected 1, got $status." >&2
		rc=1
	fi

	# ARM 4 -- REFUSE, not PASS, when the census sees nothing. A gate whose subject vanished must not
	# report clean.
	rm -f "$tmp/src/Widget/WidgetPersistenceProvider.cs"
	run_gate "$tmp" >/dev/null 2>&1
	status=$?
	if [ "$status" -ne 2 ]; then
		echo "SELF-TEST FAIL: zero implementors must REFUSE (2), not pass; got $status." >&2
		rc=1
	fi

	if [ "$rc" -eq 0 ]; then
		echo "SELF-TEST PASS: gate fails on an uncovered implementor, passes when covered, rejects a"
		echo "                wrong-type binding, and refuses rather than passing when blind."
	fi

	return "$rc"
}

# ── Entry ───────────────────────────────────────────────────────────────────────────────────────────

case "${1:-}" in
	"")
		run_gate "$REPO_ROOT"
		exit $?
		;;
	--self-test)
		if self_test; then exit 0; else exit 3; fi
		;;
	*)
		echo "REFUSE: unknown argument '${1}'. Usage: $(basename "$0") [--self-test]" >&2
		exit 2
		;;
esac
