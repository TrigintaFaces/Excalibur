#!/usr/bin/env python3
# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
"""Inventory workflow tooling steps; execute only explicitly reviewed whole steps.

This is not a GitHub Actions interpreter or a sandbox. Discovery includes every run
step, including advisory and unsupported contexts. Execution
requires a source-bound review plan, explicit selections, and resolved prerequisites.
Never infer permission to publish from a script's name or a successful inventory.

Exit codes: 0 inventory only OR complete supported local selection; 1 execution/input
mutation failure; 2 REFUSE; 3 self-test failure; 4 selected steps passed but inventory
remains outstanding or local execution differs from the workflow runner platform.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import tempfile
import time
import uuid


class Refuse(RuntimeError):
    pass


TOOLING = re.compile(r"(?:^|[\s\"'(/\\])(?:\.[/\\])?eng[/\\]", re.MULTILINE)
ALLOWED_EFFECTS = {"read", "build", "test", "pack", "benchmark"}
FORBIDDEN_EFFECTS = {"publish", "deploy", "security-change", "source-write"}


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode()).hexdigest()


def repository(start):
    for directory in (start, *start.parents):
        if (directory / '.git').exists():
            return directory
    raise Refuse('No repository root found; use --root.')


def safe_path(root, relative):
    if not isinstance(relative, str) or not relative or Path(relative).is_absolute():
        raise Refuse('Expected a nonempty repository-relative path.')
    path = (root / relative).resolve()
    if not path.is_relative_to(root):
        raise Refuse(f'Path escapes repository: {relative}')
    return path


def discover(root):
    try:
        import yaml
    except ImportError as error:
        raise Refuse('PyYAML is required; no workflows were examined.') from error
    folder = root / '.github/workflows'
    files = sorted([*folder.glob('*.yml'), *folder.glob('*.yaml')])
    if not files:
        raise Refuse('No workflow files found; an empty denominator is not verification.')
    inventory = []
    for path in files:
        try:
            document = yaml.safe_load(path.read_text(encoding='utf-8-sig'))
        except (OSError, yaml.YAMLError) as error:
            raise Refuse(f'Cannot parse {path.name}: {error}') from error
        if not isinstance(document, dict) or not isinstance(document.get('jobs'), dict):
            raise Refuse(f'{path.name} has no jobs mapping.')
        workflow_name = path.relative_to(root).as_posix()
        for job_id, job in document['jobs'].items():
            if not isinstance(job, dict):
                raise Refuse(f'Invalid job {workflow_name}:{job_id}.')
            defaults = dict((document.get('defaults') or {}).get('run') or {})
            defaults.update((job.get('defaults') or {}).get('run') or {})
            prior = []
            for index, step in enumerate(job.get('steps') or []):
                if not isinstance(step, dict):
                    raise Refuse(f'Invalid step {workflow_name}:{job_id}:{index}.')
                step_key = str(step.get('id') or f'index-{index}')
                identity = f'{workflow_name}::{job_id}::{step_key}'
                # Preserve action/setup provenance even when that step is not one of our gates.
                preparation = {'id': identity, 'definition': step}
                block = step.get('run')
                if isinstance(block, str):
                    environment = {}
                    environment_layers = []
                    for owner in (document, job, step):
                        candidate = owner.get('env') or {}
                        if not isinstance(candidate, dict):
                            raise Refuse(f'Invalid environment map in {identity}.')
                        environment_layers.append(candidate)
                        environment.update(candidate)
                    record = {
                        'id': identity, 'workflow': workflow_name, 'job': str(job_id),
                        'index': index, 'name': step.get('name'), 'run': block,
                        'mentions_repository_tooling': bool(TOOLING.search(block)),
                        'shell': step.get('shell', defaults.get('shell')),
                        'working_directory': step.get('working-directory', defaults.get('working-directory', '.')),
                        'environment': environment, 'runs_on': job.get('runs-on'),
                        'environment_layers': environment_layers,
                        'workflow_environment': document.get('env'),
                        'job_environment': job.get('environment'),
                        'job_if': job.get('if'), 'if': step.get('if'),
                        'needs': job.get('needs'), 'strategy': job.get('strategy'),
                        'services': job.get('services'), 'container': job.get('container'),
                        'permissions': job.get('permissions', document.get('permissions')),
                        'continue_on_error': step.get('continue-on-error', False),
                        'job_continue_on_error': job.get('continue-on-error', False),
                        'timeout_minutes': step.get('timeout-minutes', job.get('timeout-minutes')),
                        'prior_steps': list(prior),
                    }
                    record['fingerprint'] = digest(record)
                    inventory.append(record)
                prior.append(preparation)
    identities = [entry['id'] for entry in inventory]
    if len(identities) != len(set(identities)):
        raise Refuse('Duplicate workflow/job/step identity; give duplicate steps distinct IDs.')
    if not inventory:
        raise Refuse('No run steps discovered; no step denominator was established.')
    return inventory


def content_snapshot(root):
    """Hash tracked + nonignored untracked files, including initially dirty files.

    Git-ignored output artifacts are outside this input scope. Tracked reports remain
    inputs: changing them is reported, never hidden through an output allowance.
    """
    try:
        result = subprocess.run(['git', 'ls-files', '--cached', '--others', '--exclude-standard', '-z'],
                                cwd=root, capture_output=True, timeout=30, check=True)
    except (OSError, subprocess.SubprocessError) as error:
        raise Refuse(f'Cannot discover input content: {error}') from error
    entries = {}
    try:
        for raw in set(result.stdout.split(b'\0')) - {b''}:
            name = raw.decode('utf-8')
            path = root / name
            if path.is_symlink():
                raise Refuse(f'Symlink input needs explicit support: {name}')
            if not path.exists():
                entries[name] = {'missing': True}
                continue
            resolved = path.resolve()
            if not resolved.is_relative_to(root) or not resolved.is_file():
                raise Refuse(f'Unsupported input path: {name}')
            before = path.stat()
            hasher = hashlib.sha256()
            with path.open('rb') as source:
                while chunk := source.read(1024 * 1024):
                    hasher.update(chunk)
            after = path.stat()
            if (before.st_size, before.st_mtime_ns) != (after.st_size, after.st_mtime_ns):
                raise Refuse(f'Input changed while snapshotting: {name}')
            entries[name] = {'sha256': hasher.hexdigest()}
    except (OSError, UnicodeError) as error:
        raise Refuse(f'Cannot hash an input: {error}') from error
    return {'scope': 'all tracked and nonignored untracked files', 'files': entries, 'digest': digest(entries)}


def changed_inputs(before, after):
    left, right = before['files'], after['files']
    existing_left = {name for name, value in left.items() if not value.get('missing')}
    existing_right = {name for name, value in right.items() if not value.get('missing')}
    return {
        'changed': sorted(name for name in existing_left & existing_right if left[name] != right[name]),
        'added': sorted(existing_right - existing_left),
        'removed': sorted(existing_left - existing_right),
    }


def evidence(root, value, label):
    if not isinstance(value, dict) or not str(value.get('assertion', '')).strip():
        raise Refuse(f'{label} needs an explicit preparation assertion and hashed evidence.')
    path = safe_path(root, value.get('path'))
    try:
        actual = hashlib.sha256(path.read_bytes()).hexdigest()
    except OSError as error:
        raise Refuse(f'{label} evidence is unavailable: {error}') from error
    if actual != value.get('sha256'):
        raise Refuse(f'{label} evidence does not match its reviewed hash.')


def local_platform():
    return 'windows' if os.name == 'nt' else ('macos' if sys.platform == 'darwin' else 'linux')


def runner_platform(value):
    labels = value if isinstance(value, list) else [value]
    for label in labels:
        if isinstance(label, str):
            for prefix, platform in [('ubuntu-', 'linux'), ('windows-', 'windows'), ('macos-', 'macos')]:
                if label.startswith(prefix):
                    return platform
    return None


def validate(entry, review, root, snapshot, selected_ids=()):
    if not isinstance(review, dict) or review.get('fingerprint') != entry['fingerprint']:
        raise Refuse('Review plan does not match this complete workflow step definition.')
    if review.get('input_digest') != snapshot['digest']:
        raise Refuse('Review plan is stale: source content differs from the reviewed input digest.')
    effects = review.get('effects')
    if not isinstance(effects, list) or not effects or not all(isinstance(item, str) for item in effects) or set(effects) - ALLOWED_EFFECTS:
        raise Refuse(f'Effects must be reviewed local effects only; forbidden: {sorted(FORBIDDEN_EFFECTS)}.')
    if review.get('reviewed_transitive_effects') is not True:
        raise Refuse('Review must cover invoked scripts and their transitive effects, not just the shell line.')
    # This is a reviewed execution plan, not arbitrary shell safety inference or a sandbox.
    context = {key: value for key, value in entry.items() if key not in ('prior_steps', 'fingerprint')}
    if '${{' in json.dumps(context):
        raise Refuse('Unresolved GitHub expression: do not substitute or drop its context.')
    if entry['strategy'] or entry['services'] or entry['container'] or entry['job_environment']:
        raise Refuse('Matrix, services, container or protected environment needs faithful runner support.')
    if re.search(r'GITHUB_(?:ENV|OUTPUT|STATE|PATH|STEP_SUMMARY)', entry['run']):
        raise Refuse('GitHub runner file-command protocols are not emulated locally.')
    for name in ('job_if', 'if'):
        if entry[name] not in (None, True):
            raise Refuse(f'Conditional {name} is not evaluated by this local verifier.')
    prepared = review.get('prerequisites', {})
    if not isinstance(prepared, dict):
        raise Refuse('Prerequisites must be an evidence mapping.')
    for label, proof in prepared.items():
        evidence(root, proof, label)
    artifacts = review.get('input_artifacts')
    if not isinstance(artifacts, list):
        raise Refuse('Review must explicitly enumerate input_artifacts, including an empty list when none are consumed.')
    for index, proof in enumerate(artifacts):
        evidence(root, proof, f'Input artifact {index}')
    outputs = review.get('outputs')
    if not isinstance(outputs, list):
        raise Refuse('Review must explicitly enumerate output files, including an empty list for stdout-only checks.')
    for output in outputs:
        safe_path(root, output)
    for prior in entry['prior_steps']:
        if prior['id'] not in selected_ids:
            evidence(root, prepared.get(prior['id']), f"Prior step {prior['id']}")
    needs = entry['needs'] or []
    if isinstance(needs, str):
        needs = [needs]
    for job in needs:
        key = f"{entry['workflow']}::job:{job}"
        evidence(root, prepared.get(key), f'Dependent job {key}')
    expected = runner_platform(entry['runs_on'])
    if expected is None:
        raise Refuse('Unsupported or unspecified runner labels.')
    same_os = expected == local_platform()
    if not same_os:
        if review.get('runner_compatibility') != 'reviewed-local-variant' or not review.get('runner_rationale'):
            raise Refuse('Runner OS differs; an explicitly reviewed local variant is required.')
    elif review.get('runner_compatibility') != 'same-os':
        raise Refuse('Review must acknowledge matching runner OS.')
    timeout = review.get('timeout_seconds')
    if isinstance(timeout, bool) or not isinstance(timeout, (int, float)) or not math.isfinite(timeout) or not 0 < timeout <= 5400:
        raise Refuse('Execution timeout must be finite, positive and at most 5400 seconds.')
    if entry['timeout_minutes'] is not None:
        limit = entry['timeout_minutes']
        if not isinstance(limit, (int, float)) or timeout > limit * 60:
            raise Refuse('Local timeout must not exceed the workflow timeout.')
    directory = safe_path(root, entry['working_directory'])
    if not directory.is_dir():
        raise Refuse('Workflow working directory is unavailable.')
    # Windows environment names are case-insensitive; Python os.environ normalizes them
    # to uppercase. Merge the ORIGINAL ordered layers, not a case-sensitive flattened
    # dictionary whose insertion order can incorrectly let a job override its step.
    normalize = str.upper if os.name == 'nt' else lambda value: value
    environment = {normalize(key): value for key, value in os.environ.items()}
    literal_keys = set()
    layers = entry.get('environment_layers', [entry['environment']])
    for layer in layers:
        for key, value in layer.items():
            if (not isinstance(key, str) or not isinstance(value, str)
                    or '\0' in key + value or '=' in key):
                raise Refuse('Nonliteral environment value.')
            normalized = normalize(key)
            environment[normalized] = value
            literal_keys.add(normalized)
    reviewed_environment = review.get('environment') or {}
    if not isinstance(reviewed_environment, dict):
        raise Refuse('Reviewed environment must be a literal mapping.')
    for key, value in reviewed_environment.items():
        if not isinstance(key, str) or not isinstance(value, str) or '\0' in key + value or '=' in key:
            raise Refuse('Invalid reviewed environment value.')
        normalized = normalize(key)
        if normalized in literal_keys:
            raise Refuse('A local plan cannot override a literal workflow environment value.')
        environment[normalized] = value
    return directory, environment, float(timeout), same_os


def shell_script(entry, path, explicit_shell=None):
    shell = entry['shell']
    if shell is None:
        shell = 'pwsh' if runner_platform(entry['runs_on']) == 'windows' else 'default-bash'
    if shell not in ('bash', 'default-bash', 'pwsh'):
        raise Refuse(f'Unsupported shell/template: {shell}')
    executable = explicit_shell
    if executable is None and os.name == 'nt' and shell != 'pwsh':
        candidate = Path(os.environ.get('ProgramFiles', 'C:/Program Files')) / 'Git/bin/bash.exe'
        executable = str(candidate) if candidate.is_file() else None
    if executable is None:
        executable = shutil.which('pwsh' if shell == 'pwsh' else 'bash')
    if not executable or not Path(executable).is_file():
        raise Refuse(f'Explicit {shell} executable is unavailable.')
    allowed_name = ('pwsh', 'pwsh.exe') if shell == 'pwsh' else ('bash', 'bash.exe')
    if Path(executable).name.lower() not in allowed_name:
        raise Refuse('Shell executable does not match the declared supported shell.')
    if shell == 'pwsh':
        # GitHub's built-in PowerShell wrapper propagates native process exit codes too.
        body = "$ErrorActionPreference = 'Stop'\n" + entry['run']
        body += "\nif (Test-Path -LiteralPath variable:\\LASTEXITCODE) { exit $LASTEXITCODE }\n"
        path = path.with_suffix('.ps1')
        path.write_text(body, encoding='utf-8')
        return [str(executable), '-NoLogo', '-NoProfile', '-NonInteractive', '-File', str(path)]
    path = path.with_suffix('.sh')
    path.write_text(entry['run'], encoding='utf-8', newline='\n')
    arguments = ['--noprofile', '--norc', '-eo', 'pipefail'] if shell == 'bash' else ['-e']
    return [str(executable), *arguments, path.as_posix()]


def run_owned(argv, cwd, environment, timeout, log):
    if os.name == 'nt':
        # Assign the suspended primary process to a kill-on-close job BEFORE it can create children.
        from lib.pre_release_windows_job import run_owned_windows
        return run_owned_windows(argv, cwd, environment, timeout, log)
    with log.open('wb') as output:
        process = subprocess.Popen(argv, cwd=cwd, env=environment, stdin=subprocess.DEVNULL,
                                   stdout=output, stderr=subprocess.STDOUT, start_new_session=True)
        timed_out = False
        try:
            code = process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            timed_out = True
            code = 124
        finally:
            # Do not leave background descendants behind even when the primary exited successfully.
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            process.wait(timeout=10)
    return {'exit_code': code, 'timed_out': timed_out,
            'cleanup': 'SIGKILL delivered to owned process group; detached processes are unsupported'}


class InvocationReceipt:
    """An atomic, invocation-specific verdict, never a leftover success from a prior run."""

    TERMINAL = {'PASSED', 'PARTIAL', 'FAILED', 'REFUSED', 'ERROR', 'INTERRUPTED'}

    def __init__(self, root, output):
        self.root = root
        self.output = output.resolve()
        if not self.output.is_relative_to(root):
            raise Refuse('Receipt directory must be inside this repository.')
        ignored = subprocess.run(['git', 'check-ignore', '--quiet', '--',
                                  (self.output / 'receipt.json').relative_to(root).as_posix()],
                                 cwd=root, timeout=30, capture_output=True)
        if ignored.returncode != 0:
            raise Refuse('Receipt directory must be Git-ignored so the runner does not mutate its own input scope.')
        self.output.mkdir(parents=True, exist_ok=True)
        self.inventory = []
        self.report = {
            'schema': 1, 'mode': 'execution', 'run_id': uuid.uuid4().hex,
            'scope': 'selected local whole workflow run steps; actions and hosted CI not certified',
            'started_utc_epoch': time.time(), 'inventory_discovered': False,
            'discovered_logical_steps': None, 'matrix_instances_expanded': False,
            'results': [], 'outstanding': [], 'active_step': None,
            'all_discovered_steps_passed_locally': False,
        }
        # Replace an old PASS before source snapshot, plan or selection validation.
        self.publish('NOT_RUN')

    def publish(self, state, **updates):
        self.report.update(updates)
        self.report['state'] = state
        self.report['updated_utc_epoch'] = time.time()
        passed = {row['id'] for row in self.report['results'] if row['outcome'] == 'PASS'}
        self.report['outstanding'] = [row['id'] for row in self.inventory if row['id'] not in passed]
        self.report['all_discovered_steps_passed_locally'] = (
            state == 'PASSED' and bool(self.inventory)
            and not self.report['outstanding']
            and all(row['same_runner_os'] for row in self.report['results']))
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(mode='w', encoding='utf-8', dir=self.output,
                                             prefix='.receipt-', suffix='.tmp', delete=False) as destination:
                temporary = Path(destination.name)
                json.dump(self.report, destination, indent=2)
                destination.flush()
                os.fsync(destination.fileno())
            os.replace(temporary, self.output / 'receipt.json')
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)

    def refuse(self, error):
        state = 'INTERRUPTED' if isinstance(error, KeyboardInterrupt) else 'REFUSED'
        self.publish(state, error=f'{type(error).__name__}: {error}')


def execute(root, inventory, selections, plan, output, explicit_shell=None, receipt=None):
    current = receipt or InvocationReceipt(root, output)
    try:
        return _execute(root, inventory, selections, plan, explicit_shell, current)
    except BaseException as error:
        current.refuse(error)
        raise


def _execute(root, inventory, selections, plan, explicit_shell, receipt):
    output = receipt.output
    receipt.inventory = inventory
    receipt.publish('NOT_RUN', inventory_discovered=True, discovered_logical_steps=len(inventory))
    mapping = {step['id']: step for step in inventory}
    if not selections or len(selections) != len(set(selections)) or set(selections) - mapping.keys():
        raise Refuse('Select distinct known step IDs explicitly.')
    if not isinstance(plan, dict) or plan.get('schema') != 1 or not isinstance(plan.get('steps'), dict):
        raise Refuse('Execution requires a schema-1 reviewed plan.')
    initial = content_snapshot(root)
    # Validate ALL selected steps before ANY side effect; retain workflow order, not selection order.
    selected = [step for step in inventory if step['id'] in selections]
    for step in selected:
        validate(step, plan['steps'].get(step['id']), root, initial, selections)
    (output / 'input-snapshot.json').write_text(json.dumps(initial, indent=2), encoding='utf-8')
    results = receipt.report['results']
    receipt.publish('RUNNING', review_plan_digest=digest(plan), input_snapshot='input-snapshot.json')
    with tempfile.TemporaryDirectory(prefix='release-steps-') as temporary:
        # Resolve every shell/template before the first selected gate can execute.
        commands = {step['id']: shell_script(step, Path(temporary) / str(index), explicit_shell)
                    for index, step in enumerate(selected)}
        for ordinal, step in enumerate(selected):
            receipt.publish('RUNNING', active_step={'id': step['id'], 'phase': 'input-snapshot'})
            before = content_snapshot(root)
            if before['digest'] != initial['digest']:
                raise Refuse('Inputs changed between selected steps; review and restart from the new epoch.')
            # Ignored artifacts/evidence can change without changing the source digest.
            # Revalidate hashes at consumption, not only at the initial all-step preflight.
            receipt.publish('RUNNING', active_step={'id': step['id'], 'phase': 'pre-execution-validation'})
            directory, environment, timeout, same_os = validate(
                step, plan['steps'].get(step['id']), root, before, selections)
            started = time.time()
            log = output / f'{ordinal:03d}-{digest(step["id"])[:12]}.log'
            receipt.publish('RUNNING', active_step={'id': step['id'], 'phase': 'execution',
                                                   'log': log.relative_to(root).as_posix()})
            try:
                process = run_owned(commands[step['id']], directory, environment, timeout, log)
            except (OSError, subprocess.SubprocessError) as error:
                process = {'exit_code': None, 'timed_out': False, 'error': str(error), 'cleanup': 'execution failed; see error'}
            receipt.publish('RUNNING', active_step={'id': step['id'], 'phase': 'post-execution-proof', 'process': process})
            after = content_snapshot(root)
            (output / f'after-{ordinal:03d}.json').write_text(json.dumps(after, indent=2), encoding='utf-8')
            changes = changed_inputs(before, after)
            mutated = any(changes.values())
            produced = []
            missing_outputs = []
            for name in plan['steps'][step['id']]['outputs']:
                path = safe_path(root, name)
                if not path.is_file() or path.stat().st_mtime_ns < int(started * 1_000_000_000):
                    missing_outputs.append(name)
                else:
                    produced.append({'path': name, 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})
            passed = process['exit_code'] == 0 and not process['timed_out'] and not mutated and not missing_outputs
            results.append({'id': step['id'], 'fingerprint': step['fingerprint'],
                            'workflow': step['workflow'], 'job': step['job'], 'index': step['index'],
                            'continue_on_error': step['continue_on_error'],
                            'job_continue_on_error': step['job_continue_on_error'],
                            'input_digest': before['digest'], 'after_digest': after['digest'],
                            'outcome': 'PASS' if passed else 'FAIL', 'same_runner_os': same_os,
                            'started_utc_epoch': started, 'elapsed_seconds': time.time() - started,
                            'process': process, 'input_changes': changes,
                            'outputs': produced, 'missing_or_stale_outputs': missing_outputs,
                            'log': log.relative_to(root).as_posix()})
            receipt.publish('RUNNING', active_step=None)
            if not passed:
                break
    passed_ids = {result['id'] for result in results if result['outcome'] == 'PASS'}
    outstanding = [step['id'] for step in inventory if step['id'] not in passed_ids]
    failed = any(result['outcome'] != 'PASS' for result in results)
    same_platform = all(result['same_runner_os'] for result in results)
    code = 1 if failed else (4 if outstanding or not same_platform else 0)
    receipt.publish('FAILED' if failed else ('PARTIAL' if code == 4 else 'PASSED'), active_step=None)
    return code, receipt.report


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path)
    parser.add_argument('--list', action='store_true', help='Inventory only (also the default).')
    parser.add_argument('--execute', action='store_true')
    parser.add_argument('--step', action='append', default=[])
    parser.add_argument('--plan', type=Path)
    parser.add_argument('--output', type=Path, default=Path('artifacts/pre-release-gates'))
    parser.add_argument('--shell-executable', help='Explicit supported Bash or PowerShell executable.')
    parser.add_argument('--self-test', action='store_true')
    arguments = parser.parse_args(argv)
    if arguments.self_test:
        # Kept explicit and isolated: never run the real workflow population as a self-test.
        tests = Path(__file__).with_name('pre-release-gates.test.py')
        if not tests.is_file():
            print('REFUSE: self-test module missing.', file=sys.stderr)
            return 3
        try:
            with tempfile.TemporaryDirectory(prefix='release-selftest-') as temporary:
                log = Path(temporary) / 'selftest.log'
                result = run_owned([sys.executable, str(tests)], tests.parent, os.environ.copy(), 180, log)
                print(log.read_text(encoding='utf-8', errors='replace'))
                return 0 if result['exit_code'] == 0 and not result['timed_out'] else 3
        except (OSError, subprocess.SubprocessError) as error:
            print(f'Self-test REFUSE: {error}', file=sys.stderr)
            return 3
    receipt = None
    try:
        root = (arguments.root or repository(Path(__file__).resolve().parent)).resolve()
        if arguments.execute:
            output = arguments.output if arguments.output.is_absolute() else safe_path(root, str(arguments.output))
            receipt = InvocationReceipt(root, output)
        inventory = discover(root)
        # EXAMINED: the denominator every gate owes before it renders a verdict. On STDERR
        # deliberately -- stdout carries a JSON document a consumer pipes into a parser, and a
        # bare line there would corrupt it. A zero inventory means the workflow-step discovery
        # matcher stopped matching, which is a REFUSE: an inventory over an empty population
        # reports the same shape as a clean one, which is the false green this marker removes.
        print(f'EXAMINED: {len(inventory)} workflow tooling step(s)', file=sys.stderr)
        if not inventory:
            print('REFUSE: pre-release-gates.py examined 0 workflow tooling step(s) — a verdict '
                  'over an empty population is not a PASS. The matcher, the path, or the '
                  'workflow set changed.', file=sys.stderr)
            return 2
        if receipt is not None:
            receipt.inventory = inventory
            receipt.publish('NOT_RUN', inventory_discovered=True, discovered_logical_steps=len(inventory))
        if not arguments.execute:
            if arguments.step or arguments.plan:
                raise Refuse('--step/--plan require --execute; no implicit execution.')
            snapshot = content_snapshot(root)
            print(json.dumps({'schema': 1, 'mode': 'inventory', 'verification': 'NOT_RUN',
                              'logical_steps': len(inventory), 'matrix_instances_expanded': False,
                              'input_digest': snapshot['digest'], 'steps': inventory}, indent=2))
            return 0
        if arguments.list or arguments.plan is None:
            raise Refuse('Execution needs --plan and is incompatible with --list.')
        plan = json.loads(arguments.plan.read_text(encoding='utf-8-sig'))
        code, report = execute(root, inventory, arguments.step, plan, output, arguments.shell_executable, receipt)
        print(json.dumps(report, indent=2))
        return code
    except (Refuse, OSError, ValueError, TypeError, subprocess.SubprocessError) as error:
        if receipt is not None and receipt.report['state'] not in InvocationReceipt.TERMINAL:
            receipt.refuse(error)
        if arguments.execute and receipt is None:
            print('No current-invocation receipt was initialized; do not reuse any existing receipt.', file=sys.stderr)
        print(f'REFUSE: {error}. Nothing refused was verified.', file=sys.stderr)
        return 2
    except BaseException as error:
        if receipt is not None and receipt.report['state'] not in InvocationReceipt.TERMINAL:
            receipt.refuse(error)
        raise


if __name__ == '__main__':
    raise SystemExit(main())
