#!/usr/bin/env python3
"""Derive cache populations and reconcile collected evidence without dropping missing rows.

This module's reconciliation API requires authenticated collection inputs. It does not
authenticate downloaded artifacts. The CLI previews a local workflow population only;
it deliberately cannot certify hosted measurements from a local working-tree file.
"""
import argparse
import itertools
import json
import posixpath
from datetime import datetime
from pathlib import Path
import re

import yaml

ACTIONS = {'./.github/actions/build-and-test': 'build-and-test',
           './.github/actions/setup-dotnet-build': 'setup-dotnet-build'}
EXPRESSION = re.compile(r'\$\{\{\s*matrix\.([a-zA-Z_][\w.-]*)\s*\}\}')


class UniqueLoader(yaml.SafeLoader):
    pass


def unique_mapping(loader, node, deep=False):
    result = {}
    for key_node, value_node in node.value:
        key = loader.construct_object(key_node, deep=deep)
        if key in result:
            raise ValueError(f'duplicate workflow mapping key: {key}')
        result[key] = loader.construct_object(value_node, deep=deep)
    return result


UniqueLoader.add_constructor(yaml.resolver.BaseResolver.DEFAULT_MAPPING_TAG, unique_mapping)


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(',', ':'), allow_nan=False)


def render(value, matrix):
    if not isinstance(value, str):
        raise ValueError('expected a workflow string')

    def replace(match):
        part = matrix
        for name in match.group(1).split('.'):
            if not isinstance(part, dict) or name not in part:
                raise ValueError('unresolved matrix reference')
            part = part[name]
        if not isinstance(part, (str, int)) or isinstance(part, bool):
            raise ValueError('non-scalar matrix interpolation')
        return str(part)

    result = EXPRESSION.sub(replace, value)
    if '${{' in result:
        raise ValueError('unsupported workflow expression; population unresolved')
    return result


def matrices(job):
    strategy = job.get('strategy', {})
    if not isinstance(strategy, dict):
        raise ValueError('dynamic strategy cannot establish population')
    matrix = strategy.get('matrix')
    if matrix is None:
        return [{}]
    if not isinstance(matrix, dict) or not matrix or {'include', 'exclude'} & matrix.keys():
        raise ValueError('unsupported matrix expansion; population unresolved')
    count = 1
    for values in matrix.values():
        if not isinstance(values, list) or not values:
            raise ValueError('matrix axis must be a nonempty literal array')
        if '${{' in canonical(values):
            raise ValueError('dynamic matrix values cannot establish population')
        count *= len(values)
    if count > 256:
        raise ValueError('matrix exceeds supported population bound')
    return [dict(zip(matrix, combination)) for combination in itertools.product(*matrix.values())]


def row_key(row):
    return row['job_key'], canonical(row['matrix_context']), row['action']


def shared_action(value):
    if not isinstance(value, str):
        return None
    normalized = posixpath.normpath(value.replace('\\', '/')).casefold()
    for path, action in ACTIONS.items():
        if normalized == path[2:].casefold():
            if value != path:
                raise ValueError('noncanonical shared-composite path; population unresolved')
            return action
    return None


def skipped_without_execution(job):
    if job.get('steps') != []:
        return False
    start, end = job.get('started_at'), job.get('completed_at')
    if start is None and end is None:
        return True
    if not isinstance(start, str) or not isinstance(end, str):
        return False
    try:
        start, end = (datetime.fromisoformat(value.replace('Z', '+00:00')) for value in (start, end))
        return start.tzinfo is not None and end.tzinfo is not None and start == end
    except ValueError:
        return False


def expected_invocations(workflow_text):
    workflow = yaml.load(workflow_text, Loader=UniqueLoader)
    if not isinstance(workflow, dict) or not isinstance(workflow.get('jobs'), dict):
        raise ValueError('workflow jobs missing')
    rows, names, identities = [], set(), set()
    for job_key, job in workflow['jobs'].items():
        if not isinstance(job, dict):
            raise ValueError('invalid job definition')
        steps = job.get('steps', [])
        if not isinstance(steps, list) or any(not isinstance(step, dict) for step in steps):
            raise ValueError('invalid job steps')
        calls = [step for step in steps if shared_action(step.get('uses'))]
        if not calls:
            continue
        if len(calls) != 1:
            raise ValueError('multiple composite calls need explicit call identity')
        call = calls[0]
        if 'if' in call:
            raise ValueError('conditional composite call needs independent skip evidence')
        inputs = call.get('with', {})
        if not isinstance(inputs, dict):
            raise ValueError('invalid composite inputs')
        variants = matrices(job)
        has_matrix = 'matrix' in job.get('strategy', {})
        required_context = '${{ toJSON(matrix) }}' if has_matrix else '{}'
        if inputs.get('telemetry-matrix') != required_context:
            raise ValueError(f'{job_key}: explicit telemetry matrix context missing')
        name_template = job.get('name', job_key)
        if has_matrix and not EXPRESSION.search(name_template):
            raise ValueError('matrix job requires an explicit distinguishable rendered name')
        for matrix in variants:
            row = {'job_key': job_key, 'matrix_context': matrix,
                   'action': ACTIONS[call['uses']], 'job_name': render(name_template, matrix),
                   'solution_filter': render(inputs.get('solution-filter', ''), matrix)}
            identity = row_key(row)
            if identity in identities or row['job_name'] in names:
                raise ValueError('ambiguous expanded cache invocation')
            identities.add(identity)
            names.add(row['job_name'])
            rows.append(row)
    if not rows:
        raise ValueError('no direct shared-composite cache invocations')
    return rows


