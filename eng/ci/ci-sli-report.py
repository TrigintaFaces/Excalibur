#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
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
Reported: latest-conclusion success/cancellation rates, rerun rate, created-to-updated
workflow duration, and created-to-run-start queue proxy. Named-job objectives use validated
attempt-specific verdict completion instead. Optional job details report observed interval
occupancy; they do not establish billed cost or the dependency critical path.

NOT reported: infrastructure failure rate requires classified rerun evidence, and flake rate
is the flake report's job. Printing a plausible
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
--history-file appends one dated row per objective to the supplied JSONL series. Repository,
population and policy identities keep incomparable observations separate. Nightly uploads its
copy as an artifact; it does not commit or automatically merge histories from previous runs.

EXIT CODES
  0  report produced (objectives may be missed; this command reports, it does not gate by default)
  1  an objective was MISSED and --fail-on-missed was passed
  2  REFUSE: absent, inconsistent or incomplete API evidence, exhausted collection budget, or invalid
     objectives. "No data" and "everything is
     healthy" must never print the same, and neither must "we never checked".
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import os
from pathlib import Path
import statistics
import subprocess
import sys
import time
from datetime import datetime, timezone

EXIT_OK = 0
EXIT_MISSED = 1
EXIT_REFUSE = 2

MET, MISSED, UNMEASURABLE = "MET", "MISSED", "UNMEASURABLE"
DEFAULT_OBJECTIVES = os.path.join("eng", "ci", "ci-objectives.json")
DEFAULT_HISTORY = os.path.join("eng", "ci", "ci-sli-history.jsonl")
API_DEADLINE = None


def api_call(command):
    remaining = 60 if API_DEADLINE is None else API_DEADLINE - time.monotonic()
    if remaining <= 0:
        raise ValueError('collection deadline exhausted; retain partial evidence for diagnosis')
    return subprocess.run(command, capture_output=True, text=True, timeout=min(60, remaining))


def _iso(s):
    if not s:
        return None
    try:
        value = datetime.fromisoformat(s.replace("Z", "+00:00"))
        return value if value.tzinfo is not None else None
    except (ValueError, AttributeError, TypeError):
        return None


def population(workflow, event=None, branch=None):
    """Branch means the run's head branch, never the PR's base branch."""
    return workflow, event, branch


def scope_label(scope):
    workflow, event, branch = scope
    return f"{workflow} (event={event or 'all'}, head-branch={branch or 'all'})"


