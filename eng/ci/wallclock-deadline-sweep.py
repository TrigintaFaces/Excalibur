"""Sweep tests/ for the wall-clock shapes that produced three red mains in one evening.

Shapes, each taken from a confirmed instance rather than invented:

  A  SHORT DEADLINE            a CancellationTokenSource whose timeout is small enough that a loaded
                               runner can blow it.        (EventStoreLiveSubscription: 5s)
  B  NESTED DEADLINE           a CTS deadline in a method that then waits LONGER for something that
                               deadline can prevent. The inner one wins and the outer wait is a lie.
                               (EventStoreLiveSubscription: 5s token, 30s scaled wait)
  C  REDUNDANT HANG-GUARD      a long CTS deadline used only to stop a hang, duplicating the harness's
                               --blame-hang-timeout, and converting slow-but-correct into failed.
                               (DispatchChannel backpressure: 60s)

`// deadline-ok:` on the line exempts it, same escape hatch the CI gate uses.
"""
import pathlib
import sys
import re



CTS = re.compile(r'new CancellationTokenSource\(\s*TimeSpan\.From(Seconds|Milliseconds)\(\s*([0-9.]+)\s*\)')
# Waits that a too-short inner deadline can invalidate.
WAIT = re.compile(r'AwaitSignalAsync|WaitUntilAsync|WhenAny|\.WaitAsync\(|Scale\(\s*TimeSpan\.From(Seconds|Minutes)\(\s*([0-9.]+)')
WAIT_SECS = re.compile(r'(?:Scale\(\s*)?TimeSpan\.FromSeconds\(\s*([0-9.]+)\s*\)')
METHOD = re.compile(r'\b(?:public|private|protected|internal)[^\r\n;=]*?\b(\w+)\s*\([^)]*\)\s*$')


def to_seconds(unit, value):
    v = float(value)
    return v / 1000.0 if unit == 'Milliseconds' else v


def enclosing_method(lines, idx):
    """The declaration this line sits under, and where that method's text ENDS.

    The end bound matters and the first version of this got it wrong: a fixed 120-line window spills
    into the following test, so a wait belonging to the NEXT method was paired with this method's
    token and reported as a nested deadline. Every Compliance hit was that false pairing. Bounding at
    the next declaration can only stop early, never late -- it can miss a pairing, it cannot invent
    one, which is the right direction for a list a human is going to act on.
    """
    start = None
    for j in range(idx, max(0, idx - 120), -1):
        if METHOD.search(lines[j].rstrip()):
            start = j
            break
    if start is None:
        return max(0, idx - 60), min(len(lines), idx + 60), '<unknown>'
    name = METHOD.search(lines[start].rstrip()).group(1)
    end = len(lines)
    for j in range(start + 1, len(lines)):
        if METHOD.search(lines[j].rstrip()):
            end = j
            break
    return start, end, name


def scan(root):
    """All deadline findings under `root`, grouped by shape."""
    findings = {'A': [], 'B': [], 'C': [], 'SUBJECT': []}

    for path in pathlib.Path(root).rglob('*.cs'):
        if any(p in path.parts for p in ('bin', 'obj')):
            continue
        text = path.read_text(encoding='utf-8', errors='replace')
        if 'CancellationTokenSource(' not in text:
            continue
        lines = text.split(chr(10))

        for i, line in enumerate(lines):
            if 'deadline-ok' in line:
                continue
            stripped = line.strip()
            if stripped.startswith('//') or stripped.startswith('*'):
                continue
            m = CTS.search(line)
            if not m:
                continue
            secs = to_seconds(m.group(1), m.group(2))
            start, end, name = enclosing_method(lines, i)
            body = chr(10).join(lines[start:end])

            # B: is there a LONGER wait in the same method that this deadline could pre-empt?
            longer = []
            if WAIT.search(body):
                for w in WAIT_SECS.finditer(body):
                    wv = float(w.group(1))
                    if wv > secs:
                        longer.append(wv)
            rec = (str(path).replace(chr(92), '/'), i + 1, secs, name, max(longer) if longer else None)

            # THE DISCRIMINATOR. A deadline that IS the subject under test is correct usage: a test named
            # for cancellation or timeout is supposed to construct a short token and watch it fire. A
            # deadline in a test about something else is a GUARD, and a guard is what turns a slow runner
            # into a red build. Validated against the two confirmed instances: UnsubscribeAsyncStopsPolling
            # and ExertBackpressure_WhenBoundedChannelIsFull both survive this filter; a control test named
            # RespectTimeoutWhenNoSignalReceived is correctly dropped by it.
            subject = re.search(r'Cancel|Timeout|Expire|Deadline|Elapsed|Abort', name, re.IGNORECASE)
            if subject:
                findings['SUBJECT'].append(rec)
            elif longer:
                findings['B'].append(rec)
            elif secs <= 9:
                findings['A'].append(rec)
            elif secs >= 30:
                findings['C'].append(rec)
    return findings


