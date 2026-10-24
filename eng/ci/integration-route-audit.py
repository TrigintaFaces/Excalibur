"""Resolve supported scheduled integration routes; compare independent adapter identities.

This verifies routing, not execution. Unsupported workflow shapes refuse evidence.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys


def require(value, message):
    if not value:
        raise ValueError(message)


def routes(repo):
    workflow = repo / '.github/workflows/ci.yml'
    text = workflow.read_text(encoding='utf-8-sig')
    match = re.search(r'^  integration-tests:\n(.*?)(?=^  [a-zA-Z0-9_-]+:|\Z)', text, re.M | re.S)
    require(match, 'Missing integration job')
    body = match[1]
    require(not re.search(r'^    if:', body, re.M), 'Conditional integration job is unsupported')
    require('    needs: smoke-tests\n' in body, 'Integration prerequisites changed')
    matrix = body.split('        shard:\n', 1)[1].split('    needs:', 1)[0]
    matrix = '\n'.join(line for line in matrix.splitlines() if line.strip() and not line.lstrip().startswith('#')) + '\n'
    pattern = (r'          - name: ([a-z0-9-]+)\n'
               r"            leg: (''|[a-z]+)\n"
               r'            filter: (eng/ci/shards/IntegrationTests-[A-Za-z]+\.slnf)\n'
               r'            probe: ([A-Za-z0-9_./-]+\.csproj)\n')
    rows = list(re.finditer(pattern, matrix))
    require(rows and ''.join(row[0] for row in rows) == matrix, 'Unsupported integration matrix shape')
    steps = re.split(r'^      - name: ', body, flags=re.M)[1:]
    composer = [step for step in steps if step.startswith('Compose integration test filter\n')]
    runner = [step for step in steps if step.startswith('Run Integration Tests\n')]
    require(len(composer) == len(runner) == 1, 'Missing unique composer/execution step')
    for step in composer + runner:
        require(not re.search(r'^        if:', step, re.M), 'Conditional integration step is unsupported')
    line = "          $filterInfo = ./eng/ci/get-integration-test-filter.ps1 -Leg '${{ matrix.shard.leg }}' | ConvertFrom-Json"
    executable = [value for value in composer[0].split('        run: |\n', 1)[1].splitlines()
                  if value.strip() and not value.lstrip().startswith('#')]
    require(executable == [line,
        '          "INTEGRATION_TEST_FILTER=$($filterInfo.filter)" >> $env:GITHUB_ENV',
        '          Write-Host "Using integration filter with $($filterInfo.count) quarantined exclusions."'],
        'Execution does not use the supported composer/export body')
    require(text.count('INTEGRATION_TEST_FILTER') == 3, 'Execution filter has an additional use or override')
    require('\n          dotnet test ${{ matrix.shard.filter }} \\\n' in runner[0] and
            '\n            --filter "${{ env.INTEGRATION_TEST_FILTER }}" \\\n' in runner[0], 'Execution source/filter binding changed')
    result = []
    for row in rows:
        name, leg, source, _ = row.groups()
        if leg != "''":
            continue  # This audit covers embedded unit assemblies, not namespace-split integration assemblies.
        path = repo / source
        solution = json.loads(path.read_text())['solution']
        solution_root = (path.parent / solution['path'].replace('\\', '/')).resolve().parent
        projects = [(solution_root / p.replace('\\', '/')).resolve().relative_to(repo).as_posix()
                    for p in solution['projects']]
        result.append({'name': name, 'source': source, 'leg': '',
                       'projects': projects,
                       'sourceSha256': hashlib.sha256(path.read_bytes()).hexdigest()})
    require(result, 'No supported scheduled integration routes')
    return {'workflowSha256': hashlib.sha256(workflow.read_bytes()).hexdigest(), 'routes': result}


def compare(required, admitted):
    require(required.get('purpose') == 'execution', 'Wrong discovery purpose')
    require(required['discoveredCount'] == len(required['tests']), 'Incomplete independent discovery')
    expected = {case['id'] for case in required['tests']}
    require(len(expected) == len(required['tests']), 'Duplicate independent identity')
    observed = set()
    for census in admitted:
        for key in ('context', 'assemblySha256', 'adapterSha256', 'sdk', 'inputBundle'):
            require(census[key] == required[key], f'Route discovery input/context mismatch: {key}')
        require(census['discoveredCount'] == len(census['tests']), 'Incomplete route discovery')
        observed.update(case['id'] for case in census['tests'])
    missing = expected - observed
    require(not missing, 'Integration cases have no scheduled route: ' + ', '.join(
        case['name'] for case in required['tests'] if case['id'] in missing))
    return len(expected)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--repo', default='.')
    parser.add_argument('--required')
    parser.add_argument('--admitted', nargs='*', default=[])
    args = parser.parse_args()
    if args.required:
        count = compare(json.loads(Path(args.required).read_text(encoding='utf-8-sig')),
                        [json.loads(Path(p).read_text(encoding='utf-8-sig')) for p in args.admitted])
        print(f'EXAMINED: {count} independently discovered test identities', file=sys.stderr)
        print(count)
    else:
        result = routes(Path(args.repo).resolve())
        print(f"EXAMINED: {len(result['routes'])} scheduled integration routes", file=sys.stderr)
        print(json.dumps(result))