def fetch_runs(repo: str, workflow: str, limit: int, event=None, branch=None) -> list[dict]:
    """Retrieve a bounded population; partial or mismatched evidence refuses scoring."""
    if not 1 <= limit <= 1000:
        raise ValueError('run limit must be between 1 and 1000')
    runs, seen = [], set()
    size = min(limit, 100)
    for page in range(1, (limit + size - 1) // size + 1):
        cmd = ["gh", "api", "-X", "GET",
               f"repos/{repo}/actions/workflows/{workflow}/runs",
               "-f", f"per_page={size}", "-f", f"page={page}", "--jq", ".workflow_runs"]
        for key, value in (("event", event), ("branch", branch)):
            if value is not None:
                cmd.extend(["-f", f"{key}={value}"])
        try:
            out = api_call(cmd)
            if out.returncode:
                raise ValueError(f'gh exited {out.returncode}: {out.stderr.strip()[:120]}')
            batch = json.loads(out.stdout)
            if not isinstance(batch, list):
                raise ValueError('run response is not an array')
            for run in batch:
                if not isinstance(run, dict) or not isinstance(run.get('id'), int):
                    raise ValueError('run identity missing')
                if run['id'] in seen:
                    raise ValueError('duplicate run across pages; retry with a stable window')
                if run.get('repository', {}).get('full_name', '').lower() != repo.lower():
                    raise ValueError('run repository differs from requested population')
                if run.get('path', '').split('@', 1)[0] != f'.github/workflows/{workflow}':
                    raise ValueError('run workflow differs from requested population')
                if event is not None and run.get('event') != event:
                    raise ValueError('run event differs from requested population')
                if branch is not None and run.get('head_branch') != branch:
                    raise ValueError('run head branch differs from requested population')
                seen.add(run['id'])
            runs.extend(batch)
            if len(batch) < size or len(runs) >= limit:
                return runs[:limit]
        except (OSError, subprocess.SubprocessError, ValueError) as exc:
            raise ValueError(f'{scope_label(population(workflow, event, branch))}: {exc}') from exc
    return runs[:limit]


def summarise(runs: list[dict]) -> dict:
    done = [r for r in runs if r.get("status") == "completed"]
    concl = [r.get("conclusion") for r in done]
    durations, queue_waits = [], []
    for r in done:
        created, started, updated = _iso(r.get("created_at")), _iso(r.get("run_started_at")), _iso(r.get("updated_at"))
        if created and started and updated and created <= started <= updated:
            durations.append((updated - created).total_seconds() / 60)
        if created and started and started >= created:
            queue_waits.append((started - created).total_seconds() / 60)
    def pct(name):
        return (concl.count(name) / len(concl) * 100) if concl else None
    return {
        "runs_seen": len(runs),
        "completed": len(done),
        "duration_samples": len(durations),
        "queue_samples": len(queue_waits),
        "run_identities": [{'id': r.get('id'), 'attempt': r.get('run_attempt')} for r in runs],
        "success_rate": pct("success"),
        "failure_rate": pct("failure"),
        "cancel_rate": pct("cancelled"),
        "rerun_rate": (sum(1 for r in done if (r.get("run_attempt") or 1) > 1) / len(done) * 100) if done else None,
        "median_min": statistics.median(durations) if durations else None,
        "p95_min": (sorted(durations)[math.ceil(0.95 * len(durations)) - 1] if len(durations) >= 20
                    else (max(durations) if durations else None)),
        "p95_exact": len(durations) >= 20,
        "median_queue_min": statistics.median(queue_waits) if queue_waits else None,
    }


def fetch_attempt_jobs(repo, run):
    """Never mix successful jobs retained from a previous run attempt into this attempt."""
    run_id, attempt, sha = run.get('id'), run.get('run_attempt'), run.get('head_sha')
    if type(run_id) is not int or type(attempt) is not int or attempt < 1 or not isinstance(sha, str) or not sha:
        raise ValueError('run identity, attempt or source SHA missing for job measurement')
    jobs, seen = [], set()
    for page in range(1, 101):
        cmd = ['gh', 'api', '-X', 'GET', f'repos/{repo}/actions/runs/{run_id}/attempts/{attempt}/jobs',
               '-f', 'per_page=100', '-f', f'page={page}', '--jq', '.jobs']
        try:
            result = api_call(cmd)
            if result.returncode:
                raise ValueError(f'jobs API exited {result.returncode}')
            batch = json.loads(result.stdout)
            if not isinstance(batch, list):
                raise ValueError('jobs response is not an array')
            for job in batch:
                if not isinstance(job, dict) or type(job.get('id')) is not int or job['id'] in seen:
                    raise ValueError('missing or duplicate job identity')
                if job.get('run_id') != run_id or job.get('head_sha') != sha:
                    raise ValueError('job belongs to another run or source SHA')
                if 'run_attempt' in job and job['run_attempt'] != attempt:
                    raise ValueError('job belongs to another attempt')
                seen.add(job['id'])
            jobs.extend(batch)
            if len(batch) < 100:
                current = api_call(['gh', 'api', f'repos/{repo}/actions/runs/{run_id}'])
                if current.returncode:
                    raise ValueError('cannot revalidate run after job collection')
                current_run = json.loads(current.stdout)
                if not isinstance(current_run, dict) or any(current_run.get(key) != run.get(key)
                       for key in ('id', 'head_sha', 'run_attempt', 'status', 'conclusion', 'updated_at')):
                    raise ValueError('run changed during job collection; retry the measurement')
                return jobs
        except (OSError, subprocess.SubprocessError, ValueError) as exc:
            raise ValueError(f'run {run_id} attempt {attempt}: {exc}') from exc
    raise ValueError('job pagination bound exceeded; incomplete evidence')


def summarise_job(runs, name, verdict_step):
    """Elapsed time until the named aggregate emits a verdict, not until the run is updated."""
    observations, durations = [], []
    for run in runs:
        found = [job for job in run.get('_jobs', []) if job.get('name') == name]
        observation = {'run_id': run['id'], 'attempt': run['run_attempt'], 'head_sha': run['head_sha'],
                       'created_at': run.get('created_at'), 'status': run.get('status'), 'job_name': name, 'jobs': found}
        observations.append(observation)
        if run.get('status') != 'completed':
            observation['missing_because'] = 'run has not completed; latency is censored'
            continue
        if len(found) != 1:
            observation['missing_because'] = 'named job missing or ambiguous'
            continue
        job = found[0]
        steps = [step for step in job.get('steps', []) if step.get('name') == verdict_step]
        if len(steps) != 1 or steps[0].get('status') != 'completed' or steps[0].get('conclusion') not in ('success', 'failure'):
            observation['missing_because'] = 'decisive verdict step did not complete'
            continue
        created, started, completed = _iso(run.get('created_at')), _iso(job.get('started_at')), _iso(job.get('completed_at'))
        if (job.get('status') != 'completed' or job.get('conclusion') not in ('success', 'failure')
                or not created or not started or not completed or not created <= started <= completed):
            observation['missing_because'] = 'job did not emit a timed success/failure verdict'
            continue
        duration = (completed - created).total_seconds() / 60
        observation['elapsed_min'] = duration
        durations.append(duration)
    return {'completed': sum(r.get('status') == 'completed' for r in runs),
            'population_size': len(runs), 'pending_runs': sum(r.get('status') != 'completed' for r in runs),
            'duration_samples': len(durations),
            'successful_jobs': sum(1 for o in observations if 'elapsed_min' in o and o['jobs'][0]['conclusion'] == 'success'),
            'failed_jobs': sum(1 for o in observations if 'elapsed_min' in o and o['jobs'][0]['conclusion'] == 'failure'),
            'job_p95_min': sorted(durations)[math.ceil(.95 * len(durations)) - 1] if durations else None,
            'observations': observations}


def fmt(v, suffix=""):
    return "n/a" if v is None else f"{v:.0f}{suffix}" if suffix == "%" else f"{v:.1f}{suffix}"


def runner_usage(run):
    """Observed selected-attempt job occupancy, not billable time or dependency critical path."""
    result = {'run_id': run.get('id'), 'attempt': run.get('run_attempt'), 'runner_minutes': None,
              'busy_wall_minutes': None, 'peak_jobs': None, 'skipped_jobs': 0, 'jobs': [], 'missing': []}
    jobs = run.get('_jobs')
    if run.get('status') != 'completed' or not isinstance(jobs, list) or not jobs:
        result['missing'].append('completed run and nonempty job evidence required')
        return result
    intervals = []
    created = _iso(run.get('created_at'))
    for job in jobs:
        if job.get('conclusion') == 'skipped':
            start, end = _iso(job.get('started_at')), _iso(job.get('completed_at'))
            if (job.get('status') != 'completed' or (start and end and end != start)
                    or bool(start) != bool(end)
                    or (job.get('started_at') is not None and start is None)
                    or (job.get('completed_at') is not None and end is None)
                    or job.get('steps') != []):
                result['missing'].append(f"job {job.get('id')}: skipped status conflicts with execution evidence")
                continue
            result['skipped_jobs'] += 1
            continue
        start, end = _iso(job.get('started_at')), _iso(job.get('completed_at'))
        if job.get('status') != 'completed' or not created or not start or not end or not created <= start <= end:
            result['missing'].append(f"job {job.get('id')}: missing or invalid interval")
            continue
        minutes = (end - start).total_seconds() / 60
        result['jobs'].append({'id': job['id'], 'name': job.get('name'), 'minutes': minutes,
                               'started_at': job['started_at'], 'completed_at': job['completed_at'],
                               'labels': job.get('labels', [])})
        intervals.append((start, end))
    result['jobs'].sort(key=lambda j: (-j['minutes'], j['id']))
    if result['missing']:
        return result
    # Half-open intervals: a job ending exactly when another starts does not overlap it.
    events = sorted([(start, 1) for start, end in intervals if start < end]
                    + [(end, -1) for start, end in intervals if start < end])
    active, peak, busy, previous = 0, 0, 0.0, None
    for stamp, delta in events:
        if previous is not None and active:
            busy += (stamp - previous).total_seconds() / 60
        active += delta
        peak = max(peak, active)
        previous = stamp
    result.update(runner_minutes=sum(j['minutes'] for j in result['jobs']), busy_wall_minutes=busy, peak_jobs=peak)
    return result


def save_evidence(path, evidence):
    if path is None:
        return
    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    pending = target.with_name(target.name + '.tmp')
    pending.write_text(json.dumps(evidence, indent=2, sort_keys=True) + '\n', encoding='utf-8')
    pending.replace(target)


def load_rerun_classifications(path):
    """Reviewed attribution assertions with provenance, not automated authentication of reviewers."""
    if path is None:
        return {'records': [], 'sha256': None}
    try:
        raw = Path(path).read_bytes()
        document = json.loads(raw)
        if (not isinstance(document, dict) or type(document.get('version')) is not int
                or document['version'] != 1 or not isinstance(document.get('records'), list)):
            raise ValueError('expected version 1 rerun classification records')
        seen = set()
        for record in document['records']:
            if not isinstance(record, dict):
                raise ValueError('classification must be an object')
            repo, sha = record.get('repository'), record.get('head_sha')
            if (not isinstance(repo, str) or not re.fullmatch(r'[^/\s]+/[^/\s]+', repo)
                    or not isinstance(sha, str) or not re.fullmatch(r'[0-9a-f]{40}', sha)
                    or type(record.get('run_id')) is not int or record['run_id'] <= 0
                    or type(record.get('attempt')) is not int or record['attempt'] < 2
                    or record.get('cause') not in ('infrastructure', 'product', 'operator', 'unknown')
                    or any(not isinstance(record.get(key), str) or not record[key].strip()
                           for key in ('rationale', 'evidence', 'reviewer'))
                    or _iso(record.get('reviewed_at')) is None):
                raise ValueError('invalid rerun classification identity, cause or review provenance')
            key = (repo.lower(), record['run_id'], record['attempt'])
            if key in seen:
                raise ValueError('duplicate rerun classification')
            seen.add(key)
        return {'records': document['records'], 'sha256': hashlib.sha256(raw).hexdigest()}
    except (OSError, UnicodeError, ValueError) as exc:
        raise ValueError(f'cannot load rerun classifications: {exc}') from exc


def rerun_incidence(repo, runs, classifications):
    """Strict policy: every observed transition must be reviewed before scoring the population."""
    result = {'denominator': len(runs), 'infrastructure_runs': 0, 'expected_transitions': 0,
              'classified_transitions': 0, 'unknown_runs': 0, 'pending_runs': 0,
              'infrastructure_rerun_rate': None, 'observations': []}
    for run in runs:
        attempt = run.get('run_attempt')
        if type(attempt) is not int or attempt < 1:
            raise ValueError('rerun incidence requires a positive integer attempt for every selected run')
        records = [r for r in classifications['records'] if r['repository'].lower() == repo.lower() and r['run_id'] == run['id']]
        if any(r['head_sha'] != run.get('head_sha') or r['attempt'] > attempt for r in records):
            raise ValueError('rerun classification has stale SHA or exceeds the observed attempt')
        indexed = {r['attempt']: r for r in records}
        missing = [number for number in range(2, attempt + 1)
                   if number not in indexed or indexed[number]['cause'] == 'unknown']
        infrastructure = any(r['cause'] == 'infrastructure' for r in records)
        pending = run.get('status') != 'completed'
        result['expected_transitions'] += attempt - 1
        result['classified_transitions'] += attempt - 1 - len(missing)
        result['infrastructure_runs'] += infrastructure
        result['unknown_runs'] += bool(missing)
        result['pending_runs'] += pending
        result['observations'].append({'run_id': run['id'], 'head_sha': run.get('head_sha'), 'attempt': attempt,
                                       'records': records, 'unclassified_attempts': missing, 'pending': pending})
    if runs and not result['unknown_runs'] and not result['pending_runs']:
        result['infrastructure_rerun_rate'] = result['infrastructure_runs'] / len(runs) * 100
    return result


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

    objectives = doc.get("objectives") if isinstance(doc, dict) else None
    if not isinstance(objectives, list) or not objectives:
        print(f"::error::REFUSE: {path!r} declares no objectives. Either the file is wrong or "
              "nothing is promised; both are failures, neither is a pass.", file=sys.stderr)
        return None
    identities, job_contracts = set(), {}
    metrics = {'p95_min', 'job_p95_min', 'median_min', 'median_queue_min', 'success_rate', 'failure_rate', 'cancel_rate', 'rerun_rate', 'infrastructure_rerun_rate'}
    for obj in objectives:
        if not isinstance(obj, dict):
            print('::error::REFUSE: objective must be an object', file=sys.stderr)
            return None
        identity, value = obj.get('id'), obj.get('value')
        valid = (isinstance(identity, str) and bool(identity) and identity not in identities
                 and obj.get('comparison') in ('at-most', 'at-least', 'less-than')
                 and isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)
                 and (obj.get('metric') is None or obj.get('metric') in metrics)
                 and isinstance(obj.get('description'), str)
                 and (obj.get('metric') is None or isinstance(obj.get('workflow'), str))
                 and (obj.get('metric') != 'job_p95_min' or isinstance(obj.get('job'), str) and bool(obj['job']))
                 and (obj.get('metric') != 'job_p95_min' or isinstance(obj.get('verdict_step'), str) and bool(obj['verdict_step']))
                 and all(obj.get(key) is None or isinstance(obj.get(key), str) and bool(obj[key]) for key in ('event', 'branch')))
        if not valid:
            print(f'::error::REFUSE: invalid or duplicate objective {identity!r}', file=sys.stderr)
            return None
        identities.add(identity)
        if obj.get('metric') == 'job_p95_min':
            key = (obj['workflow'], obj.get('event'), obj.get('branch'), obj['job'])
            if key in job_contracts and job_contracts[key] != obj['verdict_step']:
                print('::error::REFUSE: conflicting verdict steps for the same job population', file=sys.stderr)
                return None
            job_contracts[key] = obj['verdict_step']
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

    scope = population(workflow, objective.get('event'), objective.get('branch'))
    summary = summaries.get(scope)
    # Legacy callers may supply workflow-only summaries, but never use those to score a scoped objective.
    if summary is None and 'event' not in objective and 'branch' not in objective:
        summary = summaries.get(workflow)
    if summary is not None and metric == 'job_p95_min':
        summary = summary.get('job_measurements', {}).get(objective.get('job'))
    if summary is None:
        return UNMEASURABLE, None, f"no run history was retrieved for {workflow}"
    if metric == 'job_p95_min' and summary.get('pending_runs', 0):
        return UNMEASURABLE, None, 'selected runs are still pending; completed-only timings cannot score the full population'
    if summary.get("completed", 0) == 0:
        return UNMEASURABLE, None, f"{workflow} had no completed runs in the window"

    observed = summary.get(metric)
    if observed is None:
        return UNMEASURABLE, None, f"{workflow} produced no value for {metric} in this window"
    if metric in ('p95_min', 'job_p95_min') and summary.get('duration_samples', 0) < 20:
        return UNMEASURABLE, None, 'p95 requires at least 20 valid durations; small-window maximum is descriptive only'
    if metric in ('p95_min', 'job_p95_min', 'median_min') and summary.get('duration_samples') != summary.get('completed'):
        return UNMEASURABLE, None, 'completed runs have missing or invalid duration evidence'
    if metric == 'median_queue_min' and summary.get('queue_samples') != summary.get('completed'):
        return UNMEASURABLE, None, 'completed runs have missing or invalid queue evidence'

    threshold = objective["value"]
    if objective.get('comparison') == 'less-than':
        return (MET if observed < threshold else MISSED), observed, ''
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
        arrow = '<' if obj.get('comparison') == 'less-than' else ("<=" if obj.get("comparison") != "at-least" else ">=")
        target = f"{arrow} {obj['value']}{unit}"
        shown = "--" if observed is None else f"{observed:.1f}{unit}"
        print(f"| {obj['description']} | {target} | {shown} | **{verdict}** |")
        rows.append({"id": obj["id"], "verdict": verdict, "observed": observed,
                     "target": obj["value"], "reason": reason,
                     "population": {'workflow': obj.get('workflow'), 'event': obj.get('event'),
                                    'head_branch': obj.get('branch')},
                     "measurement_version": 2,
                     "objective_policy": obj,
                     "policy_sha256": hashlib.sha256(json.dumps(obj, sort_keys=True, separators=(',', ':')).encode()).hexdigest(),
                     "evidence": summaries.get(population(obj.get('workflow'), obj.get('event'), obj.get('branch')), {})})

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
                    "population": row['population'],
                    "reason": row['reason'],
                    "measurement_version": row['measurement_version'],
                    "objective_policy": row['objective_policy'],
                    "policy_sha256": row['policy_sha256'],
                    "evidence": row['evidence'],
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
        key = (e.get('objective'), e.get('measurement_version', 1),
               json.dumps(e.get('population', 'legacy-main-only'), sort_keys=True),
               e.get('repo', 'legacy-unknown-repository'), e.get('policy_sha256', 'legacy-unknown-policy'))
        by_id.setdefault(key, []).append(e)

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
        print(f"| `{oid[0]}` v{oid[1]} {oid[2]} repo={oid[3]} policy={oid[4]} | {' '.join(marks)} |")
    print()
    print("`O` met &nbsp; `X` missed &nbsp; `?` unmeasurable")


