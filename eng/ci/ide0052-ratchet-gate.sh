#!/usr/bin/env bash
# ide0052-ratchet-gate.sh — the compiler's own unread-private-member detector, re-armed and ratcheted.
#
# THE DEFECT CLASS:
#
#   A private member that is ASSIGNED and never READ is a dependency that does nothing. The shape is
#   always the same and always invisible at review: a constructor takes a factory and stores it in a
#   field nobody reads; a subscriber stores its message handler and never invokes it; a scope stores
#   its timeout and never applies it; a serializer option is stored and never passed, so the default
#   is silently used instead. Every one compiles, ships, and passes its tests — because a test that
#   constructs the type and asserts on its OUTPUT cannot see a field the type never consults.
#
#   The compiler already detects this. It is IDE0052. This repository configured it as an error under
#   [*.{cs,csx,cake}] and then downgraded it to `suggestion` for every C# file in a later [*.cs]
#   section, with the comment "tracked for incremental cleanup, visible as suggestions". A suggestion
#   is not emitted by the build at all, so for the whole life of that line the detector reported to
#   nobody. Two sibling detectors for the same shape are off with it: CA1823 (unused private field)
#   sits in NoWarn, and CS9113 (unread primary-constructor parameter) is also `suggestion`.
#
# WHY A BASELINE AND NOT A FLIP: re-arming found a population too large to fix in one reviewable
#   change. Flipping to error on day one would have redded every build and been switched back off
#   within the hour — which is how the original downgrade happened. The population is instead frozen
#   in eng/ci/ide0052-baseline.txt and can only shrink.
#
# THE TWO ARMS, AND WHY THE CONFIG ARM EXISTS:
#
#   ARM 1 (--check-armed, cheap, always runs): the detector is still turned ON. It asserts that the
#     LAST globally-scoped section of .editorconfig to set IDE0052 sets it to `warning` or `error`,
#     and that EnforceCodeStyleInBuild is not disabled for src. This arm exists because of HOW this
#     defect originally shipped: not by anyone deleting the check, but by a later section quietly
#     overriding an earlier one. Without this arm, re-downgrading IDE0052 to `suggestion` would make
#     the log arm below find zero sites and report a clean PASS — a gate reporting a pass it did not
#     earn, which is the exact false-safety this file exists to prevent.
#
#   ARM 2 (--log <build.log>): given a build log, every IDE0052 site in src/ must already be in the
#     baseline. A site not in the baseline is a NEW unread member and FAILS. A baseline entry that no
#     longer appears is STALE — reported as a warning, not a failure, mirroring gate-wiring.sh, so
#     that cleaning a site in a parallel change never reds CI on a race.
#
#   ARM 2 REFUSES rather than passes when the log holds no evidence a build actually ran. A log with
#   zero IDE0052 lines is indistinguishable, from this gate's position, between "the tree is clean"
#   and "nothing compiled" / "the log is truncated" / "the analyzer never ran". REFUSE, don't guess —
#   a clean bill of health from a scan that did not happen is worse than no scan at all.
#
# Usage:
#   ide0052-ratchet-gate.sh --check-armed        — assert the detector is still enabled (cheap)
#   ide0052-ratchet-gate.sh --log <build.log>    — ratchet a build log against the baseline
#   ide0052-ratchet-gate.sh --self-test          — prove the gate is non-vacuous (must run first in CI)
#
# Exit codes: 0 PASS · 1 FAIL (detector disarmed, or a new unread member) ·
#             2 REFUSE (blind: unreadable baseline/config, or a log with no build evidence) ·
#             3 self-test harness failure (unknown argument, etc.)

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="${IDE0052_ROOT:-$(cd "$SCRIPT_DIR/../.." && pwd)}"
BASELINE="${IDE0052_BASELINE:-$SCRIPT_DIR/ide0052-baseline.txt}"

