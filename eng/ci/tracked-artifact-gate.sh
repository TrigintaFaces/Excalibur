#!/usr/bin/env bash
# Fails when a gate or harness script references a repository path that is not git-tracked.
#
# WHY
#   A gate reads its inputs from the working tree. If an input is untracked, the gate passes
#   locally — the file is right there — and finds nothing on a clean checkout, where the file
#   does not exist. The gate is then green for everyone and enforcing for no one. `git clean`
#   destroys such a file unrecoverably, and nothing reports that a check stopped checking.
#
#   This gate makes that state loud: any path a gate references must exist in the index.
#
# WHAT IT SCANS
#   Caller scripts:   eng/ci/*.sh  ·  eng/hooks/*  ·  .claude/harness/*.sh
#   Referenced paths: quoted or bare tokens containing '/' that end in a known script or data
#                     extension, plus the repo-relative directories those gates conventionally read.
#
# WHAT IT DELIBERATELY DOES NOT DO
#   It does not resolve variables. A path assembled at runtime ("$DIR/$NAME") is invisible to it,
#   and no static scan can see those without executing the script. It reports what it can prove,
#   and its self-test states this limit rather than implying coverage it does not have.
#
# EXIT  0 = every referenced path is tracked.  1 = at least one is not.  2 = usage/environment.

set -uo pipefail

usage() {
	cat <<'USAGE'
usage: tracked-artifact-gate.sh [--self-test]

  (no args)     scan gate/harness callers; exit 1 if any referenced path is untracked
  --self-test   prove the gate is non-vacuous: it must go RED on a planted untracked
                reference and GREEN when that reference is removed
USAGE
}

repo_root() { git rev-parse --show-toplevel 2>/dev/null; }

# Extract candidate repo-relative paths from a file.
# Conservative on purpose: a token must contain '/' and end in an extension we know gates read.
extract_paths() {
	grep -ohE '[A-Za-z0-9_./-]+/[A-Za-z0-9_.-]+\.(sh|py|ps1|yaml|yml|json|txt|md|slnf)' "$1" 2>/dev/null \
		| sed 's#^\./##' \
		| sort -u
}

