#!/usr/bin/env bash
# changelog-tag-coverage-gate — every published release tag must have a CHANGELOG section.
#
# A tag is the moment a version becomes something a consumer can install. The changelog is where that
# consumer looks to find out what is in it. Nothing linked the two, so `v10.0.0-alpha.11` was published
# on 2026-09-14 and the file never grew a section for it. The consequence is not a missing heading: the
# entries that shipped in that release stay under `[Unreleased]`, which the file's own preamble defines
# as "landed since the previous pre-release" — so a consumer reading the newest documented section sees
# a release they are running described as not yet released, and a Known Issues entry saying a defect
# "is not yet fixed" keeps saying it after the fix has shipped in the version they installed.
#
# THE PREDICATE IS THE REQUIREMENT, NOT AN ENUMERATION. It is derived from the tags that exist, so a
# NEW release is covered the moment it is tagged — there is no list to remember to update. The sibling
# Oracle gate in this directory records what happens when a gate scopes itself by what a file happens
# to contain: the population it was meant to protect fell outside the predicate by construction and the
# gate stayed green across all of it.
#
# THE FLOOR IS DELIBERATE AND IS THE FILE'S OWN POLICY. CHANGELOG.md states: "Releases earlier than
# 10.0.0-alpha.8 are not documented individually". Tags below that floor are therefore not a defect and
# are not reported. The floor cannot go stale in the dangerous direction: a newly published release is
# always above it, which is the case this gate exists to catch.
#
# NOT A COMMIT-PATH GATE. It is shell plus a `git tag` read, it runs in CI only, and it must never be
# wired into a hook — no gate may slow down a commit.
#
# Exit codes:
#   0  every release tag at or above the floor has a matching CHANGELOG section
#   1  at least one published release is undocumented (gate fail)
#   2  usage / environment error, including "no tags found" — a tagless checkout REFUSES rather
#      than reporting the vacuous pass that an empty loop would otherwise produce
#   3  --self-test failed (the gate itself is broken or vacuous)
#
# Usage:
#   changelog-tag-coverage-gate.sh
#   changelog-tag-coverage-gate.sh --self-test
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/gate-denominator.sh"

readonly E_PASS=0
readonly E_FAIL=1
readonly E_ENV=2
readonly E_SELFTEST=3

# The oldest release the file documents individually. See the header.
FLOOR="${CHANGELOG_TAG_GATE_FLOOR:-10.0.0-alpha.8}"

# Sort two version strings and report whether $1 is at or above $2.
at_or_above_floor() {
	local v="$1" floor="$2" oldest
	oldest="$(printf '%s\n%s\n' "$v" "$floor" | sort -V | head -n1)"
	[ "$oldest" = "$floor" ]
}

# A section heading is a line that STARTS with `## [<version>]`. A version mentioned in prose, inside a
# code fence, or as a link reference is not a section and must not satisfy the requirement — that is the
# difference between "the file talks about the release" and "the file documents it".
#
# FENCED BLOCKS ARE STRIPPED FIRST, and the gate's own self-test is why. A fenced sample that shows what
# a section looks like begins at column zero exactly like the real thing, so a naive line match accepts
# documentation ABOUT the format as though it were the format. Arm 4 caught this in the first run of
# this gate; without it, a changelog could satisfy the requirement with an illustration.
uncommented() {
	awk '/^[[:space:]]*```/ { fenced = !fenced; next } !fenced { print }' "$1"
}

# NOTE THE ABSENCE OF `grep -q`, WHICH IS LOAD-BEARING UNDER `set -o pipefail`. `-q` exits at the first
# match, the writer upstream takes SIGPIPE, and pipefail turns that into a non-zero pipeline — so a
# section that EXISTS reports as missing. It is size-dependent: on a small file the writer finishes
# before the reader leaves, so the bug is invisible. This gate's own self-test passed while the real
# 2,699-line file reported all four releases undocumented.
has_section() {
	local version="$1" changelog="$2"
	uncommented "$changelog" | grep -E "^## \[${version//./\\.}\]" >/dev/null
}

scan() {
	local changelog="${CHANGELOG_TAG_GATE_FILE:-$REPO_ROOT/CHANGELOG.md}"
	local tags_src="${CHANGELOG_TAG_GATE_TAGS:-}"
	local -a tags=()
	local missing=0 checked=0 skipped=0 tag version

	if [ ! -f "$changelog" ]; then
		echo "[changelog-tag-coverage] CANNOT EVALUATE — no changelog at $changelog" >&2
		return "$E_ENV"
	fi

	if [ -n "$tags_src" ]; then
		mapfile -t tags < <(printf '%s\n' "$tags_src" | tr ' ' '\n' | grep -v '^$')
	else
		mapfile -t tags < <(git -C "$REPO_ROOT" tag --list 'v[0-9]*' 2>/dev/null)
	fi

	if [ "${#tags[@]}" -eq 0 ]; then
		# REFUSE, never PASS. An empty tag list makes the loop below vacuous, and a vacuous loop
		# reports the same 0 as a healthy run over a fully-documented file.
		echo "[changelog-tag-coverage] CANNOT EVALUATE — no release tags found (shallow clone, or fetch-depth without tags)." >&2
		return "$E_ENV"
	fi

	for tag in "${tags[@]}"; do
		version="${tag#v}"
		if ! at_or_above_floor "$version" "$FLOOR"; then
			skipped=$((skipped + 1))
			continue
		fi
		checked=$((checked + 1))
		if ! has_section "$version" "$changelog"; then
			echo "::error::published release $tag has no '## [$version]' section in CHANGELOG.md — everything it shipped still reads as [Unreleased]" >&2
			missing=$((missing + 1))
		fi
	done

	# Report the denominator BEFORE the verdict, so "0 undocumented" is never read as a green that
	# examined nothing. The may-be-empty variant is correct HERE and the waiver is deliberate: a broken
	# tag enumeration is already a REFUSE above ("no release tags found"), so a zero at this point can
	# only mean every tag sits below the documented floor -- a legitimately empty population, not a
	# matcher that stopped matching.
	gate_denominator_may_be_empty "$checked" "release tag(s) at or above $FLOOR"
	echo "[changelog-tag-coverage] checked $checked tag(s) at or above $FLOOR; $skipped below the floor; $missing undocumented."
	[ "$missing" -eq 0 ] && return "$E_PASS"
	return "$E_FAIL"
}