check_armed() {
	python3 - "$1" <<'PYEOF'
import os, re, sys
root = sys.argv[1]
ec = os.path.join(root, ".editorconfig")
if not os.path.isfile(ec):
    print("REFUSE: no .editorconfig at %s" % ec); sys.exit(2)
try:
    text = open(ec, encoding="utf-8").read()
except OSError as e:
    print("REFUSE: cannot read .editorconfig: %s" % e); sys.exit(2)

# Walk sections IN ORDER and remember the severity set by the LAST section applying to every .cs
# file. The original defect was precisely an earlier section being overridden by a later one, so the
# section ORDER is the thing under test -- not the mere presence of an `= error` line somewhere.
section, last_sev, last_sec, saw_any = None, None, None, False
GLOBAL_CS = ("*.cs", "*.{cs,csx,cake}", "*.{cs,csx,cake,vb,vbx}", "*.{cs,vb}")
for raw in text.split("\n"):
    line = raw.strip()
    if line.startswith("[") and line.endswith("]"):
        section = line[1:-1]
        continue
    m = re.match(r"dotnet_diagnostic\.IDE0052\.severity\s*=\s*(\w+)", line)
    if not m or section is None:
        continue
    saw_any = True
    # A narrowly-scoped section (a path glob, a specific file list) is NOT a global override and
    # must not be read as one -- otherwise a legitimate per-file exemption reads as a disarm.
    if section in GLOBAL_CS:
        last_sev, last_sec = m.group(1), section

if not saw_any:
    print("REFUSE: no IDE0052 severity set anywhere in .editorconfig"); sys.exit(2)
if last_sev is None:
    print("REFUSE: IDE0052 is set, but by no globally-scoped section this gate recognises"); sys.exit(2)
if last_sev not in ("warning", "error"):
    print("FAIL: IDE0052 is DISARMED -- last global section [%s] sets severity = %s." % (last_sec, last_sev))
    print("      `suggestion` and `none` are not emitted by the build, so the detector reports to nobody.")
    sys.exit(1)
print("armed: last global section [%s] sets IDE0052 severity = %s" % (last_sec, last_sev))

# EnforceCodeStyleInBuild=false silences the IDE analyzers wholesale, regardless of severity.
props = os.path.join(root, "src", "Directory.Build.props")
if os.path.isfile(props):
    if re.search(r"<EnforceCodeStyleInBuild>\s*false\s*</EnforceCodeStyleInBuild>",
                 open(props, encoding="utf-8").read(), re.I):
        print("FAIL: src/Directory.Build.props sets EnforceCodeStyleInBuild=false -- IDE analyzers do not run.")
        sys.exit(1)
sys.exit(0)
PYEOF
}

