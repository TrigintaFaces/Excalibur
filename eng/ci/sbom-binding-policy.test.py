#!/usr/bin/env python3
"""Synthetic parser policy and actual Linux streaming-owner controls; not crypto verification."""
import copy
import importlib.util
import json
import os
from pathlib import Path
import sys
sys.dont_write_bytecode = True
import tempfile
import unittest
from unittest import mock

spec = importlib.util.spec_from_file_location('sbom_binding', Path(__file__).with_name('verify-sbom-binding.py'))
policy = importlib.util.module_from_spec(spec)
spec.loader.exec_module(policy)
IDENTITY = {'repo': 'fixture-owner/repository', 'sha': 'a' * 40, 'run': '123', 'attempt': '2'}
SUBJECTS = {'A.nupkg': 'b' * 64, 'B.nupkg': 'c' * 64}
BOM = {'bomFormat': 'CycloneDX', 'specVersion': '1.6', 'serialNumber': 'urn:uuid:fixture',
       'components': [{'name': 'A', 'version': '1'}, {'name': 'B', 'version': '1'}], 'unknown': {'must': 'remain'}}


def row():
    workflow = 'https://github.com/fixture-owner/repository/.github/workflows/official-build.yml@refs/heads/main'
    return {'attestation': {'fixture': 'NOT ACTUAL CRYPTO'}, 'verificationResult': {
        'statement': {'_type': policy.STATEMENT, 'predicateType': policy.CYCLONE,
                      'subject': [{'name': n, 'digest': {'sha256': d}} for n, d in SUBJECTS.items()],
                      'predicate': copy.deepcopy(BOM)},
        'signature': {'certificate': {'subjectAlternativeName': workflow, 'buildSignerURI': workflow,
                    'buildConfigURI': workflow, 'issuer': 'https://token.actions.githubusercontent.com',
                    'sourceRepositoryURI': 'https://github.com/fixture-owner/repository',
                    'sourceRepositoryDigest': IDENTITY['sha'],
                    'runInvocationURI': 'https://github.com/fixture-owner/repository/actions/runs/123/attempts/2'}}}}


