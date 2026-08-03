#!/usr/bin/env bash
# actionlint-gate.sh — the workflow YAML must lint clean.
#
# WHY THIS EXISTS
#   Eight defects sat in committed workflows and GitHub reported none of them, because none of them
#   stop a run:
#
#     release.yml   post-release read `needs.release-quality-gates.result` without the job in `needs`,
#                   so the property was undefined, the comparison was always false, and the summary
#                   printed "Quality Gates | Failed" on every SUCCESSFUL release.
#     ci.yml   x6   the composite action declared `checkout-ref` required:true while all six call
#                   sites omitted it -- an input advertised as mandatory that nothing ever passed.
#     ci.yml        a bool handed to an input the reusable workflow types as `string`.
#
#   Every one is invisible to a green run. A workflow that lints dirty still executes, so "CI passed"
#   never contradicted them. Only a linter does.
#
# WHAT IT CHECKS
#   actionlint over the repository's workflows. Depends ONLY on .github/**, which is in the mirrored
#   set -- so this gate is live on the mirror, where the Actions actually run. A gate that needs a
#   non-mirrored path would be inert exactly where it matters.
#
# Exit codes (the orphan-test-project-gate.sh / spa-gate.sh contract):
#   0  clean: actionlint reported nothing
#   1  the property is FALSE: actionlint reported at least one finding
#   2  the property could not be EVALUATED (no actionlint binary, no workflows dir) -- REFUSE.
#      REFUSE IS NEVER A PASS. A missing linter must not read as "clean"; that is the failure mode
#      this gate exists to prevent, reproduced one level up.
#
# Overridable for the self-test:
#   ACTIONLINT     path to the binary (default: first of $ACTIONLINT, PATH, ~/go/bin)
#   WORKFLOW_ROOT  directory to lint (default: <repo>/.github/workflows)
set -uo pipefail

die_refuse() { printf 'actionlint-gate: REFUSE — %s\n' "$1" >&2; exit 2; }

find_actionlint() {
	if [ -n "${ACTIONLINT:-}" ]; then
		[ -x "$ACTIONLINT" ] && { printf '%s' "$ACTIONLINT"; return 0; }
		# An explicitly-pointed binary that is not executable is a misconfiguration, not a
		# reason to silently fall back to some other actionlint.
		return 1
	fi
	local c
	for c in actionlint "$HOME/go/bin/actionlint" "$HOME/go/bin/actionlint.exe"; do
		if command -v "$c" >/dev/null 2>&1; then printf '%s' "$c"; return 0; fi
		if [ -x "$c" ]; then printf '%s' "$c"; return 0; fi
	done
	return 1
}

run_gate() {
	local root="$1" bin findings
	bin="$(find_actionlint)" || die_refuse "no actionlint binary (set \$ACTIONLINT or install it)"
	[ -d "$root" ] || die_refuse "no workflow directory at $root"

	# Collect real paths rather than globbing inline. An unmatched glob expands to the literal
	# pattern, actionlint then fails to READ that path, and its read error does not match the
	# finding pattern below -- so the gate reported "clean" while never having linted anything.
	# That is the same false-clean this gate exists to catch, and it was in the gate itself.
	local -a files=()
	while IFS= read -r f; do files+=("$f"); done < <(
		find "$root" -maxdepth 1 \( -name '*.yml' -o -name '*.yaml' \) -type f | sort
	)
	[ "${#files[@]}" -gt 0 ] || die_refuse "no workflow files under $root"

	# -oneline keeps one finding per line so the count below is a count of findings, not of the
	# multi-line source excerpts actionlint prints by default.
	findings="$("$bin" -no-color -oneline "${files[@]}" 2>&1 | grep -E ':[0-9]+:[0-9]+:' || true)"

	if [ -n "$findings" ]; then
		printf '%s\n' "$findings" >&2
		printf 'actionlint-gate: FAIL — %d finding(s).\n' "$(printf '%s\n' "$findings" | wc -l)" >&2
		return 1
	fi
	printf 'actionlint-gate: clean across %d workflow file(s) LINTED.\n' "${#files[@]}"
	return 0
}

