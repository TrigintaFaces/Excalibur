#!/usr/bin/env python3
"""Positive and adversarial controls for the reproducibility evidence comparator."""
import importlib.util
import base64
import hashlib
import json
import os
from pathlib import Path
import stat
import sys
import tempfile
import unittest
from unittest.mock import patch
import warnings
import zipfile

SPEC = importlib.util.spec_from_file_location("reproducibility", Path(__file__).with_name("verify-reproducible-build.py"))
repro = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(repro)


class OutputControls(unittest.TestCase):
    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory()
        self.addCleanup(self.scratch.cleanup)
        self.root = Path(self.scratch.name)
        self.first, self.second = self.root / "first", self.root / "a-longer-second-root"
        self.first.mkdir()
        self.second.mkdir()
        self.expected = {"bin/Product.dll", "bin/Product.pdb", "feed/Product.1.0.0.nupkg"}
        for root in (self.first, self.second):
            (root / "bin").mkdir()
            (root / "feed").mkdir()
            (root / "bin/Product.dll").write_bytes(b"identical-assembly")
            (root / "bin/Product.pdb").write_bytes(b"identical-symbols")
            self.package(root)

    def package(self, root, payload=b"payload", timestamp=(2020, 1, 1, 0, 0, 0), reverse=False):
        path = root / "feed/Product.1.0.0.nupkg"
        entries = [("Product.nuspec", b"metadata"), ("lib/net10.0/Product.dll", payload)]
        with zipfile.ZipFile(path, "w") as archive:
            for name, content in reversed(entries) if reverse else entries:
                item = zipfile.ZipInfo(name, timestamp)
                item.compress_type = zipfile.ZIP_DEFLATED if reverse else zipfile.ZIP_STORED
                archive.writestr(item, content)
        return path

    def compare(self):
        return repro.compare_outputs(self.expected, repro.output_snapshot(self.first, self.expected),
                                     repro.output_snapshot(self.second, self.expected))

    def test_identical_complete_outputs_pass(self):
        self.assertEqual([], self.compare())

    def test_archive_representation_changes_preserve_content_equality(self):
        self.package(self.second, timestamp=(2026, 2, 2, 1, 2, 4), reverse=True)
        self.assertNotEqual(repro.sha256(self.first / "feed/Product.1.0.0.nupkg"),
                            repro.sha256(self.second / "feed/Product.1.0.0.nupkg"))
        self.assertEqual([], self.compare())

    def test_changed_assembly_or_pdb_is_detected(self):
        for extension in ("dll", "pdb"):
            with self.subTest(extension=extension):
                path = self.second / f"bin/Product.{extension}"
                original = path.read_bytes()
                path.write_bytes(original + b"changed")
                self.assertTrue(any(f"Product.{extension}: bytes differ" in item for item in self.compare()))
                path.write_bytes(original)

    def test_embedded_timestamp_and_absolute_path_are_not_normalized(self):
        for value in (b"build-time=2026-10-02", str(self.second).encode()):
            with self.subTest(value=value):
                self.package(self.second, payload=value)
                self.assertTrue(any("package content differs: lib/net10.0/Product.dll" in item for item in self.compare()))

    def test_both_missing_expected_output_is_not_success(self):
        for root in (self.first, self.second):
            (root / "bin/Product.pdb").unlink()
        with self.assertRaisesRegex(repro.ReproducibilityError, "Missing expected output"):
            self.compare()
        self.assertTrue(repro.compare_outputs(self.expected, {}, {}))

    def test_empty_inventory_is_refused(self):
        with self.assertRaisesRegex(repro.ReproducibilityError, "inventory is empty"):
            repro.compare_outputs(set(), {}, {})

    def test_extra_output_is_reported(self):
        (self.second / "bin/extra.dll").write_bytes(b"extra")
        first = repro.output_snapshot(self.first, self.expected)
        second = repro.output_snapshot(self.second, self.expected)
        self.assertIn("bin/extra.dll", second)
        self.assertIn("first: missing expected output bin/extra.dll",
                      repro.compare_outputs(self.expected | first.keys() | second.keys(), first, second))

    def test_signed_duplicate_unsafe_and_linked_entries_are_refused(self):
        variants = [".signature.p7s", "Product.nuspec", "../escape", "/absolute", "lib\\ambiguous", "lib/./alias", "C:/absolute", "linked"]
        for name in variants:
            with self.subTest(name=name):
                path = self.package(self.second)
                with warnings.catch_warnings():
                    warnings.simplefilter("ignore", UserWarning)
                    with zipfile.ZipFile(path, "a") as archive:
                        item = zipfile.ZipInfo(name)
                        item.filename = name
                        if name == "linked":
                            item.create_system = 3
                            item.external_attr = (stat.S_IFLNK | 0o777) << 16
                        archive.writestr(item, b"invalid")
                with self.assertRaises(repro.ReproducibilityError):
                    repro.package_contents(path)

    def test_missing_or_changed_package_entry_is_detected(self):
        path = self.package(self.second)
        with zipfile.ZipFile(path, "w") as archive:
            archive.writestr("Product.nuspec", b"changed metadata")
        differences = self.compare()
        self.assertTrue(any("Product.nuspec" in item for item in differences))
        self.assertTrue(any("lib/net10.0/Product.dll" in item for item in differences))

    def test_invalid_and_empty_archives_are_refused(self):
        path = self.second / "feed/Product.1.0.0.nupkg"
        path.write_bytes(b"not a zip")
        with self.assertRaisesRegex(repro.ReproducibilityError, "Cannot read package"):
            repro.package_contents(path)
        with zipfile.ZipFile(path, "w"):
            pass
        with self.assertRaisesRegex(repro.ReproducibilityError, "Empty package"):
            repro.package_contents(path)

    def test_same_or_nested_roots_are_refused(self):
        self.assertEqual((self.first.resolve(), self.second.resolve()), repro.independent_roots(self.first, self.second))
        for other in (self.first, self.first / "bin"):
            with self.assertRaisesRegex(repro.ReproducibilityError, "distinct, nonnested"):
                repro.independent_roots(self.first, other)

    def test_process_failure_and_timeout_are_not_success(self):
        for name, script, timeout in (("failure", "raise SystemExit(7)", 10),
                                      ("timeout", "import time; time.sleep(60)", 0.2)):
            with self.subTest(name=name), self.assertRaises(repro.ReproducibilityError):
                repro.command([sys.executable, "-c", script], self.first, self.root / "logs",
                              name, dict(os.environ), timeout)
            receipt = json.loads((self.root / "logs" / (name + ".receipt.json")).read_text())
            self.assertNotEqual("passed", receipt["status"])

    def test_clean_output_roots_use_isolated_caches(self):
        for root in (self.first, self.second):
            (root / "src").mkdir()
            (root / "templates").mkdir()
        left, _ = repro.clean_build_environment(self.first, "a" * 40)
        right, _ = repro.clean_build_environment(self.second, "a" * 40)
        self.assertEqual(left["NUGET_PACKAGES"], left["RestoreFallbackFolders"])
        self.assertEqual(left["NUGET_PACKAGES"], left["NUGET_FALLBACK_PACKAGES"])
        for key in ("NUGET_PACKAGES", "NUGET_HTTP_CACHE_PATH", "NUGET_SCRATCH", "DOTNET_CLI_HOME"):
            self.assertNotEqual(left[key], right[key])
            self.assertEqual([], list(Path(left[key]).iterdir()))
        with self.assertRaisesRegex(repro.ReproducibilityError, "must not exist"):
            repro.clean_build_environment(self.first, "a" * 40)

    def test_preexisting_build_output_is_refused(self):
        (self.first / "src/Widget/obj").mkdir(parents=True)
        (self.first / "templates").mkdir()
        with self.assertRaisesRegex(repro.ReproducibilityError, "prior outputs"):
            repro.clean_build_environment(self.first, "a" * 40)

    def test_source_input_hashes_include_template_hidden_files(self):
        names = ["global.json", "templates/widget/.template.config/template.json", "src/Widget.cs"]
        for name in names:
            path = self.first / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("input")

        def fake_git(root, *args):
            return {("rev-parse", "--show-toplevel"): str(self.first.resolve()).encode(),
                    ("rev-parse", "HEAD"): b"a" * 40,
                    ("status", "--porcelain", "--untracked-files=all"): b" M src/Widget.cs",
                    ("ls-files", "--cached", "--others", "--exclude-standard", "-z"): "\0".join(names).encode()}[args]

        with patch.object(repro, "git", side_effect=fake_git):
            with self.assertRaisesRegex(repro.ReproducibilityError, "dirty"):
                repro.source_inputs(self.first.resolve(), False)
            before = repro.source_inputs(self.first.resolve(), True)
            (self.first / names[1]).write_text("changed-template-input")
            after = repro.source_inputs(self.first.resolve(), True)
        self.assertNotEqual(before["sha256"], after["sha256"])
        self.assertEqual(set(names), set(before["files"]))

    def test_expected_inventory_comes_from_evaluated_outputs(self):
        project = self.first / "src/Widget/Widget.csproj"
        project.parent.mkdir(parents=True)
        project.write_text("<Project/>")
        (self.first / "templates").mkdir()
        (self.first / "templates/Excalibur.Dispatch.Templates.csproj").write_text("<Project/>")

        def evaluate(arguments, *_):
            template = "templates" in arguments[2]
            properties = dict.fromkeys(repro.PROPERTIES, "")
            properties.update(TargetFramework="net10.0", TargetPath=str(project.parent / "bin/Release/net10.0/Widget.dll"),
                              DebugType="portable", DebugSymbols="false", _DebugSymbolsProduced="true", IsPackable="true",
                              PackageId="Templates" if template else "Widget", PackageVersion="0.1.0" if template else "1.2.3",
                              IncludeSymbols="false" if template else "true", SymbolPackageFormat="snupkg")
            self.assertIn("-p:RestoreLockedMode=true", arguments)
            return json.dumps({"Properties": properties})

        with patch.object(repro, "command", side_effect=evaluate):
            outputs, roster = repro.expected_outputs(self.first.resolve(), "dotnet", "1.2.3", {}, self.root / "logs")
        self.assertEqual({"src/Widget/bin/Release/net10.0/Widget.dll", "src/Widget/bin/Release/net10.0/Widget.pdb",
                          "artifacts/reproducibility/feed/Widget.1.2.3.nupkg",
                          "artifacts/reproducibility/feed/Widget.1.2.3.snupkg",
                          "artifacts/reproducibility/feed/Templates.0.1.0.nupkg"}, outputs)
        self.assertEqual(2, len(roster))

    def test_resolved_dependency_bytes_and_cache_location_are_verified(self):
        cache = self.root / "cache"
        package = cache / "library/1.0.0"
        package.mkdir(parents=True)
        archive = package / "library.1.0.0.nupkg"
        archive.write_bytes(b"downloaded package")
        archive_hash = base64.b64encode(hashlib.sha512(archive.read_bytes()).digest()).decode()
        Path(str(archive) + ".sha512").write_text(archive_hash)
        # Signed packages have distinct archive and unsigned-content identities.
        content_hash = base64.b64encode(hashlib.sha512(b"unsigned content").digest()).decode()
        repro.write_json(package / ".nupkg.metadata", {"contentHash": content_hash})
        (package / "library.dll").write_bytes(b"extracted assembly")
        assets = self.first / "src/Widget/obj/project.assets.json"
        assets.parent.mkdir(parents=True)
        model = {"packageFolders": {str(cache.resolve()): {}}, "targets": {},
                 "libraries": {"library/1.0.0": {"type": "package", "path": "library/1.0.0", "files": ["library.dll"],
                    "sha512": content_hash}}}
        assets.write_text(json.dumps(model))
        roster = [{"project": "src/Widget/Widget.csproj"}]
        baseline = repro.dependency_inputs(self.first, roster, cache)
        self.assertNotEqual(baseline[roster[0]["project"]]["libraries"]["library/1.0.0"]["archiveSha512"],
                            baseline[roster[0]["project"]]["libraries"]["library/1.0.0"]["contentHash"])
        repro.write_json(package / ".nupkg.metadata", {"contentHash": "wrong locked identity"})
        with self.assertRaisesRegex(repro.ReproducibilityError, "content hash mismatch"):
            repro.dependency_inputs(self.first, roster, cache)
        repro.write_json(package / ".nupkg.metadata", {"contentHash": content_hash})
        (package / "library.dll").write_bytes(b"altered extraction")
        self.assertNotEqual(baseline, repro.dependency_inputs(self.first, roster, cache))
        archive.write_bytes(b"corrupt downloaded package")
        with self.assertRaisesRegex(repro.ReproducibilityError, "archive hash mismatch"):
            repro.dependency_inputs(self.first, roster, cache)
        model["packageFolders"][str(self.second)] = {}
        assets.write_text(json.dumps(model))
        with self.assertRaisesRegex(repro.ReproducibilityError, "shared/fallback"):
            repro.dependency_inputs(self.first, roster, cache)


