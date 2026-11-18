#!/usr/bin/env python3
"""Bind downloaded raw SBOM and whole CycloneDX predicate to one trusted official run.

Production executes real gh itself. JSON fixtures exercise policy only, not cryptography.
Release workflow orchestration is supported on Linux (the actual hosted job platform).
"""
import argparse
import ctypes
from decimal import Decimal
import hashlib
import json
import os
from pathlib import Path
import queue
import re
import shutil
import signal
import stat
import subprocess
import sys
sys.dont_write_bytecode = True
import threading
import time

SLSA = 'https://slsa.dev/provenance/v1'
CYCLONE = 'https://cyclonedx.org/bom'
STATEMENT = 'https://in-toto.io/Statement/v1'
BOM_LIMIT = 16 * 1024 * 1024
OUTPUT_LIMIT = 64 * 1024 * 1024
STDERR_LIMIT = 4 * 1024 * 1024


class Refusal(ValueError):
    pass


def strict_json(raw):
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise Refusal('Duplicate JSON key: ' + key)
            result[key] = value
        return result
    def nonfinite(value):
        raise Refusal('Nonfinite JSON number: ' + value)
    return json.loads(raw.decode('utf-8'), object_pairs_hook=pairs, parse_float=Decimal, parse_constant=nonfinite)


def digest(path):
    value = hashlib.sha256()
    with path.open('rb') as handle:
        for block in iter(lambda: handle.read(65536), b''):
            value.update(block)
    return value.hexdigest()


def regular(path):
    path = Path(os.path.abspath(path))
    for part in [path, *path.parents]:
        info = part.lstat()
        if stat.S_ISLNK(info.st_mode) or getattr(info, 'st_file_attributes', 0) & 0x400:
            raise Refusal('Symlink/reparse path refused: ' + str(part))
    if not path.is_file():
        raise Refusal('Expected a regular file: ' + str(path))
    return path


def exact_bom_inventory(path):
    """The pinned producer emits only bom.json; no unsigned sidecar is releasable."""
    path = regular(path)
    if path.name != 'bom.json' or sorted(p.name for p in path.parent.iterdir()) != ['bom.json']:
        raise Refusal('Expected exact SBOM artifact inventory: bom.json only, no directories or sidecars')
    return path


def bounded_bom(path):
    path = exact_bom_inventory(path)
    with path.open('rb') as handle:
        raw = handle.read(BOM_LIMIT + 1)
    if len(raw) > BOM_LIMIT:
        raise Refusal('SBOM exceeds bounded sixteen MiB reader')
    return raw


def require_bom_binding(path, expected):
    if hashlib.sha256(bounded_bom(path)).hexdigest() != expected:
        raise Refusal('Raw BOM path differs from immutable parsed snapshot')


def bom_snapshot(path):
    raw = bounded_bom(path)
    # SAME immutable bytes feed both semantic parsing and raw-provenance digest.
    expected = hashlib.sha256(raw).hexdigest()
    parsed = strict_json(raw)
    require_bom_binding(path, expected)
    return parsed, expected


def subjects(value):
    if not isinstance(value, list) or not value:
        raise Refusal('Empty or malformed subject set')
    result = {}
    for item in value:
        if not isinstance(item, dict):
            raise Refusal('Malformed subject')
        name = item.get('name')
        hashes = item.get('digest')
        if not isinstance(name, str) or not name or '/' in name or '\\' in name or name in ('.', '..'):
            raise Refusal('Path-like/missing subject name')
        if name in result:
            raise Refusal('Duplicate subject name: ' + name)
        if not isinstance(hashes, dict) or set(hashes) != {'sha256'} or not isinstance(hashes.get('sha256'), str) or not re.fullmatch('[0-9a-f]{64}', hashes['sha256']):
            raise Refusal('Expected one lowercase SHA256 digest')
        result[name] = hashes['sha256']
    return result


def json_equal(left, right):
    if isinstance(left, dict) and isinstance(right, dict):
        return left.keys() == right.keys() and all(json_equal(left[key], right[key]) for key in left)
    if isinstance(left, list) and isinstance(right, list):
        return len(left) == len(right) and all(json_equal(a, b) for a, b in zip(left, right))
    if isinstance(left, bool) or isinstance(right, bool):
        return type(left) is type(right) and left == right
    if isinstance(left, (int, Decimal)) and isinstance(right, (int, Decimal)):
        return Decimal(left) == Decimal(right)
    return type(left) is type(right) and left == right


