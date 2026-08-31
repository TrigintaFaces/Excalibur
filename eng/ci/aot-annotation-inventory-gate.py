#!/usr/bin/env python3
"""Locks the inventory of public members annotated as not ahead-of-time safe.

WHY THIS EXISTS, AND WHY IT IS NOT OPTIONAL ONCE A PACKAGE OPTS OUT
    Setting IsAotCompatible=false turns OFF the AOT analyzer for that project. That is the correct
    metadata -- the package really is not AOT-safe -- but it means the build stops reporting new
    dynamic-code violations in it. Honest metadata and active detection pull in opposite directions,
    and the opt-out silently buys the first by giving up the second.

    This gate is what keeps the second. It records every public member currently annotated
    RequiresDynamicCode / RequiresUnreferencedCode, so a NEW one has to be added deliberately, in a
    reviewed diff, whether or not an analyzer is still watching that project.

WHAT AN ENTRY MEANS
    An entry is not an approval. It is a record that this member's reflection path was known and
    accepted at the time it was written down. Removing an entry is always safe. Adding one is a
    decision, and this gate exists to make sure it is made by a person rather than by drift.

WHY AN ANNOTATION IS NOT ITSELF A DEFECT
    Microsoft annotates reflection paths on consumer-facing APIs and ships an AOT-safe alternative
    alongside them -- JsonSerializer.Serialize(Object, Type, JsonSerializerOptions) carries
    RequiresUnreferencedCode, and its message names the alternative. So this gate does not forbid
    annotations. It forbids UNNOTICED ones.

EXIT
    0  the inventory matches the source
    1  a public member is annotated that the inventory does not record (or the file is unreadable)
    2  REFUSE: the source could not be enumerated. Nothing was checked. NOT a pass.
"""

import os
import re
import sys

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(os.path.dirname(__file__))))
SRC = os.path.join(REPO_ROOT, "src")
INVENTORY = os.path.join(REPO_ROOT, "eng", "ci", "aot-annotation-inventory.txt")

ATTR = re.compile(
    r"\[\s*(?:System\.Diagnostics\.CodeAnalysis\.)?Requires(DynamicCode|UnreferencedCode)\s*[\(\]]"
)
PUBLIC = re.compile(r"^\s*(public|protected)\b")
# A member declaration's own name, enough to identify it without pinning a line number -- line
# numbers churn on every edit above them and would make this inventory unmergeable.
MEMBER = re.compile(r"[\w<>\[\]?,\. ]*?(\w+)\s*(?:\(|=>|\{|$)")


def enumerate_annotated(src_root, base=None):
    """Return (entries, unreadable). Each entry: 'relative/path.cs::MemberName'."""
    # Relativise against an explicit base. REPO_ROOT is right in production but wrong for a
    # self-test whose fixture may sit on a different drive, where relpath raises rather than
    # returning a path.
    base = base or REPO_ROOT
    entries, unreadable = set(), 0
    if not os.path.isdir(src_root):
        return entries, 1
    for root, dirs, files in os.walk(src_root):
        dirs[:] = [d for d in dirs if d not in ("bin", "obj")]
        for fn in files:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(root, fn)
            try:
                lines = open(path, encoding="utf-8", errors="replace").read().splitlines()
            except Exception:
                unreadable += 1
                continue
            rel = os.path.relpath(path, base).replace(os.sep, "/")
            for i, line in enumerate(lines):
                if not ATTR.search(line):
                    continue
                # Walk forward to the declaration, skipping attribute lists BY BRACKET BALANCE.
                #
                # The obvious version -- skip lines that start with "[" -- is blind to a wrapped
                # attribute. An [UnconditionalSuppressMessage(...)] with a Justification on the
                # next line puts a continuation line in the way that is not blank, not "[", and
                # not a comment, so the walk stops there and never reaches the member. That is the
                # commonest shape in this repo, because long justifications wrap. It made this
                # gate blind to 73 of 279 annotated members while reporting a clean pass.
                k = i
                depth = 0
                found = None
                limit = min(i + 40, len(lines))
                while k < limit:
                    line = lines[k]
                    stripped = line.strip()
                    if depth == 0 and not stripped.startswith("[") and k > i:
                        if not stripped or stripped.startswith("//"):
                            k += 1
                            continue
                        if PUBLIC.match(line):
                            found = stripped
                        break
                    depth += line.count("[") - line.count("]")
                    if depth < 0:
                        depth = 0
                    k += 1
                if found:
                    m = MEMBER.search(found)
                    entries.add("{}::{}".format(rel, m.group(1) if m else found[:40]))
    return entries, unreadable


