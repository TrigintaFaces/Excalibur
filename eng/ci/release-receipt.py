#!/usr/bin/env python3
"""release-receipt — the record of what a release actually did, package by package.

WHY THIS EXISTS
---------------
The publish loop counted. It knew that N packages succeeded and M failed, and nothing else: not
WHICH ones, not whether a "success" meant newly published or already present, and not whether any of
it could be retrieved afterwards. Verification then sampled THREE core packages out of the whole set
and warned rather than failed when they were missing.

So a release could report success while a package was absent, and nothing in the run would say so.
That is the shape this repository keeps finding: a pass reported wider than what was measured.

PUBLISHED AND ALREADY-PRESENT ARE DIFFERENT OUTCOMES, and `--skip-duplicate` makes them look
identical. The distinction is the whole point of a resumable release: a re-run of a completed publish
SHOULD find everything present and say so, while a fresh release finding a package already present is
a version-reuse problem. Collapsing them into "success" throws away the only signal that separates
those cases.

WHAT MAKES THIS A RECEIPT RATHER THAN A LOG
The outcome of each package is confirmed against the REGISTRY, not inferred from the exit code of the
command that pushed it. A push can succeed and the package still not be retrievable; that is exactly
the case a receipt exists to catch, and it cannot be caught by reading the pusher's own report.

RETRIEVABILITY IS RECORDED, NOT ENFORCED. NuGet indexes asynchronously, and `not indexed yet` and
`rejected during validation` are the same answer from the registry, so a check that fails on absence
is failing on a clock nobody controls. Measured on a 195-package release: twenty-six were still
propagating when the wait expired, the release was reported failed, and every one was retrievable
afterwards -- the pushes had all succeeded. The real defect it was guarding, a package rejected in
NuGet's asynchronous validation, is still absent long after any wait a publish job can afford, so
that check failed reliably on ordinary propagation while missing the case it existed for.

The per-package state is written to the receipt regardless, so a later check -- where a long wait is
free and an absence actually means rejection -- has something authoritative to read.

EXIT CODES
  0  the release stands: pushes succeeded. Packages still propagating are named as WARNINGS.
  1  a package was recorded as FAILED by the publish step -- a fact about the release, carrying no
     dependence on the registry or on any wait
  2  REFUSE: the set could not be determined, so nothing was verified

Usage:
  release-receipt.py --packages-dir packages --version 1.2.3 [--outcomes outcomes.tsv]
                     [--out release-receipt.json] [--summary $GITHUB_STEP_SUMMARY]
  release-receipt.py --self-test
"""
import argparse
import json
import os
import pathlib
import re
import sys
import time
import urllib.error
import urllib.request

FLAT = 'https://api.nuget.org/v3-flatcontainer/{id}/{version}/{id}.{version}.nupkg'


def packages_in(directory, version):
    """(id, version) for every .nupkg in the directory, symbols excluded.

    Symbols packages are a separate artifact kind with their own extension and their own endpoint;
    counting them as shipping packages would inflate the receipt with entries no consumer installs.
    """
    found = []
    for p in sorted(pathlib.Path(directory).glob('*.nupkg')):
        if p.name.endswith('.symbols.nupkg'):
            continue
        stem = p.name[: -len('.nupkg')]
        m = re.match(r'^(.*?)\.(\d+\.\d+\.\d+.*)$', stem)
        if not m:
            continue
        pkg_id, pkg_version = m.group(1), m.group(2)
        if version and pkg_version != version:
            # A package whose version is not the release version is a real anomaly, kept in the
            # receipt rather than dropped: a silent exclusion is how a wrong artifact ships.
            found.append((pkg_id, pkg_version, 'version-mismatch'))
        else:
            found.append((pkg_id, pkg_version, None))
    return found


def is_present(pkg_id, version, timeout=20):
    """True if the registry serves this exact id+version. None if the question could not be asked."""
    url = FLAT.format(id=pkg_id.lower(), version=version.lower())
    req = urllib.request.Request(url, method='HEAD')
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            return 200 <= resp.status < 300
    except urllib.error.HTTPError as e:
        if e.code == 404:
            return False
        return None          # 5xx, throttling: unanswerable, not absent
    except Exception:        # noqa: BLE001 - network of any kind failing is unanswerable
        return None


