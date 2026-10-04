#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
"""Accept only complete exact-run evidence; inherit solely across verified docs changes."""

import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys

WORKFLOW = ".github/workflows/ci.yml"
# This policy is independent of API results. Validate it against the candidate's
# workflow before inspecting jobs: missing/renamed shards must never disappear.
JOBS = {
    "changes": ("Classify Changes", ()),
    "preflight": ("PR Preflight", ()),
    "package": ("Package (Ubuntu)", ()),
    "dashboard-spa": ("Dashboard SPA (tests, types, asset drift)", ()),
    "documentation-validation": ("Documentation Verification (T5.4)", ()),
    "smoke-tests": ("Smoke Tests (Critical Path)", ()),
    "unit-shard-coverage": ("Unit Shard Coverage Audit", ()),
    "unit-tests": ("Unit Tests (${{ matrix.shard.name }})", (
        "core", "transport", "middleware", "excalibur-data", "excalibur-platform", "excalibur-messaging")),
    "unit-tests-async-risk": ("Unit Tests - Async Risk (${{ matrix.shard.name }})", ("messaging", "observability")),
    "unit-shard-aggregation": ("Unit Shard Aggregation (honest set + compile + de-dup)", ()),
    "unit-tests-windows": ("Unit Tests (Windows - Deterministic)", ()),
    "unit-tests-windows-async-risk": ("Unit Tests (Windows - Async Risk)", ()),
    "unit-tests-macos": ("Unit Tests (macOS - Deterministic)", ()),
    "unit-tests-macos-async-risk": ("Unit Tests (macOS - Async Risk)", ()),
    "integration-tests": ("Integration Tests (${{ matrix.shard.name }})", (
        "excalibur-eventsourcing", "excalibur-outbox", "excalibur-inbox", "excalibur-eventstore",
        "excalibur-leaderelection", "excalibur-rest", "dispatch")),
    "functional-tests": ("Functional Tests", ()),
    "contract-tests": ("Contract Tests", ()),
    "architecture-tests": ("Architecture Tests", ()),
    "flaky-quarantine-governance": ("Flaky Quarantine Governance", ()),
    "flaky-tests-quarantine": ("Flaky Tests Quarantine (Non-blocking)", ()),
    "serialization-policy": ("Serialization Policy Validation", ()),
    "aot-build-analysis": ("AOT Build Analysis (report-only)", ()),
    "quality": ("Quality Analysis", ()),
    "transport-conformance": ("Transport Conformance", ()),
    "release-blocking-tests": ("Release-Blocking Test Governance", ()),
    "release-blocking-ci": ("Release-Blocking CI Governance", ()),
    "pack-quality": ("Pack Quality Gate (report)", ()),
    "packaging-smoke-tests": ("Packaging Smoke Tests (T5.2)", ()),
    "sample-validation": ("Sample Certification Validation (T3.4)", ()),
    "aot-sample-validation": ("AOT Sample Validation", ()),
    "summary": ("CI Summary", ()),
}
ALWAYS = {"changes", "preflight", "dashboard-spa", "documentation-validation", "flaky-tests-quarantine",
          "release-blocking-tests", "release-blocking-ci", "summary"}
