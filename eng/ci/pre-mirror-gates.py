#!/usr/bin/env python3
"""Run, locally, the gate commands the CI workflows invoke — before the copy to the mirror.

WHY THIS EXISTS
---------------
CI jobs are named after their headline step and run more steps than that name implies. A job
called "Code Analysis (Warning-as-Error Build)" built clean — 0 errors, 0 warnings — and failed
anyway, on a later step in the same job. Reproducing the build therefore reproduced nothing, and
the build was reproduced repeatedly, on two operating systems, while the actual failure sat in a
shell script that runs identically on any machine and had simply never been run.

The gap was COVERAGE, not platform. A curated local battery ran 16 gates. The workflows invoke
well over a hundred commands. Nothing locally ran that set, so anything outside the curated
battery was invisible until the mirror ran it.

WHAT IT DOES
------------
Parses every workflow, extracts the commands that invoke this repository's own tooling, and runs
the ones that are runnable here. It is a REPORT, not an enforcement point.

THE LIST IS DERIVED, NEVER HAND-MAINTAINED. A hand-written list of what CI runs is a second copy
of the workflows, and a second copy drifts silently toward the comfortable answer. This repository
has already paid for that: a hand-maintained shard table went two shards short, and a phase
reported "all shards green" over a shard that had never run.

WHAT IT DOES NOT CLAIM
----------------------
Many CI steps need context that does not exist on a developer machine: uploaded artifacts, tokens,
matrix variables, a specific runner OS. Those are NOT run, and they are REPORTED as not run rather
than skipped quietly — an unexamined command must never read like an examined one. A green here
means "every gate I could run locally passed", and the output says how many that was out of how
many exist.

Some failures are genuinely platform-specific and this tool cannot see them (a sample that aborts
on Linux and succeeds on Windows). That residue is real and is named here so a clean run is not
mistaken for a guarantee about the mirror.

NOT A COMMIT GATE
-----------------
Nothing wires this to a hook. It is slow by construction — it runs real gates — and the standing
instruction is that no gate may slow down a commit. Run it by hand before copying to the mirror.

EXIT CODES
  0  every gate that ran, passed
  1  at least one gate FAILED
  2  REFUSE: nothing was derived or nothing was run — an instrument with no subject is not a pass
  3  REFUSE: --self-test failed (this tool is broken or vacuous)
"""

import os
import re
import subprocess
import sys

import shutil as _shutil

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
HAVE_PWSH = _shutil.which("pwsh") is not None
WORKFLOW_DIR = os.path.join(".github", "workflows")

# A command is ours if it invokes something under eng/. Anything else in a workflow is a runner
# action, a package manager, or the SDK, none of which this tool is trying to second-guess.
OURS = re.compile(r"(?:^|[\s|&;(])(?:bash|sh|python3?|pwsh\s+[^\n]*?-File)\s+\.?/?(eng/[^\s|&;)'\"]+)")
OURS_DIRECT = re.compile(r"(?:^|[\s|&;(])\.?/(eng/[^\s|&;)'\"]+\.(?:sh|py|ps1))")

# Markers that a command cannot run outside the CI runner. Each one is a REASON, printed with the
# command, so a reader can tell "not applicable here" from "nobody looked".
CONTEXT_MARKERS = [
    ("${{", "interpolates a workflow expression (matrix, secret, or needs output)"),
    ("$GITHUB_", "writes to a GitHub Actions output/summary file"),
    ("artifacts/", "reads an artifact produced by an earlier CI job"),
    ("$RUNNER_", "depends on runner-provided paths"),
    ("secrets.", "requires a repository secret"),
]


def workflows(root):
    d = os.path.join(root, WORKFLOW_DIR)
    if not os.path.isdir(d):
        return []
    return sorted(
        os.path.join(d, f) for f in os.listdir(d) if f.endswith((".yml", ".yaml"))
    )


def run_blocks(path):
    """Every `run:` block in a workflow, as raw shell text.

    Parsed as YAML rather than grepped: a run block is a multi-line scalar, and a line-oriented
    scan of one reports fragments of commands as whole commands.
    """
    try:
        import yaml
    except ImportError:
        return None
    try:
        with open(path, encoding="utf-8") as fh:
            doc = yaml.safe_load(fh)
    except Exception:
        return []
    if not isinstance(doc, dict):
        return []
    out = []
    for job in (doc.get("jobs") or {}).values():
        if not isinstance(job, dict):
            continue
        job_shell = ((job.get("defaults") or {}).get("run") or {}).get("shell")
        for step in job.get("steps") or []:
            if isinstance(step, dict) and isinstance(step.get("run"), str):
                # The step's declared shell is part of the command. A PowerShell step is not a
                # bash command that happens to mention a .ps1 -- it has PowerShell SYNTAX
                # (`$x = ...`, pipelines into cmdlets), and running it under bash fails for a
                # reason that has nothing to do with the tree. A runner that reports those as
                # gate failures is a runner people learn to ignore.
                out.append((step["run"], step.get("shell") or job_shell or "bash"))
    return out