class OrchestrationControls(unittest.TestCase):
    """Stub expensive commands, retaining real file collection and dependency checks."""

    def setUp(self):
        self.scratch = tempfile.TemporaryDirectory()
        self.addCleanup(self.scratch.cleanup)
        self.parent = Path(self.scratch.name).resolve()
        self.first, self.second = self.parent / "first", self.parent / "longer-second"
        self.evidence = self.parent / "evidence"
        self.sha = "a" * 40
        self.fault = None
        self.expected = {"src/Widget/bin/Widget.dll", "src/Widget/bin/Widget.pdb",
                         "artifacts/reproducibility/feed/Widget.1.2.3.nupkg"}
        self.roster = [{"project": "src/Widget/Widget.csproj"}]
        for root in (self.first, self.second):
            (root / "src/Widget").mkdir(parents=True)
            (root / "templates").mkdir()
            repro.write_json(root / "global.json", {"sdk": {"version": "10.0.400", "rollForward": "disable"}})
        self.addCleanup(patch.stopall)
        patch.object(repro.shutil, "which", side_effect=lambda name: name).start()
        patch.object(repro, "source_inputs", side_effect=self.source).start()
        patch.object(repro, "expected_outputs", return_value=(self.expected, self.roster)).start()
        patch.object(repro, "command", side_effect=self.command).start()

    def source(self, root, _):
        return {"candidateSha": self.sha, "files": {"input": "changed" if
                self.fault == "source" and root == self.second else "same"}}

    def dependency_file(self, root):
        return root / "artifacts/reproducibility/packages-cache/library/1.0.0/library.dll"

    def command(self, arguments, root, logs, name, environment, timeout=3600):
        if name == "sdk":
            return "wrong" if self.fault == "sdk" else "10.0.400"
        if name == "restore-shipping":
            cache = Path(environment["NUGET_PACKAGES"])
            package = cache / "library/1.0.0"
            package.mkdir(parents=True)
            archive = package / "library.1.0.0.nupkg"
            archive.write_bytes(b"fixed dependency archive")
            archive_hash = base64.b64encode(hashlib.sha512(archive.read_bytes()).digest()).decode()
            Path(str(archive) + ".sha512").write_text(archive_hash)
            repro.write_json(package / ".nupkg.metadata", {"contentHash": archive_hash})
            self.dependency_file(root).write_bytes(b"fixed extraction")
            repro.write_json(root / "src/Widget/obj/project.assets.json", {
                "packageFolders": {str(cache): {}}, "targets": {}, "libraries": {
                    "library/1.0.0": {"type": "package", "path": "library/1.0.0",
                        "files": ["library.dll"], "sha512": base64.b64encode(
                            hashlib.sha512(archive.read_bytes()).digest()).decode()}}})
        if name == "core-first":
            if self.fault == "command":
                raise repro.ReproducibilityError("Injected build command failure")
            if self.fault == "during-build":
                self.dependency_file(root).write_bytes(b"mutated during build")
            if self.fault == "after-collection" and root == self.second:
                self.dependency_file(self.first).write_bytes(b"mutated during second build")
            if self.fault == "late-package" and root == self.second:
                with zipfile.ZipFile(self.first / "artifacts/reproducibility/feed/Late.nupkg", "w") as archive:
                    archive.writestr("Late.nuspec", b"unexpected valid package")
        if name == "produce":
            for output in self.expected:
                path = root / output
                path.parent.mkdir(parents=True, exist_ok=True)
                if path.suffix == ".nupkg":
                    with zipfile.ZipFile(path, "w") as archive:
                        archive.writestr("lib/net10.0/Widget.dll", b"fixed assembly")
                else:
                    path.write_bytes(b"fixed primary output")
            if self.fault == "extra-output" and root == self.second:
                (root / "src/Widget/bin/Unexpected.dll").write_bytes(b"extra")
            if self.fault == "extra-package":
                (root / "artifacts/reproducibility/feed/Unexpected.nupkg").write_bytes(b"extra")
            repro.write_json(logs / "producer/candidate-packages.json", {
                "status": "failed" if self.fault == "producer" else "passed",
                "buildVerified": True, "lockedRestore": True, "continuousIntegrationBuild": True})
        return ""

    def verify(self):
        return repro.verify(self.first, self.second, self.evidence, "1.2.3", self.sha)

    def failure(self, fault, message):
        self.fault = fault
        with self.assertRaisesRegex(repro.ReproducibilityError, message):
            self.verify()
        verdict = json.loads((self.evidence / "verdict.json").read_text())
        self.assertEqual("failed", verdict["status"])
        self.assertRegex(verdict["error"], message)

    def test_complete_orchestration_passes(self):
        self.assertEqual("passed", self.verify()["status"])
        self.assertTrue((self.evidence / "first/dependencies-before.json").is_file())
        self.assertTrue((self.evidence / "second/dependencies.json").is_file())

    def test_dependency_mutation_during_build_fails(self):
        self.failure("during-build", "Dependency inputs changed during build")

    def test_dependency_mutation_after_collection_fails(self):
        self.failure("after-collection", "Dependency inputs changed after collection")

    def test_package_added_after_collection_fails(self):
        self.failure("late-package", "Source or output changed after collection")

    def test_source_mismatch_retains_failed_verdict(self):
        self.failure("source", "fixed source inputs differ")

    def test_sdk_mismatch_retains_failed_verdict(self):
        self.failure("sdk", "SDK mismatch")

    def test_failed_producer_retains_failed_verdict(self):
        self.failure("producer", "Producer did not prove")

    def test_command_failure_retains_failed_verdict(self):
        self.failure("command", "Injected build command failure")

    def test_extra_assembly_reaches_failed_verdict(self):
        self.failure("extra-output", "output mismatches")

    def test_extra_package_reaches_failed_verdict(self):
        self.failure("extra-package", "Package inventory mismatch")

    def test_same_roots_retain_failed_verdict(self):
        self.second = self.first
        self.failure(None, "distinct, nonnested")

    def test_missing_root_retains_failed_verdict(self):
        self.second = self.parent / "absent"
        with self.assertRaises(FileNotFoundError):
            self.verify()
        self.assertEqual("failed", json.loads((self.evidence / "verdict.json").read_text())["status"])

    def test_existing_evidence_is_never_overwritten(self):
        repro.write_json(self.evidence / "verdict.json", {"status": "original"})
        with self.assertRaisesRegex(repro.ReproducibilityError, "new or empty"):
            self.verify()
        self.assertEqual({"status": "original"}, json.loads((self.evidence / "verdict.json").read_text()))

    def test_evidence_inside_source_is_refused_without_writing(self):
        self.evidence = self.first / "evidence"
        with self.assertRaisesRegex(repro.ReproducibilityError, "outside both"):
            self.verify()
        self.assertFalse(self.evidence.exists())


if __name__ == "__main__":
    unittest.main(verbosity=2)