def matching_row(rows, predicate_type, expected_subjects, identity, expected_predicate=None):
    if not isinstance(rows, list) or not rows:
        raise Refusal('No verified CLI rows')
    matched = []
    for index, row in enumerate(rows):
        # Only the actual successful gh export shape is accepted. Raw bundles/decoded DSSE are not verification.
        if not isinstance(row, dict) or not isinstance(row.get('attestation'), dict) or not isinstance(row.get('verificationResult'), dict):
            raise Refusal('Malformed verified CLI row; no bundle fallback')
        verified = row['verificationResult']
        statement = verified.get('statement')
        signature = verified.get('signature')
        certificate = signature.get('certificate') if isinstance(signature, dict) else None
        if not isinstance(statement, dict) or not isinstance(certificate, dict):
            raise Refusal('Missing statement/certificate in one verified row')
        actual_subjects = subjects(statement.get('subject'))
        workflow = f"https://github.com/{identity['repo']}/.github/workflows/official-build.yml@"
        signer = certificate.get('buildSignerURI')
        if not isinstance(signer, str) or not signer.startswith(workflow) or len(signer) == len(workflow):
            continue
        if certificate.get('subjectAlternativeName') != signer or certificate.get('buildConfigURI') != signer:
            continue
        expected_run = f"https://github.com/{identity['repo']}/actions/runs/{identity['run']}/attempts/{identity['attempt']}"
        checks = (statement.get('_type') == STATEMENT, statement.get('predicateType') == predicate_type,
                  actual_subjects == expected_subjects,
                  certificate.get('issuer') == 'https://token.actions.githubusercontent.com',
                  certificate.get('sourceRepositoryURI') == 'https://github.com/' + identity['repo'],
                  certificate.get('sourceRepositoryDigest') == identity['sha'],
                  certificate.get('runInvocationURI') == expected_run,
                  isinstance(statement.get('predicate'), dict))
        if all(checks) and (expected_predicate is None or json_equal(statement['predicate'], expected_predicate)):
            matched.append(index)
    if not matched:
        raise Refusal('No single verified row matches predicate, complete subjects, source/workflow/run/attempt and whole BOM')
    return matched[0]


def capture(argv, directory, stem, seconds=90):
    """One actual gh process group/subreaper, streamed bounded files BEFORE any JSON read."""
    if sys.platform != 'linux':
        raise Refusal('Actual release verifier process ownership supports Linux only; no other-platform crypto claim')
    libc = ctypes.CDLL(None, use_errno=True)
    previous = ctypes.c_int()
    if libc.prctl(37, ctypes.byref(previous), 0, 0, 0) != 0 or libc.prctl(36, 1, 0, 0, 0) != 0:
        raise Refusal('Cannot establish dedicated subreaper')
    children_path = Path(f'/proc/self/task/{os.getpid()}/children')
    def children():
        return [int(item) for item in children_path.read_text().split()]
    stdout = directory / (stem + '.stdout.json')
    stderr = directory / (stem + '.stderr.log')
    receipt = {'argv': argv, 'exit': None, 'cleanupVerified': False, 'timedOut': False, 'budgetExceeded': False}
    faults = queue.Queue()
    process = None
    readers = []
    primary = None
    detached = False
    def pump(stream, destination, limit):
        used = 0
        try:
            with destination.open('xb') as output:
                while True:
                    chunk = stream.read1(65536)
                    if not chunk:
                        break
                    used += len(chunk)
                    if used > limit:
                        raise Refusal('Streaming byte budget exceeded before capture: ' + destination.name)
                    output.write(chunk)
        except BaseException as error:
            faults.put(error)
        finally:
            stream.close()
    try:
        if children():
            raise Refusal('Dedicated verifier already has child processes')
        process = subprocess.Popen(argv, stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                                   start_new_session=True)
        readers = [threading.Thread(target=pump, args=(process.stdout, stdout, OUTPUT_LIMIT), daemon=True),
                   threading.Thread(target=pump, args=(process.stderr, stderr, STDERR_LIMIT), daemon=True)]
        for reader in readers:
            reader.start()
        deadline = time.monotonic() + seconds
        while process.poll() is None:
            if not faults.empty():
                receipt['budgetExceeded'] = True
                raise faults.get()
            if time.monotonic() >= deadline:
                receipt['timedOut'] = True
                raise Refusal('gh exceeded bounded execution deadline')
            time.sleep(0.02)
        receipt['exit'] = process.returncode
    except BaseException as error:
        primary = error
    finally:
        try:
            if process is not None:
                try: os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError: pass
                process.wait(timeout=10)
                cleanup_deadline = time.monotonic() + 5
                while True:
                    while True:
                        try: pid, _ = os.waitpid(-1, os.WNOHANG)
                        except ChildProcessError: break
                        if pid == 0: break
                    remaining = children()
                    for pid in remaining:
                        try:
                            if os.getpgid(pid) != process.pid:
                                detached = True
                            os.kill(pid, signal.SIGKILL)
                        except ProcessLookupError: pass
                    try: os.killpg(process.pid, 0); alive = True
                    except ProcessLookupError: alive = False
                    if not remaining and not alive:
                        break
                    if time.monotonic() >= cleanup_deadline:
                        raise Refusal('Cannot prove zero owned gh descendants')
                    time.sleep(0.02)
                for reader in readers:
                    reader.join(timeout=2)
                    if reader.is_alive():
                        raise Refusal('gh output reader did not finish after owned cleanup')
                if not faults.empty():
                    receipt['budgetExceeded'] = True
                    raise faults.get()
                if detached:
                    raise Refusal('Detached gh descendants were killed but are outside supported CLI process contract')
            receipt['cleanupVerified'] = True
        except BaseException as error:
            if primary is None: primary = error
            else: receipt['secondaryCleanup'] = str(error)
        finally:
            if libc.prctl(36, previous.value, 0, 0, 0) != 0:
                if primary is None: primary = Refusal('Cannot restore scoped subreaper')
                receipt['cleanupVerified'] = False
            receipt['failure'] = None if primary is None else str(primary)
            (directory / (stem + '.process.json')).write_bytes(json.dumps(receipt, indent=2).encode('utf-8'))
    if primary is not None:
        raise primary
    if receipt['exit'] != 0:
        raise Refusal('Actual gh returned nonzero; plausible stdout is not verified evidence')
    return stdout.read_bytes()