def derive(root):
    """(runnable, deferred) — commands invoking eng/ tooling, split by whether they run here."""
    runnable, deferred, unreadable = {}, {}, []
    for wf in workflows(root):
        blocks = run_blocks(wf)
        if blocks is None:
            return None, None, None
        if not blocks:
            unreadable.append(os.path.basename(wf))
        for block, shell in blocks:
            for raw in block.splitlines():
                line = raw.strip()
                if not line or line.startswith("#"):
                    continue
                if not (OURS.search(line) or OURS_DIRECT.search(line)):
                    continue
                # `|| true` means the workflow already declared it advisory.
                if "|| true" in line:
                    continue
                key = re.sub(r"\s+", " ", line)
                reason = next((r for m, r in CONTEXT_MARKERS if m in line), None)
                is_ps = str(shell).startswith(("pwsh", "powershell"))
                if is_ps and not HAVE_PWSH:
                    reason = reason or "PowerShell step and pwsh is not on PATH here"
                if reason:
                    deferred.setdefault(key, reason)
                else:
                    runnable.setdefault(key, "pwsh" if is_ps else "bash")
    return runnable, deferred, unreadable


def script_of(cmd):
    m = OURS.search(cmd) or OURS_DIRECT.search(cmd)
    return m.group(1) if m else None


def check(root, list_only=False):
    runnable, deferred, unreadable = derive(root)
    if runnable is None:
        print("REFUSE: PyYAML is not installed, so no workflow could be parsed. Nothing was "
              "checked; this is NOT a pass.", file=sys.stderr)
        return 2
    if not runnable and not deferred:
        print("REFUSE: no gate invocation was derived from any workflow. Either the workflows "
              "moved or the extractor stopped matching; an empty derivation is not a clean tree.",
              file=sys.stderr)
        return 2

    # A command naming a script that is not on disk would fail for a reason that has nothing to do
    # with the tree's health, so it is reported separately rather than counted as a gate failure.
    missing = {c: s for c, s in ((c, script_of(c)) for c in runnable)
               if s and not os.path.isfile(os.path.join(root, s))}

    print("DERIVED: {} runnable + {} context-dependent gate invocation(s) across {} workflow(s)"
          .format(len(runnable), len(deferred), len(workflows(root))))
    if unreadable:
        print("  note: {} workflow(s) declared no run steps".format(len(unreadable)))

    if list_only:
        for c in sorted(runnable):
            print("  WOULD RUN [{}]  {}".format(runnable[c], c))
        for c in sorted(deferred):
            print("  DEFERRED   {}   <- {}".format(c, deferred[c]))
        return 0

    failed, passed, skipped = [], [], []
    for cmd in sorted(runnable):
        if cmd in missing:
            skipped.append((cmd, "script not on disk: {}".format(missing[cmd])))
            continue
        shell = runnable.get(cmd, "bash")
        if shell == "pwsh":
            argv = ["pwsh", "-NoProfile", "-NonInteractive", "-Command", cmd]
            proc = subprocess.run(argv, cwd=root, capture_output=True, text=True)
        else:
            proc = subprocess.run(cmd, shell=True, cwd=root, capture_output=True, text=True)
        if proc.returncode == 0:
            passed.append(cmd)
            print("  PASS    {}".format(cmd))
        else:
            failed.append((cmd, proc.returncode, (proc.stdout or "") + (proc.stderr or "")))
            print("  FAIL {}  {}".format(proc.returncode, cmd))

    print("")
    print("EXAMINED: {} gate(s) ran — {} passed, {} failed".format(
        len(passed) + len(failed), len(passed), len(failed)))
    print("NOT RUN:  {} context-dependent, {} unresolvable".format(len(deferred), len(skipped)))
    for cmd, why in skipped:
        print("    unresolvable: {}  ({})".format(cmd, why), file=sys.stderr)

    if failed:
        print("", file=sys.stderr)
        print("The following gate(s) FAILED here and will fail on the mirror:", file=sys.stderr)
        for cmd, rc, out in failed:
            print("  exit {}  {}".format(rc, cmd), file=sys.stderr)
            for line in [ln for ln in out.splitlines() if ln.strip()][-6:]:
                print("      {}".format(line[:200]), file=sys.stderr)
        return 1

    if not passed:
        print("REFUSE: no gate actually ran, so nothing was verified. An empty run is not a "
              "clean one.", file=sys.stderr)
        return 2

    print("")
    print("pre-mirror-gates: {} gate(s) pass locally. This does NOT cover the {} "
          "context-dependent command(s), nor anything that only reproduces on the CI runner's "
          "operating system.".format(len(passed), len(deferred)))
    return 0


