#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
"""Real committed histories and raw API JSON controls for the release gate."""

from contextlib import redirect_stderr, redirect_stdout
from copy import deepcopy
import importlib.util
import io
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("verdict_gate", Path(__file__).with_name("release-test-verdict-gate.py"))
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)
WORKFLOW = (ROOT / GATE.WORKFLOW).read_text(encoding="utf-8")
REPO = "owner/repository"


def run_record(sha, identifier=10, attempt=1, **updates):
    return dict(id=identifier, head_sha=sha, run_attempt=attempt, status="completed",
                conclusion="success", event="push", path=GATE.WORKFLOW,
                head_repository={"full_name": REPO}, **updates)


def job_records(run, docs=False):
    jobs = []
    for identifier, (template, shards) in GATE.JOBS.items():
        # Use GitHub's observed unexpanded name for a skipped matrix.
        names = [template] if docs or not shards else [template.replace("${{ matrix.shard.name }}", shard) for shard in shards]
        for name in names:
            state = "skipped" if docs and identifier not in GATE.ALWAYS else "success"
            steps = set(GATE.TEST_STEPS.get(identifier, ()))
            if identifier == "changes":
                steps.add("Classify")
            if identifier == "documentation-validation":
                steps.update(GATE.DOC_STEPS)
            if identifier == "flaky-tests-quarantine":
                steps.add("Compose quarantined test filter")
            jobs.append(dict(id=len(jobs) + 1, run_id=run["id"], run_attempt=run["run_attempt"],
                             head_sha=run["head_sha"], name=name, status="completed", conclusion=state,
                             steps=[dict(name=step, status="completed", conclusion="success") for step in sorted(steps)]))
    for identifier, (_, children) in GATE.REUSABLE.items():
        for name in ([identifier] if docs else [f"{identifier} / {child}" for child in children]):
            jobs.append(dict(id=len(jobs) + 1, run_id=run["id"], run_attempt=run["run_attempt"],
                             head_sha=run["head_sha"], name=name, status="completed",
                             conclusion="skipped" if docs or name in GATE.PR_ONLY else "success",
                             steps=[dict(name=step, status='completed', conclusion='success')
                                    for step in GATE.REUSABLE_TEST_STEPS.get(name, ())]))
    return jobs


class RawApi:
    """Stub transport bytes only: selection, parsing, job checks and Git stay real."""
    def __init__(self, runs, jobs):
        self.runs = runs
        self.jobs = jobs
        self.calls = []
        self.override = None
        self.real_command = GATE.command

    def __call__(self, args):
        if args[0] != "gh":
            return self.real_command(args)
        self.calls.append(args)
        if self.override:
            value = self.override(args)
            if value is not None:
                return value
        endpoint = args[-1]
        if "/workflows/ci.yml/runs?" in endpoint:
            sha = re.search(r"head_sha=([0-9a-f]+)", endpoint)[1]
            records = self.runs.get(sha, [])
            key = "workflow_runs"
        else:
            match = re.search(r"/runs/(\d+)/attempts/(\d+)/jobs", endpoint)
            if not match:
                raise AssertionError(f"Wrong API endpoint: {endpoint}")
            records = self.jobs[(int(match[1]), int(match[2]))]
            key = "jobs"
        # Exercise multiple pages on every jobs call, preserving independent total.
        chunks = [records[index:index + 10] for index in range(0, len(records), 10)] or [[]]
        return json.dumps([{"total_count": len(records), key: chunk} for chunk in chunks])


class VerdictTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp = tempfile.TemporaryDirectory(prefix="release-verdict-")
        cls.previous = Path.cwd()
        os.chdir(cls.temp.name)
        GATE.git("init", "-q")
        GATE.git("config", "user.email", "gate-fixture@example.invalid")
        GATE.git("config", "user.name", "Gate Fixture")
        GATE.git("config", "core.autocrlf", "false")
        GATE.git("config", "core.hooksPath", ".no-hooks")
        (Path(GATE.WORKFLOW)).parent.mkdir(parents=True)
        Path(GATE.WORKFLOW).write_text(WORKFLOW, encoding="utf-8")
        for filename, _ in GATE.REUSABLE.values():
            relative = Path('.github/workflows') / filename
            relative.write_text((ROOT / relative).read_text(encoding="utf-8"), encoding="utf-8")
        Path("eng/ci").mkdir(parents=True)
        Path("eng/ci/flaky-tests-quarantine.json").write_text('{"tests":[]}', encoding="utf-8")
        GATE.git("add", ".")
        GATE.git("commit", "-qm", "fixture baseline")
        cls.serial = 0

    @classmethod
    def tearDownClass(cls):
        os.chdir(cls.previous)
        cls.temp.cleanup()

    def commit(self, path):
        type(self).serial += 1
        destination = Path(path)
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(str(self.serial), encoding="utf-8")
        GATE.git("add", "--", path)
        GATE.git("commit", "-qm", "fixture change")
        return GATE.git("rev-parse", "HEAD").strip()

    def full(self):
        sha = self.commit("src/fixture.cs")
        run = run_record(sha)
        jobs = job_records(run)
        return sha, run, jobs, RawApi({sha: [run]}, {(10, 1): jobs})

    def check(self, sha, api, expected=0, **arguments):
        output = Path("proof.json")
        args = ["--sha", sha, "--repo", REPO, "--out", str(output)]
        for name, value in arguments.items():
            args += ["--" + name.replace("_", "-"), str(value)]
        with patch.object(GATE, "command", api), redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
            result = GATE.main(args)
        self.assertEqual(expected, result, output.read_text(encoding="utf-8")[:1500])
        return json.loads(output.read_text(encoding="utf-8"))

    def test_complete_passing_matrix_and_exact_attempt_evidence(self):
        sha, run, jobs, api = self.full()
        report = self.check(sha, api)
        self.assertEqual(sha, report["tested_sha"])
        self.assertTrue(any("/attempts/1/jobs?" in call[-1] for call in api.calls))
        self.assertGreater(len(report["api_evidence"]), 2)

    def test_missing_renamed_duplicate_and_partial_jobs_refuse(self):
        sha, run, jobs, api = self.full()
        for mutation in ("missing", "renamed", "duplicate", "skipped", "incomplete", "old-attempt", "wrong-sha"):
            with self.subTest(mutation=mutation):
                changed = deepcopy(jobs)
                index = next(i for i, job in enumerate(changed) if job["name"] == "Unit Tests (core)")
                if mutation == "missing": del changed[index]
                elif mutation == "renamed": changed[index]["name"] = "Renamed Tests"
                elif mutation == "duplicate": changed.append(dict(changed[index], id=999))
                elif mutation == "skipped": changed[index]["conclusion"] = "skipped"
                elif mutation == "incomplete": changed[index]["status"] = "in_progress"
                elif mutation == "old-attempt": changed[index]["run_attempt"] = 2
                elif mutation == "wrong-sha": changed[index]["head_sha"] = "f" * 40
                api.jobs[(10, 1)] = changed
                self.check(sha, api, 2)

    def test_every_test_execution_step_must_succeed(self):
        sha, run, jobs, api = self.full()
        for identifier, names in GATE.TEST_STEPS.items():
            template, shards = GATE.JOBS[identifier]
            name = template.replace("${{ matrix.shard.name }}", shards[0]) if shards else template
            with self.subTest(job=name):
                changed = deepcopy(jobs)
                job = next(job for job in changed if job["name"] == name)
                next(step for step in job["steps"] if step["name"] in names)["conclusion"] = "skipped"
                api.jobs[(10, 1)] = changed
                self.check(sha, api, 2)
        for name, names in GATE.REUSABLE_TEST_STEPS.items():
            with self.subTest(job=name):
                changed = deepcopy(jobs)
                job = next(job for job in changed if job['name'] == name)
                job['steps'][0]['conclusion'] = 'skipped'
                api.jobs[(10,1)] = changed
                self.check(sha, api, 2)

    def test_failed_cancelled_timed_out_unknown_jobs_fail(self):
        sha, run, jobs, api = self.full()
        for status in ("failure", "cancelled", "timed_out", "action_required", None):
            with self.subTest(status=status):
                changed = deepcopy(jobs)
                changed[1]["conclusion"] = status
                api.jobs[(10, 1)] = changed
                self.check(sha, api, 1)

    def test_failed_prerequisite_never_inherits_green_parent(self):
        parent, old, old_jobs, api = self.full()
        sha = self.commit("src/fixture.cs")
        failed = run_record(sha, 20)
        failed["conclusion"] = "failure"
        api.runs[sha] = [failed]
        api.jobs[(20, 1)] = job_records(failed, docs=True)
        self.check(sha, api, 1)
        self.assertFalse(any(f"head_sha={parent}" in call[-1] for call in api.calls))

    def test_source_build_package_and_consumer_docs_never_inherit(self):
        for path in ("src/fixture.cs", "tests/test.cs", "Directory.Build.props", "Directory.Packages.props",
                     "global.json", "nuget.config", "README.md", "src/Package/README.md",
                     "docs-site/docs/pipeline/profiles.md", "eng/build.sh", "docs/tool.py", "unknown.md"):
            with self.subTest(path=path):
                parent = GATE.git("rev-parse", "HEAD").strip()
                sha = self.commit(path)
                old, run = run_record(parent), run_record(sha, 20)
                api = RawApi({parent: [old], sha: [run]}, {(10, 1): job_records(old), (20, 1): job_records(run, docs=True)})
                self.check(sha, api, 2)

    def test_docs_chain_inherits_only_complete_tested_ancestor(self):
        parent, old, old_jobs, api = self.full()
        for number, path in enumerate(("docs/contributing.md", "management/specs/review.md"), start=20):
            sha = self.commit(path)
            run = run_record(sha, number)
            api.runs[sha] = [run]
            api.jobs[(number, 1)] = job_records(run, docs=True)
        self.assertEqual(parent, self.check(sha, api)["tested_sha"])
        self.check(sha, api, 2, max_walk=1)
        api.runs[parent][0]["conclusion"] = "failure"
        self.check(sha, api, 1)

    def test_docs_need_successful_classifier_and_current_validation(self):
        parent, old, old_jobs, api = self.full()
        sha = self.commit("docs/contributing.md")
        run = run_record(sha, 20)
        original = job_records(run, docs=True)
        api.runs[sha] = [run]
        for name in ("Classify Changes", "Documentation Verification (T5.4)"):
            for mutation in ("job-skipped", "step-skipped", "step-missing"):
                with self.subTest(name=name, mutation=mutation):
                    changed = deepcopy(original)
                    job = next(job for job in changed if job["name"] == name)
                    if mutation == "job-skipped": job["conclusion"] = "skipped"
                    elif mutation == "step-skipped": job["steps"][0]["conclusion"] = "skipped"
                    else: job["steps"] = []
                    api.jobs[(20, 1)] = changed
                    self.check(sha, api, 2)

    def test_latest_run_wins_over_old_high_attempt_and_green(self):
        sha, run, jobs, api = self.full()
        older = dict(run, id=9, run_attempt=99)
        for status, conclusion, expected in (("queued", None, 2), ("in_progress", None, 2), ("completed", "failure", 1)):
            with self.subTest(status=status):
                latest = dict(run, status=status, conclusion=conclusion)
                api.runs[sha] = [older, latest]
                self.check(sha, api, expected)

    def test_wrong_event_sha_workflow_repository_are_not_evidence(self):
        sha, run, jobs, api = self.full()
        for field, value in (("event", "pull_request"), ("event", "workflow_dispatch"), ("head_sha", "f"*40),
                             ("path", ".github/workflows/other.yml"), ("head_repository", {"full_name": "fork/repo"})):
            with self.subTest(field=field, value=value):
                api.runs[sha] = [dict(run, **{field: value})]
                self.check(sha, api, 2)

    def test_missing_run_never_walks(self):
        sha, run, jobs, api = self.full()
        api.runs[sha] = []
        self.check(sha, api, 2)

    def test_incomplete_duplicate_malformed_api_pages_refuse(self):
        sha, run, jobs, api = self.full()
        for payload in ("not json", "[]", '{}', '[{"total_count":1,"workflow_runs":[]}]',
                        json.dumps([{"total_count":2,"workflow_runs":[run,run]}]),
                        json.dumps([{"total_count":1,"workflow_runs":[run]}, {"total_count":0,"workflow_runs":[]}])):
            with self.subTest(payload=payload[:60]):
                api.override = lambda args: payload
                self.check(sha, api, 2)

    def test_candidate_rerun_during_ancestor_walk_refuses(self):
        parent, old, old_jobs, api = self.full()
        sha = self.commit("docs/contributing.md")
        run = run_record(sha, 20)
        api.runs[sha] = [run]
        api.jobs[(20, 1)] = job_records(run, docs=True)
        count = 0
        def race(args):
            nonlocal count
            if f"head_sha={sha}" in args[-1]:
                count += 1
                if count == 2:
                    return json.dumps([{"total_count":1,"workflow_runs":[dict(run,run_attempt=2)]}])
            return None
        api.override = race
        self.check(sha, api, 2)

    def test_deleted_renamed_newline_and_symlink_inputs_are_not_docs(self):
        before = self.commit("src/renamed.cs")
        Path("docs").mkdir(exist_ok=True)
        GATE.git("mv", "src/renamed.cs", "docs/renamed.md")
        GATE.git("commit", "-qm", "rename fixture")
        after = GATE.git("rev-parse", "HEAD").strip()
        self.assertFalse(GATE.docs_only(before, after))
        before = self.commit("src/deleted.cs")
        GATE.git("rm", "src/deleted.cs")
        GATE.git("commit", "-qm", "delete fixture")
        after = GATE.git("rev-parse", "HEAD").strip()
        self.assertFalse(GATE.docs_only(before, after))
        for raw in (":100644 100644 a b M\0docs/line\nfile.md\0", ":000000 120000 0 a A\0docs/link.md\0",
                    ":100644 100755 a b M\0docs/executable.md\0", ":000000 160000 0 a A\0docs/submodule.md\0"):
            with patch.object(GATE, "git", return_value=raw):
                self.assertFalse(GATE.docs_only(before, after))

    def test_empty_diff_and_unknown_commit_do_not_classify_as_docs(self):
        sha = GATE.git("rev-parse", "HEAD").strip()
        self.assertFalse(GATE.docs_only(sha, sha))
        with self.assertRaises(GATE.Refuse):
            GATE.docs_only("a" * 40, sha)

    def test_workflow_names_shards_and_new_test_jobs_cannot_drift(self):
        GATE.verify_workflow(WORKFLOW)
        for altered in (WORKFLOW.replace("name: Unit Tests (", "name: New Unit Tests (", 1),
                        WORKFLOW.replace("name: core,", "name: renamed,", 1),
                        WORKFLOW + "\n  new-tests:\n    name: Newly Required Tests\n",
                        WORKFLOW + "\n  verify-new:\n    name: New Verification\n",
                        WORKFLOW + "\n  verify_new:\n    name: New Verification\n",
                        WORKFLOW + "\n  VerifyNew:\n    name: New Verification\n",
                        WORKFLOW + '\n  "verify-new":\n    name: New Verification\n',
                        WORKFLOW + '\n  inline: {name: New Verification}\n',
                        WORKFLOW + '\n  changes:\n    name: Classify Changes\n'):
            with self.assertRaises(GATE.Refuse):
                GATE.verify_workflow(altered)

    def test_classifier_and_docs_comparison_use_same_immutable_policy(self):
        GATE.verify_docs_policy(WORKFLOW)
        self.assertNotIn('export DOCS_GATE_BASE_REF="origin/', WORKFLOW)
        for command in ('docs_only="$(python3 eng/ci/release-test-verdict-gate.py --classify-base "$base" --sha "$head")"',
                        'export DOCS_GATE_BASE_REF="$(git rev-parse HEAD^1)"'):
            with self.subTest(command=command), self.assertRaises(GATE.Refuse):
                GATE.verify_docs_policy(WORKFLOW.replace(command, '# ' + command))
        for altered in (
            WORKFLOW.replace('EVENT_NAME: ${{ github.event_name }}', 'EVENT_NAME: pull_request'),
            WORKFLOW.replace('PR_BASE_SHA: ${{ github.event.pull_request.base.sha }}', 'PR_BASE_SHA: ${{ github.sha }}'),
            WORKFLOW.replace('  changes:\n', '  other-classification:\n'),
        ):
            with self.assertRaises((GATE.Refuse, KeyError)):
                GATE.verify_docs_policy(altered)

    def test_preflight_candidate_and_required_wiring(self):
        preflight = GATE.job_sections(WORKFLOW)['preflight']
        for changed in (preflight.replace("github.event.pull_request.head.sha", "github.event.pull_request.base.sha"),
                        preflight + '    if: false\n', preflight + '    needs: package\n',
                        preflight + '    continue-on-error: true\n'):
            with self.assertRaises(GATE.Refuse):
                GATE.verify_workflow(WORKFLOW.replace(preflight, changed))
        for changed in (WORKFLOW.replace('needs: [changes, preflight, package', 'needs: [changes, package'),
                        WORKFLOW.replace('ALWAYS_REQUIRED: "changes preflight', 'ALWAYS_REQUIRED: "changes')):
            with self.assertRaises(GATE.Refuse):
                GATE.verify_workflow(changed)

    def test_no_production_verdict_override(self):
        sha, run, jobs, api = self.full()
        api.runs[sha] = []
        with patch.dict(os.environ, {"RTV_VERDICT_CMD": "echo GREEN"}):
            self.check(sha, api, 2)

    def test_reporting_failure_is_not_a_test_failure(self):
        sha, run, jobs, api = self.full()
        for name in GATE.REPORT_ONLY.values():
            jobs.append(dict(id=len(jobs)+1, run_id=run['id'], run_attempt=1, head_sha=sha,
                             name=name, status='completed', conclusion='failure', steps=[]))
        self.check(sha, api)

    def test_unknown_and_missing_reusable_jobs_refuse(self):
        sha, run, jobs, api = self.full()
        jobs.append(dict(id=999, run_id=run['id'], run_attempt=1, head_sha=sha,
                         name='Unregistered verification', status='completed', conclusion='skipped', steps=[]))
        self.check(sha, api, 2)
        jobs.pop()
        jobs.pop()
        self.check(sha, api, 2)

    def test_skipped_matrix_shape_matches_captured_github_evidence(self):
        fixture = json.loads((ROOT / 'eng/ci/release-test-verdict-gate.fixtures.json').read_text(encoding='utf-8'))
        self.assertEqual(2, len(fixture['jobs']))
        sha, run, jobs, api = self.full()
        collapsed = job_records(run, docs=True)
        for observed in fixture['jobs']:
            self.assertEqual('skipped', observed['conclusion'])
            self.assertEqual(1, len([job for job in collapsed if job['name'] == observed['name']]))
        self.assertEqual('DOCS', GATE.jobs_verdict(collapsed, run))
        collapsed.append(next(job for job in jobs if job['name'] == 'Unit Tests (core)'))
        with self.assertRaises(GATE.Refuse):
            GATE.jobs_verdict(collapsed, run)

    def test_nonempty_quarantine_requires_execution_and_malformed_population_refuses(self):
        sha, run, jobs, api = self.full()
        original = Path('eng/ci/flaky-tests-quarantine.json').read_text()
        try:
            for population in ([{'fullyQualifiedName':'Required.Test'}], {}, None):
                with self.subTest(population=population):
                    Path('eng/ci/flaky-tests-quarantine.json').write_text(json.dumps({'tests':population}))
                    GATE.git('add', 'eng/ci/flaky-tests-quarantine.json')
                    GATE.git('commit', '-qm', 'quarantine fixture')
                    candidate = GATE.git('rev-parse', 'HEAD').strip()
                    run = run_record(candidate)
                    jobs = job_records(run)
                    api = RawApi({candidate:[run]}, {(10,1):jobs})
                    self.check(candidate, api, 2)
                    if isinstance(population, list):
                        job = next(job for job in jobs if job['name'] == GATE.JOBS['flaky-tests-quarantine'][0])
                        job['steps'].append(dict(name='Run quarantined tests',status='completed',conclusion='skipped'))
                        self.check(candidate, api, 2)
                        job['steps'][-1]['conclusion'] = 'success'
                        self.check(candidate, api)
        finally:
            Path('eng/ci/flaky-tests-quarantine.json').write_text(original)
            GATE.git('add', 'eng/ci/flaky-tests-quarantine.json')
            GATE.git('commit', '-qm', 'restore fixture quarantine')


if __name__ == "__main__":
    unittest.main(verbosity=2)
