#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
"""Classify strict publication; the approved upstream failure is never AOT certification."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

BASELINE = Path(__file__).with_name('inline-native-aot-known-diagnostics.json')
TESTS = {
    'SerializationFallback_RequiresResolverWhenReflectionIsDisabled',
    'GeneratedSerialization_PreservesEventAndMetadataWithoutReflection',
}


def diagnostics(log):
    # Only machine-dependent project/ILC paths are normalized. Member names and
    # complete analyzer messages remain part of the reviewed exception identity.
    result = []
    for line in log.splitlines():
        if not re.search(r'\b(?:error|warning)\s*(?:[a-z]+\d+\s*)?:', line, re.IGNORECASE):
            continue
        line = re.sub(r' \[[^\r\n]+[\\/]SqlElasticInlineAotProbe\.csproj\]$', ' [PROBE]', line)
        if ': error MSB3077:' in line:
            line = re.sub(r'^.*[\\/]Microsoft\.NETCore\.Native\.targets', 'Microsoft.NETCore.Native.targets', line)
            line = re.sub(r'""[^"\r\n]+[\\/]ilc"', '""ILC"', line)
        result.append(line.strip())
    return result


def verify_tests(directory):
    results = []
    for path in Path(directory).glob('sql-reflection-disabled*.trx'):
        root = ET.parse(path).getroot()
        for node in root.iter():
            if node.tag.endswith('}UnitTestResult'):
                results.append((node.attrib['testName'].rsplit('.', 1)[-1], node.attrib['outcome']))
    if Counter(results) != Counter((name, 'Passed') for name in TESTS):
        raise ValueError('Exactly the two reflection-disabled regressions must execute and pass.')
    return len(results)


def classify(log, exit_code, assets, properties, executable_exists, baseline):
    observed = diagnostics(log)
    for name in ('PublishAot', 'TreatWarningsAsErrors', 'ILLinkTreatWarningsAsErrors',
                 'SuppressTrimAnalysisWarnings', 'TrimmerSingleWarn'):
        if properties.get(name) != baseline['properties'][name]:
            raise ValueError('Strict native publication property changed: ' + name)
    if exit_code == 0:
        if observed or not executable_exists:
            raise ValueError('Successful strict publish requires no diagnostics and a fresh executable.')
        return {'decision': 'strict-publish-passed', 'publishStatus': 'passed',
                'publishExitCode': 0, 'aotCertified': False,
                'reason': 'Native runtime and recovery matrix still required.'}
    if exit_code != 1 or properties != baseline['properties']:
        raise ValueError('Unexpected publish exit or evaluated toolchain/properties.')
    libraries = assets['libraries']
    for name, version in baseline['packages'].items():
        matches = [key for key in libraries if key.split('/')[0].lower() == name.lower()]
        if matches != [name + '/' + version]:
            raise ValueError('Dependency differs from the approved exception: ' + name)
    if 'Generating native code' not in log or Counter(observed) != Counter(baseline['diagnostics']):
        raise ValueError('Strict diagnostics differ from the complete approved upstream failure.')
    return {'decision': 'accepted-upstream-limitation', 'publishStatus': 'failed',
            'publishExitCode': exit_code, 'aotCertified': False,
            'diagnosticCount': len(observed), 'reason': baseline['reason']}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for argument in ('log', 'assets', 'properties', 'results', 'executable', 'output'):
        parser.add_argument('--' + argument, required=True)
    parser.add_argument('--exit-code', required=True, type=int)
    args = parser.parse_args()
    verdict = {'decision': 'rejected', 'aotCertified': False, 'publishExitCode': args.exit_code}
    try:
        test_count = verify_tests(args.results)
        log = Path(args.log).read_text(encoding='utf-8-sig')
        print(f'EXAMINED: {test_count} reflection-disabled tests and {len(diagnostics(log))} strict publish diagnostics', file=sys.stderr)
        verdict = classify(log, args.exit_code,
                           json.loads(Path(args.assets).read_text(encoding='utf-8-sig')),
                           json.loads(Path(args.properties).read_text(encoding='utf-8-sig'))['Properties'],
                           Path(args.executable).is_file(), json.loads(BASELINE.read_text()))
        code = 0
    except (ValueError, KeyError, OSError, ET.ParseError) as error:
        verdict['reason'] = str(error)
        code = 1
    Path(args.output).write_text(json.dumps(verdict, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(verdict))
    return code


if __name__ == '__main__':
    raise SystemExit(main())
