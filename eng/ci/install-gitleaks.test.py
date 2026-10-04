#!/usr/bin/env python3
"""Installer refusal controls; --real-archive also runs the pinned Linux binary."""

import argparse
from contextlib import ExitStack, chdir
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import secrets
import shutil
import subprocess
import tarfile
import tempfile
import unittest
from unittest import mock

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("installer", Path(__file__).with_name("install-gitleaks.py"))
installer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(installer)


def archive_bytes(entries=None):
    stream = io.BytesIO()
    with tarfile.open(fileobj=stream, mode="w:gz") as archive:
        for name, kind in entries or [("gitleaks", tarfile.REGTYPE)]:
            member = tarfile.TarInfo(name)
            member.type = kind
            member.linkname = "gitleaks" if kind in (tarfile.SYMTYPE, tarfile.LNKTYPE) else ""
            payload = b"fixture binary"
            member.size = len(payload) if kind == tarfile.REGTYPE else 0
            archive.addfile(member, io.BytesIO(payload) if member.size else None)
    return stream.getvalue()


class InstallerTests(unittest.TestCase):
    def setUp(self):
        self.stack = ExitStack()
        self.addCleanup(self.stack.close)
        self.directory = Path(self.stack.enter_context(tempfile.TemporaryDirectory()))
        self.path_file = self.directory / "github-path"
        self.path_file.write_text("/previous/path\n", encoding="utf-8")
        self.original_path = self.path_file.read_bytes()
        self.runner = self.directory / "runner"
        self.runner.mkdir()
        self.stack.enter_context(mock.patch.dict(os.environ, {"RUNNER_TEMP": str(self.runner), "GITHUB_PATH": str(self.path_file)}))
        self.stack.enter_context(mock.patch.object(installer.platform, "system", return_value="Linux"))
        self.stack.enter_context(mock.patch.object(installer.platform, "machine", return_value="x86_64"))
        self.payload = archive_bytes()
        self.stack.enter_context(mock.patch.object(installer, "SHA256", hashlib.sha256(self.payload).hexdigest()))
        self.download = self.stack.enter_context(mock.patch.object(installer, "download", side_effect=lambda path: path.write_bytes(self.payload)))
        self.execute = self.stack.enter_context(mock.patch.object(installer.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, installer.VERSION + "\n", "")))

    def refused(self, error, message):
        with self.assertRaisesRegex(error, message):
            installer.install()
        self.assertEqual(self.path_file.read_bytes(), self.original_path)
        self.assertEqual(list(self.runner.iterdir()), [])

    def test_valid_archive_publishes_only_private_binary_after_version(self):
        binary = installer.install()
        self.assertEqual(binary.read_bytes(), b"fixture binary")
        self.assertEqual(list(binary.parent.iterdir()), [binary])
        self.assertEqual(self.path_file.read_text(), "/previous/path\n" + str(binary.parent) + "\n")
        self.assertTrue(binary.is_absolute())
        self.execute.assert_called_once_with([str(binary), "version"], check=True, capture_output=True, text=True, timeout=15)

    def test_hash_mismatch_prevents_archive_parsing_and_execution(self):
        self.payload += b"tamper"
        with mock.patch.object(installer.tarfile, "open") as parse:
            self.refused(RuntimeError, "SHA-256")
            parse.assert_not_called()
        self.execute.assert_not_called()

    def test_truncated_download_is_refused(self):
        self.payload = self.payload[:20]
        self.refused(RuntimeError, "SHA-256")
        self.execute.assert_not_called()

    def test_missing_download_is_refused(self):
        self.download.side_effect = lambda path: None
        self.refused(FileNotFoundError, "")
        self.execute.assert_not_called()

    def test_download_failure_is_refused(self):
        self.download.side_effect = subprocess.CalledProcessError(22, "curl")
        self.refused(subprocess.CalledProcessError, "22")
        self.execute.assert_not_called()

    def test_download_timeout_is_refused(self):
        self.download.side_effect = subprocess.TimeoutExpired("curl", 130)
        self.refused(subprocess.TimeoutExpired, "130")
        self.execute.assert_not_called()

    def test_empty_and_oversize_archives_are_refused(self):
        for payload in (b"", b"1234"):
            with self.subTest(payload=payload), mock.patch.object(installer, "MAX_ARCHIVE_BYTES", 3):
                self.payload = payload
                self.refused(RuntimeError, "size limit")
        self.execute.assert_not_called()

    def test_wrong_platform_is_refused_before_download(self):
        for system, machine in (("Windows", "AMD64"), ("Darwin", "arm64"), ("Linux", "aarch64")):
            with self.subTest(system=system, machine=machine), mock.patch.object(installer.platform, "system", return_value=system), mock.patch.object(installer.platform, "machine", return_value=machine):
                self.refused(RuntimeError, "Linux x64")
        self.download.assert_not_called()
        self.execute.assert_not_called()

    def test_missing_runner_configuration_is_refused_before_download(self):
        for variable in ("RUNNER_TEMP", "GITHUB_PATH"):
            with self.subTest(variable=variable), mock.patch.dict(os.environ):
                del os.environ[variable]
                self.refused(KeyError, variable)
        self.download.assert_not_called()

    def test_nonexistent_path_file_is_not_created(self):
        missing = self.directory / "missing"
        with mock.patch.dict(os.environ, {"GITHUB_PATH": str(missing)}):
            self.refused(FileNotFoundError, "")
        self.assertFalse(missing.exists())
        self.download.assert_not_called()

    def test_path_line_break_is_refused_before_download(self):
        with mock.patch.object(Path, "resolve", side_effect=[Path("/runner\nbad"), self.path_file]), mock.patch.object(Path, "is_dir", return_value=True):
            self.refused(RuntimeError, "line breaks")
        self.download.assert_not_called()

    def test_publication_io_failure_keeps_verified_installation(self):
        original_open = Path.open

        def fail_publication(path, mode="r", *args, **kwargs):
            if path == self.path_file and mode == "a":
                raise OSError("Planted PATH publication failure")
            return original_open(path, mode, *args, **kwargs)

        with mock.patch.object(Path, "open", new=fail_publication), self.assertRaisesRegex(OSError, "publication failure"):
            installer.install()
        self.assertEqual(self.path_file.read_bytes(), self.original_path)
        retained = list(self.runner.iterdir())
        self.assertEqual(len(retained), 1)
        self.assertEqual((retained[0] / "gitleaks").read_bytes(), b"fixture binary")

    def test_invalid_archive_entries_never_execute(self):
        fixtures = [
            [("README.md", tarfile.REGTYPE)],
            [("gitleaks", tarfile.REGTYPE), ("gitleaks", tarfile.REGTYPE)],
            [("gitleaks", tarfile.SYMTYPE)], [("gitleaks", tarfile.LNKTYPE)],
            [("gitleaks", tarfile.DIRTYPE)], [("../gitleaks", tarfile.REGTYPE)],
            [("/gitleaks", tarfile.REGTYPE)], [("nested/gitleaks", tarfile.REGTYPE)],
            [("gitleaks", tarfile.REGTYPE), ("README.md", tarfile.SYMTYPE)],
        ]
        for entries in fixtures:
            with self.subTest(entries=entries):
                self.payload = archive_bytes(entries)
                with mock.patch.object(installer, "SHA256", hashlib.sha256(self.payload).hexdigest()):
                    self.refused(RuntimeError, "archive")
        self.execute.assert_not_called()

    def test_invalid_tar_with_matching_pin_never_executes(self):
        self.payload = b"not a tar file"
        with mock.patch.object(installer, "SHA256", hashlib.sha256(self.payload).hexdigest()):
            self.refused(tarfile.ReadError, "")
        self.execute.assert_not_called()

    def test_wrong_version_never_publishes_path(self):
        self.execute.return_value.stdout = "0.0.0\n"
        self.refused(RuntimeError, "unexpected version")

    def test_version_failure_and_timeout_never_publish_path(self):
        for failure in (subprocess.CalledProcessError(1, "gitleaks"), subprocess.TimeoutExpired("gitleaks", 15)):
            with self.subTest(failure=type(failure).__name__):
                self.execute.side_effect = failure
                self.refused(type(failure), "")

    def test_existing_binary_on_path_is_never_a_fallback(self):
        existing = self.directory / "existing"
        existing.mkdir()
        (existing / "gitleaks").write_text("must not execute")
        with mock.patch.dict(os.environ, {"PATH": str(existing)}):
            self.payload += b"corrupt"
            self.refused(RuntimeError, "SHA-256")
        self.execute.assert_not_called()
        self.assertEqual((existing / "gitleaks").read_text(), "must not execute")

    def test_checkout_preservation_detects_relative_write(self):
        invoking = self.directory / "invoking-checkout"
        invoking.mkdir()
        sentinel = invoking / "README.md"
        sentinel.write_text("Original invoking checkout")

        def pollute_checkout():
            Path("README.md").write_text("Planted relative installer write")
            return self.directory / "unused-binary"

        # Keep even the broken control inside a disposable directory.
        with chdir(invoking), mock.patch.object(installer, "install", side_effect=pollute_checkout):
            with self.assertRaisesRegex(AssertionError, "Installer changed the checkout"):
                real_smoke(self.directory / "unused.tar.gz", self.directory / "unused.json")
        self.assertEqual(sentinel.read_text(), "Original invoking checkout")


