#!/usr/bin/env bash
# Preflight hygiene gate for .github/workflows and .github/actions.
#
# Every rule below exists because the defect it detects actually shipped here and cost a run:
#
#   unpinned action      a mutable tag is a promise, not an identity; a retag changes what runs
#   missing permissions  a workflow with none inherits the repository default, possibly write-all
#   session >= wall      the cap can never fire; the runner kills the job and writes NO results
#   dangling backslash   a continuation followed by a blank line silently truncates the command
#   comment in a run-on  a backslash joins lines BEFORE comments are stripped, so a `#` inside a
#                        continued command swallows every flag after it
#
# Exit 0 clean · 1 violations · 2 REFUSE (could not evaluate). REFUSE is never a pass.
set -uo pipefail
E_OK=0; E_VIOL=1; E_ENV=2
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT" || { echo "REFUSE: cannot enter repo root" >&2; exit $E_ENV; }

if ! command -v python3 >/dev/null 2>&1; then
    echo "REFUSE: python3 not found; the YAML checks cannot run. This is NOT a pass." >&2
    exit $E_ENV
fi

SELF_TEST=0; [ "${1:-}" = "--self-test" ] && SELF_TEST=1

python3 - "$SELF_TEST" <<'PY'
import sys, glob, io, re, os
BS = chr(92)
self_test = sys.argv[1] == "1"
viol = []

def scan(files):
    out = []
    try:
        import yaml
    except ImportError:
        print("REFUSE: PyYAML unavailable; cannot evaluate. This is NOT a pass.", file=sys.stderr)
        sys.exit(2)
    for f in files:
        raw = io.open(f, encoding="utf-8").read()
        try:
            d = yaml.safe_load(raw)
        except Exception as e:
            out.append((f, "unparseable", str(e)[:90])); continue
        if d is None:
            continue
        is_wf = "/workflows/" in f.replace(BS, "/")
        if is_wf and "permissions" not in d:
            out.append((f, "no-permissions", "workflow inherits the default token"))
        jobs = (d.get("jobs") or {})
        steps = []
        for jn, j in jobs.items():
            wall = j.get("timeout-minutes")
            for s in (j.get("steps") or []):
                steps.append((jn, wall, s))
        for s in ((d.get("runs") or {}).get("steps") or []):
            steps.append(("<composite>", None, s))
        for jn, wall, s in steps:
            u = s.get("uses")
            if u and not u.startswith("./") and not re.search(r"@[0-9a-f]{40}$", u):
                out.append((f, "unpinned-action", f"{jn}: {u}"))
            # Shipping packages must be ONE artifact. Two package artifacts built from the same
            # sources create two candidates and no rule for choosing, and the consumers duly
            # disagreed: publish took `packages-ubuntu-latest` by name while the GitHub release
            # merged `packages-*` into one directory where same-named .nupkg files overwrite each
            # other. Any OS-suffixed or pattern-matched shipping artifact reopens that.
            w = s.get("with") or {}
            if u and ("upload-artifact" in u or "download-artifact" in u):
                art, pat = str(w.get("name") or ""), str(w.get("pattern") or "")
                bad = art if re.match(r"^packages[-.]", art) else (pat if re.match(r"^packages[-*.]", pat) else "")
                if bad:
                    out.append((f, "split-shipping-artifact",
                                f"{jn}: '{bad}' -- shipping packages must be a single artifact named "
                                f"'packages'; an OS suffix or a glob makes two candidates pickable"))
            r = str(s.get("run") or "")
            if not r:
                continue
            m = re.search(r"TestSessionTimeout=(\d+)", r)
            if m and wall:
                if int(m.group(1)) / 60000.0 >= wall:
                    out.append((f, "session-ge-wall",
                                f"{jn}: cap {int(m.group(1))//60000}m vs wall {wall}m -- cap can never fire"))
            lines = r.split("\n")
            for i, l in enumerate(lines[:-1]):
                if l.rstrip().endswith(BS):
                    nxt = lines[i + 1]
                    if not nxt.strip():
                        out.append((f, "dangling-continuation", f"{jn}: line {i+1}"))
                    elif nxt.strip().startswith("#"):
                        out.append((f, "comment-in-continuation", f"{jn}: line {i+2} swallows the rest"))
    return out

if self_test:
    import tempfile
    d = tempfile.mkdtemp()
    os.makedirs(os.path.join(d, "workflows"))
    bad = os.path.join(d, "workflows", "bad.yml")
    io.open(bad, "w", newline="\n").write(
        "name: bad\non: push\njobs:\n  j:\n    runs-on: ubuntu-latest\n    timeout-minutes: 10\n"
        "    steps:\n      - uses: actions/checkout@v7\n      - run: |\n"
        "          cmd " + BS + "\n          # swallowed\n          --flag\n"
        "      - run: dotnet test -- RunConfiguration.TestSessionTimeout=600000\n"
        "      - uses: actions/upload-artifact@" + "0"*40 + "\n        with:\n"
        "          name: packages-ubuntu-latest\n")
    found = {k for _, k, _ in scan([bad])}
    need = {"no-permissions", "unpinned-action", "comment-in-continuation", "session-ge-wall",
            "split-shipping-artifact"}
    missing = need - found
    print("SELF-TEST: planted 5 defect classes, detected", sorted(found))
    if missing:
        print("SELF-TEST FAIL -- undetected:", sorted(missing), file=sys.stderr); sys.exit(1)
    clean = os.path.join(d, "workflows", "ok.yml")
    io.open(clean, "w", newline="\n").write(
        "name: ok\non: push\npermissions:\n  contents: read\njobs:\n  j:\n    runs-on: ubuntu-latest\n"
        "    steps:\n      - run: echo hi\n")
    if scan([clean]):
        print("SELF-TEST FAIL -- flagged a clean file:", scan([clean]), file=sys.stderr); sys.exit(1)
    print(f"SELF-TEST PASS (safety: all {len(need)} planted defect classes caught; "
          "liveness: clean file passes)")
    sys.exit(0)

files = sorted(glob.glob(".github/workflows/*.yml")) + sorted(glob.glob(".github/actions/*/action.yml"))
if not files:
    print("REFUSE: no workflow files found. This is NOT a pass.", file=sys.stderr); sys.exit(2)
viol = scan(files)
if viol:
    print(f"workflow-hygiene: {len(viol)} violation(s)")
    for f, kind, detail in viol:
        print(f"  ::error::{kind}: {f} -- {detail}")
    sys.exit(1)
print(f"workflow-hygiene: clean across {len(files)} file(s)")
sys.exit(0)
PY
