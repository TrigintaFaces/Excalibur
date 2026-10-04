#!/usr/bin/env python3
"""Record one cache restore observation; never infer post-job cache save success."""
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from datetime import datetime, timezone
import uuid


def observation(env, source_sha, identity):
    required = ('GITHUB_REPOSITORY', 'GITHUB_RUN_ID', 'GITHUB_RUN_ATTEMPT', 'GITHUB_JOB',
                'GITHUB_SHA', 'CI_CACHE_ACTION', 'CI_CACHE_KEY', 'RUNNER_OS')
    if any(not env.get(key) for key in required):
        raise ValueError('missing cache observation identity')
    if not re.fullmatch(r'[^/\s]+/[^/\s]+', env['GITHUB_REPOSITORY']):
        raise ValueError('invalid repository identity')
    if any(not re.fullmatch(r'[1-9][0-9]*', env[key]) for key in ('GITHUB_RUN_ID', 'GITHUB_RUN_ATTEMPT')):
        raise ValueError('run ID and attempt must be positive integers')
    if any(not re.fullmatch(r'[0-9a-f]{40}', value) for value in (source_sha, env['GITHUB_SHA'])):
        raise ValueError('source and event SHA must be explicit commit identities')
    outcome, conclusion = env.get('CI_CACHE_OUTCOME'), env.get('CI_CACHE_CONCLUSION')
    if outcome not in ('success', 'failure', 'cancelled', 'skipped') or conclusion not in ('success', 'failure', 'cancelled', 'skipped'):
        raise ValueError('invalid cache step status')
    hit = env.get('CI_CACHE_HIT', '')
    if hit not in ('true', 'false', ''):
        raise ValueError('invalid raw cache-hit output')
    state = 'unknown'
    if outcome == conclusion == 'success':
        state = {'true': 'exact', 'false': 'non-exact', '': 'unknown'}[hit]
    matrix = json.loads(env['CI_CACHE_MATRIX']) if env.get('CI_CACHE_MATRIX') else None
    if matrix is not None and not isinstance(matrix, dict):
        raise ValueError('caller matrix context must be a JSON object')
    return {'schema': 1, 'kind': 'cache-restore', 'invocation_id': identity,
            'recorded_at': datetime.now(timezone.utc).isoformat(),
            'repository': env['GITHUB_REPOSITORY'], 'run_id': int(env['GITHUB_RUN_ID']),
            'attempt': int(env['GITHUB_RUN_ATTEMPT']), 'job_key': env['GITHUB_JOB'],
            'source_sha': source_sha, 'event_sha': env['GITHUB_SHA'],
            'workflow_sha': env.get('GITHUB_WORKFLOW_SHA'), 'runner_os': env['RUNNER_OS'],
            'runner_name': env.get('RUNNER_NAME'), 'action': env['CI_CACHE_ACTION'],
            'solution_filter': env.get('CI_CACHE_FILTER'), 'matrix_context': matrix,
            'primary_key': env['CI_CACHE_KEY'], 'restore_keys': env.get('CI_CACHE_RESTORE_KEYS', '').splitlines(),
            'outcome': outcome, 'conclusion': conclusion, 'raw_cache_hit': hit, 'restore_state': state,
            'save_state': 'not-observed'}


def main():
    try:
        source = subprocess.run(['git', 'rev-parse', 'HEAD'], check=True, capture_output=True, text=True, timeout=15).stdout.strip()
        identity = uuid.uuid4().hex
        record = observation(os.environ, source, identity)
        root = Path(os.environ['RUNNER_TEMP']) / 'ci-cache-observations'
        root.mkdir(parents=True, exist_ok=True)
        path = root / (identity + '.json')
        with path.open('x', encoding='utf-8') as output:
            json.dump(record, output, indent=2, sort_keys=True)
        # Paths and identity are generated, not taken from caller-supplied keys or matrix values.
        if '\n' in str(path) or '\r' in str(path):
            raise ValueError('invalid runner temporary path')
        with open(os.environ['GITHUB_OUTPUT'], 'a', encoding='utf-8') as output:
            output.write(f'id={identity}\npath={path.as_posix()}\n')
        print(f"Cache restore observation: {record['restore_state']} (save not observed)")
        return 0
    except (OSError, ValueError, KeyError, subprocess.SubprocessError) as exc:
        print(f'::error::cache observation unavailable: {exc}', file=sys.stderr)
        return 2


if __name__ == '__main__':
    sys.exit(main())
