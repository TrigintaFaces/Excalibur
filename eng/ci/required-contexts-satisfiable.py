#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0
"""Every required status check must be SATISFIABLE.

WHY
---
A required context that nothing can satisfy does not fail a pull request -- it leaves it
"Expected - waiting for status to be reported", forever, with every check green and the merge
button greyed out. Nothing is running, so there is nothing to re-run, and the pipeline looks
broken rather than misconfigured. It is the most expensive failure mode available, because the
signal points away from the cause.

Four distinct instances of this shipped here, and each hid behind the one in front of it:

  1. a required context named  "Continuous Integration / Release-Blocking CI Governance"
     while the check is named  "Release-Blocking CI Governance"
     -- a check run is named after the JOB. "Workflow / Job" is a display convention.
  2. a required context named  "CodeQL Security Analysis / Analyze C# Code"
     while the check is named  "Analyze C# Code (csharp)"
     -- the matrix dimension is part of the name.
  3. CodeQL path-filtered to src/tests, so on a documentation-only pull request the workflow
     never ran and its required context never arrived at all.
  4. jobs gated on the change classifier: a job that skips on a docs-only change cannot
     satisfy a required context either.

All four are the same invariant, and all four are checkable from two files we already have.

WHAT THIS CHECKS
----------------
For every required context, in both the ruleset and classic branch protection:

  NAMED      some workflow declares a job whose check name can equal it
  REACHABLE  that workflow runs on pull_request, unrestricted by a paths filter, and the job
             is not gated on the docs-only classifier

EXIT CODES -- distinct on purpose.
  0  every required context is satisfiable
  1  at least one is not
  3  REFUSE: the required set could not be read. Not measured is not the same as clean.
"""
from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

EXIT_UNSATISFIABLE = 1
EXIT_REFUSE = 3


def gh_json(*args: str):
    """Return parsed JSON from gh, or None when the call fails."""
    try:
        out = subprocess.run(["gh", *args], capture_output=True, text=True, timeout=60)
    except Exception:  # noqa: BLE001
        return None
    if out.returncode != 0:
        return None
    try:
        return json.loads(out.stdout or "null")
    except json.JSONDecodeError:
        return None


def required_contexts(repo: str) -> list[tuple[str, str]] | None:
    """(context, source) for every required check. None means REFUSE, [] means genuinely none."""
    found: list[tuple[str, str]] = []
    ok = False

    rules = gh_json("api", f"repos/{repo}/rules/branches/main")
    if rules is not None:
        ok = True
        for rule in rules:
            if rule.get("type") == "required_status_checks":
                for chk in (rule.get("parameters") or {}).get("required_status_checks") or []:
                    found.append((chk["context"], "ruleset"))

    prot = gh_json("api", f"repos/{repo}/branches/main/protection")
    if prot is not None:
        ok = True
        for ctx in ((prot.get("required_status_checks") or {}).get("contexts") or []):
            found.append((ctx, "classic"))

    return found if ok else None


def workflow_jobs(root: Path):
    """Yield (workflow_path, workflow_name, job_key, job_name, runs_on_pr, pr_paths, docs_gated)."""
    try:
        import yaml
    except ImportError:
        return

    for path in sorted((root / ".github" / "workflows").glob("*.yml")):
        try:
            doc = yaml.safe_load(path.read_text(encoding="utf-8"))
        except Exception:  # noqa: BLE001
            continue
        if not isinstance(doc, dict):
            continue

        # `on` parses as the boolean True in YAML 1.1, which is the single most common way a
        # workflow parser silently sees no triggers at all.
        triggers = doc.get(True, doc.get("on")) or {}
        pr = triggers.get("pull_request") if isinstance(triggers, dict) else None
        runs_on_pr = pr is not None or (isinstance(triggers, list) and "pull_request" in triggers)
        pr_paths = (pr or {}).get("paths") if isinstance(pr, dict) else None

        for key, job in (doc.get("jobs") or {}).items():
            job = job or {}
            name = job.get("name") or key
            docs_gated = "docs_only" in str(job.get("if") or "")
            yield path, doc.get("name") or path.stem, key, str(name), runs_on_pr, pr_paths, docs_gated


