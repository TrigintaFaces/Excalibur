#!/usr/bin/env python3
"""container-image-pinning-gate — a test container must not float to an arbitrary image.

WHAT THIS EXISTS TO PREVENT
---------------------------
A `:latest` tag, or no tag at all, means the image can change completely between two runs of the
same commit. A suite that passed yesterday then fails today for a reason that is not in the diff,
and the failure looks like a flake because nothing in the repository changed. Container drift is
indistinguishable from test flakiness from the outside, which is what makes it expensive: it is
debugged as the wrong thing.

`mcr.microsoft.com/mssql/server:2022-latest` is the case that matters here. It reads like a version
pin and is not one -- Microsoft moves it to each new cumulative update, so the database under 21 of
this repository's integration fixtures can change without any commit.

WHY THIS RATCHETS INSTEAD OF DEMANDING DIGESTS
----------------------------------------------
Digest pinning is the stronger answer and is deliberately NOT required here, because it would rot.
Dependabot's docker ecosystem covers `/`, `/samples/**` and `/templates/**` -- Dockerfiles and
compose files. It cannot parse a C# string, so a digest pinned inside `WithImage("...")` has nothing
that will ever update it. Pinned-and-abandoned is worse than floating: a stale image accumulates
CVEs silently and eventually breaks outright when the registry drops the layer.

So the bar is CONCRETE TAG, not digest: an image whose tag identifies one build that the publisher
does not move. That removes the "changed overnight for no reason" class without creating a
maintenance obligation nothing is set up to meet. Where a digest IS used it passes -- five already
do, and that is the stronger pin, so it is accepted rather than argued with.

THE THREE STATES -- REFUSE IS NOT A PASS
  0 PASS     floating count is at or below the baseline
  1 FAIL     it grew, or the baseline file is missing
  2 REFUSE   the tree could not be measured, so nothing was checked

Usage:
  container-image-pinning-gate.py            report every call site by pin quality
  container-image-pinning-gate.py --gate     enforce the ratchet
  container-image-pinning-gate.py --self-test
"""
import pathlib
import re
import sys

WITH_IMAGE = re.compile(r'WithImage\("([^"]+)"\)')
BASELINE = pathlib.Path('eng/ci/container-image-pinning-baseline.txt')
TAG = '[container-image-pinning-gate]'


def classify(image):
    """digest | concrete | floating -- floating is a moved tag or no tag at all."""
    if '@sha256:' in image:
        return 'digest'
    # A colon in the last path segment is a tag; a colon earlier is a registry port.
    last = image.rsplit('/', 1)[-1]
    if ':' not in last:
        return 'floating'          # no tag at all is an implicit :latest
    tag = last.rsplit(':', 1)[1]
    return 'floating' if 'latest' in tag.lower() else 'concrete'


def scan(root):
    found = {'digest': [], 'concrete': [], 'floating': []}
    for path in pathlib.Path(root).rglob('*.cs'):
        if any(p in path.parts for p in ('bin', 'obj')):
            continue
        try:
            text = path.read_text(encoding='utf-8', errors='replace')
        except OSError:
            continue
        if 'WithImage(' not in text:
            continue
        for i, line in enumerate(text.split(chr(10))):
            stripped = line.strip()
            if stripped.startswith('//') or stripped.startswith('*'):
                continue
            for m in WITH_IMAGE.finditer(line):
                found[classify(m.group(1))].append(
                    (str(path).replace(chr(92), '/'), i + 1, m.group(1)))
    return found


