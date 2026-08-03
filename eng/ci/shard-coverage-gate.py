#!/usr/bin/env python3
"""Every test project must belong to at least one CI shard.

A test project in no shard is compiled by nobody and run by nobody, and nothing else notices:
the suite is green, the coverage report omits it silently, and the gap is invisible until someone
asks why a subsystem has no failures. Adding a project is the moment this breaks, and it is the
moment nobody remembers to update a shard file.

Membership is derived from project METADATA, never from a hand-maintained list. A project counts
as a test project when it declares xUnit facts or theories and is not an executable; a fixture
library or a BenchmarkDotNet runner therefore excludes itself by what it IS, so no exemption list
exists to go stale.

Exit 0 clean · 1 unassigned test project · 2 REFUSE (could not evaluate). REFUSE is not a pass.
"""
import glob, io, json, os, re, sys, tempfile

TEST_ATTR = re.compile(r'\[\s*(?:Xunit\.)?(?:Fact|Theory)\b')

def is_test_project(csproj, root):
    try:
        txt = io.open(csproj, encoding='utf-8-sig', errors='ignore').read()
    except OSError:
        return False
    if re.search(r'<OutputType>\s*Exe\s*</OutputType>', txt, re.I):
        return False            # a runner, not a suite
    d = os.path.dirname(csproj)
    for cs in glob.glob(os.path.join(d, '**', '*.cs'), recursive=True):
        try:
            if TEST_ATTR.search(io.open(cs, encoding='utf-8-sig', errors='ignore').read()):
                return True
        except OSError:
            continue
    return False

def shard_members(shard_glob):
    members = set()
    files = sorted(glob.glob(shard_glob))
    if not files:
        return None, []
    for f in files:
        try:
            d = json.load(io.open(f, encoding='utf-8-sig'))
        except Exception as e:
            print(f"REFUSE: {f} is not readable JSON: {e}", file=sys.stderr); sys.exit(2)
        for p in (d.get('solution', {}) or {}).get('projects', []) or []:
            members.add(os.path.basename(p.replace(chr(92), '/'))[:-7])
    return members, files

def run(tests_glob, shard_glob, root='.'):
    members, files = shard_members(shard_glob)
    if members is None:
        print("REFUSE: no shard files found. This is NOT a pass.", file=sys.stderr); sys.exit(2)
    projects = sorted(glob.glob(tests_glob, recursive=True))
    if not projects:
        print("REFUSE: no test projects found. This is NOT a pass.", file=sys.stderr); sys.exit(2)
    unassigned, considered = [], 0
    for p in projects:
        if not is_test_project(p, root):
            continue
        considered += 1
        if os.path.basename(p)[:-7] not in members:
            unassigned.append(p)
    return considered, len(projects) - considered, unassigned, len(files)

def self_test():
    d = tempfile.mkdtemp()
    os.makedirs(os.path.join(d, 'tests', 'Assigned'))
    os.makedirs(os.path.join(d, 'tests', 'Orphan'))
    os.makedirs(os.path.join(d, 'tests', 'FixtureLib'))
    os.makedirs(os.path.join(d, 'shards'))
    for n in ('Assigned', 'Orphan'):
        io.open(os.path.join(d, 'tests', n, n + '.csproj'), 'w').write('<Project/>')
        io.open(os.path.join(d, 'tests', n, 'T.cs'), 'w').write('public class T { [Fact] public void A(){} }')
    io.open(os.path.join(d, 'tests', 'FixtureLib', 'FixtureLib.csproj'), 'w').write('<Project/>')
    io.open(os.path.join(d, 'tests', 'FixtureLib', 'F.cs'), 'w').write('public class F { }')
    io.open(os.path.join(d, 'shards', 's.slnf'), 'w').write(
        json.dumps({"solution": {"path": "x.sln", "projects": ["tests/Assigned/Assigned.csproj"]}}))
    considered, skipped, unassigned, _ = run(os.path.join(d, 'tests', '**', '*.csproj'),
                                             os.path.join(d, 'shards', '*.slnf'), d)
    names = [os.path.basename(u) for u in unassigned]
    ok = (names == ['Orphan.csproj']) and considered == 2 and skipped == 1
    print(f"  safety:   an unassigned test project IS detected -- {'PASS' if 'Orphan.csproj' in names else 'FAIL'}")
    print(f"  safety:   a fixture library with no facts is NOT counted -- {'PASS' if skipped == 1 else 'FAIL'}")
    print(f"  liveness: an assigned test project is NOT flagged -- {'PASS' if 'Assigned.csproj' not in names else 'FAIL'}")
    print("SELF-TEST " + ("PASS (safety + liveness, non-vacuous)" if ok else "FAIL"))
    sys.exit(0 if ok else 1)

if __name__ == '__main__':
    if '--self-test' in sys.argv:
        self_test()
    considered, skipped, unassigned, nshards = run('tests/**/*.csproj', 'eng/ci/shards/*.slnf')
    print(f"shard-coverage: {considered} test project(s) across {nshards} shard file(s); "
          f"{skipped} non-test project(s) excluded by metadata")
    if unassigned:
        print(f"shard-coverage: {len(unassigned)} test project(s) belong to NO shard -- they are "
              f"compiled by nobody and run by nobody:")
        for u in unassigned:
            print(f"  ::error::unassigned test project: {u}")
        sys.exit(1)
    print("shard-coverage: every test project is assigned.")
