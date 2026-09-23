#!/usr/bin/env bash
# rigor-ladder-gate.sh — a seam that owes a class of guarantee it does not produce, reported as covered.
#
# THE DEFECT CLASS:
#
#   Every guarantee-critical seam states a guarantee in its ARCHITECTURE.md and names a conformance
#   arm. That is rung R2 of the rigor ladder — the CONTRACT rung. Nothing has ever asserted that a
#   seam whose blast radius demands a HIGHER class of guarantee actually produces one, and nothing
#   notices when a seam's coverage silently shrinks: a property suite deleted, a model file renamed,
#   or a brand-new guarantee-critical package landing with no rung declared at all.
#
#   This is the same shape as architecture-evidence-gate.sh one rung up. There, a DELETED test read
#   as a STANDING one. Here, an ABSENT CLASS OF GUARANTEE reads as a covered seam, because the only
#   thing anyone looks at is whether the tests are green — and a green sampling-class suite says
#   nothing whatsoever about a state space it never enumerated.
#
# WHAT THIS GATE ASSERTS:
#   1. COVERAGE IS BIDIRECTIONAL. Every src/**/ARCHITECTURE.md has a ladder entry, and every ladder
#      entry names a real ARCHITECTURE.md. A new guarantee-critical package cannot arrive undeclared.
#   2. THE FLOOR IS DERIVED FROM THE BLAST RADIUS (catastrophic=R4, high=R3, moderate=R2, low=R1)
#      and is NOT declared anywhere. A declared floor was a transcription of that map, so a check
#      comparing the two could only ever find a typo. The field is gone; the inconsistency is now
#      inexpressible rather than detected.
#   3. RUNGS ARE CUMULATIVE. A seam claiming R4 must also claim R1..R3.
#   4. EVERY CLAIMED RUNG HAS A RESOLVABLE, NON-VACUOUS ARTIFACT.
#   5. NOTHING HERE ESTABLISHES THAT DEBT SHRANK. That is a DIFF property and it is unknowable from
#      inside one commit, so this gate does not claim it. The `unmet:` list in the manifest IS the
#      debt; `git diff eng/governance/rigor-ladder.yaml` is the signed, reviewable act. A reviewer
#      asking "is this seam new, or is this admitted debt on an old one?" answers it with:
#          git log --diff-filter=A -- <the seam's ARCHITECTURE.md>
#      A separate baseline file was tried and deleted: it held exactly the `unmet` rows and nothing
#      else, so the only thing it could catch was a transcription error between two files we write
#      ourselves — which is where the `floor` field was a day earlier.
#
# WHY A CLAIMED RUNG WITH A PRESENT-BUT-EMPTY ARTIFACT FAILS RATHER THAN PASSES:
#   A .tla file with no INVARIANT, or a property suite with no generated input, is indistinguishable
#   from a discharged rung by file existence alone. Existence is not the property; the property is
#   that something can FAIL. An R4 model must name an invariant AND a scope statement, because a
#   proof is valid only inside what it modeled, and a model that does not say what it abstracted
#   away is the Ariane 5 shape: correct against a specification written for a different system.
#
# WHY AN UNPARSEABLE MANIFEST REFUSES RATHER THAN PASSES:
#   Reporting "no seams found, all clear" for a manifest this gate could not read is precisely the
#   false-safety the whole ladder exists to prevent. REFUSE, don't guess.
#
# Usage:
#   rigor-ladder-gate.sh              — run the real gate against this repo
#   rigor-ladder-gate.sh --self-test  — prove the gate is non-vacuous (must run FIRST in CI)
#   rigor-ladder-gate.sh --for-files <path>...  — ADVISORY: what do these files' seams owe?
#                                       (always exit 0 — a lookup, never a gate)
#
# Exit codes: 0 PASS · 1 FAIL · 2 REFUSE (blind: manifest unreadable, empty, or no ARCHITECTURE.md)
#             3 self-test harness failure

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

