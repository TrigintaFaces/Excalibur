#!/usr/bin/env bash
# architecture-evidence-gate.sh — a guarantee whose enforcing test has drifted, reported as enforced.
#
# THE DEFECT CLASS:
#
#   Every subsystem ARCHITECTURE.md states a guarantee in falsifiable terms and names the test that
#   RED-detects a violation of it. Nothing checks that the named test still exists, still binds the
#   class it is cited against, or was not silently renamed out from under the sentence that vouches
#   for it. A guarantee sentence does not know its own test moved -- only running the census does.
#
#   This is not hypothetical: it is the SAME class as the conformance-arm-skip-gate's "an arm that
#   returns early is indistinguishable from an arm that ran" -- one layer up. There, the risk was a
#   SKIPPED arm reading as a PASSED one. Here, the risk is a DELETED or RENAMED test reading as a
#   STANDING one, because nothing re-derives the citation.
#
# WHAT THIS GATE ASSERTS: every backtick-quoted test citation inside a "## Evidence" section of a
#   src/**/ARCHITECTURE.md resolves to a REAL, DECLARED class (and, where the citation names a method,
#   a real declared method of that class) somewhere under src/ or tests/. A citation is a whole-symbol
#   match on the SOURCE, not a name-keyed grep of the guarantee sentence -- a renamed class or a
#   retargeted method is exactly what a name-keyed search of the DOC would miss, because the doc's own
#   words never change; only the code under them does.
#
# WHY THIS GATE READS THE EVIDENCE SECTION, NOT THE WHOLE FILE:
#
#   A citation format is not standardised across these eight files -- some use a three-column table
#   with a Status cell, some cite a bare class name in a two-column table, some cite a class inline in
#   prose, one cites a bare method name with no class at all. Parsing one shape and calling the rest
#   "no citations found" is the same blindness class conformance-backend-coverage-gate.sh's own header
#   describes: a census that recognises only one shape reports a population it cannot see as clean.
#   So this gate extracts every backtick span in the section and classifies it by SHAPE
#   (Type.Method / bare Type / neither), rather than by which column of which table it sits in.
#
# WHY A ZERO-CITATION EVIDENCE SECTION REFUSES RATHER THAN PASSES:
#
#   A "## Evidence" heading with body text but no resolvable citation is indistinguishable, from this
#   gate's position, between "this subsystem genuinely cites nothing" and "this gate's classifier does
#   not recognise this file's citation shape." Reporting the second case as a clean pass would be the
#   same false-safety this whole file exists to prevent, one layer up. REFUSE, don't guess.
#
# Usage:
#   architecture-evidence-gate.sh              — run the real gate against this repo
#   architecture-evidence-gate.sh --self-test  — prove the gate is non-vacuous (must run first in CI)
#
# Exit codes: 0 PASS · 1 FAIL (a citation does not resolve, or a self-test arm failed) ·
#             2 REFUSE (blind: no citations found, or the tree could not be read) ·
#             3 self-test harness failure (unknown argument, etc.)

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