self_test() {
	local tmp pass=0 fail=0 rc
	tmp="$(mktemp -d)"
	# Deliberately NOT a trap: an EXIT trap is inherited by every ( ... ) subshell below, so each
	# arm would delete the fixtures the next arm needs, and fire again under `set -u` at a point
	# where the variable it references is out of scope.
	mkdir -p "$tmp/clean" "$tmp/dirty"

	cat >"$tmp/clean/ok.yml" <<-'YAML'
		name: ok
		on: push
		permissions: {}
		jobs:
		  a:
		    runs-on: ubuntu-latest
		    steps:
		      - run: echo "${{ github.sha }}"
	YAML

	# The dirty fixture reproduces the release.yml defect in miniature: an expression reading a
	# context property that does not exist. If the gate cannot catch THIS, it could not have
	# caught the one that shipped.
	cat >"$tmp/dirty/bad.yml" <<-'YAML'
		name: bad
		on: push
		permissions: {}
		jobs:
		  a:
		    runs-on: ubuntu-latest
		    steps:
		      - run: echo "${{ github.nosuchcontext }}"
	YAML

	# LIVENESS — a clean tree must PASS. A gate that fails everything is as useless as one that
	# passes everything, and only this arm can tell the two apart.
	if ( WORKFLOW_ROOT="$tmp/clean" run_gate "$tmp/clean" >/dev/null 2>&1 ); then
		echo "  PASS  LIVENESS  a clean workflow tree exits 0"; pass=$((pass + 1))
	else
		echo "  FAIL  LIVENESS  a clean workflow tree did not exit 0"; fail=$((fail + 1))
	fi

	# SAFETY — a known defect must be caught, with exit 1 specifically. The fixture is the
	# release.yml defect in miniature, so a green here means the gate could have caught the
	# one that actually shipped.
	( run_gate "$tmp/dirty" >/dev/null 2>&1 ); rc=$?
	if [ "$rc" -eq 1 ]; then
		echo "  PASS  SAFETY    an undefined-context expression exits 1"; pass=$((pass + 1))
	else
		echo "  FAIL  SAFETY    an undefined-context expression was not caught (exit $rc)"; fail=$((fail + 1))
	fi

	# REFUSE — a missing binary must be distinguishable from clean.
	( ACTIONLINT="$tmp/nonexistent-binary" run_gate "$tmp/clean" >/dev/null 2>&1 ); rc=$?
	if [ "$rc" -eq 2 ]; then
		echo "  PASS  REFUSE    a missing actionlint exits 2, not 0"; pass=$((pass + 1))
	else
		echo "  FAIL  REFUSE    a missing actionlint did not exit 2 (exit $rc)"; fail=$((fail + 1))
	fi

	# REFUSE — a missing workflow directory must not read as "nothing wrong here".
	( run_gate "$tmp/nonexistent-dir" >/dev/null 2>&1 ); rc=$?
	if [ "$rc" -eq 2 ]; then
		echo "  PASS  REFUSE    a missing workflow dir exits 2, not 0"; pass=$((pass + 1))
	else
		echo "  FAIL  REFUSE    a missing workflow dir did not exit 2 (exit $rc)"; fail=$((fail + 1))
	fi

	# REFUSE — an EMPTY workflow directory is the case that produced this gate's own false-clean.
	# Zero files linted must never report as clean.
	mkdir -p "$tmp/empty"
	( run_gate "$tmp/empty" >/dev/null 2>&1 ); rc=$?
	if [ "$rc" -eq 2 ]; then
		echo "  PASS  REFUSE    an empty workflow dir exits 2 (0 linted is not clean)"; pass=$((pass + 1))
	else
		echo "  FAIL  REFUSE    an empty workflow dir did not exit 2 (exit $rc)"; fail=$((fail + 1))
	fi

	rm -rf "$tmp"

	echo
	echo "  $pass passed, $fail failed"
	[ "$fail" -eq 0 ]
}

main() {
	local repo_root
	repo_root="$(git rev-parse --show-toplevel 2>/dev/null)" || repo_root="$(pwd)"

	case "${1:-}" in
		--self-test) self_test; exit $? ;;
		--help | -h)
			echo "usage: actionlint-gate.sh              lint the repository's workflows"
			echo "       actionlint-gate.sh --self-test  prove this guard is non-vacuous"
			echo "exit: 0 PASS · 1 FAIL (findings) · 2 REFUSE (could not evaluate — NOT a pass)"
			exit 0
			;;
	esac

	run_gate "${WORKFLOW_ROOT:-$repo_root/.github/workflows}"
	exit $?
}

main "$@"
