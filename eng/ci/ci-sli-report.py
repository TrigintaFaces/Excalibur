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

OBJECTIVES
----------
Until they were added, every number above was compared to nothing. The targets existed as prose in a
planning document, which is not a place any artifact can read, so an indicator could drift by any
amount and nothing would say so. They now live in eng/ci/ci-objectives.json and each is scored.

THREE VERDICTS, AND THE THIRD IS THE POINT.

  MET           measured, and inside the objective.
  MISSED        measured, and outside it.
  UNMEASURABLE  no number to compare. Either the window held no runs of that workflow, or the
                pipeline does not produce the quantity the objective names at all.

MISSED and UNMEASURABLE must never print the same. "The official build succeeds 80% of the time"
and "the official build has not run" are both bad and their remedies have nothing in common; a
report that renders them identically sends whoever reads it to fix the wrong thing. The same
distinction is why an empty window REFUSES rather than reporting zeroes.

HISTORY
-------
--history-file appends one dated row per objective per run to a small JSONL series, so a trend can
be read from a single committed file instead of by downloading and diffing nightly artifacts. Rows
are append-only and one line each; nothing is ever rewritten, because a series that can be edited in
place is a series that can be made to say anything.

EXIT CODES
  0  report produced (objectives may be missed; this command reports, it does not gate by default)
  1  an objective was MISSED and --fail-on-missed was passed
  2  REFUSE: no runs retrieved, or the objectives could not be loaded. "No data" and "everything is
     healthy" must never print the same, and neither must "we never checked".