class BindingPolicyControls(unittest.TestCase):
    def check(self, rows):
        return policy.matching_row(rows, policy.CYCLONE, SUBJECTS, IDENTITY, BOM)

    def test_same_immutable_bom_snapshot_binds_parse_and_digest_under_replacement(self):
        with tempfile.TemporaryDirectory() as temporary:
            bom = Path(temporary) / 'bom.json'
            original = json.dumps(BOM).encode()
            replacement = original + b'\n'
            bom.write_bytes(original)
            real_parser = policy.strict_json
            def replace_after_read(raw):
                self.assertEqual(raw, original)
                bom.write_bytes(replacement)
                return real_parser(raw)
            with mock.patch.object(policy, 'strict_json', side_effect=replace_after_read):
                with self.assertRaisesRegex(policy.Refusal, 'immutable parsed snapshot'):
                    policy.bom_snapshot(bom)
            bom.write_bytes(original)
            parsed, expected = policy.bom_snapshot(bom)
            self.assertTrue(policy.json_equal(parsed, BOM))
            self.assertEqual(expected, policy.hashlib.sha256(original).hexdigest())
            bom.write_bytes(replacement)
            with self.assertRaises(policy.Refusal): policy.require_bom_binding(bom, expected)

    def test_exact_bom_inventory_refuses_unsigned_sidecars_nested_paths_and_symlinks(self):
        for kind in ('json', 'xml', 'nested', 'symlink'):
            with self.subTest(kind=kind), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                bom = root / 'bom.json'
                bom.write_bytes(json.dumps(BOM).encode())
                policy.bom_snapshot(bom)  # Valid exact producer population remains usable.
                if kind == 'nested': (root / 'nested').mkdir()
                elif kind == 'symlink':
                    try: (root / 'extra.json').symlink_to(bom)
                    except OSError as error:
                        self.fail('SETUP: symlink mutation requires supported owner platform: ' + str(error))
                else: (root / ('unsigned.' + kind)).write_bytes(b'unsigned')
                with self.assertRaises(policy.Refusal): policy.bom_snapshot(bom)

    def test_bom_reader_is_bounded_even_if_file_size_changes(self):
        with tempfile.TemporaryDirectory() as temporary:
            bom = Path(temporary) / 'bom.json'
            bom.write_bytes(b'x' * 33)
            with mock.patch.object(policy, 'BOM_LIMIT', 32):
                with self.assertRaisesRegex(policy.Refusal, 'bounded'):
                    policy.bom_snapshot(bom)

    def test_exact_complete_verified_row_is_accepted(self):
        self.assertEqual(self.check([row()]), 0)

    def test_do_not_blindly_select_first_or_merge_candidate_rows(self):
        wrong = row()
        wrong['verificationResult']['signature']['certificate']['sourceRepositoryDigest'] = 'd' * 40
        self.assertEqual(self.check([wrong, row()]), 1)
        second = row()
        second['verificationResult']['statement']['subject'] = second['verificationResult']['statement']['subject'][:1]
        with self.assertRaises(policy.Refusal): self.check([wrong, second])

    def test_wrong_identity_fields_are_independently_refused(self):
        for field in ('issuer', 'sourceRepositoryURI', 'sourceRepositoryDigest', 'runInvocationURI',
                      'subjectAlternativeName', 'buildSignerURI', 'buildConfigURI'):
            with self.subTest(field=field):
                value = row()
                value['verificationResult']['signature']['certificate'][field] = 'wrong'
                with self.assertRaises(policy.Refusal): self.check([value])
        for run in ('123/attempts/1', '124/attempts/2', '123/attempts/2/extra'):
            with self.subTest(run=run):
                value = row()
                value['verificationResult']['signature']['certificate']['runInvocationURI'] = 'https://github.com/fixture-owner/repository/actions/runs/' + run
                with self.assertRaises(policy.Refusal): self.check([value])

    def test_exact_subject_set_not_subset_union_or_normalized_path(self):
        for mutation in ('missing', 'extra', 'digest', 'duplicate', 'path', 'sha512'):
            with self.subTest(mutation=mutation):
                value = row()
                subjects = value['verificationResult']['statement']['subject']
                if mutation == 'missing': subjects.pop()
                elif mutation == 'extra': subjects.append({'name': 'C.nupkg', 'digest': {'sha256': 'e' * 64}})
                elif mutation == 'digest': subjects[0]['digest']['sha256'] = 'f' * 64
                elif mutation == 'duplicate': subjects.append(copy.deepcopy(subjects[0]))
                elif mutation == 'path': subjects[0]['name'] = '../' + subjects[0]['name']
                else: subjects[0]['digest'] = {'sha512': '0' * 128}
                with self.assertRaises(policy.Refusal): self.check([value])

    def test_entire_predicate_and_statement_profiles_are_binding(self):
        for mutation in ('component', 'unknown', 'array_order', 'type', 'predicate_type'):
            with self.subTest(mutation=mutation):
                value = row()
                statement = value['verificationResult']['statement']
                if mutation == 'component': statement['predicate']['components'][0]['version'] = '2'
                elif mutation == 'unknown': del statement['predicate']['unknown']
                elif mutation == 'array_order': statement['predicate']['components'].reverse()
                elif mutation == 'type': statement['_type'] = 'https://in-toto.io/Statement/v0.1'
                else: statement['predicateType'] = policy.SLSA
                with self.assertRaises(policy.Refusal): self.check([value])

    def test_json_types_do_not_conflate_boolean_and_numeric_values(self):
        self.assertFalse(policy.json_equal({'value': True}, {'value': 1}))
        self.assertFalse(policy.json_equal([False], [0]))
        self.assertTrue(policy.json_equal(policy.strict_json(b'{"n":1.0}'), {'n': 1}))
        self.assertFalse(policy.json_equal(policy.strict_json(b'{"n":1.00000000000000000001}'), {'n': 1}))
        self.assertFalse(policy.json_equal({'n': 9007199254740993}, {'n': 9007199254740992}))

    def test_raw_bom_digest_is_required_even_when_whole_json_semantics_match(self):
        value = row()
        statement = value['verificationResult']['statement']
        statement['predicateType'] = policy.SLSA
        statement['subject'].append({'name': 'bom.json', 'digest': {'sha256': 'd' * 64}})
        expected = dict(SUBJECTS, **{'bom.json': 'e' * 64})
        with self.assertRaises(policy.Refusal):
            policy.matching_row([value], policy.SLSA, expected, IDENTITY)

    def test_fake_wrappers_and_missing_verified_fields_refused(self):
        for value in ({'attestation': {'bundle': {'statement': row()}}}, {'verificationResult': {}}, {}):
            with self.subTest(value=value):
                with self.assertRaises(policy.Refusal): self.check([value])
        with self.assertRaises(policy.Refusal): self.check([])

    def test_strict_json_refuses_duplicate_nonfinite_and_invalid_utf8(self):
        for value in (b'{"a":1,"a":2}', b'{"a":NaN}', b'{"a":Infinity}', b'\xff'):
            with self.subTest(value=value):
                with self.assertRaises((policy.Refusal, UnicodeDecodeError)): policy.strict_json(value)
        number = policy.strict_json(b'{"exact":9007199254740993,"decimal":1.25}')
        self.assertEqual(number['exact'], 9007199254740993)
        self.assertEqual(str(number['decimal']), '1.25')