def main() -> int:
    global API_DEADLINE
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
    ap.add_argument('--job-details', action='store_true', help='collect observed runner occupancy for selected attempts')
    ap.add_argument('--evidence-file', help='retain run/job inputs, measurement identity and collection state as JSON')
    ap.add_argument('--rerun-classifications', help='reviewed per-attempt cause assertions with evidence provenance')
    ap.add_argument('--collection-timeout-seconds', type=int, default=480,
                    help='API collection deadline; leave runner time for evidence upload (default: 480)')
    ap.add_argument("--fail-on-missed", action="store_true",
                    help="exit 1 when an objective is MISSED (advisory by default)")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()

    if args.self_test:
        return self_test()
    if args.collection_timeout_seconds <= 0:
        print('::error::REFUSE: collection timeout must be positive', file=sys.stderr)
        return EXIT_REFUSE
    API_DEADLINE = time.monotonic() + args.collection_timeout_seconds

    if not 1 <= args.limit <= 1000:
        print('::error::REFUSE: --limit must be between 1 and 1000', file=sys.stderr)
        return EXIT_REFUSE

    if not args.repo:
        print("::error::REFUSE: no repository given. Pass --repo, or set GITHUB_REPOSITORY. "
              "Measuring the wrong repository produces a well-formed verdict about a surface "
              "nobody asked about, which is worse than no verdict at all.", file=sys.stderr)
        return EXIT_REFUSE

    objectives = load_objectives(args.objectives)
    if objectives is None:
        return EXIT_REFUSE
    try:
        classifications = load_rerun_classifications(args.rerun_classifications)
    except ValueError as exc:
        print(f'::error::REFUSE: {exc}', file=sys.stderr)
        return EXIT_REFUSE

    # Every workflow an objective names is fetched, whether or not it appears in --workflows.
    # Otherwise an objective could be UNMEASURABLE purely because nobody remembered to list its
    # subject, and that reads identically to the workflow having stopped running.
    wanted = [population(w.strip(), 'push', 'main') for w in args.workflows.split(",") if w.strip()]
    for obj in objectives:
        wf = obj.get("workflow")
        scope = population(wf, obj.get('event'), obj.get('branch'))
        if wf and obj.get('metric') is not None and scope not in wanted:
            wanted.append(scope)

    rows, any_data, summaries = [], False, {}
    evidence = {'schema': 1, 'repository': args.repo, 'requested_limit': args.limit,
                'collection_timeout_seconds': args.collection_timeout_seconds,
                'started_at': datetime.now(timezone.utc).isoformat(), 'status': 'collecting',
                'report_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                'rerun_classifications': classifications,
                'objectives': objectives, 'populations': []}
    save_evidence(args.evidence_file, evidence)
    for scope in wanted:
        wf, event, branch = scope
        try:
            runs = fetch_runs(args.repo, wf, args.limit, event, branch)
        except ValueError as exc:
            evidence.update(status='refused', reason=str(exc))
            save_evidence(args.evidence_file, evidence)
            print(f'::error::REFUSE: {exc}', file=sys.stderr)
            return EXIT_REFUSE
        if runs:
            any_data = True
        record = {'workflow': wf, 'event': event, 'head_branch': branch, 'runs': runs}
        evidence['populations'].append(record)
        try:
            attribution = rerun_incidence(args.repo, runs, classifications)
        except ValueError as exc:
            evidence.update(status='refused', reason=str(exc))
            save_evidence(args.evidence_file, evidence)
            print(f'::error::REFUSE: {exc}', file=sys.stderr)
            return EXIT_REFUSE
        s = summarise(runs)
        s['rerun_attribution'] = attribution
        s['infrastructure_rerun_rate'] = s['rerun_attribution']['infrastructure_rerun_rate']
        jobs = {obj['job']: obj['verdict_step'] for obj in objectives if obj.get('metric') == 'job_p95_min'
                and population(obj.get('workflow'), obj.get('event'), obj.get('branch')) == scope}
        if jobs or args.job_details:
            try:
                for run in runs:
                    if run.get('status') == 'completed':
                        run['_jobs'] = fetch_attempt_jobs(args.repo, run)
                        save_evidence(args.evidence_file, evidence)
            except ValueError as exc:
                evidence.update(status='refused', reason=str(exc))
                save_evidence(args.evidence_file, evidence)
                print(f'::error::REFUSE: {exc}', file=sys.stderr)
                return EXIT_REFUSE
            s['job_measurements'] = {name: summarise_job(runs, name, jobs[name]) for name in sorted(jobs)}
        if args.job_details:
            s['runner_usage'] = [runner_usage(run) for run in runs]
        s['requested_limit'] = args.limit
        s['retrieved_at'] = datetime.now(timezone.utc).isoformat()
        record['summary'] = s
        save_evidence(args.evidence_file, evidence)
        summaries[scope] = s
        rows.append((scope_label(scope), s))

    if not any_data:
        evidence.update(status='refused', reason='no workflow runs retrieved')
        save_evidence(args.evidence_file, evidence)
        print("::error::REFUSE: no workflow runs retrieved. This is 'no data', not 'CI is healthy'.",
              file=sys.stderr)
        return EXIT_REFUSE

    evidence.update(status='collected', completed_at=datetime.now(timezone.utc).isoformat())
    save_evidence(args.evidence_file, evidence)

    print(f"## CI service-level indicators - up to {args.limit} latest runs per explicit population")
    print('\nDurations are logical-run created-to-updated proxies, including rerun delays; rates use latest conclusions. '
          'These are not per-attempt execution times or first-attempt reliability. p95 uses the empirical nearest-rank estimator with at least 20 valid durations.')
    print('Named-job objectives instead use run creation to the named job completion in the selected attempt. '
          'The decisive verdict step must execute; latency is not a claim that validation passed. '
          'This does not establish the live branch-protection check set or a dependency critical path.')
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
    for scope, summary in summaries.items():
        for name, measurement in summary.get('job_measurements', {}).items():
            print(f"- {scope_label(scope)} / {name}: {measurement['duration_samples']}/{measurement['population_size']} "
                  f"timed verdicts, {measurement['successful_jobs']} successful jobs, {measurement['failed_jobs']} failed jobs, "
                  f"{measurement['pending_runs']} pending runs.")
    print('\nRerun incidence uses reviewed cause assertions, not automatic diagnosis. The strict scoring policy requires '
          'every transition to be classified even if another transition already establishes infrastructure attribution.\n')
    for scope, summary in summaries.items():
        attribution = summary['rerun_attribution']
        print(f"- {scope_label(scope)}: {attribution['infrastructure_runs']}/{attribution['denominator']} runs have infrastructure attribution; "
              f"{attribution['classified_transitions']}/{attribution['expected_transitions']} retry transitions classified, "
              f"{attribution['unknown_runs']} runs with unknown causes, {attribution['pending_runs']} pending runs.")

    scored = render_objectives(objectives, summaries)

    if args.job_details:
        print('\n## Observed runner occupancy (selected attempts only)\n')
        print('Job elapsed time excludes prior attempts and is not billed cost. Busy wall time is the union of job intervals, '
              'not the dependency critical path. Missing timing makes totals unknown.\n')
        print('| population / run / attempt | runner minutes | busy wall minutes | peak jobs | skipped |')
        print('| --- | ---: | ---: | ---: | ---: |')
        for scope, summary in summaries.items():
            for usage in summary.get('runner_usage', []):
                print(f"| {scope_label(scope)} / {usage['run_id']} / {usage['attempt']} | {fmt(usage['runner_minutes'])} | "
                      f"{fmt(usage['busy_wall_minutes'])} | {usage['peak_jobs'] if usage['peak_jobs'] is not None else 'n/a'} | {usage['skipped_jobs']} |")
        print('\nLongest observed jobs per selected run (full intervals are retained in the evidence):\n')
        for scope, summary in summaries.items():
            for usage in summary.get('runner_usage', []):
                for job in usage['jobs'][:3]:
                    print(f"- Run {usage['run_id']} job {job['id']} ({job['name']}): {job['minutes']:.2f} minutes.")

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
        "fast.yml": {"completed": 20, "duration_samples": 20, "p95_min": 12.0, "success_rate": 99.0},
        "slow.yml": {"completed": 20, "duration_samples": 20, "p95_min": 44.0, "success_rate": 80.0},
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
