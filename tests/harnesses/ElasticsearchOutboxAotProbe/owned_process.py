# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
"""Private one-command owner: existing Windows Job helper; bounded Linux group/subreaper.

Both output streams go directly to one retained file, never redirected pipes.
Detached Linux descendants are killed if adopted, but make the invocation refuse.
This is not a general task runner; it serves this native regression harness only.
"""
import argparse
import ctypes
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import sys
sys.dont_write_bytecode = True
import time


def linux_owned(argv, cwd, environment, timeout, log):
    libc = ctypes.CDLL(None, use_errno=True)
    previous = ctypes.c_int()
    if libc.prctl(37, ctypes.byref(previous), 0, 0, 0) != 0 or libc.prctl(36, 1, 0, 0, 0) != 0:
        raise OSError(ctypes.get_errno(), 'Cannot establish scoped Linux subreaper ownership')
    children_path = Path(f'/proc/self/task/{os.getpid()}/children')
    def children():
        return [int(value) for value in children_path.read_text().split()]
    process = None
    escaped = False
    reaped = []
    result = {'exit_code': 125, 'timed_out': False, 'cleanup_verified': False}
    try:
        if children():
            raise RuntimeError('Refuse ownership when this private wrapper already has children')
        with log.open('wb') as output:
            process = subprocess.Popen(argv, cwd=cwd, env=environment, stdin=subprocess.DEVNULL,
                                       stdout=output, stderr=subprocess.STDOUT, start_new_session=True)
            try:
                result['exit_code'] = process.wait(timeout=timeout)
            except subprocess.TimeoutExpired:
                result.update(exit_code=124, timed_out=True)
            finally:
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
                process.wait(timeout=10)
                deadline = time.monotonic() + 5
                while True:
                    while True:
                        try:
                            pid, _ = os.waitpid(-1, os.WNOHANG)
                        except ChildProcessError:
                            break
                        if pid == 0:
                            break
                        reaped.append(pid)
                    remaining = children()
                    for pid in remaining:
                        try:
                            if os.getpgid(pid) != process.pid:
                                escaped = True
                            # Every PID here is a direct child adopted by this sole-command subreaper.
                            os.kill(pid, signal.SIGKILL)
                        except ProcessLookupError:
                            pass
                    try:
                        os.killpg(process.pid, 0)
                        group_alive = True
                    except ProcessLookupError:
                        group_alive = False
                    if not remaining and not group_alive:
                        result['cleanup_verified'] = True
                        break
                    if time.monotonic() >= deadline:
                        raise RuntimeError('Owned Linux processes remain after bounded termination/reaping')
                    time.sleep(0.02)
    finally:
        if libc.prctl(36, previous.value, 0, 0, 0) != 0:
            raise OSError(ctypes.get_errno(), 'Cannot restore Linux subreaper setting')
    result.update(cleanup='owned Linux group and adopted children have zero processes',
                  reaped_children=reaped, detached_descendant_refused=escaped)
    if escaped:
        result.update(exit_code=125, execution_error='Detached descendants are outside the supported command contract')
    return result


def run_command(repo, argv, cwd, timeout, log, receipt):
    started = time.time()
    result = {'argv': argv, 'cwd': str(cwd), 'timeout_seconds': timeout,
              'started_utc_epoch': started, 'exit_code': 125, 'timed_out': False,
              'cleanup_verified': False, 'output_streams': 'stdout and stderr merged directly to retained file'}
    log = Path(log)
    receipt = Path(receipt)
    log.parent.mkdir(parents=True, exist_ok=True)
    if log.exists():
        raise FileExistsError(f'Refuse an existing command log: {log}')
    try:
        executable = shutil.which(argv[0])
        if not executable:
            raise FileNotFoundError(f'Command not found: {argv[0]}')
        argv = [str(Path(executable).resolve()), *argv[1:]]
        if os.name == 'nt':
            sys.path.insert(0, str(Path(repo) / 'eng' / 'ci'))
            from lib.pre_release_windows_job import run_owned_windows
            owned = run_owned_windows(argv, cwd, os.environ.copy(), timeout, log)
            owned['cleanup_verified'] = owned.get('cleanup') == 'owned Job Object has zero active processes'
        elif sys.platform == 'linux':
            owned = linux_owned(argv, cwd, os.environ.copy(), timeout, log)
        else:
            raise RuntimeError('Only Windows Job and Linux process-group ownership are supported')
        result.update(owned)
    except BaseException as error:
        result['execution_error'] = f'{type(error).__name__}: {error}'
        # Existing Job helper closes its kill-on-close job on failure; do not claim verified zero on an exception.
        with log.open('ab') as output:
            output.write(('\nOWNER ERROR: ' + result['execution_error'] + '\n').encode('utf-8'))
    finally:
        result['finished_utc_epoch'] = time.time()
        temporary = receipt.with_suffix(receipt.suffix + '.tmp')
        temporary.write_text(json.dumps(result, indent=2), encoding='utf-8')
        os.replace(temporary, receipt)
    return result


def main():
    if sys.platform == 'linux':
        def interrupted(signum, _frame):
            raise KeyboardInterrupt(f'Owner interrupted by signal {signum}')
        signal.signal(signal.SIGTERM, interrupted)
    parser = argparse.ArgumentParser()
    parser.add_argument('--repo', required=True, type=Path)
    parser.add_argument('--cwd', required=True, type=Path)
    parser.add_argument('--timeout', required=True, type=float)
    parser.add_argument('--log', required=True, type=Path)
    parser.add_argument('--receipt', required=True, type=Path)
    parser.add_argument('command', nargs=argparse.REMAINDER)
    args = parser.parse_args()
    argv = args.command[1:] if args.command[:1] == ['--'] else args.command
    if not argv or not 0 < args.timeout <= 1800:
        parser.error('Require one command and a timeout between zero and1800 seconds')
    result = run_command(args.repo, argv, args.cwd, args.timeout, args.log, args.receipt)
    if not result.get('cleanup_verified') or result.get('execution_error'):
        return 125
    code = result['exit_code']
    return code if 0 <= code <= 125 else 1


if __name__ == '__main__':
    sys.exit(main())