run_gate() {
	local root="$1"
	local manifest="${2:-$root/eng/governance/rigor-ladder.yaml}"
	python3 - "$root" "$manifest" <<'PYEOF'
import os, sys, glob, re

root, manifest_path = sys.argv[1], sys.argv[2]
FAIL = []

try:
    import yaml
except ImportError:
    print("REFUSE: PyYAML is not importable, so the manifest cannot be read. This gate reports")
    print("        nothing rather than reporting a tree it could not inspect as clean.")
    sys.exit(2)

if not os.path.isfile(manifest_path):
    print("REFUSE: no manifest at %s -- cannot audit what does not exist." % manifest_path)
    sys.exit(2)
try:
    doc = yaml.safe_load(open(manifest_path, encoding="utf-8")) or {}
except Exception as e:
    print("REFUSE: manifest did not parse: %s" % e)
    sys.exit(2)

seams = doc.get("seams") or []
if not seams:
    print("REFUSE: manifest parsed but declares zero seams. An empty ladder is indistinguishable")
    print("        from a ladder this gate failed to read.")
    sys.exit(2)

arch_on_disk = set()
for p in glob.glob(os.path.join(root, "src", "**", "ARCHITECTURE.md"), recursive=True):
    arch_on_disk.add(os.path.relpath(p, root).replace(os.sep, "/"))
if not arch_on_disk:
    print("REFUSE: no src/**/ARCHITECTURE.md found. Either the tree is wrong or the glob is.")
    sys.exit(2)

FLOOR_FOR = {"catastrophic": 4, "high": 3, "moderate": 2, "low": 1}

def rnum(r):
    m = re.fullmatch(r"R([1-5])", str(r).strip())
    return int(m.group(1)) if m else None

declared_arch = set()
for s in seams:
    name = s.get("name") or "<unnamed>"
    amd = (s.get("architecture_md") or "").strip()
    br = (s.get("blast_radius") or "").strip()
    metn = {rnum(r) for r in (s.get("met") or []) if rnum(r)}
    unmetn = {rnum(r) for r in (s.get("unmet") or []) if rnum(r)}

    if not amd:
        FAIL.append("%s: no architecture_md declared." % name)
        continue
    declared_arch.add(amd)
    if amd not in arch_on_disk:
        FAIL.append("%s: architecture_md '%s' does not exist. An entry cannot outlive its seam." % (name, amd))
        continue

    if br not in FLOOR_FOR:
        FAIL.append("%s: blast_radius '%s' is not one of %s." % (name, br, sorted(FLOOR_FOR)))
        continue
    # The floor is DERIVED, never declared. A declared floor is a transcription of this map, and a
    # check comparing the two can only find a typo -- so the field was deleted and the inconsistency
    # is now inexpressible rather than detected.
    floor = FLOOR_FOR[br]

    both = metn & unmetn
    if both:
        FAIL.append("%s: rung(s) %s appear in BOTH met and unmet." % (name, sorted("R%d" % r for r in both)))

    if metn:
        for lower in range(1, max(metn)):
            if lower not in metn:
                FAIL.append("%s: claims R%d but R%d is not met. Rungs are cumulative." % (name, max(metn), lower))

    for r in range(1, floor + 1):
        if r not in metn and r not in unmetn:
            FAIL.append("%s: floor is R%d but R%d appears in neither met nor unmet." % (name, floor, r))

    for r in sorted(metn):
        if r == 1:
            continue  # R1 is the compiler. TreatWarningsAsErrors is asserted elsewhere.
        if r == 2:
            body = open(os.path.join(root, amd), encoding="utf-8", errors="replace").read()
            if not re.search(r"^##\s+.*[Gg]uarantee", body, re.M):
                FAIL.append("%s: claims R2 but %s has no '## ...Guarantee...' section." % (name, amd))
            if not re.search(r"^##\s+.*Evidence", body, re.M):
                FAIL.append("%s: claims R2 but %s has no '## ...Evidence...' section -- a contract "
                            "with no RED-detecting arm named is a description, not a contract." % (name, amd))
        if r == 3:
            key = name.replace("-", "").lower()
            hits = [p for p in glob.glob(os.path.join(root, "tests", "property", "**", "*.cs"), recursive=True)
                    if key in p.lower().replace(os.sep, "/").replace("-", "")]
            if not hits:
                FAIL.append("%s: claims R3 but no property suite under tests/property/ resolves to it." % name)
            elif not any(re.search(r"Gen\.|Check\.|Prop\.|\[Property\]|Arb\.|new Random\(|[Ss]eed",
                                   open(h, encoding="utf-8", errors="replace").read()) for h in hits):
                # PROXY, and named as one. The REQUIREMENT is "inputs are generated rather than
                # enumerated, and a failure prints something that reproduces it." No regex tests that.
                # The first version matched only FsCheck/CsCheck API names, so a hand-rolled
                # Random-plus-printed-seed suite FAILED -- which meant this gate had silently decided
                # the dependency question that nobody had ruled. Verified with a control: the
                # hand-rolled form did not match and the vendor form did.
                FAIL.append("%s: claims R3 but its suite shows no sign of generated input (no "
                            "generator API, no Random, no seed) -- a fixed-input test filed under "
                            "tests/property/ is an example test." % name)
        if r >= 4:
            models = (glob.glob(os.path.join(root, "spec", "tla", "%s.tla" % name))
                      + glob.glob(os.path.join(root, "spec", "alloy", "%s.als" % name)))
            if not models:
                FAIL.append("%s: claims R%d but neither spec/tla/%s.tla nor spec/alloy/%s.als exists."
                            % (name, r, name, name))
            for m in models:
                txt = open(m, encoding="utf-8", errors="replace").read()
                rel = os.path.relpath(m, root).replace(os.sep, "/")
                if not re.search(r"\bINVARIANT\b|\bassert\b|\bcheck\b", txt, re.I):
                    FAIL.append("%s: %s names no invariant to check. A model that asserts nothing "
                                "cannot fail." % (name, rel))
                if "SCOPE:" not in txt:
                    FAIL.append("%s: %s has no 'SCOPE:' statement. A proof is valid only inside what "
                                "it modeled; a model that does not say what it abstracted away is the "
                                "Ariane 5 shape." % (name, rel))

for o in sorted(arch_on_disk - declared_arch):
    FAIL.append("%s declares a guarantee but has NO entry in the rigor ladder. Declaring a "
                "guarantee obliges a rung." % o)

unmet_total = sum(len(s.get("unmet") or []) for s in seams)
print("rigor-ladder: %d seams declared, %d ARCHITECTURE.md on disk, %d rung(s) declared UNVERIFIED."
      % (len(seams), len(arch_on_disk), unmet_total))
if FAIL:
    print("\nFAIL: %d finding(s)\n" % len(FAIL))
    for f in FAIL:
        print("  - %s" % f)
    sys.exit(1)
print("PASS: every declared seam's claimed rungs resolve to a non-vacuous artifact, and no guarantee "
      "is declared without a rung.")
print("      NOT established here: that debt SHRANK. That is a diff property, unknowable from inside")
print("      one commit. The manifest's unmet list is the debt; `git diff` on it is the reviewable")
print("      act, and `git log --diff-filter=A -- <ARCHITECTURE.md>` says whether a seam is new.")
sys.exit(0)
PYEOF
}

