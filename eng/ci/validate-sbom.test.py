import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('sbom', Path(__file__).with_name('validate-sbom.py'))
sbom = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sbom)


class SbomControls(unittest.TestCase):
    def component(self, name):
        return {'name': name, 'version': '1.0.0', 'purl': f'pkg:nuget/{name}@1.0.0'}

    def test_complete_graph_passes(self):
        self.assertEqual(sbom.validate({'bomFormat': 'CycloneDX', 'components': [self.component('A'), self.component('B')]},
                                       {('a', '1.0.0'), ('b', '1.0.0')}), 2)

    def test_file_only_empty_bom_is_not_complete(self):
        with self.assertRaisesRegex(ValueError, 'no NuGet'):
            sbom.validate({'bomFormat': 'CycloneDX'}, {('a', '1.0.0')})

    def test_one_omitted_package_fails(self):
        with self.assertRaisesRegex(ValueError, 'omits'):
            sbom.validate({'bomFormat': 'CycloneDX', 'components': [self.component('A')]}, {('a', '1.0.0'), ('b', '1.0.0')})

    def test_contradictory_identity_refuses(self):
        component = self.component('A'); component['version'] = '2.0.0'
        with self.assertRaisesRegex(ValueError, 'contradicts'):
            sbom.validate({'bomFormat': 'CycloneDX', 'components': [component]}, {('a', '1.0.0')})

    def test_empty_expected_graph_refuses(self):
        with self.assertRaisesRegex(ValueError, 'Expected'):
            sbom.validate({'bomFormat': 'CycloneDX'}, set())

    def test_restore_inputs_are_required_and_bound_to_project(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            project = root / 'Shipping.csproj'
            project.write_text('<Project />')
            source = root / 'Shipping.slnf'
            source.write_text(json.dumps({'solution': {'path': 'Shipping.sln', 'projects': ['Shipping.csproj']}}))
            with self.assertRaises(FileNotFoundError):
                sbom.expected_packages(source)
            (root / 'obj').mkdir()
            assets = root / 'obj/project.assets.json'
            assets.write_text('not json')
            with self.assertRaises(json.JSONDecodeError):
                sbom.expected_packages(source)
            data = {'project': {'restore': {'projectPath': str(project)}}, 'targets': {'net10.0': {}},
                    'libraries': {'A/1.0.0': {'type': 'package'}, 'B/2.0.0': {'type': 'project'}}}
            assets.write_text(json.dumps(data))
            self.assertEqual(sbom.expected_packages(source), {('a', '1.0.0')})
            data['project']['restore']['projectPath'] = str(root / 'Other.csproj')
            assets.write_text(json.dumps(data))
            with self.assertRaisesRegex(ValueError, 'another project'):
                sbom.expected_packages(source)


if __name__ == '__main__':
    unittest.main()
