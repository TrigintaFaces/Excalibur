#!/usr/bin/env python3
"""Enumerate conformance-kit arms and each in-repo deriver's wrappers, and report every gap.

WHY THIS CANNOT LIVE INSIDE THE KIT.  The kits ship without a test-framework reference on purpose, so
an arm carries no discovery attribute and the deriver supplies one.  An arm the deriver never wraps is
not skipped and not reported -- it does not exist, and the suite is green over what remains.  The kits
carry an in-process arm that reflects over the deriver and fails on a gap, which helps a consumer, but
that arm is itself unattributed: a deriver that forgets IT disables the check silently.  A mechanism
cannot detect its own absence.  Only something outside the test process can, which is this.

Exit: 0 = every arm wired.  1 = a gap.  2 = REFUSE (nothing measured -- never a pass).
"""
import io
import os
import re
import sys

KIT_DECL = re.compile(r'^public abstract class (\w+)(?:<[^>]*>)?\s*(?::\s*([\w<>, .]+))?', re.M)
# An arm is a virtual zero-arg Task/void member whose name carries the Member_Scenario_Expectation
# convention.  Visibility does NOT discriminate: three kits declare every arm `protected virtual`, and a
# deriver calls them fine.  The underscore does discriminate, and is checked rather than assumed --
# across every kit the only virtual zero-arg members WITHOUT one are the four lifecycle hooks
# (Cleanup, CleanupAsync, ResetDataAsync, DisposeTransportAsync), which are correctly not arms.
PROTECTED_ARM = re.compile(
    r'^\s*protected virtual (?:async )?(?:Task|void)\s+(\w+_\w+)\s*\(\s*\)', re.M)
ARM = re.compile(r'^\s*(?:public|protected) virtual (?:async )?(?:Task|void)\s+(\w+_\w+)\s*\(\s*\)', re.M)
CLASS_DECL = re.compile(r'^(?:public|internal)[\w ]*class\s+(\w+)(?:<[^>]*>)?\s*:\s*([\w<>, .]+)', re.M)
# Every arm-shaped identifier the file mentions OUTSIDE a comment: wrapper declarations, the calls in
# their bodies, overrides, and METHOD GROUPS -- a suite may reach an arm as `Run(Fencing_StaleToken_...)`
# with no parentheses at all, and matching on `Name()` reports that live suite as unwired.  Deliberately
# generous: this gate reds on ABSENCE, so over-matching can only lose a finding, never invent one.
# Comments are stripped first so that naming an arm in prose does not count as wiring it.
MEMBER = re.compile(r'\b(\w+_\w+)\b')
LINE_COMMENT = re.compile(r'//[^\n]*')
BLOCK_COMMENT = re.compile(r'/\*.*?\*/', re.S)


def _code_only(text):
    """The file with comments removed, so prose mentioning an arm is not mistaken for wiring it."""
    return LINE_COMMENT.sub(' ', BLOCK_COMMENT.sub(' ', text))
PARTIAL = re.compile(r'conformance-partial-suite:\s*full coverage in (\w+)')

GUARD = 'ConformanceSuite_ShouldWireEveryArm'


def _sources(root):
    for base, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in ('bin', 'obj')]
        for f in files:
            if f.endswith('.cs'):
                yield os.path.join(base, f)


def load_kits(kit_root):
    """-> {kit: (base, arms, guard_shape)} where guard_shape is None when the kit has no in-process
    guard, else True when that guard can actually see the kit's own arms."""
    kits = {}
    for path in _sources(kit_root):
        text = open(path, encoding='utf-8-sig', errors='replace').read()
        m = KIT_DECL.search(text)
        if not m or not m.group(1).endswith('TestKit'):
            continue
        base = (m.group(2) or '').split(',')[0].strip().split('<')[0].split('.')[-1]
        arms = ARM.findall(text)
        guard = None
        if GUARD in text:
            # A guard bound to public members alone enumerates NOTHING on a kit that declares its arms
            # protected, so it passes over an empty set for every deriver forever -- green, and unable
            # to be otherwise. Three kits declare protected arms and all three already bind NonPublic;
            # this keeps that true rather than trusting it to stay true.
            body = text[text.index('BindingFlags Declared'):]
            body = body[:body.index('.ToList()')] if '.ToList()' in body else body
            protected_arms = PROTECTED_ARM.findall(text)
            guard = bool(protected_arms) <= ('NonPublic' in body)
        kits[m.group(1)] = (base, arms, guard)
    return kits


def arms_of(kits, kit, seen=None):
    seen = seen or set()
    if kit not in kits or kit in seen:
        return []
    seen.add(kit)
    base, arms, _guard = kits[kit]
    return list(arms) + arms_of(kits, base, seen)


def load_derivers(kits, test_root):
    derivers = {}
    for path in _sources(test_root):
        text = open(path, encoding='utf-8-sig', errors='replace').read()
        for m in CLASS_DECL.finditer(text):
            base = m.group(2).split(',')[0].strip().split('<')[0].split('.')[-1]
            if base in kits:
                partial = PARTIAL.search(text)
                derivers[m.group(1)] = (base, path, set(MEMBER.findall(_code_only(text))),
                                        partial.group(1) if partial else None)
    return derivers