self_test() {
	local tmp status rc=0
	tmp="$(mktemp -d)"

	# ARM 1 — LIVENESS: a published tag with no section must FAIL. This is the arm that would have
	# caught 2026-09-14, and a gate that cannot produce it is not a check.
	printf '## [10.0.0-alpha.10] - 2026-09-05\n\ncontent\n' > "$tmp/missing.md"
	CHANGELOG_TAG_GATE_FILE="$tmp/missing.md" CHANGELOG_TAG_GATE_TAGS="v10.0.0-alpha.11" scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne "$E_FAIL" ]; then
		echo "self-test FAIL: an undocumented published tag was not reported (got exit $status, wanted $E_FAIL)." >&2
		rc=1
	fi

	# ARM 2 — SAFETY: a fully documented file must PASS, so the gate is not simply always-red.
	printf '## [10.0.0-alpha.11] - 2026-09-14\n\ncontent\n' > "$tmp/present.md"
	CHANGELOG_TAG_GATE_FILE="$tmp/present.md" CHANGELOG_TAG_GATE_TAGS="v10.0.0-alpha.11" scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne "$E_PASS" ]; then
		echo "self-test FAIL: a documented release was reported as missing (got exit $status, wanted $E_PASS)." >&2
		rc=1
	fi

	# ARM 3 — NON-VACUITY: no tags must REFUSE, not pass. A shallow clone must not be able to
	# manufacture a green by giving the loop nothing to iterate.
	CHANGELOG_TAG_GATE_FILE="$tmp/present.md" CHANGELOG_TAG_GATE_TAGS=" " scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne "$E_ENV" ]; then
		echo "self-test FAIL: a tagless checkout did not REFUSE (got exit $status, wanted $E_ENV)." >&2
		rc=1
	fi

	# ARM 4 — THE PREDICATE IS A HEADING, NOT A MENTION. A version named in prose or inside a fence
	# must not satisfy the requirement; otherwise the changelog's own narrative text about a release
	# silently discharges the obligation to document it.
	printf '## [Unreleased]\n\nSee 10.0.0-alpha.11 for details.\n\n```\n## [10.0.0-alpha.11]\n```\n' > "$tmp/mention.md"
	CHANGELOG_TAG_GATE_FILE="$tmp/mention.md" CHANGELOG_TAG_GATE_TAGS="v10.0.0-alpha.11" scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne "$E_FAIL" ]; then
		echo "self-test FAIL: a prose mention was accepted as a section (got exit $status, wanted $E_FAIL)." >&2
		rc=1
	fi

	# ARM 5 — THE FLOOR EXCLUDES, AND ONLY BELOW ITSELF. A tag below the floor is not a defect; a tag
	# above it is. Both directions, so the floor cannot be widened into a blanket exemption unnoticed.
	CHANGELOG_TAG_GATE_FILE="$tmp/present.md" CHANGELOG_TAG_GATE_TAGS="v3.0.0-alpha" scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne "$E_PASS" ]; then
		echo "self-test FAIL: a tag below the documented floor was reported (got exit $status, wanted $E_PASS)." >&2
		rc=1
	fi

	# ARM 6 — SCALE. Arms 1-5 all use three-line fixtures, and a three-line fixture cannot reproduce a
	# reader that exits before the writer finishes. That is not hypothetical: the first version of this
	# gate piped through `grep -q` under `set -o pipefail`, which made every EXISTING section report as
	# missing on the real file while all five small-fixture arms passed. A self-test whose inputs cannot
	# produce the failing shape is the vacuity this repository keeps paying for, so this arm asserts the
	# safety property at a size where early reader exit actually happens.
	{
		printf '## [10.0.0-alpha.11] - 2026-09-14\n\n'
		for _ in $(seq 1 3000); do printf 'filler line so the reader can exit before the writer does.\n'; done
	} > "$tmp/large.md"
	CHANGELOG_TAG_GATE_FILE="$tmp/large.md" CHANGELOG_TAG_GATE_TAGS="v10.0.0-alpha.11" scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne "$E_PASS" ]; then
		echo "self-test FAIL: a documented release in a LARGE file was reported as missing (got exit $status, wanted $E_PASS) — early reader exit is being treated as absence." >&2
		rc=1
	fi

	rm -rf "$tmp"

	if [ "$rc" -eq 0 ]; then
		echo "[changelog-tag-coverage] self-test PASSED — 6 arms, liveness and safety both proven."
		return "$E_PASS"
	fi
	return "$E_SELFTEST"
}

main() {
	case "${1:-}" in
		--self-test) self_test ;;
		"") scan ;;
		*) echo "usage: $(basename "$0") [--self-test]" >&2; return "$E_ENV" ;;
	esac
}

main "$@"
exit $?