def self_test():
    """A gate that cannot fail is not evidence."""
    import os
    import tempfile
    ok = True

    cases = [
        ('an explicit :latest floats', 'server:2022-latest', 'floating'),
        ('a bare :latest floats', 'azurite:latest', 'floating'),
        ('no tag at all floats (implicit latest)', 'azure-service-bus/emulator', 'floating'),
        ('a concrete tag is accepted', 'postgres:16-alpine', 'concrete'),
        ('a digest is accepted, being the stronger pin',
         'x/y@sha256:aaaabbbbccccddddeeeeffff0000111122223333444455556666777788889999', 'digest'),
        # A registry port must not be read as a tag: the tag is what follows the LAST slash.
        ('a registry port is not a tag', 'localhost:5000/foo:1.2.3', 'concrete'),
    ]
    for label, image, expect in cases:
        got = classify(image)
        if got == expect:
            print(f'SELF-TEST: PASS -- {label}')
        else:
            print(f'SELF-TEST: FAIL -- {label} (expected {expect}, got {got})')
            ok = False

    # LIVENESS: the scanner must actually find a call site, or every verdict above is about nothing.
    with tempfile.TemporaryDirectory() as d:
        with open(os.path.join(d, 'F.cs'), 'w', encoding='utf-8') as fh:
            fh.write('var c = new Builder().WithImage("mcr.example/db:2022-latest").Build();' + chr(10))
        n = len(scan(d)['floating'])
    if n == 1:
        print('SELF-TEST: PASS -- the scanner finds a planted floating image')
    else:
        print(f'SELF-TEST: FAIL -- planted floating image not found (got {n})')
        ok = False

    with tempfile.TemporaryDirectory() as d:
        with open(os.path.join(d, 'F.cs'), 'w', encoding='utf-8') as fh:
            fh.write('// var c = new Builder().WithImage("db:latest");' + chr(10))
        n = len(scan(d)['floating'])
    if n == 0:
        print('SELF-TEST: PASS -- a commented-out call site is not counted')
    else:
        print(f'SELF-TEST: FAIL -- counted a comment (got {n})')
        ok = False

    print('SELF-TEST: all arms passed -- the pinning gate is non-vacuous.' if ok
          else 'SELF-TEST: at least one arm failed.')
    return 0 if ok else 1


def main():
    argv = sys.argv[1:]
    if '--self-test' in argv:
        return self_test()

    if not pathlib.Path('tests').is_dir():
        print(f'{TAG} CANNOT EVALUATE -- no tests/ directory here. Run from the repository root.',
              file=sys.stderr)
        return 2

    found = scan('tests')
    total = sum(len(v) for v in found.values())
    if total == 0:
        print(f'{TAG} CANNOT EVALUATE -- no WithImage call sites found at all. The scan matched '
              'nothing, which is a parsing fault rather than a clean tree.', file=sys.stderr)
        return 2

    gate = '--gate' in argv
    print(f'{TAG} {total} container image call site(s): '
          f"{len(found['digest'])} digest-pinned, {len(found['concrete'])} concrete tag, "
          f"{len(found['floating'])} FLOATING.")

    if found['floating'] and not gate:
        print(chr(10) + '  floating (a moved tag, or none at all):')
        for f, ln, img in sorted(found['floating']):
            print(f'    {f}:{ln}  {img}')

    if not gate:
        return 0

    if not BASELINE.is_file():
        print(f'{TAG} baseline {BASELINE} is missing, so the ratchet has nothing to compare against. '
              'Refusing to report a pass it cannot justify.', file=sys.stderr)
        return 1

    baseline = int(re.sub(r'\D', '', BASELINE.read_text(encoding='utf-8')) or '0')
    count = len(found['floating'])

    if count > baseline:
        print(f'::error::{TAG} floating container images grew: {count} against a baseline of '
              f'{baseline}. A :latest tag can change the image under a passing suite between two '
              'runs of the same commit, and the failure then looks like a flake because nothing in '
              'the repository changed. Pin the new one to a concrete tag, or raise the baseline '
              'deliberately and say why.', file=sys.stderr)
        for f, ln, img in sorted(found['floating']):
            print(f'    {f}:{ln}  {img}', file=sys.stderr)
        return 1

    if count < baseline:
        print(f'{TAG} PASS -- floating images down to {count} from a baseline of {baseline}. '
              f'Lower the baseline to {count} to hold the ground.')
    else:
        print(f'{TAG} PASS -- floating images at the baseline of {baseline}.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
