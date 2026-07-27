#!/usr/bin/env bash
# Verify that the STAGED SNAPSHOT is internally coherent — i.e. that the commit you are about to
# create can actually be built by a clean checkout.
#
# WHY THIS EXISTS, and why the sibling gate cannot do it.
#
# committed-sha-build-gate.sh --staged is the INSTANT half: no build, it asserts `git status` is clean
# for the inputs of every project the commit touches. That catches UNDER-STAGING — a file dirty in your
# tree and absent from the commit. It is blind to the opposite shape: a staged SET that is individually
# valid file-by-file and INCOHERENT as a snapshot. Two measured examples, in opposite directions:
#
#   a PublicAPI baseline staged declaring members the interface at that same commit does not declare
#       -> RS0017, the baseline was staged, the interface it described was not
#   a source file staged adding a public member whose baseline line is in no commit at all
#       -> RS0016, source staged, baseline not
#
# Neither is visible to `git status` (which reports per-file modification and says nothing about whether
# a staged SET coheres), nor to any working-tree build (the tree holds the coupled files, so it compiles
# — the incoherence exists ONLY in the commit). Both reached HEAD and were found only because someone
# happened to build from an isolated worktree.
#
# HOW: materialise the index as a real, dangling commit object (`git write-tree` + `git commit-tree`,
# neither of which touches the working tree or moves any ref), then hand that SHA to the existing,
# already-self-tested `--sha` path, which checks out the SHA into a throwaway worktree and builds the
# projects it touches. We add the snapshot-materialisation; we reuse the build machinery rather than
# re-implementing it. The probe commit is unreferenced and is reaped by gc.
#
# It never mutates the working tree, the index, or any ref — so it is safe to run mid-commit, and it
# cannot destroy uncommitted work the way a checkout/stash of a shared file can.
#
# THREE-STATE, and REFUSE is not a PASS: 0 = the snapshot builds, 1 = it does not (this commit would
# create a HEAD nobody can build), 64 = could not evaluate. A gate must be unable to report a PASS it
# did not earn, so every "we could not check" path exits 64 and says so, rather than falling through to 0.

set -uo pipefail

readonly E_OK=0
readonly E_FAIL=1
readonly E_ENV=64

case "${1:-}" in
    --self-test)
        exec "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/staged-snapshot-coherence.test.sh"
        ;;
    "") ;;
    *)
        echo "[staged-snapshot-coherence] unknown argument: $1" >&2
        exit "$E_ENV"
        ;;
esac

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null || true)"
[ -n "$REPO_ROOT" ] || {
    echo "[staged-snapshot-coherence] CANNOT EVALUATE — not inside a git repository." >&2
    exit "$E_ENV"
}
cd "$REPO_ROOT" || exit "$E_ENV"

SIBLING="$REPO_ROOT/eng/ci/committed-sha-build-gate.sh"
[ -f "$SIBLING" ] || {
    echo "[staged-snapshot-coherence] CANNOT EVALUATE — missing $SIBLING." >&2
    echo "      This gate delegates its build to that one; without it nothing was checked." >&2
    exit "$E_ENV"
}

# An unborn HEAD has no parent to hang the probe commit from, and a repo with no commits has no
# incoherence to find. Say so rather than exiting 0 as if we had checked something.
git rev-parse --verify HEAD >/dev/null 2>&1 || {
    echo "[staged-snapshot-coherence] CANNOT EVALUATE — HEAD is unborn (no commits yet)." >&2
    exit "$E_ENV"
}

# Nothing staged => no snapshot to verify. This is a genuine pass: there is no commit being created
# whose coherence could be wrong.
if git diff --cached --quiet 2>/dev/null; then
    echo "[staged-snapshot-coherence] nothing staged — no snapshot to verify."
    exit "$E_OK"
fi

