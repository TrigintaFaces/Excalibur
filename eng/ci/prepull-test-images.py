#!/usr/bin/env python3
"""prepull-test-images — download a shard's container images before its fixtures are timed.

WHY THIS EXISTS
---------------
Container fixtures run under a TOTAL initialization budget (ContainerFixtureBase), and that budget
is deliberately tight: it sits below the shortest --blame-hang-timeout so a slow container makes the
fixture throw a NAMED error while the test host is still alive. The alternative it exists to prevent
is far worse -- a killed host reports "Passed! - Failed: 0" while the tests that never ran are simply
absent, which once cost 96 tests that nothing but the population census noticed.

The problem is what that budget is being spent ON. Hosted runners are ephemeral, so the Docker cache
is cold on every job, and the SQL Server image alone is well over a gigabyte. The download therefore
happens INSIDE the fixture's budget, and a slow registry consumes the whole thing before the
container has been asked to start. Measured 2026-08-08: twelve tests failed across SQL Server and
Cosmos fixtures, every one of them "Container startup failed after 1 attempt(s) (max 3) within a
total budget of 240s, having spent 240s", on a run where nothing else was wrong.

Pulling first moves the download out of that budget, so the budget covers STARTUP -- which is what it
was sized for. It changes no timeout and re-opens no race.

THIS IS A WARM-UP, NOT A GATE, and the difference decides how it fails.
A gate that cannot evaluate must refuse, because a pass it did not earn is a lie. This makes no
claim about anything: if a pull fails, the fixture pulls again later exactly as it does today, so the
job is no worse off than before this step existed. Failing the build here would invent a new way for
a transient registry blip to break a run that would otherwise have passed. So a failed pull warns,
loudly and by name, and the step still succeeds.

Usage:
  prepull-test-images.py --filter eng/ci/shards/IntegrationTests-Excalibur.slnf
  prepull-test-images.py --filter <slnf> --dry-run     list what would be pulled
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import json
import pathlib
import re
import subprocess
import sys
import time

WITH_IMAGE = re.compile(r'WithImage\("([^"]+)"\)')
BS = chr(92)


def test_projects(filter_path):
    """Project directories named by a solution filter, as repo-relative paths."""
    data = json.loads(pathlib.Path(filter_path).read_text(encoding='utf-8'))
    dirs = []
    for proj in data['solution']['projects']:
        p = proj.replace(BS, '/')
        dirs.append(pathlib.Path(p).parent)
    return dirs


def images_in(dirs):
    """Distinct container images referenced by the sources under these directories."""
    found = set()
    for d in dirs:
        if not d.is_dir():
            continue
        for cs in d.rglob('*.cs'):
            if any(x in cs.parts for x in ('bin', 'obj')):
                continue
            try:
                text = cs.read_text(encoding='utf-8', errors='replace')
            except OSError:
                continue
            if 'WithImage(' not in text:
                continue
            for line in text.split(chr(10)):
                s = line.strip()
                if s.startswith('//') or s.startswith('*'):
                    continue
                for m in WITH_IMAGE.finditer(line):
                    found.add(m.group(1))
    return sorted(found)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--filter', required=True, help='solution filter naming the shard')
    ap.add_argument('--dry-run', action='store_true')
    args = ap.parse_args()

    if not pathlib.Path(args.filter).is_file():
        # No filter is a real environment error: the caller asked for a shard that does not exist,
        # and silently pulling nothing would look identical to a shard with no containers.
        print(f'::error::prepull-test-images: no solution filter at {args.filter}', file=sys.stderr)
        return 2

    images = images_in(test_projects(args.filter))
    if not images:
        print(f'prepull-test-images: no container images referenced by {args.filter}; nothing to warm.')
        return 0

    print(f'prepull-test-images: {len(images)} image(s) referenced by {args.filter}')
    if args.dry_run:
        for i in images:
            print(f'    {i}')
        return 0

    # CONCURRENTLY, which is the entire point.
    #
    # Sequentially this would be a lateral move at best: the same downloads, merely earlier, added to
    # a job already on the critical path. Fixtures pull one at a time, each inside its own budget, so
    # pulling several at once is strictly faster than the behaviour being replaced -- the wall clock
    # becomes the slowest single image rather than the sum of all of them.
    #
    # Bounded rather than unbounded: a hosted runner has two cores and one network link, and enough
    # simultaneous pulls stop being parallel and start being contention -- which is exactly the
    # condition this step exists to relieve.
    failed = []
    started = time.monotonic()

    def pull(image):
        t0 = time.monotonic()
        proc = subprocess.run(['docker', 'pull', '--quiet', image],
                              capture_output=True, text=True, check=False)
        return image, proc.returncode, time.monotonic() - t0, (proc.stderr or proc.stdout or '')

    with ThreadPoolExecutor(max_workers=4) as pool:
        for image, rc, took, output in sorted(pool.map(pull, images), key=lambda r: -r[2]):
            if rc == 0:
                print(f'    ok    {took:6.1f}s  {image}')
            else:
                # Named, not swallowed. The fixture will try again inside its own budget, which is
                # the behaviour that existed before this step -- a lost optimisation, not a fault.
                failed.append(image)
                detail = output.strip().split(chr(10))[-1][:160]
                print(f'::warning::prepull-test-images: pull FAILED for {image} after {took:.1f}s '
                      f'({detail}). Its fixture will pull it again inside the initialization budget, '
                      'which is what this step exists to avoid -- expect that fixture to be slow or '
                      'to time out.')

    total = time.monotonic() - started
    print(f'prepull-test-images: warmed {len(images) - len(failed)} of {len(images)} image(s) '
          f'in {total:.1f}s.')
    # Deliberately 0 even with failures. See the module docstring: this is a warm-up, and failing
    # here would invent a new way for a registry blip to break an otherwise-passing run.
    return 0


if __name__ == '__main__':
    sys.exit(main())
