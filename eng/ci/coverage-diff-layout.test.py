#!/usr/bin/env python3
"""Exercise the real coverage-diff checkout layout and preflight without a build."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

import yaml


ROOT = Path(__file__).resolve().parents[2]
STEPS = yaml.safe_load((ROOT / '.github/workflows/coverage.yml').read_text(encoding='utf-8'))['jobs']['coverage-diff']['steps']


def step(name):
    return next(item for item in STEPS if item['name'] == name)


class CoverageDiffLayout(unittest.TestCase):
    def test_exact_candidate_precedes_nested_base_checkout(self):
        candidate = step('Checkout candidate helpers')
        base = step('Checkout base branch')
        self.assertLess(STEPS.index(candidate), STEPS.index(base))
        self.assertNotIn('path', candidate['with'])
        self.assertEqual(candidate['with']['ref'], '${{ inputs.checkout-ref }}')
        self.assertEqual(base['with']['path'], 'base')
        self.assertEqual(base['with']['ref'], '${{ inputs.pr-base-sha }}')

    def test_sdk_and_helper_select_different_correct_snapshots(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'base').mkdir()
            (root / 'global.json').write_text(json.dumps({'sdk': {'version': 'candidate-sdk'}}))
            (root / 'base/global.json').write_text(json.dumps({'sdk': {'version': 'base-sdk'}}))
            sdk = root / step('Setup .NET')['with']['global-json-file']
            self.assertEqual(json.loads(sdk.read_text())['sdk']['version'], 'base-sdk')
            test = step('Run tests with coverage (base)')
            self.assertEqual(test['working-directory'], 'base')
            self.assertIn('bash ../eng/ci/run-filtered-tests.sh', test['run'])
            self.assertIn('--no-build', test['run'])
            self.assertEqual((root / test['working-directory'] / '../eng/ci/run-filtered-tests.sh').resolve(),
                             root / 'eng/ci/run-filtered-tests.sh')

    def run_preflight(self, missing=None):
        bash = Path('C:/Program Files/Git/bin/bash.exe') if os.name == 'nt' else shutil.which('bash')
        self.assertTrue(bash, 'bash is required to execute the actual workflow preflight')
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for relative in ('base/global.json', 'eng/ci/run-filtered-tests.sh'):
                if relative != missing:
                    path = root / relative
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_text('{}' if relative.endswith('.json') else '#!/bin/bash\n')
            summary = root / 'summary.txt'
            env = dict(os.environ, GITHUB_STEP_SUMMARY=summary.as_posix())
            # Avoid repository mutations. Distinct checkout identities exercise both real calls.
            git = '''git() {
              case "$*" in
                "rev-parse HEAD") printf '%s\\n' candidate-sha ;;
                "-C base rev-parse HEAD") printf '%s\\n' base-sha ;;
                *) return 97 ;;
              esac
            }
            '''
            result = subprocess.run([str(bash), '-c', git + step('Verify coverage checkout layout')['run']],
                                    cwd=root, env=env, capture_output=True, text=True, timeout=20)
            return result, summary.read_text() if summary.exists() else ''

    def test_preflight_records_both_checkout_identities(self):
        result, summary = self.run_preflight()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('Coverage candidate: candidate-sha', summary)
        self.assertIn('Coverage base: base-sha', summary)

    def test_missing_base_sdk_fails_before_claiming_comparison(self):
        result, summary = self.run_preflight('base/global.json')
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertEqual(summary, '')

    def test_missing_candidate_helper_fails_before_claiming_comparison(self):
        result, summary = self.run_preflight('eng/ci/run-filtered-tests.sh')
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertEqual(summary, '')


if __name__ == '__main__':
    unittest.main()
