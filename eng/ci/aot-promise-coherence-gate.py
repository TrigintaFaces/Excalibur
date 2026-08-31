#!/usr/bin/env python3
"""Fails when a project claims AOT compatibility that one of its own shipped dependencies denies.

WHY
    IsAotCompatible is package metadata a consumer reads BEFORE writing a line of code: it is how
    they decide whether this package belongs in a PublishAot application. The claim is made at
    selection time, and nothing downstream corrects it -- by the time they hit a warning they have
    already chosen us.

    A project cannot be AOT-compatible if an assembly it ships with is not. The referenced assembly
    lands inside the same published application and its dynamic-code paths are reachable. So "I am
    AOT-compatible and my dependency is not" is a contradiction, and the build will never surface
    it, because each project is analyzed alone.

WHAT IT ASSERTS
    For every project declaring IsAotCompatible=true: no SHIPPED ProjectReference resolves to a
    project declaring false.

WHAT IT DELIBERATELY DOES NOT DO
    It does not judge references that do not ship. An analyzer or source generator is referenced
    with ReferenceOutputAssembly="false" (usually OutputItemType="Analyzer"): it runs in our
    compiler and is never published into the consumer's app, so it cannot affect their AOT publish.
    Source generators must target netstandard2.0 and are correctly marked false -- treating them as
    dependencies flags every project that uses one. "References it" and "ships it" are different
    predicates, and only the second one bears on the claim.

    It does not judge third-party PackageReferences. Their AOT status is not derivable from this
    repository, and a gate that guessed would be worse than one that abstains.

EXIT
    0  every AOT claim is coherent with the dependencies that ship alongside it
    1  at least one project claims AOT that a shipped dependency denies
    2  REFUSE: something could not be read, so coherence is UNMEASURED. NOT a pass.
"""

import os
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(os.path.dirname(__file__))))
SRC = os.path.join(REPO_ROOT, "src")


def _tag(el):
    return el.tag.split("}")[-1]


def aot_claim(proj):
    """Return 'true', 'false', or 'unknown' for a project's IsAotCompatible declaration.

    An explicit declaration is authoritative and needs no MSBuild evaluation. A project that does
    not declare one inherits, and inheritance is resolved by MSBuild rather than assumed -- an
    assumed default is how a silent wrong answer gets in.
    """
    try:
        root = ET.parse(proj).getroot()
    except Exception:
        return "unknown"
    for el in root.iter():
        if _tag(el) == "IsAotCompatible" and el.text:
            v = el.text.strip().lower()
            if v in ("true", "false"):
                return v
    dotnet = os.path.join(REPO_ROOT, ".dotnet", "dotnet.exe")
    if not os.path.isfile(dotnet):
        dotnet = os.path.join(REPO_ROOT, ".dotnet", "dotnet")
    if not os.path.isfile(dotnet):
        dotnet = "dotnet"
    try:
        out = subprocess.run(
            [dotnet, "msbuild", proj, "-getProperty:IsAotCompatible", "-v:q", "-nologo"],
            capture_output=True, text=True, timeout=180,
        ).stdout.strip().lower()
    except Exception:
        return "unknown"
    return out if out in ("true", "false") else "unknown"


def shipped_refs(proj):
    """Return (refs, unreadable): absolute paths of ProjectReferences that ship with this project."""
    base = os.path.dirname(os.path.abspath(proj))
    try:
        root = ET.parse(proj).getroot()
    except Exception:
        return [], 1
    refs, unreadable = [], 0
    for el in root.iter():
        if _tag(el) != "ProjectReference":
            continue
        inc = el.get("Include")
        if not inc:
            continue

        def child(name):
            for c in el:
                if _tag(c) == name:
                    return (c.text or "").strip()
            return None

        roa = el.get("ReferenceOutputAssembly") or child("ReferenceOutputAssembly")
        oit = el.get("OutputItemType") or child("OutputItemType")
        if roa is not None and roa.strip().lower() == "false":
            continue
        if oit is not None and oit.strip().lower() == "analyzer":
            continue
        p = os.path.normpath(os.path.join(base, inc.replace("\\", os.sep)))
        if os.path.isfile(p):
            refs.append(p)
        else:
            unreadable += 1
    return refs, unreadable


def scan(scan_root, quiet=False):
    projects = sorted(
        os.path.join(d, f)
        for d, _, fs in os.walk(scan_root)
        for f in fs
        if f.endswith(".csproj")
    )
    if not projects:
        if not quiet:
            print("REFUSE: no .csproj found under {} -- nothing was measured.".format(scan_root),
                  file=sys.stderr)
        return 2

    claims = {p: aot_claim(p) for p in projects}
    violations = unreadable = checked = 0

    for proj in projects:
        if claims[proj] != "true":
            continue
        checked += 1
        refs, missing = shipped_refs(proj)
        unreadable += missing
        for ref in refs:
            rclaim = claims.get(ref) or aot_claim(ref)
            if rclaim == "unknown":
                unreadable += 1
                continue
            if rclaim == "false":
                violations += 1
                if not quiet:
                    print("  {}".format(os.path.relpath(proj, scan_root)), file=sys.stderr)
                    print("      claims IsAotCompatible=true, but ships", file=sys.stderr)
                    print("      {}".format(os.path.relpath(ref, scan_root)), file=sys.stderr)
                    print("      which declares false.\n", file=sys.stderr)

    if not quiet:
        print("aot-promise-coherence: {} project(s) claim AOT; {} incoherent claim(s); "
              "{} unreadable reference(s).".format(checked, violations, unreadable))
    if violations:
        return 1
    if unreadable:
        # A gate that could not look at something must not return the same exit code as one that
        # looked at everything and found it coherent.
        if not quiet:
            print("REFUSE: {} reference(s) unreadable -- coherence UNMEASURED for them.".format(
                unreadable), file=sys.stderr)
        return 2
    return 0


