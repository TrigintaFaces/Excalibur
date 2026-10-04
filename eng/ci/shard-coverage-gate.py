#!/usr/bin/env python3
"""Require every source-discovered test project to belong to a CI filter by exact path.

This is assignment coverage, not proof that a workflow executes its selected tests. Discovery
retains the existing xUnit-source/non-executable rule. Unreadable inputs refuse evaluation.
Exit 0: assigned; 1: unassigned; 2: could not evaluate.
"""
import glob
import json
import os
from pathlib import Path
import re
import sys
import tempfile
import unittest

TEST_ATTR = re.compile(r'\[\s*(?:Xunit\.)?(?:Fact|Theory)\b')
EXCLUDED = {'bin', 'obj', 'node_modules', 'BenchmarkDotNet.Artifacts'}


def canonical(path):
    path = path.replace('\\', '/')
    if not path or ':' in path or any(part in ('', '.', '..') for part in path.split('/')):
        raise ValueError(f'non-canonical repository path: {path!r}')
    return path


def generated(path):
    return any(part in EXCLUDED or part.startswith('.') for part in Path(path).parts)


def is_test_project(csproj, root):
    text = Path(csproj).read_text(encoding='utf-8-sig')
    if re.search(r'<OutputType>\s*Exe\s*</OutputType>', text, re.I):
        return False
    for directory, directories, files in os.walk(Path(csproj).parent, onerror=lambda error: (_ for _ in ()).throw(error)):
        directories[:] = [name for name in directories if name not in EXCLUDED and not name.startswith('.')]
        for name in files:
            if name.endswith('.cs') and TEST_ATTR.search(Path(directory, name).read_text(encoding='utf-8-sig')):
                return True
    return False


def shard_members(shard_glob, root='.'):
    root = Path(root).resolve()
    files = sorted(glob.glob(shard_glob))
    if not files:
        raise ValueError('no shard files found')
    solution = root / 'Excalibur.sln'
    solution_members = {
        canonical(path) for path in re.findall(
            r'^Project\("[^"]+"\) = "[^"]+", "([^"]+\.csproj)", "\{[^}]+\}"',
            solution.read_text(encoding='utf-8-sig'), re.M)
    }
    if not solution_members:
        raise ValueError('no solution project paths found')
    members = set()
    for file in files:
        data = json.loads(Path(file).read_text(encoding='utf-8-sig'))['solution']
        if (Path(file).resolve().parent / data['path'].replace('\\', '/')).resolve() != solution:
            raise ValueError(f'filter references another solution: {file}')
        projects = data['projects']
        if not isinstance(projects, list) or not projects:
            raise ValueError(f'empty or malformed filter: {file}')
        unique = set()
        for project in projects:
            path = canonical(project)
            if path in unique or path not in solution_members:
                raise ValueError(f'duplicate or absent solution project: {file}: {path}')
            unique.add(path)
        members.update(unique)
    return members, files


def run(tests_glob, shard_glob, root='.'):
    root = Path(root).resolve()
    members, files = shard_members(shard_glob, root)
    projects = sorted(path for path in glob.glob(tests_glob, recursive=True)
                      if not generated(Path(path).resolve().relative_to(root)))
    if not projects:
        raise ValueError('no test projects found')
    unassigned, considered = [], 0
    for path in projects:
        if not is_test_project(path, root):
            continue
        considered += 1
        identity = Path(path).resolve().relative_to(root).as_posix()
        if identity not in members:
            unassigned.append(path)
    if not considered:
        raise ValueError('no test projects could be identified by the source-discovery rule')
    return considered, len(projects) - considered, unassigned, len(files)


class CoverageControls(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='shard-coverage-')
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.paths = ['tests/Assigned/Suite.csproj', 'tests/Orphan/Suite.csproj', 'tests/Fixture/Fixture.csproj']
        for path in self.paths:
            full = self.root / path
            full.parent.mkdir(parents=True)
            full.write_text('<Project/>', encoding='utf-8')
            full.with_suffix('.cs').write_text('class Test { [Fact] void Check() {} }' if 'Fixture' not in path else 'class Fixture {}', encoding='utf-8')
        solution = '\n'.join(f'Project("{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}") = "Suite", "{path}", "{{00000000-0000-0000-0000-{index:012}}}"'
                             for index, path in enumerate(self.paths))
        (self.root / 'Excalibur.sln').write_text(solution, encoding='utf-8')
        (self.root / 'shards').mkdir()
        self.filter = self.root / 'shards/test.slnf'
        self.write_filter([self.paths[0]])

    def write_filter(self, projects, solution='../Excalibur.sln'):
        self.filter.write_text(json.dumps({'solution': {'path': solution, 'projects': projects}}), encoding='utf-8')

    def evaluate(self):
        return run(str(self.root / 'tests/**/*.csproj'), str(self.root / 'shards/*.slnf'), self.root)

    def test_same_basename_does_not_cover_orphan(self):
        considered, excluded, missing, _ = self.evaluate()
        self.assertEqual((considered, excluded), (2, 1))
        self.assertEqual([Path(path).relative_to(self.root).as_posix() for path in missing], [self.paths[1]])

    def test_complete_assignment_passes_with_both_separators(self):
        self.write_filter([self.paths[0].replace('/', '\\'), self.paths[1]])
        self.assertEqual(self.evaluate()[2], [])

    def test_case_substitution_is_refused(self):
        self.write_filter([self.paths[0].replace('Assigned', 'assigned')])
        with self.assertRaises(ValueError):
            self.evaluate()

    def test_duplicate_or_escaping_path_is_refused(self):
        for paths in ([self.paths[0], self.paths[0]], ['../outside.csproj']):
            with self.subTest(paths=paths):
                self.write_filter(paths)
                with self.assertRaises(ValueError):
                    self.evaluate()

    def test_wrong_solution_is_refused(self):
        self.write_filter([self.paths[0]], '../Other.sln')
        with self.assertRaises(ValueError):
            self.evaluate()

    def test_empty_population_is_refused(self):
        for path in self.root.glob('tests/**/*.cs'):
            path.write_text('class Fixture {}', encoding='utf-8')
        with self.assertRaises(ValueError):
            self.evaluate()

    def test_unreadable_source_is_not_a_fixture_exemption(self):
        (self.root / self.paths[1]).with_suffix('.cs').write_bytes(b'\xff')
        with self.assertRaises(UnicodeError):
            self.evaluate()

    def test_generated_projects_are_not_test_population(self):
        generated_project = self.root / 'tests/Assigned/obj/Generated.csproj'
        generated_project.parent.mkdir()
        generated_project.write_text('<Project/>', encoding='utf-8')
        self.assertEqual(self.evaluate()[:2], (2, 1))


if __name__ == '__main__':
    if '--self-test' in sys.argv:
        unittest.main(argv=[sys.argv[0]], verbosity=2)
    try:
        considered, excluded, unassigned, filters = run('tests/**/*.csproj', 'eng/ci/shards/*.slnf')
    except (OSError, ValueError, KeyError, TypeError) as error:
        print(f'REFUSE: {error}', file=sys.stderr)
        sys.exit(2)
    print(f'shard-coverage: {considered} source-discovered test projects; {filters} filters; {excluded} non-test projects.')
    for project in unassigned:
        print(f'::error::unassigned test project: {project}')
    sys.exit(1 if unassigned else 0)