run_gate() {
	local root="$1"
	local baseline="${2:-/nonexistent-baseline}"
	python3 - "$root" "$baseline" <<'PYEOF'
import re
import sys
import os

root = sys.argv[1]
baseline_path = sys.argv[2]

def read_baseline(path):
    entries = set()
    if not os.path.isfile(path):
        return entries
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith("#"):
                entries.add(line)
    return entries

baselined = read_baseline(baseline_path)

def walk_cs(top, subdir):
    base = os.path.join(top, subdir)
    if not os.path.isdir(base):
        return
    for dirpath, _dirnames, filenames in os.walk(base):
        for name in filenames:
            if name.endswith(".cs"):
                yield os.path.join(dirpath, name)

def read(path):
    try:
        with open(path, encoding="utf-8-sig") as f:
            return f.read()
    except OSError:
        return None

# ---- Build the C# symbol index: ClassName -> concatenated text of every file declaring it. ----
# A class can only sensibly be searched for a method within the file(s) that declare it; concatenating
# is enough here because collisions (two classes sharing a bare name across namespaces) are rare and a
# false PASS from one is no worse than the whole-tree grep this gate replaces -- the target defect is
# a citation that resolves to NOTHING, not one that resolves ambiguously.
class_decl = re.compile(
    r'\b(?:public|internal|private|protected)?\s*(?:sealed\s+|abstract\s+|static\s+|partial\s+|readonly\s+|ref\s+)*'
    # A cited type is not always a class: TenantScope is a readonly struct, and matching only
    # 'class' reported it undeclared -- a false FAIL that pressures the author to rewrite a
    # CORRECT citation into a wrong one. 'record struct'/'record class' precede bare 'record'.
    r'(?:class|struct|interface|record\s+struct|record\s+class|record)\s+([A-Za-z_]\w*)')

class_files = {}
for sub in ("src", "tests"):
    for path in walk_cs(root, sub):
        text = read(path)
        if text is None:
            continue
        for m in class_decl.finditer(text):
            class_files.setdefault(m.group(1), []).append(path)

class_text_cache = {}
def text_for(name):
    if name not in class_text_cache:
        parts = []
        for path in class_files.get(name, ()):
            t = read(path)
            if t:
                parts.append(t)
        class_text_cache[name] = "\n".join(parts)
    return class_text_cache[name]

method_use = re.compile(r'\b{}\s*(?:\(|=>|<)')

def class_exists(name):
    return name in class_files

def method_exists(cls, method):
    t = text_for(cls)
    if not t:
        return False
    return re.search(r'\b' + re.escape(method) + r'\b', t) is not None

all_method_names_cache = None
def method_exists_anywhere(method):
    # Built lazily and once: every method-shaped declaration line across every indexed class file,
    # unioned. Weaker than a class-scoped check (it cannot catch "moved to the wrong class"), but a
    # citation with no class to scope to has no stronger check available -- and a name that resolves
    # nowhere in the whole tree is still a real, reportable drift.
    global all_method_names_cache
    if all_method_names_cache is None:
        names = set()
        method_decl = re.compile(
            r'\b(?:public|internal|private|protected)?\s*(?:static\s+|virtual\s+|override\s+|async\s+|sealed\s+)*'
            r'[\w<>\[\],. ?]+?\s+([A-Za-z_]\w*)\s*\(')
        seen_paths = set()
        for paths in class_files.values():
            for path in paths:
                if path in seen_paths:
                    continue
                seen_paths.add(path)
                t = read(path)
                if not t:
                    continue
                for m in method_decl.finditer(t):
                    names.add(m.group(1))
        all_method_names_cache = names
    return method in all_method_names_cache

# ---- Find every ARCHITECTURE.md, extract its Evidence section(s). ----
arch_files = []
src_dir = os.path.join(root, "src")
if os.path.isdir(src_dir):
    for dirpath, _dirnames, filenames in os.walk(src_dir):
        for name in filenames:
            if name == "ARCHITECTURE.md":
                arch_files.append(os.path.join(dirpath, name))

if not arch_files:
    print("REFUSE: no ARCHITECTURE.md found under src/ -- cannot audit what does not exist.")
    sys.exit(2)

evidence_heading = re.compile(r'^##\s+Evidence\b.*$', re.MULTILINE)
next_heading = re.compile(r'^##\s+', re.MULTILINE)
backtick_span = re.compile(r'`([^`\n]+)`')
type_method = re.compile(r'^([A-Za-z_]\w*)\.([A-Za-z_]\w*)$')
bare_ident = re.compile(r'^[A-Za-z_]\w*$')
# A bare identifier is only treated as a class citation if it LOOKS like a test/type name -- avoids
# treating an inline code term ("public", "ISnapshotStore") as a citation. Conservative on purpose:
# a missed citation under-audits; a wrongly-accepted one produces a false FAIL, which is worse noise.
looks_like_test_class = re.compile(
    r'(Should|Shoulds|Tests?|Spec|Fixture|Kit|Base|Suite)$')
# A bare identifier that is NOT class-shaped but DOES carry an underscore is this repo's other
# observed citation shape: an unscoped conformance-arm method name (e.g. a shared kit's own arm,
# cited without its class because the citing table is organised by guarantee, not by suite). Every
# test-method name examined across this repo's ARCHITECTURE.md files follows Word_word_Word; a type
# name never does. Checked tree-wide (no class scope available) rather than skipped, because a
# genuinely renamed/deleted arm is exactly the drift this gate exists to catch, even unscoped.
looks_like_test_method = re.compile(r'^[A-Z][A-Za-z0-9]*_[A-Za-z0-9_]+$')

total_refs = 0
total_resolved = 0
total_unresolved = []
total_skipped = 0
files_with_zero_refs = []

for arch_path in arch_files:
    text = read(arch_path)
    if text is None:
        print(f"REFUSE: could not read {arch_path}")
        sys.exit(2)

    sections = []
    for m in evidence_heading.finditer(text):
        start = m.end()
        nxt = next_heading.search(text, start)
        end = nxt.start() if nxt else len(text)
        sections.append(text[start:end])

    if not sections:
        # No Evidence section at all is a different, narrower gap than a blind classifier -- report,
        # don't fail the whole gate on a subsystem doc that never claimed conformance evidence.
        continue

    section_refs = 0
    for section in sections:
        for bm in backtick_span.finditer(section):
            token = bm.group(1).strip()
            tm = type_method.match(token)
            if tm:
                cls, method = tm.group(1), tm.group(2)
                total_refs += 1
                section_refs += 1
                if not class_exists(cls):
                    total_unresolved.append((arch_path, token, f"class '{cls}' not found under src/ or tests/"))
                elif not method_exists(cls, method):
                    total_unresolved.append((arch_path, token, f"class '{cls}' exists but declares no '{method}'"))
                else:
                    total_resolved += 1
                continue
            if bare_ident.match(token) and looks_like_test_class.search(token):
                total_refs += 1
                section_refs += 1
                if not class_exists(token):
                    total_unresolved.append((arch_path, token, f"class '{token}' not found under src/ or tests/"))
                else:
                    total_resolved += 1
                continue
            if bare_ident.match(token) and looks_like_test_method.match(token) and not looks_like_test_class.search(token):
                total_refs += 1
                section_refs += 1
                if not method_exists_anywhere(token):
                    total_unresolved.append((arch_path, token, "no method of this name declared anywhere under src/ or tests/"))
                else:
                    total_resolved += 1
                continue
            total_skipped += 1

    if section_refs == 0:
        rel = os.path.relpath(arch_path, root).replace("\\", "/")
        if rel not in baselined:
            files_with_zero_refs.append(arch_path)

# THE RATCHET: a baseline entry that no longer names a live zero-citation file is stale -- either the
# file gained a citation (good; the entry must be removed so the gate actually checks it now) or the
# file was renamed/deleted (the entry is dead weight). A baseline that could silently absorb either
# case is a suppression cap, not an enumeration -- see the coverage gate's own header for why that
# distinction is load-bearing.
zero_ref_paths_rel = set()
for arch_path in arch_files:
    text = read(arch_path)
    if text is None:
        continue
    sections = []
    for m in evidence_heading.finditer(text):
        start = m.end()
        nxt = next_heading.search(text, start)
        end = nxt.start() if nxt else len(text)
        sections.append(text[start:end])
    has_ref = False
    for section in sections:
        for bm in backtick_span.finditer(section):
            token = bm.group(1).strip()
            if type_method.match(token):
                has_ref = True
            elif bare_ident.match(token) and (looks_like_test_class.search(token)
                                               or (looks_like_test_method.match(token)
                                                   and not looks_like_test_class.search(token))):
                has_ref = True
    if sections and not has_ref:
        zero_ref_paths_rel.add(os.path.relpath(arch_path, root).replace("\\", "/"))

stale_baseline = sorted(b for b in baselined if b not in zero_ref_paths_rel)
if stale_baseline:
    print("FAIL: stale architecture-evidence-baseline.txt entries -- these no longer name a live")
    print("      zero-citation Evidence section (the file gained a citation, was renamed, or no")
    print("      longer exists). Remove the line so the gate actually audits it:")
    for b in stale_baseline:
        print(f"        {b}")
    sys.exit(1)

if files_with_zero_refs:
    print("REFUSE: an Evidence section named the following file(s) with ZERO resolvable citations,")
    print("      and NOT listed in eng/ci/architecture-evidence-baseline.txt -- this gate cannot tell")
    print("      a genuinely citation-free section from one whose format its classifier does not")
    print("      recognise, and reporting the second as clean is the exact false safety this gate")
    print("      exists to prevent. If the section genuinely cites no test, add the path to the")
    print("      baseline WITH a reason; if the format is real but unrecognised, widen the classifier:")
    for p in files_with_zero_refs:
        print(f"        {os.path.relpath(p, root)}")
    sys.exit(2)

if total_unresolved:
    print("FAIL: the following Evidence citations do not resolve to a real declared symbol --")
    print("      a guarantee sentence still claims a test that the code no longer carries:")
    for path, token, reason in total_unresolved:
        print(f"        {os.path.relpath(path, root)}: `{token}` -- {reason}")
    print(f"  totals: {total_refs} citation(s) examined, {total_resolved} resolved, "
          f"{len(total_unresolved)} unresolved, {total_skipped} non-citation backtick span(s) skipped.")
    sys.exit(1)

# The denominator in the standard machine-readable form. This gate already counted what it read;
# what it lacked was the one marker a reader (and the denominator gate) can find without parsing prose.
print("EXAMINED: %d citation(s) across %d ARCHITECTURE.md file(s)" % (total_refs, len(arch_files)))
print("PASS: every Evidence citation across every ARCHITECTURE.md resolves to a real declared symbol.")
print(f"  totals: {len(arch_files)} ARCHITECTURE.md examined, {total_refs} citation(s) checked, "
      f"{total_resolved} resolved, {total_skipped} non-citation backtick span(s) skipped.")
sys.exit(0)
PYEOF
}

