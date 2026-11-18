#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
"""Fail-closed controls for strict publication and historical diagnostic recognition."""
import copy
import importlib.util
import json
from pathlib import Path
import tempfile
from unittest.mock import patch
import unittest

SPEC = importlib.util.spec_from_file_location('gate', Path(__file__).with_name('inline-native-aot-verdict.py'))
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)


class VerdictTests(unittest.TestCase):
    def setUp(self):
        self.baseline = json.loads(GATE.BASELINE.read_text())
        self.log = 'Generating native code\n' + '\n'.join(self.baseline['diagnostics'])
        self.assets = {'libraries': {name + '/' + version: {} for name, version in self.baseline['packages'].items()}}
        self.properties = copy.deepcopy(self.baseline['properties'])

    def check(self, log=None, exit_code=1, executable=False):
        return GATE.classify(self.log if log is None else log, exit_code, self.assets,
                             self.properties, executable, self.baseline)

    def test_complete_historical_failure_is_recognized_but_not_admitted(self):
        result = self.check()
        self.assertEqual('known-upstream-failure', result['decision'])
        self.assertEqual('failed', result['publishStatus'])
        self.assertFalse(result['aotCertified'])

    def test_cli_recognized_failure_is_nonzero_and_clean_publish_is_zero(self):
        for failed in (True, False):
            with self.subTest(failed=failed), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                (root / 'log').write_text(self.log if failed else 'Generating native code\n')
                (root / 'assets').write_text(json.dumps(self.assets))
                (root / 'properties').write_text(json.dumps({'Properties': self.properties}))
                (root / 'baseline').write_text(json.dumps(self.baseline))
                (root / 'probe.exe').write_bytes(b'fresh-test-artifact')
                cases = ''.join('<UnitTestResult testName="Namespace.' + name + '" outcome="Passed" />' for name in GATE.TESTS)
                (root / 'sql-reflection-disabled.trx').write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">' + cases + '</TestRun>')
                argv = ['gate', '--exit-code', '1' if failed else '0']
                for name, value in {'log': 'log', 'assets': 'assets', 'properties': 'properties', 'results': '.', 'executable': 'probe.exe', 'output': 'verdict.json'}.items():
                    argv.extend(['--' + name, str(root / value)])
                with patch.object(GATE, 'BASELINE', root / 'baseline'), patch.object(GATE.sys, 'argv', argv):
                    code = GATE.main()
                result = json.loads((root / 'verdict.json').read_text())
                self.assertEqual(1 if failed else 0, code)
                self.assertEqual('known-upstream-failure' if failed else 'strict-publish-passed', result['decision'])
                self.assertEqual('failed' if failed else 'passed', result['publishStatus'])
                self.assertFalse(result['aotCertified'])

    def test_diagnostic_mutations_are_rejected(self):
        mutations = [self.log + '\nILC : AOT analysis error IL3050: Excalibur.NewFailure()',
                     self.log + '\nfile: error MSB1001: Unknown switch',
                     self.log.replace(self.baseline['diagnostics'][0], '', 1),
                     self.log.replace(self.baseline['diagnostics'][0], self.baseline['diagnostics'][0] + ' changed', 1),
                     self.log + '\nfile: warning NU1901: Vulnerability', '',
                     self.log + '\nerror : Additional failure',
                     self.log + '\nfatal error: Additional failure',
                     self.log + '\nwarning nu1901: Additional warning',
                     self.log.replace('Generating native code', '')]
        for log in mutations:
            with self.subTest(log=log[-100:]), self.assertRaises(ValueError):
                self.check(log)

    def test_crash_and_toolchain_changes_are_rejected(self):
        with self.assertRaises(ValueError):
            self.check(exit_code=139)
        self.properties['PublishAot'] = 'false'
        with self.assertRaises(ValueError):
            self.check()
        with self.assertRaises(ValueError):
            self.check('', exit_code=0, executable=True)

    def test_changed_dependency_is_rejected(self):
        self.assets['libraries'].pop('Microsoft.Data.SqlClient/7.1.1')
        self.assets['libraries']['Microsoft.Data.SqlClient/8.0.0'] = {}
        with self.assertRaises(ValueError):
            self.check()

    def test_only_the_exact_reviewed_sdk_is_accepted_for_known_failure(self):
        for version in ('10.0.403', '10.0.405'):
            with self.subTest(sdk=version):
                self.properties = copy.deepcopy(self.baseline['properties'])
                self.properties['NETCoreSdkVersion'] = version
                with self.assertRaises(ValueError):
                    self.check()

    def test_each_compiler_dependency_must_match_the_exact_reviewed_version(self):
        for package in ('Microsoft.DotNet.ILCompiler', 'runtime.win-x64.Microsoft.DotNet.ILCompiler'):
            for version in ('10.0.14', '10.0.16'):
                with self.subTest(package=package, version=version):
                    self.assets = {'libraries': {name + '/' + pinned: {} for name, pinned in self.baseline['packages'].items()}}
                    self.assets['libraries'].pop(package + '/' + self.baseline['packages'][package])
                    self.assets['libraries'][package + '/' + version] = {}
                    with self.assertRaises(ValueError):
                        self.check()

    def test_clean_publish_requires_executable_and_is_not_runtime_certification(self):
        with self.assertRaises(ValueError):
            self.check('Generating native code\n', exit_code=0)
        with self.assertRaises(ValueError):
            self.check(exit_code=0, executable=True)
        result = self.check('Generating native code\n', exit_code=0, executable=True)
        self.assertEqual('strict-publish-passed', result['decision'])
        self.assertFalse(result['aotCertified'])

    def test_fresh_link_output_does_not_substitute_for_native_compiler_execution(self):
        # A failed ILC invocation can leave a native object. A later incremental publish
        # can link that object into a new executable without rerunning trim/AOT analysis.
        # Exit zero, no diagnostics and a fresh executable alone must therefore fail.
        with self.assertRaises(ValueError):
            self.check('Using cached native object\nProbe -> fresh/publish/\n', exit_code=0, executable=True)
        result = self.check('Generating native code\nProbe -> fresh/publish/\n', exit_code=0, executable=True)
        self.assertEqual('strict-publish-passed', result['decision'])
        self.assertEqual('passed', result['publishStatus'])
        self.assertFalse(result['aotCertified'])

    def test_reflection_tests_cannot_be_absent_skipped_failed_or_duplicated(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'sql-reflection-disabled.trx'
            with self.assertRaises(ValueError):
                GATE.verify_tests(directory)
            cases = ''.join('<UnitTestResult testName="Namespace.' + name + '" outcome="Passed" />' for name in GATE.TESTS)
            def write(value):
                path.write_text('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">' + value + '</TestRun>')
            write(cases)
            GATE.verify_tests(directory)
            for invalid in ('', cases.replace('Passed', 'NotExecuted'), cases.replace('Passed', 'Failed'), cases + cases):
                write(invalid)
                with self.assertRaises(ValueError):
                    GATE.verify_tests(directory)


if __name__ == '__main__':
    unittest.main(verbosity=2)
