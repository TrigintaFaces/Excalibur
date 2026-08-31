#!/usr/bin/env bash
# oracle-refusal-exit-code-gate — a shipped Oracle script that can REFUSE must exit non-zero when it does.
#
# The Oracle migration scripts signal a refusal with RAISE_APPLICATION_ERROR inside a PL/SQL block.
# SQL*Plus returns exit 0 for that unless the script carries a WHENEVER SQLERROR EXIT FAILURE
# directive, so an unattended runner records a DECLINED migration as applied and runs the next step
# against a database that was never changed. A refusal that exits 0 is indistinguishable from
# success. The Postgres siblings do not have this problem: they carry ON_ERROR_STOP.
#
# The predicate is derived from the script itself — "contains a refusal" implies "must carry the
# directive" — so a NEW script that raises and forgets the directive is detected without anyone
# remembering to add it to a list.
#
# Exit codes:
#   0  every refusing Oracle script exits non-zero on its refusal
#   1  at least one refusing script would exit 0 (gate fail)
#   2  usage / environment error
#   3  --self-test failed (the gate itself is broken or vacuous)
#
# Usage:
#   oracle-refusal-exit-code-gate.sh
#   oracle-refusal-exit-code-gate.sh --self-test
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

# Whole-line comments are stripped first: both the refusal and the directive are only real when the
# server or the client actually sees them, and both words appear in the prose of these files.
uncommented() { sed 's/^[[:space:]]*--.*$//' "$1"; }

scan() {
	local failures=0 scripts=0 refusing=0 script
	local root="${ORACLE_REFUSAL_GATE_ROOT:-$REPO_ROOT}"

	while IFS= read -r script; do
		scripts=$((scripts + 1))
		local body
		body="$(uncommented "$script")"

		grep -qi 'RAISE_APPLICATION_ERROR' <<<"$body" || continue
		refusing=$((refusing + 1))

		if ! grep -qiE '^[[:space:]]*WHENEVER[[:space:]]+SQLERROR[[:space:]]+EXIT[[:space:]]+FAILURE' <<<"$body"; then
			printf '  %s\n' "$script"
			failures=$((failures + 1))
		fi
	done < <(find "$root/src" -type d -name Scripts -path '*Oracle*' -exec find {} -name '*.sql' -type f \; 2>/dev/null | sort)

	if [ "$scripts" -eq 0 ]; then
		echo "oracle-refusal-exit-code-gate: no shipped Oracle scripts found under '$root/src' — refusing to report a vacuous pass." >&2
		return 2
	fi

	echo "EXAMINED: $scripts shipped Oracle script(s); $refusing can refuse."
	return $((failures > 0 ? 1 : 0))
}

self_test() {
	local tmp status rc=0
	tmp="$(mktemp -d)"
	trap 'rm -rf "$tmp"' RETURN

	mkdir -p "$tmp/src/Pkg.Oracle/Scripts"

	# LIVENESS arm: a refusing script with no directive must be REPORTED.
	cat > "$tmp/src/Pkg.Oracle/Scripts/planted-missing.sql" <<'FIXTURE'
BEGIN
  RAISE_APPLICATION_ERROR(-20099, 'planted refusal');
END;
/
FIXTURE
	ORACLE_REFUSAL_GATE_ROOT="$tmp" scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne 1 ]; then
		echo "self-test FAIL: a refusing script with no directive was not reported (got exit $status, wanted 1)." >&2
		rc=3
	fi

	# SAFETY arm: adding the directive must clear it — the gate must not be permanently red.
	cat > "$tmp/src/Pkg.Oracle/Scripts/planted-missing.sql" <<'FIXTURE'
WHENEVER SQLERROR EXIT FAILURE ROLLBACK
BEGIN
  RAISE_APPLICATION_ERROR(-20099, 'planted refusal');
END;
/
FIXTURE
	ORACLE_REFUSAL_GATE_ROOT="$tmp" scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne 0 ]; then
		echo "self-test FAIL: a refusing script WITH the directive was still reported (got exit $status, wanted 0)." >&2
		rc=3
	fi

	# The directive must be seen only where the client sees it — a commented one is not a directive.
	cat > "$tmp/src/Pkg.Oracle/Scripts/planted-missing.sql" <<'FIXTURE'
-- Run this with WHENEVER SQLERROR EXIT FAILURE.
BEGIN
  RAISE_APPLICATION_ERROR(-20099, 'planted refusal');
END;
/
FIXTURE
	ORACLE_REFUSAL_GATE_ROOT="$tmp" scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne 1 ]; then
		echo "self-test FAIL: a COMMENTED directive was accepted as the real thing (got exit $status, wanted 1)." >&2
		rc=3
	fi

	# A script that cannot refuse is out of scope and must not be reported.
	rm "$tmp/src/Pkg.Oracle/Scripts/planted-missing.sql"
	printf 'CREATE TABLE T (ID NUMBER);\n' > "$tmp/src/Pkg.Oracle/Scripts/planted-plain.sql"
	ORACLE_REFUSAL_GATE_ROOT="$tmp" scan >/dev/null 2>&1
	status=$?
	if [ "$status" -ne 0 ]; then
		echo "self-test FAIL: a script with no refusal was reported (got exit $status, wanted 0)." >&2
		rc=3
	fi

	[ "$rc" -eq 0 ] && echo "✅ oracle-refusal-exit-code-gate --self-test: 4/4 arms pass (2 liveness, 2 safety)."
	return "$rc"
}

case "${1:-}" in
	--self-test) self_test; exit $? ;;
	"") ;;
	*) echo "usage: $(basename "$0") [--self-test]" >&2; exit 2 ;;
esac

if scan; then
	echo "✅ oracle-refusal-exit-code-gate: every Oracle script that can refuse exits non-zero when it does."
	exit 0
fi

status=$?
[ "$status" -eq 2 ] && exit 2
cat >&2 <<'MSG'
❌ oracle-refusal-exit-code-gate: the script(s) above raise a refusal that SQL*Plus still exits 0 on.
   A pipeline reads that as success and applies the next migration to an unchanged database.
   Add, before the first statement:  WHENEVER SQLERROR EXIT FAILURE ROLLBACK
MSG
exit 1
