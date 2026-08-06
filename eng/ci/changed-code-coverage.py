#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
"""Coverage of the lines THIS change touched, alongside the whole-repository baseline.

WHY BOTH NUMBERS
----------------
The baseline answers "how covered is the repository", which barely moves and which no single change
can be held to. Changed-code coverage answers "did this change bring its own tests" -- the question
a reviewer actually has, and the only one an author can act on. A repository can sit at a stable
baseline for months while every new line arrives untested, and the baseline will not say so.

It reuses the cobertura reports the coverage job has already downloaded and the diff git can already
produce. No extra collector, no second test run, no additional aggregation job -- the duplicate
coverage run was removed on purpose and this must not put one back.

DISPLAY ONLY. Nothing here gates. A changed-code floor is a policy decision and is not made here.

THE THREE HONEST ANSWERS, kept distinct because they call for different things:
  covered        the changed line was executed by a test
  uncovered      the changed line was instrumented and never executed  -> write a test
  NOT MEASURED   the changed line is in no report at all               -> nothing is known about it

The third is the one that must never be folded into either of the others. Counting unmeasured lines
as covered inflates the number; counting them as uncovered blames an author for a file the collector
never instrumented. Both are wrong in a way that survives review, because either produces a
plausible percentage.

EXIT CODES
  0  report produced (including "nothing to report", which is a real answer)
  2  REFUSE: no coverage reports. "No data" and "nothing was covered" are opposite conclusions.
"""
from __future__ import annotations

import argparse
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET

EXIT_OK, EXIT_REFUSE = 0, 2
PRODUCT = re.compile(r"(?i)(src/(?:Dispatch|Excalibur)/.+)$")
HUNK = re.compile(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@")


def normalise(path: str | None) -> str | None:
    """Reduce a cobertura filename or a diff path to a repository-relative product path.

    Cobertura filenames arrive as SourceLink URLs; diff paths arrive as `b/src/...`. Both are
    reduced through the same expression so the two sides of the intersection cannot disagree about
    what counts as the same file.
    """
    if not path:
        return None
    m = PRODUCT.search(path.strip().replace("\\", "/"))
    return m.group(1) if m else None


def changed_lines(diff_text: str) -> dict[str, set[int]]:
    """Added line numbers per product file, from a `git diff --unified=0` patch.

    Only ADDED lines count. A deleted line cannot be covered, and treating removed code as
    uncovered would penalise deletions -- which are the change most likely to raise coverage.
    """
    out: dict[str, set[int]] = {}
    current = None
    for line in diff_text.splitlines():
        if line.startswith("+++ "):
            current = normalise(line[4:].strip())
            continue
        if line.startswith("@@") and current:
            m = HUNK.match(line)
            if m:
                start = int(m.group(1))
                count = 1 if m.group(2) is None else int(m.group(2))
                if count:  # count 0 means a pure deletion at this position
                    out.setdefault(current, set()).update(range(start, start + count))
    return out


def coverage_hits(root: str) -> tuple[dict[tuple[str, int], int], int]:
    """Best observed hit count per (product file, line), plus the number of reports read.

    Deduplicated by taking the MAXIMUM across reports: six shards each report the same source line,
    and a line executed by any shard is covered.
    """
    hits: dict[tuple[str, int], int] = {}
    files = sorted(glob.glob(os.path.join(root, "**", "coverage.cobertura.xml"), recursive=True))
    read = 0
    for f in files:
        try:
            root_el = ET.parse(f).getroot()
        except ET.ParseError as exc:
            print(f"  warning: {os.path.basename(f)} did not parse: {str(exc)[:90]}", file=sys.stderr)
            continue
        read += 1
        for cls in root_el.iter("class"):
            path = normalise(cls.get("filename"))
            if path is None:
                continue
            for ln in cls.iter("line"):
                num, hit = ln.get("number"), ln.get("hits")
                if not (num or "").isdigit():
                    continue
                key = (path, int(num))
                n = int(hit) if (hit or "").lstrip("-").isdigit() else 0
                if n > hits.get(key, -1):
                    hits[key] = n
    return hits, read


def analyse(changed: dict[str, set[int]], hits: dict[tuple[str, int], int]):
    covered = uncovered = 0
    unmeasured_files: dict[str, int] = {}
    per_file = []
    for path in sorted(changed):
        c = u = n = 0
        for line in sorted(changed[path]):
            h = hits.get((path, line))
            if h is None:
                n += 1
            elif h > 0:
                c += 1
            else:
                u += 1
        covered += c
        uncovered += u
        if n:
            unmeasured_files[path] = n
        if c or u:
            per_file.append((path, c, c + u))
    return covered, uncovered, unmeasured_files, per_file


def report(changed, hits, reports_read, baseline=None) -> int:
    print("## Changed-code coverage")
    print()
    if baseline:
        print(f"- baseline (whole repository): **{baseline}**")

    if not changed:
        # Not 0%. A change that touches no product source has nothing to be covered, and printing a
        # percentage here would read as "this change is untested".
        print("- changed product lines: **none** -- this change touches no file under "
              "`src/Dispatch` or `src/Excalibur`, so there is nothing to measure.")
        return EXIT_OK

    covered, uncovered, unmeasured, per_file = analyse(changed, hits)
    measured = covered + uncovered
    total = sum(len(v) for v in changed.values())

    if measured == 0:
        print(f"- changed product lines: **{total}**, of which **0 were measured**.")
        print()
        print("_No percentage is shown. Every changed line is outside the assemblies these reports "
              "instrument, so nothing is known about their coverage -- which is not the same as "
              "their being uncovered._")
        _print_unmeasured(unmeasured)
        return EXIT_OK

    pct = 100.0 * covered / measured
    print(f"- changed code: **{pct:.1f}%** ({covered}/{measured} changed lines covered)")
    print(f"- read from {reports_read} coverage report(s)")
    if unmeasured:
        print(f"- **not measured: {sum(unmeasured.values())} changed line(s)** in "
              f"{len(unmeasured)} file(s) that appear in no report")
    print()
    print("| file | covered / changed |")
    print("| --- | ---: |")
    for path, c, m in sorted(per_file, key=lambda r: (r[1] / r[2], r[0])):
        print(f"| `{path}` | {c}/{m} |")
    _print_unmeasured(unmeasured)
    print()
    print("_Display only: no threshold is applied to this figure._")
    return EXIT_OK


def _print_unmeasured(unmeasured: dict[str, int]) -> None:
    if not unmeasured:
        return
    print()
    print("**Not measured** -- these changed lines are in no coverage report, so they are neither "
          "covered nor uncovered. Counting them either way would produce a believable and wrong "
          "number:")
    print()
    for path, n in sorted(unmeasured.items()):
        print(f"- `{path}` -- {n} line(s)")


def main() -> int:
    ap = argparse.ArgumentParser(description="Coverage of changed lines, alongside the baseline.")
    ap.add_argument("--coverage-root", default="cov")
    ap.add_argument("--diff-file", help="a `git diff --unified=0` patch; '-' reads stdin")
    ap.add_argument("--baseline", help="baseline percentage to display alongside, e.g. '39.6%%'")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    hits, reports_read = coverage_hits(args.coverage_root)
    if reports_read == 0:
        print(f"::error::REFUSE: no parseable coverage reports under {args.coverage_root!r}. "
              "Nothing was measured, which is not the same as nothing being covered.",
              file=sys.stderr)
        return EXIT_REFUSE

    if not args.diff_file:
        print("::error::REFUSE: no --diff-file. Without a diff there is no changed-line set, and "
              "an empty one would read as 'this change touched nothing'.", file=sys.stderr)
        return EXIT_REFUSE

    text = sys.stdin.read() if args.diff_file == "-" else \
        open(args.diff_file, encoding="utf-8", errors="replace").read()
    return report(changed_lines(text), hits, reports_read, args.baseline)


def self_test() -> int:
    """Liveness: a covered changed line reads as covered. Safety: an uncovered one is not hidden."""
    import tempfile
    d = tempfile.mkdtemp()

    src = "src/Dispatch/Excalibur.Dispatch/Thing.cs"
    os.makedirs(os.path.join(d, "shard"), exist_ok=True)
    with open(os.path.join(d, "shard", "coverage.cobertura.xml"), "w", encoding="utf-8") as fh:
        fh.write(f"""<?xml version="1.0"?><coverage><packages><package name="p"><classes>
          <class name="c" filename="https://raw.githubusercontent.com/o/r/sha/{src}">
            <lines>
              <line number="10" hits="4" />
              <line number="11" hits="0" />
              <line number="12" hits="7" />
            </lines>
          </class></classes></package></packages></coverage>""")
    hits, read = coverage_hits(d)

    def diff(*lines):
        body = "".join(f"@@ -0,0 +{n},1 @@\n+x\n" for n in lines)
        return f"--- a/{src}\n+++ b/{src}\n{body}"

    checks = []

    # LIVENESS: a changed line that IS covered reports as covered.
    c, u, n, _ = analyse(changed_lines(diff(10, 12)), hits)
    checks.append(("a covered changed line counts as covered", (c, u, len(n)) == (2, 0, 0), (c, u, n)))

    # SAFETY: a changed line that is NOT covered is visible. This is the whole purpose; if an
    # uncovered new line could report as covered the number would be actively misleading.
    c, u, n, _ = analyse(changed_lines(diff(11)), hits)
    checks.append(("an uncovered changed line counts as uncovered", (c, u, len(n)) == (0, 1, 0), (c, u, n)))

    # The mixed case, where the percentage has to be right rather than merely non-zero.
    c, u, n, _ = analyse(changed_lines(diff(10, 11)), hits)
    checks.append(("a half-covered change reports 1 of 2", (c, u, len(n)) == (1, 1, 0), (c, u, n)))

    # SAFETY: a changed line in no report is NOT MEASURED -- neither covered nor uncovered.
    c, u, n, _ = analyse(changed_lines(diff(99)), hits)
    checks.append(("a changed line absent from every report is unmeasured, not covered",
                   (c, u, len(n)) == (0, 0, 1), (c, u, n)))

    # SAFETY: a file outside the product tree is not part of the changed set at all.
    other = "--- a/tests/Foo/Bar.cs\n+++ b/tests/Foo/Bar.cs\n@@ -0,0 +1,1 @@\n+x\n"
    checks.append(("a non-product file is excluded", changed_lines(other) == {}, changed_lines(other)))

    # SAFETY: a pure deletion contributes no changed lines. Removing code must not read as
    # uncovered new code.
    deletion = f"--- a/{src}\n+++ b/{src}\n@@ -5,2 +4,0 @@\n-gone\n-gone\n"
    checks.append(("a pure deletion adds no changed lines", changed_lines(deletion) == {}, changed_lines(deletion)))

    # LIVENESS: the reports were actually read, so the checks above are not passing on an empty map.
    checks.append(("the fixture report was parsed", read == 1 and len(hits) == 3, (read, len(hits))))

    bad = 0
    for desc, ok, detail in checks:
        print(f"SELF-TEST: {'PASS' if ok else 'FAIL'} -- {desc}" + ("" if ok else f"  got {detail}"))
        bad += 0 if ok else 1

    # REFUSE: no reports must not read as "nothing covered".
    if main_with(["--coverage-root", os.path.join(d, "empty"), "--diff-file", "-"]) != EXIT_REFUSE:
        print("SELF-TEST FAIL -- an empty coverage root did not REFUSE", file=sys.stderr)
        bad += 1
    else:
        print("SELF-TEST: PASS -- no coverage reports REFUSES rather than reporting 0%")

    if bad:
        print(f"SELF-TEST: FAIL -- {bad} problem(s).", file=sys.stderr)
        return 1
    print("SELF-TEST: changed-code coverage is non-vacuous.")
    return 0


def main_with(argv):
    saved = sys.argv
    try:
        sys.argv = [saved[0]] + argv
        return main()
    finally:
        sys.argv = saved


if __name__ == "__main__":
    sys.exit(main())
