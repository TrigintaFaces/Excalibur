"""Require the shipping restore graph's NuGet identities in a generated CycloneDX BOM."""
import argparse
import json
from pathlib import Path
from urllib.parse import unquote


def require(value, message):
    if not value:
        raise ValueError(message)


def expected_packages(source):
    solution = json.loads(source.read_text(encoding='utf-8-sig'))['solution']
    root = (source.parent / solution['path'].replace('\\', '/')).resolve().parent
    require(solution['projects'], 'Shipping project population is empty')
    expected = set()
    for relative in solution['projects']:
        project = root / relative.replace('\\', '/')
        require(project.is_file(), f'Missing shipping project: {project}')
        assets_path = project.parent / 'obj/project.assets.json'
        assets = json.loads(assets_path.read_text(encoding='utf-8-sig'))
        require(Path(assets['project']['restore']['projectPath']).resolve() == project.resolve(),
                f'Assets belong to another project: {assets_path}')
        require(assets.get('targets'), f'Restore has no targets: {assets_path}')
        for identity, library in assets['libraries'].items():
            if library['type'] == 'package':
                name, version = identity.rsplit('/', 1)
                expected.add((name.lower(), version.lower()))
    require(expected, 'Shipping restore graph contains no NuGet packages')
    return expected


def validate(bom, expected):
    require(bom.get('bomFormat') == 'CycloneDX', 'Not a CycloneDX BOM')
    require(expected, 'Expected package population is empty')
    observed = set()
    for component in bom.get('components', []):
        purl = component.get('purl', '')
        if not purl.startswith('pkg:nuget/'):
            continue
        identity = purl.removeprefix('pkg:nuget/').split('?', 1)[0].split('#', 1)[0]
        require('@' in identity, 'NuGet component lacks a version')
        name, version = (unquote(part).lower() for part in identity.rsplit('@', 1))
        require(component.get('name', '').lower() == name and component.get('version', '').lower() == version,
                'NuGet component name/version contradicts its package URL')
        observed.add((name, version))
    require(observed, 'BOM contains no NuGet components')
    missing = expected - observed
    require(not missing, 'BOM omits restored shipping dependencies: ' + ', '.join('/'.join(p) for p in sorted(missing)))
    return len(expected)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', default='eng/ci/shards/ShippingOnly.slnf')
    parser.add_argument('--bom', required=True)
    args = parser.parse_args()
    count = validate(json.loads(Path(args.bom).read_text(encoding='utf-8-sig')), expected_packages(Path(args.source).resolve()))
    print(f'EXAMINED: {count} resolved shipping NuGet identities')
    print(f'PASS: BOM covers all {count} resolved shipping NuGet identities.')