"""
from __future__ import annotations

import argparse
import json
import os
import statistics
import subprocess
import sys
from datetime import datetime, timezone

EXIT_OK = 0
EXIT_MISSED = 1
EXIT_REFUSE = 2

MET, MISSED, UNMEASURABLE = "MET", "MISSED", "UNMEASURABLE"
DEFAULT_OBJECTIVES = os.path.join("eng", "ci", "ci-objectives.json")
DEFAULT_HISTORY = os.path.join("eng", "ci", "ci-sli-history.jsonl")


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


def load_objectives(path: str):
    """Read the objectives policy. A missing or unreadable file REFUSES.

    Returning an empty list instead would let a deleted, renamed, or malformed policy file present
    as 'no objectives were missed' -- a report certifying compliance with a promise it never read.
    """
    try:
        with open(path, encoding="utf-8") as fh:
            doc = json.load(fh)
    except FileNotFoundError:
        print(f"::error::REFUSE: objectives file {path!r} not found. There is nothing to score "
              "against, which is not the same as nothing being missed.", file=sys.stderr)
        return None
    except (OSError, json.JSONDecodeError) as exc:
        print(f"::error::REFUSE: objectives file {path!r} could not be read: {exc}. "
              "An unreadable policy is not an empty one.", file=sys.stderr)
        return None

    objectives = doc.get("objectives")
    if not isinstance(objectives, list) or not objectives:
        print(f"::error::REFUSE: {path!r} declares no objectives. Either the file is wrong or "
              "nothing is promised; both are failures, neither is a pass.", file=sys.stderr)
        return None
    return objectives


def score(objective: dict, summaries: dict):
    """Score one objective against the measured summaries.

    Returns (verdict, observed_value_or_None, reason). `reason` is populated only for
    UNMEASURABLE, and it always says WHICH kind of unmeasurable, because "we do not produce this
    quantity" and "this workflow did not run in the window" need different work to fix.
    """
    metric = objective.get("metric")
    workflow = objective.get("workflow")

    # Unmeasurable by construction: the pipeline does not produce the quantity at all.
    if metric is None:
        return UNMEASURABLE, None, objective.get(
            "unmeasurable-because",
            "declared unmeasurable, but the policy file gives no reason -- fix the policy file")

    summary = summaries.get(workflow)
    if summary is None:
        return UNMEASURABLE, None, f"no run history was retrieved for {workflow}"
    if summary.get("completed", 0) == 0:
        return UNMEASURABLE, None, f"{workflow} had no completed runs in the window"

    observed = summary.get(metric)
    if observed is None:
        return UNMEASURABLE, None, f"{workflow} produced no value for {metric} in this window"

    threshold = objective["value"]
    if objective.get("comparison") == "at-least":
        return (MET if observed >= threshold else MISSED), observed, ""
    return (MET if observed <= threshold else MISSED), observed, ""


def render_objectives(objectives, summaries) -> list:
    """Print the objectives table. Returns the scored rows for the history series."""
    rows = []
    print()
    print("## Objectives")
    print()
    print("| objective | target | observed | verdict |")
    print("| --- | ---: | ---: | :---: |")
    for obj in objectives:
        verdict, observed, reason = score(obj, summaries)
        unit = obj.get("unit", "")
        # ASCII on purpose: this prints to whatever console the caller has, and the Windows default
        # code page cannot encode the maths glyphs. A report that crashes on a developer's terminal
        # is one nobody runs before pushing.
        arrow = "<=" if obj.get("comparison") != "at-least" else ">="
        target = f"{arrow} {obj['value']}{unit}"
        shown = "--" if observed is None else f"{observed:.1f}{unit}"
        print(f"| {obj['description']} | {target} | {shown} | **{verdict}** |")
        rows.append({"id": obj["id"], "verdict": verdict, "observed": observed,
                     "target": obj["value"], "reason": reason})

    unmeasurable = [(o, r) for o, r in zip(objectives, rows) if r["verdict"] == UNMEASURABLE]
    if unmeasurable:
        print()
        print("**Why the unmeasurable ones are unmeasurable** -- this is not a softer MISSED. "
              "Nothing was compared, so nothing can be concluded either way:")
        print()
        for obj, row in unmeasurable:
            print(f"- _{obj['description']}_ -- {row['reason']}")

    missed = [r for r in rows if r["verdict"] == MISSED]
    if missed:
        print()
        print(f"**{len(missed)} objective(s) MISSED.** These were measured and fell short, which is "
              "a different fact from the unmeasurable ones above.")
    return rows


def append_history(path: str, rows: list, repo: str) -> None:
    """Append one dated line per scored objective. Append-only, one JSON object per line.

    Deliberately not a rewritten summary file: a series you can edit in place is a series that can
    be made to say anything, and the whole point of keeping it is to be able to see a number move.
    """
    stamp = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    try:
        os.makedirs(os.path.dirname(path) or ".", exist_ok=True)
        with open(path, "a", encoding="utf-8", newline="\n") as fh:
            for row in rows:
                fh.write(json.dumps({
                    "at": stamp,
                    "repo": repo,
                    "objective": row["id"],
                    "verdict": row["verdict"],
                    "observed": row["observed"],
                    "target": row["target"],
                }, sort_keys=True) + "\n")
    except OSError as exc:
        # A history write failure must not destroy the report that was already produced. It is
        # surfaced, not swallowed, and not escalated to a failure of the measurement itself.
        print(f"::warning::could not append to history file {path!r}: {exc}", file=sys.stderr)
        return
    print()
    print(f"_Appended {len(rows)} row(s) to `{path}`._")


def render_trend(path: str, limit: int = 8) -> None:
    """Show how each objective's verdict has moved, read from the committed series alone."""
    try:
        with open(path, encoding="utf-8") as fh:
            entries = [json.loads(line) for line in fh if line.strip()]
    except (OSError, json.JSONDecodeError):
        return
    if len(entries) <= 1:
        return

    by_id = {}
    for e in entries:
        by_id.setdefault(e.get("objective"), []).append(e)

    print()
    print(f"### Trend (most recent {limit} observations per objective)")
    print()
    print("| objective | verdicts, oldest to newest |")
    print("| --- | --- |")
    for oid, es in by_id.items():
        marks = []
        for e in es[-limit:]:
            v = e.get("verdict")
            marks.append("O" if v == MET else ("X" if v == MISSED else "?"))
        print(f"| `{oid}` | {' '.join(marks)} |")
    print()
    print("`O` met &nbsp; `X` missed &nbsp; `?` unmeasurable")