NESTED_HELP = """
The NESTED category is the only one this gate BLOCKS on, and the reason is that it is the only one
where the code is wrong on its face rather than merely suspicious.

An inner deadline shorter than a wait in the same method cannot be correct: whatever the wait is
for, the deadline can prevent it from ever arriving, and the test then fails reporting a timeout
that describes a stopwatch rather than a defect. Two of those shipped red mains on 2026-08-07, in
an event-store subscription and an event-driven wait pattern, and both were the same five-second
token nested inside a thirty-second wait.

SHORT and LONG are reported and NOT enforced, deliberately. They are candidates: 23 short deadlines
sit in tests whose names do not suggest the deadline is the subject, and several are fine on
inspection. Blocking on an unreviewed population would make this gate something people route
around, and a gate people route around enforces nothing -- which is the failure it exists to
prevent.
"""


def report(findings, blocking_only=False):
    titles = {
        'A': 'SHORT DEADLINE (<=9s, no longer wait in the method) -- advisory',
        'B': 'NESTED DEADLINE (inner deadline shorter than a wait in the same method) -- BLOCKING',
        'C': 'LONG DEADLINE (>=30s), candidate redundant hang-guard -- advisory',
    }
    keys = ('B',) if blocking_only else ('B', 'A', 'C')
    for key in keys:
        rows = sorted(findings[key])
        print(f"{chr(10)}=== {titles[key]}: {len(rows)}")
        for f, ln, secs, name, outer in rows:
            extra = f'   inner {secs:g}s vs wait {outer:g}s' if outer else f'   {secs:g}s'
            print(f"  {f}:{ln}{extra}")
            print(f"      in {name}")


def self_test():
    """Prove the gate discriminates. A gate that cannot fail is not evidence."""
    import tempfile, os
    ok = True

    planted = """
public class T
{
    public async Task DoesSomethingUnrelated()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Consume(cts.Token);
        await WaitHelpers.AwaitSignalAsync(ready.Task, TestTimeouts.Scale(TimeSpan.FromSeconds(30)));
    }
}
"""
    # The same shape in a test NAMED for the deadline: correct usage, must NOT be flagged.
    subject = planted.replace('DoesSomethingUnrelated', 'RespectTimeoutWhenNoSignalReceived')
    clean = planted.replace('new CancellationTokenSource(TimeSpan.FromSeconds(5))',
                            'new CancellationTokenSource()')

    for label, body, expect in (
            ('a nested deadline is DETECTED', planted, 1),
            ('the same shape in a timeout-named test is NOT flagged', subject, 0),
            ('no deadline at all is clean', clean, 0)):
        with tempfile.TemporaryDirectory() as d:
            with open(os.path.join(d, 'X.cs'), 'w', encoding='utf-8') as fh:
                fh.write(body)
            got = len(scan(d)['B'])
        if got == expect:
            print(f'SELF-TEST: PASS -- {label}')
        else:
            print(f'SELF-TEST: FAIL -- {label} (expected {expect} nested finding(s), got {got})')
            ok = False

    if ok:
        print('SELF-TEST: all arms passed -- the nested-deadline gate is non-vacuous.')
        return 0
    print('SELF-TEST: at least one arm failed.')
    return 1


if __name__ == '__main__':
    argv = sys.argv[1:]
    if '--self-test' in argv:
        sys.exit(self_test())

    root = 'tests'
    if not pathlib.Path(root).is_dir():
        print(f'[wallclock-deadline-sweep] CANNOT EVALUATE -- no {root}/ directory here. '
              'Run from the repository root. An unmeasured tree is not a clean one.', file=sys.stderr)
        sys.exit(2)

    findings = scan(root)
    gate = '--gate' in argv
    print(f"  (dropped as SUBJECT-under-test, correct usage: {len(findings['SUBJECT'])})")
    report(findings, blocking_only=gate)

    if gate:
        nested = findings['B']
        if nested:
            print(NESTED_HELP, file=sys.stderr)
            print(f'::error::wallclock-deadline-sweep: {len(nested)} nested deadline(s). '
                  'An inner deadline shorter than a wait in the same method can stop that wait from '
                  'ever completing, and the failure reports a stopwatch rather than a defect.',
                  file=sys.stderr)
            sys.exit(1)
        print(f'{chr(10)}wallclock-deadline-sweep: PASS -- no nested deadlines. '
              f"({len(findings['A'])} short and {len(findings['C'])} long deadline(s) reported, not enforced.)")
    sys.exit(0)