DOC_STEPS = {
    "TypeScript check", "Validate version-safe docs links",
    "Docs C# snippet phantom gate (diff-scoped)", "Prove the docs phantom gate is non-vacuous",
    "Build Docusaurus (enforcing zero warnings)",
}
TEST_STEPS = {
    "preflight": {"Validate preflight structure and policy"},
    "package": {"Verify independent build outputs"},
    "dashboard-spa": {"SPA gate", "Prove the gate is non-vacuous"},
    "smoke-tests": {"Build and test"},
    "unit-tests": {"Build and test"},
    "unit-tests-async-risk": {"Build and test"},
    "unit-tests-windows": {"Build and test", "Verify every expected assembly reported"},
    "unit-tests-windows-async-risk": {"Build and test"},
    "unit-tests-macos": {"Build and test"},
    "unit-tests-macos-async-risk": {"Build and test"},
    "integration-tests": {"Establish EXPECTED test population", "Run Integration Tests", "Verify integration test results"},
    "functional-tests": {"Run Functional Tests"},
    "contract-tests": {"Run contract tests"},
    "architecture-tests": {"Run architecture test projects"},
    "transport-conformance": {"Run conformance tests"},
    "packaging-smoke-tests": {"Run packaging smoke tests"},
    "sample-validation": {"Run sample validation"},
    "aot-sample-validation": {"Build AOT sample (Release)", "Run AOT sample (smoke test)"},
}
REPORT_ONLY = {"triage-labels": "Apply CI Triage Labels", "test-result-reporter": "Test Result Reporter"}
REUSABLE = {
    "call-security": ("security.yml", (
        "Security SAST (PR report-only, push blocking)", "Secrets Scan (Gitleaks)",
        "SBOM Generation (CycloneDX)", "Dependency Review", "Dependency Vulnerability Audit",
        "Notices & License Headers", "OSS Metadata Audit")),
    "call-governance": ("governance.yml", (
        "Solution Governance Validation", "Package Dependency Graph Validation", "Package Composition Validation (T2.4)",
        "Public API Baseline Audit", "Transitive Bloat Audit", "Repo Inventory", "Specs Location Guard")),
    "call-coverage": ("coverage.yml", (
        "Coverage Gate Enforcement", "Coverage Diff On Touched Files", "Coverage Analysis", "Coverage Baseline Artifact")),
    "call-performance": ("performance.yml", (
        "Performance Gate - MediatR Local Parity", "Performance Gate - Transport Comparison",
        "Performance Gate - Dispatch Hot Path", "Performance Gate - Observability Overhead",
        "Performance Gate - Persistence/Background Smoke")),
}
PR_ONLY = {"call-security / Dependency Review", "call-coverage / Coverage Diff On Touched Files"}
REUSABLE_TEST_STEPS = {
    "call-performance / Performance Gate - MediatR Local Parity": {"Run MediatR local parity benchmark gate", "Enforce MediatR local parity thresholds"},
    "call-performance / Performance Gate - Transport Comparison": {"Run transport comparison benchmark gate", "Enforce transport comparison thresholds"},
    "call-performance / Performance Gate - Dispatch Hot Path": {"Run dispatch hot-path benchmark gate", "Enforce dispatch hot-path thresholds"},
    "call-performance / Performance Gate - Observability Overhead": {"Run observability overhead benchmark gate", "Enforce observability overhead thresholds"},
    "call-performance / Performance Gate - Persistence/Background Smoke": {"Run persistence/background benchmark smoke classes", "Enforce persistence/background smoke thresholds"},
}
CLASSIFIER_SCRIPT = '''set -euo pipefail
if [ "${{ github.event_name }}" = "pull_request" ]; then
  base="${{ github.event.pull_request.base.sha }}"
  head="${{ github.event.pull_request.head.sha }}"
else
  base="$(git rev-parse HEAD^1 2>/dev/null || echo '')"
  head="$(git rev-parse HEAD)"
fi
if [ -z "$base" ] || ! git cat-file -e "${base}^{commit}" 2>/dev/null; then
  echo "docs_only=false" >> "$GITHUB_OUTPUT"
  exit 0
fi
docs_only="$(python3 eng/ci/release-test-verdict-gate.py --classify-base "$base" --sha "$head")"
echo "docs_only=$docs_only" >> "$GITHUB_OUTPUT"'''
DOC_SCRIPT = '''set -euo pipefail
if [ "$EVENT_NAME" = "pull_request" ]; then
  export DOCS_GATE_BASE_REF="$PR_BASE_SHA"
else
  export DOCS_GATE_BASE_REF="$(git rev-parse HEAD^1)"
fi
git cat-file -e "${DOCS_GATE_BASE_REF}^{commit}"
bash eng/ci/docs-csharp-phantom-gate.sh'''


class Refuse(Exception):
    """No complete proof can be obtained."""


class Failed(Exception):
    """An authoritative run/job reported failure."""


def command(args):
    result = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", timeout=120)
    if result.returncode:
        raise Refuse(f"{args[0]} failed (exit {result.returncode}): {result.stderr.strip()}")
    return result.stdout


def git(*args):
    return command(["git", *args])


def docs_only(base, head):
    """Classify committed blobs/modes; no working-tree or job-absence inference."""
    if any(not re.fullmatch(r"[0-9a-f]{40}|[0-9a-f]{64}", value) for value in (base, head)):
        raise Refuse("Change classification requires exact commit IDs")
    raw = git("diff", "--raw", "-z", "--no-renames", base, head, "--").split("\0")
    if raw == [""]:
        return False
    if raw[-1] != "" or len(raw) % 2 != 1:
        raise Refuse("Malformed git diff")
    for index in range(0, len(raw) - 1, 2):
        metadata, path = raw[index:index + 2]
        fields = metadata.split()
        if (len(fields) != 5 or fields[0] not in (":100644", ":000000")
                or fields[1] not in ("100644", "000000")):
            return False
        # Consumer docs feed architecture tests. Root/package READMEs are packaged.
        # Contributor markdown still needs this candidate's documentation checks.
        if not re.fullmatch(r"(?:docs|management)/[^\r\n]+\.md", path):
            return False
    return True


