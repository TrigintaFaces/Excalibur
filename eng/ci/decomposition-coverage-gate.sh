#!/usr/bin/env bash
# decomposition-coverage-gate.sh — every requirement id in a sprint spec must be mapped by its
# decomposition, or the gate names the ones that are not.
#
# THE CLASS THIS CATCHES, and why prose could not. A decomposition is a DERIVED artifact: it is
# generated once from a spec and then both drift. When the spec moves, nothing re-derives the
# mapping and nothing fails — there is no diff to review and no error to notice, because a stale
# derivation is a perfectly valid file. Measured: a decomposition was premise-gated at one commit
# and never re-derived; 15 spec ids ended up unmapped while all four lanes built from the table, and
# one of them was a live P0 with no mini-spec and therefore NO OWNER. A person caught it. Nothing
# else could have, because the failure mode is defined by producing no signal.
#
# THE TOKENISATION TRAP, which is why a naive grep-diff is worse than useless here. Specs write
# ranges: `AC-16,17`. A raw token scan reads that as `AC-16` alone and silently loses `AC-17` — and
# it loses it on BOTH sides, so the two sides can agree while both are wrong. That over-reported an
# earlier count as 22 against a true 15. Ranges are EXPANDED before either side is compared:
#
#     AC-16,17    ->  AC-16  AC-17
#     FR-13,14    ->  FR-13  FR-14
#     AC-11a      ->  AC-11a           (suffixed variants are distinct ids, never folded)
#
#   USAGE
#     eng/ci/decomposition-coverage-gate.sh [<spec.md> <decomposition.md>]
#     eng/ci/decomposition-coverage-gate.sh --self-test
#     (with no args, resolves the highest-numbered management/specs/sprint-*-spec.md and its pair)
#
#   CONTRACT
#     exit 0  every id in the spec appears in the decomposition
#     exit 1  at least one spec id is UNMAPPED — named individually
#     exit 2  cannot evaluate (a file missing, or a side parsed to ZERO ids) — never a silent pass
#
# A side that parses to zero ids is exit 2, NOT exit 0. Zero-vs-zero is the shape a broken extractor
# produces, and it is indistinguishable from perfect coverage unless it is refused explicitly.

set -u

_here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "${1:-}" = "--self-test" ]; then
    exec bash "$_here/decomposition-coverage-gate.test.sh"
fi

command -v python3 >/dev/null 2>&1 || { echo "REFUSE: python3 unavailable" >&2; exit 2; }

SPEC="${1:-}"
DECOMP="${2:-}"

# --dry-run / the two overrides exist to close a SPECIFIC hole, and it is worth naming because the
# hole is invisible from inside the gate.
#
# The gate's own RED path is provable from its lock (planted unmapped id -> exit 1). The HOOK's
# rejection branch is NOT: pre-commit calls this with no arguments, so it always resolves the real
# sprint spec, which is currently fully mapped. That means the end-to-end path could only ever be
# exercised GREEN, and "GREEN end-to-end plus RED at the unit" is not "RED end-to-end" — a
# rejection branch nothing has ever driven is exactly an instrument reporting on a property it
# never examined. These overrides let the REAL hook be driven against fixtures, so the branch that
# blocks a commit is proven to block one, without editing anyone's spec.
if [ -n "${DECOMP_SPEC_OVERRIDE:-}" ] && [ -z "$SPEC" ]; then
    SPEC="$DECOMP_SPEC_OVERRIDE"
    DECOMP="${DECOMP_DECOMP_OVERRIDE:-}"
    [ -n "$DECOMP" ] || { echo "REFUSE: DECOMP_SPEC_OVERRIDE set without DECOMP_DECOMP_OVERRIDE" >&2; exit 2; }
fi

if [ -z "$SPEC" ]; then
    REPO="$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "REFUSE: not a git repo" >&2; exit 2; }
    SPEC="$(ls "$REPO"/management/specs/sprint-*-spec.md 2>/dev/null | sort -V | tail -1)"
    [ -n "$SPEC" ] || { echo "REFUSE: no sprint spec found" >&2; exit 2; }
    _n="$(basename "$SPEC" | sed -E 's/sprint-([0-9]+)-spec\.md/\1/')"
    DECOMP="$REPO/management/specs/sprint-${_n}-decomposition.md"
fi

[ -f "$SPEC" ]   || { echo "REFUSE: spec not readable: $SPEC" >&2; exit 2; }
[ -f "$DECOMP" ] || { echo "REFUSE: decomposition not readable: $DECOMP" >&2; exit 2; }

python3 - "$SPEC" "$DECOMP" <<'PY'
import re, sys

spec_path, decomp_path = sys.argv[1], sys.argv[2]

# One id, or a comma-separated run sharing the prefix: FR-13,14  ·  AC-16,17  ·  AC-11a
RUN = re.compile(r'\b(FR|AC)-(\d+[a-z]?(?:\s*,\s*\d+[a-z]?)*)', re.IGNORECASE)

def ids(path):
    out = set()
    with open(path, encoding='utf-8', errors='replace') as f:
        for line in f:
            for prefix, run in RUN.findall(line):
                for part in run.split(','):
                    part = part.strip()
                    if part:
                        out.add(f"{prefix.upper()}-{part}")
    return out

spec, decomp = ids(spec_path), ids(decomp_path)

# A side that extracted nothing means the EXTRACTOR failed, not that coverage is perfect. Refusing
# here is the whole difference between this gate and one that reports success for doing nothing.
if not spec:
    print(f"REFUSE: extracted ZERO ids from the spec ({spec_path}) — extractor is broken, not coverage perfect", file=sys.stderr)
    sys.exit(2)
if not decomp:
    print(f"REFUSE: extracted ZERO ids from the decomposition ({decomp_path}) — refusing to call that full coverage", file=sys.stderr)
    sys.exit(2)

def sort_key(i):
    m = re.match(r'(FR|AC)-(\d+)([a-z]?)', i)
    return (m.group(1), int(m.group(2)), m.group(3)) if m else (i, 0, '')

unmapped = sorted(spec - decomp, key=sort_key)
extra    = sorted(decomp - spec, key=sort_key)

print(f"spec ids: {len(spec)} · decomposition ids: {len(decomp)} · unmapped: {len(unmapped)}")

# Ids only in the decomposition are reported but do NOT fail: a mini-spec may legitimately introduce
# a sub-id the parent does not spell out. Reported anyway, because it is also how a decomposition
# built against a SUPERSEDED spec first shows itself.
if extra:
    print(f"note: {len(extra)} id(s) in the decomposition but not the spec (not a failure): "
          + ", ".join(extra[:12]) + ("..." if len(extra) > 12 else ""))

if unmapped:
    print("\nUNMAPPED — in the spec, absent from the decomposition:", file=sys.stderr)
    for i in unmapped:
        print(f"  {i}", file=sys.stderr)
    print("\nFAIL: the decomposition does not cover the spec. Each id above has NO mini-spec,", file=sys.stderr)
    print("therefore no owner, and a lane building from the table will not know it exists.", file=sys.stderr)
    print("Re-derive the decomposition from the current spec.", file=sys.stderr)
    sys.exit(1)

print("OK: every spec id is mapped by the decomposition.")
sys.exit(0)
PY