# ── Self-test ───────────────────────────────────────────────────────────────────────────────────
# The failing arms are the load-bearing ones: a runner that cannot report a failure is a runner
# that will report a pass it did not earn.

def self_test():
    import shutil
    import tempfile

    tmp = tempfile.mkdtemp()
    fails = []

    def tree(name, workflow_body, scripts):
        root = os.path.join(tmp, name)
        os.makedirs(os.path.join(root, WORKFLOW_DIR), exist_ok=True)
        os.makedirs(os.path.join(root, "eng", "ci"), exist_ok=True)
        with open(os.path.join(root, WORKFLOW_DIR, "ci.yml"), "w", encoding="utf-8") as fh:
            fh.write(workflow_body)
        for rel, body in scripts.items():
            p = os.path.join(root, rel)
            os.makedirs(os.path.dirname(p), exist_ok=True)
            with open(p, "w", encoding="utf-8") as fh:
                fh.write(body)
            os.chmod(p, 0o755)
        return root

    def arm(label, want, root):
        got = check(root)
        if got == want:
            print("  ok    {} (exit {})".format(label, got))
        else:
            print("  FAIL  {} — expected {}, got {}".format(label, want, got), file=sys.stderr)
            fails.append(label)

    wf = ("name: t\non: [push]\njobs:\n  j:\n    runs-on: ubuntu-latest\n    steps:\n"
          "      - run: bash eng/ci/{}\n")

    arm("a passing gate passes", 0,
        tree("ok", wf.format("good.sh"), {"eng/ci/good.sh": "#!/usr/bin/env bash\nexit 0\n"}))

    # THE ARM THAT MATTERS. Without it this tool is a green light with no bulb.
    arm("a FAILING gate is reported (exit 1)", 1,
        tree("bad", wf.format("bad.sh"), {"eng/ci/bad.sh": "#!/usr/bin/env bash\necho boom\nexit 1\n"}))

    arm("a tree with no workflows REFUSEs, not passes", 2,
        tree("nowf", "name: t\non: [push]\njobs: {}\n", {}))

    # Every command context-dependent => nothing ran => must REFUSE. A pass here would be the
    # exact defect this tool exists to prevent, one level up.
    ctx = ("name: t\non: [push]\njobs:\n  j:\n    runs-on: ubuntu-latest\n    steps:\n"
           "      - run: bash eng/ci/good.sh ${{ matrix.x }}\n")
    arm("only context-dependent commands REFUSEs (nothing ran)", 2,
        tree("ctx", ctx, {"eng/ci/good.sh": "#!/usr/bin/env bash\nexit 0\n"}))

    # An advisory step the workflow itself neutralised must not be counted as a gate.
    adv = ("name: t\non: [push]\njobs:\n  j:\n    runs-on: ubuntu-latest\n    steps:\n"
           "      - run: bash eng/ci/bad.sh || true\n      - run: bash eng/ci/good.sh\n")
    arm("an advisory (|| true) step is not counted as a gate", 0,
        tree("adv", adv, {"eng/ci/bad.sh": "#!/usr/bin/env bash\nexit 1\n",
                          "eng/ci/good.sh": "#!/usr/bin/env bash\nexit 0\n"}))

    # A multi-line run block: the reason this parses YAML instead of grepping lines.
    multi = ("name: t\non: [push]\njobs:\n  j:\n    runs-on: ubuntu-latest\n    steps:\n"
             "      - run: |\n          echo setting up\n          bash eng/ci/bad.sh\n")
    arm("a gate inside a multi-line run block is still found (exit 1)", 1,
        tree("multi", multi, {"eng/ci/bad.sh": "#!/usr/bin/env bash\nexit 1\n"}))

    shutil.rmtree(tmp, ignore_errors=True)
    print("self-test: {} arm(s), {} failed".format(6, len(fails)))
    return 3 if fails else 0


if __name__ == "__main__":
    args = sys.argv[1:]
    if "--self-test" in args:
        sys.exit(self_test())
    sys.exit(check(REPO_ROOT, list_only="--list" in args))