def verify(args):
    if sys.platform != 'linux':
        raise Refusal('Hosted release verifier is supported on Linux only, before resolving or invoking gh')
    if not re.fullmatch('[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', args.repo) or not re.fullmatch('[0-9a-f]{40}', args.sha):
        raise Refusal('Invalid trusted repository/source SHA')
    if not re.fullmatch('[1-9][0-9]*', args.run) or not re.fullmatch('[1-9][0-9]*', args.attempt):
        raise Refusal('Invalid selected official run/attempt')
    bom = exact_bom_inventory(args.bom)
    parsed, bom_digest = bom_snapshot(bom)
    if not isinstance(parsed, dict) or parsed.get('bomFormat') != 'CycloneDX' or not parsed.get('specVersion') or not parsed.get('serialNumber'):
        raise Refusal('Expected complete CycloneDX document')
    packages = sorted(Path(args.packages).glob('*.nupkg'))
    if not packages:
        raise Refusal('No actual packages; zero subjects cannot satisfy release')
    # EXAMINED: the denominator every gate owes before it renders a verdict. The refusal above
    # already covers the empty population; what was missing is SAYING what the population was.
    # A reader of 'SBOM-BINDING-PASS' cannot otherwise tell a release whose packages were all
    # bound from one where the glob silently matched a smaller set than was published.
    print(f'EXAMINED: {len(packages) + 1} release subject(s) ({len(packages)} package(s) + 1 BOM)')
    files = [regular(path) for path in packages] + [bom]
    expected = {}
    for path in files:
        if path.name in expected:
            raise Refusal('Colliding local subject basename')
        expected[path.name] = bom_digest if path == bom else digest(path)
    directory = Path(args.evidence)
    directory.mkdir(parents=True, exist_ok=False)
    gh = shutil.which('gh')
    if gh is None:
        raise Refusal('Real gh executable unavailable')
    gh = str(Path(gh).resolve())
    version = capture([gh, '--version'], directory, 'gh-version', 15).decode('utf-8')
    match = re.match(r'gh version (\d+)\.(\d+)\.(\d+)', version)
    if match is None or tuple(map(int, match.groups())) < (2, 97, 0):
        raise Refusal('gh 2.97.0 or later is required by signer identity security policy')
    identity = {'repo': args.repo, 'sha': args.sha, 'run': args.run, 'attempt': args.attempt}
    base = ['--repo', args.repo, '--signer-workflow', args.repo + '/.github/workflows/official-build.yml',
            '--source-digest', args.sha, '--format', 'json']
    require_bom_binding(bom, bom_digest)
    raw = strict_json(capture([gh, 'attestation', 'verify', str(bom), '--predicate-type', SLSA, *base], directory, 'raw-bom'))
    raw_row = matching_row(raw, SLSA, expected, identity)
    package_set = {path.name: expected[path.name] for path in packages}
    require_bom_binding(bom, bom_digest)
    semantic = strict_json(capture([gh, 'attestation', 'verify', str(packages[0]), '--predicate-type', CYCLONE, *base], directory, 'package-sbom'))
    sbom_row = matching_row(semantic, CYCLONE, package_set, identity, parsed)
    for path in files:
        regular(path)
        if (hashlib.sha256(bounded_bom(path)).hexdigest() if path == bom else digest(path)) != expected[path.name]:
            raise Refusal('Verified input changed during attestation checks')
    if {path.name for path in Path(args.packages).glob('*.nupkg')} != set(package_set):
        raise Refusal('Package population changed during verification')
    report = {'rawBomRow': raw_row, 'sbomRow': sbom_row, 'ghExecutable': gh, 'identity': identity,
              'subjects': expected, 'scope': 'Actual successful gh verification plus complete binding; dependency accuracy not certified'}
    require_bom_binding(bom, bom_digest)  # Final handoff to the workflow consumer.
    (directory / 'binding.json').write_bytes(json.dumps(report, indent=2).encode('utf-8'))
    print('SBOM-BINDING-PASS gh=' + gh)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('packages', 'bom', 'repo', 'sha', 'run', 'attempt', 'evidence'):
        parser.add_argument('--' + name, required=True)
    args = parser.parse_args()
    try:
        verify(args)
    except Exception as error:
        print('REFUSE SBOM binding: ' + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