def main() -> int:
    ap = argparse.ArgumentParser()
    # NO DEFAULT, deliberately. This repository's own Actions do not execute -- it still carries
    # thousands of historical runs from before they were disabled -- while the workflows actually
    # run in a separate public repository. A hardcoded default therefore picks a repository for the
    # caller, and both available choices are wrong in a way that is invisible in the output: one
    # scores objectives over runs that no longer happen, the other reaches for a repository local
    # work has no business querying. Either produces a confident, well-formed, meaningless verdict.
    # In CI, GITHUB_REPOSITORY is correct by construction; anywhere else, refusing beats guessing.
    ap.add_argument("--repo", default=os.environ.get("GITHUB_REPOSITORY"))
    ap.add_argument("--workflows", default="ci.yml,quality-gates.yml,committed-content-gates.yml")
    ap.add_argument("--limit", type=int, default=30)
    ap.add_argument("--objectives", default=DEFAULT_OBJECTIVES,
                    help="objectives policy file (JSON)")
    ap.add_argument("--history-file", default=None,
                    help=f"append a dated row per objective (e.g. {DEFAULT_HISTORY})")
    ap.add_argument("--fail-on-missed", action="store_true",
                    help="exit 1 when an objective is MISSED (advisory by default)")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    if not args.repo:
        print("::error::REFUSE: no repository given. Pass --repo, or set GITHUB_REPOSITORY. "
              "Measuring the wrong repository produces a well-formed verdict about a surface "
              "nobody asked about, which is worse than no verdict at all.", file=sys.stderr)
        return EXIT_REFUSE

    objectives = load_objectives(args.objectives)
    if objectives is None:
        return EXIT_REFUSE

    # Every workflow an objective names is fetched, whether or not it appears in --workflows.
    # Otherwise an objective could be UNMEASURABLE purely because nobody remembered to list its
    # subject, and that reads identically to the workflow having stopped running.
    wanted = [w.strip() for w in args.workflows.split(",") if w.strip()]
    for obj in objectives:
        wf = obj.get("workflow")
        if wf and wf not in wanted:
            wanted.append(wf)

    rows, any_data, summaries = [], False, {}
    for wf in wanted:
        runs = fetch_runs(args.repo, wf, args.limit)
        if runs:
            any_data = True
        s = summarise(runs)
        summaries[wf] = s
        rows.append((wf, s))

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

    scored = render_objectives(objectives, summaries)

    if args.history_file:
        append_history(args.history_file, scored, args.repo)
        render_trend(args.history_file)

    if args.fail_on_missed and any(r["verdict"] == MISSED for r in scored):
        return EXIT_MISSED
    return EXIT_OK


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

    # ---- objective scoring ----
    summaries = {
        "fast.yml": {"completed": 5, "p95_min": 12.0, "success_rate": 99.0},
        "slow.yml": {"completed": 5, "p95_min": 44.0, "success_rate": 80.0},
        "silent.yml": {"completed": 0, "p95_min": None, "success_rate": None},
    }
    at_most = {"id": "x", "workflow": "fast.yml", "metric": "p95_min",
               "comparison": "at-most", "value": 20}
    at_least = {"id": "y", "workflow": "fast.yml", "metric": "success_rate",
                "comparison": "at-least", "value": 95}

    cases = [
        ("an at-most objective inside its target is MET", at_most, MET),
        ("an at-most objective outside its target is MISSED",
         {**at_most, "workflow": "slow.yml"}, MISSED),
        ("an at-least objective above its target is MET", at_least, MET),
        ("an at-least objective below its target is MISSED",
         {**at_least, "workflow": "slow.yml"}, MISSED),
        # Boundary. Exactly on target is inside it; an off-by-one here reports a miss for the
        # performance the objective was written to accept.
        ("a value exactly ON an at-most target is MET",
         {**at_most, "value": 12}, MET),
        ("a value exactly ON an at-least target is MET",
         {**at_least, "value": 99}, MET),
        # THE ARM THAT MATTERS. An empty window must not score, in either direction.
        ("a workflow with no completed runs is UNMEASURABLE, not MISSED",
         {**at_most, "workflow": "silent.yml"}, UNMEASURABLE),
        ("a workflow absent from the window entirely is UNMEASURABLE",
         {**at_most, "workflow": "never-fetched.yml"}, UNMEASURABLE),
        ("a metric this pipeline does not produce is UNMEASURABLE",
         {**at_most, "metric": None, "unmeasurable-because": "no classification exists"},
         UNMEASURABLE),
    ]
    for desc, obj, want in cases:
        verdict, _, reason = score(obj, summaries)
        if verdict != want:
            print(f"SELF-TEST FAIL -- {desc}: got {verdict}, expected {want}", file=sys.stderr)
            return 1
        if want == UNMEASURABLE and not reason:
            print(f"SELF-TEST FAIL -- {desc}: UNMEASURABLE with no reason given. A verdict that "
                  "does not say what is missing cannot be acted on.", file=sys.stderr)
            return 1
        print(f"SELF-TEST: PASS -- {desc}")

    # A missing or empty policy REFUSES rather than scoring nothing and reporting no misses.
    import tempfile
    if load_objectives(os.path.join(tempfile.mkdtemp(), "absent.json")) is not None:
        print("SELF-TEST FAIL -- a missing objectives file did not REFUSE", file=sys.stderr)
        return 1
    print("SELF-TEST: PASS -- a missing objectives policy REFUSES rather than scoring zero misses")

    # The shipped policy must actually load. A self-test that only ever reads its own fixtures
    # cannot notice that the file the report will really open is malformed.
    if os.path.exists(DEFAULT_OBJECTIVES):
        if load_objectives(DEFAULT_OBJECTIVES) is None:
            print(f"SELF-TEST FAIL -- the shipped {DEFAULT_OBJECTIVES} does not load",
                  file=sys.stderr)
            return 1
        print(f"SELF-TEST: PASS -- the shipped {DEFAULT_OBJECTIVES} loads")

    print("SELF-TEST: the SLI report is non-vacuous.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
