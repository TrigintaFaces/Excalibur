#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
"""CI service-level indicators, computed from workflow run history.

WHY
---
Every question this answers has been answered by hand at least once, and got a wrong answer at
least once: is the queue draining, how long does a run take, how often does main go red, is a
failure new or recurring. Hand-measurement of a moving system produces a snapshot reported as a
state -- three consecutive readings of the same run gave "nothing is running", "2 failures" and
"0 failures", all from glancing at labels instead of counting.

WHAT IS AND IS NOT MEASURED
---------------------------
Reported: success rate, cancellation rate, rerun rate, median and p95 wall time, and queue time
(run created -> first job started). All derive from data the API actually returns.

NOT reported, deliberately: "infrastructure failure rate" needs a failure CLASSIFICATION this
repository does not yet produce, and flake rate is the flake report's job. Printing a plausible
number for either would be worse than the gap -- an indicator nobody can trace to a measurement is
how a dashboard starts lying.

EXIT CODES
  0  report produced
  2  REFUSE: no runs retrieved. "No data" and "everything is healthy" must never print the same.
"""
from __future__ import annotations

import argparse
import json
import statistics
import subprocess
import sys
from datetime import datetime

EXIT_REFUSE = 2


def _iso(s):
    if not s:
        return None
    try:
        return datetime.fromisoformat(s.replace("Z", "+00:00"))
    except ValueError:
        return None


def fetch_runs(repo: str, workflow: str, limit: int) -> list[dict]:
    cmd = ["gh", "api", "-X", "GET",
           f"repos/{repo}/actions/workflows/{workflow}/runs",
           "-f", f"per_page={min(limit,100)}", "-f", "branch=main",
           "--jq", ".workflow_runs"]
    try:
        out = subprocess.run(cmd, capture_output=True, text=True, timeout=120)
    except Exception as exc:  # noqa: BLE001
        print(f"  warning: gh failed for {workflow}: {exc}", file=sys.stderr)
        return []
    if out.returncode != 0:
        print(f"  warning: gh exit {out.returncode} for {workflow}: {out.stderr.strip()[:120]}",
              file=sys.stderr)
        return []
    try:
        return json.loads(out.stdout or "[]")[:limit]
    except json.JSONDecodeError:
        return []


def summarise(runs: list[dict]) -> dict:
    done = [r for r in runs if r.get("status") == "completed"]
    concl = [r.get("conclusion") for r in done]
    durations, queue_waits = [], []
    for r in done:
        created, started, updated = _iso(r.get("created_at")), _iso(r.get("run_started_at")), _iso(r.get("updated_at"))
        if created and updated:
            durations.append((updated - created).total_seconds() / 60)
        if created and started:
            queue_waits.append(max(0.0, (started - created).total_seconds() / 60))
    def pct(name):
        return (concl.count(name) / len(concl) * 100) if concl else None
    return {
        "runs_seen": len(runs),
        "completed": len(done),
        "success_rate": pct("success"),
        "failure_rate": pct("failure"),
        "cancel_rate": pct("cancelled"),
        "rerun_rate": (sum(1 for r in done if (r.get("run_attempt") or 1) > 1) / len(done) * 100) if done else None,
        "median_min": statistics.median(durations) if durations else None,
        "p95_min": (statistics.quantiles(durations, n=20)[-1] if len(durations) >= 20
                    else (max(durations) if durations else None)),
        "p95_exact": len(durations) >= 20,
        "median_queue_min": statistics.median(queue_waits) if queue_waits else None,
    }


def fmt(v, suffix=""):
    return "n/a" if v is None else f"{v:.0f}{suffix}" if suffix == "%" else f"{v:.1f}{suffix}"


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", default="TrigintaFaces/Excalibur")
    ap.add_argument("--workflows", default="ci.yml,quality-gates.yml,committed-content-gates.yml")
    ap.add_argument("--limit", type=int, default=30)
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    rows, any_data = [], False
    for wf in [w.strip() for w in args.workflows.split(",") if w.strip()]:
        runs = fetch_runs(args.repo, wf, args.limit)
        if runs:
            any_data = True
        rows.append((wf, summarise(runs)))

    if not any_data:
        print("::error::REFUSE: no workflow runs retrieved. This is 'no data', not 'CI is healthy'.",
              file=sys.stderr)
        return EXIT_REFUSE

    print(f"## CI service-level indicators - last {args.limit} runs on main")
    print()
    print("| workflow | runs | success | failed | cancelled | rerun | median | p95 | queue |")
    print("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |")
    for wf, s in rows:
        p95 = fmt(s["p95_min"], "m") + ("" if s["p95_exact"] else "*")
        print(f"| `{wf}` | {s['completed']} | {fmt(s['success_rate'],'%')} | {fmt(s['failure_rate'],'%')} | "
              f"{fmt(s['cancel_rate'],'%')} | {fmt(s['rerun_rate'],'%')} | {fmt(s['median_min'],'m')} | "
              f"{p95} | {fmt(s['median_queue_min'],'m')} |")
    print()
    print("`*` p95 shown as the observed maximum: fewer than 20 completed runs in the window, which is "
          "too few for a real 95th percentile. Marked rather than quietly rounded.")
    print()
    print("**Not measured here, on purpose:** infrastructure-failure rate needs a failure "
          "classification this pipeline does not yet produce, and flake rate is the flake report's "
          "job. A number with no measurement behind it is how a dashboard starts lying.")
    return 0


def self_test() -> int:
    """Liveness: real inputs produce the right arithmetic. Safety: no input REFUSES."""
    runs = [
        {"status": "completed", "conclusion": "success", "run_attempt": 1,
         "created_at": "2026-01-01T00:00:00Z", "run_started_at": "2026-01-01T00:02:00Z",
         "updated_at": "2026-01-01T00:12:00Z"},
        {"status": "completed", "conclusion": "failure", "run_attempt": 2,
         "created_at": "2026-01-01T01:00:00Z", "run_started_at": "2026-01-01T01:00:00Z",
         "updated_at": "2026-01-01T01:20:00Z"},
        {"status": "in_progress", "conclusion": None, "run_attempt": 1,
         "created_at": "2026-01-01T02:00:00Z", "run_started_at": None, "updated_at": None},
    ]
    s = summarise(runs)
    checks = [
        ("completed excludes in-progress", s["completed"] == 2),
        ("success rate", abs(s["success_rate"] - 50.0) < 1e-9),
        ("cancel rate is 0, not None", s["cancel_rate"] == 0.0),
        ("rerun rate counts attempt>1", abs(s["rerun_rate"] - 50.0) < 1e-9),
        ("median duration", abs(s["median_min"] - 16.0) < 1e-9),
        ("queue time uses run_started_at", abs(s["median_queue_min"] - 1.0) < 1e-9),
        ("p95 flagged inexact on a small window", s["p95_exact"] is False),
    ]
    bad = [n for n, ok in checks if not ok]
    for n, ok in checks:
        print(f"SELF-TEST: {'PASS' if ok else 'FAIL'} -- {n}")
    if bad:
        print(f"SELF-TEST FAIL: {bad}", file=sys.stderr)
        return 1

    empty = summarise([])
    if empty["success_rate"] is not None or empty["median_min"] is not None:
        print("SELF-TEST FAIL -- an empty window produced numbers instead of n/a", file=sys.stderr)
        return 1
    print("SELF-TEST: PASS -- an empty window yields n/a, never a fabricated rate (safety)")
    print("SELF-TEST: the SLI report is non-vacuous.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