# ---------------------------------------------------------------------------
# --for-files: what does THIS bead owe? Answers the question an agent actually has while working,
# which the manifest cannot: "the files I am about to change — do they sit on a laddered seam, and
# which rungs does that seam still owe?" Read-only, no exit-code opinion (always 0): this is an
# ADVISORY LOOKUP, not a gate. Blocking here would be a commit-path gate in a costume.
# ---------------------------------------------------------------------------
for_files() {
	local root="$REPO_ROOT"
	python3 - "$root" "$root/eng/governance/rigor-ladder.yaml" "$@" <<'PYFF'
import os, sys

root, manifest = sys.argv[1], sys.argv[2]
paths = [p.replace("\\", "/").lstrip("./") for p in sys.argv[3:]]
if not paths:
    print("usage: rigor-ladder-gate.sh --for-files <path> [path...]")
    sys.exit(0)
try:
    import yaml
    doc = yaml.safe_load(open(manifest, encoding="utf-8")) or {}
except Exception as e:
    print("rigor-ladder: cannot read the manifest (%s). No obligation could be computed -- that is" % e)
    print("              NOT the same as no obligation. Check the seam by hand.")
    sys.exit(0)

seams = doc.get("seams") or []
# A seam owns the DIRECTORY its ARCHITECTURE.md sits in, and everything beneath it.
owners = []
for s in seams:
    amd = (s.get("architecture_md") or "").strip()
    if amd:
        owners.append((os.path.dirname(amd) + "/", s))

hits, clean = {}, []
for p in paths:
    matched = None
    for prefix, s in owners:
        if p.startswith(prefix):
            if matched is None or len(prefix) > len(matched[0]):
                matched = (prefix, s)
    if matched:
        hits.setdefault(matched[1]["name"], (matched[1], []))[1].append(p)
    else:
        clean.append(p)

if not hits:
    print("rigor-ladder: none of the %d path(s) sit on a laddered seam. R1/R2 still apply as usual." % len(paths))
    sys.exit(0)

print("rigor-ladder: %d of %d path(s) sit on a LADDERED SEAM."
      % (len(paths) - len(clean), len(paths)))
print("")
for name, (s, ps) in sorted(hits.items()):
    # DERIVED here too, for the same reason the field was deleted from the manifest: reading a
    # `floor` key that no longer exists printed "None" to anyone running --for-files. The map is
    # restated rather than shared because this is a SEPARATE python heredoc with its own scope --
    # referencing the validator's FLOOR_FOR here raises NameError at the point of use, which is a
    # traceback in an advisory command an agent is told to run.
    _FLOOR_FOR = {"catastrophic": "R4", "high": "R3", "moderate": "R2", "low": "R1"}
    _br = s.get("blast_radius")
    floor = _FLOOR_FOR.get(_br, "?")
    unmet = s.get("unmet") or []
    print("  SEAM %s  blast_radius=%s  floor=%s" % (name, _br, floor))
    print("    why: %s" % (s.get("why") or "(unstated)"))
    for p in ps:
        print("    touches: %s" % p)
    if unmet:
        print("    STILL OWED: %s  <-- UNVERIFIED at these rungs today. Do not describe this seam" % ", ".join(unmet))
        print("                as covered, verified, or proven; the suite that passes is sampling-class.")
    else:
        print("    all rungs to the floor are met. Changing this seam may INVALIDATE them --")
        print("    the model's SCOPE statement and the property suite both bind to behaviour you are changing.")
    print("")
print("R3 is TestsDeveloper's. R4 is SoftwareArchitect's, at DESIGN time (a model written after the")
print("implementation restates it instead of checking it). The rung assignment is SoftwareArchitect's,")
print("ruled at PLAN and written into the bead's acceptance criteria by ProductManager.")
PYFF
}

