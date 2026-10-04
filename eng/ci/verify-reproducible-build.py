#!/usr/bin/env python3
"""Compare independently produced, unsigned build outputs without rewriting payloads."""
from __future__ import annotations

import hashlib
import argparse
import base64
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import signal
import stat
import subprocess
import time
import zipfile


class ReproducibilityError(RuntimeError):
    """The evidence cannot establish reproducibility."""


def sha256(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def relative_name(name: str) -> str:
    """Use one spelling for an output; never collapse an unsafe spelling."""
    if (not name or "\\" in name or ":" in name or "\0" in name
            or name.startswith("/") or any(p in ("", ".", "..") for p in name.split("/"))):
        raise ReproducibilityError(f"Noncanonical output path: {name!r}")
    return name


def independent_roots(first: Path, second: Path) -> tuple[Path, Path]:
    first, second = first.resolve(strict=True), second.resolve(strict=True)
    if (not first.is_dir() or not second.is_dir() or first == second
            or first in second.parents or second in first.parents):
        raise ReproducibilityError("Build roots must be distinct, nonnested directories")
    return first, second


def package_contents(path: Path) -> dict[str, dict[str, str | int]]:
    """Hash all logical contents, ignoring only ZIP container representation.

    Compression, entry order, container timestamps and container attributes are not
    part of this content-equality claim. Embedded metadata is compared unchanged.
    Raw archive hashes are retained separately and are never called equal by this rule.
    """
    entries: dict[str, dict[str, str | int]] = {}
    names: set[str] = set()
    try:
        with zipfile.ZipFile(path) as archive:
            for item in archive.infolist():
                # ZipInfo normalizes Windows separators and truncates NULs in filename.
                # Validate the original spelling before that normalization can hide it.
                original = item.orig_filename
                name = relative_name(original[:-1] if original.endswith("/") else original)
                if name in names:
                    raise ReproducibilityError(f"Duplicate package entry: {path.name}: {name}")
                names.add(name)
                if item.flag_bits & 1:
                    raise ReproducibilityError(f"Encrypted package entry: {path.name}: {name}")
                if stat.S_ISLNK(item.external_attr >> 16):
                    raise ReproducibilityError(f"Linked package entry: {path.name}: {name}")
                if name.casefold() == ".signature.p7s":
                    raise ReproducibilityError(f"Signed package is not an unsigned build input: {path.name}")
                if item.is_dir():
                    if item.file_size:
                        raise ReproducibilityError(f"Directory entry contains data: {name}")
                    continue
                digest = hashlib.sha256()
                size = 0
                with archive.open(item) as stream:
                    for block in iter(lambda: stream.read(1024 * 1024), b""):
                        digest.update(block)
                        size += len(block)
                entries[name] = {"sha256": digest.hexdigest(), "size": size}
    except (OSError, zipfile.BadZipFile, RuntimeError) as exc:
        if isinstance(exc, ReproducibilityError):
            raise
        raise ReproducibilityError(f"Cannot read package {path}: {exc}") from exc
    if not entries:
        raise ReproducibilityError(f"Empty package: {path}")
    return entries


def output_snapshot(root: Path, expected: set[str]) -> dict:
    if not expected:
        raise ReproducibilityError("Expected output inventory is empty")
    root = root.resolve(strict=True)
    result = {}
    collected = set(expected)
    # Include dependency copies and satellite assemblies in evaluated output trees.
    # The evaluated primary inventory still detects outputs absent in BOTH builds.
    for name in expected:
        relative_name(name)
        if name.endswith((".dll", ".exe", ".pdb")):
            directory = (root / name).parent
            if directory.is_dir():
                collected.update(path.relative_to(root).as_posix() for path in directory.rglob("*")
                                 if path.suffix.lower() in (".dll", ".exe", ".pdb"))
        elif name.endswith((".nupkg", ".snupkg")):
            directory = (root / name).parent
            if directory.is_dir():
                collected.update(path.relative_to(root).as_posix() for path in directory.iterdir()
                                 if path.suffix.lower() in (".nupkg", ".snupkg"))
    for name in sorted(collected):
        relative_name(name)
        path = root.joinpath(*PurePosixPath(name).parts)
        try:
            resolved = path.resolve(strict=True)
        except FileNotFoundError as exc:
            raise ReproducibilityError(f"Missing expected output: {name}") from exc
        if root not in resolved.parents or resolved != path or not resolved.is_file():
            raise ReproducibilityError(f"Output escapes its build root or uses a link: {name}")
        record = {"sha256": sha256(path), "size": path.stat().st_size}
        if path.suffix in (".nupkg", ".snupkg"):
            record["entries"] = package_contents(path)
        result[name] = record
    return result


def compare_outputs(expected: set[str], first: dict, second: dict) -> list[str]:
    """Return every mismatch; inventory comes from evaluation, not output discovery."""
    if not expected:
        raise ReproducibilityError("Expected output inventory is empty")
    mismatches = []
    for label, actual in (("first", first), ("second", second)):
        for name in sorted(expected - actual.keys()):
            mismatches.append(f"{label}: missing expected output {name}")
        for name in sorted(actual.keys() - expected):
            mismatches.append(f"{label}: unexpected output {name}")
    for name in sorted(expected & first.keys() & second.keys()):
        left, right = first[name], second[name]
        if name.endswith((".nupkg", ".snupkg")):
            if not left.get("entries") or not right.get("entries"):
                mismatches.append(f"{name}: missing package-content evidence")
                continue
            for entry in sorted(left["entries"].keys() | right["entries"].keys()):
                if left["entries"].get(entry) != right["entries"].get(entry):
                    mismatches.append(f"{name}: package content differs: {entry}")
        elif left != right:
            mismatches.append(f"{name}: bytes differ")
    return mismatches


def write_json(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def command(arguments: list[str], root: Path, evidence: Path, name: str,
            environment: dict, timeout: int = 3600) -> str:
    evidence.mkdir(parents=True, exist_ok=True)
    receipt = {"arguments": arguments, "workingDirectory": str(root), "status": "incomplete"}
    start = time.monotonic()
    try:
        with (evidence / (name + ".stdout.log")).open("wb") as stdout, \
                (evidence / (name + ".stderr.log")).open("wb") as stderr:
            process = subprocess.Popen(arguments, cwd=root, env=environment, stdout=stdout,
                                       stderr=stderr, start_new_session=os.name != "nt")
            try:
                process.wait(timeout=timeout)
            except subprocess.TimeoutExpired:
                if os.name == "nt":
                    try:
                        cleanup = subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                                                 stdout=stderr, stderr=stderr, timeout=30, check=False)
                        receipt["treeCleanupExitCode"] = cleanup.returncode
                    finally:
                        # A restricted host can deny taskkill. Never leave even the
                        # directly owned child alive; the timeout still fails the run.
                        if process.poll() is None:
                            process.kill()
                else:
                    os.killpg(process.pid, signal.SIGKILL)
                process.wait(timeout=30)
                raise
        receipt["exitCode"] = process.returncode
        if process.returncode:
            raise ReproducibilityError(f"Command failed ({process.returncode}): {name}; see {evidence}")
        receipt["status"] = "passed"
        return (evidence / (name + ".stdout.log")).read_text(encoding="utf-8-sig")
    except subprocess.TimeoutExpired as exc:
        receipt["status"] = "timed_out"
        raise ReproducibilityError(f"Command timed out: {name}") from exc
    finally:
        receipt["elapsedSeconds"] = time.monotonic() - start
        write_json(evidence / (name + ".receipt.json"), receipt)


def git(root: Path, *arguments: str) -> bytes:
    process = subprocess.run(["git", "-C", str(root), *arguments], capture_output=True, timeout=60, check=False)
    if process.returncode:
        raise ReproducibilityError(f"Cannot inspect source checkout: {root}: {process.stderr.decode(errors='replace')}")
    return process.stdout


def source_inputs(root: Path, allow_dirty: bool) -> dict:
    if Path(git(root, "rev-parse", "--show-toplevel").decode().strip()).resolve() != root:
        raise ReproducibilityError(f"Not a checkout root: {root}")
    dirty = bool(git(root, "status", "--porcelain", "--untracked-files=all").strip())
    if dirty and not allow_dirty:
        raise ReproducibilityError(f"Source checkout is dirty: {root}")
    paths = git(root, "ls-files", "--cached", "--others", "--exclude-standard", "-z").decode().split("\0")
    files = {}
    for name in sorted(set(paths) - {""}):
        relative_name(name)
        path = root / name
        if not path.is_file() or path.resolve() != path:
            raise ReproducibilityError(f"Missing, linked or unsupported source input: {name}")
        files[name] = sha256(path)
    if not files or "global.json" not in files:
        raise ReproducibilityError("Missing fixed source/toolchain inputs")
    return {"candidateSha": git(root, "rev-parse", "HEAD").decode().strip(), "dirtySnapshot": dirty,
            "files": files, "sha256": hashlib.sha256(json.dumps(files, sort_keys=True).encode()).hexdigest()}


def clean_build_environment(root: Path, expected_sha: str) -> tuple[dict, Path]:
    for tree in (root / "src", root / "templates"):
        if not tree.is_dir():
            raise ReproducibilityError(f"Missing build population: {tree}")
        for path in tree.rglob("*"):
            if path.is_dir() and path.name in ("bin", "obj"):
                raise ReproducibilityError(f"Build root contains prior outputs: {path}")
    work = root / "artifacts/reproducibility"
    if work.exists():
        raise ReproducibilityError(f"Build work directory must not exist: {work}")
    work.mkdir(parents=True)
    environment = dict(os.environ)
    environment.update({
        "CI": "true", "ContinuousIntegrationBuild": "true", "RestoreLockedMode": "true",
        "COMPOSITION_EXPECTED_SHA": expected_sha,
        "NUGET_PACKAGES": str(work / "packages-cache"),
        "NUGET_HTTP_CACHE_PATH": str(work / "http-cache"),
        "NUGET_SCRATCH": str(work / "nuget-scratch"),
        "DOTNET_CLI_HOME": str(work / "cli-home"),
        # Empty overrides inherit machine/user fallback directories. Explicitly
        # restrict fallback lookup to this same private cache; NuGet deduplicates it.
        "RestoreFallbackFolders": str(work / "packages-cache"),
        "RestoreAdditionalProjectFallbackFolders": "",
        "NUGET_FALLBACK_PACKAGES": str(work / "packages-cache"),
        "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "true",
        "DOTNET_SKIP_FIRST_TIME_EXPERIENCE": "1",
    })
    for key in ("NUGET_PACKAGES", "NUGET_HTTP_CACHE_PATH", "NUGET_SCRATCH", "DOTNET_CLI_HOME"):
        Path(environment[key]).mkdir()
    return environment, work


PROPERTIES = ("TargetFramework", "TargetFrameworks", "TargetPath", "DebugType", "DebugSymbols", "_DebugSymbolsProduced",
              "IsPackable", "PackageId", "PackageVersion", "IncludeSymbols", "SymbolPackageFormat")


def expected_outputs(root: Path, dotnet: str, version: str, environment: dict, evidence: Path) -> tuple[set[str], list[dict]]:
    """Evaluate the declared population before any build output can influence it."""
    projects = sorted((root / "src").rglob("*.csproj"))
    if not projects:
        raise ReproducibilityError("Source project population is empty")
    projects.append(root / "templates/Excalibur.Dispatch.Templates.csproj")
    expected: set[str] = set()
    roster = []
    package_ids = set()
    for index, project in enumerate(projects):
        template = project.parent == root / "templates"
        common = ["-p:Configuration=Release", "-p:BuildExamplesAndTests=true", "-p:UsePackageReferences=false",
                  "-p:ContinuousIntegrationBuild=true", "-p:RestoreLockedMode=true", f"-p:MinVerVersionOverride={version}"]
        if not template:
            common += [f"-p:PackageVersion={version}"]
        arguments = [dotnet, "msbuild", str(project), "-nologo", "-getProperty:" + ",".join(PROPERTIES), *common]
        properties = json.loads(command(arguments, root, evidence, f"evaluate-{index}", environment, 120))["Properties"]
        frameworks = properties["TargetFrameworks"].split(";") if properties["TargetFrameworks"] else [properties["TargetFramework"]]
        if not frameworks or any(not framework for framework in frameworks):
            raise ReproducibilityError(f"Cannot evaluate target frameworks: {project}")
        record = {"project": project.relative_to(root).as_posix(), "properties": properties.copy(), "outputs": []}
        # Template assemblies are internal pack intermediates; only its declared package is delivered.
        if not template:
            for framework in frameworks:
                inner = properties
                if properties["TargetFrameworks"]:
                    inner = json.loads(command(arguments + [f"-p:TargetFramework={framework}"], root,
                                               evidence, f"evaluate-{index}-{framework}", environment, 120))["Properties"]
                target = Path(inner["TargetPath"])
                if not target.is_absolute():
                    target = project.parent / target
                try:
                    name = target.resolve().relative_to(root).as_posix()
                except ValueError as exc:
                    raise ReproducibilityError(f"Target leaves build root: {target}") from exc
                if target.suffix not in (".dll", ".exe"):
                    raise ReproducibilityError(f"Unsupported assembly output: {target}")
                record["outputs"].append(name)
                # Use the pinned SDK's evaluated decision. Release can evaluate
                # DebugSymbols=false while DebugType=portable still produces a PDB.
                if inner["_DebugSymbolsProduced"] == "true":
                    record["outputs"].append(str(PurePosixPath(name).with_suffix(".pdb")))
        if properties["IsPackable"] != "false":
            identity, package_version = properties["PackageId"], properties["PackageVersion"]
            if not identity or not package_version or identity in package_ids:
                raise ReproducibilityError(f"Invalid or duplicate evaluated package identity: {project}")
            package_ids.add(identity)
            package = f"artifacts/reproducibility/feed/{identity}.{package_version}"
            record["outputs"].append(package + ".nupkg")
            if properties["IncludeSymbols"] == "true":
                if properties["SymbolPackageFormat"] != "snupkg":
                    raise ReproducibilityError(f"Unsupported symbol package policy: {project}")
                record["outputs"].append(package + ".snupkg")
        for name in record["outputs"]:
            if name in expected:
                raise ReproducibilityError(f"Multiple projects declare the same output: {name}")
            expected.add(relative_name(name))
        # Absolute evaluated paths differ intentionally; the relative output list is compared.
        record["properties"].pop("TargetPath", None)
        roster.append(record)
    if not any(name.endswith(".dll") for name in expected) or not any(name.endswith(".pdb") for name in expected):
        raise ReproducibilityError("Expected assembly/symbol population is empty")
    return expected, roster


def dependency_inputs(root: Path, roster: list[dict], cache: Path) -> dict:
    result = {}
    for project in roster:
        assets = root / Path(project["project"]).parent / "obj/project.assets.json"
        if not assets.is_file():
            raise ReproducibilityError(f"Missing dependency evidence: {assets}")
        model = json.loads(assets.read_text(encoding="utf-8-sig"))
        folders = [Path(folder).resolve() for folder in model.get("packageFolders", {})]
        if folders != [cache.resolve()]:
            raise ReproducibilityError(f"Restore used a shared/fallback package cache: {assets}: {folders}")
        libraries = {}
        for identity, library in model["libraries"].items():
            if library["type"] != "package":
                continue
            directory = cache / library["path"]
            archives = list(directory.glob("*.nupkg"))
            if len(archives) != 1:
                raise ReproducibilityError(f"Missing resolved package archive: {identity}")
            with archives[0].open("rb") as stream:
                actual = base64.b64encode(hashlib.file_digest(stream, "sha512").digest()).decode()
            # NuGet lock hashes describe unsigned content for signed packages;
            # the sidecar describes the downloaded archive, including its signature.
            # Locked restore validates the content identity. Bind that restore receipt
            # to its raw archive here and compare both identities between clean roots.
            sidecar = Path(str(archives[0]) + ".sha512")
            metadata = directory / ".nupkg.metadata"
            if not sidecar.is_file() or sidecar.read_text(encoding="utf-8-sig").strip() != actual:
                raise ReproducibilityError(f"Resolved package archive hash mismatch: {identity}")
            if not metadata.is_file():
                raise ReproducibilityError(f"Missing restored content identity: {identity}")
            content_hash = json.loads(metadata.read_text(encoding="utf-8-sig")).get("contentHash")
            if not content_hash or content_hash != library.get("sha512"):
                raise ReproducibilityError(f"Resolved package content hash mismatch: {identity}")
            files = {}
            for name in library.get("files", []):
                relative_name(name)
                path = directory / name
                if not path.is_file() or directory.resolve() not in path.resolve().parents:
                    raise ReproducibilityError(f"Missing or escaping dependency content: {identity}: {name}")
                files[name] = sha256(path)
            if not files:
                raise ReproducibilityError(f"Empty dependency content inventory: {identity}")
            libraries[identity] = {"archiveSha512": actual, "contentHash": content_hash, "files": files}
        result[project["project"]] = {"libraries": libraries, "targets": model["targets"]}
    return result


def verify(first: Path, second: Path, evidence: Path, version: str, expected_sha: str, allow_dirty: bool = False) -> dict:
    # Evidence destination refusals must not modify either source or older evidence.
    # Once a safe, fresh destination exists, all validation failures retain a verdict.
    first, second = first.resolve(), second.resolve()
    evidence = evidence.resolve()
    if any(evidence == root or root in evidence.parents for root in (first, second)):
        raise ReproducibilityError("Evidence must be outside both fixed-input roots")
    if evidence.exists() and any(evidence.iterdir()):
        raise ReproducibilityError("Evidence directory must be new or empty")
    evidence.mkdir(parents=True, exist_ok=True)
    verdict = {"status": "incomplete", "claim": "same-runner clean-root reproducibility and unsigned package-content equality",
               "expectedSha": expected_sha, "version": version, "allowDirtySnapshot": allow_dirty, "builds": []}
    try:
        first, second = independent_roots(first, second)
        inputs = [source_inputs(root, allow_dirty) for root in (first, second)]
        if any(item["candidateSha"] != expected_sha for item in inputs) or inputs[0] != inputs[1]:
            raise ReproducibilityError("Candidate SHA or fixed source inputs differ")
        write_json(evidence / "source-inputs.json", inputs[0])
        pinned_sdk = json.loads((first / "global.json").read_text())["sdk"]
        if pinned_sdk.get("rollForward") != "disable":
            raise ReproducibilityError("SDK must be pinned with rollForward disabled")
        dotnet, pwsh = shutil.which("dotnet"), shutil.which("pwsh")
        if not dotnet or not pwsh:
            raise ReproducibilityError("Pinned dotnet and PowerShell are required")
        snapshots, dependencies, expected_sets, rosters, caches = [], [], [], [], []
        for label, root in (("first", first), ("second", second)):
            environment, work = clean_build_environment(root, expected_sha)
            logs = evidence / label
            sdk = command([dotnet, "--version"], root, logs, "sdk", environment, 60).strip()
            if sdk != pinned_sdk["version"]:
                raise ReproducibilityError(f"SDK mismatch: {sdk}, expected {pinned_sdk['version']}")
            common = ["-p:Configuration=Release", "-p:BuildExamplesAndTests=true", "-p:UsePackageReferences=false",
                      "-p:ContinuousIntegrationBuild=true", "-p:RestoreLockedMode=true", f"-p:MinVerVersionOverride={version}"]
            command([dotnet, "restore", str(root / "eng/ci/shards/ShippingOnly.slnf"), "--locked-mode", *common],
                    root, logs, "restore-shipping", environment)
            command([dotnet, "restore", str(root / "templates/Excalibur.Dispatch.Templates.csproj"), "--locked-mode",
                     "-p:ContinuousIntegrationBuild=true"], root, logs, "restore-templates", environment)
            expected, roster = expected_outputs(root, dotnet, version, environment, logs)
            write_json(logs / "expected-outputs.json", {"outputs": sorted(expected), "projects": roster})
            expected_sets.append(expected)
            rosters.append(roster)
            cache = Path(environment["NUGET_PACKAGES"])
            caches.append(cache)
            dependency_baseline = dependency_inputs(root, roster, cache)
            write_json(logs / "dependencies-before.json", dependency_baseline)
            command([dotnet, "build", str(root / "src/Dispatch/Excalibur.Dispatch/Excalibur.Dispatch.csproj"),
                     "-c", "Release", "--no-restore", "--no-incremental", "--disable-build-servers", *common],
                    root, logs, "core-first", environment)
            command([pwsh, "-NoProfile", "-File", str(root / "eng/pack-local.ps1"), "-Version", version,
                     "-LockedRestore", "-ContinuousIntegrationBuild", "-OutputDirectory", str(work / "feed"),
                     "-EvidenceDirectory", str(logs / "producer")], root, logs, "produce", environment)
            production = json.loads((logs / "producer/candidate-packages.json").read_text(encoding="utf-8-sig"))
            if (production.get("status") != "passed" or production.get("buildVerified") is not True
                    or production.get("lockedRestore") is not True or production.get("continuousIntegrationBuild") is not True):
                raise ReproducibilityError("Producer did not prove a locked CI build")
            command([dotnet, "pack", str(root / "templates/Excalibur.Dispatch.Templates.csproj"), "-c", "Release",
                     "-o", str(work / "feed"), "-p:RestoreLockedMode=true", "-p:ContinuousIntegrationBuild=true",
                     "--disable-build-servers"], root, logs, "templates", environment)
            actual_packages = {path.relative_to(root).as_posix() for path in (work / "feed").iterdir()
                               if path.suffix in (".nupkg", ".snupkg")}
            expected_packages = {name for name in expected if name.endswith((".nupkg", ".snupkg"))}
            if actual_packages != expected_packages:
                raise ReproducibilityError(f"Package inventory mismatch: missing={sorted(expected_packages-actual_packages)}, extra={sorted(actual_packages-expected_packages)}")
            snapshot = output_snapshot(root, expected)
            dependency = dependency_inputs(root, roster, cache)
            if dependency != dependency_baseline:
                raise ReproducibilityError(f"Dependency inputs changed during build: {label}")
            write_json(logs / "outputs.json", snapshot)
            write_json(logs / "dependencies.json", dependency)
            snapshots.append(snapshot)
            dependencies.append(dependency)
            verdict["builds"].append({"root": str(root), "sdk": sdk, "outputs": len(snapshot), "packages": len(actual_packages)})
            if source_inputs(root, allow_dirty) != inputs[0]:
                raise ReproducibilityError(f"Source inputs changed during build: {label}")
        if expected_sets[0] != expected_sets[1] or rosters[0] != rosters[1]:
            raise ReproducibilityError("Evaluated output contracts differ")
        if dependencies[0] != dependencies[1]:
            raise ReproducibilityError("Resolved dependency inputs differ; inspect dependencies.json")
        for root, expected, snapshot, roster, dependency, cache in zip(
                (first, second), expected_sets, snapshots, rosters, dependencies, caches):
            if source_inputs(root, allow_dirty) != inputs[0] or output_snapshot(root, expected) != snapshot:
                raise ReproducibilityError(f"Source or output changed after collection: {root}")
            if dependency_inputs(root, roster, cache) != dependency:
                raise ReproducibilityError(f"Dependency inputs changed after collection: {root}")
        # Compare every discovered output as well as the independently evaluated
        # primary population. Additional dependency copies must agree in both roots.
        comparison_inventory = expected_sets[0] | snapshots[0].keys() | snapshots[1].keys()
        mismatches = compare_outputs(comparison_inventory, *snapshots)
        verdict["mismatches"] = mismatches
        if mismatches:
            raise ReproducibilityError(f"{len(mismatches)} output mismatches; inspect verdict.json")
        verdict["status"] = "passed"
        return verdict
    except Exception as exc:
        verdict["status"] = "failed"
        verdict["error"] = str(exc)
        raise
    finally:
        write_json(evidence / "verdict.json", verdict)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--first", type=Path, required=True)
    parser.add_argument("--second", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--sha", required=True)
    parser.add_argument("--allow-dirty-snapshot", action="store_true", help="Local evidence binds full source hashes as well as HEAD")
    args = parser.parse_args()
    try:
        result = verify(args.first, args.second, args.evidence, args.version, args.sha, args.allow_dirty_snapshot)
        # The denominator, taken from counts verify() already records per build. Reproducibility
        # PASSES by finding no mismatch, so a comparison that walked no outputs at all would print
        # exactly the same green as one that compared every package. Saying what was compared is
        # what separates the two.
        builds = result.get("builds", [])
        print(f"EXAMINED: {len(builds)} independent build(s), "
              f"{sum(b['outputs'] for b in builds)} output file(s), "
              f"{sum(b['packages'] for b in builds)} package(s)")
        print(f"PASS: {result['claim']}; evidence: {args.evidence}")
        return 0
    except (ReproducibilityError, OSError, ValueError, KeyError) as exc:
        print(f"FAIL: {exc}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