class LinuxStreamingOwnerControls(unittest.TestCase):
    def invoke(self, body, seconds=2):
        self.assertEqual(sys.platform, 'linux', 'Owner controls require actual production OS; Windows parser pass is not Linux ownership proof')
        directory = Path(self.directory)
        return policy.capture([sys.executable, '-u', '-c', body], directory, 'child', seconds)

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='sbom-owner-')
        self.addCleanup(self.temporary.cleanup)
        self.directory = self.temporary.name

    def test_symlinked_subject_is_refused_before_opening(self):
        self.assertEqual(sys.platform, 'linux')
        original = Path(self.directory) / 'subject'
        original.write_bytes(b'owned')
        alias = Path(self.directory) / 'alias'
        alias.symlink_to(original)
        with self.assertRaisesRegex(policy.Refusal, 'Symlink/reparse'):
            policy.regular(alias)

    def test_output_budget_is_enforced_while_streaming_before_capture(self):
        before = policy.OUTPUT_LIMIT
        policy.OUTPUT_LIMIT = 128
        try:
            with self.assertRaises(policy.Refusal): self.invoke("print('x'*1024)")
        finally: policy.OUTPUT_LIMIT = before
        self.assertLessEqual((Path(self.directory) / 'child.stdout.json').stat().st_size, 128)

    def test_stderr_budget_is_enforced_before_capture(self):
        before = policy.STDERR_LIMIT
        policy.STDERR_LIMIT = 128
        try:
            with self.assertRaises(policy.Refusal): self.invoke("import sys;print('x'*1024,file=sys.stderr)")
        finally: policy.STDERR_LIMIT = before
        self.assertLessEqual((Path(self.directory) / 'child.stderr.log').stat().st_size, 128)

    def test_nonzero_cannot_supply_plausible_verified_json(self):
        with self.assertRaises(policy.Refusal): self.invoke("import sys;print('[]',flush=True);sys.exit(7)")
        receipt = json.loads((Path(self.directory) / 'child.process.json').read_text())
        self.assertEqual(receipt['exit'], 7)
        self.assertTrue(receipt['cleanupVerified'])

    def test_timeout_retains_partial_log_and_verified_cleanup(self):
        with self.assertRaises(policy.Refusal): self.invoke("import time;print('BEFORE',flush=True);time.sleep(30)", 2)
        self.assertIn('BEFORE', (Path(self.directory) / 'child.stdout.json').read_text())
        receipt = json.loads((Path(self.directory) / 'child.process.json').read_text())
        self.assertTrue(receipt['timedOut'])
        self.assertTrue(receipt['cleanupVerified'])

    def test_exited_parent_cannot_leave_stdout_holding_orphan(self):
        pidfile = str(Path(self.directory) / 'orphan.pid')
        child = f"import os,time;from pathlib import Path;Path({pidfile!r}).write_text(str(os.getpid()));print('CHILD',flush=True);time.sleep(30)"
        body = ("import subprocess,sys,time;from pathlib import Path;"
                f"subprocess.Popen([sys.executable,'-u','-c',{child!r}]);p=Path({pidfile!r});end=time.monotonic()+1;"
                "\nwhile not p.exists() and time.monotonic()<end:time.sleep(.01)"
                "\nassert p.exists();print('PARENT',flush=True)")
        output = self.invoke(body)
        pid = int(Path(pidfile).read_text())
        self.assertFalse(Path(f'/proc/{pid}').exists())
        self.assertIn(b'PARENT', output)
        self.assertTrue(json.loads((Path(self.directory) / 'child.process.json').read_text())['cleanupVerified'])


if __name__ == '__main__':
    unittest.main()