# ---------------------------------------------------------------------------
# Self-test. Proves the gate FAILS on each defect it claims to detect, and REFUSES when blind.
# ---------------------------------------------------------------------------
self_test() {
	local tmp pass=0 fail=0
	tmp="$(mktemp -d)"
	trap 'rm -rf "$tmp"' RETURN

	arm() { # arm <name> <expected-exit> <root>
		local name="$1" want="$2" r="$3" got
		run_gate "$r" >/dev/null 2>&1
		got=$?
		if [[ "$got" == "$want" ]]; then
			echo "  self-test ok:   $name (exit $got)"
			pass=$((pass + 1))
		else
			echo "  self-test FAIL: $name -- expected exit $want, got $got" >&2
			fail=$((fail + 1))
		fi
	}

	mk() { # mk <root> <blast> <met> <unmet>   (no floor -- it is derived from <blast>)
		local r="$1"
		mkdir -p "$r/src/Pkg" "$r/eng/governance" "$r/eng/ci"
		printf '## Guarantee\nat-least-once, duplicate window bounded by F.\n## Evidence\nOutboxShould\n' \
			> "$r/src/Pkg/ARCHITECTURE.md"
		{
			echo 'version: "1.0"'
			echo 'seams:'
			echo '  - name: demo'
			echo '    architecture_md: src/Pkg/ARCHITECTURE.md'
			echo "    blast_radius: $2"
			echo "    met: $3"
			echo "    unmet: $4"
		} > "$r/eng/governance/rigor-ladder.yaml"
		printf 'demo:R3\ndemo:R4\n' > "$r/eng/ci/rigor-ladder-baseline.txt"
	}

	pbt() { # pbt <root> — a real property suite with a generator
		mkdir -p "$1/tests/property/demo"
		printf 'public class DemoProperties { public void P() { Gen.Int.Sample(); } }\n' \
			> "$1/tests/property/demo/DemoProperties.cs"
	}

	# SAFETY 1 — the real repository must satisfy its own gate.
	arm "the real repository PASSES" 0 "$REPO_ROOT"

	# LIVENESS 2 — a healthy fixture with baselined debt PASSES.
	mk "$tmp/ok" catastrophic "[R1, R2]" "[R3, R4]"
	arm "healthy fixture with baselined debt PASSES" 0 "$tmp/ok"

	# FAIL 3 — an entry naming an ARCHITECTURE.md that does not exist.
	mk "$tmp/ghost" catastrophic "[R1, R2]" "[R3, R4]"
	rm "$tmp/ghost/src/Pkg/ARCHITECTURE.md"
	mkdir -p "$tmp/ghost/src/Other"
	printf '## Guarantee\nx\n## Evidence\nT\n' > "$tmp/ghost/src/Other/ARCHITECTURE.md"
	arm "an entry naming a missing ARCHITECTURE.md FAILS" 1 "$tmp/ghost"

	# FAIL 4 — a new guarantee-critical package with no ladder entry.
	mk "$tmp/orphan" catastrophic "[R1, R2]" "[R3, R4]"
	mkdir -p "$tmp/orphan/src/NewPkg"
	printf '## Guarantee\nx\n## Evidence\nT\n' > "$tmp/orphan/src/NewPkg/ARCHITECTURE.md"
	arm "an undeclared ARCHITECTURE.md FAILS (coverage cannot shrink)" 1 "$tmp/orphan"

	# FAIL 5 — the floor is DERIVED from blast_radius, so a catastrophic seam owes R1..R4 whatever
	# anyone writes. A seam accounting for only R1-R2 must FAIL because R3/R4 are unaccounted for --
	# this is the arm that used to compare a declared floor against the map, which could only catch a
	# typo. The invariant is now structural; this proves the derivation actually drives the check.
	mk "$tmp/derived" catastrophic "[R1, R2]" "[]"
	arm "a catastrophic seam leaving R3/R4 unaccounted FAILS (floor is derived)" 1 "$tmp/derived"

	# FAIL 6 — claims R3 with no property suite at all.
	mk "$tmp/nopbt" catastrophic "[R1, R2, R3]" "[R4]"
	arm "claiming R3 with no property suite FAILS" 1 "$tmp/nopbt"

	# FAIL 7 — a property suite with no generator is an example test in a property folder.
	mk "$tmp/fakepbt" catastrophic "[R1, R2, R3]" "[R4]"
	mkdir -p "$tmp/fakepbt/tests/property/demo"
	printf 'public class DemoProperties { public void X() { Assert.True(true); } }\n' \
		> "$tmp/fakepbt/tests/property/demo/DemoProperties.cs"
	arm "a property suite with no generator FAILS" 1 "$tmp/fakepbt"

	# FAIL 8 — claims R4 with no model at all.
	mk "$tmp/nomodel" catastrophic "[R1, R2, R3, R4]" "[]"
	pbt "$tmp/nomodel"
	arm "claiming R4 with no model FAILS" 1 "$tmp/nomodel"

	# FAIL 9 — a model that asserts nothing.
	mk "$tmp/emptymodel" catastrophic "[R1, R2, R3, R4]" "[]"
	pbt "$tmp/emptymodel"
	mkdir -p "$tmp/emptymodel/spec/tla"
	printf -- '---- MODULE demo ----\nSCOPE: one publisher, two consumers.\n====\n' \
		> "$tmp/emptymodel/spec/tla/demo.tla"
	arm "a model naming no invariant FAILS" 1 "$tmp/emptymodel"

	# FAIL 10 — a model with an invariant but no SCOPE statement.
	mk "$tmp/noscope" catastrophic "[R1, R2, R3, R4]" "[]"
	pbt "$tmp/noscope"
	mkdir -p "$tmp/noscope/spec/tla"
	printf -- '---- MODULE demo ----\nINVARIANT NoDoubleSend\n====\n' > "$tmp/noscope/spec/tla/demo.tla"
	arm "a model with no SCOPE statement FAILS" 1 "$tmp/noscope"

	# LIVENESS 11 — a fully discharged R4 seam PASSES. The gate is not merely always-red.
	mk "$tmp/full" catastrophic "[R1, R2, R3, R4]" "[]"
	pbt "$tmp/full"
	mkdir -p "$tmp/full/spec/tla"
	printf -- '---- MODULE demo ----\nSCOPE: one publisher, two consumers; network reordering modeled, clock skew abstracted away.\nINVARIANT NoDoubleSend\n====\n' \
		> "$tmp/full/spec/tla/demo.tla"
	arm "a fully discharged R4 seam PASSES" 0 "$tmp/full"

	# FAIL 13 — cumulative violation: R4 met while R3 is not.
	mk "$tmp/gap" catastrophic "[R1, R2, R4]" "[R3]"
	arm "claiming R4 while R3 is unmet FAILS (rungs are cumulative)" 1 "$tmp/gap"

	# FAIL 14 — the same rung in both met and unmet.
	mk "$tmp/both" catastrophic "[R1, R2, R3]" "[R3, R4]"
	arm "a rung in both met and unmet FAILS" 1 "$tmp/both"

	# REFUSE 15 — a manifest declaring zero seams.
	mkdir -p "$tmp/empty/src/Pkg" "$tmp/empty/eng/governance"
	printf '## Guarantee\nx\n## Evidence\nT\n' > "$tmp/empty/src/Pkg/ARCHITECTURE.md"
	printf 'version: "1.0"\nseams: []\n' > "$tmp/empty/eng/governance/rigor-ladder.yaml"
	arm "a manifest with zero seams REFUSES" 2 "$tmp/empty"

	# REFUSE 16 — no manifest at all.
	mkdir -p "$tmp/nomanifest/src/Pkg"
	printf '## Guarantee\nx\n## Evidence\nT\n' > "$tmp/nomanifest/src/Pkg/ARCHITECTURE.md"
	arm "a missing manifest REFUSES" 2 "$tmp/nomanifest"

	# REFUSE 17 — a tree with no ARCHITECTURE.md at all.
	mk "$tmp/noarch" catastrophic "[R1, R2]" "[R3, R4]"
	rm -rf "$tmp/noarch/src"
	arm "a tree with no ARCHITECTURE.md REFUSES" 2 "$tmp/noarch"

	echo
	echo "self-test: $pass passed, $fail failed"
	if ((fail > 0)); then return 1; fi
	echo "NON-VACUITY ESTABLISHED: the gate FAILS on a missing seam doc, an undeclared guarantee, a"
	echo "                        catastrophic seam leaving a rung unaccounted for, a claimed rung"
	echo "                        with no artifact, a property suite with no generated input, a"
	echo "                        model with no invariant or no"
	echo "                        scope, and a cumulative gap; and REFUSES rather"
	echo "                        than passing when it cannot see the manifest or the tree."
	return 0
}

case "${1:-}" in
--self-test)
	self_test
	exit $?
	;;
--for-files)
	shift
	for_files "$@"
	exit 0
	;;
"")
	run_gate "$REPO_ROOT"
	exit $?
	;;
*)
	echo "unknown argument: $1" >&2
	exit 3
	;;
esac