ratchet_log() {
	python3 - "$1" "$2" <<'PYEOF'
import os, re, sys
log_path, baseline_path = sys.argv[1], sys.argv[2]
BS = chr(92)

if not os.path.isfile(log_path):
    print("REFUSE: build log not found: %s" % log_path); sys.exit(2)
if not os.path.isfile(baseline_path):
    print("REFUSE: baseline not found: %s" % baseline_path); sys.exit(2)
try:
    log = open(log_path, encoding="utf-8", errors="replace").read()
    base_raw = open(baseline_path, encoding="utf-8").read()
except OSError as e:
    print("REFUSE: cannot read input: %s" % e); sys.exit(2)

baseline = set()
for l in base_raw.split("\n"):
    l = l.strip()
    if l and not l.startswith("#"):
        baseline.add(l)
if not baseline:
    print("REFUSE: baseline holds no entries -- refusing to read an empty ledger as 'nothing to compare'")
    sys.exit(2)

rx = re.compile(r"^(.*?)[(](\d+),(\d+)[)]: warning IDE0052: Private member '(.*?)' can be removed")
found = {}
for line in log.split("\n"):
    m = rx.match(line.strip())
    if not m:
        continue
    f, ln, _col, mem = m.groups()
    f = f.replace(BS, "/")
    i = f.find("src/")
    if i < 0:
        continue                      # only src/ is in this population; see the baseline header
    found.setdefault("%s::%s" % (f[i:], mem), ln)

# A log with no IDE0052 lines is ambiguous between a clean tree and a scan that never happened.
# Require positive evidence a build ran before believing a zero.
if not found and not re.search(r"Time Elapsed|Build succeeded|Build FAILED|error [A-Z]{2}\d|warning [A-Z]{2}\d", log):
    print("REFUSE: log has no IDE0052 sites AND no evidence a build ran (%s)." % log_path)
    print("        A zero from a scan that did not happen is not a clean bill of health.")
    sys.exit(2)

new = sorted(set(found) - baseline)
stale = sorted(baseline - set(found))

# STALE entries are only meaningful against a WHOLE-SOLUTION log. Fed a single-project log, every
# other project's baselined sites are "missing" purely because that project was not compiled -- and
# printing "delete this line" for each one instructs the reader to erase live debt on the strength of
# a scan that never looked at it. Absence of a site is only evidence when the site was in scope.
# So: summarise by default, and list them only when the caller asserts a full-solution log.
if stale:
    if os.environ.get("IDE0052_REPORT_STALE") == "1":
        for k in stale:
            print("::warning::STALE baseline entry (site no longer reported -- delete this line): %s" % k)
    else:
        print("note: %d baseline entr(y/ies) not reported in this log. Not listed: on a partial log that"
              % len(stale))
        print("      means 'not compiled', not 'fixed'. Re-run with IDE0052_REPORT_STALE=1 against a")
        print("      whole-solution build log to get the list that is safe to delete.")

if new:
    print("::error::%d NEW unread private member(s) -- assigned and never read:" % len(new))
    for k in new:
        path, mem = k.split("::", 1)
        print("  %s:%s  %s" % (path, found[k], mem))
    print("")
    print("A field written and never read is a dependency that does nothing. Either read it, or")
    print("remove it AND its constructor parameter. Do NOT add it to the baseline: that ledger is")
    print("frozen debt and it only shrinks.")
    sys.exit(1)

print("PASS: %d IDE0052 site(s) in src/, all already baselined (%d entries, %d stale)."
      % (len(found), len(baseline), len(stale)))
sys.exit(0)
PYEOF
}

