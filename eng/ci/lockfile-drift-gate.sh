#!/usr/bin/env bash
# Fails when a tracked packages.lock.json has been rewritten by restore.
#
# WHY THIS EXISTS
#   Directory.Build.props sets RestorePackagesWithLockFile=true, so `dotnet restore`
#   WRITES lock files. No workflow uses RestoreLockedMode, so restore is free to
#   resolve a different package graph and silently rewrite the lock file to match.
#   The lock file then records whatever CI happened to resolve, which is the exact
#   opposite of what a lock file is for: it documents the drift instead of
#   preventing it, and nothing ever reports that it happened.
#
# THREE-STATE CONTRACT — REFUSE is not a pass:
#   exit 0  PASS     tracked lock files were found and none drifted
#   exit 1  FAIL     at least one tracked lock file was rewritten
#   exit 2  REFUSE   no tracked lock files found, so no claim about drift is made
#
#   The REFUSE state matters more here than usual. Only a handful of this repo's
#   projects commit a lock file; a gate that reported PASS when it found none
#   would be green precisely because it was checking nothing, and would stay green
#   as coverage fell to zero.
#
# SCOPE
#   Only files tracked by git are considered, and agent worktrees are excluded.
#   A `find` for packages.lock.json across this repo returns 52 hits, of which 48
#   are copies inside throwaway worktrees under .claude/worktrees and .dts/. Those
#   are clones, not configuration sites; counting them overstates coverage 13x.

set -uo pipefail

EXIT_PASS=0
EXIT_FAIL=1
EXIT_REFUSE=2

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || {
	echo "::error::lockfile-drift-gate REFUSED: not inside a git work tree." >&2
	exit $EXIT_REFUSE
}
cd "$REPO_ROOT" || exit $EXIT_REFUSE

# Tracked lock files only, worktree clones excluded.
mapfile -t LOCKFILES < <(git ls-files '*packages.lock.json' | grep -v -e '^\.claude/worktrees/' -e '^\.dts/' || true)

if [ "${#LOCKFILES[@]}" -eq 0 ]; then
	echo "lockfile-drift-gate: REFUSE"
	echo "  lock files CHECKED : 0"
	echo "  reason             : no tracked packages.lock.json found"
	echo "::error::lockfile-drift-gate REFUSED: nothing to check. A gate that finds no inputs has not verified anything."
	exit $EXIT_REFUSE
fi

DRIFTED=()
for f in "${LOCKFILES[@]}"; do
	# --quiet exits non-zero when the file differs from the index/HEAD.
	if ! git diff --quiet -- "$f" 2>/dev/null; then
		DRIFTED+=("$f")
	fi
done

echo "lockfile-drift-gate: $([ "${#DRIFTED[@]}" -eq 0 ] && echo PASS || echo FAIL)"
echo "  lock files CHECKED : ${#LOCKFILES[@]}"
echo "  drifted            : ${#DRIFTED[@]}"

if [ "${#DRIFTED[@]}" -gt 0 ]; then
	for f in "${DRIFTED[@]}"; do
		stat_line="$(git diff --numstat -- "$f" | awk '{print "+"$1" -"$2}')"
		echo "::error::Lock file rewritten by restore: $f ($stat_line)"
	done
	echo
	echo "  A rewritten lock file means the resolved package graph no longer matches"
	echo "  what was committed. Either commit the new lock file deliberately, or pin"
	echo "  the versions that caused the change. Do not let restore decide silently."
	exit $EXIT_FAIL
fi

echo "  reason             : every tracked lock file matches its committed content"
exit $EXIT_PASS