def reconcile(expected, jobs, observations, context):
    """Reconcile already authenticated inputs for one frozen run attempt.

    Caller must independently establish expected source/workflow/event SHAs, obtain
    workflow and composite blobs at those revisions, and authenticate artifact origin
    and integrity before passing payloads here. Never derive context from observations.
    """
    for name in ('source_sha', 'workflow_sha', 'event_sha'):
        if not isinstance(context.get(name), str) or not re.fullmatch('[0-9a-f]{40}', context[name]):
            raise ValueError('independent source/workflow/event identities required')
    for name in ('run_id', 'attempt'):
        if type(context.get(name)) is not int or context[name] < 1:
            raise ValueError('positive run identity required')
    if not isinstance(context.get('repository'), str) or not re.fullmatch(r'[^/\s]+/[^/\s]+', context['repository']):
        raise ValueError('repository identity required')
    expected_by_key = {row_key(row): row for row in expected}
    if not expected or len(expected_by_key) != len(expected) or len({r['job_name'] for r in expected}) != len(expected):
        raise ValueError('empty or ambiguous expected population')
    by_name, job_ids = {}, set()
    for job in jobs:
        if job.get('status') == 'completed' and job.get('conclusion') not in ('success', 'failure', 'neutral', 'cancelled', 'skipped', 'timed_out', 'action_required', 'stale'):
            raise ValueError('completed job has no recognized conclusion')
        if type(job.get('id')) is not int or job['id'] < 1 or job['id'] in job_ids:
            raise ValueError('missing or duplicate API job identity')
        if job.get('run_id') != context['run_id'] or job.get('head_sha') != context['source_sha']:
            raise ValueError('API job source/run mismatch')
        if type(job.get('run_attempt', context['attempt'])) is not int or job.get('run_attempt', context['attempt']) != context['attempt']:
            raise ValueError('API job attempt mismatch')
        job_ids.add(job['id'])
        by_name.setdefault(job.get('name'), []).append(job)
    by_row, invocation_ids = {}, set()
    for observation in observations:
        if observation.get('schema') != 1 or observation.get('kind') != 'cache-restore':
            raise ValueError('unsupported observation schema')
        for field in ('repository', 'run_id', 'attempt', 'source_sha', 'workflow_sha', 'event_sha'):
            if type(observation.get(field)) is not type(context[field]) or observation[field] != context[field]:
                raise ValueError(f'observation {field} mismatch')
        identity = observation.get('invocation_id', '')
        if not isinstance(identity, str) or not re.fullmatch('[0-9a-f]{32}', identity) or identity in invocation_ids:
            raise ValueError('missing or duplicate invocation identity')
        invocation_ids.add(identity)
        if not isinstance(observation.get('matrix_context'), dict):
            raise ValueError('missing invocation matrix context')
        try:
            key = row_key(observation)
        except KeyError as exc:
            raise ValueError('missing invocation identity fields') from exc
        if key not in expected_by_key or key in by_row:
            raise ValueError('unexpected or duplicate invocation evidence')
        if observation.get('solution_filter') != expected_by_key[key]['solution_filter']:
            raise ValueError('observation solution filter mismatch')
        outcome, conclusion, raw = (observation.get(k) for k in ('outcome', 'conclusion', 'raw_cache_hit'))
        if outcome not in ('success', 'failure', 'skipped', 'cancelled') or conclusion not in ('success', 'failure', 'skipped', 'cancelled'):
            raise ValueError('invalid restore outcome')
        if raw not in ('true', 'false', ''):
            raise ValueError('invalid raw cache result')
        state = {'true': 'exact', 'false': 'non-exact', '': 'unknown'}[raw] if outcome == conclusion == 'success' else 'unknown'
        if observation.get('restore_state') != state or observation.get('save_state') != 'not-observed':
            raise ValueError('observation contradicts raw restore evidence')
        by_row[key] = observation
    result = {'scope': 'direct shared-composite cache invocations', 'expected': len(expected),
              'excluded_skipped_jobs': 0, 'missing_jobs': 0, 'pending_jobs': 0,
              'missing_observations': 0, 'unknown_restores': 0, 'exact': 0,
              'non_exact': 0, 'exact_restore_rate': None, 'rows': []}
    for key, row in expected_by_key.items():
        matches = by_name.get(row['job_name'], [])
        if len(matches) > 1:
            raise ValueError('ambiguous API job-name match')
        observation = by_row.get(key)
        if not matches:
            if observation:
                raise ValueError('observation has no matching API job')
            state = 'missing_jobs'
        else:
            job = matches[0]
            if job.get('status') != 'completed':
                state = 'pending_jobs'
            elif job.get('conclusion') == 'skipped':
                if observation or not skipped_without_execution(job):
                    raise ValueError('skipped-job exclusion conflicts with execution evidence')
                state = 'excluded_skipped_jobs'
            elif not observation:
                state = 'missing_observations'
            else:
                state = {'exact': 'exact', 'non-exact': 'non_exact', 'unknown': 'unknown_restores'}[observation['restore_state']]
        result[state] += 1
        result['rows'].append(dict(row, state=state))
    unknown = sum(result[key] for key in ('missing_jobs', 'pending_jobs', 'missing_observations', 'unknown_restores'))
    denominator = result['expected'] - result['excluded_skipped_jobs']
    result['resolved_denominator'] = denominator if not (result['missing_jobs'] or result['pending_jobs']) else None
    if not unknown and denominator:
        result['exact_restore_rate'] = result['exact'] / denominator * 100
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--workflow', default='.github/workflows/ci.yml')
    args = parser.parse_args()
    try:
        rows = expected_invocations(Path(args.workflow).read_text(encoding='utf-8'))
        print(json.dumps({'scope': 'local population preview, not hosted evidence', 'rows': rows}, indent=2))
        return 0
    except (OSError, ValueError, yaml.YAMLError) as exc:
        print(f'REFUSE: {exc}')
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
