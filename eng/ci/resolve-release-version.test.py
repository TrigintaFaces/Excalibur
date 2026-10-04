#!/usr/bin/env python3
"""Execute actual workflow version steps with adversarial inputs and unchanged output controls."""
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest

import yaml

ROOT = Path(__file__).resolve().parents[2]
BASH = str(Path(os.environ.get('ProgramFiles', 'C:/Program Files')) / 'Git/bin/bash.exe') if os.name == 'nt' else shutil.which('bash')


def workflow_step(filename, name):
    document = yaml.safe_load((ROOT / '.github/workflows' / filename).read_text(encoding='utf-8-sig'))
    matches = [step for job in document['jobs'].values() for step in job.get('steps', []) if step.get('name') == name]
    if len(matches) != 1:
        raise AssertionError('Expected one workflow step: ' + name)
    return matches[0]


class VersionBoundaryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.steps = {
            'official': workflow_step('official-build.yml', 'Resolve the version to stamp'),
            'release': workflow_step('release.yml', 'Determine version'),
        }

    def invoke(self, mode, value, event='workflow_dispatch', ref='refs/heads/main', prerelease='false', success=True, expected=None):
        with tempfile.TemporaryDirectory(prefix='version-data-') as directory:
            output = Path(directory) / 'output'
            output.write_bytes(b'existing=untouched\n')
            marker = Path(directory) / 'injected'
            value = value.replace('@MARKER@', marker.as_posix())
            ref = ref.replace('@MARKER@', marker.as_posix())
            step = self.steps[mode]
            self.assertNotIn('${{', step['run'])
            self.assertEqual(step['env']['DISPATCH_VERSION'], '${{ inputs.version }}')
            if mode == 'release':
                self.assertEqual(step['env']['DISPATCH_PRERELEASE'], '${{ inputs.prerelease }}')
            env = dict(os.environ, GITHUB_OUTPUT=output.as_posix(), GITHUB_EVENT_NAME=event, GITHUB_REF=ref,
                       DISPATCH_VERSION=value, DISPATCH_PRERELEASE=prerelease)
            result = subprocess.run([BASH, '-c', step['run']], cwd=ROOT, env=env,
                                    capture_output=True, text=True, timeout=15)
            self.assertEqual(result.returncode == 0, success, result.stdout + result.stderr)
            self.assertFalse(marker.exists(), 'Input was executed as code')
            if success:
                self.assertEqual(output.read_text(encoding='utf-8-sig'), 'existing=untouched\n' + expected)
            else:
                self.assertEqual(output.read_bytes(), b'existing=untouched\n', 'Rejected input changed workflow outputs')

    def test_valid_versions_through_both_workflow_steps(self):
        for mode in self.steps:
            for version in ('0.0.0', '1.2.3', '12.34.56-alpha', '1.2.3-rc.1', '1.2.3-1', '1.2.3-01a', '1.2.3-alpha.0'):
                with self.subTest(mode=mode, version=version):
                    self.invoke(mode, version, expected=f'version={version}\nprerelease=false\n')

    def test_script_and_output_injection_is_inert(self):
        values = [
            '1.2.3"; touch "@MARKER@"; #', "1.2.3'; touch '@MARKER@'; #",
            '1.2.3$(touch "@MARKER@")', '1.2.3`touch "@MARKER@"`', '1.2.3;touch "@MARKER@"',
            '1.2.3\nprerelease=true', '1.2.3\r\nversion=9.9.9', '1.2.3\t', '1.2.3\x01',
            '1.2.3\n::error::forged', '${GITHUB_TOKEN}', '1.2.3"\n$(touch "@MARKER@")\n"',
        ]
        for mode in self.steps:
            for value in values:
                with self.subTest(mode=mode, value=value):
                    self.invoke(mode, value, success=False)
                    self.invoke(mode, '', event='push', ref='refs/tags/v' + value, success=False)

    def test_invalid_or_out_of_policy_versions(self):
        for version in ('1.2', 'v1.2.3', '01.2.3', '1.02.3', '1.2.03', '1.2.3-01', '1.2.3-alpha.01',
                        '1.2.3-', '1.2.3-alpha.', '1.2.3 alpha', ' 1.2.3', '1.2.3+build', '1.2.3-alpha.beta'):
            with self.subTest(version=version):
                self.invoke('release', version, success=False)

    def test_optional_official_and_required_release(self):
        self.invoke('official', '', expected='version=\nprerelease=false\n')
        self.invoke('official', '', event='push', expected='version=\nprerelease=false\n')
        self.invoke('release', '', success=False)
        self.invoke('release', '', event='push', success=False)

    def test_tag_precedence_and_numeric_prerelease(self):
        self.invoke('official', '9.9.9', ref='refs/tags/v1.2.3-1', expected='version=1.2.3-1\nprerelease=true\n')
        self.invoke('release', '9.9.9', ref='refs/tags/v1.2.3', prerelease='true', expected='version=9.9.9\nprerelease=true\n')
        for mode in self.steps:
            self.invoke(mode, '', event='push', ref='refs/tags/v1.2.3-1', expected='version=1.2.3-1\nprerelease=true\n')
            self.invoke(mode, '', event='push', ref='refs/tags/v1.2.3', expected='version=1.2.3\nprerelease=false\n')
            self.invoke(mode, '', event='push', ref='refs/tags/v', success=False)

    def test_invalid_event_or_boolean_refuses_without_outputs(self):
        self.invoke('release', '1.2.3', event='pull_request', success=False)
        self.invoke('official', '1.2.3', event='push', ref='invalid', success=False)
        self.invoke('release', '1.2.3', prerelease='false\nversion=9.9.9', success=False)

    def test_downstream_version_consumers_use_data(self):
        for filename in ('official-build.yml', 'release.yml'):
            document = yaml.safe_load((ROOT / '.github/workflows' / filename).read_text(encoding='utf-8-sig'))
            for job in document['jobs'].values():
                for step in job.get('steps', []):
                    self.assertNotRegex(step.get('run', ''), r'\$\{\{[^}]*outputs\.version[^}]*\}\}', step.get('name'))

    def test_composite_restore_build_preserve_one_literal_source_argument(self):
        for action in ('build-and-test', 'setup-dotnet-build'):
            document = yaml.safe_load((ROOT / '.github/actions' / action / 'action.yml').read_text(encoding='utf-8-sig'))
            for step in document['runs']['steps']:
                if step.get('name') not in ('Restore dependencies', 'Build'):
                    continue
                with self.subTest(action=action, step=step['name']), tempfile.TemporaryDirectory() as directory:
                    marker = Path(directory) / 'injected'
                    source = 'folder with spaces/$(touch "' + marker.as_posix() + '");\'project.slnf'
                    output = Path(directory) / 'argv'
                    script = 'dotnet() { printf "%s\\0" "$@" > "$ARGV_OUTPUT"; }\n' + step['run']
                    result = subprocess.run([BASH, '-c', script], cwd=ROOT,
                                            env=dict(os.environ, SOLUTION_FILTER=source, ARGV_OUTPUT=output.as_posix()),
                                            capture_output=True, text=True, timeout=15)
                    self.assertEqual(result.returncode, 0, result.stderr)
                    self.assertEqual(output.read_bytes().split(b'\0')[1].decode(), source)
                    self.assertFalse(marker.exists())

    def test_archive_upload_uses_literal_data_and_preserves_http_failure(self):
        step = workflow_step('release.yml', 'Upload release assets')
        self.assertNotIn('${{', step['run'])
        for code in (0, 22):
            with self.subTest(exit_code=code), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                output = root / 'argv'
                marker = root / 'injected'
                token = 'literal$(touch "' + marker.as_posix() + '")'
                script = 'tar() { :; }\ncurl() { printf "%s\\0" "$@" > "$ARGV_OUTPUT"; return "$CURL_EXIT"; }\n' + step['run']
                env = dict(os.environ, RELEASE_VERSION='1.2.3-rc.1', RELEASE_UPLOAD_URL='https://uploads.example/releases/1/assets{?name,label}',
                           GH_TOKEN=token, ARGV_OUTPUT=output.as_posix(), CURL_EXIT=str(code))
                result = subprocess.run([BASH, '-c', script], cwd=ROOT, env=env, capture_output=True, text=True, timeout=15)
                self.assertEqual(result.returncode, code)
                arguments = [value.decode() for value in output.read_bytes().split(b'\0') if value]
                self.assertIn('--fail-with-body', arguments)
                self.assertIn('Authorization: token ' + token, arguments)
                self.assertEqual(arguments[-1], 'https://uploads.example/releases/1/assets?name=excalibur-dispatch-v1.2.3-rc.1-packages.tar.gz&label=All+NuGet+Packages')
                self.assertFalse(marker.exists())

    def test_filter_environment_output_rejects_newlines(self):
        document = yaml.safe_load((ROOT / '.github/actions/build-and-test/action.yml').read_text(encoding='utf-8-sig'))
        step = next(s for s in document['runs']['steps'] if s.get('name') == 'Set default test filter')
        for value, succeeds in [('Category=Unit', True), ('Category=Unit\nINJECTED=yes', False), ('Unit\rBAD=yes', False)]:
            with self.subTest(value=value), tempfile.TemporaryDirectory() as directory:
                output = Path(directory) / 'env'
                output.write_bytes(b'existing=untouched\n')
                result = subprocess.run([BASH, '-c', step['run']], cwd=ROOT,
                                        env=dict(os.environ, INPUT_TEST_FILTER=value, GITHUB_ENV=output.as_posix()),
                                        capture_output=True, text=True, timeout=15)
                self.assertEqual(result.returncode == 0, succeeds)
                self.assertEqual(output.read_text(encoding='utf-8-sig'), 'existing=untouched\n' + (f'TEST_FILTER={value}\n' if succeeds else ''))

    def test_composite_powershell_passes_literal_parameters_to_entrypoint(self):
        document = yaml.safe_load((ROOT / '.github/actions/build-and-test/action.yml').read_text(encoding='utf-8-sig'))
        step = next(s for s in document['runs']['steps'] if s.get('name') == 'Run tests')
        self.assertNotIn('${{', step['run'])
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'eng').mkdir()
            (root / 'eng/build.ps1').write_text('''param([switch]$Test,[switch]$NoRestore,[switch]$NoBuild,
[string]$Project,[string]$ResultsDirectory,[string]$ResultsPrefix,[string]$BlameTimeout,[string]$RequiredTestContext,
[string]$TestFilter,[switch]$Coverage,[string]$MaxCpuCount,[string]$TestSessionTimeout,[string]$ExtraRunSettings)
$PSBoundParameters | ConvertTo-Json | Set-Content -LiteralPath $env:PARAMETER_OUTPUT
''')
            source = root / 'fixture.slnf'
            source.write_text('{}')
            marker = root / 'injected'
            payload = '$([IO.File]::WriteAllText(\'' + marker.as_posix() + "','injected'))"
            output = root / 'parameters.json'
            script = root / 'actual-action.ps1'
            script.write_text(step['run'])
            head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
            env = dict(os.environ, EVIDENCE_SHA=head, EVIDENCE_SOURCE=str(source), EVIDENCE_SHARD='fixture',
                       EVIDENCE_PROVIDER='in-process', RUNNER_TEMP=str(root), GITHUB_WORKSPACE=str(root),
                       GITHUB_RUN_ID='control', GITHUB_RUN_ATTEMPT='1', GITHUB_JOB='fixture', RUNNER_OS='Windows' if os.name == 'nt' else 'Linux',
                       TEST_FILTER=payload, TEST_BLAME_TIMEOUT='5m', TEST_COLLECT_COVERAGE='true', TEST_MAX_CPU_COUNT='1',
                       TEST_SESSION_TIMEOUT='1000', TEST_EXTRA_RUN_SETTINGS=payload, PARAMETER_OUTPUT=str(output))
            result = subprocess.run(['pwsh', '-NoProfile', '-File', str(script)], cwd=ROOT, env=env,
                                    capture_output=True, text=True, timeout=30)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            import json
            parameters = json.loads(output.read_text(encoding='utf-8-sig'))
            self.assertEqual(parameters['Project'], str(source))
            self.assertEqual(parameters['TestFilter'], payload)
            self.assertEqual(parameters['ExtraRunSettings'], payload)
            self.assertEqual(parameters['MaxCpuCount'], '1')
            self.assertEqual(parameters['TestSessionTimeout'], '1000')
            self.assertTrue(parameters['Coverage']['IsPresent'])
            self.assertFalse(marker.exists())


if __name__ == '__main__':
    unittest.main()
