"""Routing controls use workflow mutations and adapter-shaped identity evidence."""
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('routing', Path(__file__).with_name('integration-route-audit.py'))
routing = importlib.util.module_from_spec(spec)
spec.loader.exec_module(routing)
REPO = Path(__file__).resolve().parents[2]


class RoutingControls(unittest.TestCase):
    def fixture(self, ids):
        return {'purpose': 'execution', 'context': {'candidateSha': 'a' * 40},
                'assemblySha256': 'assembly', 'adapterSha256': 'adapter', 'sdk': 'sdk',
                'inputBundle': ['dependencies'], 'discoveredCount': len(ids),
                'tests': [{'id': x, 'name': x} for x in ids]}

    def test_valid_and_zero_integration_populations(self):
        self.assertEqual(routing.compare(self.fixture(['a']), [self.fixture(['a', 'b'])]), 1)
        self.assertEqual(routing.compare(self.fixture([]), []), 0)

    def test_missing_route_and_equal_count_wrong_identity_fail(self):
        for routes in ([], [self.fixture(['b'])]):
            with self.assertRaisesRegex(ValueError, 'no scheduled route'):
                routing.compare(self.fixture(['a']), routes)

    def test_wrong_candidate_or_input_refuses(self):
        for key in ('context', 'assemblySha256', 'adapterSha256', 'sdk', 'inputBundle'):
            other = self.fixture(['a'])
            other[key] = 'different'
            with self.assertRaisesRegex(ValueError, 'mismatch'):
                routing.compare(self.fixture(['a']), [other])

    def test_incomplete_discovery_refuses(self):
        broken = self.fixture(['a']); broken['discoveredCount'] = 2
        with self.assertRaisesRegex(ValueError, 'Incomplete'):
            routing.compare(broken, [self.fixture(['a'])])

    def test_workflow_routes_and_mutations(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            workflow = root / '.github/workflows/ci.yml'
            workflow.parent.mkdir(parents=True)
            original = (REPO / '.github/workflows/ci.yml').read_text()
            workflow.write_text(original)
            (root / 'eng/ci/shards').mkdir(parents=True)
            for source in (REPO / 'eng/ci/shards').glob('IntegrationTests*.slnf'):
                shutil.copyfile(source, root / 'eng/ci/shards' / source.name)
            found = routing.routes(root)['routes']
            embedded = next(row for row in found if row['name'] == 'embedded')
            project = 'tests/unit/Excalibur.EventSourcing.Tests/Excalibur.EventSourcing.Tests.csproj'
            self.assertIn(project, embedded['projects'])
            # Removing from both parent and child used to pass the partition gate.
            for name in ('IntegrationTests.slnf', 'IntegrationTests-Embedded.slnf'):
                path = root / 'eng/ci/shards' / name
                value = json.loads(path.read_text())
                value['solution']['projects'] = [p for p in value['solution']['projects'] if p.replace('\\', '/') != project]
                path.write_text(json.dumps(value))
            owners = [r for r in routing.routes(root)['routes'] if project in r['projects']]
            self.assertEqual(owners, [])
            with self.assertRaisesRegex(ValueError, 'no scheduled route'):
                routing.compare(self.fixture(['inherited-trait-case']), [])
            for mutation in (
                original.replace('          "INTEGRATION_TEST_FILTER=$($filterInfo.filter)" >> $env:GITHUB_ENV',
                    '          "INTEGRATION_TEST_FILTER=$($filterInfo.filter)" >> $env:GITHUB_ENV\n'
                    '          "INTEGRATION_TEST_FILTER=Category=Unit" >> $env:GITHUB_ENV'),
                original.replace('  integration-tests:\n', '  integration-tests:\n    if: false\n'),
                original.replace('          - name: embedded\n', '          - name: embedded\n            if: false\n'),
                original.replace('get-integration-test-filter.ps1 -Leg', 'other-filter.ps1 -Leg'),
                original.replace('--filter "${{ env.INTEGRATION_TEST_FILTER }}"', '--filter "Category=Unit"'),
                original.replace('          dotnet test ${{ matrix.shard.filter }}', '          # dotnet test ${{ matrix.shard.filter }}'),
            ):
                workflow.write_text(mutation)
                with self.assertRaises(ValueError):
                    routing.routes(root)


if __name__ == '__main__':
    unittest.main()