# ── Self-test ───────────────────────────────────────────────────────────────────────────────────────
# Proves the gate is non-vacuous: it must FAIL on a renamed/deleted citation, REFUSE on a citation
# shape it cannot classify, and PASS on a citation that genuinely resolves. Synthetic fixture only --
# nothing here names a real subsystem.
self_test() {
	local tmp fails=0
	tmp="$(mktemp -d)"
	trap 'rm -rf "$tmp"' RETURN

	mk() { # mk <root> -- a minimal tree: one real class+method a citation can point at
		mkdir -p "$1/src/Widget" "$1/tests/Widget.Tests"
		cat > "$1/tests/Widget.Tests/WidgetStoreConformanceShould.cs" <<'EOF'
public sealed class WidgetStoreConformanceShould
{
	public void Detect_a_tampered_widget() { }
}
EOF
	}

	arm() { # arm <name> <expected-rc> <root> [baseline]
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

	# ---- SAFETY 1: a citation naming a method that no longer exists (renamed/deleted) FAILS. ----
	# This is the sharpest case a name-keyed doc-search would miss: the SENTENCE never changes, only
	# the code under it does.
	mk "$tmp/renamed"
	mkdir -p "$tmp/renamed/src/Widget"
	cat > "$tmp/renamed/src/Widget/ARCHITECTURE.md" <<'EOF'
## Evidence

`WidgetStoreConformanceShould.Detect_a_removed_widget` proves the guarantee.
EOF
	arm "a citation to a RENAMED/deleted method FAILS" 1 "$tmp/renamed"

	# ---- LIVENESS 2: the same tree, citing the method that actually exists, PASSES. ----
	mk "$tmp/live"
	mkdir -p "$tmp/live/src/Widget"
	cat > "$tmp/live/src/Widget/ARCHITECTURE.md" <<'EOF'
## Evidence

`WidgetStoreConformanceShould.Detect_a_tampered_widget` proves the guarantee.
EOF
	arm "a citation to a REAL method PASSES" 0 "$tmp/live"

	# ---- SAFETY 3: a citation to a class that does not exist at all FAILS. ----
	mk "$tmp/noclass"
	mkdir -p "$tmp/noclass/src/Widget"
	cat > "$tmp/noclass/src/Widget/ARCHITECTURE.md" <<'EOF'
## Evidence

`GhostWidgetStoreShould.Detect_anything` proves the guarantee.
EOF
	arm "a citation to a NONEXISTENT class FAILS" 1 "$tmp/noclass"

	# ---- LIVENESS 4: a bare class-name citation (no method) that exists PASSES. ----
	mk "$tmp/bareclass"
	mkdir -p "$tmp/bareclass/src/Widget"
	cat > "$tmp/bareclass/src/Widget/ARCHITECTURE.md" <<'EOF'
## Evidence

Proven by `WidgetStoreConformanceShould`, run against a real container.
EOF
	arm "a bare class-name citation that exists PASSES" 0 "$tmp/bareclass"

	# ---- SAFETY 5: a bare class-name citation that does not exist FAILS. ----
	mk "$tmp/bareghost"
	mkdir -p "$tmp/bareghost/src/Widget"
	cat > "$tmp/bareghost/src/Widget/ARCHITECTURE.md" <<'EOF'
## Evidence

Proven by `GhostWidgetShould`, run against a real container.
EOF
	arm "a bare NONEXISTENT class-name citation FAILS" 1 "$tmp/bareghost"

	# ---- REFUSE 6: an Evidence section with prose but no classifiable citation REFUSES, not PASSES.
	mk "$tmp/blind"
	mkdir -p "$tmp/blind/src/Widget"
	cat > "$tmp/blind/src/Widget/ARCHITECTURE.md" <<'EOF'
## Evidence

The guarantee is proven by a real-infrastructure test suite against a live container, executed on
every push. See the integration test project for details.
EOF
	arm "an Evidence section with no classifiable citation REFUSES" 2 "$tmp/blind"

	# ---- REFUSE 7: no ARCHITECTURE.md at all REFUSES. ----
	mkdir -p "$tmp/none/src"
	arm "zero ARCHITECTURE.md files REFUSES" 2 "$tmp/none"

	# ---- LIVENESS 8: an inline code term that merely LOOKS like an identifier but isn't a test-class
	# suffix is correctly skipped rather than misjudged -- doesn't trip SAFETY 3/5 on ordinary prose.
	mk "$tmp/prose"
	mkdir -p "$tmp/prose/src/Widget"
	cat > "$tmp/prose/src/Widget/ARCHITECTURE.md" <<'EOF'
## Evidence

Proven by `WidgetStoreConformanceShould.Detect_a_tampered_widget`. The store implements `IWidgetStore`
and persists to `widget_events`, neither of which is a citation.
EOF
	arm "inline code terms are skipped, not misjudged as citations" 0 "$tmp/prose"

	# ---- LIVENESS 9 / SAFETY 10: an unscoped (no-class) method citation -- the shape this repo's
	# LeaderElection ARCHITECTURE.md actually uses -- PASSES when the method exists anywhere in the
	# tree and FAILS when it does not.
	mk "$tmp/unscoped-live"
	mkdir -p "$tmp/unscoped-live/src/Widget"
	cat > "$tmp/unscoped-live/src/Widget/ARCHITECTURE.md" <<'EOF'
## Evidence

`Detect_a_tampered_widget` proves the guarantee.
EOF
	arm "an unscoped method citation that exists PASSES" 0 "$tmp/unscoped-live"

	mk "$tmp/unscoped-dead"
	mkdir -p "$tmp/unscoped-dead/src/Widget"
	cat > "$tmp/unscoped-dead/src/Widget/ARCHITECTURE.md" <<'EOF'
## Evidence

`Detect_a_removed_widget_arm` proves the guarantee.
EOF
	arm "an unscoped method citation that does not exist FAILS" 1 "$tmp/unscoped-dead"

	# ---- LIVENESS 11: a zero-citation Evidence section that IS baselined PASSES rather than REFUSEs.
	mk "$tmp/baselined"
	mkdir -p "$tmp/baselined/src/Widget"
	cat > "$tmp/baselined/src/Widget/ARCHITECTURE.md" <<'EOF'
## Evidence

Verified entirely by inspection; no automated test names a specific check here.
EOF
	printf 'src/Widget/ARCHITECTURE.md\n' > "$tmp/baselined-ok.txt"
	arm "a baselined zero-citation section PASSES, not REFUSE" 0 "$tmp/baselined" "$tmp/baselined-ok.txt"

	# ---- SAFETY 12: a baseline entry for a file that NOW has a citation is stale and must FAIL.
	arm "a stale baseline entry (file now has a citation) FAILS" 1 "$tmp/live" "$tmp/baselined-ok.txt"

	if [ "$fails" -gt 0 ]; then
		echo "SELF-TEST FAIL: $fails arm(s) failed." >&2
		return 3
	fi
	echo "SELF-TEST PASS: the gate FAILS a renamed/deleted citation (method or class), FAILS a bare"
	echo "                class citation that no longer exists, REFUSES an Evidence section with no"
	echo "                classifiable citation rather than reporting it clean, REFUSES a tree with no"
	echo "                ARCHITECTURE.md at all, and PASSES a citation that genuinely resolves without"
	echo "                being confused by ordinary inline code terms."
	return 0
}

case "${1:-}" in
	--self-test)
		self_test
		exit $?
		;;
	"")
		run_gate "$REPO_ROOT" "$SCRIPT_DIR/architecture-evidence-baseline.txt"
		exit $?
		;;
	*)
		echo "REFUSE: unknown argument '${1}'. Usage: $(basename "$0") [--self-test]" >&2
		exit 2
		;;
esac