class ContractTests(unittest.TestCase):
    def test_production_pin_has_no_environment_override(self):
        with mock.patch.dict(os.environ, {"GITLEAKS_VERSION": "attacker", "GITLEAKS_URL": "file:///wrong", "GITLEAKS_SHA256": "wrong"}):
            reloaded = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(reloaded)
            self.assertEqual(reloaded.VERSION, "8.24.3")
            self.assertEqual(reloaded.URL, "https://github.com/gitleaks/gitleaks/releases/download/v8.24.3/gitleaks_8.24.3_linux_x64.tar.gz")
            self.assertEqual(reloaded.SHA256, "9991e0b2903da4c8f6122b5c3186448b927a5da4deef1fe45271c3793f4ee29c")

    def test_downloader_has_total_deadline_https_only_and_no_curlrc(self):
        with mock.patch.object(installer.subprocess, "run") as execute:
            installer.download(Path("archive.tar.gz"))
        args = execute.call_args.args[0]
        self.assertEqual(args[:2], ["curl", "-q"])
        for flag, value in (("--max-time", "120"), ("--max-filesize", str(installer.MAX_ARCHIVE_BYTES)), ("--proto", "=https"), ("--proto-redir", "=https")):
            self.assertEqual(args[args.index(flag) + 1], value)
        self.assertEqual(execute.call_args.kwargs, {"check": True, "timeout": 130})
        self.assertEqual(args[-1], installer.URL)

    def test_both_workflows_share_required_installer(self):
        for name in ("secret-scan.yml", "security.yml"):
            with self.subTest(workflow=name):
                source = (ROOT / ".github/workflows" / name).read_text(encoding="utf-8")
                install_step = source.split("      - name: Install Gitleaks\n", 1)[1].split("      - name:", 1)[0]
                self.assertEqual(install_step.strip(), "run: python3 eng/ci/install-gitleaks.py")
                self.assertIn("--config .gitleaks.toml", source)
                self.assertIn("--exit-code 1", source)
        dedicated = (ROOT / ".github/workflows/secret-scan.yml").read_text(encoding="utf-8")
        self.assertIn("if: steps.gitleaks_scan.outcome == 'failure'", dedicated)
        self.assertIn("continue-on-error: true", dedicated)