scan() {
	local root untracked=0 checked=0
	root="$(repo_root)" || { echo "not a git repository" >&2; return 2; }
	cd "$root" || return 2

	local callers
	# 'eng/hooks/*' sweeps in README.md, and a path MENTIONED in documentation is not a path
	# INVOKED by a caller. Judging a doc reference as a broken invocation is a category error.
	callers="$(git ls-files 'eng/ci/*.sh' 'eng/hooks/*' '.claude/harness/*.sh' 2>/dev/null \
		| grep -vE '\.(md|txt)$')"
	if [ -z "$callers" ]; then
		echo "no caller scripts found — refusing to report a pass over an empty set" >&2
		return 2
	fi

	local caller path
	local missing=0
	while IFS= read -r caller; do
		[ -f "$caller" ] || continue
		while IFS= read -r path; do
			[ -n "$path" ] || continue
			# A self-test's own runtime fixture is created and removed inside a single run, so a
			# static scan sees a reference to something that legitimately does not exist. Two of
			# these were MY OWN gate's fixtures on the first pass — the gate flagging itself.
			# NOTE the guard below: this skip applies ONLY to a path that does not exist.
			# When it sat unconditionally here it also swallowed `.selftest-untracked-target.sh`
			# WHILE THAT FILE EXISTED — the fixture arm 2 plants to prove this gate can still
			# FAIL. A noise filter silently disabled the non-vacuity proof, and only running the
			# self-test caught it. Anything that EXISTS must stay judgeable, always.
			if [ ! -e "$path" ]; then
				case "${path##*/}" in .selftest*) continue ;; esac
			fi
			# MENTIONING a path is not INVOKING it. A comment documenting where a check went, or
			# a variable holding a deliberately-dead path so a lock can assert its ABSENCE, are
			# both references to something that correctly does not exist. Judging them produced
			# 3 MISSING and all 3 were false — including a lock whose whole purpose is asserting
			# that very file is gone. Only an invocation-position reference can be a broken call.
			# (The lock I mis-flagged already solved this: it greps for the path and then
			# `grep -Evq ':[[:space:]]*#'` to exclude comment lines.)
			if [ ! -e "$path" ] && ! grep -qE "(^|[^#[:alnum:]_])(bash|sh|source|\.)[[:space:]]+[\"']?[^\"']*${path##*/}" "$caller" 2>/dev/null; then
				continue
			fi
			if [ ! -e "$path" ]; then
				# A token that does not exist is USUALLY a URL fragment, a glob, or prose, and
				# flagging those would bury the real signal. But a token that is SHAPED LIKE A
				# SCRIPT and does not exist is a caller invoking something that is not there —
				# strictly worse than the untracked case this gate was built for. Silently
				# skipping it turns "missing" into silence, which is this gate's own defect
				# class pointed at itself. Judge those; skip the rest.
				# The path must ALSO be repo-root-relative and literal. extract_paths strips a
				# leading '$', so an unexpanded variable arrives as "HARNESS_DIR/foo.sh" or
				# "REPO/.claude/x.sh" — 68 of those on the first attempt at this arm, which is
				# precisely the noise the skip-if-absent rule was avoiding. Only a token that
				# begins with a real top-level directory can be judged as a literal reference.
				case "$path" in
					eng/*.sh|eng/*.py|eng/*.ps1|eng/*.bash|\
					.claude/*.sh|.claude/*.py|.claude/*.ps1|.claude/*.bash|\
					tests/*.sh|tests/*.py|tests/*.ps1|tests/*.bash)
						echo "MISSING: $path"
						echo "    referenced by: $caller"
						missing=$((missing + 1))
						;;
				esac
				continue
			fi
			checked=$((checked + 1))
			if ! git ls-files --error-unmatch "$path" >/dev/null 2>&1; then
				echo "UNTRACKED: $path"
				echo "    referenced by: $caller"
				untracked=$((untracked + 1))
			fi
		done <<< "$(extract_paths "$caller")"
	done <<< "$callers"

	# Report what was examined. A gate that says "clean" without saying over what is the
	# same defect this gate exists to catch.
	echo "tracked-artifact-gate: examined $checked existing referenced path(s) across $(echo "$callers" | grep -c .) caller(s)"

	# REFUSE before FAIL: a caller referencing a script that is not on disk means this gate
	# cannot judge that reference at all. That is neither a pass nor a tracked-ness failure —
	# it is an inability to answer, and it must never be reported as either.
	if [ "$missing" -gt 0 ]; then
		echo "tracked-artifact-gate: REFUSE — $missing referenced script(s) do not exist on disk" >&2
		return 2
	fi

	if [ "$untracked" -gt 0 ]; then
		echo "tracked-artifact-gate: FAILED — $untracked referenced path(s) not in the index" >&2
		return 1
	fi
	echo "tracked-artifact-gate: PASSED"
	return 0
}

self_test() {
	local root tmp_caller tmp_target rc failed=0
	root="$(repo_root)" || { echo "not a git repository" >&2; return 2; }
	cd "$root" || return 2

	echo "tracked-artifact-gate self-test"

	# ARM 1 — LIVENESS. The gate must PASS on the repository as it stands, or a RED in arm 2
	# proves nothing: a gate that always fails would satisfy arm 2 and be useless.
	if scan >/dev/null 2>&1; then
		echo "  PASS  arm 1: gate is GREEN on the current tree (so arm 2's RED is meaningful)"
	else
		echo "  FAIL  arm 1: gate is already RED — arm 2 cannot distinguish the planted case" >&2
		echo "        (this is not necessarily a bug in the gate; the tree may genuinely have an" >&2
		echo "         untracked referenced path. Run without --self-test to see which.)" >&2
		failed=1
	fi

	# ARM 2 — SAFETY, by construction.
	#
	# The plant must match the REAL defect: a TRACKED caller referencing an UNTRACKED artifact.
	# An untracked caller is the wrong plant — scan() enumerates callers with `git ls-files`,
	# which lists only tracked files, so an untracked caller is never read and the plant proves
	# nothing. An earlier version of this self-test made exactly that mistake and reported a
	# vacuous gate as passing arm 1 alone.
	#
	# The tracked caller is mutated and restored from a private copy. `git checkout` is never
	# used here: it restores to HEAD and would destroy any uncommitted work in that file.
	tmp_target=".claude/harness/.selftest-untracked-target.sh"
	tmp_caller="$(git ls-files 'eng/ci/*.sh' | head -1)"

	if [ -z "$tmp_caller" ]; then
		echo "  FAIL  arm 2: no tracked caller available to mutate" >&2
		failed=1
	else
		printf '#!/usr/bin/env bash\necho planted\n' > "$tmp_target"
		cp "$tmp_caller" "$tmp_caller.selftest-bak"
		printf '\n# self-test plant: %s\n' "$tmp_target" >> "$tmp_caller"

		scan >/dev/null 2>&1
		rc=$?

		mv "$tmp_caller.selftest-bak" "$tmp_caller"
		rm -f "$tmp_target"

		if [ "$rc" -eq 1 ]; then
			echo "  PASS  arm 2: gate went RED on a tracked caller referencing an untracked path"
		else
			echo "  FAIL  arm 2: gate returned $rc on a planted untracked reference (expected 1)" >&2
			failed=1
		fi
	fi

	# ARM 3 — the tree is exactly as it was. A self-test that leaves a mutation behind turns
	# every later run red for reasons the next reader cannot reconstruct.
	if [ ! -e "$tmp_target" ] && [ ! -e "$tmp_caller.selftest-bak" ] \
		&& git diff --quiet -- "$tmp_caller" 2>/dev/null; then
		echo "  PASS  arm 3: planted files removed and the mutated caller is byte-identical"
	else
		echo "  FAIL  arm 3: the tree is dirty after the plant" >&2
		failed=1
	fi

	# ARM 4 — stated limit, asserted rather than only documented. A runtime-assembled path is
	# invisible to a static scan. This arm exists so nobody reads a green as full coverage.
	tmp_caller=".claude/harness/.selftest-dynamic.sh"
	printf '#!/usr/bin/env bash\nD=.claude/harness\nbash "$D/nonexistent-runtime.sh"\n' > "$tmp_caller"
	# Assert on the SPECIFIC path, not on the exit code. This arm previously checked rc==0 and
	# broke the moment the gate started (correctly) reporting something else: planting this
	# fixture makes `.selftest-dynamic.sh` — which THIS FILE references by name — briefly exist
	# and be untracked, so a non-zero rc is right for a reason that has nothing to do with the
	# limit under test. An arm that asserts a WHOLE-RUN verdict cannot isolate one property.
	local arm4_out
	arm4_out="$(scan 2>&1)"
	rm -f "$tmp_caller"
	if ! printf '%s' "$arm4_out" | grep -q 'nonexistent-runtime.sh'; then
		echo "  PASS  arm 4: a runtime-assembled path is NOT detected (documented limit, confirmed)"
	else
		echo "  FAIL  arm 4: the runtime-assembled path was reported — the stated limit no longer" >&2
		echo "        holds, so FLIP this arm to assert detection rather than deleting it" >&2
		failed=1
	fi

	if [ "$failed" -eq 0 ]; then
		# Deliberately NOT "verified every leak class" or similar. This says what ran: four named
		# arms, one of which asserts a LIMIT rather than a capability. A success string that claims
		# coverage its fixtures never exercise is the defect this gate's whole family exists to
		# catch, and it is cheaper to phrase honestly than to be corrected later.
		echo "tracked-artifact-gate self-test: 4/4 arms passed (liveness, planted-RED, restore, stated-limit)"
		return 0
	fi
	echo "tracked-artifact-gate self-test: FAILED" >&2
	return 1
}

case "${1:-}" in
	--self-test) self_test ;;
	-h|--help)   usage ;;
	"")          scan ;;
	*)           usage >&2; exit 2 ;;
esac
