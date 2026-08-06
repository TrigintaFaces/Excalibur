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

BUDGETS
-------
--max-flaky-rate and --max-flaky-count declare what is acceptable. Until they were added this
report computed a rate that nothing compared to anything, which is a measurement with no decision
attached to it -- the number could double and no artifact anywhere would say so.

They are BUDGETS, not a ratchet, and the difference decides what to do when one is breached. A
coverage floor starts at the level already achieved, so it can only detect a fall. A flake budget
starts at the level considered tolerable, so it can be breached on its first run. When it is, the
response is to fix or quarantine the tests -- NOT to raise the budget to whatever was observed,
which would convert the only line anyone might defend into a description of the status quo.

EXIT CODES -- distinct on purpose.
  0  report produced, and within budget (or no budget was declared)
  1  report produced, and a declared budget was EXCEEDED
  2  REFUSE: no parseable input. An empty report and an unread one look identical, and the
     difference matters: "no flakes" and "no data" are opposite conclusions.

Exit 1 and exit 2 must never be collapsed. "Too many flaky tests" and "the report could not be
produced" call for opposite responses, and a budget that turned a REFUSE into either a pass or a
breach would be reporting on a window it never read. The budget is therefore evaluated only after
input has been parsed, never before.
"""
from __future__ import annotations

import argparse
import collections
import glob
import os
import sys
import xml.etree.ElementTree as ET

TRX_NS = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
EXIT_OK = 0
EXIT_OVER_BUDGET = 1
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


def evaluate_budget(flaky, max_rate, max_count):
    """Compare the observed flake population against the declared budgets.

    Returns (breaches, lines) -- breaches is a list of one-line reasons, empty when within budget.
    `flaky` is the same (rate, failed, total, name) tuple list the report renders, so the verdict
    and the table can never disagree about what was counted.
    """
    breaches, lines = [], []

    if max_count is None and max_rate is None:
        lines.append("_No flake budget was declared, so nothing was compared. "
                     "Pass `--max-flaky-count` and/or `--max-flaky-rate` to make this report "
                     "capable of a verdict._")
        return breaches, lines

    if max_count is not None:
        verdict = "MET" if len(flaky) <= max_count else "EXCEEDED"
        lines.append(f"- flaky test count: **{len(flaky)}** against a budget of "
                     f"**{max_count}** -- **{verdict}**")
        if verdict == "EXCEEDED":
            breaches.append(f"{len(flaky)} flaky test(s) against a budget of {max_count}")

    if max_rate is not None:
        # Strictly greater. A test sitting exactly ON the budget is within it; an off-by-one here
        # reports a breach for the population the budget was chosen to permit.
        over = [f for f in flaky if f[0] > max_rate]
        worst = max((f[0] for f in flaky), default=0.0)
        verdict = "MET" if not over else "EXCEEDED"
        lines.append(f"- worst individual flake rate: **{worst:.0%}** against a budget of "
                     f"**{max_rate:.0%}** ({len(over)} test(s) over) -- **{verdict}**")
        if over:
            breaches.append(f"{len(over)} test(s) flake more often than {max_rate:.0%}")

    return breaches, lines


def render_budget(flaky, max_rate, max_count) -> int:
    """Print the budget section and return the process exit code for it."""
    breaches, lines = evaluate_budget(flaky, max_rate, max_count)
    print()
    print("### Budget")
    print()
    for line in lines:
        print(line)
    if not breaches:
        return EXIT_OK
    print()
    print("**Over budget.** The fix is to repair or quarantine the tests named above. Raising the "
          "budget to match what was observed would turn the only defensible line into a description "
          "of the status quo.")
    for b in breaches:
        print(f"::error::Flake budget exceeded: {b}", file=sys.stderr)
    return EXIT_OVER_BUDGET


def main() -> int:
    ap = argparse.ArgumentParser(description="Report tests that both passed and failed across runs.")
    ap.add_argument("--glob", default="flake-input/**/*.trx",
                    help="glob for TRX files gathered from recent runs")
    ap.add_argument("--top", type=int, default=20, help="how many offenders to list")
    ap.add_argument("--max-flaky-count", type=int, default=None,
                    help="maximum tolerated number of flaky tests; exceeding it exits 1")
    ap.add_argument("--max-flaky-rate", type=float, default=None,
                    help="maximum tolerated per-test flake rate as a fraction (0.05 = 5%%); "
                         "any test above it exits 1")
    ap.add_argument("--self-test", action="store_true", help="prove this report is non-vacuous")
    args = ap.parse_args()

    if args.max_flaky_rate is not None and not 0.0 <= args.max_flaky_rate <= 1.0:
        print(f"::error::--max-flaky-rate must be a fraction between 0 and 1, got "
              f"{args.max_flaky_rate}. A budget of '5' would permit every test to fail every run.",
              file=sys.stderr)
        return EXIT_REFUSE

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
        # The budget is still rendered on a clean window. A verdict that appears only when there is
        # bad news leaves a reader unable to tell "within budget" from "the budget was never
        # evaluated", which is the gap this whole section exists to close.
        return render_budget(flaky, args.max_flaky_rate, args.max_flaky_count)

    print(f"### Top {min(args.top, len(flaky))} by flake rate")
    print()
    print("| rate | failed | runs | test |")
    print("| ---: | ---: | ---: | --- |")
    for rate, failed, total, name in flaky[: args.top]:
        short = name if len(name) <= 90 else name[:87] + "..."
        print(f"| {rate:.0%} | {failed} | {total} | `{short}` |")
    return render_budget(flaky, args.max_flaky_rate, args.max_flaky_count)


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

    # ---- budget arms ----
    # The fixture window contains exactly one flaky test at a 50% rate (Wobbly: 1 pass, 1 fail).
    # Every expectation below is derived from that, not from a remembered number.
    glob_arg = ["--glob", os.path.join(d, "*.trx")]

    budget_cases = [
        # (description, extra args, expected exit)
        ("no budget declared leaves the exit code at 0 (unchanged behaviour)",
         [], EXIT_OK),
        ("a count budget the window MEETS exits 0 (liveness)",
         ["--max-flaky-count", "1"], EXIT_OK),
        ("a count budget the window EXCEEDS exits 1 (safety)",
         ["--max-flaky-count", "0"], EXIT_OVER_BUDGET),
        ("a rate budget the window MEETS exits 0 (liveness)",
         ["--max-flaky-rate", "0.5"], EXIT_OK),
        ("a rate budget the window EXCEEDS exits 1 (safety)",
         ["--max-flaky-rate", "0.4"], EXIT_OVER_BUDGET),
        ("a rate exactly ON the budget is within it, not over",
         ["--max-flaky-rate", "0.5", "--max-flaky-count", "1"], EXIT_OK),
        ("a nonsensical rate budget REFUSES instead of permitting everything",
         ["--max-flaky-rate", "5"], EXIT_REFUSE),
    ]
    for desc, extra, want in budget_cases:
        got = main_with(glob_arg + extra)
        if got != want:
            print(f"SELF-TEST FAIL -- {desc}: expected exit {want}, got {got}", file=sys.stderr)
            return 1
        print(f"SELF-TEST: PASS -- {desc}")

    # THE ARM THAT MATTERS MOST. A budget must not be able to turn "nothing was measured" into a
    # verdict of any kind. If an unread window could exit 0 because zero flaky tests is within
    # budget, the report would certify a clean window it had never opened -- and that is the exact
    # failure this file's REFUSE code was introduced to prevent, re-entering through the budget.
    rc = main_with(["--glob", os.path.join(empty, "*.trx"), "--max-flaky-count", "0",
                    "--max-flaky-rate", "0.0"])
    if rc != EXIT_REFUSE:
        print(f"SELF-TEST FAIL -- an unreadable window with a budget returned {rc}, expected "
              f"REFUSE ({EXIT_REFUSE}). A budget must never convert 'no data' into a verdict.",
              file=sys.stderr)
        return 1
    print("SELF-TEST: PASS -- a budget does not convert REFUSE into a pass or a breach (safety)")

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