def load_inventory(path):
    if not os.path.isfile(path):
        return None
    out = set()
    for line in open(path, encoding="utf-8"):
        line = line.strip()
        if line and not line.startswith("#"):
            out.add(line)
    return out


def check(src_root=SRC, inventory_path=INVENTORY, quiet=False, base=None):
    found, unreadable = enumerate_annotated(src_root, base)
    if unreadable:
        if not quiet:
            print("REFUSE: {} source file(s) unreadable -- the inventory was NOT verified.".format(
                unreadable), file=sys.stderr)
        return 2
    recorded = load_inventory(inventory_path)
    if recorded is None:
        if not quiet:
            print("REFUSE: inventory not found at {} -- nothing was compared.".format(
                inventory_path), file=sys.stderr)
        return 2
    if not found and not recorded:
        # Both empty is indistinguishable from a scanner that matched nothing. Say so.
        if not quiet:
            print("REFUSE: no annotated members found and inventory is empty -- "
                  "cannot tell a clean tree from a broken scan.", file=sys.stderr)
        return 2

    added = sorted(found - recorded)
    removed = sorted(recorded - found)

    if not quiet:
        print("aot-annotation-inventory: {} annotated public member(s); "
              "{} new, {} recorded-but-gone.".format(len(found), len(added), len(removed)))
        for e in removed:
            print("  no longer annotated (safe -- drop it from the inventory): {}".format(e))
    if added:
        if not quiet:
            print("", file=sys.stderr)
            print("NEW public member(s) annotated as not ahead-of-time safe:", file=sys.stderr)
            for e in added:
                print("  {}".format(e), file=sys.stderr)
            print("", file=sys.stderr)
            print("An annotation is a contract with the consumer: their build gets the warning at",
                  file=sys.stderr)
            print("their call site. Add an AOT-safe alternative and name it in the attribute",
                  file=sys.stderr)
            print("message, or record the entry in eng/ci/aot-annotation-inventory.txt.",
                  file=sys.stderr)
        return 1
    return 0


def _selftest_tree(tmp, annotate_extra):
    os.makedirs(os.path.join(tmp, "p"), exist_ok=True)
    body = [
        "using System.Diagnostics.CodeAnalysis;",
        "namespace P;",
        "public static class C {",
        '    [RequiresDynamicCode("reflection")]',
        "    public static void Known() { }",
    ]
    if annotate_extra:
        # A WRAPPED attribute, deliberately. A single-line attribute does not exercise the walk
        # that finds the declaration, so a fixture built only from single-line attributes passes
        # whether or not the walk is correct -- which is exactly how this gate shipped blind to 73
        # members with a green self-test.
        body += ['    [RequiresUnreferencedCode("reflection")]',
                 '    [UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",',
                 '        Justification = "a justification long enough to wrap onto its own line")]',
                 "    public static void Sneaky() { }"]
    body.append("}")
    with open(os.path.join(tmp, "p", "C.cs"), "w", encoding="utf-8") as fh:
        fh.write("\n".join(body))


DOCS_TABLE = os.path.join(REPO_ROOT, "docs-site", "docs", "advanced", "aot-compatibility.md")