def job_sections(text):
    """Recognize every job key, then refuse unsupported YAML rather than omit it."""
    body = text.split("\njobs:\n", 1)[1]
    entries = list(re.finditer(r"^  ([^\s#][^:\n]*):([^\n]*)\n", body, re.M))
    sections = {}
    for index, entry in enumerate(entries):
        identifier, suffix = entry[1], entry[2].strip()
        if (not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_-]*", identifier)
                or (suffix and not suffix.startswith("#")) or identifier in sections):
            raise Refuse(f"Unsupported/duplicate YAML job entry: {identifier}")
        end = entries[index + 1].start() if index + 1 < len(entries) else len(body)
        sections[identifier] = body[entry.end():end]
    return sections


def verify_workflow(text):
    """Refuse changes outside the supported static-name/shard-name matrix shape."""
    sections = job_sections(text)
    if set(sections) != JOBS.keys() | REPORT_ONLY.keys() | REUSABLE.keys():
        raise Refuse("Workflow job set changed; every job needs an explicit release policy")
    for identifier, (name, shards) in JOBS.items():
        body = sections.get(identifier, "")
        match = re.search(r"^    name: (.+)$", body, re.M)
        if not match or match[1] != name:
            raise Refuse(f"Expected workflow job changed/missing: {identifier}")
        if shards:
            strategy = body.split("    strategy:", 1)[-1].split("    steps:", 1)[0]
            actual = re.findall(r"^          - (?:\{ )?name: ([a-z0-9-]+)", strategy, re.M)
            if actual != list(shards):
                raise Refuse(f"Expected matrix changed: {identifier}: {actual}")
        elif re.search(r"^      matrix:", body, re.M):
            raise Refuse(f"Unexpected matrix: {identifier}")
    for identifier, name in REPORT_ONLY.items():
        if f"    name: {name}\n" not in sections[identifier]:
            raise Refuse(f"Reporting job changed: {identifier}")
    for identifier, (filename, _) in REUSABLE.items():
        if f"    uses: ./.github/workflows/{filename}\n" not in sections[identifier]:
            raise Refuse(f"Reusable workflow changed: {identifier}")
    preflight = sections['preflight']
    if re.search(r'^    (?:if|needs|continue-on-error):', preflight, re.M):
        raise Refuse('Preflight must remain an unconditional blocking root job')
    if "          ref: ${{ github.event_name == 'pull_request' && github.event.pull_request.head.sha || github.sha }}\n" not in preflight:
        raise Refuse('Preflight must check the same candidate as production validation')
    needs = re.search(r'^    needs: \[([^\n]+)\]', sections['summary'], re.M)
    if not needs or 'preflight' not in [item.strip() for item in needs[1].split(',')]:
        raise Refuse('CI Summary must require preflight')
    required = re.search(r'^          ALWAYS_REQUIRED: "([^"]+)"', sections['summary'], re.M)
    if not required or 'preflight' not in required[1].split():
        raise Refuse('Documentation-only changes must still require preflight')


def verify_reusable(text, names):
    actual = []
    for body in job_sections(text).values():
        match = re.search(r"^    name: (.+)$", body, re.M)
        if not match or "${{" in match[1] or re.search(r"^    (?:strategy|uses):", body, re.M):
            raise Refuse("Unsupported reusable job shape")
        actual.append(match[1])
    if actual != list(names):
        raise Refuse("Reusable workflow job population changed")


def verify_docs_policy(workflow):
    # Only the executable run block of the named step counts. A quoted command
    # in a comment or another step cannot establish the comparison baseline.
    sections = job_sections(workflow)
    policies = (
        ("changes", "Classify", CLASSIFIER_SCRIPT, ["id: classify", "shell: bash"]),
        ("documentation-validation", "Docs C# snippet phantom gate (diff-scoped)", DOC_SCRIPT,
         ["env:", "EVENT_NAME: ${{ github.event_name }}", "PR_BASE_SHA: ${{ github.event.pull_request.base.sha }}"]),
    )
    for job, name, script, bindings in policies:
        blocks = re.findall(r"^      - name: " + re.escape(name) + r"\n(.*?)(?=^      - name:|\Z)", sections[job], re.M | re.S)
        if len(blocks) != 1:
            raise Refuse(f"Missing/duplicate policy step: {name}")
        runs = re.findall(r"^        run: \|\n((?:          [^\n]*\n|\n)*)", blocks[0], re.M)
        if len(runs) != 1:
            raise Refuse(f"Unsupported policy script: {name}")
        header = blocks[0].split("        run: |\n", 1)[0]
        actual_bindings = [line.strip() for line in header.splitlines() if line.strip() and not line.lstrip().startswith("#")]
        if actual_bindings != bindings:
            raise Refuse(f"Docs policy environment/step binding changed: {name}")
        commands = [line.strip() for line in runs[0].splitlines() if line.strip() and not line.lstrip().startswith("#")]
        if commands != [line.strip() for line in script.splitlines()]:
            raise Refuse(f"Executable docs policy changed: {name}")