DOC = os.path.join(REPO_ROOT, "docs-site", "docs", "advanced", "aot-compatibility.md")
DOC_ROW = re.compile(r"^\|\s*`(Excalibur[\w.]+)`\s*\|\s*(.+?)\s*\|")


def check_docs(doc_path=DOC, src_root=SRC, quiet=False):
    """The published per-package table must agree with the property it claims to report.

    That document defines its own statuses in terms of the build property -- "AOT-safe" means
    IsAotCompatible=true, "Not compatible" means false. So a row that disagrees with the csproj is
    not a stale note, it is a false statement to a consumer choosing packages for an AOT
    application, and it is the same defect this gate exists to catch one artifact further out.

    Rows whose status is neither (an analyzer or source generator marked N/A) are skipped
    deliberately: those components run in our compiler and are never published into a consumer
    app, so they make no claim to a consumer at all.
    """
    if not os.path.isfile(doc_path):
        if not quiet:
            print("REFUSE: {} not found -- doc claims were NOT checked.".format(doc_path),
                  file=sys.stderr)
        return 2
    projects = {}
    for d, dirs, fs in os.walk(src_root):
        dirs[:] = [x for x in dirs if x not in ("bin", "obj")]
        for fn in fs:
            if fn.endswith(".csproj"):
                projects[fn[:-len(".csproj")]] = os.path.join(d, fn)

    checked = wrong = 0
    for line in open(doc_path, encoding="utf-8", errors="replace"):
        m = DOC_ROW.match(line)
        if not m:
            continue
        pkg, status = m.group(1), m.group(2).lower()
        proj = projects.get(pkg)
        if not proj:
            continue
        if "aot-safe" in status or "annotated" in status:
            # "Annotated" is the third published status: the package IS compatible, and some
            # methods carry reflection annotations a consumer can avoid. It therefore asserts the
            # same build property as "AOT-safe" and must be checked the same way -- skipping it
            # would drop the cross-check for exactly the packages most likely to drift.
            claim = "true"
        elif "not compatible" in status:
            claim = "false"
        else:
            continue
        actual = aot_claim(proj)
        if actual == "unknown":
            if not quiet:
                print("REFUSE: could not read IsAotCompatible for {}".format(pkg), file=sys.stderr)
            return 2
        checked += 1
        if claim != actual:
            wrong += 1
            if not quiet:
                print("  {} -- the published table says {}, the project declares "
                      "IsAotCompatible={}".format(
                          pkg, "AOT-safe" if claim == "true" else "Not compatible", actual),
                      file=sys.stderr)
    if not quiet:
        print("aot-doc-claims: {} published row(s) checked; {} disagree with the project."
              .format(checked, wrong))
    if checked == 0:
        if not quiet:
            print("REFUSE: no published rows matched -- a parser that matches nothing looks "
                  "exactly like a table that agrees.", file=sys.stderr)
        return 2

    # COVERAGE. A wrong row is a false statement; a MISSING row is silence, and silence is the
    # failure mode nobody notices -- a consumer evaluating the package for an ahead-of-time
    # application finds no status and has nothing to be corrected by.
    documented = set()
    for line in open(doc_path, encoding="utf-8", errors="replace"):
        m = DOC_ROW.match(line)
        if m:
            documented.add(m.group(1))
    undocumented = []
    for name, proj in sorted(projects.items()):
        if name in documented:
            continue
        try:
            if "<IsPackable>false</IsPackable>" in open(
                    proj, encoding="utf-8", errors="replace").read():
                continue          # not shipped, so it makes no claim to a consumer
        except Exception:
            return 2
        undocumented.append(name)
    if undocumented and not quiet:
        for n in undocumented:
            print("  {} ships but has no row in the published table".format(n), file=sys.stderr)
    if not quiet:
        print("aot-doc-coverage: {} shipped package(s) with no published status.".format(
            len(undocumented)))
    return 1 if (wrong or undocumented) else 0


def _write(path, aot, ref=None, attrs=""):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    item = ""
    if ref:
        item = '<ItemGroup><ProjectReference Include="{}"{} /></ItemGroup>'.format(ref, attrs)
    with open(path, "w", encoding="utf-8") as fh:
        fh.write("<Project><PropertyGroup><IsAotCompatible>{}</IsAotCompatible>"
                 "</PropertyGroup>{}</Project>".format(aot, item))