self_test() {
	local tmp rc fails=0
	tmp="$(mktemp -d)"
	trap 'rm -rf "$tmp"' RETURN

	arm() { # arm <name> <expected-rc> <actual-rc>
		if [ "$2" -eq "$3" ]; then
			echo "  ok   $1 (rc=$3)"
		else
			echo "  FAIL $1 (expected rc=$2, got rc=$3)"
			fails=$((fails + 1))
		fi
	}

	# Fixture logs use forward slashes: the parser normalises separators, so this exercises the same
	# path without smuggling backslash-quoting bugs into the fixtures themselves.
	local site="src/X/A.cs(9,3): warning IDE0052: Private member 'A._known' can be removed as the value assigned to it is never read"
	local newsite="src/X/B.cs(4,7): warning IDE0052: Private member 'B._brandNew' can be removed as the value assigned to it is never read"

	echo "-- ARM A: an armed .editorconfig PASSES (liveness: the gate can say yes)"
	mkdir -p "$tmp/a"
	printf '[*.cs]\ndotnet_diagnostic.IDE0052.severity = warning\n' > "$tmp/a/.editorconfig"
	check_armed "$tmp/a" >/dev/null 2>&1; rc=$?; arm "armed config passes" 0 "$rc"

	echo "-- ARM B: THE ORIGINAL DEFECT -- an early error overridden by a later suggestion -- FAILS"
	mkdir -p "$tmp/b"
	printf '[*.{cs,csx,cake}]\ndotnet_diagnostic.IDE0052.severity = error\n\n[*.cs]\ndotnet_diagnostic.IDE0052.severity = suggestion\n' > "$tmp/b/.editorconfig"
	check_armed "$tmp/b" >/dev/null 2>&1; rc=$?; arm "later suggestion overriding earlier error fails" 1 "$rc"

	echo "-- ARM C: severity=none FAILS"
	mkdir -p "$tmp/c"
	printf '[*.cs]\ndotnet_diagnostic.IDE0052.severity = none\n' > "$tmp/c/.editorconfig"
	check_armed "$tmp/c" >/dev/null 2>&1; rc=$?; arm "severity=none fails" 1 "$rc"

	echo "-- ARM D: EnforceCodeStyleInBuild=false FAILS even with severity armed"
	mkdir -p "$tmp/d/src"
	printf '[*.cs]\ndotnet_diagnostic.IDE0052.severity = warning\n' > "$tmp/d/.editorconfig"
	printf '<Project><PropertyGroup><EnforceCodeStyleInBuild>false</EnforceCodeStyleInBuild></PropertyGroup></Project>\n' > "$tmp/d/src/Directory.Build.props"
	check_armed "$tmp/d" >/dev/null 2>&1; rc=$?; arm "EnforceCodeStyleInBuild=false fails" 1 "$rc"

	echo "-- ARM E: a NARROW section must not read as a global override (no false FAIL)"
	mkdir -p "$tmp/e"
	printf '[*.cs]\ndotnet_diagnostic.IDE0052.severity = warning\n\n[tests/Shared/Only.cs]\ndotnet_diagnostic.IDE0052.severity = none\n' > "$tmp/e/.editorconfig"
	check_armed "$tmp/e" >/dev/null 2>&1; rc=$?; arm "narrow-scope none does not disarm" 0 "$rc"

	printf 'src/X/A.cs::A._known\n' > "$tmp/base.txt"

	echo "-- ARM F: a log whose only site IS baselined PASSES"
	printf '%s\nTime Elapsed 00:00:01\n' "$site" > "$tmp/ok.log"
	ratchet_log "$tmp/ok.log" "$tmp/base.txt" >/dev/null 2>&1; rc=$?; arm "baselined site passes" 0 "$rc"

	echo "-- ARM G: a NEW site FAILS (safety: the ratchet bites)"
	printf '%s\nTime Elapsed 00:00:01\n' "$newsite" > "$tmp/new.log"
	ratchet_log "$tmp/new.log" "$tmp/base.txt" >/dev/null 2>&1; rc=$?; arm "new unread member fails" 1 "$rc"

	echo "-- ARM H: an EMPTY log (no build evidence) REFUSES rather than passing"
	printf '\n' > "$tmp/empty.log"
	ratchet_log "$tmp/empty.log" "$tmp/base.txt" >/dev/null 2>&1; rc=$?; arm "no-build-evidence refuses" 2 "$rc"

	echo "-- ARM I: a real build log with zero sites PASSES (a zero IS believable with evidence)"
	printf 'Build succeeded.\nTime Elapsed 00:00:03\n' > "$tmp/clean.log"
	ratchet_log "$tmp/clean.log" "$tmp/base.txt" >/dev/null 2>&1; rc=$?; arm "clean build with evidence passes" 0 "$rc"

	echo "-- ARM J: a missing baseline REFUSES (never a silent 'nothing to compare')"
	ratchet_log "$tmp/ok.log" "$tmp/nonexistent.txt" >/dev/null 2>&1; rc=$?; arm "missing baseline refuses" 2 "$rc"

	echo ""
	if [ "$fails" -eq 0 ]; then
		echo "ide0052-ratchet-gate --self-test: 10 arms EXECUTED, all passed."
		return 0
	fi
	echo "ide0052-ratchet-gate --self-test: $fails arm(s) FAILED."
	return 1
}

case "${1:---check-armed}" in
	--check-armed) check_armed "$REPO_ROOT"; exit $? ;;
	--log)
		[ $# -ge 2 ] || { echo "usage: $0 --log <build.log>" >&2; exit 3; }
		ratchet_log "$2" "$BASELINE"; exit $?
		;;
	--self-test) self_test; exit $? ;;
	*)
		echo "unknown argument: $1" >&2
		echo "usage: $0 [--check-armed | --log <build.log> | --self-test]" >&2
		exit 3
		;;
esac