def real_smoke(archive_path: Path, evidence: Path):
    # The supplied archive changes transport only; the production digest/platform/version checks remain active.
    with tempfile.TemporaryDirectory(prefix="ci10-real-") as temporary:
        root = Path(temporary)
        path_file = root / "github-path"
        path_file.touch()
        checkout = root / "checkout"
        checkout.mkdir()
        clean = checkout / "README.md"
        clean.write_text("A clean scanner control.\n")
        before = clean.read_bytes()
        with chdir(checkout), mock.patch.dict(os.environ, {"RUNNER_TEMP": str(root), "GITHUB_PATH": str(path_file)}), mock.patch.object(installer, "download", side_effect=lambda destination: shutil.copyfile(archive_path, destination)):
            binary = installer.install()
        if clean.read_bytes() != before or sorted(path.name for path in checkout.iterdir()) != ["README.md"]:
            raise AssertionError("Installer changed the checkout")
        args = [str(binary), "detect", "--source", str(checkout), "--no-git", "--config", str(ROOT / ".gitleaks.toml"), "--redact", "--exit-code", "1", "--report-format", "json", "--report-path", str(root / "scan.json")]
        clean_result = subprocess.run(args, capture_output=True, text=True, timeout=30)
        if clean_result.returncode != 0:
            raise AssertionError(f"Clean scan failed: {clean_result.stderr}")
        (checkout / "planted.txt").write_text('token = "' + 'gh' + 'p_' + secrets.token_hex(18) + '"\n')
        planted_result = subprocess.run(args, capture_output=True, text=True, timeout=30)
        findings = json.loads((root / "scan.json").read_text())
        if planted_result.returncode != 1 or not any(finding["RuleID"] == "github-pat" for finding in findings):
            raise AssertionError("Planted synthetic credential did not fail with the expected rule")
        evidence.parent.mkdir(parents=True, exist_ok=True)
        evidence.write_text(json.dumps({"status": "passed", "version": installer.VERSION, "archiveSha256": installer.SHA256, "binarySha256": hashlib.sha256(binary.read_bytes()).hexdigest(), "cleanExit": clean_result.returncode, "plantedExit": planted_result.returncode, "rules": [finding["RuleID"] for finding in findings], "checkoutUnchangedByInstall": True}, indent=2) + "\n")
        print("PASS pinned binary install, clean scan, planted-secret refusal, and checkout preservation")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--real-archive", type=Path)
    parser.add_argument("--evidence", type=Path, default=ROOT / "artifacts/tools/ci10-real-smoke.json")
    options = parser.parse_args()
    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromModule(__import__(__name__)))
    if not result.wasSuccessful():
        raise SystemExit(1)
    if options.real_archive:
        real_smoke(options.real_archive.resolve(strict=True), options.evidence)