def evaluate(kits, derivers):
    """-> (exit_code, report_lines).  Full coverage is required unless a deriver declares itself a
    partial suite AND names a sibling over the same kit that IS fully wired."""
    gaps, notes = [], []
    full = set()
    for cls, (kit, _p, members, _s) in derivers.items():
        if not [a for a in arms_of(kits, kit) if a not in members]:
            full.add(cls)

    for cls, (kit, path, members, sibling) in sorted(derivers.items()):
        arms = arms_of(kits, kit)
        if not arms:
            continue
        missing = [a for a in arms if a not in members]
        if not missing:
            continue
        if sibling:
            if sibling not in derivers:
                gaps.append(f"{cls} ({path}) declares partial coverage deferring to {sibling}, "
                            f"which is not a conformance suite in this repo.")
            elif derivers[sibling][0] != kit:
                gaps.append(f"{cls} ({path}) defers to {sibling}, which derives "
                            f"{derivers[sibling][0]}, not {kit}.")
            elif sibling not in full:
                gaps.append(f"{cls} ({path}) defers to {sibling}, which is not itself fully wired, "
                            f"so no suite runs every {kit} arm.")
            else:
                notes.append(f"partial: {cls} ({len(arms) - len(missing)}/{len(arms)} {kit} arms) "
                             f"-> full coverage in {sibling}")
            continue
        head = f"{cls} wires {len(arms) - len(missing)} of {len(arms)} {kit} arms"
        if GUARD in missing:
            head += " -- INCLUDING the in-process wiring guard, so nothing checks it from inside either"
        gaps.append(head + f"\n    {path}\n    unwired: " + ", ".join(sorted(missing)))

    vacuous = sorted(k for k, (_b, a, g) in kits.items() if a and g is False)
    unguarded = sorted(k for k, (_b, a, g) in kits.items() if a and g is None)
    for kit in vacuous:
        gaps.append(f"{kit} carries the in-process wiring guard, but binds public members only while "
                    f"declaring its arms protected. It enumerates zero arms, so it passes over an empty "
                    f"set for every deriver and cannot fail. Add BindingFlags.NonPublic.")

    lines = [f"kits: {sum(1 for k in kits if arms_of(kits, k))}   derivers: {len(derivers)}   "
             f"arms checked: {sum(len(arms_of(kits, d[0])) for d in derivers.values())}"]
    if unguarded:
        lines.append(f"  note: {len(unguarded)} kit(s) ship no in-process wiring guard, so a CONSUMER "
                     f"deriving them gets no check from inside the test process; this gate covers the "
                     f"in-repo derivers only: " + ", ".join(unguarded))
    lines += ["  " + n for n in notes]
    if not derivers or not any(arms_of(kits, k) for k in kits):
        return 2, lines + ["REFUSE: found no kit arms or no derivers -- nothing was measured."]
    if gaps:
        lines.append("")
        lines.append(f"{len(gaps)} suite(s) leave a conformance arm unwired. An unwired arm never runs, "
                     "so it cannot fail, and it reads in the results exactly like one that passed.")
        lines.append("Wire one attributed member per arm. A known gap stays wired and visibly skipped "
                     "in your runner. A suite that covers a subset ON PURPOSE says so with:")
        lines.append("    // conformance-partial-suite: full coverage in <SiblingSuiteName>")
        lines.append("and the named sibling must itself wire every arm.")
        lines.append("")
        lines += [f"  {g}" for g in gaps]
        return 1, lines
    return 0, lines + ["every conformance arm is wired to a runner."]




# ---------------------------------------------------------------------------------------------------
# self-test: this gate's own non-vacuity.  Every arm below must be able to FAIL, or the gate is theatre
# -- which is the exact defect it exists to catch, turned on itself.
# ---------------------------------------------------------------------------------------------------
KIT_TEMPLATE = """
public abstract class {name}
{{
	protected virtual Task CleanupAsync() => Task.CompletedTask;
	protected virtual Task ResetDataAsync() => Task.CompletedTask;
{arms}{guard}
}}
"""
GUARD_SRC = """
	public virtual Task ConformanceSuite_ShouldWireEveryArm()
	{{
		const System.Reflection.BindingFlags Declared =
			System.Reflection.BindingFlags.{flags}System.Reflection.BindingFlags.Instance;
		var arms = typeof(X).GetMethods(Declared).ToList();
		return Task.CompletedTask;
	}}
"""


def _kit(name, arms, visibility='public', guard=None):
    body = "".join(f"\t{visibility} virtual Task {a}() => Task.CompletedTask;\n" for a in arms)
    g = "" if guard is None else GUARD_SRC.format(flags=guard)
    return KIT_TEMPLATE.format(name=name, arms=body, guard=g)


