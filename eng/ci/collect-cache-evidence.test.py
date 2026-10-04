#!/usr/bin/env python3
"""Adversarial controls for hosted cache collection and its trust boundaries."""
import base64
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location('collector', Path(__file__).with_name('collect-cache-evidence.py'))
collector = importlib.util.module_from_spec(spec)
spec.loader.exec_module(collector)
REPO = 'owner/repo'
PREFIX = f'repos/{REPO}/actions/runs/123'
IDENTITY = '1'*32


def context():
    return dict(schema=1, repository=REPO, run_id=123, attempt=2, source_sha='a'*40,
                workflow_sha='b'*40, event_sha='c'*40, event='pull_request',
                workflow_path='.github/workflows/ci.yml', basis='independent run context capture', reviewer='build-owner')


def archive(payload, filename=IDENTITY+'.json', extra=None):
    output = io.BytesIO()
    with zipfile.ZipFile(output, 'w', compression=zipfile.ZIP_DEFLATED) as z:
        z.writestr(filename, payload)
        if extra:
            z.writestr(*extra)
    return output.getvalue()


class FakeGitHub:
    def __init__(self):
        self.calls = []
        self.run_reads = self.artifact_reads = 0
        self.workflow = b'''jobs:
  tests:
    name: Tests
    steps:
      - uses: ./.github/actions/build-and-test
        with:
          solution-filter: tests.slnf
          telemetry-matrix: '{}'
'''
        self.run = dict(id=123, run_attempt=2, head_sha='a'*40, event='pull_request', status='completed',
                        conclusion='success', updated_at='2026-10-03T01:00:00Z', path='.github/workflows/ci.yml',
                        repository={'id': 42, 'full_name': REPO}, head_repository={'id': 99})
        self.job = dict(id=17, run_id=123, run_attempt=2, head_sha='a'*40, name='Tests', status='completed',
                        conclusion='success', steps=[{'name': 'Cache NuGet packages'}])
        self.observation = dict(context(), schema=1, kind='cache-restore', invocation_id=IDENTITY,
                                job_key='tests', action='build-and-test', matrix_context={},
                                solution_filter='tests.slnf', outcome='success', conclusion='success',
                                raw_cache_hit='true', restore_state='exact', save_state='not-observed')
        self.payload = archive(json.dumps(self.observation))
        self.artifact = dict(id=27, name=f'ci-cache-123-2-{IDENTITY}', expired=False,
            workflow_run={'id':123, 'head_sha':'a'*40, 'repository_id':42, 'head_repository_id':99})
        self.refresh_digest()
        self.artifacts = [self.artifact]
        self.run_after = None
        self.artifacts_after = None
        self.bad_blob = False

    def refresh_digest(self):
        self.artifact.update(size_in_bytes=len(self.payload), digest='sha256:'+hashlib.sha256(self.payload).hexdigest())

    def json(self, endpoint):
        self.calls.append(endpoint)
        if endpoint == PREFIX:
            self.run_reads += 1
            return copy.deepcopy(self.run_after if self.run_reads > 1 and self.run_after is not None else self.run)
        if endpoint == PREFIX+'/attempts/2/jobs?per_page=100&page=1':
            return {'jobs':[copy.deepcopy(self.job)]}
        if endpoint == PREFIX+'/artifacts?per_page=100&page=1':
            self.artifact_reads += 1
            return {'artifacts':copy.deepcopy(self.artifacts_after if self.artifact_reads > 1 and self.artifacts_after is not None else self.artifacts)}
        if endpoint.startswith(f'repos/{REPO}/contents/'):
            path, commit = endpoint.split('/contents/', 1)[1].split('?ref=')
            if path == '.github/workflows/ci.yml':
                assert commit == 'b'*40
                raw = self.workflow
            else:
                assert commit == 'a'*40
                raw = (collector.ROOT/path).read_bytes()
                if self.bad_blob:
                    raw += b'\n# altered executable source contract\n'
            return dict(type='file', path=path, size=len(raw), encoding='base64',
                        content=base64.b64encode(raw).decode(),
                        sha=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest())
        raise AssertionError('unexpected endpoint '+endpoint)

    def raw(self, endpoint, limit):
        self.calls.append(endpoint)
        assert endpoint == f'repos/{REPO}/actions/artifacts/27/zip'
        return self.payload


