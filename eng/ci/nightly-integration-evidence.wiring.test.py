#!/usr/bin/env python3
"""Exercise nightly summary decisions and lock the shared evidence wiring."""
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = (ROOT / '.github/workflows/nightly.yml').read_text(encoding='utf-8-sig')


def job(name):
    match = re.search(r'^  ' + re.escape(name) + r':\n(.*?)(?=^  [a-z][\w-]*:|\Z)', WORKFLOW, re.M | re.S)
    if not match:
        raise AssertionError('Missing job: ' + name)
    return match[1]


def step(body, name):
    match = re.search(r'^      - name: ' + re.escape(name) + r'\n(.*?)(?=^      - |\Z)', body, re.M | re.S)
    if not match:
        raise AssertionError('Missing step: ' + name)
    return match[1]


class NightlyWiringTests(unittest.TestCase):
    def test_runner_failure_is_not_softened(self):
        body = job('integration-tests')
        self.assertNotIn('continue-on-error:', body)
        run = step(body, 'Run Integration Tests (-m:1)')
        self.assertIn('nightly-integration-evidence.ps1 -Mode Run', run)
        self.assertIn("-Source '${{ matrix.shard.filter }}' -Shard '${{ matrix.shard.name }}'", run)
        self.assertNotIn('non-fatal', body)

    def test_verification_is_independent_and_preserves_step_outcome(self):
        verify = step(job('integration-tests'), 'Verify integration test results')
        self.assertIn('if: always()', verify)
        self.assertIn('${{ steps.integration-tests.outcome }}', verify)
        self.assertIn('nightly-integration-evidence.ps1 -Mode Verify', verify)
        self.assertIn('-RunnerOutcome $env:INTEGRATION_RUN_OUTCOME', verify)
        script = (ROOT / 'eng/ci/nightly-integration-evidence.ps1').read_text()
        self.assertIn("'required-test-evidence.py'", script)
        self.assertIn("'validate-shard-results.ps1'", script)

    def test_evidence_is_fresh_and_complete(self):
        body = job('integration-tests')
        path = 'TestResults/nightly-${{ github.run_id }}-${{ github.run_attempt }}-${{ matrix.shard.name }}'
        self.assertIn(path + '/cosmos-execution-evidence.tsv', step(body, 'Run Integration Tests (-m:1)'))
        self.assertIn(path + '/cosmos-execution-evidence.tsv', step(body, 'Verify the emulator was actually reached'))
        upload = step(body, 'Upload test results')
        self.assertIn('if: always()', upload)
        self.assertIn('path: ' + path + '/**', upload)
        self.assertIn('if-no-files-found: error', upload)

    def test_both_declared_shards_remain(self):
        body = job('integration-tests')
        sources = re.findall(r'^            filter: (\S+)$', body, re.M)
        self.assertEqual(sources, ['eng/ci/shards/IntegrationTests-Dispatch.slnf', 'eng/ci/shards/IntegrationTests-Excalibur.slnf'])

    def test_final_job_verdict_rejects_failed_and_missing_status(self):
        body = step(job('nightly-summary'), 'Enforce nightly verdict')
        self.assertIn('if: always()', body)
        self.assertIn('${{ steps.results.outputs.status }}', body)
        script = '\n'.join(line[10:] for line in body.split('        run: |\n', 1)[1].splitlines() if line.startswith('          '))
        bash = str(Path(os.environ.get('ProgramFiles', 'C:/Program Files')) / 'Git/bin/bash.exe') if os.name == 'nt' else shutil.which('bash')
        for status, opt_out, succeeds in (('passed','false',True), ('failed','false',False), ('','false',False),
                                          ('incomplete','true',True), ('incomplete','false',False)):
            with self.subTest(status=status, opt_out=opt_out):
                result = subprocess.run([bash, '-c', script], env=dict(os.environ, NIGHTLY_STATUS=status, INTEGRATION_OPT_OUT=opt_out),
                                        capture_output=True, text=True, timeout=10)
                self.assertEqual(result.returncode == 0, succeeds, result.stdout + result.stderr)

    def test_summary_executes_actual_workflow_script(self):
        body = step(job('nightly-summary'), 'Compute nightly results').split('        run: |\n', 1)[1]
        template = '\n'.join(line[10:] for line in body.splitlines() if line.startswith('          '))
        bash = str(Path(os.environ.get('ProgramFiles', 'C:/Program Files')) / 'Git/bin/bash.exe') if os.name == 'nt' else shutil.which('bash')
        for integration, opt_out, expected in (
            ('success', 'false', 'passed'), ('skipped', 'false', 'failed'), ('skipped', 'true', 'incomplete'),
            ('failure', 'true', 'failed'), ('cancelled', 'false', 'failed'), ('', 'false', 'failed'),
        ):
            with self.subTest(integration=integration, opt_out=opt_out), tempfile.TemporaryDirectory() as directory:
                values = {
                    'needs.deterministic-unit-tests.result': 'success', 'needs.integration-tests.result': integration,
                    'needs.package-composition.result': 'success', 'needs.dependency-audit.result': 'success',
                    'needs.npm-audit.result': 'success', "github.event_name == 'workflow_dispatch' && inputs.skip_integration_tests": opt_out,
                }
                script = template
                for key, value in values.items():
                    script = script.replace('${{ ' + key + ' }}', value)
                self.assertNotIn('${{', script)
                output = Path(directory) / 'output'
                result = subprocess.run([bash, '-c', script], env=dict(os.environ, GITHUB_OUTPUT=output.as_posix(), INTEGRATION_OPT_OUT=opt_out),
                                        capture_output=True, text=True, timeout=10)
                self.assertEqual(result.returncode, 0, result.stderr)
                self.assertIn('status=' + expected + '\n', output.read_text())


if __name__ == '__main__':
    unittest.main()
