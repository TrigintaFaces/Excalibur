"""CLI safety and promotion parity using real subprocesses against disposable API trees."""
from pathlib import Path
import os
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(os.environ.get('PROMOTE_PUBLIC_API_SCRIPT', Path(__file__).resolve().parents[2] / '.claude/tools/promote-public-api.py')).resolve()

class PromotionCliSafety(unittest.TestCase):
    def fixture(self, root, shipped_nl=b'\r\n', unshipped_nl=b'\n'):
        project = root / 'src/Example'
        project.mkdir(parents=True)
        (project / 'PublicAPI.Shipped.txt').write_bytes(shipped_nl.join([b'#nullable enable', b'Api.Keep', b'Api.Remove', b'']))
        (project / 'PublicAPI.Unshipped.txt').write_bytes(unshipped_nl.join([b'#nullable enable', b'Api.Add', b'*REMOVED*Api.Remove', b'']))
        return project

    def snapshot(self, root):
        return {p.relative_to(root).as_posix(): p.read_bytes() for p in root.rglob('*') if p.is_file()}

    def invoke(self, cwd, *args):
        return subprocess.run([sys.executable, str(SCRIPT), *map(str, args)], cwd=cwd,
                              capture_output=True, text=True, encoding='utf-8', timeout=20, check=False)

    def test_help_is_success_and_never_promotes_default_or_explicit_root(self):
        for arguments in [('long',), ('short',), ('root-first',)]:
            with self.subTest(arguments=arguments), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                self.fixture(root)
                before = self.snapshot(root)
                args = ['--help'] if arguments[0] == 'long' else ['-h'] if arguments[0] == 'short' else [str(root), '--help']
                result = self.invoke(root, *args)
                self.assertEqual(0, result.returncode, result.stderr)
                self.assertEqual(before, self.snapshot(root), 'Help must not promote pending API entries')
                self.assertIn('--check', result.stdout)
                self.assertIn('usage:', result.stdout.lower())

    def test_unknown_arguments_fail_before_any_api_mutation(self):
        for args in [('--unknown',), ('--chec',), ('--check', '--unknown'), ('root-first',)]:
            with self.subTest(args=args), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                self.fixture(root)
                before = self.snapshot(root)
                actual = [str(root), '--unknown'] if args == ('root-first',) else args
                result = self.invoke(root, *actual)
                self.assertNotEqual(0, result.returncode)
                self.assertEqual(before, self.snapshot(root), 'Invalid CLI must not promote pending entries')
                self.assertIn('error:', result.stderr.lower())

    def test_extra_positional_root_is_refused_without_writes(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.fixture(root)
            before = self.snapshot(root)
            result = self.invoke(root, root, root)
            self.assertNotEqual(0, result.returncode)
            self.assertEqual(before, self.snapshot(root))

    def test_check_is_read_only_and_accepts_root_on_either_side(self):
        for root_first in [True, False]:
            with self.subTest(root_first=root_first), tempfile.TemporaryDirectory() as temporary:
                base = Path(temporary)
                target, cwd = base / 'target', base / 'cwd'
                self.fixture(target)
                cwd.mkdir()
                before = self.snapshot(base)
                args = [target, '--check'] if root_first else ['--check', target]
                result = self.invoke(cwd, *args)
                self.assertEqual(0, result.returncode, result.stderr)
                self.assertEqual(before, self.snapshot(base))
                self.assertIn('projects: 1 | additions promoted: 1 | removals applied: 1', result.stdout)
                self.assertIn('CHECK ONLY, nothing written', result.stdout)

    def test_default_no_argument_mode_still_promotes_adds_removals_and_preserves_each_newline(self):
        for shipped_nl, unshipped_nl in [(b'\r\n', b'\n'), (b'\n', b'\r\n')]:
            with self.subTest(shipped_nl=shipped_nl), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                project = self.fixture(root, shipped_nl, unshipped_nl)
                result = self.invoke(root)
                self.assertEqual(0, result.returncode, result.stderr)
                self.assertEqual(shipped_nl.join([b'#nullable enable', b'Api.Add', b'Api.Keep', b'']), (project / 'PublicAPI.Shipped.txt').read_bytes())
                self.assertEqual(b'#nullable enable' + unshipped_nl, (project / 'PublicAPI.Unshipped.txt').read_bytes())
                self.assertIn('mode: WRITTEN', result.stdout)
                again = self.invoke(root, '--check')
                self.assertEqual(0, again.returncode, again.stderr)
                self.assertIn('projects: 0 | additions promoted: 0 | removals applied: 0', again.stdout)

    def test_explicit_root_promotes_only_requested_tree(self):
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary)
            target, cwd = base / 'target', base / 'cwd'
            target_project = self.fixture(target)
            self.fixture(cwd)
            cwd_before = self.snapshot(cwd)
            result = self.invoke(cwd, target)
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual(cwd_before, self.snapshot(cwd))
            self.assertEqual(b'#nullable enable\n', (target_project / 'PublicAPI.Unshipped.txt').read_bytes())

    def test_missing_shipped_file_and_spurious_removal_keep_legacy_behavior(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            project = root / 'src/New'
            project.mkdir(parents=True)
            (project / 'PublicAPI.Unshipped.txt').write_bytes(b'#nullable enable\r\nApi.New\r\n*REMOVED*Api.NotShipped\r\n')
            result = self.invoke(root)
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual(b'#nullable enable\r\nApi.New\r\n', (project / 'PublicAPI.Shipped.txt').read_bytes())
            self.assertEqual(b'#nullable enable\r\n', (project / 'PublicAPI.Unshipped.txt').read_bytes())
            self.assertIn('SPURIOUS *REMOVED* MARKERS DROPPED (1)', result.stdout)

if __name__ == '__main__':
    unittest.main()
