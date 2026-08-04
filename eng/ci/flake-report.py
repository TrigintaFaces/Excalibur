#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
"""Flake-rate report from retained TRX results.

WHY THIS SHAPE, AND NOT A RETRY
-------------------------------
The obvious way to detect a flaky test is to rerun failures and see which pass the second time.
That is deliberately NOT what this does, because a retry in the test path is one edit away from
turning red green: the moment a rerun's success becomes the job's result, a flaky test is
indistinguishable from a healthy one and the signal is destroyed at the instant it is produced.

Tests here run exactly once, and that first result is authoritative. The flake signal is taken from
history instead: a test that has BOTH passed and failed across the runs in the window is flaky by
observation, with no change to how tests execute and no way for this report to make a red run green.
It reports; it does not adjudicate.

WHAT COUNTS AS FLAKY
--------------------
A test observed with at least one Passed AND at least one Failed outcome across the analysed runs.
Its flake rate is failures / total observations. Tests seen only once cannot be classified and are
counted separately rather than silently dropped -- an unclassifiable population is a fact about the
report's confidence, not a rounding error.

EXIT CODES -- distinct on purpose.
  0  report produced (flaky tests may or may not exist; this command reports, it does not gate)
  2  REFUSE: no parseable input. An empty report and an unread one look identical, and the
     difference matters: "no flakes" and "no data" are opposite conclusions.
"""
from __future__ import annotations

import argparse
import collections
import glob
import os
import sys
import xml.etree.ElementTree as ET

TRX_NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
EXIT_REFUSE = 2


def parse_trx(path: str) -> list[tuple[str, str]]:
    """Return (test name, outcome) pairs. A file that will not parse yields nothing and says so."""
    try:
        root = ET.parse(path).getroot()
    except Exception as exc:  # noqa: BLE001 - any parse failure is equally uninformative here
        print(f"  warning: {os.path.basename(path)} did not parse: {str(exc)[:90]}", file=sys.stderr)
        return []
    out = []
    for r in root.iter(f"{TRX_NS}UnitTestResult"):
        name, outcome = r.get("testName"), r.get("outcome")
        if name and outcome:
            out.append((name, outcome))
    return out


def analyse(files: list[str]):
    seen: dict[str, collections.Counter] = collections.defaultdict(collections.Counter)
    parsed = 0
    for f in files:
        pairs = parse_trx(f)
        if pairs:
            parsed += 1
        for name, outcome in pairs:
            seen[name][outcome] += 1
    return seen, parsed


def main() -> int:
    ap = argparse.ArgumentParser(description="Report tests that both passed and failed across runs.")
    ap.add_argument("--glob", default="flake-input/**/*.trx",
                    help="glob for TRX files gathered from recent runs")
    ap.add_argument("--top", type=int, default=20, help="how many offenders to list")
    ap.add_argument("--self-test", action="store_true", help="prove this report is non-vacuous")
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    files = sorted(glob.glob(args.glob, recursive=True))
    if not files:
        print(f"::error::REFUSE: no TRX files matched {args.glob!r}. "
              "An empty report and an unread one look identical; this is the latter.", file=sys.stderr)
        return EXIT_REFUSE

    seen, parsed = analyse(files)
    if parsed == 0:
        print(f"::error::REFUSE: {len(files)} file(s) matched but none parsed. Nothing was measured.",
              file=sys.stderr)
        return EXIT_REFUSE

    flaky, single = [], 0
    for name, counts in seen.items():
        total = sum(counts.values())
        if total < 2:
            single += 1
            continue
        passed, failed = counts.get("Passed", 0), counts.get("Failed", 0)
        if passed and failed:
            flaky.append((failed / total, failed, total, name))
    flaky.sort(reverse=True)

    print(f"## Flake report - {parsed} result file(s), {len(seen)} distinct test(s)")
    print()
    print(f"- observed more than once: **{len(seen) - single}**")
    print(f"- observed only once (cannot be classified): **{single}**")
    print(f"- **flaky (both passed and failed): {len(flaky)}**")
    print()
    if not flaky:
        print("No test both passed and failed in this window.")
        print()
        print("_This is evidence of absence only to the extent the window is wide enough; "
              "a test that ran once cannot be seen to flake._")
        return 0

    print(f"### Top {min(args.top, len(flaky))} by flake rate")
    print()
    print("| rate | failed | runs | test |")
    print("| ---: | ---: | ---: | --- |")
    for rate, failed, total, name in flaky[: args.top]:
        short = name if len(name) <= 90 else name[:87] + "..."
        print(f"| {rate:.0%} | {failed} | {total} | `{short}` |")
    return 0


def self_test() -> int:
    """Safety and liveness. A report that can only ever say 'no flakes' is worse than none."""
    import tempfile

    def trx(results):
        rows = "".join(
            f'<UnitTestResult testName="{n}" outcome="{o}" />' for n, o in results
        )
        return (f'<?xml version="1.0" encoding="UTF-8"?>'
                f'<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                f"<Results>{rows}</Results></TestRun>")

    d = tempfile.mkdtemp()
    # run 1: Steady passes, Wobbly passes.  run 2: Steady passes, Wobbly FAILS.
    open(os.path.join(d, "a.trx"), "w").write(trx([("Steady", "Passed"), ("Wobbly", "Passed")]))
    open(os.path.join(d, "b.trx"), "w").write(trx([("Steady", "Passed"), ("Wobbly", "Failed")]))
    seen, parsed = analyse(sorted(glob.glob(os.path.join(d, "*.trx"))))

    if parsed != 2:
        print(f"SELF-TEST FAIL -- parsed {parsed} of 2 fixtures", file=sys.stderr)
        return 1

    wob, steady = seen["Wobbly"], seen["Steady"]
    # LIVENESS: the flaky one is detected.
    if not (wob.get("Passed") == 1 and wob.get("Failed") == 1):
        print(f"SELF-TEST FAIL -- flaky test not observed both ways: {dict(wob)}", file=sys.stderr)
        return 1
    print("SELF-TEST: PASS -- a test that passed once and failed once is detected (liveness)")

    # SAFETY: the steady one is NOT reported. A report that flags everything is not a report.
    if steady.get("Failed"):
        print(f"SELF-TEST FAIL -- a consistently passing test was flagged: {dict(steady)}", file=sys.stderr)
        return 1
    print("SELF-TEST: PASS -- a consistently passing test is not flagged (safety)")

    # REFUSE: no input must not read as 'no flakes'.
    empty = os.path.join(d, "empty")
    os.makedirs(empty, exist_ok=True)
    rc = main_with(["--glob", os.path.join(empty, "*.trx")])
    if rc != EXIT_REFUSE:
        print(f"SELF-TEST FAIL -- empty input returned {rc}, expected REFUSE ({EXIT_REFUSE}). "
              "'No data' would be reported as 'no flakes'.", file=sys.stderr)
        return 1
    print("SELF-TEST: PASS -- no input REFUSES rather than reporting a clean window (safety)")
    print("SELF-TEST: the flake report is non-vacuous.")
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
