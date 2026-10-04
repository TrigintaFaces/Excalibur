#!/usr/bin/env python3
"""Collect cache restore evidence from GitHub for an independently declared run context.

GitHub authenticates artifact origin and digest. Workflow/event SHA association is an
explicit caller assertion with retained basis, not inferred from observation payloads.
Only the latest completed attempt is accepted. Exit 2 means no measurement certified.
"""
import argparse
import base64
from datetime import datetime, timezone
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import tempfile
import threading
import time
import zipfile
import zlib

import yaml

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('cache_report', Path(__file__).with_name('cache-restore-report.py'))
report = importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)
MAX_ARCHIVE = 128 * 1024
MAX_PAYLOAD = 32 * 1024
MAX_ARTIFACTS = 256


def require(condition, message):
    if not condition:
        raise ValueError(message)


def pairs_unique(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'duplicate JSON key')
        result[key] = value
    return result


def decode(raw):
    def invalid_constant(value):
        raise ValueError('non-finite JSON value')
    return json.loads(raw, object_pairs_hook=pairs_unique, parse_constant=invalid_constant)


def validate_context(context):
    require(isinstance(context, dict) and context.get('schema') == 1, 'context schema must be 1')
    for field in ('source_sha', 'workflow_sha', 'event_sha'):
        require(isinstance(context.get(field), str) and re.fullmatch('[0-9a-f]{40}', context[field]), 'invalid context SHA')
    for field in ('run_id', 'attempt'):
        require(type(context.get(field)) is int and context[field] > 0, 'invalid context run identity')
    require(isinstance(context.get('repository'), str) and re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', context['repository']), 'invalid repository')
    require(isinstance(context.get('workflow_path'), str) and re.fullmatch(r'\.github/workflows/[A-Za-z0-9_.-]+\.ya?ml', context['workflow_path']), 'invalid workflow path')
    for field in ('event', 'basis', 'reviewer'):
        require(isinstance(context.get(field), str) and context[field].strip(), f'independent context {field} required')


class GitHub:
    def __init__(self, seconds=480):
        require(type(seconds) is int and 0 < seconds <= 1800, 'collection budget must be 1..1800 seconds')
        self.deadline = time.monotonic() + seconds

    def raw(self, endpoint, limit=4*1024*1024):
        remaining = self.deadline - time.monotonic()
        require(remaining > 0, 'collection deadline exhausted')
        # gh owns authentication and redirect handling. Never copy tokens or signed URLs into evidence.
        process = subprocess.Popen(['gh', 'api', '--hostname', 'github.com', '-X', 'GET', endpoint,
                                    '-H', 'X-GitHub-Api-Version: 2022-11-28'],
                                   stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
        chunks, errors = [], []

        def receive():
            try:
                size = 0
                while True:
                    chunk = process.stdout.read(min(65536, limit + 1 - size))
                    if not chunk:
                        break
                    size += len(chunk)
                    require(size <= limit, 'GitHub response exceeds collection limit')
                    chunks.append(chunk)
            except (OSError, ValueError) as exc:
                errors.append(str(exc))
                if process.poll() is None:
                    process.kill()
            finally:
                process.stdout.close()

        reader = threading.Thread(target=receive, daemon=True)
        reader.start()
        try:
            process.wait(timeout=min(60, remaining))
            reader.join(timeout=max(0, min(5, self.deadline - time.monotonic())))
            require(not reader.is_alive(), 'GitHub response stream did not finish')
            require(not errors, errors[0] if errors else 'GitHub stream failed')
            require(process.returncode == 0, f'GitHub request failed: {endpoint.split("?", 1)[0]}')
            return b''.join(chunks)
        finally:
            if process.poll() is None:
                process.kill()
                process.wait(timeout=5)
            reader.join(timeout=5)

    def json(self, endpoint):
        return decode(self.raw(endpoint))


def paged(client, endpoint, field):
    rows, seen = [], set()
    for page in range(1, 101):
        document = client.json(f'{endpoint}?per_page=100&page={page}')
        require(isinstance(document, dict) and isinstance(document.get(field), list), 'invalid paginated API response')
        batch = document[field]
        require(len(batch) <= 100, 'API page exceeds requested size')
        for row in batch:
            require(isinstance(row, dict) and type(row.get('id')) is int and row['id'] > 0, 'invalid API identity')
            require(row['id'] not in seen, 'duplicate API identity across pages')
            seen.add(row['id'])
        rows.extend(batch)
        if len(batch) < 100:
            return rows
    raise ValueError('API pagination exceeded collection bound')


def validate_run(run, context):
    require(isinstance(run, dict), 'invalid workflow run')
    require(isinstance(run.get('repository'), dict) and isinstance(run.get('head_repository'), dict), 'run repository objects missing')
    require(isinstance(run.get('path'), str) and isinstance(run['repository'].get('full_name'), str), 'run repository/path identity missing')
    for field, expected in (('id', context['run_id']), ('run_attempt', context['attempt']),
                            ('head_sha', context['source_sha']), ('event', context['event']),
                            ('status', 'completed')):
        require(type(run.get(field)) is type(expected) and run[field] == expected, f'run {field} mismatch')
    require(run.get('path', '').split('@', 1)[0] == context['workflow_path'], 'run workflow path mismatch')
    require(run.get('repository', {}).get('full_name', '').lower() == context['repository'].lower(), 'run repository mismatch')
    require(type(run.get('repository', {}).get('id')) is int and run['repository']['id'] > 0, 'run repository ID missing')
    require(type(run.get('head_repository', {}).get('id')) is int and run['head_repository']['id'] > 0, 'run head repository ID missing')
    require(run.get('conclusion') in ('success', 'failure', 'neutral', 'cancelled', 'skipped', 'timed_out', 'action_required', 'stale'), 'run conclusion missing')
    require(isinstance(run.get('updated_at'), str) and run['updated_at'], 'run update identity missing')


def frozen_run(run):
    return {key: run.get(key) for key in ('id', 'run_attempt', 'head_sha', 'event', 'path', 'status', 'conclusion', 'updated_at', 'repository', 'head_repository')}


def blob(client, repository, commit, path):
    item = client.json(f'repos/{repository}/contents/{path}?ref={commit}')
    require(isinstance(item, dict) and item.get('type') == 'file' and item.get('path') == path and item.get('encoding') == 'base64', 'immutable source blob unavailable')
    require(isinstance(item.get('content'), str), 'blob content missing')
    raw = base64.b64decode(''.join(item['content'].split()), validate=True)
    require(type(item.get('size')) is int and item['size'] == len(raw) and len(raw) <= 1024*1024, 'source blob size mismatch')
    git_hash = hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest()
    require(item.get('sha') == git_hash, 'source blob Git hash mismatch')
    return raw


def selected_artifacts(all_artifacts, run, context):
    selected, names = [], set()
    for artifact in all_artifacts:
        name = artifact.get('name', '')
        require(isinstance(name, str), 'invalid artifact name')
        if not name.startswith('ci-cache-'):
            continue
        match = re.fullmatch(r'ci-cache-([1-9][0-9]*)-([1-9][0-9]*)-([0-9a-f]{32})', name)
        require(match is not None and int(match[1]) == context['run_id'], 'malformed cache artifact identity')
        if int(match[2]) != context['attempt']:
            continue
        require(name not in names, 'duplicate cache artifact name')
        names.add(name)
        origin = artifact.get('workflow_run', {})
        require(isinstance(origin, dict), 'artifact origin object missing')
        require(origin.get('id') == run['id'] and origin.get('head_sha') == run['head_sha'] and
                origin.get('repository_id') == run['repository']['id'] and
                origin.get('head_repository_id') == run['head_repository']['id'], 'artifact origin mismatch')
        require(artifact.get('expired') is False, 'cache artifact expired')
        require(type(artifact.get('size_in_bytes')) is int and 0 < artifact['size_in_bytes'] <= MAX_ARCHIVE, 'cache artifact size invalid')
        require(isinstance(artifact.get('digest'), str) and re.fullmatch(r'sha256:[0-9a-f]{64}', artifact['digest']), 'cache artifact digest missing')
        selected.append(artifact)
    require(len(selected) <= MAX_ARTIFACTS, 'too many cache artifacts')
    return sorted(selected, key=lambda item: item['id'])


def unpack(artifact, raw):
    require(0 < len(raw) <= MAX_ARCHIVE and len(raw) == artifact['size_in_bytes'], 'archive size mismatch')
    require('sha256:' + hashlib.sha256(raw).hexdigest() == artifact['digest'], 'archive digest mismatch')
    identity = artifact['name'].rsplit('-', 1)[1]
    with zipfile.ZipFile(io.BytesIO(raw)) as archive:
        entries = archive.infolist()
        require(len(entries) == 1, 'cache archive must contain exactly one payload')
        entry = entries[0]
        require(entry.filename == identity + '.json' and not entry.is_dir(), 'unexpected cache payload path')
        require(not stat.S_ISLNK(entry.external_attr >> 16) and not entry.flag_bits & 1, 'unsupported archive entry')
        require(0 < entry.file_size <= MAX_PAYLOAD and entry.compress_type in (zipfile.ZIP_STORED, zipfile.ZIP_DEFLATED), 'cache payload size/encoding invalid')
        with archive.open(entry) as stream:
            payload = stream.read(MAX_PAYLOAD + 1)
        require(len(payload) == entry.file_size, 'cache payload exceeds bound')
        observation = decode(payload)
        require(isinstance(observation, dict) and observation.get('invocation_id') == identity, 'payload invocation mismatch')
    return observation


def collect(client, context, evidence):
    validate_context(context)
    repository = context['repository']
    prefix = f'repos/{repository}/actions/runs/{context["run_id"]}'
    run = client.json(prefix)
    evidence['run_before'] = run
    validate_run(run, context)
    workflow = blob(client, repository, context['workflow_sha'], context['workflow_path'])
    evidence['workflow_blob'] = {'sha256': hashlib.sha256(workflow).hexdigest(), 'text': workflow.decode('utf-8')}
    expected = report.expected_invocations(workflow.decode('utf-8'))
    evidence['expected'] = expected
    evidence['source_contracts'] = {}
    paths = ['eng/ci/record-cache-observation.py'] + [f'.github/actions/{action}/action.yml' for action in sorted({row['action'] for row in expected})]
    for path in paths:
        raw = blob(client, repository, context['source_sha'], path)
        supported = (ROOT / path).read_text(encoding='utf-8').replace('\r\n', '\n')
        evidence['source_contracts'][path] = {'sha256': hashlib.sha256(raw).hexdigest(), 'text': raw.decode('utf-8')}
        require(raw.decode('utf-8').replace('\r\n', '\n') == supported, 'source recorder/composite contract differs from supported revision')
    jobs = paged(client, prefix + f'/attempts/{context["attempt"]}/jobs', 'jobs')
    evidence['jobs'] = jobs
    raw_artifacts = paged(client, prefix + '/artifacts', 'artifacts')
    evidence['raw_artifacts_before'] = raw_artifacts
    artifacts = selected_artifacts(raw_artifacts, run, context)
    evidence['artifacts_before'] = artifacts
    evidence['downloads'] = []
    observations = []
    for artifact in artifacts:
        raw = client.raw(f'repos/{repository}/actions/artifacts/{artifact["id"]}/zip', limit=MAX_ARCHIVE)
        evidence['downloads'].append({'artifact_id': artifact['id'], 'archive_base64': base64.b64encode(raw).decode('ascii')})
        observation = unpack(artifact, raw)
        observations.append(observation)
    evidence['observations'] = observations
    raw_after_artifacts = paged(client, prefix + '/artifacts', 'artifacts')
    evidence['raw_artifacts_after'] = raw_after_artifacts
    after_artifacts = selected_artifacts(raw_after_artifacts, run, context)
    evidence['artifacts_after'] = after_artifacts
    require(artifacts == after_artifacts, 'artifact roster changed during collection')
    after_run = client.json(prefix)
    evidence['run_after'] = after_run
    validate_run(after_run, context)
    require(frozen_run(run) == frozen_run(after_run), 'run changed during collection')
    evidence['measurement'] = report.reconcile(expected, jobs, observations, context)
    evidence['status'] = 'measured' if evidence['measurement']['exact_restore_rate'] is not None else 'unmeasurable'
    return evidence


def save(path, evidence):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', dir=path.parent, delete=False) as output:
        temporary = output.name
        json.dump(evidence, output, indent=2, sort_keys=True, allow_nan=False)
    try:
        os.replace(temporary, path)
    finally:
        if os.path.exists(temporary):
            os.unlink(temporary)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--context', required=True, help='independently supplied expected identities and their basis')
    parser.add_argument('--evidence-file', required=True)
    parser.add_argument('--collection-timeout-seconds', type=int, default=480)
    args = parser.parse_args()
    evidence = {'schema': 1, 'status': 'collecting', 'started_at': datetime.now(timezone.utc).isoformat(),
                'collector_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
                'reconciler_sha256': hashlib.sha256(Path(report.__file__).read_bytes()).hexdigest(),
                'identity_basis': 'Caller asserts workflow/event SHA association; source head and artifact run origin checked against GitHub. Job, matrix and attempt attribution are producer assertions, not independent uploader-job authentication.'}
    try:
        raw_context = Path(args.context).read_bytes()
        context = decode(raw_context)
        evidence.update(context=context, context_sha256=hashlib.sha256(raw_context).hexdigest())
        collect(GitHub(args.collection_timeout_seconds), context, evidence)
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError, zipfile.BadZipFile, zlib.error, RuntimeError, yaml.YAMLError) as exc:
        evidence.update(status='refused', reason=str(exc))
    evidence['completed_at'] = datetime.now(timezone.utc).isoformat()
    save(args.evidence_file, evidence)
    print(f"Cache evidence: {evidence['status']}; context association is caller-asserted, not inferred from payloads.")
    if evidence['status'] == 'measured':
        print(f"Exact restore rate for selected composite invocations: {evidence['measurement']['exact_restore_rate']:.2f}%")
        return 0
    return 2


if __name__ == '__main__':
    raise SystemExit(main())
