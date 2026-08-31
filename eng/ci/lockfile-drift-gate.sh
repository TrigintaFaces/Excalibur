#!/usr/bin/env bash
# Fails when a tracked packages.lock.json has been rewritten by restore.
#
# WHY THIS EXISTS
#   Directory.Build.props sets RestorePackagesWithLockFile=true, so `dotnet restore`
#   WRITES lock files. An unlocked restore is therefore free to resolve a different
#   package graph and silently rewrite the lock file to match. The lock file then
#   records whatever the restore happened to resolve, which is the exact opposite of
#   what a lock file is for: it documents the drift instead of preventing it, and
#   nothing ever reports that it happened.
#
#   The producing and verifying builds now both restore in locked mode, so a graph
#   that disagrees with the committed lock files fails there. This gate covers what
#   locked mode does not: the unlocked restores that still exist, and any lock file
#   a change left stale. Locked mode refuses to resolve the wrong graph; this gate
#   reports a lock file that was rewritten to match one.
#
# THREE-STATE CONTRACT — REFUSE is not a pass:
#   exit 0  PASS     tracked lock files were found and none drifted
#   exit 1  FAIL     at least one tracked lock file was rewritten
#   exit 2  REFUSE   no tracked lock files found, so no claim about drift is made
#
#   The REFUSE state matters more here than usual. A gate that reported PASS when it
#   found no lock files would be green precisely because it was checking nothing, and
#   would stay green as coverage fell to zero. REFUSE is what makes an empty input set
#   visible instead of reassuring.
#
# SCOPE
#   Only files tracked by git are considered. `git ls-files` structurally cannot
#   return a gitignored path, so throwaway worktree clones are excluded by
#   construction -- no path-based filter is needed or present. Measured on the
#   tracked set: 203 lock files, all of them real configuration sites. An earlier
#   version of this note put the figure at 4 by reasoning from a `find` count rather
#   than from `git ls-files`; the two answer different questions and only the second
#   one describes what this gate reads.

set -uo pipefail

EXIT_PASS=0
EXIT_FAIL=1
EXIT_REFUSE=2

REPO_ROOT="$(git rev-parse --show-toplevel 2>/dev/null)" || {
	echo "::error::lockfile-drift-gate REFUSED: not inside a git work tree." >&2
	exit $EXIT_REFUSE
}
cd "$REPO_ROOT" || exit $EXIT_REFUSE

# Tracked lock files only; gitignored worktree clones cannot appear here.
#
# ...but "cannot" there leans on .gitignore being right, and this gate's whole job is to refuse to
# report a coverage it did not have. Track one file under a clone directory -- by accident, or by a
# repo that ships its worktrees -- and a tree with ZERO real lock files reports PASS on the strength
# of throwaway copies. So the clone paths are excluded HERE as well, by path, and the two exclusions
# are independent: trackedness and location. Dropping every entry still REFUSEs below.
mapfile -t LOCKFILES < <(git ls-files '*packages.lock.json' 	| grep -vE '(^|/)(\.claude/worktrees|\.dts)/')

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
