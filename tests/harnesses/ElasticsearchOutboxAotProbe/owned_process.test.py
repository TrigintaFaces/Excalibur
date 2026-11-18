# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
"""Finite ownership controls; execute only on the platform whose ownership is being claimed."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import sys
sys.dont_write_bytecode = True
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('outbox_probe_owner', Path(__file__).with_name('owned_process.py'))
owner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(owner)
REPO = Path(__file__).resolve().parents[3]


class OwnedProcessControls(unittest.TestCase):
    def invoke(self, body, timeout=3):
        destination = Path(self.directory)
        result = owner.run_command(REPO, [sys.executable, '-u', '-c', body], REPO, timeout,
                                   destination / 'child.log', destination / 'receipt.json')
        self.assertEqual(result, json.loads((destination / 'receipt.json').read_text()))
        self.assertTrue(result['cleanup_verified'], result)
        return result, (destination / 'child.log').read_text(errors='replace')

    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='outbox-native-owner-')
        self.directory = self.temporary.name

    def tearDown(self):
        self.temporary.cleanup()

    def test_nonzero_exit_retains_both_streams(self):
        result, output = self.invoke("import sys; print('OUT',flush=True); print('ERR',file=sys.stderr,flush=True); sys.exit(7)")
        self.assertEqual(result['exit_code'], 7)
        self.assertFalse(result['timed_out'])
        self.assertIn('OUT', output)
        self.assertIn('ERR', output)

    def test_hanging_child_is_killed_with_partial_log(self):
        result, output = self.invoke("import time; print('BEFORE-HANG',flush=True); time.sleep(30)", timeout=2)
        self.assertEqual(result['exit_code'], 124)
        self.assertTrue(result['timed_out'])
        self.assertIn('BEFORE-HANG', output)

    def test_exited_parent_does_not_leave_output_holding_orphan(self):
        ready = str(Path(self.directory) / 'orphan.pid')
        child = f"import os,time; from pathlib import Path; print('ORPHAN-OUT',flush=True); Path({ready!r}).write_text(str(os.getpid())); time.sleep(30)"
        body = ("import subprocess,sys,time; from pathlib import Path; "
                f"subprocess.Popen([sys.executable,'-u','-c',{child!r}], stdout=sys.stdout, stderr=sys.stderr); "
                f"ready=Path({ready!r}); deadline=time.monotonic()+2; "
                "\nwhile not ready.exists() and time.monotonic()<deadline: time.sleep(.01)"
                "\nassert ready.exists(), 'orphan never started'"
                "\nprint('PARENT-EXIT',flush=True)")
        result, output = self.invoke(body)
        self.assertEqual(result['exit_code'], 0)
        self.assertIn('ORPHAN-OUT', output)
        self.assertIn('PARENT-EXIT', output)
        if sys.platform == 'linux':
            pid = int(Path(ready).read_text())
            self.assertIn(pid, result['reaped_children'])
            self.assertFalse(Path(f'/proc/{pid}').exists())
        else:
            self.assertEqual(result['cleanup'], 'owned Job Object has zero active processes')

    def test_start_failure_retains_failure_receipt(self):
        root = Path(self.directory)
        result = owner.run_command(REPO, ['nonexistent-outbox-native-command-42'], REPO, 1,
                                   root / 'missing.log', root / 'missing.json')
        self.assertEqual(result['exit_code'], 125)
        self.assertIn('FileNotFoundError', result['execution_error'])
        self.assertIn('OWNER ERROR', (root / 'missing.log').read_text())
        self.assertEqual(result, json.loads((root / 'missing.json').read_text()))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--repo', type=Path, default=REPO)
    REPO = parser.parse_args().repo.resolve()
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(OwnedProcessControls)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    if not result.wasSuccessful() or result.testsRun != 4:
        sys.exit(1)
    print('OWNED-CONTROLS-COMPLETE checks=4', flush=True)