def _suite(name, kit, wraps, extra=''):
    body = "".join(f"\t[Fact] public Task {w}_Test() => {w}();\n" for w in wraps)
    return f"{extra}\npublic sealed class {name} : {kit}\n{{\n{body}}}\n"


def _run(kit_src, test_src):
    import tempfile
    with tempfile.TemporaryDirectory() as d:
        k, t = os.path.join(d, 'k'), os.path.join(d, 't')
        os.makedirs(k)
        os.makedirs(t)
        io.open(os.path.join(k, 'Kit.cs'), 'w', encoding='utf-8').write(kit_src)
        io.open(os.path.join(t, 'Suite.cs'), 'w', encoding='utf-8').write(test_src)
        kits = load_kits(k)
        return evaluate(kits, load_derivers(kits, t))[0]


def self_test():
    ok = True

    def check(label, got, want):
        nonlocal ok
        if got != want:
            print(f"self-test FAIL: {label} -> exit {got}, expected {want}", file=sys.stderr)
            ok = False

    kit = _kit('FooConformanceTestKit', ['A_Does_Thing', 'B_Does_Thing'], guard='Public | ')
    arms = ['A_Does_Thing', 'B_Does_Thing', 'ConformanceSuite_ShouldWireEveryArm']

    check('a fully wired suite passes',
          _run(kit, _suite('FullSuite', 'FooConformanceTestKit', arms)), 0)
    check('a suite missing an arm fails',
          _run(kit, _suite('GappySuite', 'FooConformanceTestKit', arms[1:])), 1)
    check('a suite missing ONLY the wiring guard fails (the guard cannot report its own absence)',
          _run(kit, _suite('NoGuardSuite', 'FooConformanceTestKit', arms[:2])), 1)
    check('an arm reached as a METHOD GROUP counts as wired',
          _run(kit, _suite('GroupSuite', 'FooConformanceTestKit', arms[1:],
                           extra='// x\npublic sealed class Helper { void Q() '
                                 '{ Run(A_Does_Thing); } }')), 0)
    check('an arm named only in a COMMENT does not count as wired',
          _run(kit, _suite('ProseSuite', 'FooConformanceTestKit', arms[1:],
                           extra='// we should really wire A_Does_Thing one day')), 1)

    partial = ('// conformance-partial-suite: full coverage in FullSuite\n'
               + _suite('PartSuite', 'FooConformanceTestKit', arms[1:])
               + _suite('FullSuite', 'FooConformanceTestKit', arms))
    check('a declared partial suite passes when its named sibling is fully wired',
          _run(kit, partial), 0)
    check('a declared partial suite fails when its named sibling does not exist',
          _run(kit, '// conformance-partial-suite: full coverage in Nowhere\n'
                    + _suite('PartSuite', 'FooConformanceTestKit', arms[1:])), 1)
    check('a declared partial suite fails when its named sibling is itself partial',
          _run(kit, '// conformance-partial-suite: full coverage in AlsoPartial\n'
                    + _suite('PartSuite', 'FooConformanceTestKit', arms[1:])
                    + _suite('AlsoPartial', 'FooConformanceTestKit', arms[1:])), 1)

    vacuous = _kit('BarConformanceTestKit', ['A_Does_Thing'], visibility='protected', guard='Public | ')
    check('a guard binding public members only, over protected arms, is reported vacuous',
          _run(vacuous, _suite('VacSuite', 'BarConformanceTestKit',
                               ['A_Does_Thing', 'ConformanceSuite_ShouldWireEveryArm'])), 1)
    sound = _kit('BarConformanceTestKit', ['A_Does_Thing'], visibility='protected',
                 guard='Public | System.Reflection.BindingFlags.NonPublic | ')
    check('the same kit passes once its guard binds NonPublic',
          _run(sound, _suite('SoundSuite', 'BarConformanceTestKit',
                             ['A_Does_Thing', 'ConformanceSuite_ShouldWireEveryArm'])), 0)

    check('no kits and no derivers REFUSEs rather than passing', _run('', ''), 2)

    if ok:
        print('conformance-arm-wiring self-test PASSED '
              '(wired / gap / missing-guard / method-group / prose-only / partial x3 / '
              'vacuous-guard x2 / refuse arms all non-vacuous).')
        return 0
    print('conformance-arm-wiring self-test FAILED.', file=sys.stderr)
    return 3


def main(argv):
    if '--self-test' in argv:
        return self_test()
    kit_root = argv[1] if len(argv) > 1 else 'src/Excalibur/Excalibur.Testing.Conformance'
    test_root = argv[2] if len(argv) > 2 else 'tests'
    if not os.path.isdir(kit_root) or not os.path.isdir(test_root):
        print(f"REFUSE: {kit_root} or {test_root} is not a directory -- nothing scanned.", file=sys.stderr)
        return 2
    kits = load_kits(kit_root)
    code, lines = evaluate(kits, load_derivers(kits, test_root))
    print("\n".join(lines))
    return code


if __name__ == '__main__':
    sys.exit(main(sys.argv))
