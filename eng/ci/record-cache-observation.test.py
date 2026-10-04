#!/usr/bin/env python3
"""Cache restore evidence controls, including failure and repeated invocation paths."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import shutil
from unittest.mock import patch

import yaml

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('cache_observation', Path(__file__).with_name('record-cache-observation.py'))
recorder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(recorder)
SHA = 'a' * 40


def environment():
    return dict(GITHUB_REPOSITORY='owner/repo', GITHUB_RUN_ID='123', GITHUB_RUN_ATTEMPT='2',
                GITHUB_JOB='test', GITHUB_SHA='b' * 40, CI_CACHE_ACTION='build-and-test',
                CI_CACHE_KEY='Linux-nuget-key', RUNNER_OS='Linux',
                CI_CACHE_OUTCOME='success', CI_CACHE_CONCLUSION='success', CI_CACHE_HIT='true')


class ObservationTests(unittest.TestCase):
    def test_restore_states_do_not_invent_fallback_or_save(self):
        for raw, expected in [('true', 'exact'), ('false', 'non-exact'), ('', 'unknown')]:
            with self.subTest(raw=raw):
                result = recorder.observation(dict(environment(), CI_CACHE_HIT=raw), SHA, 'id')
                self.assertEqual(expected, result['restore_state'])
                self.assertEqual('not-observed', result['save_state'])
                self.assertEqual(raw, result['raw_cache_hit'])

    def test_failed_soft_failed_cancelled_skipped_are_unknown(self):
        for outcome, conclusion in [('failure', 'failure'), ('failure', 'success'),
                                    ('cancelled', 'cancelled'), ('skipped', 'skipped')]:
            with self.subTest(outcome=outcome, conclusion=conclusion):
                result = recorder.observation(dict(environment(), CI_CACHE_OUTCOME=outcome,
                                                     CI_CACHE_CONCLUSION=conclusion), SHA, 'id')
                self.assertEqual('unknown', result['restore_state'])
                self.assertEqual(outcome, result['outcome'])
                self.assertEqual(conclusion, result['conclusion'])

    def test_source_event_and_matrix_provenance(self):
        env = dict(environment(), CI_CACHE_MATRIX='{"os":"linux","shard":2}', SECRET='not-recorded')
        result = recorder.observation(env, SHA, 'id')
        self.assertEqual(SHA, result['source_sha'])
        self.assertEqual('b' * 40, result['event_sha'])
        self.assertEqual({'os': 'linux', 'shard': 2}, result['matrix_context'])
        self.assertNotIn('not-recorded', json.dumps(result))
        self.assertIsNone(recorder.observation(environment(), SHA, 'id')['matrix_context'])

    def test_malformed_metadata_refused(self):
        for key, value in [('GITHUB_RUN_ID', '0'), ('GITHUB_RUN_ATTEMPT', 'true'),
                           ('GITHUB_REPOSITORY', 'repo'), ('GITHUB_SHA', 'bad'),
                           ('CI_CACHE_OUTCOME', ''), ('CI_CACHE_HIT', 'TRUE'),
                           ('CI_CACHE_MATRIX', '[]'), ('CI_CACHE_MATRIX', '{'), ('CI_CACHE_KEY', '')]:
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                recorder.observation(dict(environment(), **{key: value}), SHA, 'id')
        with self.assertRaises(ValueError):
            recorder.observation(environment(), 'bad', 'id')

    def test_repeated_cli_invocations_produce_distinct_readable_artifacts(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / 'output.txt'
            env = dict(os.environ, **environment(), RUNNER_TEMP=directory, GITHUB_OUTPUT=str(output))
            for _ in range(2):
                run = subprocess.run([os.sys.executable, str(Path(recorder.__file__))], env=env,
                                     cwd=ROOT, capture_output=True, text=True, timeout=30)
                self.assertEqual(0, run.returncode, run.stderr)
            pairs = output.read_text(encoding='utf-8').splitlines()
            identities = [line[3:] for line in pairs if line.startswith('id=')]
            paths = [Path(line[5:]) for line in pairs if line.startswith('path=')]
            self.assertEqual(2, len(set(identities)))
            self.assertEqual(2, len(set(paths)))
            for identity, path in zip(identities, paths):
                record = json.loads(path.read_text(encoding='utf-8'))
                self.assertEqual(identity, record['invocation_id'])
                self.assertEqual('exact', record['restore_state'])

    def test_output_failure_is_not_success(self):
        with tempfile.TemporaryDirectory() as directory:
            env = dict(environment(), RUNNER_TEMP=directory, GITHUB_OUTPUT=directory)
            with patch.dict(os.environ, env, clear=True), patch.object(recorder.subprocess, 'run') as git:
                git.return_value.stdout = SHA
                self.assertEqual(2, recorder.main())

    def test_git_timeout_is_not_success(self):
        with patch.object(recorder.subprocess, 'run', side_effect=subprocess.TimeoutExpired('git', 15)):
            self.assertEqual(2, recorder.main())

    def test_action_wiring_preserves_required_work_and_failure(self):
        for name in ('build-and-test', 'setup-dotnet-build'):
            with self.subTest(action=name):
                action = yaml.safe_load((ROOT / '.github/actions' / name / 'action.yml').read_text())
                steps = action['runs']['steps']
                record, upload = steps[-2:]
                cache = next(step for step in steps if step.get('id') == 'nuget-cache')
                request = next(step for step in steps if step.get('id') == 'cache-request')
                self.assertLess(steps.index(request), steps.index(cache))
                self.assertEqual('${{ steps.cache-request.outputs.primary-key }}', cache['with']['key'])
                self.assertEqual('always()', record['if'])
                self.assertNotIn('continue-on-error', record)
                self.assertNotIn('continue-on-error', upload)
                self.assertEqual('python3 eng/ci/record-cache-observation.py', record['run'])
                self.assertEqual(cache['with']['key'], record['env']['CI_CACHE_KEY'])
                self.assertEqual(cache['with']['restore-keys'].strip(), record['env']['CI_CACHE_RESTORE_KEYS'])
                self.assertEqual('${{ steps.nuget-cache.outcome }}', record['env']['CI_CACHE_OUTCOME'])
                self.assertEqual('${{ steps.nuget-cache.conclusion }}', record['env']['CI_CACHE_CONCLUSION'])
                self.assertEqual('${{ steps.nuget-cache.outputs.cache-hit }}', record['env']['CI_CACHE_HIT'])
                self.assertEqual("always() && steps.cache-observation.outputs.id != ''", upload['if'])
                self.assertIn('steps.cache-observation.outputs.id', upload['with']['name'])
                self.assertEqual('error', upload['with']['if-no-files-found'])
                self.assertTrue(any(step['name'] == 'Build' for step in steps[:-2]))
                if name == 'build-and-test':
                    self.assertTrue(any(step['name'] == 'Run tests' for step in steps[:-2]))

    def test_workspace_mutation_cannot_change_captured_request(self):
        bash = 'C:/Program Files/Git/bin/bash.exe' if os.name == 'nt' else shutil.which('bash')
        self.assertTrue(bash, 'Bash is required for the actual composite command control')
        for name in ('build-and-test', 'setup-dotnet-build'):
            action = yaml.safe_load((ROOT / '.github/actions' / name / 'action.yml').read_text())
            request = next(step for step in action['runs']['steps'] if step.get('id') == 'cache-request')
            with tempfile.TemporaryDirectory() as directory:
                output = Path(directory) / 'output'
                env = dict(os.environ, PRIMARY_KEY='Linux-nuget-original', RESTORE_KEY='Linux-nuget-',
                           GITHUB_OUTPUT=output.as_posix())
                run = subprocess.run([bash, '-c', request['run']], env=env, cwd=directory,
                                     capture_output=True, text=True, timeout=15)
                self.assertEqual(0, run.returncode, run.stderr)
                frozen = dict(line.split('=', 1) for line in output.read_text().splitlines())
                (Path(directory) / 'CensusFixture.csproj').write_text('<Project/>')
                result = recorder.observation(dict(environment(), CI_CACHE_KEY=frozen['primary-key'],
                    CI_CACHE_RESTORE_KEYS=frozen['restore-key']), SHA, 'id')
                self.assertEqual('Linux-nuget-original', result['primary_key'])
                self.assertEqual(['Linux-nuget-'], result['restore_keys'])


if __name__ == '__main__':
    unittest.main()