class GitHub:
    def __init__(self, repo):
        self.repo = repo
        self.evidence = []

    def pages(self, endpoint, member):
        pages = json.loads(command(["gh", "api", "--paginate", "--slurp", endpoint]))
        self.evidence.append({"endpoint": endpoint, "response": pages})
        if not isinstance(pages, list) or not pages:
            raise Refuse("Missing API pages")
        total = pages[0]["total_count"]
        values = [item for page in pages for item in page[member]]
        if type(total) is not int or len(values) != total:
            raise Refuse(f"Incomplete {member} pagination")
        if any(page["total_count"] != total for page in pages):
            raise Refuse(f"Changing {member} page population")
        if len({item["id"] for item in values}) != len(values):
            raise Refuse(f"Duplicate {member} identities")
        return values

    def run(self, sha):
        runs = self.pages(f"repos/{self.repo}/actions/workflows/ci.yml/runs?head_sha={sha}&event=push&per_page=100", "workflow_runs")
        eligible = [run for run in runs if run["head_sha"] == sha and run["event"] == "push"
                    and run["path"].split("@", 1)[0] == WORKFLOW
                    and run["head_repository"]["full_name"] == self.repo]
        if not eligible:
            raise Refuse(f"No eligible push CI run for {sha}")
        run = max(eligible, key=lambda item: (item["id"], item["run_attempt"]))
        if type(run["id"]) is not int or type(run["run_attempt"]) is not int or run["run_attempt"] < 1:
            raise Refuse("Invalid run/attempt identity")
        if run["status"] != "completed":
            raise Refuse("Latest eligible run is incomplete")
        if run["conclusion"] != "success":
            raise Failed(f"CI run {run['id']} attempt {run['run_attempt']}: {run['conclusion']}")
        return run


def require_steps(job, names):
    for name in names:
        matching = [step for step in job["steps"] if step["name"] == name]
        if len(matching) != 1 or matching[0]["status"] != "completed" or matching[0]["conclusion"] != "success":
            raise Refuse(f"Required successful step missing: {job['name']}/{name}")


def jobs_verdict(jobs, run):
    by_name = {}
    for job in jobs:
        if (job["run_id"] != run["id"] or job["run_attempt"] != run["run_attempt"]
                or job["head_sha"] != run["head_sha"]):
            raise Refuse("Job belongs to another run/attempt/SHA")
        if job["name"] in by_name:
            raise Refuse(f"Duplicate job name: {job['name']}")
        by_name[job["name"]] = job
        if job["status"] != "completed":
            raise Refuse(f"Incomplete job: {job['name']}")
        if job["conclusion"] not in ("success", "skipped") and job["name"] not in REPORT_ONLY.values():
            raise Failed(f"Job {job['name']}: {job['conclusion']}")
    gated = []
    for identifier, (template, shards) in JOBS.items():
        # Observed GitHub run 31004778042: a skipped matrix has ONE job with
        # the unexpanded name expression, because its condition precedes expansion.
        if shards and template in by_name:
            if by_name[template]["conclusion"] != "skipped":
                raise Refuse(f"Unexpanded successful matrix: {template}")
            if any(template.replace("${{ matrix.shard.name }}", shard) in by_name for shard in shards):
                raise Refuse(f"Both expanded and unexpanded matrix: {template}")
            gated.append("skipped")
            continue
        for shard in shards or (None,):
            name = template.replace("${{ matrix.shard.name }}", shard) if shard else template
            if name not in by_name:
                raise Refuse(f"Missing expected job: {name}")
            conclusion = by_name[name]["conclusion"]
            if identifier in ALWAYS:
                if conclusion != "success":
                    raise Refuse(f"Required job did not succeed: {name}")
            else:
                gated.append(conclusion)
            if conclusion == "success" and identifier in TEST_STEPS:
                require_steps(by_name[name], TEST_STEPS[identifier])
    require_steps(by_name["Classify Changes"], {"Classify"})
    require_steps(by_name["Documentation Verification (T5.4)"], DOC_STEPS)
    expected_names = set(REPORT_ONLY.values())
    for template, shards in JOBS.values():
        expected_names.add(template)
        expected_names.update(template.replace("${{ matrix.shard.name }}", shard) for shard in shards)
    for identifier, (_, children) in REUSABLE.items():
        expected_names.add(identifier)
        names = [f"{identifier} / {child}" for child in children]
        expected_names.update(names)
        if identifier in by_name:
            if by_name[identifier]["conclusion"] != "skipped" or any(name in by_name for name in names):
                raise Refuse(f"Invalid collapsed reusable workflow: {identifier}")
            gated.append("skipped")
        else:
            for name in names:
                if name not in by_name:
                    raise Refuse(f"Missing reusable workflow job: {name}")
                if name not in PR_ONLY:
                    gated.append(by_name[name]["conclusion"])
                if by_name[name]["conclusion"] == "success" and name in REUSABLE_TEST_STEPS:
                    require_steps(by_name[name], REUSABLE_TEST_STEPS[name])
    if by_name.keys() - expected_names:
        raise Refuse(f"Unclassified API jobs: {sorted(by_name.keys() - expected_names)}")
    if all(value == "success" for value in gated):
        return "GREEN"
    if all(value == "skipped" for value in gated):
        return "DOCS"
    raise Refuse("Partially executed required matrix")