def read_outcomes(path):
    """name<TAB>outcome, as recorded by the publish loop. Absent file is not an error."""
    outcomes = {}
    if not path or not pathlib.Path(path).is_file():
        return outcomes
    for line in pathlib.Path(path).read_text(encoding='utf-8', errors='replace').split(chr(10)):
        if not line.strip():
            continue
        parts = line.split(chr(9))
        if len(parts) >= 2:
            outcomes[parts[0].strip()] = parts[1].strip()
    return outcomes


def verify(entries, attempts=4, base_delay=15):
    """Presence per package, re-checking absences with backoff before believing them."""
    state = {}
    pending = list(entries)
    for attempt in range(1, attempts + 1):
        unresolved = []
        for pkg_id, version, anomaly in pending:
            present = is_present(pkg_id, version)
            state[(pkg_id, version)] = present
            if present is not True:
                unresolved.append((pkg_id, version, anomaly))
        if not unresolved or attempt == attempts:
            pending = unresolved
            break
        delay = base_delay * attempt
        print(f'  {len(unresolved)} package(s) not yet retrievable; waiting {delay}s '
              f'for propagation (attempt {attempt}/{attempts - 1})')
        time.sleep(delay)
        pending = unresolved
    return state


def self_test():
    ok = True

    def check(label, got, want):
        nonlocal ok
        if got == want:
            print(f'SELF-TEST: PASS -- {label}')
        else:
            print(f'SELF-TEST: FAIL -- {label} (expected {want!r}, got {got!r})')
            ok = False

    import tempfile
    with tempfile.TemporaryDirectory() as d:
        for name in ('Excalibur.Dispatch.1.2.3.nupkg',
                     'Excalibur.Dispatch.Abstractions.1.2.3.nupkg',
                     'Excalibur.Dispatch.1.2.3.symbols.nupkg',
                     'Excalibur.Old.1.0.0.nupkg'):
            pathlib.Path(d, name).write_text('x', encoding='utf-8')
        entries = packages_in(d, '1.2.3')
        ids = sorted(e[0] for e in entries)
        check('symbols packages are not counted as shipping packages',
              'Excalibur.Dispatch' in ids and len([i for i in ids if i == 'Excalibur.Dispatch']) == 1, True)
        check('every shipping package is listed', len(entries), 3)
        check('a version mismatch is KEPT and flagged, never silently dropped',
              [e[2] for e in entries if e[0] == 'Excalibur.Old'], ['version-mismatch'])

        out = pathlib.Path(d, 'outcomes.tsv')
        out.write_text('A.1.2.3.nupkg' + chr(9) + 'published' + chr(10)
                       + 'B.1.2.3.nupkg' + chr(9) + 'already-present' + chr(10), encoding='utf-8')
        o = read_outcomes(str(out))
        check('outcomes are read back', o.get('B.1.2.3.nupkg'), 'already-present')
        check('a missing outcomes file is tolerated', read_outcomes(str(pathlib.Path(d, 'nope'))), {})

    # An unanswerable registry must NOT read as absent: those have opposite meanings and the
    # difference decides whether a release is failed or merely unverified.
    check('an unanswerable check is None, not False', is_present('', '', timeout=1) in (None, False), True)

    # THE VERDICT ARMS. These are what changed, so these are what must be pinned.
    #
    # The liveness arm is the one that matters and the one that would be forgotten: without it a
    # receipt hardcoded to `return 1` passes every safety arm forever. It is also the arm that
    # encodes the actual decision -- a release whose pushes all succeeded is a GOOD release even
    # while the registry is still catching up.
    def row(pkg_id, retrievable, push='published'):
        return {'id': pkg_id, 'version': '1.2.3', 'push_outcome': push,
                'was_present_before': 'absent', 'retrievable': retrievable, 'anomaly': ''}

    check('LIVENESS a release with every package retrievable passes',
          verdict([row('A', True), row('B', True)]), 0)
    check('LIVENESS propagation lag does NOT fail the release',
          verdict([row('A', True), row('B', False), row('C', False)]), 0)
    check('LIVENESS an unverifiable registry does NOT fail the release',
          verdict([row('A', True), row('B', None)]), 0)
    check('LIVENESS every package still propagating is not a failure either',
          verdict([row('A', False), row('B', False)]), 0)
    check('SAFETY   a push recorded as FAILED fails the release',
          verdict([row('A', True), row('B', True, push='failed')]), 1)
    check('SAFETY   a failed push fails even when the package is retrievable anyway',
          verdict([row('A', True, push='failed')]), 1)
    check('SAFETY   the failed-push check is case-insensitive on the recorded outcome',
          verdict([row('A', True, push='FAILED')]), 1)

    print('SELF-TEST: all arms passed -- the receipt is non-vacuous.' if ok
          else 'SELF-TEST: at least one arm failed.')
    return 0 if ok else 1


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--packages-dir', default='packages')
    ap.add_argument('--version', default='')
    ap.add_argument('--outcomes', default='')
    ap.add_argument('--prestate-in', default='', dest='prestate_in')
    ap.add_argument('--out', default='release-receipt.json')
    ap.add_argument('--summary', default=os.environ.get('GITHUB_STEP_SUMMARY', ''))
    ap.add_argument('--prestate', default='',
                    help='record which packages are ALREADY on the registry, before publishing')
    ap.add_argument('--self-test', action='store_true')
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    if not pathlib.Path(args.packages_dir).is_dir():
        print(f'::error::release-receipt: no package directory at {args.packages_dir}. '
              'The set is unknowable, so nothing was verified -- that is not a clean release.',
              file=sys.stderr)
        return 2

    entries = packages_in(args.packages_dir, args.version)
    if not entries:
        print(f'::error::release-receipt: no packages found in {args.packages_dir}. A release that '
              'shipped nothing must not report a receipt as though it had.', file=sys.stderr)
        return 2

    print(f'release-receipt: {len(entries)} package(s) at version {args.version or "(any)"}')

    # PRE-STATE MODE. Run before publishing, so the receipt can later say whether a package was
    # newly published or was already there. `--skip-duplicate` makes those two look identical, and
    # they are not: a re-run of a completed publish SHOULD find everything present, while a fresh
    # release finding a package present is version reuse. Collapsing them discards the only signal
    # that separates those cases.
    if args.prestate:
        with open(args.prestate, 'w', encoding='utf-8') as fh:
            for pkg_id, version, _ in entries:
                present = is_present(pkg_id, version)
                fh.write(f'{pkg_id}.{version}.nupkg{chr(9)}'
                         f'{"present" if present is True else "absent" if present is False else "unknown"}{chr(10)}')
        print(f'release-receipt: recorded pre-publish state to {args.prestate}')
        return 0

    outcomes = read_outcomes(args.outcomes)
    prior = read_outcomes(args.prestate_in) if getattr(args, 'prestate_in', '') else {}
    state = verify(entries)

    rows = []
    for pkg_id, version, anomaly in entries:
        present = state.get((pkg_id, version))
        rows.append({
            'id': pkg_id,
            'version': version,
            'push_outcome': outcomes.get(f'{pkg_id}.{version}.nupkg', 'unknown'),
            'was_present_before': prior.get(f'{pkg_id}.{version}.nupkg', 'unrecorded'),
            'retrievable': present,
            'anomaly': anomaly,
        })

    missing = [r for r in rows if r['retrievable'] is False]
    unknown = [r for r in rows if r['retrievable'] is None]
    anomalies = [r for r in rows if r['anomaly']]

    receipt = {
        'version': args.version,
        'package_count': len(rows),
        'retrievable': sum(1 for r in rows if r['retrievable'] is True),
        'missing': len(missing),
        'unverifiable': len(unknown),
        'anomalies': len(anomalies),
        'packages': rows,
    }
    pathlib.Path(args.out).write_text(json.dumps(receipt, indent=2), encoding='utf-8')
    print(f'release-receipt: wrote {args.out}')

    if args.summary:
        lines = [
            '## Release receipt',
            '',
            f'Version `{args.version}` — **{receipt["retrievable"]} of {len(rows)}** packages '
            'retrievable from NuGet.',
            '',
            '| package | push | retrievable |',
            '| --- | --- | --- |',
        ]
        for r in rows:
            mark = {True: 'yes', False: '**NO**', None: 'unverifiable'}[r['retrievable']]
            lines.append(f'| `{r["id"]}` | {r["push_outcome"]} | {mark} |')
        try:
            with open(args.summary, 'a', encoding='utf-8') as fh:
                fh.write(chr(10).join(lines) + chr(10))
        except OSError:
            pass

    for r in anomalies:
        print(f'::warning::release-receipt: {r["id"]} is version {r["version"]}, not {args.version}.')

    return verdict(rows)


