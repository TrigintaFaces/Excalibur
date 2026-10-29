"""Check a proposed cooperative projection design, with known-red controls.

Run with --java PATH --jar PATH. Exit 0: all requested answers observed;
1: unexpected answer; 2: missing toolchain, timeout or exhausted heap (REFUSE).
Retains configs, complete logs and TLC state artifacts in --output.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
import uuid

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--java', default=os.environ.get('TLA_JAVA', shutil.which('java')))
parser.add_argument('--jar', default=os.environ.get('TLA_JAR'))
parser.add_argument('--output', type=Path)
parser.add_argument('--timeout', type=int, default=1800)
parser.add_argument('--heap-mb', type=int, default=4096)
parser.add_argument('--only', nargs='+')
args = parser.parse_args()
if args.timeout <= 0 or args.heap_mb <= 0:
    parser.error('--timeout and --heap-mb must be positive')
if not args.java or not args.jar or not Path(args.java).is_file() or not Path(args.jar).is_file():
    print('REFUSE: provide existing --java and --jar paths; nothing checked.')
    sys.exit(2)
java = str(Path(args.java).resolve())
jar = str(Path(args.jar).resolve())
here = Path(__file__).resolve().parent
out = (args.output or (here / ('evidence-' + uuid.uuid4().hex[:8]))).resolve()
out.mkdir(parents=True, exist_ok=False)
for name in ['CooperativeProjection.tla', 'ProjectionModelSelfTest.tla']:
    shutil.copyfile(here / name, out / name)

# The fourth element is the set of invariant names ANY of which is an acceptable
# violation for that mutant. It is a SET, not a single name, because one broken
# mechanism can violate more than one invariant and TLC reports whichever
# reachable violation it encounters first -- which is not deterministic across
# worker counts. Asserting one name would make a correct arm flake. Dropping the
# assertion entirely would accept ANY violation, including one caused by a
# modelling error rather than by the planted mutant, so the set is the honest
# middle: the mutant must break something, and it must be something we predicted.
arms = [
    ('healthy', False, None, None),
    ('mixed-manifest-rejected', True, None, None),
    ('skip-manifest-check', True, 'SkipManifestCheck', {'ManifestAgreement'}),
    ('blind-stale-row-write', False, 'BlindStaleWrite', {'RowNeverRewinds'}),
    # PrematureCheckpoint checkpoints before every required row has settled, so the
    # unsettled row is both un-written and un-visible: either invariant may catch it.
    ('premature-checkpoint', False, 'PrematureCheckpoint',
     {'CheckpointCoverage', 'CheckpointVisibility'}),
    ('cancel-waiter-overrelease', False, 'CancelWaiterReleases', {'GateOwnership'}),
    # The barrier arms. These are the ones that make CheckpointVisibility non-vacuous:
    # without a planted break it would be satisfied by a model that simply never
    # acknowledges anything, which is the inaction-passes-safety trap.
    ('skip-barrier', False, 'SkipBarrier', {'CheckpointVisibility'}),
    # The subtle one: the page wrote nothing because every row was already at or past
    # its target, so it skips the barrier -- while the COMPETING writer's accepted
    # writes are not yet readable. Acknowledging here is a silent loss.
    ('skip-barrier-when-not-writing', False, 'SkipBarrierWhenNotWriting',
     {'CheckpointVisibility'}),
    # The contract says a failed or cancelled barrier must prevent acknowledgment.
    ('barrier-fault-acknowledges', False, 'BarrierFaultAcknowledges',
     {'CheckpointVisibility'}),
    ('checker-self-test', False, None, {'NeverThree'}),
]
if args.only:
    unknown = set(args.only) - {a[0] for a in arms}
    if unknown:
        parser.error('unknown arms: ' + ', '.join(sorted(unknown)))
    arms = [a for a in arms if a[0] in args.only]

provenance = {
    'java': java,
    'javaVersion': subprocess.run([java, '-version'], capture_output=True, text=True).stderr,
    'jar': jar,
    'jarSha256': hashlib.sha256(Path(jar).read_bytes()).hexdigest().upper(),
    'modelSha256': hashlib.sha256((here / 'CooperativeProjection.tla').read_bytes()).hexdigest().upper(),
    'checkerSha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest().upper(),
    'heapMb': args.heap_mb,
    'timeoutSeconds': args.timeout,
    'workers': 2,
    'scope': '3 actors, 2 processes, 2 rows, 2 events; one crash/cancel per actor; FIFO local gates',
    'arms': [],
}
status = 0


def observed_violations(log, expected):
    invariants = re.findall(r'Invariant ([^\r\n]+?) is violated', log)
    temporal = 'Temporal properties were violated' in log
    unexpected = temporal or any(name not in (expected or set()) for name in invariants)
    return invariants, unexpected


for name, mixed, mutation, expected in arms:
    module = 'ProjectionModelSelfTest' if name == 'checker-self-test' else 'CooperativeProjection'
    if name == 'checker-self-test':
        cfg = 'SPECIFICATION Spec\nINVARIANT NeverThree\n'
    else:
        constants = {
            'MixedManifest': mixed,
            'SkipManifestCheck': False,
            'BlindStaleWrite': False,
            'PrematureCheckpoint': False,
            'CancelWaiterReleases': False,
            'SkipBarrier': False,
            'SkipBarrierWhenNotWriting': False,
            'BarrierFaultAcknowledges': False,
        }
        if mutation:
            constants[mutation] = True
        cfg = 'SPECIFICATION Spec\nCONSTANTS\n'
        cfg += ''.join(f'    {key} = {str(value).upper()}\n' for key, value in constants.items())
        cfg += ('INVARIANTS TypeOK GateOwnership ManifestAgreement RowNeverRewinds'
                ' RowIsPrefix CheckpointCoverage CheckpointNeverRewinds'
                ' VisibleNeverExceedsWritten CheckpointVisibility\n')
        if expected is None:
            cfg += 'PROPERTY Completion\n'
    (out / (name + '.cfg')).write_text(cfg, encoding='utf-8')
    cmd = [java, '-XX:+UseParallelGC', f'-Xmx{args.heap_mb}m', '-cp', jar, 'tlc2.TLC',
           '-workers', '2', '-deadlock', '-metadir', str(out / (name + '-states')),
           '-config', name + '.cfg', module + '.tla']
    started = time.monotonic()
    try:
        result = subprocess.run(cmd, cwd=out, capture_output=True, text=True, timeout=args.timeout)
        log = result.stdout + result.stderr
    except subprocess.TimeoutExpired as exc:
        def text(value):
            return value.decode(errors='replace') if isinstance(value, bytes) else (value or '')
        log = text(exc.stdout) + text(exc.stderr)
        (out / (name + '.log')).write_text(log, encoding='utf-8')
        _, unexpected = observed_violations(log, expected)
        verdict = 'FAIL' if unexpected else 'REFUSE'
        print(f'{verdict} {name}: timeout after {args.timeout}s; this is not a pass.', flush=True)
        provenance['arms'].append({'name': name, 'timedOut': True,
                                   'correctAnswer': False if unexpected else None,
                                   'seconds': round(time.monotonic() - started, 2)})
        if unexpected:
            status = 1
        elif status != 1:
            status = 2
        continue
    (out / (name + '.log')).write_text(log, encoding='utf-8')
    # Resource exhaustion is incomplete exploration, not a counterexample.
    # Never mask an invariant/temporal violation already reported in this arm.
    violations, unexpected = observed_violations(log, expected)
    if 'OutOfMemoryError' in log and not unexpected:
        provenance['arms'].append({'name': name, 'resourceExhausted': True,
                                   'exitCode': result.returncode, 'correctAnswer': None,
                                   'seconds': round(time.monotonic() - started, 2)})
        print(f'REFUSE {name}: Java heap exhausted; this is not a pass.', flush=True)
        if status != 1:
            status = 2
        continue
    if expected:
        wanted = {expected} if isinstance(expected, str) else set(expected)
        correct = result.returncode == 12 and bool(violations) and not unexpected
    else:
        wanted = None
        correct = result.returncode == 0 and not unexpected and 'Model checking completed. No error has been found.' in log
    counts = re.findall(r'([\d,]+) states generated, ([\d,]+) distinct states found', log)
    entry = {'name': name,
             'expectedViolation': sorted(wanted) if wanted else None,
             'exitCode': result.returncode,
             'correctAnswer': correct, 'seconds': round(time.monotonic() - started, 2),
             'stateCounts': counts[-1] if counts else None}
    provenance['arms'].append(entry)
    print(('PASS' if correct else 'FAIL') + ' ' + json.dumps(entry), flush=True)
    if not correct:
        status = 1

(out / 'summary.json').write_text(json.dumps(provenance, indent=2), encoding='utf-8')
print(f'Evidence: {out}', flush=True)
print('SCOPE: finite FIFO-gated protocol only; no proof of production code or external side-effect uniqueness.', flush=True)
sys.exit(status)
