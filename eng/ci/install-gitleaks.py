#!/usr/bin/env python3
"""Install the reviewed scanner bytes for the Linux x64 security jobs."""

import hashlib
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import tarfile
import tempfile

# Update together after reviewing the release and its publisher checksum (docs/ci-gates.md).
VERSION = "8.24.3"
URL = f"https://github.com/gitleaks/gitleaks/releases/download/v{VERSION}/gitleaks_{VERSION}_linux_x64.tar.gz"
SHA256 = "9991e0b2903da4c8f6122b5c3186448b927a5da4deef1fe45271c3793f4ee29c"
MAX_ARCHIVE_BYTES = 32 * 1024 * 1024
MAX_BINARY_BYTES = 64 * 1024 * 1024


def download(destination: Path) -> None:
    # -q must be first: a user's curlrc must not alter this transfer. Both curl and
    # the parent process bound elapsed time; no retry resets the transfer deadline.
    subprocess.run(
        ["curl", "-q", "--fail", "--location", "--silent", "--show-error",
         "--proto", "=https", "--proto-redir", "=https", "--connect-timeout", "15",
         "--max-time", "120", "--max-filesize", str(MAX_ARCHIVE_BYTES),
         "--output", str(destination), URL],
        check=True, timeout=130,
    )


def install() -> Path:
    if platform.system() != "Linux" or platform.machine().lower() not in {"x86_64", "amd64"}:
        raise RuntimeError("Gitleaks installation supports only Linux x64 runners")
    runner_temp = Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    github_path = Path(os.environ["GITHUB_PATH"]).resolve(strict=True)
    if not runner_temp.is_dir() or not github_path.is_file():
        raise RuntimeError("RUNNER_TEMP must be a directory and GITHUB_PATH an existing file")
    if any(character in str(runner_temp) for character in "\r\n"):
        raise RuntimeError("RUNNER_TEMP cannot contain line breaks")
    # Check publication access before downloading. This does not publish a path.
    with github_path.open("ab"):
        pass
    directory = Path(tempfile.mkdtemp(prefix="gitleaks-", dir=runner_temp))
    publishing = False
    try:
        archive_path = directory / "release.tar.gz"
        download(archive_path)
        if not 0 < archive_path.stat().st_size <= MAX_ARCHIVE_BYTES:
            raise RuntimeError("Gitleaks archive is empty or exceeds the size limit")
        digest = hashlib.sha256(archive_path.read_bytes()).hexdigest()
        if digest != SHA256:
            raise RuntimeError("Gitleaks archive SHA-256 does not match the reviewed pin")

        # Parse only after hash verification. Never extract paths, links, permissions,
        # or ownership from the archive; copy only the single regular root binary.
        binary = directory / "gitleaks"
        with tarfile.open(archive_path, "r:gz") as archive:
            members = {}
            for member in archive:
                if (member.name not in {"LICENSE", "README.md", "gitleaks"}
                        or member.name in members or not member.isfile()
                        or member.size < 0 or member.size > MAX_BINARY_BYTES):
                    raise RuntimeError("Gitleaks archive contains an unexpected, duplicate, or non-regular entry")
                members[member.name] = member
            if "gitleaks" not in members:
                raise RuntimeError("Gitleaks archive has no root binary")
            source = archive.extractfile(members["gitleaks"])
            if source is None:
                raise RuntimeError("Gitleaks binary could not be read")
            with source, binary.open("xb") as output:
                shutil.copyfileobj(source, output)
        binary.chmod(0o755)
        result = subprocess.run([str(binary), "version"], check=True, capture_output=True, text=True, timeout=15)
        if result.stdout.strip() != VERSION:
            raise RuntimeError("Gitleaks binary returned an unexpected version")
        archive_path.unlink()
        # Validation failures leave GITHUB_PATH unchanged. If final publication I/O
        # fails, keep the verified directory: some path bytes may already be visible.
        publishing = True
        with github_path.open("a", encoding="utf-8", newline="\n") as output:
            output.write(str(directory) + "\n")
        print(f"Installed Gitleaks {VERSION}; verified archive SHA-256 {SHA256}")
        return binary
    finally:
        if not publishing:
            shutil.rmtree(directory)


if __name__ == "__main__":
    try:
        if len(sys.argv) != 1:
            raise RuntimeError("This installer accepts no version, URL, or digest overrides")
        install()
    except Exception as error:
        print(f"Gitleaks installation failed: {error}", file=sys.stderr)
        sys.exit(1)
