# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
"""Isolated candidate controls. Root must run only after the serialized CI slot is free.

Fixtures initialize/stage files in temporary repositories; they never create commits,
modify the real checkout, invoke its gates, publish, or change host settings.
"""
import contextlib
import importlib.util
import io
import hashlib
import json
import os
from pathlib import Path
import shlex
import shutil
import subprocess
import sys
import tempfile
import time
import unittest
from unittest.mock import patch

import yaml

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('candidate_release_gates', HERE / 'pre-release-gates.py')
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class Fidelity(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='release-controls-')
        self.root = Path(self.temporary.name).resolve()
        subprocess.run(['git', 'init', '-q', str(self.root)], check=True, capture_output=True)
        (self.root / '.github/workflows').mkdir(parents=True)
        (self.root / 'eng/ci').mkdir(parents=True)
        (self.root / '.gitignore').write_text('artifacts/\n', encoding='utf-8')
        (self.root / 'eng/ci/tool.py').write_text('pass\n', encoding='utf-8')
        self.output = self.root / 'artifacts/results'
        self.platform = runner.local_platform()
        self.runs_on = {'windows': 'windows-latest', 'linux': 'ubuntu-latest', 'macos': 'macos-latest'}[self.platform]
        self.python = shlex.quote(Path(sys.executable).as_posix())

    def tearDown(self):
        self.temporary.cleanup()

    def workflow(self, steps, workflow_env=None, **job_fields):
        body = {'name': 'fixture', 'on': ['push'], 'jobs': {'check': {
            'runs-on': self.runs_on, **job_fields, 'steps': steps}}}
        if workflow_env is not None:
            body['env'] = workflow_env
        (self.root / '.github/workflows/ci.yml').write_text(yaml.safe_dump(body, sort_keys=False), encoding='utf-8')
        subprocess.run(['git', 'add', '.'], cwd=self.root, check=True, capture_output=True)
        return runner.discover(self.root)

    def bash(self):
        if os.name == 'nt':
            path = Path(os.environ.get('ProgramFiles', 'C:/Program Files')) / 'Git/bin/bash.exe'
            if not path.is_file():
                self.skipTest('Git for Windows Bash is unavailable; these controls are not passes.')
            return str(path)
        result = shutil.which('bash')
        if not result:
            self.skipTest('Bash unavailable; these controls are not passes.')
        return result

    def step(self, body, identity=None, shell='bash'):
        value = {'shell': shell, 'run': body}
        if identity:
            value['id'] = identity
        return value

    def plan(self, inventory, effects=None, timeout=10):
        snapshot = runner.content_snapshot(self.root)
        return {'schema': 1, 'steps': {entry['id']: {
            'fingerprint': entry['fingerprint'], 'input_digest': snapshot['digest'],
            'effects': effects or ['test'], 'reviewed_transitive_effects': True,
            'runner_compatibility': 'same-os', 'timeout_seconds': timeout,
            'input_artifacts': [], 'outputs': [],
        } for entry in inventory}}

    def execute(self, inventory, plan=None, selected=None, shell=None):
        return runner.execute(self.root, inventory, selected or [entry['id'] for entry in inventory],
                              plan or self.plan(inventory), self.output, shell or self.bash())

    def test_default_inventory_does_not_execute_a_gate(self):
        self.workflow([self.step('mkdir -p artifacts; touch artifacts/sentinel; python eng/ci/tool.py')])
        with contextlib.redirect_stdout(io.StringIO()) as output:
            code = runner.main(['--root', str(self.root)])
        self.assertEqual(code, 0)
        self.assertEqual(json.loads(output.getvalue())['verification'], 'NOT_RUN')
        self.assertFalse((self.root / 'artifacts/sentinel').exists())

    def test_multiline_bash_setup_and_continuation_are_preserved(self):
        (self.root / 'eng/ci/tool.py').write_text(
            'import sys\nfrom pathlib import Path\nPath("artifacts/observed").write_text(sys.argv[1])\n', encoding='utf-8')
        block = '\n'.join(['TOKEN="whole argument"', 'mkdir -p artifacts',
                           f'{self.python} eng/ci/tool.py ' + '\\', '  "$TOKEN"', ''])
        inventory = self.workflow([self.step(block)])
        self.assertEqual(inventory[0]['run'], block)
        code, report = self.execute(inventory)
        self.assertEqual(code, 0)
        self.assertEqual((self.root / 'artifacts/observed').read_text(), 'whole argument')
        self.assertEqual(report['results'][0]['outcome'], 'PASS')

    def test_multiline_powershell_and_native_exit_wrapper(self):
        pwsh = shutil.which('pwsh')
        if not pwsh:
            self.skipTest('PowerShell unavailable; these controls are not passes.')
        python_literal = Path(sys.executable).as_posix().replace("'", "''")
        body = "$x = 'whole argument'\nNew-Item -ItemType Directory -Force artifacts | Out-Null\n"
        body += f"& '{python_literal}' eng/ci/tool.py `\n  $x\n"
        (self.root / 'eng/ci/tool.py').write_text('import sys\nraise SystemExit(17)\n', encoding='utf-8')
        inventory = self.workflow([self.step(body, shell='pwsh')])
        code, report = self.execute(inventory, shell=pwsh)
        self.assertEqual(code, 1)
        self.assertEqual(report['results'][0]['process']['exit_code'], 17)

    def test_distinct_identical_steps_are_not_deduplicated(self):
        inventory = self.workflow([self.step('python eng/ci/tool.py'), self.step('python eng/ci/tool.py')])
        self.assertEqual(len(inventory), 2)
        self.assertNotEqual(inventory[0]['id'], inventory[1]['id'])
        self.assertEqual(inventory[1]['prior_steps'][0]['id'], inventory[0]['id'])

    def test_advisory_and_matrix_context_stays_in_inventory(self):
        step = self.step('python eng/ci/tool.py || true')
        step['continue-on-error'] = True
        inventory = self.workflow([step], strategy={'matrix': {'runtime': ['a', 'b']}})
        self.assertTrue(inventory[0]['continue_on_error'])
        self.assertEqual(inventory[0]['strategy']['matrix']['runtime'], ['a', 'b'])
        with self.assertRaisesRegex(runner.Refuse, 'Matrix'):
            self.execute(inventory)

    def test_selection_order_cannot_reorder_workflow_steps(self):
        (self.root / 'eng/ci/tool.py').write_text(
            'import sys\nfrom pathlib import Path\np=Path("artifacts/order")\np.parent.mkdir(exist_ok=True)\n'
            'with p.open("a") as f: f.write(sys.argv[1])\n', encoding='utf-8')
        inventory = self.workflow([self.step(f'{self.python} eng/ci/tool.py first', 'first'),
                                   self.step(f'{self.python} eng/ci/tool.py second', 'second')])
        code, report = self.execute(inventory, selected=[entry['id'] for entry in reversed(inventory)])
        self.assertEqual(code, 0)
        self.assertEqual((self.root / 'artifacts/order').read_text(), 'firstsecond')
        self.assertEqual([row['id'] for row in report['results']], [row['id'] for row in inventory])

    def test_unresolved_later_context_refuses_before_first_sentinel(self):
        inventory = self.workflow([self.step('mkdir -p artifacts; touch artifacts/sentinel; python eng/ci/tool.py'),
                                   self.step('python eng/ci/tool.py ${{ matrix.value }}')])
        with self.assertRaisesRegex(runner.Refuse, 'Unresolved GitHub'):
            self.execute(inventory)
        self.assertFalse((self.root / 'artifacts/sentinel').exists())

    def test_unsupported_later_shell_refuses_before_first_sentinel(self):
        inventory = self.workflow([self.step('mkdir -p artifacts; touch artifacts/sentinel; python eng/ci/tool.py'),
                                   self.step('python eng/ci/tool.py', shell='bash {0}')])
        with self.assertRaisesRegex(runner.Refuse, 'Unsupported shell'):
            self.execute(inventory)
        self.assertFalse((self.root / 'artifacts/sentinel').exists())

    def test_forbidden_publication_refuses_before_execution(self):
        inventory = self.workflow([self.step('mkdir -p artifacts; touch artifacts/sentinel; python eng/ci/tool.py')])
        with self.assertRaisesRegex(runner.Refuse, 'Effects'):
            self.execute(inventory, self.plan(inventory, effects=['publish']))
        self.assertFalse((self.root / 'artifacts/sentinel').exists())

    def test_a_deliberately_failing_gate_is_not_green(self):
        inventory = self.workflow([self.step(f'{self.python} eng/ci/tool.py\nexit 17')])
        code, report = self.execute(inventory)
        self.assertEqual(code, 1)
        self.assertEqual(report['results'][0]['process']['exit_code'], 17)
        self.assertEqual(report['results'][0]['outcome'], 'FAIL')

    def test_initially_dirty_content_mutation_is_detected(self):
        source = self.root / 'input.txt'
        source.write_text('committed shape', encoding='utf-8')
        inventory = self.workflow([self.step(f'{self.python} eng/ci/tool.py')])
        source.write_text('initially dirty', encoding='utf-8')
        (self.root / 'eng/ci/tool.py').write_text('from pathlib import Path\nPath("input.txt").write_text("changed again")\n', encoding='utf-8')
        code, report = self.execute(inventory)
        self.assertEqual(code, 1)
        self.assertIn('input.txt', report['results'][0]['input_changes']['changed'])

    def test_deleted_and_new_source_files_are_detected(self):
        (self.root / 'input.txt').write_text('input', encoding='utf-8')
        (self.root / 'eng/ci/tool.py').write_text(
            'from pathlib import Path\nPath("input.txt").unlink()\nPath("new-source.cs").write_text("new")\n', encoding='utf-8')
        inventory = self.workflow([self.step(f'{self.python} eng/ci/tool.py')])
        code, report = self.execute(inventory)
        self.assertEqual(code, 1)
        changes = report['results'][0]['input_changes']
        self.assertIn('input.txt', changes['removed'])
        self.assertIn('new-source.cs', changes['added'])

    def test_stale_source_review_refuses_before_side_effect(self):
        inventory = self.workflow([self.step('mkdir -p artifacts; touch artifacts/sentinel; python eng/ci/tool.py')])
        plan = self.plan(inventory)
        (self.root / 'eng/ci/tool.py').write_text('print("different implementation")\n', encoding='utf-8')
        with self.assertRaisesRegex(runner.Refuse, 'source content'):
            self.execute(inventory, plan)
        self.assertFalse((self.root / 'artifacts/sentinel').exists())

    def test_missing_declared_input_artifact_refuses_before_side_effect(self):
        inventory = self.workflow([self.step('mkdir -p artifacts; touch artifacts/sentinel; python eng/ci/tool.py')])
        plan = self.plan(inventory)
        plan['steps'][inventory[0]['id']]['input_artifacts'] = [
            {'path': 'artifacts/missing.json', 'sha256': '0' * 64, 'assertion': 'required coverage report'}]
        with self.assertRaisesRegex(runner.Refuse, 'unavailable'):
            self.execute(inventory, plan)
        self.assertFalse((self.root / 'artifacts/sentinel').exists())

    def test_ignored_artifact_and_preparation_evidence_are_rechecked_at_consumption(self):
        proof = self.root / 'artifacts/proof.txt'
        proof.parent.mkdir(parents=True)
        marker = self.root / 'artifacts/consumed'
        (self.root / 'eng/ci/tool.py').write_text(
            'import sys\nfrom pathlib import Path\n'
            'if sys.argv[1] == "produce": Path("artifacts/proof.txt").write_text("new")\n'
            'else: Path("artifacts/consumed").write_text("consumed")\n', encoding='utf-8')
        inventory = self.workflow([self.step(f'{self.python} eng/ci/tool.py produce', 'producer'),
                                   self.step(f'{self.python} eng/ci/tool.py consume', 'consumer')])
        for kind in ('input_artifacts', 'prerequisites'):
            with self.subTest(kind=kind):
                proof.write_bytes(b'old')
                marker.unlink(missing_ok=True)
                plan = self.plan(inventory)
                evidence = {'path': 'artifacts/proof.txt', 'sha256': hashlib.sha256(b'old').hexdigest(),
                            'assertion': 'consumer must observe this reviewed proof'}
                consumer = plan['steps'][inventory[1]['id']]
                if kind == 'input_artifacts':
                    consumer[kind] = [evidence]
                else:
                    consumer[kind] = {'ignored-preparation': evidence}
                with self.assertRaisesRegex(runner.Refuse, 'does not match'):
                    self.execute(inventory, plan)
                self.assertEqual(proof.read_text(), 'new', 'the producer must actually change the ignored proof')
                self.assertFalse(marker.exists(), 'consumer executed with stale reviewed proof')
                report = json.loads((self.output / 'receipt.json').read_text())
                self.assertEqual(report['state'], 'REFUSED')
                self.assertEqual([row['id'] for row in report['results']], [inventory[0]['id']])
                self.assertEqual(report['active_step']['id'], inventory[1]['id'])
                self.assertFalse(report['all_discovered_steps_passed_locally'])

    def test_old_pass_is_replaced_after_preflight_or_snapshot_refusal(self):
        inventory = self.workflow([self.step(f'{self.python} eng/ci/tool.py')])
        for failure in ('preflight', 'snapshot'):
            with self.subTest(failure=failure):
                plan = self.plan(inventory)
                code, old = self.execute(inventory, plan)
                self.assertEqual(code, 0, 'positive control: a real previous PASS must exist')
                self.assertEqual(old['state'], 'PASSED')
                if failure == 'preflight':
                    plan['steps'][inventory[0]['id']]['fingerprint'] = '0' * 64
                    injected = contextlib.nullcontext()
                else:
                    injected = patch.object(runner, 'content_snapshot', side_effect=runner.Refuse('injected snapshot failure'))
                with injected, self.assertRaises(runner.Refuse):
                    self.execute(inventory, plan)
                current = json.loads((self.output / 'receipt.json').read_text())
                self.assertNotEqual(current['run_id'], old['run_id'])
                self.assertEqual(current['state'], 'REFUSED')
                self.assertEqual(current['results'], [])
                self.assertFalse(current['all_discovered_steps_passed_locally'])

    def test_cli_plan_parse_refusal_cannot_leave_an_old_pass_receipt(self):
        inventory = self.workflow([self.step(f'{self.python} eng/ci/tool.py')])
        code, old = self.execute(inventory)
        self.assertEqual(code, 0)
        bad_plan = self.root / 'artifacts/bad-plan.json'
        bad_plan.write_text('{invalid', encoding='utf-8')
        with contextlib.redirect_stderr(io.StringIO()):
            code = runner.main(['--root', str(self.root), '--execute', '--step', inventory[0]['id'],
                                '--plan', str(bad_plan), '--output', str(self.output)])
        self.assertEqual(code, 2)
        current = json.loads((self.output / 'receipt.json').read_text())
        self.assertNotEqual(current['run_id'], old['run_id'])
        self.assertEqual(current['state'], 'REFUSED')
        self.assertFalse(current['all_discovered_steps_passed_locally'])

    def test_old_pass_is_replaced_by_incremental_results_and_midrun_failure(self):
        (self.root / 'eng/ci/tool.py').write_text(
            'import json,sys\nfrom pathlib import Path\n'
            'if sys.argv[1] == "second":\n'
            '    receipt=json.loads(Path("artifacts/results/receipt.json").read_text())\n'
            '    assert receipt["state"] == "RUNNING"\n'
            '    assert len(receipt["results"]) == 1\n'
            '    assert not receipt["all_discovered_steps_passed_locally"]\n'
            '    if Path("artifacts/fail-second").exists(): raise SystemExit(17)\n', encoding='utf-8')
        inventory = self.workflow([self.step(f'{self.python} eng/ci/tool.py first', 'first'),
                                   self.step(f'{self.python} eng/ci/tool.py second', 'second')])
        plan = self.plan(inventory)
        code, old = self.execute(inventory, plan)
        self.assertEqual(code, 0)
        (self.root / 'artifacts/fail-second').write_text('fail', encoding='utf-8')
        code, current = self.execute(inventory, plan)
        self.assertEqual(code, 1)
        self.assertNotEqual(current['run_id'], old['run_id'])
        self.assertEqual(current['state'], 'FAILED')
        self.assertEqual([row['outcome'] for row in current['results']], ['PASS', 'FAIL'])
        self.assertEqual(current['results'][1]['process']['exit_code'], 17)
        self.assertFalse(current['all_discovered_steps_passed_locally'])
        self.assertEqual(json.loads((self.output / 'receipt.json').read_text()), current)

    def test_environment_layer_precedence_matches_the_actual_platform(self):
        (self.root / 'eng/ci/tool.py').write_text(
            'import json,os\nfrom pathlib import Path\n'
            'Path("artifacts").mkdir(exist_ok=True)\n'
            'Path("artifacts/environment.json").write_text(json.dumps('
            '{key:value for key,value in os.environ.items() if key.upper() == "Q2DRUF_CASE"}))\n', encoding='utf-8')
        step = self.step(f'{self.python} eng/ci/tool.py')
        step['env'] = {'q2druf_case': 'step'}
        inventory = self.workflow([step], workflow_env={'q2druf_case': 'workflow'}, env={'Q2DRUF_CASE': 'job'})
        with patch.dict(os.environ, {'Q2DRUF_CASE': 'inherited'}):
            plan = self.plan(inventory)
            snapshot = runner.content_snapshot(self.root)
            _, environment, _, _ = runner.validate(inventory[0], plan['steps'][inventory[0]['id']],
                                                 self.root, snapshot, [inventory[0]['id']])
            code, _ = self.execute(inventory, plan)
        self.assertEqual(code, 0)
        actual = json.loads((self.root / 'artifacts/environment.json').read_text())
        if os.name == 'nt':
            self.assertEqual({key:value for key,value in environment.items() if key.upper() == 'Q2DRUF_CASE'},
                             {'Q2DRUF_CASE': 'step'}, 'the native Windows block must not contain case-duplicate names')
            self.assertEqual(actual, {'Q2DRUF_CASE': 'step'})
        else:
            self.assertEqual(actual, {'Q2DRUF_CASE': 'job', 'q2druf_case': 'step'})

    def test_missing_declared_output_cannot_count_as_a_pass(self):
        inventory = self.workflow([self.step(f'{self.python} eng/ci/tool.py')])
        plan = self.plan(inventory)
        plan['steps'][inventory[0]['id']]['outputs'] = ['artifacts/report.json']
        code, report = self.execute(inventory, plan)
        self.assertEqual(code, 1)
        self.assertEqual(report['results'][0]['missing_or_stale_outputs'], ['artifacts/report.json'])

    def test_partial_selection_is_not_all_gates_passed(self):
        inventory = self.workflow([self.step(f'{self.python} eng/ci/tool.py', 'first'),
                                   self.step(f'{self.python} eng/ci/tool.py', 'second')])
        code, report = self.execute(inventory, selected=[inventory[0]['id']])
        self.assertEqual(code, 4)
        self.assertEqual(report['outstanding'], [inventory[1]['id']])
        self.assertFalse(report['all_discovered_steps_passed_locally'])

    def test_timeout_terminates_owned_child_before_it_writes(self):
        self.output.mkdir(parents=True)
        helper = self.root / 'eng/ci/hang.py'
        marker = self.output / 'escaped-child'
        helper.write_text(
            'import subprocess,sys,time\n'
            'subprocess.Popen([sys.executable,"-c",sys.argv[1]])\n'
            'time.sleep(30)\n', encoding='utf-8')
        child = f'import time; from pathlib import Path; time.sleep(2); Path({str(marker)!r}).write_text("escaped")'
        result = runner.run_owned([sys.executable, str(helper), child], self.root,
                                  os.environ.copy(), 0.5, self.output / 'timeout.log')
        self.assertTrue(result['timed_out'])
        self.assertEqual(result['exit_code'], 124)
        time.sleep(2.2)
        self.assertFalse(marker.exists(), 'a child survived the owned execution timeout')

    def test_successful_primary_does_not_leave_a_background_child(self):
        self.output.mkdir(parents=True)
        helper = self.root / 'eng/ci/spawn.py'
        marker = self.output / 'escaped-after-success'
        helper.write_text('import subprocess,sys\nsubprocess.Popen([sys.executable,"-c",sys.argv[1]])\n', encoding='utf-8')
        child = f'import time; from pathlib import Path; time.sleep(2); Path({str(marker)!r}).write_text("escaped")'
        result = runner.run_owned([sys.executable, str(helper), child], self.root,
                                  os.environ.copy(), 10, self.output / 'success.log')
        self.assertFalse(result['timed_out'])
        self.assertEqual(result['exit_code'], 0)
        time.sleep(2.2)
        self.assertFalse(marker.exists(), 'a successful primary leaked an owned background child')

    def test_malformed_workflow_is_refused_not_silently_omitted(self):
        (self.root / '.github/workflows/bad.yml').write_text('jobs: [broken', encoding='utf-8')
        with self.assertRaisesRegex(runner.Refuse, 'Cannot parse'):
            runner.discover(self.root)

    def test_empty_workflow_population_refuses(self):
        with self.assertRaisesRegex(runner.Refuse, 'No workflow'):
            runner.discover(self.root)


if __name__ == '__main__':
    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(Fidelity))
    # A host missing a tested shell has not passed this complete instrument acceptance suite.
    raise SystemExit(0 if result.wasSuccessful() and not result.skipped else 3)
