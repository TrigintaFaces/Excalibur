#!/usr/bin/env python3
"""Safety/liveness tests for per-invocation discovery/result binding."""
import copy
import importlib.util
import json
import re
from pathlib import Path
import tempfile
import unittest
import uuid

spec = importlib.util.spec_from_file_location("evidence", Path(__file__).with_name("required-test-evidence.py"))
gate = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gate)


class EvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="required-evidence-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.context = dict(candidateSha="a" * 40, runId="12", runAttempt="2", job="unit", shard="core", os="Linux", provider="none",
                            source="tests.slnf", sourceSha256="d" * 64, filter="")
        self.ids = [str(uuid.uuid4()), str(uuid.uuid4())]
        self.plan = {"schemaVersion": 1, "context": self.context, "invocations": [], "nonTestProjects": []}
        self.roster = {"schemaVersion": 1, "context": self.context, "entries": []}
        self.receipts = []
        self.add_invocation("first", self.ids)

    def write(self, name, value):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value), encoding="utf-8")
        return {"path": name, "sha256": gate.digest(path)}

    def add_invocation(self, name, ids, framework="net10.0"):
        settings = self.write(f"{name}/effective.settings", {"settings": "fixture"})
        census = {"schemaVersion": 1, "purpose": "execution", "context": dict(self.context, targetFramework=framework, targetPlatform="x64"),
                  "assembly": "A.Tests.dll", "tests": [{"id": value} for value in ids], "discoveredCount": len(ids),
                  "assemblySha256": "a" * 64, "adapterSha256": "b" * 64, "runSettingsSha256": settings["sha256"],
                  "filter": "", "inputBundle": [{"path": "A.Tests.dll", "sha256": "a" * 64}]}
        reference = self.write(f"{name}/discovery.json", census)
        invocation = dict(id=name, project=f"{name}.csproj", targetFramework=framework, targetPlatform="x64",
                          assembly="A.Tests.dll", census=reference, receipt=f"{name}/receipt.json")
        self.plan["invocations"].append(invocation)
        self.roster["entries"].append(dict(project=invocation["project"], targetFramework=framework,
                                            targetPlatform="x64", evaluatedIsTestProject="true", assembly="A.Tests.dll",
                                            effectiveSettings=settings))
        trx = self.root / name / "results.trx"
        definitions = ''.join(f'<UnitTest id="{value}" storage="A.Tests.dll"/>' for value in ids)
        results = ''.join(f'<UnitTestResult testId="{value}" outcome="Passed"/>' for value in ids)
        trx.write_text(f'<TestRun xmlns="{gate.NS["t"]}" id="{uuid.uuid4()}"><TestDefinitions>{definitions}</TestDefinitions>'
                       f'<Results>{results}</Results></TestRun>', encoding="utf-8")
        inputs = {key: census[key] for key in ("assemblySha256", "adapterSha256", "runSettingsSha256", "inputBundle")}
        receipt = dict(schemaVersion=1, invocationId=name, exitCode=0, disposition="executed",
                       censusSha256=reference["sha256"], before=copy.deepcopy(inputs), after=copy.deepcopy(inputs),
                       trx=[{"path": str(trx.relative_to(self.root)).replace('\\', '/'), "sha256": gate.digest(trx)}])
        self.receipts.append(receipt)
        return census, invocation, receipt

    def flush(self):
        self.plan["evaluatedRoster"] = self.write("evaluated-roster.json", self.roster)
        self.write("plan.json", self.plan)
        for invocation, receipt in zip(self.plan["invocations"], self.receipts):
            receipt["planSha256"] = gate.digest(self.root / "plan.json")
            self.write(invocation["receipt"], receipt)

    def verify(self):
        return gate.verify(self.root / "plan.json", self.context)

    def test_complete_pass(self):
        self.flush()
        self.assertEqual(self.verify()["executed"], 2)

    def test_external_source_roster_refuses_consistently_omitted_project(self):
        source = self.root / 'tests.slnf'
        source.write_text(json.dumps({'solution': {'path': 'tests.sln', 'projects': ['first.csproj', 'second.csproj']}}))
        expected = dict(self.context, sourceSha256=gate.digest(source))
        self.flush()
        # The internal artifact is entirely consistent and passes; the external source requires more.
        self.assertEqual(self.verify()['executed'], 2)
        with self.assertRaisesRegex(ValueError, 'differs from external source'):
            gate.verify_source_roster(self.plan, expected, self.root)
        self.add_invocation('second', self.ids)
        gate.verify_source_roster(self.plan, expected, self.root)

    def test_external_csproj_roster_and_source_hash(self):
        source = self.root / 'first.csproj'
        source.write_text('<Project />')
        expected = dict(self.context, source='first.csproj', sourceSha256=gate.digest(source))
        gate.verify_source_roster(self.plan, expected, self.root)
        self.plan['invocations'][0]['project'] = 'other.csproj'
        with self.assertRaisesRegex(ValueError, 'differs from external source'):
            gate.verify_source_roster(self.plan, expected, self.root)
        source.write_text('<Project Sdk="changed"/>')
        with self.assertRaisesRegex(ValueError, 'External source hash mismatch'):
            gate.verify_source_roster(self.plan, expected, self.root)

    def test_overlapping_shards_independently_pass(self):
        self.add_invocation("second", self.ids)
        self.flush()
        self.assertEqual(self.verify()["executed"], 4)

    def test_each_external_context_is_required(self):
        self.flush()
        for key in gate.CONTEXT:
            with self.subTest(key=key), self.assertRaises(ValueError):
                gate.verify(self.root / "plan.json", dict(self.context, **{key: "wrong"}))

    def test_evidence_from_an_earlier_attempt_of_the_same_run_is_accepted(self):
        # A partial re-run re-runs only the failed jobs, so every shard that already passed still
        # carries the attempt it ran in. Refusing those refuses every partial re-run, which makes a
        # single lost runner permanently red rather than retryable. The plan here records attempt 2
        # and the aggregation is asked for a later one, which is exactly that shape.
        self.flush()
        self.assertEqual(gate.verify(self.root / "plan.json", dict(self.context, runAttempt="7"))["executed"], 2)

    def test_evidence_from_a_later_attempt_than_this_aggregation_is_refused(self):
        # The bound must remain a bound. Evidence from an attempt that has not happened yet could only
        # mean artifact confusion, so this is what separates the fix from deleting the check.
        self.flush()
        with self.assertRaises(ValueError):
            gate.verify(self.root / "plan.json", dict(self.context, runAttempt="1"))

    def test_missing_receipt(self):
        self.flush()
        (self.root / self.plan["invocations"][0]["receipt"]).unlink()
        with self.assertRaises(ValueError): self.verify()

    def test_failed_runner_after_passing_results(self):
        self.receipts[0]["exitCode"] = 1
        self.flush()
        with self.assertRaises(ValueError): self.verify()

    def test_changed_execution_inputs(self):
        for field in ("assemblySha256", "adapterSha256", "runSettingsSha256", "inputBundle"):
            with self.subTest(field=field):
                original = self.receipts[0]["after"][field]
                self.receipts[0]["after"][field] = "changed"
                self.flush()
                with self.assertRaises(ValueError): self.verify()
                self.receipts[0]["after"][field] = original

    def test_identity_substitution_with_equal_counts(self):
        census_path = self.root / self.plan["invocations"][0]["census"]["path"]
        census = gate.read_json(census_path)
        census["tests"][0]["id"] = str(uuid.uuid4())
        self.plan["invocations"][0]["census"] = self.write(census_path.relative_to(self.root).as_posix(), census)
        self.receipts[0]["censusSha256"] = gate.digest(census_path)
        self.flush()
        with self.assertRaisesRegex(ValueError, "identity mismatch"): self.verify()

    def test_passing_sibling_cannot_cover_missing_result(self):
        self.add_invocation("second", self.ids)
        self.receipts[1]["trx"] = []
        self.flush()
        with self.assertRaisesRegex(ValueError, "did not execute"): self.verify()

    def test_trx_reuse(self):
        self.add_invocation("second", self.ids)
        self.receipts[1]["trx"] = self.receipts[0]["trx"]
        self.flush()
        with self.assertRaisesRegex(ValueError, "reused"): self.verify()

    def test_required_skip(self):
        path = self.root / self.receipts[0]["trx"][0]["path"]
        path.write_text(path.read_text().replace('outcome="Passed"', 'outcome="NotExecuted"', 1))
        self.receipts[0]["trx"][0]["sha256"] = gate.digest(path)
        self.flush()
        with self.assertRaisesRegex(ValueError, "did not pass"): self.verify()

    def test_receipt_hash_binds_raw_results(self):
        self.flush()
        path = self.root / self.receipts[0]["trx"][0]["path"]
        path.write_text(path.read_text() + ' ')
        with self.assertRaisesRegex(ValueError, "hash mismatch"): self.verify()

    def test_plan_cannot_be_rewritten_after_execution(self):
        self.flush()
        self.plan["extra"] = "changed after execution"
        self.write("plan.json", self.plan)
        with self.assertRaisesRegex(ValueError, "another plan"): self.verify()

    def test_tfm_mismatch(self):
        self.plan["invocations"][0]["targetFramework"] = "net9.0"
        self.flush()
        with self.assertRaisesRegex(ValueError, "evaluated test project/TFM roster"): self.verify()

    def test_omitted_target_framework(self):
        omitted = copy.deepcopy(self.roster["entries"][0])
        omitted["targetFramework"] = "net9.0"
        self.roster["entries"].append(omitted)
        self.flush()
        with self.assertRaisesRegex(ValueError, "evaluated test project/TFM roster"): self.verify()

    def test_test_project_relabeled_as_helper(self):
        self.add_invocation("second", self.ids)
        self.plan["invocations"].pop()
        self.receipts.pop()
        self.plan["nonTestProjects"] = [{"project": "second.csproj", "evaluatedIsTestProject": "false"}]
        self.flush()
        with self.assertRaisesRegex(ValueError, "evaluated test project/TFM roster"): self.verify()

    def test_unclaimed_result(self):
        self.flush()
        (self.root / "unclaimed.trx").write_text('<TestRun/>')
        with self.assertRaisesRegex(ValueError, "Unclaimed"): self.verify()

    def test_empty_plan(self):
        self.plan["invocations"] = []
        self.flush()
        with self.assertRaisesRegex(ValueError, "Empty planned"): self.verify()

    def test_path_escape(self):
        self.plan["invocations"][0]["receipt"] = "../outside.json"
        self.flush()
        with self.assertRaises(ValueError): self.verify()

    def test_duplicate_json_key(self):
        self.flush()
        path = self.root / "plan.json"
        path.write_text(path.read_text().replace('"schemaVersion": 1', '"schemaVersion": 1, "schemaVersion": 1'))
        with self.assertRaisesRegex(ValueError, "Duplicate JSON"): self.verify()

    def test_filtered_zero_with_passing_sibling(self):
        census, invocation, receipt = self.add_invocation("empty", [])
        original = self.root / receipt["trx"][0]["path"]
        original.unlink()
        receipt["trx"] = []
        receipt["disposition"] = "filtered-zero"
        unfiltered = copy.deepcopy(census)
        unfiltered.update(purpose="source-presence", discoveredCount=1, tests=[{"id": str(uuid.uuid4())}])
        invocation["unfilteredCensus"] = self.write("empty/unfiltered.json", unfiltered)
        self.flush()
        self.assertEqual(self.verify()["executed"], 2)

    def test_source_presence_cannot_replace_execution_census(self):
        path = self.plan["invocations"][0]["census"]["path"]
        census = gate.read_json(self.root / path)
        census["purpose"] = "source-presence"
        reference = self.write(path, census)
        self.plan["invocations"][0]["census"] = reference
        self.receipts[0]["censusSha256"] = reference["sha256"]
        self.flush()
        with self.assertRaisesRegex(ValueError, "cannot certify"): self.verify()

    def test_unit_artifact_population(self):
        repo = self.root / "repo"
        (repo / ".github/workflows").mkdir(parents=True)
        workflow = Path(__file__).resolve().parents[2] / ".github/workflows/ci.yml"
        (repo / ".github/workflows/ci.yml").write_bytes(workflow.read_bytes())
        sources = [
            ("core", "Core"), ("transport", "Transport"), ("middleware", "Middleware"),
            ("excalibur-data", "Excalibur-Data"), ("excalibur-platform", "Excalibur-Platform"),
            ("excalibur-messaging", "Excalibur-Messaging"), ("async-risk-messaging", "Messaging"),
            ("async-risk-observability", "Observability"),
        ]
        rows = [(suffix, "unit-tests-async-risk" if suffix.startswith("async-risk") else "unit-tests", "Linux", source)
                for suffix, source in sources]
        rows += [(f"{platform}-{suffix}", f"unit-tests-{platform}" + ("-async-risk" if suffix == "async-risk" else ""), os_name, source)
                 for platform, os_name in (("windows", "Windows"), ("macos", "macOS"))
                 for suffix, source in (("deterministic", "Deterministic"), ("async-risk", "AsyncRisk"))]
        artifacts = self.root / "artifacts"
        original_root = self.root
        for suffix, job, os_name, source_name in rows:
            source = f"eng/ci/shards/UnitTests-{source_name}.slnf"
            path = repo / source
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(json.dumps({"solution": {"path": "../../../Excalibur.sln", "projects": ["first.csproj"]}}))
            self.context = dict(candidateSha="a"*40, runId="12", runAttempt="2", job=job,
                                shard=f"unit-tests-{suffix}", os=os_name, provider="in-process",
                                source=source, sourceSha256=gate.digest(path).upper(), filter="")
            self.root = artifacts / f"test-results-unit-{suffix}" / "evidence.required"
            self.root.mkdir(parents=True)
            self.plan = dict(schemaVersion=1, context=self.context, invocations=[], nonTestProjects=[])
            self.roster = dict(schemaVersion=1, context=self.context, entries=[])
            self.receipts = []
            self.add_invocation("first", self.ids)
            self.flush()
        self.root = original_root
        result = gate.verify_unit_artifacts(artifacts, repo, "a"*40, "12", "2", "")
        self.assertEqual(result, {"jobs": 12, "executed": 24})
        missing = artifacts / "test-results-unit-core"
        missing.rename(self.root / "withheld-core")
        with self.assertRaisesRegex(ValueError, "Missing or unexpected unit artifact context"):
            gate.verify_unit_artifacts(artifacts, repo, "a"*40, "12", "2", "")