# A published table row: | `Package` | Status | Notes |
DOC_ROW = re.compile(r"^\|\s*`([A-Za-z0-9_.]+)`\s*\|\s*(AOT-safe|Annotated)\s*\|", re.M)


def packages_with_annotations(inventory_path):
    """Package names that own at least one recorded annotated member."""
    packages = set()
    with open(inventory_path, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            parts = line.split("::")[0].split("/")
            if len(parts) > 2 and parts[0] == "src":
                packages.add(parts[2])
    return packages


def check_docs(docs_path=None, inventory_path=None, quiet=False):
    """The published package table must agree with the inventory.

    A row calling a package AOT-safe while the inventory records an annotated public member in it
    tells a consumer they can publish it with no warnings, which their own build will contradict.
    The inventory is the measurement; the table is a claim about it, so the table is what must move.

    EXIT  0 agree  |  1 a row disagrees  |  2 REFUSE: nothing was checked.
    """
    docs_path = docs_path or DOCS_TABLE
    inventory_path = inventory_path or INVENTORY
    try:
        annotated = packages_with_annotations(inventory_path)
        text = open(docs_path, encoding="utf-8").read()
    except OSError as exc:
        print("REFUSE: {}. Nothing was checked; this is NOT a pass.".format(exc), file=sys.stderr)
        return 2

    rows = DOC_ROW.findall(text)
    if not rows or not annotated:
        print("REFUSE: no table rows ({}) or no annotated packages ({}). Nothing was compared."
              .format(len(rows), len(annotated)), file=sys.stderr)
        return 2

    wrong = []
    for package, status in rows:
        has = package in annotated
        if has and status == "AOT-safe":
            wrong.append("{}: table says AOT-safe, inventory records annotated members".format(package))
        elif not has and status == "Annotated":
            wrong.append("{}: table says Annotated, inventory records none".format(package))

    if wrong:
        if not quiet:
            print("The published ahead-of-time table disagrees with the annotation inventory:",
                  file=sys.stderr)
            for line in wrong:
                print("  " + line, file=sys.stderr)
        return 1
    if not quiet:
        print("docs table agrees with the inventory ({} rows, {} annotated packages)"
              .format(len(rows), len(annotated)))
    return 0


def self_test():
    import tempfile
    fails = 0
    with tempfile.TemporaryDirectory() as tmp:
        src = os.path.join(tmp, "src")
        os.makedirs(src, exist_ok=True)
        inv = os.path.join(tmp, "inv.txt")

        # Arm 1 (safety): source matching the inventory passes.
        _selftest_tree(src, annotate_extra=False)
        found, _ = enumerate_annotated(src, tmp)
        with open(inv, "w", encoding="utf-8") as fh:
            fh.write("\n".join(sorted(found)) + "\n")
        rc = check(src, inv, quiet=True, base=tmp)
        print("  PASS  matching inventory passes" if rc == 0
              else "  FAIL  matching inventory returned {}, expected 0".format(rc))
        fails += rc != 0

        # Arm 2 (liveness): a NEW annotated member must fail. Without this the gate could pass by
        # never detecting anything -- the failure it exists to prevent, applied to itself.
        _selftest_tree(src, annotate_extra=True)
        rc = check(src, inv, quiet=True, base=tmp)
        print("  PASS  a newly annotated public member fails" if rc == 1
              else "  FAIL  new annotation returned {}, expected 1".format(rc))
        fails += rc != 1

        # Arm 3 (refusal): a missing inventory must REFUSE, never pass.
        rc = check(src, os.path.join(tmp, "nope.txt"), quiet=True, base=tmp)
        print("  PASS  missing inventory REFUSEs (exit 2)" if rc == 2
              else "  FAIL  missing inventory returned {}, expected 2".format(rc))
        fails += rc != 2

        # Arm 4 (refusal): an empty tree with an empty inventory must REFUSE, not report clean --
        # a scanner that matches nothing looks exactly like a tree with nothing to find.
        empty = os.path.join(tmp, "empty")
        os.makedirs(empty, exist_ok=True)
        empty_inv = os.path.join(tmp, "empty.txt")
        open(empty_inv, "w", encoding="utf-8").close()
        rc = check(empty, empty_inv, quiet=True, base=tmp)
        print("  PASS  empty tree + empty inventory REFUSEs (exit 2)" if rc == 2
              else "  FAIL  empty/empty returned {}, expected 2".format(rc))
        fails += rc != 2

        # Arms 5-7 (docs table): the same three states, on the claim rather than the source.
        doc_inv = os.path.join(tmp, "docinv.txt")
        with open(doc_inv, "w", encoding="utf-8") as fh:
            fh.write("src/Dispatch/Pkg.Annotated/File.cs::Member\n")
        agree = os.path.join(tmp, "agree.md")
        with open(agree, "w", encoding="utf-8") as fh:
            fh.write("## heading, so a first-line-only match cannot carry the arm\n| `Pkg.Annotated` | Annotated | note |\n| `Pkg.Clean` | AOT-safe | |\n")
        rc = check_docs(agree, doc_inv, quiet=True)
        print("  PASS  an agreeing docs table passes" if rc == 0
              else "  FAIL  agreeing table returned {}, expected 0".format(rc))
        fails += rc != 0

        disagree = os.path.join(tmp, "disagree.md")
        with open(disagree, "w", encoding="utf-8") as fh:
            fh.write("## heading\n| `Pkg.Annotated` | AOT-safe | |\n")
        rc = check_docs(disagree, doc_inv, quiet=True)
        print("  PASS  a row claiming AOT-safe over an annotated package fails" if rc == 1
              else "  FAIL  disagreeing table returned {}, expected 1".format(rc))
        fails += rc != 1

        rc = check_docs(os.path.join(tmp, "nope.md"), doc_inv, quiet=True)
        print("  PASS  a missing docs table REFUSEs (exit 2)" if rc == 2
              else "  FAIL  missing table returned {}, expected 2".format(rc))
        fails += rc != 2

    if fails:
        print("self-test: {} arm(s) FAILED".format(fails))
        return 1
    print("self-test: 7/7 arms pass")
    return 0


def regenerate():
    found, unreadable = enumerate_annotated(SRC)
    if unreadable:
        print("REFUSE: {} unreadable file(s); inventory NOT written.".format(unreadable),
              file=sys.stderr)
        return 2
    header = [
        "# Public members annotated RequiresDynamicCode / RequiresUnreferencedCode.",
        "#",
        "# An entry is a RECORD, not an approval: this member's reflection path was known and",
        "# accepted when it was written down. Removing an entry is always safe. Adding one is a",
        "# decision -- it means a consumer's build will now warn at their call site.",
        "#",
        "# Regenerate deliberately, never to clear a failure you have not read:",
        "#   python3 eng/ci/aot-annotation-inventory-gate.py --regenerate",
        "",
    ]
    with open(INVENTORY, "w", encoding="utf-8") as fh:
        fh.write("\n".join(header) + "\n".join(sorted(found)) + "\n")
    print("wrote {} entries to {}".format(len(found), os.path.relpath(INVENTORY, REPO_ROOT)))
    return 0


if __name__ == "__main__":
    arg = sys.argv[1] if len(sys.argv) > 1 else ""
    if arg == "--self-test":
        sys.exit(self_test())
    if arg == "--regenerate":
        sys.exit(regenerate())
    if arg == "--check-docs":
        sys.exit(check_docs())
    if arg in ("", "--check"):
        sys.exit(max(check(), check_docs()))
    print("usage: {} [--self-test|--regenerate|--check-docs]".format(os.path.basename(sys.argv[0])),
          file=sys.stderr)
    sys.exit(2)