def _doc_fixture(d, status):
    """One csproj declaring false, and a published row making `status` about it."""
    _write(os.path.join(d, "Excalibur.Whatever", "Excalibur.Whatever.csproj"), "false")
    doc = os.path.join(d, "table.md")
    rows = [
        "| Package | AOT Status | Notes |",
        "|---|---|---|",
        "| `Excalibur.Whatever` | {} | |".format(status),
        "",
    ]
    with open(doc, "w", encoding="utf-8") as fh:
        fh.write("\n".join(rows))
    return doc


def _case_coherent(d):
    _write(os.path.join(d, "b", "B.csproj"), "true")
    _write(os.path.join(d, "a", "A.csproj"), "true", "..\\b\\B.csproj")


def _case_incoherent(d):
    _write(os.path.join(d, "b", "B.csproj"), "false")
    _write(os.path.join(d, "a", "A.csproj"), "true", "..\\b\\B.csproj")


def _case_analyzer(d):
    _write(os.path.join(d, "g", "G.csproj"), "false")
    _write(os.path.join(d, "a", "A.csproj"), "true", "..\\g\\G.csproj",
           ' OutputItemType="Analyzer" ReferenceOutputAssembly="false"')


def _case_missing(d):
    _write(os.path.join(d, "a", "A.csproj"), "true", "..\\nope\\N.csproj")


def _case_empty(d):
    os.makedirs(d, exist_ok=True)


def self_test():
    cases = [
        ("coherent pair passes", 0, _case_coherent),
        # Liveness. Without this the gate could pass by never detecting anything -- the exact
        # failure it exists to prevent, applied to itself.
        ("incoherent pair fails", 1, _case_incoherent),
        # False-positive lock. 14 of this gate's first 21 findings were non-shipping analyzer
        # references, every one of them legitimate.
        ("non-shipping analyzer ref to a false project is NOT flagged", 0, _case_analyzer),
        # An unresolvable reference must REFUSE, never read as clean.
        ("missing reference REFUSEs, does not pass", 2, _case_missing),
        # Nothing measured must never look like everything measured and clean.
        ("empty set REFUSEs (exit 2), does not pass", 2, _case_empty),
    ]
    fails = 0
    with tempfile.TemporaryDirectory() as tmp:
        for i, (name, expected, build) in enumerate(cases):
            d = os.path.join(tmp, "case{}".format(i))
            os.makedirs(d, exist_ok=True)
            build(d)
            rc = scan(d, quiet=True)
            if rc == expected:
                print("  PASS  {}".format(name))
            else:
                print("  FAIL  {} -- returned {}, expected {}".format(name, rc, expected))
                fails += 1

        # The published-table arm needs its own arms, or it is a check nobody proved can fail.
        d = os.path.join(tmp, "doc_ok")
        rc = check_docs(_doc_fixture(d, "**Not compatible**"), d, quiet=True)
        print("  PASS  published row matching the project passes" if rc == 0
              else "  FAIL  matching doc row returned {}, expected 0".format(rc))
        fails += rc != 0

        d = os.path.join(tmp, "doc_bad")
        rc = check_docs(_doc_fixture(d, "AOT-safe"), d, quiet=True)
        print("  PASS  a published row claiming AOT the project denies fails" if rc == 1
              else "  FAIL  disagreeing doc row returned {}, expected 1".format(rc))
        fails += rc != 1

        d = os.path.join(tmp, "doc_gap")
        doc = _doc_fixture(d, "**Not compatible**")
        _write(os.path.join(d, "Excalibur.Undocumented", "Excalibur.Undocumented.csproj"), "true")
        rc = check_docs(doc, d, quiet=True)
        print("  PASS  a shipped package with no published row fails" if rc == 1
              else "  FAIL  undocumented package returned {}, expected 1".format(rc))
        fails += rc != 1

        d = os.path.join(tmp, "doc_none")
        os.makedirs(d, exist_ok=True)
        rc = check_docs(os.path.join(d, "absent.md"), d, quiet=True)
        print("  PASS  a missing published table REFUSEs (exit 2)" if rc == 2
              else "  FAIL  missing doc returned {}, expected 2".format(rc))
        fails += rc != 2
    if fails:
        print("self-test: {} arm(s) FAILED".format(fails))
        return 1
    print("self-test: {}/{} arms pass".format(len(cases) + 4, len(cases) + 4))
    return 0


if __name__ == "__main__":
    arg = sys.argv[1] if len(sys.argv) > 1 else ""
    if arg == "--self-test":
        sys.exit(self_test())
    if arg in ("", "--scan"):
        rc_graph = scan(SRC)
        rc_docs = check_docs()
        # Report the worst outcome, and never let a REFUSE be masked by a pass.
        sys.exit(2 if 2 in (rc_graph, rc_docs) else max(rc_graph, rc_docs))
    print("usage: {} [--self-test]".format(os.path.basename(sys.argv[0])), file=sys.stderr)
    sys.exit(2)