class ProductionCallerTests(unittest.TestCase):
    """Lock the supported CI caller shape, not arbitrary PowerShell semantics or hosted success.

    The step name is a locator. Renaming or restructuring it requires reviewing this
    lock alongside the workflow. Exact leading statements prevent comments, quoted
    examples, conditionals, and overwritten exit codes from satisfying the lock.
    """

    prefix = """name: Verify honest aggregation (expected set compiled + ran, de-duped)
        shell: pwsh
        env:
          EXPECTED_SHA: ${{ github.event_name == 'pull_request' && github.event.pull_request.head.sha || github.sha }}
        run: |
          $filter = ./eng/ci/get-test-filter.ps1 -Mode Exclude -BaseFilter '(Category=Unit|Category=DependencyInjection)&Category!=Performance'
          python eng/ci/required-test-evidence.py --unit-artifacts AggregatedTrx `
            --candidate $env:EXPECTED_SHA --run-id $env:GITHUB_RUN_ID --attempt $env:GITHUB_RUN_ATTEMPT --filter $filter
          if ($LASTEXITCODE -ne 0) { throw 'Independent per-context test evidence failed.' }
"""

    @classmethod
    def setUpClass(cls):
        cls.workflow = (Path(__file__).resolve().parents[2] / '.github/workflows/ci.yml').read_text()
        release_spec = importlib.util.spec_from_file_location(
            'caller_release_gate', Path(__file__).with_name('release-test-verdict-gate.py'))
        cls.release = importlib.util.module_from_spec(release_spec)
        release_spec.loader.exec_module(cls.release)

    def check_caller(self, workflow):
        body = self.release.job_sections(workflow).get('unit-shard-aggregation', '')
        eligibility = re.findall(r'^    if: (.+)$', body, re.M)
        if eligibility != ["always() && needs.changes.outputs.docs_only != 'true'"]:
            raise ValueError('Production aggregation job eligibility changed')
        if re.search(r'^    continue-on-error:', body, re.M):
            raise ValueError('Production aggregation job must enforce failures')
        steps = re.split(r'(?m)^      - ', body)
        locator = self.prefix.splitlines()[0]
        callers = [step for step in steps if step.splitlines() and step.splitlines()[0] == locator]
        if len(callers) != 1 or not callers[0].startswith(self.prefix):
            raise ValueError('Production aggregation caller changed or is not executable in the supported shape')
        keys = re.findall(r'^        ([a-z][a-z-]*):', callers[0], re.M)
        if keys != ['shell', 'env', 'run']:
            raise ValueError('Unsupported or duplicate caller step keys')

    def test_current_ci_executes_bound_production_checker(self):
        self.check_caller(self.workflow)

    def test_refuse_nonexecuting_or_unbound_production_callers(self):
        command = '          python eng/ci/required-test-evidence.py --unit-artifacts AggregatedTrx `'
        fatal = "          if ($LASTEXITCODE -ne 0) { throw 'Independent per-context test evidence failed.' }"
        changes = {
            'removed': (command, ''),
            'commented': (command, command.replace('python', '# python', 1)),
            'quoted': (command, "          Write-Host 'python eng/ci/required-test-evidence.py --unit-artifacts AggregatedTrx'"),
            'self_test_only': (command, '          python eng/ci/required-test-evidence.py --self-test'),
            'early_return': (command, '          return\n' + command),
            'conditional': (command, '          if ($false) {\n' + command),
            'missing_failure_check': (fatal, ''),
            'overwritten_exit_code': (fatal, '          python -c "exit(0)"\n' + fatal),
            'unbound_candidate': ('--candidate $env:EXPECTED_SHA', '--candidate "fixture"'),
            'unbound_filter': ('--filter $filter', '--filter "Category=Unit"'),
            'disabled_step': ('        shell: pwsh\n        env:', '        if: false\n        shell: pwsh\n        env:'),
            'nonfatal_step': ('        shell: pwsh\n        env:', '        continue-on-error: true\n        shell: pwsh\n        env:'),
        }
        for name, (before, after) in changes.items():
            with self.subTest(name=name):
                self.assertIn(before, self.workflow)
                changed = self.workflow.replace(before, after, 1)
                with self.assertRaises(ValueError):
                    self.check_caller(changed)

    def test_refuse_duplicate_or_relocated_caller(self):
        locator = '      - ' + self.prefix.splitlines()[0] + '\n'
        self.assertIn(locator, self.workflow)
        for changed in (
            self.workflow.replace(locator, locator + '        run: echo duplicate\n' + locator, 1),
            self.workflow.replace('  unit-shard-aggregation:\n', '  replaced-aggregation:\n', 1),
        ):
            with self.assertRaises(ValueError):
                self.check_caller(changed)

    def test_refuse_job_level_bypasses(self):
        before = "    if: always() && needs.changes.outputs.docs_only != 'true'"
        self.assertIn(before, self.workflow)
        for after in ('    if: false', before + '\n    continue-on-error: true'):
            changed = self.workflow.replace(before, after, 1)
            with self.assertRaises(ValueError):
                self.check_caller(changed)

    def test_refuse_later_duplicate_or_disabled_step_keys(self):
        for extra in ('        if: false\n', '        run: echo bypass\n', '        env: {}\n'):
            changed = self.workflow.replace(self.prefix, self.prefix + extra, 1)
            with self.assertRaises(ValueError):
                self.check_caller(changed)

    def test_harness_executes_caller_lock(self):
        harness = (Path(__file__).with_name('harness-gates-ci.sh')).read_text()
        line = 'run "self-test: required-test-evidence" python3 eng/ci/required-test-evidence.test.py'

        def check_harness(text):
            if len(re.findall('^' + re.escape(line) + '$', text, re.M)) != 1:
                raise ValueError('Evidence caller lock is not invoked by the harness')

        check_harness(harness)
        for replacement in ('', '# ' + line):
            with self.assertRaises(ValueError):
                check_harness(harness.replace(line, replacement, 1))


if __name__ == '__main__':
    unittest.main()