class CollectorTests(unittest.TestCase):
    def test_complete_collection_binds_separate_revisions_and_fork_ids(self):
        client = FakeGitHub()
        evidence = collector.collect(client, context(), {})
        self.assertEqual('measured', evidence['status'])
        self.assertEqual(100, evidence['measurement']['exact_restore_rate'])
        self.assertEqual(2, client.run_reads)
        self.assertEqual(2, client.artifact_reads)
        self.assertIn('/attempts/2/jobs', ' '.join(client.calls))
        self.assertEqual(client.payload, base64.b64decode(evidence['downloads'][0]['archive_base64']))

    def test_missing_artifact_is_unknown_not_zero(self):
        client = FakeGitHub()
        client.artifacts = []
        result = collector.collect(client, context(), {})
        self.assertEqual('unmeasurable', result['status'])
        self.assertIsNone(result['measurement']['exact_restore_rate'])
        self.assertEqual(1, result['measurement']['missing_observations'])

    def test_expired_missing_digest_wrong_origin_and_size_refuse(self):
        mutations = [('expired',True), ('digest',None), ('size_in_bytes',collector.MAX_ARCHIVE+1),
                     ('workflow_run', {'id':123,'head_sha':'a'*40,'repository_id':42,'head_repository_id':42}),
                     ('name','ci-cache-124-2-'+IDENTITY)]
        for key,value in mutations:
            client = FakeGitHub()
            client.artifact[key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                collector.collect(client, context(), {})

    def test_stale_run_and_mid_collection_rerun_refuse(self):
        for field, value in [('run_attempt',3), ('head_sha','d'*40), ('status','in_progress'),
                             ('updated_at','2026-10-03T02:00:00Z')]:
            client = FakeGitHub()
            client.run_after = dict(client.run, **{field:value})
            with self.subTest(field=field), self.assertRaises(ValueError):
                collector.collect(client, context(), {})

    def test_artifact_deletion_addition_and_mutation_refuse(self):
        for kind in ('delete','add','modify'):
            client = FakeGitHub()
            if kind == 'delete':
                client.artifacts_after = []
            elif kind == 'add':
                client.artifacts_after = [client.artifact, dict(client.artifact,id=28,name='ci-cache-123-2-'+'2'*32)]
            else:
                client.artifacts_after = [dict(client.artifact,digest='sha256:'+'0'*64)]
            with self.subTest(kind=kind), self.assertRaises(ValueError):
                collector.collect(client, context(), {})

    def test_other_attempt_artifacts_do_not_fill_missing_current_evidence(self):
        client = FakeGitHub()
        client.artifact['name'] = 'ci-cache-123-1-'+IDENTITY
        result = collector.collect(client, context(), {})
        self.assertEqual('unmeasurable', result['status'])

    def test_digest_corruption_refuses_before_parsing(self):
        client = FakeGitHub()
        client.payload = b'not a ZIP'
        with self.assertRaisesRegex(ValueError,'size mismatch|digest mismatch'):
            collector.collect(client,context(),{})

    def test_zip_extra_path_oversize_symlink_and_duplicate_json_refuse(self):
        candidates = [archive('{}','../'+IDENTITY+'.json'), archive('{}',extra=('extra.txt','x')),
                      archive('x'*(collector.MAX_PAYLOAD+1)), archive('{"x":1,"x":2}'), b'not zip']
        stream = io.BytesIO()
        with zipfile.ZipFile(stream,'w') as z:
            entry = zipfile.ZipInfo(IDENTITY+'.json')
            entry.external_attr = 0o120777 << 16
            z.writestr(entry, 'target')
        candidates.append(stream.getvalue())
        for payload in candidates:
            client = FakeGitHub()
            client.payload = payload
            client.refresh_digest()
            with self.subTest(size=len(payload)), self.assertRaises((ValueError,zipfile.BadZipFile)):
                collector.collect(client,context(),{})

    def test_source_contract_change_refuses(self):
        client = FakeGitHub()
        client.bad_blob = True
        with self.assertRaisesRegex(ValueError,'contract differs'):
            collector.collect(client,context(),{})

    def test_pagination_duplicate_or_later_failure_cannot_return_partial_data(self):
        class Pages:
            def __init__(self, fail): self.fail = fail
            def json(self, endpoint):
                if 'page=2' in endpoint and self.fail:
                    raise ValueError('later failure')
                return {'jobs':[{'id':i} for i in range(1,101)]}
        for fail in (True,False):
            with self.assertRaises(ValueError):
                collector.paged(Pages(fail),'endpoint','jobs')

    def test_actual_streamed_byte_ceiling_and_nonzero_exit(self):
        actual_popen = subprocess.Popen
        for source in ('import sys; sys.stdout.buffer.write(b"x"*1000000)', 'raise SystemExit(3)'):
            with patch.object(collector.subprocess,'Popen',side_effect=lambda argv, **kwargs:actual_popen([sys.executable,'-c',source],**kwargs)):
                with self.assertRaises(ValueError):
                    collector.GitHub(10).raw('endpoint',limit=1024)
        with patch.object(collector.subprocess,'Popen',side_effect=lambda argv, **kwargs:actual_popen([sys.executable,'-c','print("ok")'],**kwargs)):
            self.assertEqual(b'ok',collector.GitHub(10).raw('endpoint',limit=1024).strip())

    def test_deadline_prevents_request(self):
        client = collector.GitHub(1)
        client.deadline = 0
        with patch.object(collector.subprocess,'Popen') as process, self.assertRaisesRegex(ValueError,'deadline'):
            client.raw('endpoint')
        process.assert_not_called()

    def test_cli_retains_refusal_and_context_basis(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory)/'context.json'
            target = Path(directory)/'evidence.json'
            source.write_text(json.dumps(context()))
            client = FakeGitHub()
            client.artifact['expired'] = True
            with patch.object(sys,'argv',['collector','--context',str(source),'--evidence-file',str(target)]), patch.object(collector,'GitHub',return_value=client):
                self.assertEqual(2,collector.main())
            result = json.loads(target.read_text())
            self.assertEqual('refused',result['status'])
            self.assertIn('expired',result['reason'])
            self.assertEqual(context()['basis'],result['context']['basis'])
            self.assertIn('producer assertions',result['identity_basis'])
            self.assertNotIn('measurement',result)
            self.assertEqual(True, result['raw_artifacts_before'][0]['expired'])

    def test_malformed_nested_objects_retain_main_level_refusal(self):
        cases = [('repository', None), ('repository', []), ('head_repository', None),
                 ('head_repository', []), ('path', None), ('workflow_run', None), ('workflow_run', [])]
        for field, value in cases:
            with self.subTest(field=field, value=value), tempfile.TemporaryDirectory() as directory:
                source = Path(directory)/'context.json'
                target = Path(directory)/'evidence.json'
                source.write_text(json.dumps(context()))
                client = FakeGitHub()
                if field == 'workflow_run':
                    client.artifact[field] = value
                else:
                    client.run[field] = value
                with patch.object(sys,'argv',['collector','--context',str(source),'--evidence-file',str(target)]), patch.object(collector,'GitHub',return_value=client):
                    self.assertEqual(2,collector.main())
                result = json.loads(target.read_text())
                self.assertEqual('refused',result['status'])
                if field == 'workflow_run':
                    self.assertEqual(value, result['raw_artifacts_before'][0][field])
                else:
                    self.assertEqual(value, result['run_before'][field])


if __name__ == '__main__':
    unittest.main()