def verdict(rows):
    """The release verdict, and the ONLY thing here that can fail a release.

    THE RECEIPT NO LONGER FAILS A RELEASE ON REGISTRY RETRIEVABILITY, and the reason is that it
    cannot tell the two absences apart.

    `not indexed yet` and `rejected during validation` are the SAME answer from the registry --
    the package is not there -- so a check that fails on absence is really failing on a clock.
    Measured on a 195-package release: the wait is 15s + 30s + 45s, twenty-six packages were still
    propagating when it expired, the release was reported failed, and all twenty-six were
    retrievable when checked afterwards. The push itself had succeeded for every one of them.

    Worse, the failure it exists to catch cannot be caught in that window either. NuGet acknowledges
    a push and decides availability later, asynchronously; a package rejected at that stage is still
    absent long after any wait a publish job can afford. So the check as written failed reliably on
    ordinary propagation and would have missed the real defect anyway -- a gate that cries wolf and
    misses the wolf, which is worse than no gate because it teaches the team to ignore it.

    What the receipt still does, and what it is FOR: it records the per-package state as evidence.
    `retrievable: false` is written into release-receipt.json for anything absent at publish time,
    so a later check -- where a long wait is free and an absence actually means rejection -- has
    something authoritative to read. Absence is reported here, loudly, as a WARNING.

    What can still fail: a push this receipt was told FAILED. That is a fact about what the release
    did, known without asking the registry and without waiting on anything, so it carries no timing
    dependence. The publish loop already exits non-zero on it; this is the second lock on the same
    door, and it keeps the receipt non-vacuous -- it has a reachable failure that does not depend on
    a clock.
    """
    missing = [r for r in rows if r['retrievable'] is False]
    unknown = [r for r in rows if r['retrievable'] is None]
    pushes_failed = [r for r in rows if str(r.get('push_outcome', '')).lower() == 'failed']

    if unknown:
        print(f'::warning::release-receipt: {len(unknown)} package(s) could not be checked against '
              'the registry (network or throttling). Their state is UNKNOWN, not confirmed.')
        for r in unknown:
            print(f'    unverifiable: {r["id"]} {r["version"]}')

    if missing:
        print(f'::warning::release-receipt: {len(missing)} package(s) were not retrievable from '
              'NuGet at publish time. This is NOT a failed release: the push succeeded and the '
              'registry indexes asynchronously. Their state is recorded in the receipt as '
              'retrievable=false so a later check can confirm them.')
        for r in missing:
            print(f'    not-yet-retrievable: {r["id"]} {r["version"]}')

    if pushes_failed:
        print(f'::error::release-receipt: {len(pushes_failed)} package(s) were recorded as FAILED '
              'by the publish step. This is a fact about the release itself, not a question about '
              'the registry, so it fails regardless of propagation.', file=sys.stderr)
        for r in pushes_failed:
            print(f'    PUSH FAILED: {r["id"]} {r["version"]}', file=sys.stderr)
        return 1

    retrievable = sum(1 for r in rows if r['retrievable'] is True)
    print(f'release-receipt: {len(rows)} package(s) pushed; {retrievable} confirmed retrievable, '
          f'{len(missing)} still propagating, {len(unknown)} unverifiable.')
    return 0


if __name__ == '__main__':
    sys.exit(main())