# `git write-tree` refuses on unmerged index entries. That is exactly a state where we cannot know what
# the snapshot is, so it must REFUSE, not pass.
TREE="$(git write-tree 2>/dev/null || true)"
[ -n "$TREE" ] || {
    echo "[staged-snapshot-coherence] CANNOT EVALUATE — git write-tree failed." >&2
    echo "      Usually an unmerged index (conflicts). Resolve them; nothing was checked." >&2
    exit "$E_ENV"
}

PROBE="$(git commit-tree "$TREE" -p HEAD -m "staged-snapshot coherence probe (dangling, not referenced)" 2>/dev/null || true)"
[ -n "$PROBE" ] || {
    echo "[staged-snapshot-coherence] CANNOT EVALUATE — git commit-tree failed for tree $TREE." >&2
    exit "$E_ENV"
}

echo "[staged-snapshot-coherence] materialised the index as probe commit ${PROBE:0:12} (dangling); building it."

# BOUNDED, because an unbounded build in a commit hook does not fail — it WEDGES. Measured on this
# repo: 171ms when nothing is staged (we exit before building), 74s for a one-project staged change,
# against a commit-hook budget of roughly 120s under real-time AV scanning. So a stage touching two or
# more projects can outlive the hook, and the developer then sees a killed process rather than a
# verdict — the state most likely to make someone reach for `--no-verify`, which disables EVERY gate
# and not just this one.
#
# The bound is OUR instrument, never the code's judgement: exceeding it means "not evaluated", which is
# a REFUSE, and a REFUSE is never a PASS. `timeout` returns 124 when it fires; that is mapped
# explicitly below rather than falling into the catch-all, so the message can say what actually
# happened and name the bound the reader can change.
SSC_TIMEOUT="${STAGED_SNAPSHOT_GATE_TIMEOUT:-100}"
if command -v timeout >/dev/null 2>&1; then
    timeout "${SSC_TIMEOUT}s" bash "$SIBLING" --sha "$PROBE"
    rc=$?
else
    # No `timeout` available: run unbounded rather than skip the check. Say so, because an unbounded
    # run here is a different risk profile than the one documented above and the reader deserves to know.
    echo "[staged-snapshot-coherence] note: 'timeout' unavailable; running the build UNBOUNDED." >&2
    bash "$SIBLING" --sha "$PROBE"
    rc=$?
fi

if [ "$rc" -eq 124 ]; then
    echo "::error::[staged-snapshot-coherence] CANNOT EVALUATE — the snapshot build exceeded ${SSC_TIMEOUT}s." >&2
    echo "      NOTHING was checked. That is not a clean bill of health, and it is not a failure of your" >&2
    echo "      code either — it is this gate's own time bound speaking." >&2
    echo "      Either: verify by hand — bash eng/ci/staged-snapshot-coherence.sh" >&2
    echo "      or raise the bound — STAGED_SNAPSHOT_GATE_TIMEOUT=300 git commit …" >&2
    echo "      or bypass deliberately — STAGED_SNAPSHOT_GATE=off git commit …  (and say which you did)" >&2
    exit "$E_ENV"
fi

case "$rc" in
    0)
        echo "[staged-snapshot-coherence] PASS — the staged snapshot compiles as its own commit."
        exit "$E_OK"
        ;;
    1)
        echo "::error::[staged-snapshot-coherence] THE STAGED SNAPSHOT DOES NOT COMPILE." >&2
        echo "      The commit you are about to create would produce a HEAD that no clean checkout can" >&2
        echo "      build. Your working tree compiling is not evidence — it holds files this commit omits." >&2
        echo "      Typical cause: a public member staged without its PublicAPI baseline line, or a" >&2
        echo "      baseline staged without the source change it describes. Stage the coupled file." >&2
        exit "$E_FAIL"
        ;;
    *)
        # The sibling could not evaluate. Propagate REFUSE — do NOT translate it into a pass.
        echo "::error::[staged-snapshot-coherence] CANNOT EVALUATE — the build gate exited $rc." >&2
        echo "      Nothing was checked. That is not a clean bill of health." >&2
        exit "$E_ENV"
        ;;
esac
