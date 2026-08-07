#!/usr/bin/env bash
# integration-shard-partition-gate.sh — the integration shards must PARTITION the integration filter.
#
# WHY THIS EXISTS
#   The integration suite is split across IntegrationTests-*.slnf shards so it runs in parallel, while
#   IntegrationTests.slnf remains the canonical superset — nightly builds it, committed-content-gates
#   builds it, and orphan-test-project-gate.sh uses it as its membership oracle.
#
#   That leaves a gap the orphan gate cannot see. A test project added to the PARENT filter satisfies
#   the orphan gate, compiles in nightly, and is a member in good standing — while belonging to no
#   shard, so it RUNS NOWHERE on a pull request. The suite reports green having never executed it.
#   That is the same defect the orphan gate exists to prevent, one level out: membership without
#   execution instead of existence without compilation.
#
#   The reverse is quieter but still wrong. A project in a shard but not the parent is invisible to
#   the orphan gate and to nightly, so it drifts out of the set everything else reasons about.
#
#   And a project in TWO shards runs twice: double the wall clock it was split to save, and its
#   coverage counted twice in a combined number that gates merges.
#
# WHAT IT CHECKS
#   Over the test projects (paths under tests/) of the parent and every IntegrationTests-*.slnf shard:
#     1. union(shards) covers the parent      — nothing runs nowhere
#     2. union(shards) adds nothing to it     — nothing runs unknown to the parent
#     3. no TEST ASSEMBLY is in two shards    — nothing runs twice
#     4. every shard holds >= 1 test assembly — no shard is a no-op that reports green by running nothing
#
#   tests/Shared/** is a SUPPORT LIBRARY, not a test assembly: every shard needs it and it is expected
#   in all of them, so it is exempt from 3 and does not satisfy 4.
#
# Exit codes (the orphan-test-project-gate.sh / spa-gate.sh contract):
#   0  the property holds: the shards partition the parent
#   1  the property is FALSE: a project runs nowhere, runs unknown, runs twice, or a shard is empty
#   2  the property could not be EVALUATED: the repo root, the parent, or the shards are missing
#
# Overridable for the self-test (drive an isolated fixture tree):
#   SHARD_DIR    (default <repo>/eng/ci/shards)
#   PARENT_FILE  (default $SHARD_DIR/IntegrationTests.slnf)
#
# Usage:  eng/ci/integration-shard-partition-gate.sh [--self-test]

set -uo pipefail

readonly E_OK=0
readonly E_PARTITION=1
readonly E_ENV=2

if [ "${1:-}" = "--self-test" ]; then
    exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/integration-shard-partition-gate.test.sh"
fi

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || true)"
SHARD_DIR="${SHARD_DIR:-${REPO_ROOT}/eng/ci/shards}"
PARENT_FILE="${PARENT_FILE:-${SHARD_DIR}/IntegrationTests.slnf}"

[ -n "$REPO_ROOT" ]   || { echo "[integration-shard-partition-gate] CANNOT EVALUATE — not in a git repo." >&2; exit "$E_ENV"; }
[ -d "$SHARD_DIR" ]   || { echo "[integration-shard-partition-gate] CANNOT EVALUATE — no shard dir at $SHARD_DIR." >&2; exit "$E_ENV"; }
[ -f "$PARENT_FILE" ] || { echo "[integration-shard-partition-gate] CANNOT EVALUATE — no parent filter at $PARENT_FILE." >&2; exit "$E_ENV"; }
command -v python3 >/dev/null 2>&1 || { echo "[integration-shard-partition-gate] CANNOT EVALUATE — python3 not available." >&2; exit "$E_ENV"; }

SHARD_DIR="$SHARD_DIR" PARENT_FILE="$PARENT_FILE" python3 - <<'PYEOF'
import glob, json, os, sys

E_OK, E_PARTITION, E_ENV = 0, 1, 2
BS = chr(92)
shard_dir = os.environ['SHARD_DIR']
parent_file = os.environ['PARENT_FILE']
TAG = '[integration-shard-partition-gate]'


def load(path):
    """Project paths in a solution filter, normalised so separator style cannot cause a false verdict."""
    try:
        with open(path, encoding='utf-8') as fh:
            raw = json.load(fh)['solution']['projects']
    except Exception as exc:                                    # noqa: BLE001 - any read failure is E_ENV
        print(f'{TAG} CANNOT EVALUATE — {path}: {exc}', file=sys.stderr)
        sys.exit(E_ENV)
    return {p.replace(BS, '/').lstrip('./').lower() for p in raw}


def tests_only(paths):
    return {p for p in paths if p.startswith('tests/')}


def is_support(path):
    """tests/Shared/** is a library every shard links; it is not an executable test assembly."""
    return path.startswith('tests/shared/')


parent = tests_only(load(parent_file))
shard_files = sorted(glob.glob(os.path.join(shard_dir, 'IntegrationTests-*.slnf')))

if not shard_files:
    print(f'{TAG} CANNOT EVALUATE — no IntegrationTests-*.slnf shards found in {shard_dir}.', file=sys.stderr)
    sys.exit(E_ENV)

shards = {os.path.basename(f): tests_only(load(f)) for f in shard_files}
union = set().union(*shards.values())

failures = []

# 1 + 2: the union must be exactly the parent.
runs_nowhere = parent - union
runs_unknown = union - parent
if runs_nowhere:
    failures.append(
        'these test projects are in the parent filter but in NO shard, so they RUN NOWHERE on a pull '
        'request while the suite reports green:\n    ' + '\n    '.join(sorted(runs_nowhere)))
if runs_unknown:
    failures.append(
        'these test projects are in a shard but NOT in the parent filter, so nightly and the orphan '
        'gate cannot see them:\n    ' + '\n    '.join(sorted(runs_unknown)))

# 3: no test assembly in two shards.
for path in sorted(union):
    if is_support(path):
        continue
    owners = sorted(name for name, ps in shards.items() if path in ps)
    if len(owners) > 1:
        failures.append(
            f'{path} is in {len(owners)} shards ({", ".join(owners)}), so it runs twice — double the '
            'wall clock the split exists to save, and its coverage counted twice in the combined '
            'number that gates merges.')

# 4: liveness. A shard holding no test assembly passes every check above by running nothing.
for name, ps in sorted(shards.items()):
    if not any(not is_support(p) for p in ps):
        failures.append(
            f'{name} contains no test assembly — only support libraries. It would report green having '
            'executed nothing.')

if failures:
    print(f'{TAG} the integration shards do NOT partition {os.path.basename(parent_file)}:', file=sys.stderr)
    for f in failures:
        print(f'  - {f}', file=sys.stderr)
    print(f'{TAG} add the project to exactly one IntegrationTests-*.slnf shard, or remove it from the '
          f'parent if it is not meant to run.', file=sys.stderr)
    sys.exit(E_PARTITION)

assemblies = sum(1 for p in union if not is_support(p))
print(f'{TAG} PASS — {len(shards)} shards partition {assemblies} test assembl'
      f'{"y" if assemblies == 1 else "ies"} with no gap, no overlap, and none left unrun.')
for name, ps in sorted(shards.items()):
    own = sorted(p for p in ps if not is_support(p))
    print(f'    {name:<40} {len(own)} assembl{"y" if len(own) == 1 else "ies"}')
sys.exit(E_OK)
PYEOF