def name_can_equal(declared: str, context: str) -> bool:
    """Could a check run for this job be named exactly `context`?

    A matrix job's name contains ${{ ... }} placeholders that expand at runtime, so an exact
    comparison would reject `Analyze C# Code (csharp)` against `Analyze ${{ matrix.language }}`.
    Placeholders are treated as wildcards; everything else must match exactly, because being
    lax here would defeat the whole check.
    """
    if declared == context:
        return True
    if "${{" not in declared:
        # A matrix job may still gain a " (dimension)" suffix that the declaration does not show.
        return bool(re.fullmatch(re.escape(declared) + r" \(.+\)", context))
    pattern = "".join(
        ".+" if part.startswith("${{") else re.escape(part)
        for part in re.split(r"(\$\{\{[^}]*\}\})", declared)
        if part
    )
    return bool(re.fullmatch(pattern, context) or re.fullmatch(pattern + r" \(.+\)", context))


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", default="TrigintaFaces/Excalibur")
    ap.add_argument("--root", default=".")
    ap.add_argument("--self-test", action="store_true")
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    root = Path(args.root)
    contexts = required_contexts(args.repo)
    if contexts is None:
        print("::error::REFUSE: could not read the required contexts from either the ruleset or "
              "classic branch protection. A required set that was not measured is not a clean one.",
              file=sys.stderr)
        return EXIT_REFUSE

    if not contexts:
        print("::error::REFUSE: no required contexts found at all. Either protection is off or the "
              "query is wrong; both deserve a look, and neither is a pass.", file=sys.stderr)
        return EXIT_REFUSE

    jobs = list(workflow_jobs(root))
    if not jobs:
        print("::error::REFUSE: parsed no jobs from .github/workflows. Without them nothing can be "
              "matched and every context would look unsatisfiable.", file=sys.stderr)
        return EXIT_REFUSE

    problems: list[str] = []
    print(f"checking {len(contexts)} required context(s) against {len(jobs)} declared job(s)\n")

    for context, source in contexts:
        matches = [j for j in jobs if name_can_equal(j[3], context)]
        if not matches:
            problems.append(
                f"[{source}] '{context}' matches NO job in any workflow. A check run is named after "
                f"the JOB -- 'Workflow / Job' is a display convention, and a matrix dimension such "
                f"as '(csharp)' is part of the name. Nothing will ever report this, so the pull "
                f"request waits forever.")
            print(f"  UNSATISFIABLE  {context}   [{source}]")
            continue

        reachable = [m for m in matches if m[4] and not m[5] and not m[6]]
        if not reachable:
            why = []
            if not any(m[4] for m in matches):
                why.append("its workflow does not trigger on pull_request")
            if any(m[5] for m in matches):
                why.append("its workflow is restricted by a paths filter, so some pull requests never run it")
            if any(m[6] for m in matches):
                why.append("the job is gated on the docs-only classifier, so it skips on documentation changes")
            problems.append(f"[{source}] '{context}' is declared but NOT always reachable: " + "; ".join(why))
            print(f"  UNREACHABLE    {context}   [{source}]")
            continue

        print(f"  ok             {context}   [{source}] -> {reachable[0][0].name}")

    if problems:
        print("\n::error::%d required status check(s) cannot be satisfied." % len(problems), file=sys.stderr)
        for p in problems:
            print(f"  {p}", file=sys.stderr)
        return EXIT_UNSATISFIABLE

    print("\nevery required context is named by a real job and reachable on every pull request.")
    return 0


def self_test() -> int:
    """Both arms. A matcher that accepts everything is as useless as one that accepts nothing."""
    checks = [
        ("exact job name matches", name_can_equal("CI Summary", "CI Summary"), True),
        ("workflow-prefixed context does NOT match a job name",
         name_can_equal("Release-Blocking CI Governance",
                        "Continuous Integration / Release-Blocking CI Governance"), False),
        ("matrix suffix is accepted against a bare declaration",
         name_can_equal("Analyze C# Code", "Analyze C# Code (csharp)"), True),
        ("placeholder expands as a wildcard",
         name_can_equal("Unit Tests (${{ matrix.shard.name }})", "Unit Tests (core)"), True),
        ("a different job name does NOT match",
         name_can_equal("Quality Summary", "CI Summary"), False),
        ("a prefix of a name does NOT match",
         name_can_equal("Scan", "Scan for Secrets"), False),
    ]
    bad = [n for n, got, want in checks if got != want]
    for n, got, want in checks:
        print(f"SELF-TEST: {'PASS' if got == want else 'FAIL'} -- {n}")
    if bad:
        print(f"SELF-TEST FAILED: {bad}", file=sys.stderr)
        return 1
    print("SELF-TEST: the satisfiability matcher is non-vacuous.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
