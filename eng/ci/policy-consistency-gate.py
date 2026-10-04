#!/usr/bin/env python3
"""Every declared quality threshold must equal the one in eng/ci/quality-policy.json.

This exists because four places declared a coverage floor and they disagreed: ci.yml said 65 in a
variable nothing read and passed 65 to the reusable workflow, whose own default was 44;
quality-gates.yml enforced 44; the README documented 60. So the push path and the CI path answered
the same question differently, and the highest of them was a number the repository had never met.
A threshold nobody can reach does not raise coverage -- it teaches everyone to route around the
gate, which is worse than having no gate.

Policy lives in one file. This gate asserts nothing has drifted from it.

Exit 0 clean - 1 drift - 2 REFUSE (could not evaluate). REFUSE is never a pass.
"""
import glob, io, json, os, re, sys, tempfile

PATTERNS = [
    re.compile(r'COVERAGE_THRESHOLD:\s*[\'"]?(\d+)'),
    re.compile(r'coverage-threshold:\s*[\'"](\d+)[\'"]'),
    re.compile(r'-Threshold\s+(\d+)\b'),
]

def scan(files, floor):
    drift = []
    checked = 0
    for f in files:
        with io.open(f, encoding='utf-8-sig') as source:
            txt = source.read()
        for i, line in enumerate(txt.split('\n'), 1):
            if line.lstrip().startswith('#'):
                continue                      # a comment recording history is not a declaration
            for pat in PATTERNS:
                m = pat.search(line)
                if not m:
                    continue
                checked += 1
                if int(m.group(1)) != floor:
                    drift.append((f, i, int(m.group(1)), line.strip()[:70]))
    return checked, drift

def self_test():
    d = tempfile.mkdtemp()
    ok = os.path.join(d, 'ok.yml'); bad = os.path.join(d, 'bad.yml')
    io.open(ok, 'w', newline='\n').write("env:\n  COVERAGE_THRESHOLD: 44\n")
    io.open(bad, 'w', newline='\n').write("with:\n  coverage-threshold: '65'\n")
    c1, d1 = scan([bad], 44)
    c2, d2 = scan([ok], 44)
    safety = len(d1) == 1 and d1[0][2] == 65
    liveness = len(d2) == 0 and c2 == 1
    invalid = os.path.join(d, 'invalid.yml')
    with open(invalid, 'wb') as source:
        source.write(b'coverage-threshold: 65\xff')
    refused = []
    for path in (os.path.join(d, 'missing.yml'), d, invalid):
        try:
            scan([ok, path], 44)
            refused.append(False)
        except (OSError, UnicodeError):
            refused.append(True)
    readable = all(refused)
    print(f"  safety:   a declaration that disagrees with policy IS caught -- {'PASS' if safety else 'FAIL'}")
    print(f"  liveness: a declaration that agrees is NOT flagged, and IS counted -- {'PASS' if liveness else 'FAIL'}")
    print(f"  refusal: missing, unreadable and malformed input cannot vanish -- {'PASS' if readable else 'FAIL'}")
    print("SELF-TEST " + ("PASS (safety + liveness + refusal)" if safety and liveness and readable else "FAIL"))
    sys.exit(0 if (safety and liveness and readable) else 1)

if __name__ == '__main__':
    if '--self-test' in sys.argv:
        self_test()
    pol_path = 'eng/ci/quality-policy.json'
    if not os.path.exists(pol_path):
        print(f"REFUSE: {pol_path} missing; there is no policy to check against. NOT a pass.", file=sys.stderr)
        sys.exit(2)
    try:
        with io.open(pol_path, encoding='utf-8') as source:
            floor = json.load(source)['coverage']['regressionFloor']
        if type(floor) not in (int, float) or not 0 <= floor <= 100 or floor != int(floor):
            raise ValueError('coverage regressionFloor must be an integer-valued percentage')
        floor = int(floor)
    except (OSError, UnicodeError, ValueError, KeyError, TypeError) as exc:
        print(f'REFUSE: invalid quality policy: {exc}', file=sys.stderr)
        sys.exit(2)
    files = sorted(glob.glob('.github/workflows/*.yml')) + sorted(glob.glob('.github/workflows/*.md'))
    if not files:
        print("REFUSE: no workflow files found. NOT a pass.", file=sys.stderr)
        sys.exit(2)
    try:
        checked, drift = scan(files, floor)
    except (OSError, UnicodeError) as exc:
        print(f'REFUSE: cannot read every policy input: {exc}', file=sys.stderr)
        sys.exit(2)
    if checked == 0:
        print("REFUSE: no threshold declaration found anywhere. Either the patterns rotted or the "
              "policy is unenforced; both are failures, neither is a pass.", file=sys.stderr)
        sys.exit(2)
    if drift:
        print(f"policy-consistency: {len(drift)} declaration(s) disagree with the floor of {floor}:")
        for f, i, v, line in drift:
            print(f"  ::error::{f}:{i} declares {v}, policy says {floor} -- {line}")
        sys.exit(1)
    print(f"policy-consistency: {checked} declaration(s) all agree with the floor of {floor}.")