def evaluate(sha, api, max_walk):
    if not re.fullmatch(r"[0-9a-f]{40}|[0-9a-f]{64}", sha):
        raise Refuse("--sha must be an exact commit ID")
    current = sha
    inspected = []
    for _ in range(max_walk):
        workflow = git("show", f"{current}:{WORKFLOW}")
        verify_workflow(workflow)
        for filename, names in REUSABLE.values():
            verify_reusable(git("show", f"{current}:.github/workflows/{filename}"), names)
        run = api.run(current)
        jobs = api.pages(f"repos/{api.repo}/actions/runs/{run['id']}/attempts/{run['run_attempt']}/jobs?per_page=100", "jobs")
        verdict = jobs_verdict(jobs, run)
        inspected.append(run)
        quarantine = json.loads(git("show", f"{current}:eng/ci/flaky-tests-quarantine.json"))["tests"]
        if not isinstance(quarantine, list):
            raise Refuse("Quarantine test population must be a list")
        quarantine_job = next(job for job in jobs if job["name"] == JOBS["flaky-tests-quarantine"][0])
        require_steps(quarantine_job, {"Compose quarantined test filter"})
        if quarantine:
            require_steps(quarantine_job, {"Run quarantined tests"})
        if verdict == "GREEN":
            for selected in inspected:
                fresh = api.run(selected["head_sha"])
                if (fresh["id"], fresh["run_attempt"]) != (selected["id"], selected["run_attempt"]):
                    raise Refuse("CI run/attempt changed during verification")
            return current
        verify_docs_policy(workflow)
        parent = git("rev-parse", "--verify", f"{current}^1").strip()
        if not docs_only(parent, current):
            raise Refuse(f"Skipped tests with changed/unknown test or package inputs at {current}")
        current = parent
    raise Refuse("Ancestor search bound exhausted without a tested verdict")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sha", required=True)
    parser.add_argument("--repo", default=os.environ.get("GITHUB_REPOSITORY", ""))
    parser.add_argument("--max-walk", type=int, default=25)
    parser.add_argument("--classify-base", help="Only classify base-to-sha change for CI")
    parser.add_argument("--out", default="artifacts/reports/release-test-verdict.json")
    args = parser.parse_args(argv)
    api = GitHub(args.repo)
    report = {"candidate": args.sha, "repository": args.repo}
    try:
        if args.classify_base:
            print("true" if docs_only(args.classify_base, args.sha) else "false")
            return 0
        if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", args.repo) or args.max_walk < 1:
            raise Refuse("Repository and positive ancestor bound required")
        tested_sha = evaluate(args.sha, api, args.max_walk)
        report.update(verdict="GREEN", tested_sha=tested_sha)
        print(f"GREEN: {args.sha} verified against tested commit {tested_sha}")
        return 0
    except Failed as error:
        report.update(verdict="RED", reason=str(error))
        print(f"RED: {error}", file=sys.stderr)
        return 1
    except (Refuse, KeyError, IndexError, TypeError, ValueError, OSError, subprocess.SubprocessError) as error:
        report.update(verdict="REFUSE", reason=str(error))
        print(f"REFUSE: {error}", file=sys.stderr)
        return 2
    finally:
        if not args.classify_base:
            report["api_evidence"] = api.evidence
            destination = Path(args.out)
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    sys.exit(main())
